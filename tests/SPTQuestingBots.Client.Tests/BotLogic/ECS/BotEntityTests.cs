using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS
{
    [TestFixture]
    public class BotEntityTests
    {
        [Test]
        public void Constructor_SetsIdAndDefaults()
        {
            var entity = new BotEntity(42);

            Assert.AreEqual(42, entity.Id);
            Assert.IsTrue(entity.IsActive);
            Assert.IsNull(entity.Boss);
            Assert.IsNotNull(entity.Followers);
            Assert.AreEqual(0, entity.Followers.Count);
        }

        [Test]
        public void IsActive_CanBeToggled()
        {
            var entity = new BotEntity(0);

            entity.IsActive = false;
            Assert.IsFalse(entity.IsActive);

            entity.IsActive = true;
            Assert.IsTrue(entity.IsActive);
        }

        [Test]
        public void Boss_CanBeAssigned()
        {
            var boss = new BotEntity(0);
            var follower = new BotEntity(1);

            follower.Boss = boss;
            Assert.AreSame(boss, follower.Boss);
        }

        [Test]
        public void Followers_CanBeAddedAndRemoved()
        {
            var boss = new BotEntity(0);
            var f1 = new BotEntity(1);
            var f2 = new BotEntity(2);

            boss.Followers.Add(f1);
            boss.Followers.Add(f2);
            Assert.AreEqual(2, boss.Followers.Count);

            boss.Followers.Remove(f1);
            Assert.AreEqual(1, boss.Followers.Count);
            Assert.AreSame(f2, boss.Followers[0]);
        }

        [Test]
        public void BossFollower_BidirectionalSetup()
        {
            var boss = new BotEntity(0);
            var follower = new BotEntity(1);

            follower.Boss = boss;
            boss.Followers.Add(follower);

            Assert.AreSame(boss, follower.Boss);
            Assert.Contains(follower, boss.Followers);
        }

        [Test]
        public void Equals_SameId_ReturnsTrue()
        {
            var a = new BotEntity(5);
            var b = new BotEntity(5);

            Assert.IsTrue(a.Equals(b));
            Assert.IsTrue(b.Equals(a));
        }

        [Test]
        public void Equals_DifferentId_ReturnsFalse()
        {
            var a = new BotEntity(1);
            var b = new BotEntity(2);

            Assert.IsFalse(a.Equals(b));
        }

        [Test]
        public void Equals_Null_ReturnsFalse()
        {
            var entity = new BotEntity(0);

            Assert.IsFalse(entity.Equals(null));
        }

        [Test]
        public void Equals_Object_WorksCorrectly()
        {
            var a = new BotEntity(3);
            var b = new BotEntity(3);

            Assert.IsTrue(a.Equals((object)b));
            Assert.IsFalse(a.Equals("not an entity"));
            Assert.IsFalse(a.Equals((object)null));
        }

        [Test]
        public void GetHashCode_EqualsId()
        {
            var entity = new BotEntity(99);
            Assert.AreEqual(99, entity.GetHashCode());
        }

        [Test]
        public void GetHashCode_ConsistentWithEquals()
        {
            var a = new BotEntity(7);
            var b = new BotEntity(7);

            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void ToString_ContainsRelevantInfo()
        {
            var entity = new BotEntity(3);
            var str = entity.ToString();

            Assert.That(str, Does.Contain("Id=3"));
            Assert.That(str, Does.Contain("Active=True"));
            Assert.That(str, Does.Contain("Boss=none"));
            Assert.That(str, Does.Contain("Followers=0"));
        }

        [Test]
        public void ToString_WithBoss_ShowsBossId()
        {
            var boss = new BotEntity(10);
            var follower = new BotEntity(11);
            follower.Boss = boss;

            var str = follower.ToString();
            Assert.That(str, Does.Contain("Boss=10"));
        }

        [Test]
        public void CanBeUsedAsHashSetKey()
        {
            var set = new System.Collections.Generic.HashSet<BotEntity>();
            var a = new BotEntity(1);
            var b = new BotEntity(2);
            var a2 = new BotEntity(1);

            set.Add(a);
            set.Add(b);

            Assert.AreEqual(2, set.Count);
            Assert.IsTrue(set.Contains(a2)); // Same ID = same entity
        }

        [Test]
        public void CanBeUsedAsDictionaryKey()
        {
            var dict = new System.Collections.Generic.Dictionary<BotEntity, string>();
            var entity = new BotEntity(5);

            dict[entity] = "test";
            Assert.AreEqual("test", dict[new BotEntity(5)]);
        }
    }
}
