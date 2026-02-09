using System;
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

            // Phase 1 defaults
            Assert.AreEqual(42, entity.Id);
            Assert.IsTrue(entity.IsActive);
            Assert.IsNull(entity.Boss);
            Assert.IsNotNull(entity.Followers);
            Assert.AreEqual(0, entity.Followers.Count);

            // Phase 2 sensor defaults
            Assert.IsFalse(entity.IsInCombat);
            Assert.IsFalse(entity.IsSuspicious);
            Assert.IsFalse(entity.CanQuest);
            Assert.IsTrue(entity.CanSprintToObjective); // Note: default is TRUE
            Assert.IsFalse(entity.WantsToLoot);
            Assert.AreEqual(DateTime.MinValue, entity.LastLootingTime);

            // Phase 2 classification defaults
            Assert.AreEqual(BotType.Unknown, entity.BotType);
            Assert.IsFalse(entity.IsSleeping);
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
            Assert.That(str, Does.Contain("Type=Unknown"));
            Assert.That(str, Does.Contain("Active=True"));
            Assert.That(str, Does.Contain("Sleeping=False"));
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

        // ── Phase 2: Sensor State ───────────────────────────────

        [Test]
        public void SensorBools_CanBeToggled()
        {
            var entity = new BotEntity(0);

            entity.IsInCombat = true;
            Assert.IsTrue(entity.IsInCombat);

            entity.IsSuspicious = true;
            Assert.IsTrue(entity.IsSuspicious);

            entity.CanQuest = true;
            Assert.IsTrue(entity.CanQuest);

            entity.CanSprintToObjective = false;
            Assert.IsFalse(entity.CanSprintToObjective);

            entity.WantsToLoot = true;
            Assert.IsTrue(entity.WantsToLoot);
        }

        [Test]
        public void LastLootingTime_CanBeUpdated()
        {
            var entity = new BotEntity(0);
            var now = new DateTime(2026, 1, 15, 12, 0, 0);

            entity.LastLootingTime = now;
            Assert.AreEqual(now, entity.LastLootingTime);
        }

        // ── Phase 2: BotType + IsSleeping ───────────────────────

        [Test]
        public void BotType_CanBeAssigned()
        {
            var entity = new BotEntity(0);

            entity.BotType = BotType.PMC;
            Assert.AreEqual(BotType.PMC, entity.BotType);

            entity.BotType = BotType.Boss;
            Assert.AreEqual(BotType.Boss, entity.BotType);
        }

        [Test]
        public void BotType_AllValuesExist()
        {
            // Verify all expected enum values exist
            Assert.AreEqual(0, (int)BotType.Unknown);
            Assert.AreEqual(1, (int)BotType.PMC);
            Assert.AreEqual(2, (int)BotType.Scav);
            Assert.AreEqual(3, (int)BotType.PScav);
            Assert.AreEqual(4, (int)BotType.Boss);
        }

        [Test]
        public void IsSleeping_CanBeToggled()
        {
            var entity = new BotEntity(0);

            entity.IsSleeping = true;
            Assert.IsTrue(entity.IsSleeping);

            entity.IsSleeping = false;
            Assert.IsFalse(entity.IsSleeping);
        }

        // ── Phase 2: HasBoss / HasFollowers ─────────────────────

        [Test]
        public void HasBoss_FalseByDefault()
        {
            var entity = new BotEntity(0);
            Assert.IsFalse(entity.HasBoss);
        }

        [Test]
        public void HasBoss_TrueWhenAssigned()
        {
            var boss = new BotEntity(0);
            var follower = new BotEntity(1);
            follower.Boss = boss;

            Assert.IsTrue(follower.HasBoss);
        }

        [Test]
        public void HasFollowers_FalseByDefault()
        {
            var entity = new BotEntity(0);
            Assert.IsFalse(entity.HasFollowers);
        }

        [Test]
        public void HasFollowers_TrueWhenAdded()
        {
            var boss = new BotEntity(0);
            boss.Followers.Add(new BotEntity(1));

            Assert.IsTrue(boss.HasFollowers);
        }

        // ── Phase 2: GetSensor / SetSensor ──────────────────────

        [Test]
        public void GetSensor_ReturnsCorrectField()
        {
            var entity = new BotEntity(0);
            entity.IsInCombat = true;
            entity.IsSuspicious = true;
            entity.WantsToLoot = true;

            Assert.IsTrue(entity.GetSensor(BotSensor.InCombat));
            Assert.IsTrue(entity.GetSensor(BotSensor.IsSuspicious));
            Assert.IsFalse(entity.GetSensor(BotSensor.CanQuest));
            Assert.IsTrue(entity.GetSensor(BotSensor.CanSprintToObjective)); // default true
            Assert.IsTrue(entity.GetSensor(BotSensor.WantsToLoot));
        }

        [Test]
        public void SetSensor_UpdatesCorrectField()
        {
            var entity = new BotEntity(0);

            entity.SetSensor(BotSensor.InCombat, true);
            Assert.IsTrue(entity.IsInCombat);

            entity.SetSensor(BotSensor.IsSuspicious, true);
            Assert.IsTrue(entity.IsSuspicious);

            entity.SetSensor(BotSensor.CanQuest, true);
            Assert.IsTrue(entity.CanQuest);

            entity.SetSensor(BotSensor.CanSprintToObjective, false);
            Assert.IsFalse(entity.CanSprintToObjective);

            entity.SetSensor(BotSensor.WantsToLoot, true);
            Assert.IsTrue(entity.WantsToLoot);
        }

        [Test]
        public void GetSensor_InvalidEnum_ReturnsFalse()
        {
            var entity = new BotEntity(0);
            Assert.IsFalse(entity.GetSensor((BotSensor)999));
        }

        [Test]
        public void SetSensor_RoundTripsAllSensors()
        {
            var entity = new BotEntity(0);

            // Set all to true via enum, verify via direct fields
            foreach (BotSensor sensor in Enum.GetValues(typeof(BotSensor)))
            {
                entity.SetSensor(sensor, true);
                Assert.IsTrue(entity.GetSensor(sensor), $"Sensor {sensor} should be true after SetSensor(true)");
            }

            // Set all to false via enum, verify via direct fields
            foreach (BotSensor sensor in Enum.GetValues(typeof(BotSensor)))
            {
                entity.SetSensor(sensor, false);
                Assert.IsFalse(entity.GetSensor(sensor), $"Sensor {sensor} should be false after SetSensor(false)");
            }
        }

        // ── Phase 2: CheckSensorForBoss ─────────────────────────

        [Test]
        public void CheckSensorForBoss_NoBoss_ReturnsFalse()
        {
            var entity = new BotEntity(0);
            Assert.IsFalse(entity.CheckSensorForBoss(BotSensor.InCombat));
        }

        [Test]
        public void CheckSensorForBoss_BossHasValue_ReturnsTrue()
        {
            var boss = new BotEntity(0);
            var follower = new BotEntity(1);
            follower.Boss = boss;

            boss.IsInCombat = true;
            Assert.IsTrue(follower.CheckSensorForBoss(BotSensor.InCombat));
        }

        [Test]
        public void CheckSensorForBoss_BossDoesNotHaveValue_ReturnsFalse()
        {
            var boss = new BotEntity(0);
            var follower = new BotEntity(1);
            follower.Boss = boss;

            Assert.IsFalse(follower.CheckSensorForBoss(BotSensor.InCombat));
        }

        // ── Phase 2: CheckSensorForAnyFollower ──────────────────

        [Test]
        public void CheckSensorForAnyFollower_NoFollowers_ReturnsFalse()
        {
            var boss = new BotEntity(0);
            Assert.IsFalse(boss.CheckSensorForAnyFollower(BotSensor.InCombat));
        }

        [Test]
        public void CheckSensorForAnyFollower_OneHasValue_ReturnsTrue()
        {
            var boss = new BotEntity(0);
            var f1 = new BotEntity(1);
            var f2 = new BotEntity(2);
            boss.Followers.Add(f1);
            boss.Followers.Add(f2);

            f2.IsInCombat = true;
            Assert.IsTrue(boss.CheckSensorForAnyFollower(BotSensor.InCombat));
        }

        [Test]
        public void CheckSensorForAnyFollower_NoneHaveValue_ReturnsFalse()
        {
            var boss = new BotEntity(0);
            var f1 = new BotEntity(1);
            boss.Followers.Add(f1);

            Assert.IsFalse(boss.CheckSensorForAnyFollower(BotSensor.InCombat));
        }

        // ── Phase 2: CheckSensorForGroup ────────────────────────

        [Test]
        public void CheckSensorForGroup_BossHasValue_ReturnsTrue()
        {
            var boss = new BotEntity(0);
            var follower = new BotEntity(1);
            follower.Boss = boss;
            boss.Followers.Add(follower);

            boss.IsSuspicious = true;

            // From follower's perspective, group boss has the value
            Assert.IsTrue(follower.CheckSensorForGroup(BotSensor.IsSuspicious));
        }

        [Test]
        public void CheckSensorForGroup_FollowerHasValue_ReturnsTrue()
        {
            var boss = new BotEntity(0);
            var f1 = new BotEntity(1);
            var f2 = new BotEntity(2);
            f1.Boss = boss;
            f2.Boss = boss;
            boss.Followers.Add(f1);
            boss.Followers.Add(f2);

            f2.WantsToLoot = true;

            // From f1's perspective, a group member (f2) has the value
            Assert.IsTrue(f1.CheckSensorForGroup(BotSensor.WantsToLoot));
        }

        [Test]
        public void CheckSensorForGroup_NobodyHasValue_ReturnsFalse()
        {
            var boss = new BotEntity(0);
            var follower = new BotEntity(1);
            follower.Boss = boss;
            boss.Followers.Add(follower);

            Assert.IsFalse(follower.CheckSensorForGroup(BotSensor.InCombat));
        }

        [Test]
        public void CheckSensorForGroup_SoloBot_ChecksSelf()
        {
            var solo = new BotEntity(0);
            solo.IsInCombat = true;

            // Solo bot is its own group boss
            Assert.IsTrue(solo.CheckSensorForGroup(BotSensor.InCombat));
        }

        [Test]
        public void CheckSensorForGroup_SoloBot_NoValue_ReturnsFalse()
        {
            var solo = new BotEntity(0);
            Assert.IsFalse(solo.CheckSensorForGroup(BotSensor.InCombat));
        }

        // ── Phase 2: ToString with new fields ───────────────────

        [Test]
        public void ToString_ShowsBotType()
        {
            var entity = new BotEntity(1);
            entity.BotType = BotType.PMC;

            Assert.That(entity.ToString(), Does.Contain("Type=PMC"));
        }

        [Test]
        public void ToString_ShowsSleepingState()
        {
            var entity = new BotEntity(1);
            entity.IsSleeping = true;

            Assert.That(entity.ToString(), Does.Contain("Sleeping=True"));
        }
    }
}
