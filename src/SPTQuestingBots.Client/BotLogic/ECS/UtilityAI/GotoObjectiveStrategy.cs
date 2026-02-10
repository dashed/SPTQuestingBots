using System;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.Configuration;

namespace SPTQuestingBots.BotLogic.ECS.UtilityAI
{
    /// <summary>
    /// Delegate for validating/snapping a position to a walkable surface.
    /// Returns true if a valid snapped position was found.
    /// </summary>
    public delegate bool PositionValidator(float inX, float inY, float inZ, out float outX, out float outY, out float outZ);

    /// <summary>
    /// Delegate for checking if a walkable NavMesh path exists between two positions
    /// within a maximum path length budget.
    /// </summary>
    public delegate bool ReachabilityValidator(float fromX, float fromY, float fromZ, float toX, float toY, float toZ, float maxPathLength);

    /// <summary>
    /// Delegate for checking line-of-sight between two positions
    /// (no physical obstacles blocking the view).
    /// </summary>
    public delegate bool LosValidator(float fromX, float fromY, float fromZ, float toX, float toY, float toZ);

    /// <summary>
    /// Delegate for providing pre-computed cover positions near an objective.
    /// Returns the number of positions written to outPositions (as x,y,z triples).
    /// When enough cover positions are available, they replace geometric computation.
    /// </summary>
    public delegate int CoverPositionSource(float objX, float objY, float objZ, float radius, float[] outPositions, int maxCount);

    /// <summary>
    /// Primary squad strategy: move the squad toward the leader's current quest objective.
    /// Assigns tactical roles and positions to followers based on quest action type,
    /// tracks arrivals, and adjusts hold duration.
    /// <para>
    /// Pure C# — no Unity or EFT dependencies — fully testable.
    /// </para>
    /// </summary>
    public class GotoObjectiveStrategy : SquadStrategy
    {
        private readonly SquadStrategyConfig _config;
        private readonly System.Random _rng;
        private readonly PositionValidator _positionValidator;
        private readonly ReachabilityValidator _reachabilityValidator;
        private readonly LosValidator _losValidator;
        private readonly CoverPositionSource _coverPositionSource;
        private readonly float[] _fallbackBuffer = new float[32 * 2]; // sunflower XZ candidates

        // Reusable buffers (max 6 followers)
        private readonly SquadRole[] _roleBuffer = new SquadRole[SquadObjective.MaxMembers];
        private readonly SquadRole[] _combatRoleBuffer = new SquadRole[SquadObjective.MaxMembers];
        private readonly float[] _positionBuffer = new float[SquadObjective.MaxMembers * 3];

        public GotoObjectiveStrategy(
            SquadStrategyConfig config,
            float hysteresis = 0.25f,
            PositionValidator positionValidator = null,
            ReachabilityValidator reachabilityValidator = null,
            LosValidator losValidator = null,
            CoverPositionSource coverPositionSource = null
        )
            : base(hysteresis)
        {
            _config = config;
            _rng = new System.Random();
            _positionValidator = positionValidator;
            _reachabilityValidator = reachabilityValidator;
            _losValidator = losValidator;
            _coverPositionSource = coverPositionSource;
        }

        /// <summary>
        /// Constructor with seeded RNG for deterministic testing.
        /// </summary>
        public GotoObjectiveStrategy(
            SquadStrategyConfig config,
            int seed,
            float hysteresis = 0.25f,
            PositionValidator positionValidator = null,
            ReachabilityValidator reachabilityValidator = null,
            LosValidator losValidator = null,
            CoverPositionSource coverPositionSource = null
        )
            : base(hysteresis)
        {
            _config = config;
            _rng = new System.Random(seed);
            _positionValidator = positionValidator;
            _reachabilityValidator = reachabilityValidator;
            _losValidator = losValidator;
            _coverPositionSource = coverPositionSource;
        }

