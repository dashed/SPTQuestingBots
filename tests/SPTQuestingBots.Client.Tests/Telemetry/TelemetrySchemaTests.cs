using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using SPTQuestingBots.Telemetry;

namespace SPTQuestingBots.Client.Tests.Telemetry;

[TestFixture]
public class TelemetrySchemaTests
{
    private SqliteConnection _conn;

    [SetUp]
    public void SetUp()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
    }

    [TearDown]
    public void TearDown()
    {
        _conn?.Close();
        _conn?.Dispose();
    }

    // ── Table creation ─────────────────────────────────────────

    [Test]
    public void EnsureSchema_CreatesAllEightTables()
    {
        TelemetrySchema.EnsureSchema(_conn);

        var tables = QueryTableNames();

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
            Assert.That(tables, Has.Count.EqualTo(8));
        });
    }

    [Test]
    public void EnsureSchema_IsIdempotent_NoErrorOnSecondCall()
    {
        TelemetrySchema.EnsureSchema(_conn);
        Assert.DoesNotThrow(() => TelemetrySchema.EnsureSchema(_conn));

        var tables = QueryTableNames();
        Assert.That(tables, Has.Count.EqualTo(8));
    }

    // ── Indexes ────────────────────────────────────────────────

    [Test]
    public void EnsureSchema_CreatesAllExpectedIndexes()
    {
        TelemetrySchema.EnsureSchema(_conn);

        var indexes = QueryIndexNames();

        string[] expected = new[]
        {
            "ix_bot_events_raid",
            "ix_bot_events_bot",
            "ix_bot_events_time",
            "ix_task_scores_raid",
            "ix_task_scores_bot",
            "ix_task_scores_time",
            "ix_movement_events_raid",
            "ix_movement_events_bot",
            "ix_movement_events_time",
            "ix_combat_events_raid",
            "ix_combat_events_bot",
            "ix_combat_events_time",
            "ix_squad_events_raid",
            "ix_squad_events_squad",
            "ix_squad_events_time",
            "ix_perf_samples_raid",
            "ix_perf_samples_time",
            "ix_errors_raid",
            "ix_errors_severity",
        };

        Assert.Multiple(() =>
        {
            foreach (string idx in expected)
                Assert.That(indexes, Does.Contain(idx), $"Missing index: {idx}");

            Assert.That(indexes, Has.Count.EqualTo(expected.Length));
        });
    }

    // ── user_version ───────────────────────────────────────────

    [Test]
    public void EnsureSchema_SetsUserVersion()
    {
        TelemetrySchema.EnsureSchema(_conn);

        int version = GetUserVersion();

        Assert.That(version, Is.EqualTo(TelemetrySchema.CurrentVersion));
    }

    [Test]
    public void EnsureSchema_SkipsIfVersionAlreadyCurrent()
    {
        TelemetrySchema.EnsureSchema(_conn);

        // Drop a table to prove schema creation is skipped on second call
        ExecuteNonQuery("DROP TABLE errors;");

        TelemetrySchema.EnsureSchema(_conn);

        // errors table should NOT be recreated because user_version is already current
        var tables = QueryTableNames();
        Assert.That(tables, Does.Not.Contain("errors"));
    }

    // ── Column structure ───────────────────────────────────────

    [Test]
    public void Raids_HasExpectedColumns()
    {
        TelemetrySchema.EnsureSchema(_conn);

        var columns = QueryColumnNames("raids");

        Assert.Multiple(() =>
        {
            Assert.That(columns, Does.Contain("raid_id"));
            Assert.That(columns, Does.Contain("map_id"));
            Assert.That(columns, Does.Contain("start_time"));
            Assert.That(columns, Does.Contain("end_time"));
            Assert.That(columns, Does.Contain("escape_time_sec"));
            Assert.That(columns, Does.Contain("bot_count"));
            Assert.That(columns, Does.Contain("player_side"));
            Assert.That(columns, Does.Contain("is_scav_raid"));
            Assert.That(columns, Does.Contain("mod_version"));
            Assert.That(columns, Does.Contain("config_hash"));
        });
    }

    [Test]
    public void BotEvents_HasExpectedColumns()
    {
        TelemetrySchema.EnsureSchema(_conn);

        var columns = QueryColumnNames("bot_events");

        Assert.Multiple(() =>
        {
            Assert.That(columns, Does.Contain("id"));
            Assert.That(columns, Does.Contain("raid_id"));
            Assert.That(columns, Does.Contain("raid_time"));
            Assert.That(columns, Does.Contain("bot_id"));
            Assert.That(columns, Does.Contain("bot_profile_id"));
            Assert.That(columns, Does.Contain("bot_name"));
            Assert.That(columns, Does.Contain("bot_role"));
            Assert.That(columns, Does.Contain("event_type"));
            Assert.That(columns, Does.Contain("detail"));
            Assert.That(columns, Does.Contain("pos_x"));
            Assert.That(columns, Does.Contain("pos_y"));
            Assert.That(columns, Does.Contain("pos_z"));
        });
    }

    [Test]
    public void CombatEvents_HasExpectedColumns()
    {
        TelemetrySchema.EnsureSchema(_conn);

        var columns = QueryColumnNames("combat_events");

        Assert.Multiple(() =>
        {
            Assert.That(columns, Does.Contain("id"));
            Assert.That(columns, Does.Contain("raid_id"));
            Assert.That(columns, Does.Contain("raid_time"));
            Assert.That(columns, Does.Contain("bot_id"));
            Assert.That(columns, Does.Contain("event_type"));
            Assert.That(columns, Does.Contain("target_id"));
            Assert.That(columns, Does.Contain("target_name"));
            Assert.That(columns, Does.Contain("weapon"));
            Assert.That(columns, Does.Contain("damage"));
            Assert.That(columns, Does.Contain("distance"));
            Assert.That(columns, Does.Contain("pos_x"));
            Assert.That(columns, Does.Contain("pos_y"));
            Assert.That(columns, Does.Contain("pos_z"));
        });
    }

    [Test]
    public void PerformanceSamples_HasExpectedColumns()
    {
        TelemetrySchema.EnsureSchema(_conn);

        var columns = QueryColumnNames("performance_samples");

        Assert.Multiple(() =>
        {
            Assert.That(columns, Does.Contain("id"));
            Assert.That(columns, Does.Contain("raid_id"));
            Assert.That(columns, Does.Contain("raid_time"));
            Assert.That(columns, Does.Contain("alive_bot_count"));
            Assert.That(columns, Does.Contain("active_entity_count"));
            Assert.That(columns, Does.Contain("update_duration_ms"));
            Assert.That(columns, Does.Contain("frame_time_ms"));
            Assert.That(columns, Does.Contain("memory_mb"));
            Assert.That(columns, Does.Contain("queue_depth"));
        });
    }

    // ── INSERT statement validation ────────────────────────────

    [Test]
    public void InsertRaid_CanExecute()
    {
        TelemetrySchema.EnsureSchema(_conn);

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = TelemetrySchema.InsertRaid;
        cmd.Parameters.AddWithValue("$raid_id", "test-raid-1");
        cmd.Parameters.AddWithValue("$map_id", "factory4_day");
        cmd.Parameters.AddWithValue("$start_time", "2026-01-01T00:00:00Z");
        cmd.Parameters.AddWithValue("$end_time", System.DBNull.Value);
        cmd.Parameters.AddWithValue("$escape_time_sec", 1200.0);
        cmd.Parameters.AddWithValue("$bot_count", 0);
        cmd.Parameters.AddWithValue("$player_side", "Bear");
        cmd.Parameters.AddWithValue("$is_scav_raid", 0);
        cmd.Parameters.AddWithValue("$mod_version", "1.13.3");
        cmd.Parameters.AddWithValue("$config_hash", System.DBNull.Value);
        cmd.ExecuteNonQuery();

        Assert.That(QueryScalarLong("SELECT COUNT(*) FROM raids"), Is.EqualTo(1));
    }

    [Test]
    public void InsertBotEvent_CanExecute()
    {
        TelemetrySchema.EnsureSchema(_conn);

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = TelemetrySchema.InsertBotEvent;
        cmd.Parameters.AddWithValue("$raid_id", "test-raid-1");
        cmd.Parameters.AddWithValue("$raid_time", 10.5);
        cmd.Parameters.AddWithValue("$bot_id", 1);
        cmd.Parameters.AddWithValue("$bot_profile_id", "profile-1");
        cmd.Parameters.AddWithValue("$bot_name", "Reshala");
        cmd.Parameters.AddWithValue("$bot_role", "boss");
        cmd.Parameters.AddWithValue("$event_type", "spawn");
        cmd.Parameters.AddWithValue("$detail", System.DBNull.Value);
        cmd.Parameters.AddWithValue("$pos_x", 100.0);
        cmd.Parameters.AddWithValue("$pos_y", 0.0);
        cmd.Parameters.AddWithValue("$pos_z", 200.0);
        cmd.ExecuteNonQuery();

        Assert.That(QueryScalarLong("SELECT COUNT(*) FROM bot_events"), Is.EqualTo(1));
    }

    [Test]
    public void InsertCombatEvent_CanExecute()
    {
        TelemetrySchema.EnsureSchema(_conn);

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = TelemetrySchema.InsertCombatEvent;
        cmd.Parameters.AddWithValue("$raid_id", "test-raid-1");
        cmd.Parameters.AddWithValue("$raid_time", 55.3);
        cmd.Parameters.AddWithValue("$bot_id", 2);
        cmd.Parameters.AddWithValue("$event_type", "kill");
        cmd.Parameters.AddWithValue("$target_id", 5);
        cmd.Parameters.AddWithValue("$target_name", "Player");
        cmd.Parameters.AddWithValue("$weapon", "AK-74N");
        cmd.Parameters.AddWithValue("$damage", 120.0);
        cmd.Parameters.AddWithValue("$distance", 45.2);
        cmd.Parameters.AddWithValue("$pos_x", 10.0);
        cmd.Parameters.AddWithValue("$pos_y", 1.0);
        cmd.Parameters.AddWithValue("$pos_z", 20.0);
        cmd.ExecuteNonQuery();

        Assert.That(QueryScalarLong("SELECT COUNT(*) FROM combat_events"), Is.EqualTo(1));
    }

    [Test]
    public void InsertError_CanExecute()
    {
        TelemetrySchema.EnsureSchema(_conn);

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = TelemetrySchema.InsertError;
        cmd.Parameters.AddWithValue("$raid_id", "test-raid-1");
        cmd.Parameters.AddWithValue("$raid_time", 99.0);
        cmd.Parameters.AddWithValue("$severity", "error");
        cmd.Parameters.AddWithValue("$source", "TelemetryWriter");
        cmd.Parameters.AddWithValue("$message", "test error");
        cmd.Parameters.AddWithValue("$stack_trace", "at Test.Method()");
        cmd.Parameters.AddWithValue("$bot_id", System.DBNull.Value);
        cmd.Parameters.AddWithValue("$detail", System.DBNull.Value);
        cmd.ExecuteNonQuery();

        Assert.That(QueryScalarLong("SELECT COUNT(*) FROM errors"), Is.EqualTo(1));
    }

    // ── Helpers ────────────────────────────────────────────────

    private List<string> QueryTableNames()
    {
        var names = new List<string>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            names.Add(reader.GetString(0));
        return names;
    }

    private List<string> QueryIndexNames()
    {
        var names = new List<string>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='index' AND name LIKE 'ix_%' ORDER BY name;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            names.Add(reader.GetString(0));
        return names;
    }

    private List<string> QueryColumnNames(string table)
    {
        var names = new List<string>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table});";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            names.Add(reader.GetString(1)); // column 1 is "name"
        return names;
    }

    private int GetUserVersion()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        var result = cmd.ExecuteScalar();
        return result is long l ? (int)l : 0;
    }

    private long QueryScalarLong(string sql)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        return (long)cmd.ExecuteScalar();
    }

    private void ExecuteNonQuery(string sql)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
