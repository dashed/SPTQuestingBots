using System;
using System.Collections.Generic;

namespace SPTQuestingBots.Telemetry;

internal static class TelemetryRecorder
{
    private static TelemetryWriter _writer;
    private static TelemetryConfig _config;
    private static string _raidId;
    private static bool _enabled;

    private static readonly Dictionary<int, float> _lastTaskScoreTime = new Dictionary<int, float>();
    private static readonly Dictionary<int, float> _lastMovementTime = new Dictionary<int, float>();
    private static float _lastPerformanceTime;

    public static bool IsEnabled => _enabled;

    public static void Initialize(
        TelemetryConfig config,
        string raidId,
        string mapId,
        float escapeTimeSec,
        string playerSide,
        bool isScavRaid,
        string modVersion
    )
    {
        _config = config;
        _raidId = raidId;
        _lastTaskScoreTime.Clear();
        _lastMovementTime.Clear();
        _lastPerformanceTime = float.MinValue;

        if (!config.Enabled)
        {
            _enabled = false;
            return;
        }

        _writer = new TelemetryWriter();
        _writer.Initialize(config, raidId, mapId, playerSide, isScavRaid, escapeTimeSec, modVersion, null);
        _enabled = _writer.IsInitialized;

        if (_enabled)
        {
            _writer.CleanupOldRaids(config.RetentionRaids);
        }
    }

    public static void Shutdown()
    {
        if (!_enabled || _writer == null)
            return;

        _writer.Flush();
        _writer.Shutdown();
        _writer = null;
        _enabled = false;
        _config = null;
        _raidId = null;
    }

    public static void UpdateRaidEnd(int botCount)
    {
        if (!_enabled || _writer == null || _raidId == null)
            return;

        _writer.UpdateRaidEnd(_raidId, DateTime.UtcNow.ToString("o"), botCount);
    }

    public static void RecordBotEvent(
        float raidTime,
        int botId,
        string profileId,
        string name,
        string role,
        string eventType,
        string detail,
        float posX,
        float posY,
        float posZ
    )
    {
        if (!_enabled || _writer == null)
            return;

        _writer.Enqueue(
            new TelemetryRecord(
                TelemetryTable.BotEvent,
                new object[]
                {
                    _raidId,
                    (double)raidTime,
                    botId,
                    profileId,
                    name,
                    role,
                    eventType,
                    detail,
                    (double)posX,
                    (double)posY,
                    (double)posZ,
                }
            )
        );
    }

    public static void RecordTaskScores(
        float raidTime,
        int botId,
        int activeTaskId,
        string activeTaskName,
        string scoresJson,
        string personality,
        float aggression,
        float raidTimeNorm
    )
    {
        if (!_enabled || _writer == null || !_config.RecordTaskScores)
            return;

        if (!ShouldSample(_lastTaskScoreTime, botId, raidTime, _config.TaskScoreSampleIntervalSec))
            return;

        _writer.Enqueue(
            new TelemetryRecord(
                TelemetryTable.TaskScore,
                new object[]
                {
                    _raidId,
                    (double)raidTime,
                    botId,
                    activeTaskId,
                    activeTaskName,
                    scoresJson,
                    personality,
                    (double)aggression,
                    (double)raidTimeNorm,
                }
            )
        );
    }

    public static void RecordMovement(
        float raidTime,
        int botId,
        string eventType,
        float posX,
        float posY,
        float posZ,
        float destX,
        float destY,
        float destZ,
        float speed,
        string detail
    )
    {
        if (!_enabled || _writer == null || !_config.RecordMovement)
            return;

        if (!ShouldSample(_lastMovementTime, botId, raidTime, _config.MovementSampleIntervalSec))
            return;

        _writer.Enqueue(
            new TelemetryRecord(
                TelemetryTable.MovementEvent,
                new object[]
                {
                    _raidId,
                    (double)raidTime,
                    botId,
                    eventType,
                    (double)posX,
                    (double)posY,
                    (double)posZ,
                    (double)destX,
                    (double)destY,
                    (double)destZ,
                    (double)speed,
                    detail,
                }
            )
        );
    }

    public static void RecordCombatEvent(
        float raidTime,
        int botId,
        string eventType,
        int targetId,
        string targetName,
        string weapon,
        float damage,
        float distance,
        float posX,
        float posY,
        float posZ
    )
    {
        if (!_enabled || _writer == null || !_config.RecordCombat)
            return;

        _writer.Enqueue(
            new TelemetryRecord(
                TelemetryTable.CombatEvent,
                new object[]
                {
                    _raidId,
                    (double)raidTime,
                    botId,
                    eventType,
                    targetId,
                    targetName,
                    weapon,
                    (double)damage,
                    (double)distance,
                    (double)posX,
                    (double)posY,
                    (double)posZ,
                }
            )
        );
    }

    public static void RecordSquadEvent(float raidTime, int squadId, int botId, string eventType, string detail)
    {
        if (!_enabled || _writer == null || !_config.RecordSquad)
            return;

        _writer.Enqueue(
            new TelemetryRecord(TelemetryTable.SquadEvent, new object[] { _raidId, (double)raidTime, squadId, botId, eventType, detail })
        );
    }

    public static void RecordPerformance(
        float raidTime,
        int aliveBots,
        int activeEntities,
        float updateDurationMs,
        float frameTimeMs,
        float memoryMb
    )
    {
        if (!_enabled || _writer == null || !_config.RecordPerformance)
            return;

        if (raidTime - _lastPerformanceTime < _config.PerformanceSampleIntervalSec)
            return;

        _lastPerformanceTime = raidTime;

        int queueDepth = _writer.QueueDepth;

        _writer.Enqueue(
            new TelemetryRecord(
                TelemetryTable.PerformanceSample,
                new object[]
                {
                    _raidId,
                    (double)raidTime,
                    aliveBots,
                    activeEntities,
                    (double)updateDurationMs,
                    (double)frameTimeMs,
                    (double)memoryMb,
                    queueDepth,
                }
            )
        );
    }

    public static void RecordError(float raidTime, string severity, string source, string message, string stackTrace, int botId)
    {
        if (!_enabled || _writer == null || !_config.RecordErrors)
            return;

        _writer.Enqueue(
            new TelemetryRecord(
                TelemetryTable.Error,
                new object[] { _raidId, (double)raidTime, severity, source, message, stackTrace, botId, null }
            )
        );
    }

    private static bool ShouldSample(Dictionary<int, float> lastTimes, int botId, float raidTime, float interval)
    {
        if (lastTimes.TryGetValue(botId, out float lastTime) && raidTime - lastTime < interval)
            return false;

        lastTimes[botId] = raidTime;
        return true;
    }
}
