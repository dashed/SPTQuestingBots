using System;
using System.IO;
using System.Threading;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using SPTQuestingBots.Telemetry;

namespace SPTQuestingBots.Client.Tests.Telemetry;

[TestFixture]
public class TelemetryRecorderTests
{
    private string _dbPath;

    [SetUp]
    public void SetUp()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), "qb_recorder_" + Guid.NewGuid().ToString("N") + ".db");
    }

    [TearDown]
    public void TearDown()
    {
        TelemetryRecorder.Shutdown();
        Thread.Sleep(50);
        CleanupFile(_dbPath);
    }

    // ── Enabled / Disabled ──────────────────────────────────────

    [Test]
    public void Initialize_WhenDisabled_IsEnabledReturnsFalse()
    {
        var config = MakeConfig(enabled: false);
        TelemetryRecorder.Initialize(config, "r1", "factory4_day", 1200f, "Bear", false, "1.0.0");

        Assert.That(TelemetryRecorder.IsEnabled, Is.False);
    }

    [Test]
    public void Initialize_WhenEnabled_IsEnabledReturnsTrue()
    {
        var config = MakeConfig(enabled: true);
        TelemetryRecorder.Initialize(config, "r2", "factory4_day", 1200f, "Bear", false, "1.0.0");

        Assert.That(TelemetryRecorder.IsEnabled, Is.True);
    }

    [Test]
    public void Shutdown_SetsIsEnabledToFalse()
    {
        var config = MakeConfig(enabled: true);
        TelemetryRecorder.Initialize(config, "r3", "factory4_day", 1200f, "Bear", false, "1.0.0");

        TelemetryRecorder.Shutdown();

        Assert.That(TelemetryRecorder.IsEnabled, Is.False);
    }

    // ── Record methods when disabled ────────────────────────────

    [Test]
    public void RecordBotEvent_WhenDisabled_DoesNotThrow()
    {
        var config = MakeConfig(enabled: false);
        TelemetryRecorder.Initialize(config, "r4", "factory4_day", 1200f, "Bear", false, "1.0.0");

        Assert.DoesNotThrow(() => TelemetryRecorder.RecordBotEvent(10f, 1, "p1", "Bot", "pmc", "spawn", null, 0f, 0f, 0f));
    }

    [Test]
    public void RecordBotEvent_WhenDisabled_WritesNoRecords()
    {
        var config = MakeConfig(enabled: false);
        TelemetryRecorder.Initialize(config, "r5", "factory4_day", 1200f, "Bear", false, "1.0.0");

        TelemetryRecorder.RecordBotEvent(10f, 1, "p1", "Bot", "pmc", "spawn", null, 0f, 0f, 0f);

        Assert.That(File.Exists(_dbPath), Is.False);
    }

    // ── BotEvent recording ──────────────────────────────────────

    [Test]
    public void RecordBotEvent_WhenEnabled_WritesToDb()
    {
        InitEnabled("r-bot-1");

        TelemetryRecorder.RecordBotEvent(5f, 1, "profile-1", "Reshala", "boss", "spawn", null, 100f, 0f, 200f);
        FlushAndShutdown();

        Assert.That(QueryCount("bot_events"), Is.EqualTo(1));
    }

    // ── Sub-toggle gating ───────────────────────────────────────

    [Test]
    public void RecordTaskScores_WhenSubToggleDisabled_WritesNothing()
    {
        var config = MakeConfig(enabled: true);
        config.RecordTaskScores = false;
        TelemetryRecorder.Initialize(config, "r-sub-ts", "factory4_day", 1200f, "Bear", false, "1.0.0");

        TelemetryRecorder.RecordTaskScores(10f, 1, 0, "GoTo", "{}", "Normal", 0.5f, 0.2f);
        FlushAndShutdown();

        Assert.That(QueryCount("task_scores"), Is.EqualTo(0));
    }

    [Test]
    public void RecordMovement_WhenSubToggleDisabled_WritesNothing()
    {
        var config = MakeConfig(enabled: true);
        config.RecordMovement = false;
        TelemetryRecorder.Initialize(config, "r-sub-mv", "factory4_day", 1200f, "Bear", false, "1.0.0");

        TelemetryRecorder.RecordMovement(10f, 1, "position", 0f, 0f, 0f, 1f, 1f, 1f, 3f, null);
        FlushAndShutdown();

        Assert.That(QueryCount("movement_events"), Is.EqualTo(0));
    }

    [Test]
    public void RecordCombatEvent_WhenSubToggleDisabled_WritesNothing()
    {
        var config = MakeConfig(enabled: true);
        config.RecordCombat = false;
        TelemetryRecorder.Initialize(config, "r-sub-cb", "factory4_day", 1200f, "Bear", false, "1.0.0");

        TelemetryRecorder.RecordCombatEvent(10f, 1, "kill", 2, "Enemy", "AK-74N", 120f, 50f, 0f, 0f, 0f);
        FlushAndShutdown();

        Assert.That(QueryCount("combat_events"), Is.EqualTo(0));
    }

    [Test]
    public void RecordSquadEvent_WhenSubToggleDisabled_WritesNothing()
    {
        var config = MakeConfig(enabled: true);
        config.RecordSquad = false;
        TelemetryRecorder.Initialize(config, "r-sub-sq", "factory4_day", 1200f, "Bear", false, "1.0.0");

        TelemetryRecorder.RecordSquadEvent(10f, 1, 2, "formation_change", "Column");
        FlushAndShutdown();

        Assert.That(QueryCount("squad_events"), Is.EqualTo(0));
    }

    [Test]
    public void RecordPerformance_WhenSubToggleDisabled_WritesNothing()
    {
        var config = MakeConfig(enabled: true);
        config.RecordPerformance = false;
        TelemetryRecorder.Initialize(config, "r-sub-pf", "factory4_day", 1200f, "Bear", false, "1.0.0");

        TelemetryRecorder.RecordPerformance(10f, 12, 10, 2.5f, 16.6f, 1024f);
        FlushAndShutdown();

        Assert.That(QueryCount("performance_samples"), Is.EqualTo(0));
    }

    [Test]
    public void RecordError_WhenSubToggleDisabled_WritesNothing()
    {
        var config = MakeConfig(enabled: true);
        config.RecordErrors = false;
        TelemetryRecorder.Initialize(config, "r-sub-er", "factory4_day", 1200f, "Bear", false, "1.0.0");

        TelemetryRecorder.RecordError(10f, "error", "Source", "msg", "stack", 0);
        FlushAndShutdown();

        Assert.That(QueryCount("errors"), Is.EqualTo(0));
    }

    // ── Sampling interval enforcement ───────────────────────────

    [Test]
    public void RecordTaskScores_EnforcesSamplingInterval()
    {
        var config = MakeConfig(enabled: true);
        config.TaskScoreSampleIntervalSec = 2.0f;
        TelemetryRecorder.Initialize(config, "r-samp-ts", "factory4_day", 1200f, "Bear", false, "1.0.0");

        // First call at t=0 should record
        TelemetryRecorder.RecordTaskScores(0f, 1, 0, "GoTo", "{}", "Normal", 0.5f, 0.0f);
        // Second call at t=1 (within interval) should be dropped
        TelemetryRecorder.RecordTaskScores(1f, 1, 0, "GoTo", "{}", "Normal", 0.5f, 0.1f);
        // Third call at t=2.5 (after interval) should record
        TelemetryRecorder.RecordTaskScores(2.5f, 1, 0, "GoTo", "{}", "Normal", 0.5f, 0.2f);

        FlushAndShutdown();

        Assert.That(QueryCount("task_scores"), Is.EqualTo(2));
    }

    [Test]
    public void RecordMovement_EnforcesSamplingInterval()
    {
        var config = MakeConfig(enabled: true);
        config.MovementSampleIntervalSec = 1.0f;
        TelemetryRecorder.Initialize(config, "r-samp-mv", "factory4_day", 1200f, "Bear", false, "1.0.0");

        TelemetryRecorder.RecordMovement(0f, 1, "position", 0f, 0f, 0f, 1f, 1f, 1f, 3f, null);
        TelemetryRecorder.RecordMovement(0.5f, 1, "position", 0f, 0f, 0f, 1f, 1f, 1f, 3f, null);
        TelemetryRecorder.RecordMovement(1.5f, 1, "position", 0f, 0f, 0f, 1f, 1f, 1f, 3f, null);

        FlushAndShutdown();

        Assert.That(QueryCount("movement_events"), Is.EqualTo(2));
    }

    [Test]
    public void RecordPerformance_EnforcesSamplingInterval()
    {
        var config = MakeConfig(enabled: true);
        config.PerformanceSampleIntervalSec = 5.0f;
        TelemetryRecorder.Initialize(config, "r-samp-pf", "factory4_day", 1200f, "Bear", false, "1.0.0");

        TelemetryRecorder.RecordPerformance(0f, 10, 8, 1f, 16f, 512f);
        TelemetryRecorder.RecordPerformance(3f, 10, 8, 1f, 16f, 512f);
        TelemetryRecorder.RecordPerformance(6f, 10, 8, 1f, 16f, 512f);

        FlushAndShutdown();

        Assert.That(QueryCount("performance_samples"), Is.EqualTo(2));
    }

    [Test]
    public void RecordTaskScores_SamplingIsPerBot()
    {
        var config = MakeConfig(enabled: true);
        config.TaskScoreSampleIntervalSec = 2.0f;
        TelemetryRecorder.Initialize(config, "r-perbot", "factory4_day", 1200f, "Bear", false, "1.0.0");

        // Bot 1 at t=0
        TelemetryRecorder.RecordTaskScores(0f, 1, 0, "GoTo", "{}", "Normal", 0.5f, 0.0f);
        // Bot 2 at t=0 — different bot, should also record
        TelemetryRecorder.RecordTaskScores(0f, 2, 0, "GoTo", "{}", "Normal", 0.5f, 0.0f);
        // Bot 1 at t=0.5 — same bot within interval, should be dropped
        TelemetryRecorder.RecordTaskScores(0.5f, 1, 0, "GoTo", "{}", "Normal", 0.5f, 0.0f);

        FlushAndShutdown();

        Assert.That(QueryCount("task_scores"), Is.EqualTo(2));
    }

    // ── All record methods produce correct table types ───────────

    [Test]
    public void AllRecordMethods_WriteToCorrectTables()
    {
        InitEnabled("r-all-tables");

        TelemetryRecorder.RecordBotEvent(1f, 1, "p1", "Bot1", "pmc", "spawn", null, 0f, 0f, 0f);
        TelemetryRecorder.RecordTaskScores(2f, 1, 0, "GoTo", "{}", "Normal", 0.5f, 0.1f);
        TelemetryRecorder.RecordMovement(3f, 1, "position", 0f, 0f, 0f, 1f, 1f, 1f, 3f, null);
        TelemetryRecorder.RecordCombatEvent(4f, 1, "kill", 2, "Enemy", "AK", 100f, 50f, 0f, 0f, 0f);
        TelemetryRecorder.RecordSquadEvent(5f, 1, 1, "formation", "Column");
        TelemetryRecorder.RecordPerformance(6f, 10, 8, 1f, 16f, 512f);
        TelemetryRecorder.RecordError(7f, "warn", "Test", "msg", "stack", 1);

        FlushAndShutdown();

        Assert.Multiple(() =>
        {
            Assert.That(QueryCount("bot_events"), Is.EqualTo(1));
            Assert.That(QueryCount("task_scores"), Is.EqualTo(1));
            Assert.That(QueryCount("movement_events"), Is.EqualTo(1));
            Assert.That(QueryCount("combat_events"), Is.EqualTo(1));
            Assert.That(QueryCount("squad_events"), Is.EqualTo(1));
            Assert.That(QueryCount("performance_samples"), Is.EqualTo(1));
            Assert.That(QueryCount("errors"), Is.EqualTo(1));
        });
    }

    // ── Initialize clears sampling state ─────────────────────────

    [Test]
    public void Initialize_ClearsSamplingDictionaries()
    {
        var config = MakeConfig(enabled: true);
        config.TaskScoreSampleIntervalSec = 100f;
        TelemetryRecorder.Initialize(config, "r-clear-1", "factory4_day", 1200f, "Bear", false, "1.0.0");

        // Record at t=0 — establishes last sample time for bot 1
        TelemetryRecorder.RecordTaskScores(0f, 1, 0, "GoTo", "{}", "Normal", 0.5f, 0.0f);
        TelemetryRecorder.Shutdown();
        Thread.Sleep(50);

        // Re-init should clear sampling state
        CleanupFile(_dbPath);
        _dbPath = Path.Combine(Path.GetTempPath(), "qb_recorder_" + Guid.NewGuid().ToString("N") + ".db");
        config = MakeConfig(enabled: true);
        config.TaskScoreSampleIntervalSec = 100f;
        config.DbPath = _dbPath;
        TelemetryRecorder.Initialize(config, "r-clear-2", "factory4_day", 1200f, "Bear", false, "1.0.0");

        // Bot 1 at t=0 should record again because sampling was reset
        TelemetryRecorder.RecordTaskScores(0f, 1, 0, "GoTo", "{}", "Normal", 0.5f, 0.0f);
        FlushAndShutdown();

        Assert.That(QueryCount("task_scores"), Is.EqualTo(1));
    }

    // ── Helpers ──────────────────────────────────────────────────

    private TelemetryConfig MakeConfig(bool enabled)
    {
        return new TelemetryConfig
        {
            Enabled = enabled,
            DbPath = _dbPath,
            MaxQueueDepth = 10000,
            BatchSize = 500,
            FlushIntervalMs = 100,
            TaskScoreSampleIntervalSec = 2.0f,
            MovementSampleIntervalSec = 1.0f,
            PerformanceSampleIntervalSec = 5.0f,
            RetentionRaids = 20,
            RecordTaskScores = true,
            RecordMovement = true,
            RecordCombat = true,
            RecordSquad = true,
            RecordPerformance = true,
            RecordErrors = true,
        };
    }

    private void InitEnabled(string raidId)
    {
        var config = MakeConfig(enabled: true);
        TelemetryRecorder.Initialize(config, raidId, "factory4_day", 1200f, "Bear", false, "1.0.0");
    }

    private void FlushAndShutdown()
    {
        TelemetryRecorder.Shutdown();
        Thread.Sleep(50);
    }

    private long QueryCount(string table)
    {
        using var conn = new SqliteConnection("Data Source=" + _dbPath + ";Mode=ReadOnly");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM " + table;
        return (long)cmd.ExecuteScalar();
    }

    private static void CleanupFile(string path)
    {
        foreach (string suffix in new[] { "", "-wal", "-shm" })
        {
            string file = path + suffix;
            if (File.Exists(file))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // Best effort
                }
            }
        }
    }
}
