namespace SPTQuestingBots.Telemetry;

internal enum TelemetryTable
{
    Raid,
    BotEvent,
    TaskScore,
    MovementEvent,
    CombatEvent,
    SquadEvent,
    PerformanceSample,
    Error,
}

internal readonly struct TelemetryRecord
{
    public readonly TelemetryTable Table;
    public readonly object[] Parameters;

    public TelemetryRecord(TelemetryTable table, object[] parameters)
    {
        Table = table;
        Parameters = parameters;
    }
}
