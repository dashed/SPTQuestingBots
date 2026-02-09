using System.Collections.Generic;

namespace SPTQuestingBots.BotLogic.ECS.UtilityAI
{
    /// <summary>
    /// Abstract base class for scored squad strategies. Each strategy computes a utility
    /// score for every squad (column-major), and the strategy manager selects the
    /// highest-scoring strategy per squad with additive hysteresis.
    /// <para>
    /// Mirrors <see cref="UtilityTask"/> but operates on <see cref="SquadEntity"/> instead of
    /// <see cref="BotEntity"/>. Follows the same Phobos <c>Task&lt;T&gt;</c> pattern.
    /// </para>
    /// <para>
    /// Pure C# — no Unity or EFT dependencies — fully testable.
    /// </para>
    /// </summary>
    public abstract class SquadStrategy
    {
        /// <summary>
        /// Additive bonus added to this strategy's score when it is the current strategy.
        /// A competing strategy must exceed <c>currentScore + Hysteresis</c> to take over.
        /// </summary>
        public readonly float Hysteresis;

        private readonly List<SquadEntity> _activeSquads = new List<SquadEntity>(8);
        private readonly HashSet<int> _activeSquadIds = new HashSet<int>();

        protected SquadStrategy(float hysteresis)
        {
            Hysteresis = hysteresis;
        }

        /// <summary>Read-only view of squads currently assigned to this strategy.</summary>
        public IReadOnlyList<SquadEntity> ActiveSquads => _activeSquads;

        /// <summary>Returns the number of squads currently assigned to this strategy.</summary>
        public int ActiveSquadCount => _activeSquads.Count;

        /// <summary>
        /// Compute the utility score for a single squad and write the result
        /// to <c>squad.StrategyScores[ordinal]</c>.
        /// </summary>
        public abstract void ScoreSquad(int ordinal, SquadEntity squad);

        /// <summary>
        /// Column-major score update: compute the utility score for ALL squads.
        /// Default implementation calls <see cref="ScoreSquad"/> in a loop.
        /// </summary>
        public virtual void UpdateScores(int ordinal, IReadOnlyList<SquadEntity> squads)
        {
            for (int i = 0; i < squads.Count; i++)
            {
                ScoreSquad(ordinal, squads[i]);
            }
        }

        /// <summary>
        /// Execute behavior for all squads currently assigned to this strategy.
        /// Called every tick after strategy selection.
        /// </summary>
        public abstract void Update();

        /// <summary>
        /// Called when a squad switches TO this strategy.
        /// Override to perform activation logic.
        /// </summary>
        public virtual void Activate(SquadEntity squad)
        {
            if (!_activeSquadIds.Add(squad.Id))
                return;

            _activeSquads.Add(squad);
        }

        /// <summary>
        /// Called when a squad switches AWAY from this strategy.
        /// Override to perform cleanup.
        /// </summary>
        public virtual void Deactivate(SquadEntity squad)
        {
            if (!_activeSquadIds.Remove(squad.Id))
                return;

            for (int i = 0; i < _activeSquads.Count; i++)
            {
                if (_activeSquads[i].Id != squad.Id)
                    continue;

                // Swap-remove for O(1) removal
                int lastIndex = _activeSquads.Count - 1;
                if (i != lastIndex)
                    _activeSquads[i] = _activeSquads[lastIndex];
                _activeSquads.RemoveAt(lastIndex);
                return;
            }
        }
    }
}
