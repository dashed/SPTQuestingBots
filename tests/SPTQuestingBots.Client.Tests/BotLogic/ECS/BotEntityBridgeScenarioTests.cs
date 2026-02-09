using System;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS
{
    /// <summary>
    /// Integration-style tests that verify the call patterns BotEntityBridge uses.
    /// These exercise BotRegistry + BotEntity + HiveMindSystem in the same sequences
    /// as the bridge, without requiring game types (BotOwner).
    /// </summary>
    [TestFixture]
    public class BotEntityBridgeScenarioTests
    {
        private BotRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _registry = new BotRegistry(16);
        }

        // ── Registration Scenario ─────────────────────────────

        [Test]
        public void RegisterBot_CreatesEntityWithType()
        {
            var entity = _registry.Add();
            entity.BotType = BotType.PMC;

            Assert.That(entity.IsActive, Is.True);
            Assert.That(entity.BotType, Is.EqualTo(BotType.PMC));
            Assert.That(_registry.Count, Is.EqualTo(1));
        }

        [Test]
        public void RegisterBot_MultipleBotsGetUniqueIds()
        {
            var e1 = _registry.Add();
            e1.BotType = BotType.PMC;
            var e2 = _registry.Add();
            e2.BotType = BotType.Scav;
            var e3 = _registry.Add();
            e3.BotType = BotType.Boss;

            Assert.That(e1.Id, Is.Not.EqualTo(e2.Id));
            Assert.That(e2.Id, Is.Not.EqualTo(e3.Id));
            Assert.That(_registry.Count, Is.EqualTo(3));
        }

        // ── Sleeping Scenario ─────────────────────────────────

        [Test]
        public void SetSleeping_SetsEntitySleepFlag()
        {
            var entity = _registry.Add();
            Assert.That(entity.IsSleeping, Is.False);

            entity.IsSleeping = true;
            Assert.That(entity.IsSleeping, Is.True);

            entity.IsSleeping = false;
            Assert.That(entity.IsSleeping, Is.False);
        }

        // ── Sensor Update Scenario ────────────────────────────

        [Test]
        public void UpdateSensor_WritesAllSensorTypes()
        {
            var entity = _registry.Add();

            entity.SetSensor(BotSensor.InCombat, true);
            Assert.That(entity.IsInCombat, Is.True);

            entity.SetSensor(BotSensor.IsSuspicious, true);
            Assert.That(entity.IsSuspicious, Is.True);

            entity.SetSensor(BotSensor.CanQuest, true);
            Assert.That(entity.CanQuest, Is.True);

            entity.SetSensor(BotSensor.CanSprintToObjective, false);
            Assert.That(entity.CanSprintToObjective, Is.False);

            entity.SetSensor(BotSensor.WantsToLoot, true);
            Assert.That(entity.WantsToLoot, Is.True);
        }

        [Test]
        public void UpdateSensor_ReadsBackViaGetSensor()
        {
            var entity = _registry.Add();

            entity.SetSensor(BotSensor.InCombat, true);
            Assert.That(entity.GetSensor(BotSensor.InCombat), Is.True);

            entity.SetSensor(BotSensor.InCombat, false);
            Assert.That(entity.GetSensor(BotSensor.InCombat), Is.False);
        }

        // ── Boss/Follower Scenario ────────────────────────────

        [Test]
        public void SyncBossFollower_EstablishesRelationship()
        {
            var boss = _registry.Add();
            boss.BotType = BotType.Boss;
            var follower = _registry.Add();
            follower.BotType = BotType.Scav;

            HiveMindSystem.AssignBoss(follower, boss);

            Assert.That(follower.Boss, Is.SameAs(boss));
            Assert.That(boss.Followers, Contains.Item(follower));
        }

        [Test]
        public void SyncBossFollower_MultipleFollowers()
        {
            var boss = _registry.Add();
            boss.BotType = BotType.PMC;
            var f1 = _registry.Add();
            var f2 = _registry.Add();
            var f3 = _registry.Add();

            HiveMindSystem.AssignBoss(f1, boss);
            HiveMindSystem.AssignBoss(f2, boss);
            HiveMindSystem.AssignBoss(f3, boss);

            Assert.That(boss.Followers.Count, Is.EqualTo(3));
            Assert.That(f1.Boss, Is.SameAs(boss));
            Assert.That(f2.Boss, Is.SameAs(boss));
            Assert.That(f3.Boss, Is.SameAs(boss));
        }

        // ── Group Separation Scenario ─────────────────────────

        [Test]
        public void SeparateFromGroup_DetachesBossAndFollowers()
        {
            var boss = _registry.Add();
            var f1 = _registry.Add();
            var f2 = _registry.Add();

            HiveMindSystem.AssignBoss(f1, boss);
            HiveMindSystem.AssignBoss(f2, boss);

            // Separate boss from group — should clear all relationships
            HiveMindSystem.SeparateFromGroup(boss);

            Assert.That(boss.Followers.Count, Is.EqualTo(0));
            Assert.That(f1.Boss, Is.Null);
            Assert.That(f2.Boss, Is.Null);
        }

        [Test]
        public void SeparateFromGroup_DetachesFollowerOnly()
        {
            var boss = _registry.Add();
            var f1 = _registry.Add();
            var f2 = _registry.Add();

            HiveMindSystem.AssignBoss(f1, boss);
            HiveMindSystem.AssignBoss(f2, boss);

            // Separate only f1 — f2 should still follow boss
            HiveMindSystem.SeparateFromGroup(f1);

            Assert.That(f1.Boss, Is.Null);
            Assert.That(boss.Followers, Does.Not.Contain(f1));
            Assert.That(f2.Boss, Is.SameAs(boss));
            Assert.That(boss.Followers, Contains.Item(f2));
        }

        // ── Deactivation Scenario ─────────────────────────────

        [Test]
        public void DeactivateBot_SetsInactiveFlagAndCleansUpRelationships()
        {
            var boss = _registry.Add();
            var f1 = _registry.Add();

            HiveMindSystem.AssignBoss(f1, boss);

            // Simulate deactivation + cleanup (what BotEntityBridge.DeactivateBot does)
            boss.IsActive = false;
            HiveMindSystem.CleanupDeadEntities(_registry.Entities);

            Assert.That(boss.IsActive, Is.False);
            Assert.That(boss.Followers.Count, Is.EqualTo(0));
            Assert.That(f1.Boss, Is.Null);
        }

        // ── Clear Scenario ────────────────────────────────────

        [Test]
        public void Clear_ResetsAllState()
        {
            var e1 = _registry.Add();
            e1.BotType = BotType.PMC;
            e1.IsInCombat = true;
            var e2 = _registry.Add();
            e2.BotType = BotType.Boss;

            _registry.Clear();

            Assert.That(_registry.Count, Is.EqualTo(0));
        }

        // ── Full Lifecycle Scenario ───────────────────────────

        [Test]
        public void FullLifecycle_RegisterSensorBossFollowerSeparateClear()
        {
            // 1. Register three bots
            var boss = _registry.Add();
            boss.BotType = BotType.PMC;
            var f1 = _registry.Add();
            f1.BotType = BotType.PMC;
            var f2 = _registry.Add();
            f2.BotType = BotType.PMC;

            Assert.That(_registry.Count, Is.EqualTo(3));

            // 2. Set sensor values
            boss.SetSensor(BotSensor.InCombat, true);
            f1.SetSensor(BotSensor.CanQuest, true);

            Assert.That(boss.IsInCombat, Is.True);
            Assert.That(f1.CanQuest, Is.True);

            // 3. Establish boss-follower
            HiveMindSystem.AssignBoss(f1, boss);
            HiveMindSystem.AssignBoss(f2, boss);

            Assert.That(boss.Followers.Count, Is.EqualTo(2));

            // 4. Group sensor queries work
            Assert.That(f1.CheckSensorForBoss(BotSensor.InCombat), Is.True);
            Assert.That(boss.CheckSensorForAnyFollower(BotSensor.CanQuest), Is.True);

            // 5. Put f2 to sleep
            f2.IsSleeping = true;
            Assert.That(HiveMindSystem.CountActive(_registry.Entities), Is.EqualTo(2));

            // 6. Separate f1
            HiveMindSystem.SeparateFromGroup(f1);
            Assert.That(boss.Followers.Count, Is.EqualTo(1));
            Assert.That(f1.Boss, Is.Null);

            // 7. Clear everything
            _registry.Clear();
            Assert.That(_registry.Count, Is.EqualTo(0));
        }

        // ── MapBotType Coverage ───────────────────────────────
        // These verify the enum value alignment between Controllers.BotType and ECS.BotType.
        // Since we can't reference Controllers.BotType in net9.0 tests, we verify
        // the ECS BotType enum has the expected values.

        [Test]
        public void BotType_HasExpectedValues()
        {
            Assert.That((int)BotType.Unknown, Is.EqualTo(0));
            Assert.That((int)BotType.PMC, Is.EqualTo(1));
            Assert.That((int)BotType.Scav, Is.EqualTo(2));
            Assert.That((int)BotType.PScav, Is.EqualTo(3));
            Assert.That((int)BotType.Boss, Is.EqualTo(4));
        }

        // ── MapSensorType Coverage ────────────────────────────

        [Test]
        public void BotSensor_HasExpectedValues()
        {
            Assert.That((int)BotSensor.InCombat, Is.EqualTo(0));
            Assert.That((int)BotSensor.IsSuspicious, Is.EqualTo(1));
            Assert.That((int)BotSensor.CanQuest, Is.EqualTo(2));
            Assert.That((int)BotSensor.CanSprintToObjective, Is.EqualTo(3));
            Assert.That((int)BotSensor.WantsToLoot, Is.EqualTo(4));
        }

        // ── CountActive With Types ────────────────────────────

        [Test]
        public void CountActiveByType_RespectsTypeAndSleeping()
        {
            var pmc1 = _registry.Add();
            pmc1.BotType = BotType.PMC;
            var pmc2 = _registry.Add();
            pmc2.BotType = BotType.PMC;
            pmc2.IsSleeping = true;
            var scav = _registry.Add();
            scav.BotType = BotType.Scav;
            var boss = _registry.Add();
            boss.BotType = BotType.Boss;
            boss.IsActive = false;

            Assert.That(HiveMindSystem.CountActive(_registry.Entities), Is.EqualTo(2)); // pmc1 + scav
            Assert.That(HiveMindSystem.CountActiveByType(_registry.Entities, BotType.PMC), Is.EqualTo(1)); // pmc1 only
            Assert.That(HiveMindSystem.CountActiveByType(_registry.Entities, BotType.Boss), Is.EqualTo(0)); // inactive
            Assert.That(HiveMindSystem.CountActiveByType(_registry.Entities, BotType.Scav), Is.EqualTo(1));
        }

        // ── ECS Read Path Tests (verify caller-facing read patterns) ──

        [Test]
        public void GetSensorForBossOfBot_ReturnsBossSensorState()
        {
            var boss = _registry.Add();
            boss.BotType = BotType.PMC;
            var follower = _registry.Add();
            follower.BotType = BotType.PMC;

            HiveMindSystem.AssignBoss(follower, boss);

            // Boss is questing
            boss.SetSensor(BotSensor.CanQuest, true);

            // Follower reads boss's sensor via CheckSensorForBoss
            Assert.That(follower.CheckSensorForBoss(BotSensor.CanQuest), Is.True);
            Assert.That(follower.CheckSensorForBoss(BotSensor.InCombat), Is.False);
        }

        [Test]
        public void GetSensorForBossOfBot_ReturnsFalseWhenNoBoss()
        {
            var solo = _registry.Add();
            solo.BotType = BotType.PMC;

            // Solo bot has no boss — should return false
            Assert.That(solo.CheckSensorForBoss(BotSensor.CanQuest), Is.False);
            Assert.That(solo.CheckSensorForBoss(BotSensor.InCombat), Is.False);
        }

        [Test]
        public void GetSensorForGroup_DetectsBossSensor()
        {
            var boss = _registry.Add();
            boss.BotType = BotType.PMC;
            var f1 = _registry.Add();
            var f2 = _registry.Add();

            HiveMindSystem.AssignBoss(f1, boss);
            HiveMindSystem.AssignBoss(f2, boss);

            // Boss enters combat
            boss.SetSensor(BotSensor.InCombat, true);

            // All group members should see InCombat via CheckSensorForGroup
            Assert.That(f1.CheckSensorForGroup(BotSensor.InCombat), Is.True);
            Assert.That(f2.CheckSensorForGroup(BotSensor.InCombat), Is.True);
            Assert.That(boss.CheckSensorForGroup(BotSensor.InCombat), Is.True);
        }

        [Test]
        public void GetSensorForGroup_DetectsFollowerSensor()
        {
            var boss = _registry.Add();
            var f1 = _registry.Add();
            var f2 = _registry.Add();

            HiveMindSystem.AssignBoss(f1, boss);
            HiveMindSystem.AssignBoss(f2, boss);

            // Only f1 is suspicious
            f1.SetSensor(BotSensor.IsSuspicious, true);

            // Group check should detect it from any member
            Assert.That(boss.CheckSensorForGroup(BotSensor.IsSuspicious), Is.True);
            Assert.That(f2.CheckSensorForGroup(BotSensor.IsSuspicious), Is.True);
        }

        [Test]
        public void GetSensorForGroup_ReturnsFalseWhenNoGroupMemberHasSensor()
        {
            var boss = _registry.Add();
            var f1 = _registry.Add();

            HiveMindSystem.AssignBoss(f1, boss);

            // Nobody is in combat
            Assert.That(boss.CheckSensorForGroup(BotSensor.InCombat), Is.False);
            Assert.That(f1.CheckSensorForGroup(BotSensor.InCombat), Is.False);
        }

        [Test]
        public void LastLootingTime_DefaultIsMinValue()
        {
            var entity = _registry.Add();
            Assert.That(entity.LastLootingTime, Is.EqualTo(DateTime.MinValue));
        }

        [Test]
        public void LastLootingTime_UpdatedOnBossReturnsCorrectTime()
        {
            var boss = _registry.Add();
            var follower = _registry.Add();

            HiveMindSystem.AssignBoss(follower, boss);

            var lootTime = new DateTime(2025, 6, 15, 10, 30, 0);
            boss.LastLootingTime = lootTime;

            // Follower queries boss's last looting time
            Assert.That(follower.Boss, Is.Not.Null);
            Assert.That(follower.Boss.LastLootingTime, Is.EqualTo(lootTime));
        }

        [Test]
        public void LastLootingTime_NoBossReturnsMinValue()
        {
            var solo = _registry.Add();

            // Solo bot has no boss, so boss-based loot time check returns MinValue
            Assert.That(solo.Boss, Is.Null);
            Assert.That(solo.LastLootingTime, Is.EqualTo(DateTime.MinValue));
        }

        [Test]
        public void HasBoss_TrueAfterAssignment_FalseAfterSeparation()
        {
            var boss = _registry.Add();
            var follower = _registry.Add();

            Assert.That(follower.HasBoss, Is.False);

            HiveMindSystem.AssignBoss(follower, boss);
            Assert.That(follower.HasBoss, Is.True);

            HiveMindSystem.SeparateFromGroup(follower);
            Assert.That(follower.HasBoss, Is.False);
        }

        [Test]
        public void HasFollowers_TrueWithFollowers_FalseWithout()
        {
            var boss = _registry.Add();
            Assert.That(boss.HasFollowers, Is.False);

            var f1 = _registry.Add();
            HiveMindSystem.AssignBoss(f1, boss);
            Assert.That(boss.HasFollowers, Is.True);

            HiveMindSystem.SeparateFromGroup(f1);
            Assert.That(boss.HasFollowers, Is.False);
        }

        [Test]
        public void FollowerEnumeration_MatchesAssignedFollowers()
        {
            var boss = _registry.Add();
            var f1 = _registry.Add();
            var f2 = _registry.Add();
            var f3 = _registry.Add();

            HiveMindSystem.AssignBoss(f1, boss);
            HiveMindSystem.AssignBoss(f2, boss);
            HiveMindSystem.AssignBoss(f3, boss);

            Assert.That(boss.Followers.Count, Is.EqualTo(3));
            Assert.That(boss.Followers, Contains.Item(f1));
            Assert.That(boss.Followers, Contains.Item(f2));
            Assert.That(boss.Followers, Contains.Item(f3));

            // Separate one
            HiveMindSystem.SeparateFromGroup(f2);
            Assert.That(boss.Followers.Count, Is.EqualTo(2));
            Assert.That(boss.Followers, Does.Not.Contain(f2));
        }

        [Test]
        public void GetSensorForBot_ReadsOwnSensorState()
        {
            var entity = _registry.Add();

            entity.SetSensor(BotSensor.CanSprintToObjective, true);
            Assert.That(entity.GetSensor(BotSensor.CanSprintToObjective), Is.True);

            entity.SetSensor(BotSensor.CanSprintToObjective, false);
            Assert.That(entity.GetSensor(BotSensor.CanSprintToObjective), Is.False);
        }

        [Test]
        public void GetSensorForBot_IsSuspicious_ReadsCorrectly()
        {
            var entity = _registry.Add();

            Assert.That(entity.GetSensor(BotSensor.IsSuspicious), Is.False);

            entity.SetSensor(BotSensor.IsSuspicious, true);
            Assert.That(entity.GetSensor(BotSensor.IsSuspicious), Is.True);
        }

        [Test]
        public void GroupSensorQuery_SoloBot_ChecksOwnState()
        {
            var solo = _registry.Add();
            solo.SetSensor(BotSensor.InCombat, true);

            // Solo bot with no boss: CheckSensorForGroup checks self
            Assert.That(solo.CheckSensorForGroup(BotSensor.InCombat), Is.True);
        }
    }
}
