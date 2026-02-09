using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS
{
    /// <summary>
    /// Tests for the BsgBotRegistry sparse-array pattern on BotRegistry.
    /// Verifies O(1) external-ID → BotEntity lookup.
    /// </summary>
    [TestFixture]
    public class BsgBotRegistryTests
    {
        private BotRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _registry = new BotRegistry(16);
        }

        [Test]
        public void AddWithBsgId_RegistersEntityWithLookup()
        {
            var entity = _registry.Add(42);
            entity.BotType = BotType.PMC;

            Assert.That(_registry.GetByBsgId(42), Is.SameAs(entity));
        }

        [Test]
        public void GetByBsgId_ReturnsNullForUnregisteredId()
        {
            Assert.That(_registry.GetByBsgId(99), Is.Null);
        }

        [Test]
        public void GetByBsgId_ReturnsNullForNegativeId()
        {
            Assert.That(_registry.GetByBsgId(-1), Is.Null);
        }

        [Test]
        public void AddWithBsgId_MultipleBots_EachLookupWorks()
        {
            var e1 = _registry.Add(10);
            e1.BotType = BotType.PMC;
            var e2 = _registry.Add(20);
            e2.BotType = BotType.Scav;
            var e3 = _registry.Add(30);
            e3.BotType = BotType.Boss;

            Assert.That(_registry.GetByBsgId(10), Is.SameAs(e1));
            Assert.That(_registry.GetByBsgId(20), Is.SameAs(e2));
            Assert.That(_registry.GetByBsgId(30), Is.SameAs(e3));
        }

        [Test]
        public void AddWithBsgId_SparseIds_GrowsArray()
        {
            // Large ID gap — sparse array should grow to accommodate
            var entity = _registry.Add(1000);
            entity.BotType = BotType.PScav;

            Assert.That(_registry.GetByBsgId(1000), Is.SameAs(entity));
            // IDs in between are null
            Assert.That(_registry.GetByBsgId(500), Is.Null);
        }

        [Test]
        public void ClearBsgId_NullifiesLookup()
        {
            var entity = _registry.Add(42);
            Assert.That(_registry.GetByBsgId(42), Is.SameAs(entity));

            _registry.ClearBsgId(42);
            Assert.That(_registry.GetByBsgId(42), Is.Null);
        }

        [Test]
        public void ClearBsgId_DoesNotAffectOtherEntries()
        {
            var e1 = _registry.Add(10);
            var e2 = _registry.Add(20);

            _registry.ClearBsgId(10);

            Assert.That(_registry.GetByBsgId(10), Is.Null);
            Assert.That(_registry.GetByBsgId(20), Is.SameAs(e2));
        }

        [Test]
        public void ClearBsgId_SafeWithInvalidId()
        {
            // Should not throw
            _registry.ClearBsgId(-1);
            _registry.ClearBsgId(999);
        }

        [Test]
        public void Clear_ResetsAllBsgLookups()
        {
            _registry.Add(10);
            _registry.Add(20);
            _registry.Add(30);

            _registry.Clear();

            Assert.That(_registry.GetByBsgId(10), Is.Null);
            Assert.That(_registry.GetByBsgId(20), Is.Null);
            Assert.That(_registry.GetByBsgId(30), Is.Null);
            Assert.That(_registry.Count, Is.EqualTo(0));
        }

        [Test]
        public void AddWithBsgId_StillCountsInDenseList()
        {
            _registry.Add(10);
            _registry.Add(20);
            var e3 = _registry.Add(); // no bsg id

            Assert.That(_registry.Count, Is.EqualTo(3));
            Assert.That(_registry.GetByBsgId(10), Is.Not.Null);
            Assert.That(_registry.GetByBsgId(20), Is.Not.Null);
            // e3 has no bsg id, only retrievable by entity id
            Assert.That(_registry.TryGetById(e3.Id, out _), Is.True);
        }

        [Test]
        public void AddWithBsgId_EntityAlsoInDenseList()
        {
            var entity = _registry.Add(42);

            Assert.That(_registry.Entities, Contains.Item(entity));
            Assert.That(_registry.TryGetById(entity.Id, out var found), Is.True);
            Assert.That(found, Is.SameAs(entity));
        }

        [Test]
        public void AddWithBsgId_ConsecutiveIds_AllRetrievable()
        {
            for (int i = 0; i < 10; i++)
            {
                var e = _registry.Add(i);
                e.BotType = BotType.Scav;
            }

            Assert.That(_registry.Count, Is.EqualTo(10));
            for (int i = 0; i < 10; i++)
            {
                var found = _registry.GetByBsgId(i);
                Assert.That(found, Is.Not.Null);
                Assert.That(found.BotType, Is.EqualTo(BotType.Scav));
            }
        }
    }
}
