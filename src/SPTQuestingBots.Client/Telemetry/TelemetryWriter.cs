using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Data.Sqlite;
using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.Telemetry;

internal sealed class TelemetryWriter
{
    private SqliteConnection _conn;
    private readonly ConcurrentQueue<TelemetryRecord> _queue = new ConcurrentQueue<TelemetryRecord>();
    private Thread _writerThread;
    private volatile bool _running;
    private readonly ManualResetEventSlim _signal = new ManualResetEventSlim(false);

    private int _maxQueueDepth;
    private int _batchSize;
    private int _flushIntervalMs;

    public bool IsInitialized { get; private set; }
    public int QueueDepth => _queue.Count;

    private static readonly Dictionary<TelemetryTable, string> InsertSql = new Dictionary<TelemetryTable, string>
    {
        { TelemetryTable.Raid, TelemetrySchema.InsertRaid },
        { TelemetryTable.BotEvent, TelemetrySchema.InsertBotEvent },
        { TelemetryTable.TaskScore, TelemetrySchema.InsertTaskScore },
        { TelemetryTable.MovementEvent, TelemetrySchema.InsertMovementEvent },
        { TelemetryTable.CombatEvent, TelemetrySchema.InsertCombatEvent },
        { TelemetryTable.SquadEvent, TelemetrySchema.InsertSquadEvent },
        { TelemetryTable.PerformanceSample, TelemetrySchema.InsertPerformanceSample },
        { TelemetryTable.Error, TelemetrySchema.InsertError },
    };

    private static readonly Dictionary<TelemetryTable, string[]> ParamNames = new Dictionary<TelemetryTable, string[]>
    {
        {
            TelemetryTable.Raid,
            new[]
            {
                "$raid_id",
                "$map_id",
                "$start_time",
                "$end_time",
                "$escape_time_sec",
                "$bot_count",
                "$player_side",
                "$is_scav_raid",
                "$mod_version",
                "$config_hash",
            }
        },
        {
            TelemetryTable.BotEvent,
            new[]
            {
                "$raid_id",
                "$raid_time",
                "$bot_id",
                "$bot_profile_id",
                "$bot_name",
                "$bot_role",
                "$event_type",
                "$detail",
                "$pos_x",
                "$pos_y",
                "$pos_z",
            }
        },
        {
            TelemetryTable.TaskScore,
            new[]
            {
                "$raid_id",
                "$raid_time",
                "$bot_id",
                "$active_task_id",
                "$active_task_name",
                "$scores",
                "$personality",
                "$aggression",
                "$raid_time_normalized",
            }
        },
        {
            TelemetryTable.MovementEvent,
            new[]
            {
                "$raid_id",
                "$raid_time",
                "$bot_id",
                "$event_type",
                "$pos_x",
                "$pos_y",
                "$pos_z",
                "$dest_x",
                "$dest_y",
                "$dest_z",
                "$speed",
                "$detail",
            }
        },
        {
            TelemetryTable.CombatEvent,
            new[]
            {
                "$raid_id",
                "$raid_time",
                "$bot_id",
                "$event_type",
                "$target_id",
                "$target_name",
                "$weapon",
                "$damage",
                "$distance",
                "$pos_x",
                "$pos_y",
                "$pos_z",
            }
        },
        { TelemetryTable.SquadEvent, new[] { "$raid_id", "$raid_time", "$squad_id", "$bot_id", "$event_type", "$detail" } },
        {
            TelemetryTable.PerformanceSample,
            new[]
            {
                "$raid_id",
                "$raid_time",
                "$alive_bot_count",
                "$active_entity_count",
                "$update_duration_ms",
                "$frame_time_ms",
                "$memory_mb",
                "$queue_depth",
            }
        },
        {
            TelemetryTable.Error,
            new[] { "$raid_id", "$raid_time", "$severity", "$source", "$message", "$stack_trace", "$bot_id", "$detail" }
        },
    };

    public void Initialize(
        TelemetryConfig config,
        string raidId,
        string mapId,
        string playerSide,
        bool isScavRaid,
        float escapeTimeSec,
        string modVersion,
        string configHash
    )
    {
        if (IsInitialized)
            return;

        try
        {
            _maxQueueDepth = config.MaxQueueDepth;
            _batchSize = config.BatchSize;
            _flushIntervalMs = config.FlushIntervalMs;

            string dbPath = ResolveDbPath(config.DbPath);
            string dbDir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dbDir) && !Directory.Exists(dbDir))
                Directory.CreateDirectory(dbDir);

            _conn = new SqliteConnection("Data Source=" + dbPath);
            _conn.Open();

            ExecutePragma("PRAGMA journal_mode=WAL;");
            ExecutePragma("PRAGMA synchronous=NORMAL;");

            TelemetrySchema.EnsureSchema(_conn);

            InsertRaidRecord(raidId, mapId, playerSide, isScavRaid, escapeTimeSec, modVersion, configHash);

            _running = true;
            _writerThread = new Thread(DrainLoop) { Name = "QuestingBots-Telemetry", IsBackground = true };
            _writerThread.Start();