        /// <summary>
        /// Score 0.5 for squads with an active leader who has an objective; 0 otherwise.
        /// </summary>
        public override void ScoreSquad(int ordinal, SquadEntity squad)
        {
            if (squad.Leader != null && squad.Leader.IsActive && squad.Leader.HasActiveObjective)
                squad.StrategyScores[ordinal] = 0.5f;
            else
                squad.StrategyScores[ordinal] = 0f;
        }

        public override void Update()
        {
            var squads = ActiveSquads;
            for (int i = 0; i < squads.Count; i++)
            {
                UpdateSquad(squads[i]);
            }
        }

        private void UpdateSquad(SquadEntity squad)
        {
            if (squad.Leader == null || !squad.Leader.IsActive)
                return;

            var obj = squad.Objective;

            // If no objective or leader's objective changed, assign new objective
            if (!obj.HasObjective || HasLeaderObjectiveChanged(squad))
            {
                AssignNewObjective(squad);
                return;
            }

            // Combat-triggered position re-evaluation
            if (_config.EnableCombatAwarePositioning && squad.CombatVersion != squad.LastProcessedCombatVersion && obj.HasObjective)
            {
                RecomputeForCombat(squad);
                squad.LastProcessedCombatVersion = squad.CombatVersion;
            }

            // Check member arrivals
            if (obj.State == ObjectiveState.Active)
            {
                CheckArrivals(squad);
            }
        }

        private bool HasLeaderObjectiveChanged(SquadEntity squad)
        {
            return squad.Leader.HasActiveObjective && squad.Leader.LastSeenObjectiveVersion != squad.Objective.Version;
        }

        internal void AssignNewObjective(SquadEntity squad)
        {
            var leader = squad.Leader;
            if (!leader.HasActiveObjective)
                return;

            var obj = squad.Objective;

            // Approach vector from leader position to objective
            float approachX = leader.CurrentPositionX;
            float approachZ = leader.CurrentPositionZ;

            int followerCount = 0;
            for (int i = 0; i < squad.Members.Count; i++)
            {
                if (squad.Members[i] != leader && squad.Members[i].IsActive)
                    followerCount++;
            }

            if (followerCount == 0)
                return;

            int clampedCount = Math.Min(followerCount, SquadObjective.MaxMembers);

            // Assign roles based on leader's quest type
            if (_config.UseQuestTypeRoles)
                TacticalPositionCalculator.AssignRoles(leader.CurrentQuestAction, clampedCount, _roleBuffer);
            else
                TacticalPositionCalculator.AssignRoles(0, clampedCount, _roleBuffer);

            // Try BSG cover positions first (if available)
            bool usedCoverPositions = false;
            if (_coverPositionSource != null && _config.EnableCoverPositionSource)
            {
                int coverCount = _coverPositionSource(
                    obj.ObjectiveX,
                    obj.ObjectiveY,
                    obj.ObjectiveZ,
                    _config.CoverSearchRadius,
                    _positionBuffer,
                    clampedCount
                );
                if (coverCount >= clampedCount)
                    usedCoverPositions = true;
            }

            if (!usedCoverPositions)
            {
                // Compute positions geometrically
                TacticalPositionCalculator.ComputePositions(
                    obj.ObjectiveX,
                    obj.ObjectiveY,
                    obj.ObjectiveZ,
                    approachX,
                    approachZ,
                    _roleBuffer,
                    clampedCount,
                    _positionBuffer,
                    _config
                );

                // Validate/snap positions to walkable surfaces
                if (_positionValidator != null && _config.EnablePositionValidation)
                {
                    ValidatePositions(clampedCount, obj.ObjectiveX, obj.ObjectiveY, obj.ObjectiveZ);
                }
            }

            // Distribute to followers (with communication range + personality gating)
            int posIdx = 0;
            for (int i = 0; i < squad.Members.Count; i++)
            {
                var member = squad.Members[i];
                if (member == leader || !member.IsActive)
                    continue;
                if (posIdx >= clampedCount)
                    break;

                // Communication range gate
                if (_config.EnableCommunicationRange)
                {
                    float dx = leader.CurrentPositionX - member.CurrentPositionX;
                    float dy = leader.CurrentPositionY - member.CurrentPositionY;
                    float dz = leader.CurrentPositionZ - member.CurrentPositionZ;
                    float sqrDist = dx * dx + dy * dy + dz * dz;
                    if (
                        !CommunicationRange.IsInRange(
                            leader.HasEarPiece,
                            member.HasEarPiece,
                            sqrDist,
                            _config.CommunicationRangeNoEarpiece,
                            _config.CommunicationRangeEarpiece
                        )
                    )
                    {
                        member.HasTacticalPosition = false;
                        posIdx++;
                        continue;
                    }
                }

                // Probabilistic sharing gate (SAIN formula)
                if (_config.EnableSquadPersonality)
                {
                    float sharingChance = 25f + squad.CoordinationLevel * 15f;
                    if ((float)_rng.NextDouble() * 100f >= sharingChance)
                    {
                        member.HasTacticalPosition = false;
                        posIdx++;
                        continue;
                    }
                }

                // Check if position validation failed
                if (float.IsNaN(_positionBuffer[posIdx * 3]))
                {
                    member.HasTacticalPosition = false;
                    posIdx++;
                    continue;
                }

                member.SquadRole = _roleBuffer[posIdx];
                member.TacticalPositionX = _positionBuffer[posIdx * 3];
                member.TacticalPositionY = _positionBuffer[posIdx * 3 + 1];
                member.TacticalPositionZ = _positionBuffer[posIdx * 3 + 2];
                member.HasTacticalPosition = true;
                member.LastSeenObjectiveVersion = obj.Version;

                obj.SetTacticalPosition(
                    posIdx,
                    _positionBuffer[posIdx * 3],
                    _positionBuffer[posIdx * 3 + 1],
                    _positionBuffer[posIdx * 3 + 2],
                    _roleBuffer[posIdx]
                );

                posIdx++;
            }

            obj.MemberCount = posIdx;
            obj.State = ObjectiveState.Active;
            leader.LastSeenObjectiveVersion = obj.Version;

            // Set duration with Gaussian sampling (60-180 seconds)
            obj.Duration = SampleGaussian(60f, 180f);
            obj.DurationAdjusted = false;
        }

