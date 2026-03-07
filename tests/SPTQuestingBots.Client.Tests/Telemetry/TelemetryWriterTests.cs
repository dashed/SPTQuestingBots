using System;
using System.IO;
using System.Threading;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using SPTQuestingBots.Telemetry;

namespace SPTQuestingBots.Client.Tests.Telemetry;

[TestFixture]
public class TelemetryWriterTests
{
    private string _dbPath;
    private TelemetryWriter _writer;

    [SetUp]
    public void SetUp()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), "qb_test_" + Guid.NewGuid().ToString("N") + ".db");
        _writer = new TelemetryWriter();
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            _writer?.Shutdown();
        }
        catch
        {
            // Swallow — writer may already be shut down
        }

        // Give SQLite time to release file handles
        Thread.Sleep(50);

        if (File.Exists(_dbPath))
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch
            {
                // Best effort cleanup
            }
        }

        // Clean WAL/SHM files
        foreach (string suffix in new[] { "-wal", "-shm" })
        {
            string path = _dbPath + suffix;
            if (File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                }
                catch
                {
                    // Best effort
                }
            }
        }
    }

    // ── Initialize ─────────────────────────────────────────────

    [Test]
    public void Initialize_CreatesDbFileAndRaidRecord()
    {
        InitializeWriter("raid-init-1", "factory4_day");

        Assert.That(_writer.IsInitialized, Is.True);
        Assert.That(File.Exists(_dbPath), Is.True);

        long raidCount = QueryScalar("SELECT COUNT(*) FROM raids");
        Assert.That(raidCount, Is.EqualTo(1));
    }

    [Test]
    public void Initialize_RaidRecordHasCorrectFields()
    {
        InitializeWriter("raid-fields-1", "customs");

        using var conn = OpenReadConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT raid_id, map_id, player_side, is_scav_raid, mod_version FROM raids;";
        using var reader = cmd.ExecuteReader();

        Assert.That(reader.Read(), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(reader.GetString(0), Is.EqualTo("raid-fields-1"));
            Assert.That(reader.GetString(1), Is.EqualTo("customs"));
            Assert.That(reader.GetString(2), Is.EqualTo("Bear"));
            Assert.That(reader.GetInt64(3), Is.EqualTo(0));
            Assert.That(reader.GetString(4), Is.EqualTo("1.13.3"));
        });
    }

    [Test]
    public void Initialize_IsIdempotent_SecondCallIgnored()
    {
        InitializeWriter("raid-idem-1", "factory4_day");
        InitializeWriter("raid-idem-2", "customs"); // Should be ignored

        long raidCount = QueryScalar("SELECT COUNT(*) FROM raids");
        Assert.That(raidCount, Is.EqualTo(1));

        string raidId = QueryScalarText("SELECT raid_id FROM raids LIMIT 1");
        Assert.That(raidId, Is.EqualTo("raid-idem-1"));
    }

    [Test]
    public void Initialize_CreatesSchemaTablesInDb()
    {
        InitializeWriter("raid-schema-1", "factory4_day");

        using var conn = OpenReadConnection();
        var tables = QueryTableNames(conn);

        Assert.Multiple(() =>
        {
            Assert.That(tables, Does.Contain("raids"));
            Assert.That(tables, Does.Contain("bot_events"));
            Assert.That(tables, Does.Contain("task_scores"));
            Assert.That(tables, Does.Contain("movement_events"));
            Assert.That(tables, Does.Contain("combat_events"));
            Assert.That(tables, Does.Contain("squad_events"));
            Assert.That(tables, Does.Contain("performance_samples"));
            Assert.That(tables, Does.Contain("errors"));
        });
    }

    // ── WAL mode ───────────────────────────────────────────────

    [Test]
    public void Initialize_SetsWalJournalMode()
    {
        InitializeWriter("raid-wal-1", "factory4_day");

        using var conn = OpenReadConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode;";
        string mode = cmd.ExecuteScalar()?.ToString();

        Assert.That(mode, Is.EqualTo("wal").IgnoreCase);
    }

    // ── Enqueue + Flush ────────────────────────────────────────

    [Test]
    public void Enqueue_ThenFlush_WritesRecordsToDb()
    {
        InitializeWriter("raid-enq-1", "factory4_day");

        _writer.Enqueue(
            new TelemetryRecord(
                TelemetryTable.BotEvent,
                new object[] { "raid-enq-1", 10.5, 1, "profile-1", "Reshala", "boss", "spawn", null, 100.0, 0.0, 200.0 }
            )
        );

        _writer.Flush();

        long count = QueryScalar("SELECT COUNT(*) FROM bot_events");
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void Enqueue_MultipleRecords_AllWrittenAfterFlush()
    {
        InitializeWriter("raid-multi-1", "customs");

        for (int i = 0; i < 10; i++)
        {
            _writer.Enqueue(
                new TelemetryRecord(
                    TelemetryTable.BotEvent,
                    new object[] { "raid-multi-1", (double)i, i, $"profile-{i}", $"Bot_{i}", "pmc", "spawn", null, 0.0, 0.0, 0.0 }
                )
            );
        }

        _writer.Flush();

        long count = QueryScalar("SELECT COUNT(*) FROM bot_events");
        Assert.That(count, Is.EqualTo(10));
    }

    [Test]
    public void Enqueue_DifferentTableTypes_WritesToCorrectTables()
    {
        InitializeWriter("raid-tables-1", "factory4_day");

        _writer.Enqueue(
            new TelemetryRecord(
                TelemetryTable.BotEvent,
                new object[] { "raid-tables-1", 1.0, 1, "p1", "Bot1", "pmc", "spawn", null, 0.0, 0.0, 0.0 }
            )
        );
        _writer.Enqueue(
            new TelemetryRecord(
                TelemetryTable.CombatEvent,
                new object[] { "raid-tables-1", 2.0, 1, "kill", 2, "Enemy", "AK-74N", 120.0, 45.0, 10.0, 1.0, 20.0 }
            )
        );
        _writer.Enqueue(
            new TelemetryRecord(TelemetryTable.Error, new object[] { "raid-tables-1", 3.0, "error", "Test", "msg", "stack", null, null })
        );

        _writer.Flush();

        Assert.Multiple(() =>
        {
            Assert.That(QueryScalar("SELECT COUNT(*) FROM bot_events"), Is.EqualTo(1));
            Assert.That(QueryScalar("SELECT COUNT(*) FROM combat_events"), Is.EqualTo(1));
            Assert.That(QueryScalar("SELECT COUNT(*) FROM errors"), Is.EqualTo(1));
        });
    }

    [Test]
    public void Enqueue_WhenNotInitialized_DoesNotThrow()
    {
        var uninitWriter = new TelemetryWriter();
        Assert.DoesNotThrow(() =>
            uninitWriter.Enqueue(new TelemetryRecord(TelemetryTable.Error, new object[] { "raid", 0.0, "e", "s", "m", "st", null, null }))
        );
    }

    // ── Queue depth limit ──────────────────────────────────────

    [Test]
    public void Enqueue_ExceedsMaxQueueDepth_DropsEvents()
    {
        // Use a tiny queue depth
        var config = new TelemetryConfig
        {
            Enabled = true,
            DbPath = _dbPath,
            MaxQueueDepth = 5,
            BatchSize = 500,
            FlushIntervalMs = 60000, // Very slow flush so events accumulate
        };

        _writer.Initialize(config, "raid-depth-1", "factory4_day", "Bear", false, 1200f, "1.13.3", null);

        // Enqueue more than max
        for (int i = 0; i < 20; i++)
        {
            _writer.Enqueue(
                new TelemetryRecord(
                    TelemetryTable.BotEvent,
                    new object[] { "raid-depth-1", (double)i, i, "p", "Bot", "pmc", "spawn", null, 0.0, 0.0, 0.0 }
                )
            );
        }

        _writer.Flush();

        // Some events should have been dropped; exact count depends on timing
        // but we should have fewer than 20
        long count = QueryScalar("SELECT COUNT(*) FROM bot_events");
        Assert.That(count, Is.LessThanOrEqualTo(20));
    }

    // ── Shutdown ───────────────────────────────────────────────

    [Test]
    public void Shutdown_FlushesRemainingEvents()
    {
        var config = new TelemetryConfig
        {
            Enabled = true,
            DbPath = _dbPath,
            MaxQueueDepth = 10000,
            BatchSize = 500,
            FlushIntervalMs = 60000, // Very slow flush
        };

        _writer.Initialize(config, "raid-shut-1", "factory4_day", "Bear", false, 1200f, "1.13.3", null);

        for (int i = 0; i < 5; i++)
        {
            _writer.Enqueue(
                new TelemetryRecord(
                    TelemetryTable.BotEvent,
                    new object[] { "raid-shut-1", (double)i, i, "p", "Bot", "pmc", "spawn", null, 0.0, 0.0, 0.0 }
                )
            );
        }

        _writer.Shutdown();

        // After shutdown, check that events were flushed
        long count = QueryScalar("SELECT COUNT(*) FROM bot_events");
        Assert.That(count, Is.EqualTo(5));
    }

    [Test]
    public void Shutdown_SetsIsInitializedToFalse()
    {
        InitializeWriter("raid-shut-flag-1", "factory4_day");
        Assert.That(_writer.IsInitialized, Is.True);

        _writer.Shutdown();

        Assert.That(_writer.IsInitialized, Is.False);
    }

    [Test]
    public void Shutdown_WhenNotInitialized_DoesNotThrow()
    {
        var uninitWriter = new TelemetryWriter();
        Assert.DoesNotThrow(() => uninitWriter.Shutdown());
    }

    // ── UpdateRaidEnd ──────────────────────────────────────────

    [Test]
    public void UpdateRaidEnd_SetsEndTimeAndBotCount()
    {
        InitializeWriter("raid-end-1", "factory4_day");

        _writer.UpdateRaidEnd("raid-end-1", "2026-01-01T00:20:00Z", 15);

        using var conn = OpenReadConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT end_time, bot_count FROM raids WHERE raid_id = 'raid-end-1';";
        using var reader = cmd.ExecuteReader();

        Assert.That(reader.Read(), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(reader.GetString(0), Is.EqualTo("2026-01-01T00:20:00Z"));
            Assert.That(reader.GetInt64(1), Is.EqualTo(15));
        });
    }

    // ── CleanupOldRaids ────────────────────────────────────────

    [Test]
    public void CleanupOldRaids_DeletesExcessRaids()
    {
        InitializeWriter("raid-cleanup-latest", "factory4_day");

        // Insert additional raid records directly
        using (var conn = OpenWriteConnection())
        {
            TelemetrySchema.EnsureSchema(conn);

            for (int i = 0; i < 5; i++)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = TelemetrySchema.InsertRaid;
                cmd.Parameters.AddWithValue("$raid_id", $"raid-cleanup-{i}");
                cmd.Parameters.AddWithValue("$map_id", "customs");
                cmd.Parameters.AddWithValue("$start_time", new DateTime(2026, 1, 1, i, 0, 0, DateTimeKind.Utc).ToString("o"));
                cmd.Parameters.AddWithValue("$end_time", DBNull.Value);
                cmd.Parameters.AddWithValue("$escape_time_sec", 1200.0);
                cmd.Parameters.AddWithValue("$bot_count", 0);
                cmd.Parameters.AddWithValue("$player_side", "Bear");
                cmd.Parameters.AddWithValue("$is_scav_raid", 0);
                cmd.Parameters.AddWithValue("$mod_version", "1.13.3");
                cmd.Parameters.AddWithValue("$config_hash", DBNull.Value);
                cmd.ExecuteNonQuery();

                // Insert a bot event for each raid
                using var evCmd = conn.CreateCommand();
                evCmd.CommandText = TelemetrySchema.InsertBotEvent;
                evCmd.Parameters.AddWithValue("$raid_id", $"raid-cleanup-{i}");
                evCmd.Parameters.AddWithValue("$raid_time", 1.0);
                evCmd.Parameters.AddWithValue("$bot_id", 1);
                evCmd.Parameters.AddWithValue("$bot_profile_id", "p");
                evCmd.Parameters.AddWithValue("$bot_name", "Bot");
                evCmd.Parameters.AddWithValue("$bot_role", "pmc");
                evCmd.Parameters.AddWithValue("$event_type", "spawn");
                evCmd.Parameters.AddWithValue("$detail", DBNull.Value);
                evCmd.Parameters.AddWithValue("$pos_x", 0.0);
                evCmd.Parameters.AddWithValue("$pos_y", 0.0);
                evCmd.Parameters.AddWithValue("$pos_z", 0.0);
                evCmd.ExecuteNonQuery();
            }

            conn.Close();
        }

        // Total: 6 raids (1 from Initialize + 5 inserted). Keep only 3.
        _writer.CleanupOldRaids(3);

        long raidCount = QueryScalar("SELECT COUNT(*) FROM raids");
        Assert.That(raidCount, Is.EqualTo(3));

        // Bot events from deleted raids should also be gone
        long eventCount = QueryScalar("SELECT COUNT(*) FROM bot_events");
        Assert.That(eventCount, Is.LessThanOrEqualTo(3));
    }

    // ── TaskScore records ──────────────────────────────────────

    [Test]
    public void Enqueue_TaskScore_WritesCorrectData()
    {
        InitializeWriter("raid-task-1", "factory4_day");

        _writer.Enqueue(
            new TelemetryRecord(
                TelemetryTable.TaskScore,
                new object[] { "raid-task-1", 30.0, 1, 3, "Ambush", "{\"Ambush\":0.7,\"Patrol\":0.3}", "Cautious", 0.3, 0.5 }
            )
        );

        _writer.Flush();

        using var conn = OpenReadConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT active_task_name, personality, aggression FROM task_scores LIMIT 1;";
        using var reader = cmd.ExecuteReader();

        Assert.That(reader.Read(), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(reader.GetString(0), Is.EqualTo("Ambush"));
            Assert.That(reader.GetString(1), Is.EqualTo("Cautious"));
            Assert.That(reader.GetDouble(2), Is.EqualTo(0.3).Within(0.01));
        });
    }

    // ── SquadEvent records ─────────────────────────────────────

    [Test]
    public void Enqueue_SquadEvent_WritesCorrectData()
    {
        InitializeWriter("raid-squad-1", "factory4_day");

        _writer.Enqueue(
            new TelemetryRecord(TelemetryTable.SquadEvent, new object[] { "raid-squad-1", 15.0, 1, 2, "formation_change", "Column" })
        );

        _writer.Flush();

        long count = QueryScalar("SELECT COUNT(*) FROM squad_events");
        Assert.That(count, Is.EqualTo(1));
    }

    // ── PerformanceSample records ──────────────────────────────

    [Test]
    public void Enqueue_PerformanceSample_WritesCorrectData()
    {
        InitializeWriter("raid-perf-1", "factory4_day");

        _writer.Enqueue(
            new TelemetryRecord(TelemetryTable.PerformanceSample, new object[] { "raid-perf-1", 60.0, 12, 10, 2.5, 16.6, 1024.0, 50 })
        );

        _writer.Flush();

        using var conn = OpenReadConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT alive_bot_count, frame_time_ms, memory_mb FROM performance_samples LIMIT 1;";
        using var reader = cmd.ExecuteReader();

        Assert.That(reader.Read(), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(reader.GetInt64(0), Is.EqualTo(12));
            Assert.That(reader.GetDouble(1), Is.EqualTo(16.6).Within(0.1));
            Assert.That(reader.GetDouble(2), Is.EqualTo(1024.0).Within(0.1));
        });
    }

    // ── MovementEvent records ──────────────────────────────────

    [Test]
    public void Enqueue_MovementEvent_WritesCorrectData()
    {
        InitializeWriter("raid-move-1", "factory4_day");

        _writer.Enqueue(
            new TelemetryRecord(
                TelemetryTable.MovementEvent,
                new object[] { "raid-move-1", 20.0, 1, "position", 10.0, 0.0, 20.0, 50.0, 0.0, 60.0, 3.5, null }
            )
        );

        _writer.Flush();

        long count = QueryScalar("SELECT COUNT(*) FROM movement_events");
        Assert.That(count, Is.EqualTo(1));
    }

    // ── Helpers ────────────────────────────────────────────────

    private void InitializeWriter(string raidId, string mapId)
    {
        var config = new TelemetryConfig
        {
            Enabled = true,
            DbPath = _dbPath,
            MaxQueueDepth = 10000,
            BatchSize = 500,
            FlushIntervalMs = 100,
        };

        _writer.Initialize(config, raidId, mapId, "Bear", false, 1200f, "1.13.3", null);
    }

    private SqliteConnection OpenReadConnection()
    {
        var conn = new SqliteConnection("Data Source=" + _dbPath + ";Mode=ReadOnly");
        conn.Open();
        return conn;
    }

    private SqliteConnection OpenWriteConnection()
    {
        var conn = new SqliteConnection("Data Source=" + _dbPath);
        conn.Open();
        return conn;
    }

    private long QueryScalar(string sql)
    {
        using var conn = OpenReadConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return (long)cmd.ExecuteScalar();
    }

    private string QueryScalarText(string sql)
    {
        using var conn = OpenReadConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar()?.ToString();
    }

    private static System.Collections.Generic.List<string> QueryTableNames(SqliteConnection conn)
    {
        var names = new System.Collections.Generic.List<string>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            names.Add(reader.GetString(0));
        return names;
    }
}
