using System.Collections.Generic;
using System.Runtime.CompilerServices;
using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ECS.UtilityAI
{
    /// <summary>
    /// Evaluates utility scores across all registered strategies and selects the
    /// highest-scoring strategy per squad with additive hysteresis.
    /// <para>
    /// Mirrors <see cref="UtilityTaskManager"/> but operates on <see cref="SquadEntity"/>
    /// and <see cref="SquadStrategy"/> instead of <see cref="BotEntity"/> and <see cref="UtilityTask"/>.
    /// </para>
    /// <para>
    /// Update flow (called each tick):
    /// <list type="number">
    /// <item><c>UpdateScores</c> — each strategy computes scores for all squads (column-major)</item>
    /// <item><c>PickStrategies</c> — for each squad, select highest-scoring strategy with hysteresis</item>
    /// <item><c>UpdateStrategies</c> — each strategy executes behavior for its active squads</item>
    /// </list>
    /// </para>
    /// <para>
    /// Pure C# — no Unity or EFT dependencies — fully testable.
    /// </para>
    /// </summary>
    public class SquadStrategyManager
    {
        /// <summary>Registered strategies in evaluation order.</summary>
        public readonly SquadStrategy[] Strategies;

        public SquadStrategyManager(SquadStrategy[] strategies)
        {
            Strategies = strategies;
        }

        /// <summary>
        /// Full update cycle: score → pick → execute.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(IReadOnlyList<SquadEntity> squads)
        {
            UpdateScores(squads);
            PickStrategies(squads);
            UpdateStrategies();
        }

        /// <summary>
        /// Phase 1: Each strategy computes scores for all squads (column-major).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateScores(IReadOnlyList<SquadEntity> squads)
        {
            for (int i = 0; i < Strategies.Length; i++)
            {
                Strategies[i].UpdateScores(i, squads);
            }
        }

        /// <summary>
        /// Phase 2: For each squad, select the highest-scoring strategy.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PickStrategies(IReadOnlyList<SquadEntity> squads)
        {
            for (int i = 0; i < squads.Count; i++)
            {
                var squad = squads[i];

                if (!squad.IsActive)
                {
                    if (squad.StrategyAssignment.Strategy != null)
                    {
                        ((SquadStrategy)squad.StrategyAssignment.Strategy).Deactivate(squad);
                        squad.StrategyAssignment = default;
                    }

                    continue;
                }

                PickStrategy(squad);
            }
        }

        /// <summary>
        /// Phase 3: Each strategy executes behavior for its active squads.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateStrategies()
        {
            for (int i = 0; i < Strategies.Length; i++)
            {
                Strategies[i].Update();
            }
        }

        /// <summary>
        /// Core strategy selection with hysteresis for a single squad.
        /// Identical algorithm to <see cref="UtilityTaskManager.PickTask"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PickStrategy(SquadEntity squad)
        {
            var assignment = squad.StrategyAssignment;

            float highestScore = 0f;
            int nextOrdinal = 0;

            // Seed from current strategy — including hysteresis
            if (assignment.Strategy != null)
            {
                var currentStrategy = (SquadStrategy)assignment.Strategy;
                nextOrdinal = assignment.Ordinal;
                highestScore = squad.StrategyScores[assignment.Ordinal] + currentStrategy.Hysteresis;
            }

            SquadStrategy nextStrategy = null;

            for (int j = 0; j < Strategies.Length; j++)
            {
                float score = squad.StrategyScores[j];
                if (score <= highestScore)
                    continue;

                highestScore = score;
                nextOrdinal = j;
                nextStrategy = Strategies[j];
            }

            // If no strategy beats the current (with hysteresis), keep current
            if (nextStrategy == null)
                return;

            // Switch strategies
            if (assignment.Strategy != null)
            {
                LoggingController.LogInfo(
                    "[SquadStrategyManager] Squad "
                        + squad.Id
                        + " switching strategy from ordinal "
                        + assignment.Ordinal
                        + " to "
                        + nextOrdinal
                        + " (score="
                        + highestScore
                        + ")"
                );
                ((SquadStrategy)assignment.Strategy).Deactivate(squad);
            }
            else
            {
                LoggingController.LogInfo(
                    "[SquadStrategyManager] Squad "
                        + squad.Id
                        + " activating strategy ordinal "
                        + nextOrdinal
                        + " (score="
                        + highestScore
                        + ")"
                );
            }
            nextStrategy.Activate(squad);

            squad.StrategyAssignment = new StrategyAssignment(nextStrategy, nextOrdinal);
        }

        /// <summary>
        /// Score all strategies for a single squad and pick the best one.
        /// Convenience method for per-squad evaluation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ScoreAndPick(SquadEntity squad)
        {
            if (!squad.IsActive)
            {
                if (squad.StrategyAssignment.Strategy != null)
                {
                    ((SquadStrategy)squad.StrategyAssignment.Strategy).Deactivate(squad);
                    squad.StrategyAssignment = default;
                }

                return;
            }

            for (int i = 0; i < Strategies.Length; i++)
            {
                Strategies[i].ScoreSquad(i, squad);
            }

            PickStrategy(squad);
        }

        /// <summary>
        /// Remove a squad from all strategy tracking.
        /// </summary>
        public void RemoveSquad(SquadEntity squad)
        {
            if (squad.StrategyAssignment.Strategy != null)
            {
                LoggingController.LogDebug("[SquadStrategyManager] Removing squad " + squad.Id + " from strategy tracking");
                ((SquadStrategy)squad.StrategyAssignment.Strategy).Deactivate(squad);
            }
            squad.StrategyAssignment = default;
        }
    }
}
