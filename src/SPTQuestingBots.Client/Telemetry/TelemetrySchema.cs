#if UNITY_MONO
using Mono.Data.Sqlite;
#else
using Microsoft.Data.Sqlite;
#endif

namespace SPTQuestingBots.Telemetry;

internal static class TelemetrySchema
{
    internal const int CurrentVersion = 1;

    private const string CreateRaids =
        @"CREATE TABLE IF NOT EXISTS raids (
            raid_id           TEXT PRIMARY KEY,
            map_id            TEXT,
            start_time        TEXT,
            end_time          TEXT,
            escape_time_sec   REAL,
            bot_count         INTEGER,
            player_side       TEXT,
            is_scav_raid      INTEGER,
            mod_version       TEXT,
            config_hash       TEXT
        );";

    private const string CreateBotEvents =
        @"CREATE TABLE IF NOT EXISTS bot_events (
            id              INTEGER PRIMARY KEY AUTOINCREMENT,
            raid_id         TEXT    NOT NULL,
            raid_time       REAL    NOT NULL,
            bot_id          INTEGER NOT NULL,
            bot_profile_id  TEXT,
            bot_name        TEXT,
            bot_role        TEXT,
            event_type      TEXT    NOT NULL,
            detail          TEXT,
            pos_x           REAL,
            pos_y           REAL,
            pos_z           REAL
        );";

    private const string CreateTaskScores =
        @"CREATE TABLE IF NOT EXISTS task_scores (
            id                    INTEGER PRIMARY KEY AUTOINCREMENT,
            raid_id               TEXT    NOT NULL,
            raid_time             REAL    NOT NULL,
            bot_id                INTEGER NOT NULL,
            active_task_id        INTEGER,
            active_task_name      TEXT,
            scores                TEXT,
            personality           TEXT,
            aggression            REAL,
            raid_time_normalized  REAL
        );";

    private const string CreateMovementEvents =
        @"CREATE TABLE IF NOT EXISTS movement_events (
            id          INTEGER PRIMARY KEY AUTOINCREMENT,
            raid_id     TEXT    NOT NULL,
            raid_time   REAL    NOT NULL,
            bot_id      INTEGER NOT NULL,
            event_type  TEXT    NOT NULL,
            pos_x       REAL,
            pos_y       REAL,
            pos_z       REAL,
            dest_x      REAL,
            dest_y      REAL,
            dest_z      REAL,
            speed       REAL,
            detail      TEXT
        );";

    private const string CreateCombatEvents =
        @"CREATE TABLE IF NOT EXISTS combat_events (
            id          INTEGER PRIMARY KEY AUTOINCREMENT,
            raid_id     TEXT    NOT NULL,
            raid_time   REAL    NOT NULL,
            bot_id      INTEGER NOT NULL,
            event_type  TEXT    NOT NULL,
            target_id   INTEGER,
            target_name TEXT,
            weapon      TEXT,
            damage      REAL,
            distance    REAL,
            pos_x       REAL,
            pos_y       REAL,
            pos_z       REAL
        );";

    private const string CreateSquadEvents =
        @"CREATE TABLE IF NOT EXISTS squad_events (
            id          INTEGER PRIMARY KEY AUTOINCREMENT,
            raid_id     TEXT    NOT NULL,
            raid_time   REAL    NOT NULL,
            squad_id    INTEGER NOT NULL,
            bot_id      INTEGER,
            event_type  TEXT    NOT NULL,
            detail      TEXT
        );";

    private const string CreatePerformanceSamples =
        @"CREATE TABLE IF NOT EXISTS performance_samples (
            id                    INTEGER PRIMARY KEY AUTOINCREMENT,
            raid_id               TEXT NOT NULL,
            raid_time             REAL NOT NULL,
            alive_bot_count       INTEGER,
            active_entity_count   INTEGER,
            update_duration_ms    REAL,
            frame_time_ms         REAL,
            memory_mb             REAL,
            queue_depth           INTEGER
        );";

    private const string CreateErrors =
        @"CREATE TABLE IF NOT EXISTS errors (
            id          INTEGER PRIMARY KEY AUTOINCREMENT,
            raid_id     TEXT NOT NULL,
            raid_time   REAL,
            severity    TEXT,
            source      TEXT,
            message     TEXT,
            stack_trace TEXT,
            bot_id      INTEGER,
            detail      TEXT
        );";

    private const string CreateIndexes =
        @"CREATE INDEX IF NOT EXISTS ix_bot_events_raid      ON bot_events(raid_id);
          CREATE INDEX IF NOT EXISTS ix_bot_events_bot        ON bot_events(bot_id);
          CREATE INDEX IF NOT EXISTS ix_bot_events_time       ON bot_events(raid_id, raid_time);

          CREATE INDEX IF NOT EXISTS ix_task_scores_raid      ON task_scores(raid_id);
          CREATE INDEX IF NOT EXISTS ix_task_scores_bot       ON task_scores(bot_id);
          CREATE INDEX IF NOT EXISTS ix_task_scores_time      ON task_scores(raid_id, raid_time);

          CREATE INDEX IF NOT EXISTS ix_movement_events_raid  ON movement_events(raid_id);
          CREATE INDEX IF NOT EXISTS ix_movement_events_bot   ON movement_events(bot_id);
          CREATE INDEX IF NOT EXISTS ix_movement_events_time  ON movement_events(raid_id, raid_time);

          CREATE INDEX IF NOT EXISTS ix_combat_events_raid    ON combat_events(raid_id);
          CREATE INDEX IF NOT EXISTS ix_combat_events_bot     ON combat_events(bot_id);
          CREATE INDEX IF NOT EXISTS ix_combat_events_time    ON combat_events(raid_id, raid_time);

          CREATE INDEX IF NOT EXISTS ix_squad_events_raid     ON squad_events(raid_id);
          CREATE INDEX IF NOT EXISTS ix_squad_events_squad    ON squad_events(squad_id);
          CREATE INDEX IF NOT EXISTS ix_squad_events_time     ON squad_events(raid_id, raid_time);

          CREATE INDEX IF NOT EXISTS ix_perf_samples_raid     ON performance_samples(raid_id);
          CREATE INDEX IF NOT EXISTS ix_perf_samples_time     ON performance_samples(raid_id, raid_time);

          CREATE INDEX IF NOT EXISTS ix_errors_raid           ON errors(raid_id);
          CREATE INDEX IF NOT EXISTS ix_errors_severity       ON errors(severity);";

    internal static void EnsureSchema(SqliteConnection conn)
    {
        var version = GetUserVersion(conn);
        if (version >= CurrentVersion)
            return;

        using var tx = conn.BeginTransaction();

        ExecuteNonQuery(conn, CreateRaids);
        ExecuteNonQuery(conn, CreateBotEvents);
        ExecuteNonQuery(conn, CreateTaskScores);
        ExecuteNonQuery(conn, CreateMovementEvents);
        ExecuteNonQuery(conn, CreateCombatEvents);
        ExecuteNonQuery(conn, CreateSquadEvents);
        ExecuteNonQuery(conn, CreatePerformanceSamples);
        ExecuteNonQuery(conn, CreateErrors);

        foreach (var line in CreateIndexes.Split(';'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0)
                ExecuteNonQuery(conn, trimmed);
        }

        SetUserVersion(conn, CurrentVersion);
        tx.Commit();
    }

    private static int GetUserVersion(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        var result = cmd.ExecuteScalar();
        return result is long l ? (int)l : 0;
    }

    private static void SetUserVersion(SqliteConnection conn, int version)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA user_version = {version};";
        cmd.ExecuteNonQuery();
    }

    private static void ExecuteNonQuery(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    #region INSERT statements (used by TelemetryWriter)

    internal const string InsertRaid =
        @"INSERT OR REPLACE INTO raids
            (raid_id, map_id, start_time, end_time, escape_time_sec, bot_count, player_side, is_scav_raid, mod_version, config_hash)
          VALUES
            ($raid_id, $map_id, $start_time, $end_time, $escape_time_sec, $bot_count, $player_side, $is_scav_raid, $mod_version, $config_hash);";

    internal const string InsertBotEvent =
        @"INSERT INTO bot_events
            (raid_id, raid_time, bot_id, bot_profile_id, bot_name, bot_role, event_type, detail, pos_x, pos_y, pos_z)
          VALUES
            ($raid_id, $raid_time, $bot_id, $bot_profile_id, $bot_name, $bot_role, $event_type, $detail, $pos_x, $pos_y, $pos_z);";

    internal const string InsertTaskScore =
        @"INSERT INTO task_scores
            (raid_id, raid_time, bot_id, active_task_id, active_task_name, scores, personality, aggression, raid_time_normalized)
          VALUES
            ($raid_id, $raid_time, $bot_id, $active_task_id, $active_task_name, $scores, $personality, $aggression, $raid_time_normalized);";

    internal const string InsertMovementEvent =
        @"INSERT INTO movement_events
            (raid_id, raid_time, bot_id, event_type, pos_x, pos_y, pos_z, dest_x, dest_y, dest_z, speed, detail)
          VALUES
            ($raid_id, $raid_time, $bot_id, $event_type, $pos_x, $pos_y, $pos_z, $dest_x, $dest_y, $dest_z, $speed, $detail);";

    internal const string InsertCombatEvent =
        @"INSERT INTO combat_events
            (raid_id, raid_time, bot_id, event_type, target_id, target_name, weapon, damage, distance, pos_x, pos_y, pos_z)
          VALUES
            ($raid_id, $raid_time, $bot_id, $event_type, $target_id, $target_name, $weapon, $damage, $distance, $pos_x, $pos_y, $pos_z);";

    internal const string InsertSquadEvent =
        @"INSERT INTO squad_events
            (raid_id, raid_time, squad_id, bot_id, event_type, detail)
          VALUES
            ($raid_id, $raid_time, $squad_id, $bot_id, $event_type, $detail);";

    internal const string InsertPerformanceSample =
        @"INSERT INTO performance_samples
            (raid_id, raid_time, alive_bot_count, active_entity_count, update_duration_ms, frame_time_ms, memory_mb, queue_depth)
          VALUES
            ($raid_id, $raid_time, $alive_bot_count, $active_entity_count, $update_duration_ms, $frame_time_ms, $memory_mb, $queue_depth);";

    internal const string InsertError =
        @"INSERT INTO errors
            (raid_id, raid_time, severity, source, message, stack_trace, bot_id, detail)
          VALUES
            ($raid_id, $raid_time, $severity, $source, $message, $stack_trace, $bot_id, $detail);";

    #endregion
}
