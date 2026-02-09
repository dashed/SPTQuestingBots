using System.Collections.Generic;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS
{
    [TestFixture]
    public class BotRegistryTests
    {
        private BotRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _registry = new BotRegistry();
        }

        // ── Add ──────────────────────────────────────────────────

        [Test]
        public void Add_ReturnsEntityWithIncrementingIds()
        {
            var e0 = _registry.Add();
            var e1 = _registry.Add();
            var e2 = _registry.Add();

            Assert.AreEqual(0, e0.Id);
            Assert.AreEqual(1, e1.Id);
            Assert.AreEqual(2, e2.Id);
        }

        [Test]
        public void Add_EntityIsActiveByDefault()
        {
            var entity = _registry.Add();
            Assert.IsTrue(entity.IsActive);
        }

        [Test]
        public void Add_IncrementsCount()
        {
            Assert.AreEqual(0, _registry.Count);

            _registry.Add();
            Assert.AreEqual(1, _registry.Count);

            _registry.Add();
            Assert.AreEqual(2, _registry.Count);
        }

        [Test]
        public void Add_EntityAppearsInEntitiesList()
        {
            var entity = _registry.Add();
            Assert.Contains(entity, _registry.Entities);
        }

        // ── Indexer (lookup by ID) ───────────────────────────────

        [Test]
        public void Indexer_ReturnsCorrectEntity()
        {
            var e0 = _registry.Add();
            var e1 = _registry.Add();

            Assert.AreSame(e0, _registry[0]);
            Assert.AreSame(e1, _registry[1]);
        }

        [Test]
        public void Indexer_InvalidId_Throws()
        {
            _registry.Add();

            Assert.Throws<KeyNotFoundException>(() =>
            {
                var _ = _registry[99];
            });
            Assert.Throws<KeyNotFoundException>(() =>
            {
                var _ = _registry[-1];
            });
        }

        // ── TryGetById ──────────────────────────────────────────

        [Test]
        public void TryGetById_ValidId_ReturnsTrue()
        {
            var entity = _registry.Add();

            Assert.IsTrue(_registry.TryGetById(entity.Id, out var found));
            Assert.AreSame(entity, found);
        }

        [Test]
        public void TryGetById_InvalidId_ReturnsFalse()
        {
            Assert.IsFalse(_registry.TryGetById(0, out var found));
            Assert.IsNull(found);

            Assert.IsFalse(_registry.TryGetById(-1, out found));
            Assert.IsNull(found);
        }

        [Test]
        public void TryGetById_RemovedId_ReturnsFalse()
        {
            var entity = _registry.Add();
            _registry.Remove(entity);

            Assert.IsFalse(_registry.TryGetById(entity.Id, out var found));
            Assert.IsNull(found);
        }

        // ── Contains ────────────────────────────────────────────

        [Test]
        public void Contains_RegisteredId_ReturnsTrue()
        {
            var entity = _registry.Add();
            Assert.IsTrue(_registry.Contains(entity.Id));
        }

        [Test]
        public void Contains_UnregisteredId_ReturnsFalse()
        {
            Assert.IsFalse(_registry.Contains(0));
            Assert.IsFalse(_registry.Contains(-1));
            Assert.IsFalse(_registry.Contains(999));
        }

        // ── Remove (basic) ──────────────────────────────────────

        [Test]
        public void Remove_ValidEntity_ReturnsTrue()
        {
            var entity = _registry.Add();
            Assert.IsTrue(_registry.Remove(entity));
        }

        [Test]
        public void Remove_DecrementsCount()
        {
            var e0 = _registry.Add();
            _registry.Add();
            Assert.AreEqual(2, _registry.Count);

            _registry.Remove(e0);
            Assert.AreEqual(1, _registry.Count);
        }

        [Test]
        public void Remove_Null_ReturnsFalse()
        {
            Assert.IsFalse(_registry.Remove(null));
        }

        [Test]
        public void Remove_AlreadyRemoved_ReturnsFalse()
        {
            var entity = _registry.Add();
            _registry.Remove(entity);

            Assert.IsFalse(_registry.Remove(entity));
        }

        [Test]
        public void Remove_EntityNotInRegistry_ReturnsFalse()
        {
            var foreign = new BotEntity(999);
            Assert.IsFalse(_registry.Remove(foreign));
        }

        [Test]
        public void Remove_LastElement_NoSwapNeeded()
        {
            var e0 = _registry.Add();
            var e1 = _registry.Add();

            _registry.Remove(e1);

            Assert.AreEqual(1, _registry.Count);
            Assert.AreSame(e0, _registry.Entities[0]);
            Assert.AreSame(e0, _registry[0]);
        }

        // ── Swap-Remove ─────────────────────────────────────────

        [Test]
        public void Remove_MiddleElement_SwapsLastIntoGap()
        {
            var e0 = _registry.Add(); // id=0
            var e1 = _registry.Add(); // id=1
            var e2 = _registry.Add(); // id=2

            // Remove the first entity — e2 should be swapped into index 0
            _registry.Remove(e0);

            Assert.AreEqual(2, _registry.Count);

            // Dense list should now be [e2, e1] (last was swapped into gap)
            Assert.AreSame(e2, _registry.Entities[0]);
            Assert.AreSame(e1, _registry.Entities[1]);

            // ID lookups still work correctly
            Assert.AreSame(e1, _registry[1]);
            Assert.AreSame(e2, _registry[2]);
            Assert.IsFalse(_registry.Contains(0));
        }

        [Test]
        public void Remove_FirstOfTwo_SwapsCorrectly()
        {
            var e0 = _registry.Add(); // id=0
            var e1 = _registry.Add(); // id=1

            _registry.Remove(e0);

            Assert.AreEqual(1, _registry.Count);
            Assert.AreSame(e1, _registry.Entities[0]);
            Assert.AreSame(e1, _registry[1]);
        }

        [Test]
        public void Remove_OnlyEntity_EmptiesRegistry()
        {
            var entity = _registry.Add();
            _registry.Remove(entity);

            Assert.AreEqual(0, _registry.Count);
            Assert.IsFalse(_registry.Contains(entity.Id));
        }

        [Test]
        public void SwapRemove_PreservesAllLookups_AfterMultipleRemoves()
        {
            // Add 5 entities
            var entities = new BotEntity[5];
            for (int i = 0; i < 5; i++)
                entities[i] = _registry.Add();

            // Remove middle entity (id=2)
            _registry.Remove(entities[2]);
            Assert.AreEqual(4, _registry.Count);
            Assert.IsFalse(_registry.Contains(2));

            // All remaining entities should still be findable by ID
            Assert.AreSame(entities[0], _registry[0]);
            Assert.AreSame(entities[1], _registry[1]);
            Assert.AreSame(entities[3], _registry[3]);
            Assert.AreSame(entities[4], _registry[4]);

            // Remove first entity (id=0)
            _registry.Remove(entities[0]);
            Assert.AreEqual(3, _registry.Count);

            // Remaining 3 still findable
            Assert.AreSame(entities[1], _registry[1]);
            Assert.AreSame(entities[3], _registry[3]);
            // entities[4] was previously swapped when entities[2] was removed
            Assert.IsTrue(_registry.Contains(4));
        }

        // ── ID Recycling ────────────────────────────────────────

        [Test]
        public void IdRecycling_FreedIdIsReused()
        {
            var e0 = _registry.Add(); // id=0
            var e1 = _registry.Add(); // id=1

            _registry.Remove(e0); // frees id=0

            var e2 = _registry.Add(); // should recycle id=0
            Assert.AreEqual(0, e2.Id);
        }

        [Test]
        public void IdRecycling_StackOrder_LastFreedFirst()
        {
            var e0 = _registry.Add(); // id=0
            var e1 = _registry.Add(); // id=1
            var e2 = _registry.Add(); // id=2

            _registry.Remove(e0); // free id=0
            _registry.Remove(e1); // free id=1

            // Stack is LIFO: id=1 popped first, then id=0
            var r1 = _registry.Add();
            var r2 = _registry.Add();

            Assert.AreEqual(1, r1.Id);
            Assert.AreEqual(0, r2.Id);
        }

        [Test]
        public void IdRecycling_RecycledEntityIsFullyFunctional()
        {
            var original = _registry.Add(); // id=0
            _registry.Remove(original);

            var recycled = _registry.Add(); // should get id=0
            Assert.AreEqual(0, recycled.Id);
            Assert.IsTrue(recycled.IsActive);
            Assert.AreSame(recycled, _registry[0]);
            Assert.IsTrue(_registry.Contains(0));
        }

        [Test]
        public void IdRecycling_AfterRemoveAll_IdsStartFresh()
        {
            _registry.Add();
            _registry.Add();
            _registry.Clear();

            var fresh = _registry.Add();
            Assert.AreEqual(0, fresh.Id);
        }

        // ── Dense Iteration ─────────────────────────────────────

        [Test]
        public void Entities_IsDenseAfterRemoves()
        {
            for (int i = 0; i < 10; i++)
                _registry.Add();

            // Remove every other entity
            for (int i = 0; i < 10; i += 2)
            {
                _registry.TryGetById(i, out var entity);
                if (entity != null)
                    _registry.Remove(entity);
            }

            Assert.AreEqual(5, _registry.Count);
            Assert.AreEqual(5, _registry.Entities.Count);

            // Every slot in the dense list should be non-null and valid
            for (int i = 0; i < _registry.Entities.Count; i++)
            {
                var entity = _registry.Entities[i];
                Assert.IsNotNull(entity);
                Assert.IsTrue(_registry.Contains(entity.Id));
            }
        }

        [Test]
        public void Entities_CanBeIteratedWithForLoop()
        {
            _registry.Add();
            _registry.Add();
            _registry.Add();

            int count = 0;
            for (int i = 0; i < _registry.Entities.Count; i++)
            {
                Assert.IsNotNull(_registry.Entities[i]);
                count++;
            }

            Assert.AreEqual(3, count);
        }

        // ── Clear ───────────────────────────────────────────────

        [Test]
        public void Clear_ResetsEverything()
        {
            _registry.Add();
            _registry.Add();
            _registry.Add();

            _registry.Clear();

            Assert.AreEqual(0, _registry.Count);
            Assert.AreEqual(0, _registry.Entities.Count);
            Assert.IsFalse(_registry.Contains(0));
            Assert.IsFalse(_registry.Contains(1));
            Assert.IsFalse(_registry.Contains(2));
        }

        [Test]
        public void Clear_ThenAdd_WorksCorrectly()
        {
            _registry.Add();
            _registry.Clear();

            var entity = _registry.Add();
            Assert.AreEqual(0, entity.Id);
            Assert.AreEqual(1, _registry.Count);
            Assert.AreSame(entity, _registry[0]);
        }

        // ── Stress / Integration ────────────────────────────────

        [Test]
        public void StressTest_AddRemoveRecycleCycle()
        {
            // Simulate a game session: add 40 bots, remove some, add more
            var alive = new List<BotEntity>();

            // Phase 1: Add 40 bots
            for (int i = 0; i < 40; i++)
                alive.Add(_registry.Add());

            Assert.AreEqual(40, _registry.Count);

            // Phase 2: Remove 15 bots (indices 5, 10, 15, ...)
            for (int i = 0; i < 15; i++)
            {
                var toRemove = alive[0];
                alive.RemoveAt(0);
                _registry.Remove(toRemove);
            }

            Assert.AreEqual(25, _registry.Count);

            // Phase 3: Add 10 more (should recycle IDs)
            for (int i = 0; i < 10; i++)
                alive.Add(_registry.Add());

            Assert.AreEqual(35, _registry.Count);

            // Verify: all alive entities are findable by ID
            foreach (var entity in alive)
            {
                Assert.IsTrue(_registry.Contains(entity.Id));
                Assert.AreSame(entity, _registry[entity.Id]);
            }

            // Verify: dense list matches count
            Assert.AreEqual(35, _registry.Entities.Count);
        }

        [Test]
        public void StressTest_RemoveAllOneByOne()
        {
            var entities = new List<BotEntity>();
            for (int i = 0; i < 20; i++)
                entities.Add(_registry.Add());

            // Remove all entities one at a time
            foreach (var entity in entities)
                _registry.Remove(entity);

            Assert.AreEqual(0, _registry.Count);
        }

        // ── Constructor ─────────────────────────────────────────

        [Test]
        public void Constructor_Default_StartsEmpty()
        {
            var registry = new BotRegistry();
            Assert.AreEqual(0, registry.Count);
        }

        [Test]
        public void Constructor_CustomCapacity_StartsEmpty()
        {
            var registry = new BotRegistry(64);
            Assert.AreEqual(0, registry.Count);
        }
    }
}
