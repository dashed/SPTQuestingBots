namespace SPTQuestingBots.BotLogic.ECS.Systems;

/// <summary>
/// A recorded combat event (gunshot, explosion, or airdrop landing).
/// Value type for ring-buffer storage, zero-allocation iteration.
/// Pure C# — no Unity or EFT dependencies.
/// </summary>
public struct CombatEvent
{
    /// <summary>World position X.</summary>
    public float X;

    /// <summary>World position Y.</summary>
    public float Y;

    /// <summary>World position Z.</summary>
    public float Z;

    /// <summary>Time when the event was recorded (Time.time).</summary>
    public float Time;

    /// <summary>Sound power/loudness (gunshot=100, explosion=150, airdrop=200).</summary>
    public float Power;

    /// <summary>Event type (see <see cref="CombatEventType"/>).</summary>
    public byte Type;

    /// <summary>Whether the shooter is a boss or boss follower.</summary>
    public bool IsBoss;

    /// <summary>Whether this is still a valid (non-expired) event.</summary>
    public bool IsActive;
}

/// <summary>
/// Combat event type constants.
/// </summary>
public static class CombatEventType
{
    public const byte None = 0;
    public const byte Gunshot = 1;
    public const byte Explosion = 2;
    public const byte Airdrop = 3;
    public const byte Death = 4;
}
