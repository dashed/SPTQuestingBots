using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SPTQuestingBots.BotLogic.ECS
{
    /// <summary>
    /// Dense squad storage with swap-remove and ID recycling.
    /// Follows the same pattern as <see cref="BotRegistry"/>: dense List + sparse ID-to-index map + free ID stack.
    /// Includes BSG group ID mapping for O(1) external-ID-to-squad lookup.
    /// Pure C# — no Unity or EFT dependencies — fully testable in net9.0.
    /// </summary>
    public sealed class SquadRegistry
    {
        /// <summary>Dense list of active squads. Iteration-friendly — no gaps.</summary>
        private readonly List<SquadEntity> _squads;

        /// <summary>
        /// Sparse map from squad entity ID to index in <see cref="_squads"/>.
        /// Null entries indicate freed IDs.
        /// </summary>
        private readonly List<int?> _idToIndex;

        /// <summary>Stack of recycled IDs available for reuse.</summary>
        private readonly Stack<int> _freeIds;

        /// <summary>
        /// Maps BSG group IDs to squad entity IDs for O(1) lookup.
        /// </summary>
        private readonly Dictionary<int, int> _bsgGroupToSquadId;

        public SquadRegistry(int capacity = 16)
        {
            _squads = new List<SquadEntity>(capacity);
            _idToIndex = new List<int?>(capacity);
            _freeIds = new Stack<int>(capacity);
            _bsgGroupToSquadId = new Dictionary<int, int>(capacity);
        }

        /// <summary>Read-only view of active squads for dense iteration.</summary>
        public IReadOnlyList<SquadEntity> ActiveSquads
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _squads;
        }

        /// <summary>Number of active squads.</summary>
        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _squads.Count;
        }

        /// <summary>
        /// Create and register a new squad, recycling a freed ID if available.
        /// </summary>
        /// <param name="strategyCount">Number of strategy score slots to allocate.</param>
        /// <param name="targetMembers">Target number of squad members.</param>
        /// <returns>The newly created <see cref="SquadEntity"/>.</returns>
        public SquadEntity Add(int strategyCount, int targetMembers)
        {
            var valueIndex = _squads.Count;
            int id;

            if (_freeIds.Count > 0)
            {
                id = _freeIds.Pop();
                _idToIndex[id] = valueIndex;
            }
            else
            {
                id = _idToIndex.Count;
                _idToIndex.Add(valueIndex);
            }

            var squad = new SquadEntity(id, strategyCount, targetMembers);
            _squads.Add(squad);
            return squad;
        }

        /// <summary>
        /// Get an existing squad for a BSG group ID, or create a new one if not found.
        /// </summary>
        /// <param name="bsgGroupId">External BSG group ID.</param>
        /// <param name="strategyCount">Number of strategy score slots (used only for creation).</param>
        /// <param name="targetMembers">Target member count (used only for creation).</param>
        /// <returns>The existing or newly created <see cref="SquadEntity"/>.</returns>
        public SquadEntity GetOrCreate(int bsgGroupId, int strategyCount, int targetMembers)
        {
            if (_bsgGroupToSquadId.TryGetValue(bsgGroupId, out var squadId))
            {
                if (TryGetById(squadId, out var existing))
                    return existing;

                // Stale mapping — clean it up
                _bsgGroupToSquadId.Remove(bsgGroupId);
            }

            var squad = Add(strategyCount, targetMembers);
            _bsgGroupToSquadId[bsgGroupId] = squad.Id;
            return squad;
        }

        /// <summary>
        /// Remove a squad using swap-remove to keep the dense list compact.
        /// Also cleans up any BSG group mapping pointing to this squad.
        /// </summary>
        /// <returns>True if the squad was found and removed.</returns>
        public bool Remove(SquadEntity squad)
        {
            if (squad == null)
                return false;

            if (squad.Id < 0 || squad.Id >= _idToIndex.Count)
                return false;

            var slot = _idToIndex[squad.Id];
            if (!slot.HasValue)
                return false;

            var removedIndex = slot.Value;
            var lastIndex = _squads.Count - 1;

            // Clear member references
            for (int i = squad.Members.Count - 1; i >= 0; i--)
            {
                var member = squad.Members[i];
                member.Squad = null;
                member.SquadRole = SquadRole.None;
                member.HasTacticalPosition = false;
            }

            squad.Members.Clear();
            squad.Leader = null;

            // Clean BSG group mapping
            CleanBsgMapping(squad.Id);

            // Swap last squad into the removed slot
            _squads[removedIndex] = _squads[lastIndex];
            _squads.RemoveAt(lastIndex);

            // If registry is now empty, reset everything
            if (_squads.Count == 0)
            {
                _freeIds.Clear();
                _idToIndex.Clear();
                return true;
            }

            // Free the removed squad's ID slot
            if (squad.Id == _idToIndex.Count - 1)
            {
                // Last slot in sparse array — shrink instead of wasting a free-list entry
                _idToIndex.RemoveAt(squad.Id);
            }
            else
            {
                _idToIndex[squad.Id] = null;
                _freeIds.Push(squad.Id);
            }

            // Update the swapped squad's index mapping (skip if we removed the last element)
            if (removedIndex != lastIndex)
            {
                var swapped = _squads[removedIndex];
                _idToIndex[swapped.Id] = removedIndex;
            }

            return true;
        }

        /// <summary>
        /// Look up a squad by BSG group ID.
        /// Returns null if the group ID is not mapped or the squad was removed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SquadEntity GetByBsgGroupId(int bsgGroupId)
        {
            if (_bsgGroupToSquadId.TryGetValue(bsgGroupId, out var squadId))
            {
                if (TryGetById(squadId, out var squad))
                    return squad;

                // Stale mapping — clean it up
                _bsgGroupToSquadId.Remove(bsgGroupId);
            }

            return null;
        }

        /// <summary>
        /// Add a member to a squad. The first member becomes the leader.
        /// Sets the member's Squad reference and SquadRole.
        /// </summary>
        public void AddMember(SquadEntity squad, BotEntity member)
        {
            if (squad == null || member == null)
                return;

            // Already a member of this squad
            if (member.Squad == squad)
                return;

            // Remove from previous squad if any
            if (member.Squad != null)
                RemoveMember(member.Squad, member);

            squad.Members.Add(member);
            member.Squad = squad;

            if (squad.Leader == null)
            {
                squad.Leader = member;
                member.SquadRole = SquadRole.Leader;
            }
            else
            {
                member.SquadRole = SquadRole.Guard; // Default role for non-leaders
            }
        }

        /// <summary>
        /// Remove a member from a squad. Clears the member's Squad reference and SquadRole.
        /// If the leader is removed, the next member (if any) becomes the new leader.
        /// </summary>
        public void RemoveMember(SquadEntity squad, BotEntity member)
        {
            if (squad == null || member == null)
                return;

            if (member.Squad != squad)
                return;

            squad.Members.Remove(member);
            member.Squad = null;
            member.SquadRole = SquadRole.None;
            member.HasTacticalPosition = false;

            // Reassign leader if the removed member was the leader
            if (squad.Leader == member)
            {
                if (squad.Members.Count > 0)
                {
                    squad.Leader = squad.Members[0];
                    squad.Leader.SquadRole = SquadRole.Leader;
                }
                else
                {
                    squad.Leader = null;
                }
            }
        }

        /// <summary>
        /// Try to get a squad by its stable ID.
        /// </summary>
        /// <returns>True if found.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetById(int id, out SquadEntity squad)
        {
            if (id >= 0 && id < _idToIndex.Count)
            {
                var index = _idToIndex[id];
                if (index.HasValue)
                {
                    squad = _squads[index.Value];
                    return true;
                }
            }

            squad = null;
            return false;
        }

        /// <summary>
        /// Check whether a squad with the given ID is currently registered.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(int id)
        {
            return id >= 0 && id < _idToIndex.Count && _idToIndex[id].HasValue;
        }

        /// <summary>
        /// Remove all squads and reset ID allocation.
        /// </summary>
        public void Clear()
        {
            // Clear member references before clearing squads
            for (int i = 0; i < _squads.Count; i++)
            {
                var squad = _squads[i];
                for (int j = 0; j < squad.Members.Count; j++)
                {
                    squad.Members[j].Squad = null;
                    squad.Members[j].SquadRole = SquadRole.None;
                    squad.Members[j].HasTacticalPosition = false;
                }

                squad.Members.Clear();
                squad.Leader = null;
            }

            _squads.Clear();
            _idToIndex.Clear();
            _freeIds.Clear();
            _bsgGroupToSquadId.Clear();
        }

        /// <summary>
        /// Remove any BSG group mapping that points to the given squad ID.
        /// </summary>
        private void CleanBsgMapping(int squadId)
        {
            // Find and remove the BSG group mapping for this squad.
            // Squads are typically few, so a linear scan of the dictionary is fine.
            int keyToRemove = -1;
            foreach (var kvp in _bsgGroupToSquadId)
            {
                if (kvp.Value == squadId)
                {
                    keyToRemove = kvp.Key;
                    break;
                }
            }

            if (keyToRemove >= 0)
                _bsgGroupToSquadId.Remove(keyToRemove);
        }
    }
}
