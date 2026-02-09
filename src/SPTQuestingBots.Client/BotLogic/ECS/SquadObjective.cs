namespace SPTQuestingBots.BotLogic.ECS
{
    /// <summary>
    /// State of the squad's shared objective (Active or Waiting).
    /// </summary>
    public enum ObjectiveState : byte
    {
        /// <summary>Squad is actively pursuing this objective.</summary>
        Active = 0,

        /// <summary>Squad is waiting at the objective.</summary>
        Wait = 1,
    }

    /// <summary>
    /// Shared squad objective state. Holds the current objective position,
    /// tactical positions for each member, and a version counter for change detection.
    /// Pure C# — no Unity dependencies.
    /// </summary>
    public sealed class SquadObjective
    {
        /// <summary>Maximum number of members that can have tactical positions.</summary>
        public const int MaxMembers = 6;

        // ── Current Objective ──────────────────────────────────────

        /// <summary>Objective position X coordinate.</summary>
        public float ObjectiveX;

        /// <summary>Objective position Y coordinate.</summary>
        public float ObjectiveY;

        /// <summary>Objective position Z coordinate.</summary>
        public float ObjectiveZ;

        /// <summary>Whether a valid objective is set.</summary>
        public bool HasObjective;

        // ── Previous Objective ─────────────────────────────────────

        /// <summary>Previous objective position X coordinate.</summary>
        public float PreviousX;

        /// <summary>Previous objective position Y coordinate.</summary>
        public float PreviousY;

        /// <summary>Previous objective position Z coordinate.</summary>
        public float PreviousZ;

        /// <summary>Whether a previous objective was stored.</summary>
        public bool HasPreviousObjective;

        // ── Timing / State ─────────────────────────────────────────

        /// <summary>Current state of the objective.</summary>
        public ObjectiveState State;

        /// <summary>Time when this objective was set (game time seconds).</summary>
        public float StartTime;

        /// <summary>Planned duration at the objective (seconds).</summary>
        public float Duration;

        /// <summary>Whether the duration has been adjusted from the initial value.</summary>
        public bool DurationAdjusted;

        // ── Tactical Positions ─────────────────────────────────────

        /// <summary>
        /// Tactical positions stored as x,y,z triples per member.
        /// Layout: [m0.x, m0.y, m0.z, m1.x, m1.y, m1.z, ...].
        /// Capacity: <see cref="MaxMembers"/> * 3 = 18.
        /// </summary>
        public readonly float[] TacticalPositions;

        /// <summary>
        /// Role assigned to each member slot. Indexed by member index (0..MemberCount-1).
        /// Capacity: <see cref="MaxMembers"/>.
        /// </summary>
        public readonly SquadRole[] MemberRoles;

        /// <summary>How many tactical positions are currently set.</summary>
        public int MemberCount;

        // ── Version Tracking ───────────────────────────────────────

        /// <summary>
        /// Monotonically increasing version. Incremented on each change.
        /// Bots compare against <see cref="BotEntity.LastSeenObjectiveVersion"/>
        /// to detect when the objective changes.
        /// </summary>
        public int Version;

        // ── Constructor ────────────────────────────────────────────

        public SquadObjective()
        {
            TacticalPositions = new float[MaxMembers * 3];
            MemberRoles = new SquadRole[MaxMembers];
        }

        // ── Methods ────────────────────────────────────────────────

        /// <summary>
        /// Set a new objective position. Saves current position as previous.
        /// Increments the version counter.
        /// </summary>
        public void SetObjective(float x, float y, float z)
        {
            if (HasObjective)
            {
                PreviousX = ObjectiveX;
                PreviousY = ObjectiveY;
                PreviousZ = ObjectiveZ;
                HasPreviousObjective = true;
            }

            ObjectiveX = x;
            ObjectiveY = y;
            ObjectiveZ = z;
            HasObjective = true;
            State = ObjectiveState.Active;
            Version++;
        }

        /// <summary>
        /// Clear the current objective and reset tactical positions.
        /// Increments the version counter.
        /// </summary>
        public void ClearObjective()
        {
            HasObjective = false;
            State = ObjectiveState.Active;
            MemberCount = 0;
            StartTime = 0f;
            Duration = 0f;
            DurationAdjusted = false;
            Version++;
        }

        /// <summary>
        /// Manually increment the version counter for external changes.
        /// </summary>
        public void IncrementVersion()
        {
            Version++;
        }

        /// <summary>
        /// Set a tactical position and role for a member at the given index.
        /// </summary>
        /// <param name="index">Member index (0..MaxMembers-1).</param>
        /// <param name="x">X coordinate of the tactical position.</param>
        /// <param name="y">Y coordinate of the tactical position.</param>
        /// <param name="z">Z coordinate of the tactical position.</param>
        /// <param name="role">Role for this member.</param>
        public void SetTacticalPosition(int index, float x, float y, float z, SquadRole role)
        {
            if (index < 0 || index >= MaxMembers)
                return;

            int offset = index * 3;
            TacticalPositions[offset] = x;
            TacticalPositions[offset + 1] = y;
            TacticalPositions[offset + 2] = z;
            MemberRoles[index] = role;

            if (index >= MemberCount)
                MemberCount = index + 1;
        }
    }
}