            IsInitialized = true;
            LoggingController.LogInfo("[Telemetry] Writer initialized: " + dbPath, alwaysShow: true);
        }
        catch (Exception ex)
        {
            LoggingController.LogError("[Telemetry] Failed to initialize writer: " + ex.Message);
            CloseConnection();
        }
    }

    public void Enqueue(TelemetryRecord record)
    {
        if (!IsInitialized)
            return;

        if (_queue.Count >= _maxQueueDepth)
            return;

        _queue.Enqueue(record);
        _signal.Set();
    }

    public void Flush()
    {
        if (!IsInitialized)
            return;

        _signal.Set();

        int waitMs = 0;
        while (!_queue.IsEmpty && waitMs < 5000)
        {
            Thread.Sleep(10);
            waitMs += 10;
            _signal.Set();
        }
    }

    public void Shutdown()
    {
        if (!IsInitialized)
            return;

        IsInitialized = false;
        _running = false;
        _signal.Set();

        if (_writerThread != null && _writerThread.IsAlive)
            _writerThread.Join(5000);

        CloseConnection();
        LoggingController.LogInfo("[Telemetry] Writer shut down.", alwaysShow: true);
    }

    public void UpdateRaidEnd(string raidId, string endTime, int botCount)
    {
        if (_conn == null)
            return;

        try
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE raids SET end_time = $end_time, bot_count = $bot_count WHERE raid_id = $raid_id;";
            cmd.Parameters.AddWithValue("$raid_id", raidId);
            cmd.Parameters.AddWithValue("$end_time", endTime);
            cmd.Parameters.AddWithValue("$bot_count", botCount);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            LoggingController.LogError("[Telemetry] Failed to update raid end: " + ex.Message);
        }
    }

    public void CleanupOldRaids(int retentionCount)
    {
        if (_conn == null)
            return;

        try
        {
            string subquery = "SELECT raid_id FROM raids ORDER BY start_time DESC LIMIT " + retentionCount;

            string[] tables = new[]
            {
                "bot_events",
                "task_scores",
                "movement_events",
                "combat_events",
                "squad_events",
                "performance_samples",
                "errors",
            };

            using var tx = _conn.BeginTransaction();

            foreach (string table in tables)
            {
                using var cmd = _conn.CreateCommand();
                cmd.CommandText = "DELETE FROM " + table + " WHERE raid_id NOT IN (" + subquery + ");";
                cmd.ExecuteNonQuery();
            }

            using var raidCmd = _conn.CreateCommand();
            raidCmd.CommandText = "DELETE FROM raids WHERE raid_id NOT IN (" + subquery + ");";
            raidCmd.ExecuteNonQuery();

            tx.Commit();

            LoggingController.LogInfo("[Telemetry] Cleaned up raids beyond retention limit of " + retentionCount, alwaysShow: true);
        }
        catch (Exception ex)
        {
            LoggingController.LogError("[Telemetry] Raid cleanup failed: " + ex.Message);
        }
    }

    private void DrainLoop()
    {
        while (_running || !_queue.IsEmpty)
        {
            _signal.Wait(_flushIntervalMs);
            _signal.Reset();

            DrainBatch();
        }

        // Final drain after stop signal
        DrainBatch();
    }

    private void DrainBatch()
    {
        if (_queue.IsEmpty || _conn == null)
            return;

        try
        {
            using var tx = _conn.BeginTransaction();
            int count = 0;

            while (count < _batchSize && _queue.TryDequeue(out var record))
            {
                ExecuteInsert(record);
                count++;
            }

            tx.Commit();
        }
        catch (Exception ex)
        {
            try
            {
                LoggingController.LogError("[Telemetry] Batch write failed: " + ex.Message);
            }
            catch
            {
                // Swallow — logging itself may fail on background thread
            }
        }
    }

    private void ExecuteInsert(TelemetryRecord record)
    {
        if (!InsertSql.TryGetValue(record.Table, out string sql))
            return;
        if (!ParamNames.TryGetValue(record.Table, out string[] names))
            return;

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;

        for (int i = 0; i < names.Length && i < record.Parameters.Length; i++)
            cmd.Parameters.AddWithValue(names[i], record.Parameters[i] ?? DBNull.Value);

        cmd.ExecuteNonQuery();
    }

    private void InsertRaidRecord(
        string raidId,
        string mapId,
        string playerSide,
        bool isScavRaid,
        float escapeTimeSec,
        string modVersion,
        string configHash
    )
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = TelemetrySchema.InsertRaid;
        cmd.Parameters.AddWithValue("$raid_id", raidId);
        cmd.Parameters.AddWithValue("$map_id", mapId);
        cmd.Parameters.AddWithValue("$start_time", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$end_time", DBNull.Value);
        cmd.Parameters.AddWithValue("$escape_time_sec", (double)escapeTimeSec);
        cmd.Parameters.AddWithValue("$bot_count", 0);
        cmd.Parameters.AddWithValue("$player_side", playerSide);
        cmd.Parameters.AddWithValue("$is_scav_raid", isScavRaid ? 1 : 0);
        cmd.Parameters.AddWithValue("$mod_version", modVersion);
        cmd.Parameters.AddWithValue("$config_hash", configHash ?? (object)DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    private void ExecutePragma(string pragma)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = pragma;
        cmd.ExecuteNonQuery();
    }

    private void CloseConnection()
    {
        try
        {
            _conn?.Close();
            _conn?.Dispose();
        }
        catch
        {
            // Swallow
        }
        finally
        {
            _conn = null;
        }
    }

    private static string ResolveDbPath(string configPath)
    {
        if (Path.IsPathRooted(configPath))
            return configPath;

        string modRoot = AppDomain.CurrentDomain.BaseDirectory + ConfigController.ModPathRelative + "/";
        return modRoot + configPath;
    }
}