        /// <summary>
        /// Re-compute tactical positions in response to a combat state change.
        /// Uses CombatPositionAdjuster for threat-oriented positioning when a threat
        /// direction is available, otherwise falls back to standard geometric positions.
        /// </summary>
        internal void RecomputeForCombat(SquadEntity squad)
        {
            var leader = squad.Leader;
            if (leader == null || !leader.IsActive || !leader.HasActiveObjective)
                return;

            var obj = squad.Objective;
            if (!obj.HasObjective)
                return;

            int followerCount = 0;
            for (int i = 0; i < squad.Members.Count; i++)
            {
                if (squad.Members[i] != leader && squad.Members[i].IsActive)
                    followerCount++;
            }

            if (followerCount == 0)
                return;

            int clampedCount = Math.Min(followerCount, SquadObjective.MaxMembers);

            // Get base roles from quest type
            if (_config.UseQuestTypeRoles)
                TacticalPositionCalculator.AssignRoles(leader.CurrentQuestAction, clampedCount, _roleBuffer);
            else
                TacticalPositionCalculator.AssignRoles(0, clampedCount, _roleBuffer);

            if (squad.HasThreatDirection)
            {
                // Combat mode: reassign roles (Escort→Flanker) and use threat-oriented positions
                CombatPositionAdjuster.ReassignRolesForCombat(_roleBuffer, clampedCount, _combatRoleBuffer);
                CombatPositionAdjuster.ComputeCombatPositions(
                    obj.ObjectiveX,
                    obj.ObjectiveY,
                    obj.ObjectiveZ,
                    squad.ThreatDirectionX,
                    squad.ThreatDirectionZ,
                    _combatRoleBuffer,
                    clampedCount,
                    _config,
                    _positionBuffer
                );

                // Copy combat roles into role buffer for distribution
                Array.Copy(_combatRoleBuffer, _roleBuffer, clampedCount);
            }
            else
            {
                // No threat direction (combat cleared) — revert to standard geometric positions
                float approachX = leader.CurrentPositionX;
                float approachZ = leader.CurrentPositionZ;

                // Try BSG cover positions first
                bool usedCoverPositions = false;
                if (_coverPositionSource != null && _config.EnableCoverPositionSource)
                {
                    int coverCount = _coverPositionSource(
                        obj.ObjectiveX,
                        obj.ObjectiveY,
                        obj.ObjectiveZ,
                        _config.CoverSearchRadius,
                        _positionBuffer,
                        clampedCount
                    );
                    if (coverCount >= clampedCount)
                        usedCoverPositions = true;
                }

                if (!usedCoverPositions)
                {
                    TacticalPositionCalculator.ComputePositions(
                        obj.ObjectiveX,
                        obj.ObjectiveY,
                        obj.ObjectiveZ,
                        approachX,
                        approachZ,
                        _roleBuffer,
                        clampedCount,
                        _positionBuffer,
                        _config
                    );
                }
            }

            // Validate positions if enabled
            if (_positionValidator != null && _config.EnablePositionValidation)
            {
                ValidatePositions(clampedCount, obj.ObjectiveX, obj.ObjectiveY, obj.ObjectiveZ);
            }

            // Distribute to followers (same gating as AssignNewObjective)
            int posIdx = 0;
            for (int i = 0; i < squad.Members.Count; i++)
            {
                var member = squad.Members[i];
                if (member == leader || !member.IsActive)
                    continue;
                if (posIdx >= clampedCount)
                    break;

                // Communication range gate
                if (_config.EnableCommunicationRange)
                {
                    float dx = leader.CurrentPositionX - member.CurrentPositionX;
                    float dy = leader.CurrentPositionY - member.CurrentPositionY;
                    float dz = leader.CurrentPositionZ - member.CurrentPositionZ;
                    float sqrDist = dx * dx + dy * dy + dz * dz;
                    if (
                        !CommunicationRange.IsInRange(
                            leader.HasEarPiece,
                            member.HasEarPiece,
                            sqrDist,
                            _config.CommunicationRangeNoEarpiece,
                            _config.CommunicationRangeEarpiece
                        )
                    )
                    {
                        member.HasTacticalPosition = false;
                        posIdx++;
                        continue;
                    }
                }

                // Check if position validation failed
                if (float.IsNaN(_positionBuffer[posIdx * 3]))
                {
                    member.HasTacticalPosition = false;
                    posIdx++;
                    continue;
                }

                member.SquadRole = _roleBuffer[posIdx];
                member.TacticalPositionX = _positionBuffer[posIdx * 3];
                member.TacticalPositionY = _positionBuffer[posIdx * 3 + 1];
                member.TacticalPositionZ = _positionBuffer[posIdx * 3 + 2];
                member.HasTacticalPosition = true;

                posIdx++;
            }
        }

