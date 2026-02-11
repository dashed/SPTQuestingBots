using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SPTQuestingBots.BotLogic.ECS;

/// <summary>
/// Dense entity storage with swap-remove and ID recycling.
/// Follows Phobos EntityArray pattern: dense List + sparse ID→index map + free ID stack.
/// Includes BsgBotRegistry-style sparse array for O(1) external-ID → entity lookup.
/// Pure C# — no Unity or EFT dependencies — fully testable in net9.0.
/// </summary>
public sealed class BotRegistry
{
    /// <summary>Dense list of active entities. Iteration-friendly — no gaps.</summary>
    public readonly List<BotEntity> Entities;

    /// <summary>
    /// Sparse map from entity ID to index in <see cref="Entities"/>.
    /// Null entries indicate freed IDs.
    /// </summary>
    private readonly List<int?> _idToIndex;

    /// <summary>Stack of recycled IDs available for reuse.</summary>
    private readonly Stack<int> _freeIds;

    /// <summary>
    /// Sparse array for O(1) external-ID (e.g. BotOwner.Id) → BotEntity lookup.
    /// Null-padded: slots for removed bots are set to null.
    /// Inspired by Phobos BsgBotRegistry pattern.
    /// </summary>
    private readonly List<BotEntity> _bsgIdToEntity;

    public BotRegistry(int capacity = 32)
    {
        Entities = new List<BotEntity>(capacity);
        _idToIndex = new List<int?>(capacity);
        _freeIds = new Stack<int>(capacity);
        _bsgIdToEntity = new List<BotEntity>(capacity);
    }

    /// <summary>Number of active entities.</summary>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { return Entities.Count; }
    }

    /// <summary>
    /// O(1) lookup by stable entity ID.
    /// Throws <see cref="KeyNotFoundException"/> if the ID is invalid or freed.
    /// </summary>
    public BotEntity this[int id]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (id < 0 || id >= _idToIndex.Count)
            {
                throw new KeyNotFoundException($"Entity ID {id} not found");
            }

            var index = _idToIndex[id];
            return index.HasValue ? Entities[index.Value] : throw new KeyNotFoundException($"Entity ID {id} not found");
        }
    }

    /// <summary>
    /// Create and register a new entity with an external BSG ID for O(1) reverse lookup.
    /// </summary>
    /// <param name="bsgId">External ID (e.g. BotOwner.Id) for sparse-array lookup.</param>
    /// <returns>The newly created <see cref="BotEntity"/>.</returns>
    public BotEntity Add(int bsgId)
    {
        var entity = Add();

        // Grow sparse array if needed
        while (_bsgIdToEntity.Count <= bsgId)
        {
            _bsgIdToEntity.Add(null);
        }

        _bsgIdToEntity[bsgId] = entity;
        return entity;
    }

    /// <summary>
    /// O(1) lookup by external BSG ID (e.g. BotOwner.Id).
    /// Returns null if the ID is out of range or the bot was removed.
    /// Inspired by Phobos BsgBotRegistry pattern.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BotEntity GetByBsgId(int bsgId)
    {
        if (bsgId >= 0 && bsgId < _bsgIdToEntity.Count)
        {
            return _bsgIdToEntity[bsgId];
        }

        return null;
    }

    /// <summary>
    /// Clear the external BSG ID mapping for a given ID (e.g. when a bot is removed).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ClearBsgId(int bsgId)
    {
        if (bsgId >= 0 && bsgId < _bsgIdToEntity.Count)
        {
            _bsgIdToEntity[bsgId] = null;
        }
    }

    /// <summary>
    /// Create and register a new entity, recycling a freed ID if available.
    /// </summary>
    /// <returns>The newly created <see cref="BotEntity"/>.</returns>
    public BotEntity Add()
    {
        var valueIndex = Entities.Count;
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

        var entity = new BotEntity(id);
        Entities.Add(entity);
        return entity;
    }

    /// <summary>
    /// Remove an entity using swap-remove to keep the dense list compact.
    /// The last entity is swapped into the removed entity's slot.
    /// </summary>
    /// <returns>True if the entity was found and removed.</returns>
    public bool Remove(BotEntity entity)
    {
        if (entity == null)
        {
            return false;
        }

        if (entity.Id < 0 || entity.Id >= _idToIndex.Count)
        {
            return false;
        }

        var slot = _idToIndex[entity.Id];
        if (!slot.HasValue)
        {
            return false;
        }

        var removedIndex = slot.Value;
        var lastIndex = Entities.Count - 1;

        // Swap last entity into the removed slot
        Entities[removedIndex] = Entities[lastIndex];
        Entities.RemoveAt(lastIndex);

        // If registry is now empty, reset everything
        if (Entities.Count == 0)
        {
            _freeIds.Clear();
            _idToIndex.Clear();
            return true;
        }

        // Free the removed entity's ID slot
        if (entity.Id == _idToIndex.Count - 1)
        {
            // Last slot in sparse array — shrink instead of wasting a free-list entry
            _idToIndex.RemoveAt(entity.Id);
        }
        else
        {
            _idToIndex[entity.Id] = null;
            _freeIds.Push(entity.Id);
        }

        // Update the swapped entity's index mapping (skip if we removed the last element)
        if (removedIndex != lastIndex)
        {
            var swapped = Entities[removedIndex];
            _idToIndex[swapped.Id] = removedIndex;
        }

        return true;
    }

    /// <summary>
    /// Try to get an entity by its stable ID.
    /// </summary>
    /// <returns>True if found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetById(int id, out BotEntity entity)
    {
        if (id >= 0 && id < _idToIndex.Count)
        {
            var index = _idToIndex[id];
            if (index.HasValue)
            {
                entity = Entities[index.Value];
                return true;
            }
        }

        entity = null;
        return false;
    }

    /// <summary>
    /// Check whether an entity with the given ID is currently registered.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(int id)
    {
        return id >= 0 && id < _idToIndex.Count && _idToIndex[id].HasValue;
    }

    /// <summary>
    /// Remove all entities and reset ID allocation.
    /// </summary>
    public void Clear()
    {
        Entities.Clear();
        _idToIndex.Clear();
        _freeIds.Clear();
        _bsgIdToEntity.Clear();
    }
}
