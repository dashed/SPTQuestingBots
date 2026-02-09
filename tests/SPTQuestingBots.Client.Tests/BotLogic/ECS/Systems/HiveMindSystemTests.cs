using System.Collections.Generic;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.Systems
{
    [TestFixture]
    public class HiveMindSystemTests
    {
        // ── ResetInactiveEntitySensors ──────────────────────────

        [Test]
        public void ResetInactiveEntitySensors_EmptyList_NoError()
        {
            HiveMindSystem.ResetInactiveEntitySensors(new List<BotEntity>());
        }

        [Test]
        public void ResetInactiveEntitySensors_ActiveEntity_Unchanged()
        {
            var entity = new BotEntity(0);
            entity.IsInCombat = true;
            entity.IsSuspicious = true;
            entity.CanQuest = true;
            entity.CanSprintToObjective = false;
            entity.WantsToLoot = true;

            HiveMindSystem.ResetInactiveEntitySensors(new List<BotEntity> { entity });

            Assert.IsTrue(entity.IsInCombat);
            Assert.IsTrue(entity.IsSuspicious);
            Assert.IsTrue(entity.CanQuest);
            Assert.IsFalse(entity.CanSprintToObjective);
            Assert.IsTrue(entity.WantsToLoot);
        }

        [Test]
        public void ResetInactiveEntitySensors_InactiveEntity_ResetToDefaults()
        {
            var entity = new BotEntity(0);
            entity.IsActive = false;
            entity.IsInCombat = true;
            entity.IsSuspicious = true;
            entity.CanQuest = true;
            entity.CanSprintToObjective = false;
            entity.WantsToLoot = true;

            HiveMindSystem.ResetInactiveEntitySensors(new List<BotEntity> { entity });

            Assert.IsFalse(entity.IsInCombat);
            Assert.IsFalse(entity.IsSuspicious);
            Assert.IsFalse(entity.CanQuest);
            Assert.IsTrue(entity.CanSprintToObjective); // default is true
            Assert.IsFalse(entity.WantsToLoot);
        }

        [Test]
        public void ResetInactiveEntitySensors_MixedList_OnlyResetsInactive()
        {
            var active = new BotEntity(0) { IsInCombat = true };
            var inactive = new BotEntity(1) { IsActive = false, IsInCombat = true };

            HiveMindSystem.ResetInactiveEntitySensors(new List<BotEntity> { active, inactive });

            Assert.IsTrue(active.IsInCombat);
            Assert.IsFalse(inactive.IsInCombat);
        }

        // ── CleanupDeadEntities ─────────────────────────────────

        [Test]
        public void CleanupDeadEntities_AllActive_NoChanges()
        {
            var boss = new BotEntity(0);
            var follower = new BotEntity(1);
            HiveMindSystem.AssignBoss(follower, boss);

            HiveMindSystem.CleanupDeadEntities(new List<BotEntity> { boss, follower });

            Assert.AreSame(boss, follower.Boss);
            Assert.Contains(follower, boss.Followers);
        }

        [Test]
        public void CleanupDeadEntities_DeadBoss_FollowersDetached()
        {
            var boss = new BotEntity(0);
            var f1 = new BotEntity(1);
            var f2 = new BotEntity(2);
            HiveMindSystem.AssignBoss(f1, boss);
            HiveMindSystem.AssignBoss(f2, boss);

            boss.IsActive = false;
            HiveMindSystem.CleanupDeadEntities(new List<BotEntity> { boss, f1, f2 });

            Assert.IsNull(f1.Boss);
            Assert.IsNull(f2.Boss);
            Assert.AreEqual(0, boss.Followers.Count);
        }

        [Test]
        public void CleanupDeadEntities_DeadFollower_RemovedFromBoss()
        {
            var boss = new BotEntity(0);
            var f1 = new BotEntity(1);
            var f2 = new BotEntity(2);
            HiveMindSystem.AssignBoss(f1, boss);
            HiveMindSystem.AssignBoss(f2, boss);

            f1.IsActive = false;
            HiveMindSystem.CleanupDeadEntities(new List<BotEntity> { boss, f1, f2 });

            Assert.AreEqual(1, boss.Followers.Count);
            Assert.AreSame(f2, boss.Followers[0]);
            Assert.IsNull(f1.Boss);
        }

        [Test]
        public void CleanupDeadEntities_DeadSolo_NoError()
        {
            var solo = new BotEntity(0) { IsActive = false };
            HiveMindSystem.CleanupDeadEntities(new List<BotEntity> { solo });
            // No boss or followers to clean up — just shouldn't throw
        }

        [Test]
        public void CleanupDeadEntities_EmptyList_NoError()
        {
            HiveMindSystem.CleanupDeadEntities(new List<BotEntity>());
        }

        // ── AssignBoss ──────────────────────────────────────────

        [Test]
        public void AssignBoss_Basic_BidirectionalLink()
        {
            var boss = new BotEntity(0);
            var follower = new BotEntity(1);

            HiveMindSystem.AssignBoss(follower, boss);

            Assert.AreSame(boss, follower.Boss);
            Assert.Contains(follower, boss.Followers);
        }

        [Test]
        public void AssignBoss_ReplaceBoss_OldBossUpdated()
        {
            var oldBoss = new BotEntity(0);
            var newBoss = new BotEntity(1);
            var follower = new BotEntity(2);

            HiveMindSystem.AssignBoss(follower, oldBoss);
            HiveMindSystem.AssignBoss(follower, newBoss);

            Assert.AreSame(newBoss, follower.Boss);
            Assert.AreEqual(0, oldBoss.Followers.Count);
            Assert.Contains(follower, newBoss.Followers);
        }

        [Test]
        public void AssignBoss_SameBossTwice_NoDuplicate()
        {
            var boss = new BotEntity(0);
            var follower = new BotEntity(1);

            HiveMindSystem.AssignBoss(follower, boss);
            HiveMindSystem.AssignBoss(follower, boss);

            Assert.AreEqual(1, boss.Followers.Count);
        }

        [Test]
        public void AssignBoss_NullArgs_NoError()
        {
            var entity = new BotEntity(0);

            HiveMindSystem.AssignBoss(null, entity);
            HiveMindSystem.AssignBoss(entity, null);
            HiveMindSystem.AssignBoss(null, null);
        }

        [Test]
        public void AssignBoss_SelfAssign_Ignored()
        {
            var entity = new BotEntity(0);

            HiveMindSystem.AssignBoss(entity, entity);

            Assert.IsNull(entity.Boss);
            Assert.AreEqual(0, entity.Followers.Count);
        }

        // ── RemoveBoss ──────────────────────────────────────────

        [Test]
        public void RemoveBoss_Basic_BidirectionalUnlink()
        {
            var boss = new BotEntity(0);
            var follower = new BotEntity(1);
            HiveMindSystem.AssignBoss(follower, boss);

            HiveMindSystem.RemoveBoss(follower);

            Assert.IsNull(follower.Boss);
            Assert.AreEqual(0, boss.Followers.Count);
        }

        [Test]
        public void RemoveBoss_NoBoss_NoError()
        {
            var entity = new BotEntity(0);
            HiveMindSystem.RemoveBoss(entity);
            Assert.IsNull(entity.Boss);
        }

        [Test]
        public void RemoveBoss_Null_NoError()
        {
            HiveMindSystem.RemoveBoss(null);
        }

        // ── SeparateFromGroup ───────────────────────────────────

        [Test]
        public void SeparateFromGroup_Boss_AllFollowersDetached()
        {
            var boss = new BotEntity(0);
            var f1 = new BotEntity(1);
            var f2 = new BotEntity(2);
            HiveMindSystem.AssignBoss(f1, boss);
            HiveMindSystem.AssignBoss(f2, boss);

            HiveMindSystem.SeparateFromGroup(boss);

            Assert.AreEqual(0, boss.Followers.Count);
            Assert.IsNull(f1.Boss);
            Assert.IsNull(f2.Boss);
        }

        [Test]
        public void SeparateFromGroup_Follower_DetachedFromBoss()
        {
            var boss = new BotEntity(0);
            var f1 = new BotEntity(1);
            var f2 = new BotEntity(2);
            HiveMindSystem.AssignBoss(f1, boss);
            HiveMindSystem.AssignBoss(f2, boss);

            HiveMindSystem.SeparateFromGroup(f1);

            Assert.IsNull(f1.Boss);
            Assert.AreEqual(1, boss.Followers.Count);
            Assert.AreSame(f2, boss.Followers[0]);
        }

        [Test]
        public void SeparateFromGroup_Solo_NoError()
        {
            var solo = new BotEntity(0);
            HiveMindSystem.SeparateFromGroup(solo);
        }

        [Test]
        public void SeparateFromGroup_Null_NoError()
        {
            HiveMindSystem.SeparateFromGroup(null);
        }

        [Test]
        public void SeparateFromGroup_BossWithBoss_BothLinksCleared()
        {
            // Entity is both a follower of someone and a boss of others
            var topBoss = new BotEntity(0);
            var midBoss = new BotEntity(1);
            var follower = new BotEntity(2);
            HiveMindSystem.AssignBoss(midBoss, topBoss);
            HiveMindSystem.AssignBoss(follower, midBoss);

            HiveMindSystem.SeparateFromGroup(midBoss);

            Assert.IsNull(midBoss.Boss);
            Assert.AreEqual(0, midBoss.Followers.Count);
            Assert.AreEqual(0, topBoss.Followers.Count);
            Assert.IsNull(follower.Boss);
        }

        // ── CountActive ─────────────────────────────────────────

        [Test]
        public void CountActive_EmptyList_ReturnsZero()
        {
            Assert.AreEqual(0, HiveMindSystem.CountActive(new List<BotEntity>()));
        }

        [Test]
        public void CountActive_AllActive_ReturnsAll()
        {
            var entities = new List<BotEntity> { new BotEntity(0), new BotEntity(1), new BotEntity(2) };

            Assert.AreEqual(3, HiveMindSystem.CountActive(entities));
        }

        [Test]
        public void CountActive_SomeInactive_ExcludesInactive()
        {
            var entities = new List<BotEntity>
            {
                new BotEntity(0),
                new BotEntity(1) { IsActive = false },
                new BotEntity(2),
            };

            Assert.AreEqual(2, HiveMindSystem.CountActive(entities));
        }

        [Test]
        public void CountActive_SomeSleeping_ExcludesSleeping()
        {
            var entities = new List<BotEntity>
            {
                new BotEntity(0),
                new BotEntity(1) { IsSleeping = true },
                new BotEntity(2),
            };

            Assert.AreEqual(2, HiveMindSystem.CountActive(entities));
        }

        [Test]
        public void CountActive_InactiveAndSleeping_ExcludesBoth()
        {
            var entities = new List<BotEntity>
            {
                new BotEntity(0) { IsActive = false },
                new BotEntity(1) { IsSleeping = true },
                new BotEntity(2),
            };

            Assert.AreEqual(1, HiveMindSystem.CountActive(entities));
        }

        // ── CountActiveByType ───────────────────────────────────

        [Test]
        public void CountActiveByType_EmptyList_ReturnsZero()
        {
            Assert.AreEqual(0, HiveMindSystem.CountActiveByType(new List<BotEntity>(), BotType.PMC));
        }

        [Test]
        public void CountActiveByType_FiltersCorrectly()
        {
            var entities = new List<BotEntity>
            {
                new BotEntity(0) { BotType = BotType.PMC },
                new BotEntity(1) { BotType = BotType.PMC },
                new BotEntity(2) { BotType = BotType.Scav },
                new BotEntity(3) { BotType = BotType.Boss },
            };

            Assert.AreEqual(2, HiveMindSystem.CountActiveByType(entities, BotType.PMC));
            Assert.AreEqual(1, HiveMindSystem.CountActiveByType(entities, BotType.Scav));
            Assert.AreEqual(1, HiveMindSystem.CountActiveByType(entities, BotType.Boss));
            Assert.AreEqual(0, HiveMindSystem.CountActiveByType(entities, BotType.PScav));
        }

        [Test]
        public void CountActiveByType_ExcludesInactiveAndSleeping()
        {
            var entities = new List<BotEntity>
            {
                new BotEntity(0) { BotType = BotType.PMC },
                new BotEntity(1) { BotType = BotType.PMC, IsActive = false },
                new BotEntity(2) { BotType = BotType.PMC, IsSleeping = true },
            };

            Assert.AreEqual(1, HiveMindSystem.CountActiveByType(entities, BotType.PMC));
        }
    }
}