        internal void CheckArrivals(SquadEntity squad)
        {
            var obj = squad.Objective;
            float arrivalSqr = _config.ArrivalRadius * _config.ArrivalRadius;

            int arrived = 0;
            int totalFollowers = 0;

            for (int i = 0; i < squad.Members.Count; i++)
            {
                var member = squad.Members[i];
                if (member == squad.Leader || !member.IsActive || !member.HasTacticalPosition)
                    continue;

                totalFollowers++;
                float dx = member.CurrentPositionX - member.TacticalPositionX;
                float dy = member.CurrentPositionY - member.TacticalPositionY;
                float dz = member.CurrentPositionZ - member.TacticalPositionZ;
                float sqrDist = dx * dx + dy * dy + dz * dz;

                if (sqrDist <= arrivalSqr)
                    arrived++;
            }

            // When first member arrives, switch to Wait
            if (arrived > 0 && obj.State == ObjectiveState.Active)
            {
                obj.State = ObjectiveState.Wait;
            }

            // When all arrived and not yet adjusted, cut duration
            if (arrived >= totalFollowers && totalFollowers > 0 && !obj.DurationAdjusted)
            {
                float factor = SampleGaussian(0.2f, 0.5f);
                obj.Duration *= factor;
                obj.DurationAdjusted = true;
            }
        }

