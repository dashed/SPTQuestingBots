using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ECS.UtilityAI
{
    /// <summary>
    /// Abstract base for quest-related utility tasks that map to BigBrain actions.
    /// Extends <see cref="UtilityTask"/> with a <see cref="BotActionTypeId"/> for
    /// the layer to dispatch to the correct <c>CustomLogic</c> action.
    /// <para>
    /// Update() is a no-op — BigBrain handles actual action execution.
    /// </para>
    /// </summary>
    public abstract class QuestUtilityTask : UtilityTask
    {
        /// <summary>
        /// The <c>BotActionType</c> enum value (as int) this task maps to.
        /// See <see cref="BotActionTypeId"/> for constants.
        /// </summary>
        public abstract int BotActionTypeId { get; }

        /// <summary>
        /// Human-readable reason string for this task's action.
        /// Used for logging in <c>setNextAction()</c>.
        /// </summary>
        public abstract string ActionReason { get; }

        protected QuestUtilityTask(float hysteresis)
            : base(hysteresis) { }

        /// <summary>
        /// No-op — BigBrain handles action execution via CustomLogic classes.
        /// </summary>
        public override void Update() { }
    }

    /// <summary>
    /// Integer constants mirroring <c>BotActionType</c> enum values.
    /// Used by quest utility tasks without depending on game assemblies.
    /// Values MUST stay in sync with <c>BehaviorExtensions.BotActionType</c> ordinals.
    /// </summary>
    public static class BotActionTypeId
    {
        public const int Undefined = 0;
        public const int GoToObjective = 1;
        public const int FollowBoss = 2;
        public const int HoldPosition = 3;
        public const int Ambush = 4;
        public const int Snipe = 5;
        public const int PlantItem = 6;
        public const int BossRegroup = 7;
        public const int FollowerRegroup = 8;
        public const int Sleep = 9;
        public const int ToggleSwitch = 10;
        public const int UnlockDoor = 11;
        public const int CloseNearbyDoors = 12;
        public const int Loot = 13;
        public const int Vulture = 14;
    }
}
