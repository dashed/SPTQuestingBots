using System;
using System.IO;
using System.Threading;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using SPTQuestingBots.Telemetry;

namespace SPTQuestingBots.Client.Tests.Telemetry;

[TestFixture]
public class TelemetryPipelineE2ETests
{
    private string _dbPath;

    [SetUp]
    public void SetUp()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), "qb_e2e_" + Guid.NewGuid().ToString("N") + ".db");
    }

    [TearDown]
    public void TearDown()
    {
        // Always shut down the recorder to reset static state
        TelemetryRecorder.Shutdown();

        Thread.Sleep(50);

        foreach (string suffix in new[] { "", "-wal", "-shm" })
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

    // ── Full lifecycle ─────────────────────────────────────────

    [Test]
    public void FullLifecycle_RecordsAllEventTypes_VerifyInDb()
    {
        InitRecorder("raid-e2e-1", "customs");

        // Record one of each event type
        TelemetryRecorder.RecordBotEvent(10.0f, 1, "p1", "Bot1", "pmc", "spawn", null, 100f, 0f, 200f);
        TelemetryRecorder.RecordTaskScores(15.0f, 1, 3, "Ambush", "{}", "Cautious", 0.3f, 0.25f);
        TelemetryRecorder.RecordMovement(20.0f, 1, "position", 110f, 0f, 210f, 150f, 0f, 250f, 3.5f, null);
        TelemetryRecorder.RecordCombatEvent(25.0f, 1, "kill", 2, "Enemy", "AK-74N", 120f, 45f, 110f, 0f, 210f);
        TelemetryRecorder.RecordSquadEvent(30.0f, 1, 1, "formation_change", "Column");
        TelemetryRecorder.RecordPerformance(35.0f, 12, 10, 2.5f, 16.6f, 1024f);
        TelemetryRecorder.RecordError(40.0f, "warning", "TestSource", "test message", "stack", 1);

        FlushAndShutdown();

        Assert.Multiple(() =>
        {
            Assert.That(QueryCount("raids"), Is.EqualTo(1));
            Assert.That(QueryCount("bot_events"), Is.EqualTo(1));
            Assert.That(QueryCount("task_scores"), Is.EqualTo(1));
            Assert.That(QueryCount("movement_events"), Is.EqualTo(1));
            Assert.That(QueryCount("combat_events"), Is.EqualTo(1));
            Assert.That(QueryCount("squad_events"), Is.EqualTo(1));
            Assert.That(QueryCount("performance_samples"), Is.EqualTo(1));
            Assert.That(QueryCount("errors"), Is.EqualTo(1));
        });
    }

    // ── Multi-raid ─────────────────────────────────────────────

    [Test]
    public void MultiRaid_TwoInitShutdownCycles_BothRaidsInDb()
    {
        // First raid
        InitRecorder("raid-multi-e2e-1", "customs");
        TelemetryRecorder.RecordBotEvent(5.0f, 1, "p1", "Bot1", "pmc", "spawn", null, 0f, 0f, 0f);
        FlushAndShutdown();

        // Second raid (reuse same DB path)
        InitRecorder("raid-multi-e2e-2", "factory4_day");
        TelemetryRecorder.RecordBotEvent(3.0f, 2, "p2", "Bot2", "scav", "spawn", null, 0f, 0f, 0f);
        FlushAndShutdown();

        long raidCount = QueryCount("raids");
        long eventCount = QueryCount("bot_events");

        Assert.Multiple(() =>
        {
            Assert.That(raidCount, Is.EqualTo(2));
            Assert.That(eventCount, Is.EqualTo(2));
        });
    }

    // ── Retention cleanup ──────────────────────────────────────

    [Test]
    public void RetentionCleanup_ExceedsLimit_OnlyRetainedRaidsRemain()
    {
        // Create 5 raids with retention limit of 3
        for (int i = 0; i < 5; i++)
        {
            var config = new TelemetryConfig
            {
                Enabled = true,
                DbPath = _dbPath,
                MaxQueueDepth = 10000,
                BatchSize = 500,
                FlushIntervalMs = 100,
                RetentionRaids = 3,
            };

            TelemetryRecorder.Initialize(config, $"raid-ret-{i}", "customs", 1200f, "Bear", false, "1.13.3");

            TelemetryRecorder.RecordBotEvent((float)i, i, $"p{i}", $"Bot{i}", "pmc", "spawn", null, 0f, 0f, 0f);

            FlushAndShutdown();

            // Small delay to ensure distinct start_time ordering
            Thread.Sleep(20);
        }

        long raidCount = QueryCount("raids");
        Assert.That(raidCount, Is.EqualTo(3));
    }

    // ── Stress test ────────────────────────────────────────────

    [Test]
    public void Stress_EnqueueManyEvents_AllWrittenAfterFlush()
    {
        InitRecorder("raid-stress-1", "customs");

        int eventCount = 10000;
        for (int i = 0; i < eventCount; i++)
        {
            TelemetryRecorder.RecordBotEvent((float)i * 0.001f, i % 20, $"p{i % 20}", $"Bot{i % 20}", "pmc", "spawn", null, 0f, 0f, 0f);
        }

        FlushAndShutdown();

        long count = QueryCount("bot_events");
        Assert.That(count, Is.EqualTo(eventCount));
    }

    // ── Disabled telemetry ─────────────────────────────────────

    [Test]
    public void DisabledTelemetry_NoDbOperations()
    {
        var config = new TelemetryConfig { Enabled = false, DbPath = _dbPath };

        TelemetryRecorder.Initialize(config, "raid-disabled-1", "customs", 1200f, "Bear", false, "1.13.3");

        Assert.That(TelemetryRecorder.IsEnabled, Is.False);

        TelemetryRecorder.RecordBotEvent(10.0f, 1, "p1", "Bot1", "pmc", "spawn", null, 0f, 0f, 0f);
        TelemetryRecorder.RecordCombatEvent(15.0f, 1, "kill", 2, "Enemy", "AK-74N", 120f, 45f, 0f, 0f, 0f);

        TelemetryRecorder.Shutdown();

        Assert.That(File.Exists(_dbPath), Is.False);
    }

    // ── Sub-toggle: combat disabled ────────────────────────────

    [Test]
    public void SubToggle_CombatDisabled_NoCombatEventsRecorded()
    {
        var config = new TelemetryConfig
        {
            Enabled = true,
            DbPath = _dbPath,
            MaxQueueDepth = 10000,
            BatchSize = 500,
            FlushIntervalMs = 100,
            RecordCombat = false,
        };

        TelemetryRecorder.Initialize(config, "raid-subtoggle-1", "customs", 1200f, "Bear", false, "1.13.3");

        TelemetryRecorder.RecordBotEvent(10.0f, 1, "p1", "Bot1", "pmc", "spawn", null, 0f, 0f, 0f);
        TelemetryRecorder.RecordCombatEvent(15.0f, 1, "kill", 2, "Enemy", "AK-74N", 120f, 45f, 0f, 0f, 0f);

        FlushAndShutdown();

        Assert.Multiple(() =>
        {
            Assert.That(QueryCount("bot_events"), Is.EqualTo(1));
            Assert.That(QueryCount("combat_events"), Is.EqualTo(0));
        });
    }

    [Test]
    public void SubToggle_SquadDisabled_NoSquadEventsRecorded()
    {
        var config = new TelemetryConfig
        {
            Enabled = true,
            DbPath = _dbPath,
            MaxQueueDepth = 10000,
            BatchSize = 500,
            FlushIntervalMs = 100,
            RecordSquad = false,
        };

        TelemetryRecorder.Initialize(config, "raid-squad-off-1", "customs", 1200f, "Bear", false, "1.13.3");

        TelemetryRecorder.RecordSquadEvent(10.0f, 1, 1, "formation_change", "Column");
        TelemetryRecorder.RecordBotEvent(12.0f, 1, "p1", "Bot1", "pmc", "spawn", null, 0f, 0f, 0f);

        FlushAndShutdown();

        Assert.Multiple(() =>
        {
            Assert.That(QueryCount("squad_events"), Is.EqualTo(0));
            Assert.That(QueryCount("bot_events"), Is.EqualTo(1));
        });
    }

    // ── Sampling interval ──────────────────────────────────────

    [Test]
    public void SamplingInterval_TaskScores_DeduplicatesRapidRecords()
    {
        var config = new TelemetryConfig
        {
            Enabled = true,
            DbPath = _dbPath,
            MaxQueueDepth = 10000,
            BatchSize = 500,
            FlushIntervalMs = 100,
            TaskScoreSampleIntervalSec = 2.0f,
        };

        TelemetryRecorder.Initialize(config, "raid-sample-1", "customs", 1200f, "Bear", false, "1.13.3");

        // Record task scores for same bot at rapid intervals (0.1s apart) over 5s
        for (int i = 0; i < 50; i++)
        {
            float time = i * 0.1f; // 0.0, 0.1, 0.2, ... 4.9
            TelemetryRecorder.RecordTaskScores(time, 1, 3, "Ambush", "{}", "Cautious", 0.3f, time / 1200f);
        }

        FlushAndShutdown();

        // With 2s interval over 5s of data, expect ~3 samples (at t=0, t=2, t=4)
        long count = QueryCount("task_scores");
        Assert.That(count, Is.InRange(2, 4));
    }

    [Test]
    public void SamplingInterval_Movement_DeduplicatesPerBot()
    {
        var config = new TelemetryConfig
        {
            Enabled = true,
            DbPath = _dbPath,
            MaxQueueDepth = 10000,
            BatchSize = 500,
            FlushIntervalMs = 100,
            MovementSampleIntervalSec = 1.0f,
        };

        TelemetryRecorder.Initialize(config, "raid-mv-sample-1", "customs", 1200f, "Bear", false, "1.13.3");

        // Record movement for 2 bots at 0.1s intervals over 3s
        for (int i = 0; i < 30; i++)
        {
            float time = i * 0.1f;
            TelemetryRecorder.RecordMovement(time, 1, "pos", 0f, 0f, 0f, 0f, 0f, 0f, 3.5f, null);
            TelemetryRecorder.RecordMovement(time, 2, "pos", 0f, 0f, 0f, 0f, 0f, 0f, 3.5f, null);
        }

        FlushAndShutdown();

        // Each bot: ~3-4 samples over 3s with 1s interval. Two bots = ~6-8 total.
        long count = QueryCount("movement_events");
        Assert.That(count, Is.InRange(4, 10));
    }

    // ── Error resilience ───────────────────────────────────────

    [Test]
    public void ErrorResilience_RecordAfterShutdown_DoesNotThrow()
    {
        InitRecorder("raid-resilience-1", "customs");
        FlushAndShutdown();

        // These should be no-ops, not exceptions
        Assert.DoesNotThrow(() =>
        {
            TelemetryRecorder.RecordBotEvent(10f, 1, "p", "B", "pmc", "spawn", null, 0f, 0f, 0f);
            TelemetryRecorder.RecordCombatEvent(15f, 1, "kill", 2, "E", "AK", 120f, 45f, 0f, 0f, 0f);
            TelemetryRecorder.RecordPerformance(20f, 10, 8, 2.0f, 16f, 1024f);
            TelemetryRecorder.RecordError(25f, "error", "Test", "msg", "stack", 1);
            TelemetryRecorder.UpdateRaidEnd(5);
        });
    }

    [Test]
    public void ErrorResilience_DoubleShutdown_DoesNotThrow()
    {
        InitRecorder("raid-dblshut-1", "customs");

        Assert.DoesNotThrow(() =>
        {
            TelemetryRecorder.Shutdown();
            TelemetryRecorder.Shutdown();
        });
    }

    [Test]
    public void ErrorResilience_InitWithoutPriorShutdown_Works()
    {
        InitRecorder("raid-reinit-1", "customs");
        TelemetryRecorder.RecordBotEvent(5.0f, 1, "p1", "Bot1", "pmc", "spawn", null, 0f, 0f, 0f);

        // Shutdown and reinitialize to same DB
        FlushAndShutdown();
        InitRecorder("raid-reinit-2", "factory4_day");
        TelemetryRecorder.RecordBotEvent(3.0f, 2, "p2", "Bot2", "scav", "spawn", null, 0f, 0f, 0f);
        FlushAndShutdown();

        long count = QueryCount("bot_events");
        Assert.That(count, Is.EqualTo(2));
    }

    // ── UpdateRaidEnd ──────────────────────────────────────────

    [Test]
    public void UpdateRaidEnd_SetsEndTimeAndBotCount()
    {
        InitRecorder("raid-end-e2e-1", "customs");

        TelemetryRecorder.UpdateRaidEnd(18);
        FlushAndShutdown();

        using var conn = OpenReadConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT end_time, bot_count FROM raids WHERE raid_id = 'raid-end-e2e-1';";
        using var reader = cmd.ExecuteReader();

        Assert.That(reader.Read(), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(reader.IsDBNull(0), Is.False, "end_time should be set");
            Assert.That(reader.GetInt64(1), Is.EqualTo(18));
        });
    }

    // ── Performance sub-toggle ─────────────────────────────────

    [Test]
    public void SubToggle_PerformanceDisabled_NoPerformanceSamples()
    {
        var config = new TelemetryConfig
        {
            Enabled = true,
            DbPath = _dbPath,
            MaxQueueDepth = 10000,
            BatchSize = 500,
            FlushIntervalMs = 100,
            RecordPerformance = false,
        };

        TelemetryRecorder.Initialize(config, "raid-perf-off-1", "customs", 1200f, "Bear", false, "1.13.3");

        TelemetryRecorder.RecordPerformance(60.0f, 12, 10, 2.5f, 16.6f, 1024f);
        TelemetryRecorder.RecordBotEvent(10.0f, 1, "p1", "Bot1", "pmc", "spawn", null, 0f, 0f, 0f);

        FlushAndShutdown();

        Assert.Multiple(() =>
        {
            Assert.That(QueryCount("performance_samples"), Is.EqualTo(0));
            Assert.That(QueryCount("bot_events"), Is.EqualTo(1));
        });
    }

    // ── Data integrity ─────────────────────────────────────────

    [Test]
    public void DataIntegrity_BotEventFieldsPreserved()
    {
        InitRecorder("raid-integrity-1", "customs");

        TelemetryRecorder.RecordBotEvent(42.5f, 7, "profile-abc", "TestBot", "boss", "death", "killed by player", 123.4f, 5.6f, 789.0f);

        FlushAndShutdown();

        using var conn = OpenReadConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT raid_id, raid_time, bot_id, bot_profile_id, bot_name, bot_role, event_type, detail, pos_x, pos_y, pos_z FROM bot_events LIMIT 1;";
        using var reader = cmd.ExecuteReader();

        Assert.That(reader.Read(), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(reader.GetString(0), Is.EqualTo("raid-integrity-1"));
            Assert.That(reader.GetDouble(1), Is.EqualTo(42.5).Within(0.1));
            Assert.That(reader.GetInt64(2), Is.EqualTo(7));
            Assert.That(reader.GetString(3), Is.EqualTo("profile-abc"));
            Assert.That(reader.GetString(4), Is.EqualTo("TestBot"));
            Assert.That(reader.GetString(5), Is.EqualTo("boss"));
            Assert.That(reader.GetString(6), Is.EqualTo("death"));
            Assert.That(reader.GetString(7), Is.EqualTo("killed by player"));
            Assert.That(reader.GetDouble(8), Is.EqualTo(123.4).Within(0.1));
            Assert.That(reader.GetDouble(9), Is.EqualTo(5.6).Within(0.1));
            Assert.That(reader.GetDouble(10), Is.EqualTo(789.0).Within(0.1));
        });
    }

    [Test]
    public void DataIntegrity_CombatEventFieldsPreserved()
    {
        InitRecorder("raid-combat-int-1", "factory4_day");

        TelemetryRecorder.RecordCombatEvent(55.3f, 2, "kill", 5, "TargetName", "M4A1", 85.5f, 32.1f, 10f, 1f, 20f);

        FlushAndShutdown();

        using var conn = OpenReadConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT bot_id, event_type, target_id, target_name, weapon, damage, distance FROM combat_events LIMIT 1;";
        using var reader = cmd.ExecuteReader();

        Assert.That(reader.Read(), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(reader.GetInt64(0), Is.EqualTo(2));
            Assert.That(reader.GetString(1), Is.EqualTo("kill"));
            Assert.That(reader.GetInt64(2), Is.EqualTo(5));
            Assert.That(reader.GetString(3), Is.EqualTo("TargetName"));
            Assert.That(reader.GetString(4), Is.EqualTo("M4A1"));
            Assert.That(reader.GetDouble(5), Is.EqualTo(85.5).Within(0.1));
            Assert.That(reader.GetDouble(6), Is.EqualTo(32.1).Within(0.1));
        });
    }

    // ── Helpers ────────────────────────────────────────────────

    private void InitRecorder(string raidId, string mapId)
    {
        var config = new TelemetryConfig
        {
            Enabled = true,
            DbPath = _dbPath,
            MaxQueueDepth = 10000,
            BatchSize = 500,
            FlushIntervalMs = 100,
            RetentionRaids = 20,
        };

        TelemetryRecorder.Initialize(config, raidId, mapId, 1200f, "Bear", false, "1.13.3");
    }

    private void FlushAndShutdown()
    {
        TelemetryRecorder.Shutdown();
    }

    private long QueryCount(string table)
    {
        using var conn = OpenReadConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM {table}";
        return (long)cmd.ExecuteScalar();
    }

    private SqliteConnection OpenReadConnection()
    {
        var conn = new SqliteConnection("Data Source=" + _dbPath + ";Mode=ReadOnly");
        conn.Open();
        return conn;
    }
}