        private void ValidatePositions(int count, float objX, float objY, float objZ)
        {
            for (int i = 0; i < count; i++)
            {
                int off = i * 3;
                float px = _positionBuffer[off];
                float py = _positionBuffer[off + 1];
                float pz = _positionBuffer[off + 2];

                // Step 1: Snap to NavMesh
                if (_positionValidator(px, py, pz, out float sx, out float sy, out float sz))
                {
                    _positionBuffer[off] = sx;
                    _positionBuffer[off + 1] = sy;
                    _positionBuffer[off + 2] = sz;

                    // Step 2+3: Check reachability and LOS
                    if (IsPositionValid(sx, sy, sz, objX, objY, objZ, _roleBuffer[i]))
                        continue; // Position is valid — keep it
                }

                // Primary position failed (snap or validation) — try fallback
                if (
                    TryFallbackPosition(
                        objX,
                        objZ,
                        objY,
                        _config.FallbackSearchRadius,
                        _config.FallbackCandidateCount,
                        _roleBuffer[i],
                        objX,
                        objY,
                        objZ,
                        out float fx,
                        out float fy,
                        out float fz
                    )
                )
                {
                    _positionBuffer[off] = fx;
                    _positionBuffer[off + 1] = fy;
                    _positionBuffer[off + 2] = fz;
                }
                else
                {
                    // Mark position as invalid
                    _positionBuffer[off] = float.NaN;
                }
            }
        }

        /// <summary>
        /// Checks reachability and (for Overwatch) line-of-sight for a snapped position.
        /// Pure-logic helper — uses injected delegates for actual NavMesh/Physics calls.
        /// </summary>
        private bool IsPositionValid(float px, float py, float pz, float objX, float objY, float objZ, SquadRole role)
        {
            // Reachability: verify NavMesh path exists within length budget
            if (_reachabilityValidator != null && _config.EnableReachabilityCheck)
            {
                float dx = objX - px;
                float dy = objY - py;
                float dz = objZ - pz;
                float directDist = (float)System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
                float maxLen = directDist * _config.MaxPathLengthMultiplier;
                if (!_reachabilityValidator(objX, objY, objZ, px, py, pz, maxLen))
                    return false;
            }

            // LOS: for Overwatch positions, verify line-of-sight to objective
            if (_losValidator != null && _config.EnableLosCheck && role == SquadRole.Overwatch)
            {
                if (!_losValidator(px, py, pz, objX, objY, objZ))
                    return false;
            }

            return true;
        }

        internal bool TryFallbackPosition(
            float cx,
            float cz,
            float y,
            float radius,
            int count,
            SquadRole role,
            float objX,
            float objY,
            float objZ,
            out float fx,
            out float fy,
            out float fz
        )
        {
            fx = fy = fz = 0f;
            int clampedCount = System.Math.Min(count, _fallbackBuffer.Length / 2);
            int generated = SunflowerSpiral.Generate(cx, cz, radius * 0.75f, clampedCount, _fallbackBuffer);
            for (int j = 0; j < generated; j++)
            {
                float candX = _fallbackBuffer[j * 2];
                float candZ = _fallbackBuffer[j * 2 + 1];
                if (_positionValidator(candX, y, candZ, out fx, out fy, out fz))
                {
                    // Also check reachability and LOS
                    if (IsPositionValid(fx, fy, fz, objX, objY, objZ, role))
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Box-Muller Gaussian sampling clamped to [min, max].
        /// </summary>
        internal float SampleGaussian(float min, float max)
        {
            float mean = (min + max) / 2f;
            float stddev = (max - min) / 4f; // ~95% within range

            float u1 = 1f - (float)_rng.NextDouble();
            float u2 = (float)_rng.NextDouble();
            float normal = (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
            float value = mean + stddev * normal;

            return Math.Max(min, Math.Min(max, value));
        }
    }
}
