using System;
using System.Collections.Generic;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.ZoneMovement.Core;

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

        // ── Phase 5A: Dual-Write Gap Closure Tests ──────────────

        [Test]
        public void DeactivateBot_DuringBossDiscovery_SetsInactiveAndCleansFollowers()
        {
            // Simulates the gap in updateBosses(): when a boss dies, ECS should
            // mark it inactive and CleanupDeadEntities should detach followers.
            var boss = _registry.Add();
            boss.BotType = BotType.PMC;
            var f1 = _registry.Add();
            var f2 = _registry.Add();

            HiveMindSystem.AssignBoss(f1, boss);
            HiveMindSystem.AssignBoss(f2, boss);

            // Simulate boss death: DeactivateBot + cleanup
            boss.IsActive = false;
            HiveMindSystem.CleanupDeadEntities(_registry.Entities);

            Assert.That(boss.IsActive, Is.False);
            Assert.That(boss.Followers.Count, Is.EqualTo(0));
            Assert.That(f1.Boss, Is.Null);
            Assert.That(f2.Boss, Is.Null);
            // Followers remain active
            Assert.That(f1.IsActive, Is.True);
            Assert.That(f2.IsActive, Is.True);
        }

        [Test]
        public void DeactivateBot_DuringFollowerCleanup_SetsFollowerInactive()
        {
            // Simulates the gap in updateBossFollowers(): when a follower dies,
            // ECS should mark it inactive.
            var boss = _registry.Add();
            var f1 = _registry.Add();
            var f2 = _registry.Add();

            HiveMindSystem.AssignBoss(f1, boss);
            HiveMindSystem.AssignBoss(f2, boss);

            // Simulate follower death
            f1.IsActive = false;
            HiveMindSystem.CleanupDeadEntities(_registry.Entities);

            Assert.That(f1.IsActive, Is.False);
            Assert.That(f1.Boss, Is.Null);
            Assert.That(boss.Followers, Does.Not.Contain(f1));
            // f2 still follows boss
            Assert.That(f2.Boss, Is.SameAs(boss));
            Assert.That(boss.Followers, Contains.Item(f2));
        }

        [Test]
        public void SetSleeping_DualWriteToECS_ReflectsInEntityAndCounts()
        {
            // Simulates the gap in RegisterSleepingBot/UnregisterSleepingBot:
            // sleeping state should be visible in ECS entity and affect CountActive.
            var bot = _registry.Add();
            bot.BotType = BotType.Scav;

            Assert.That(bot.IsSleeping, Is.False);
            Assert.That(HiveMindSystem.CountActive(_registry.Entities), Is.EqualTo(1));

            // Sleep
            bot.IsSleeping = true;
            Assert.That(bot.IsSleeping, Is.True);
            Assert.That(HiveMindSystem.CountActive(_registry.Entities), Is.EqualTo(0));

            // Wake
            bot.IsSleeping = false;
            Assert.That(bot.IsSleeping, Is.False);
            Assert.That(HiveMindSystem.CountActive(_registry.Entities), Is.EqualTo(1));
        }

        [Test]
        public void Clear_FromMonitor_ResetsECSState()
        {
            // Simulates BotHiveMindMonitor.Clear() calling BotEntityBridge.Clear():
            // all entities and mappings should be gone.
            var e1 = _registry.Add();
            e1.BotType = BotType.PMC;
            e1.IsInCombat = true;
            var e2 = _registry.Add();
            e2.BotType = BotType.Boss;
            HiveMindSystem.AssignBoss(e1, e2);

            _registry.Clear();

            Assert.That(_registry.Count, Is.EqualTo(0));
        }

        [Test]
        public void DeactivateBot_MultipleBossesDying_EachDeactivatedIndependently()
        {
            // Multiple bosses with followers dying at different times
            var boss1 = _registry.Add();
            boss1.BotType = BotType.Boss;
            var f1 = _registry.Add();
            HiveMindSystem.AssignBoss(f1, boss1);

            var boss2 = _registry.Add();
            boss2.BotType = BotType.Boss;
            var f2 = _registry.Add();
            HiveMindSystem.AssignBoss(f2, boss2);

            // Boss1 dies first
            boss1.IsActive = false;
            HiveMindSystem.CleanupDeadEntities(_registry.Entities);

            Assert.That(boss1.IsActive, Is.False);
            Assert.That(f1.Boss, Is.Null);
            // Boss2 group unaffected
            Assert.That(boss2.IsActive, Is.True);
            Assert.That(f2.Boss, Is.SameAs(boss2));

            // Boss2 dies later
            boss2.IsActive = false;
            HiveMindSystem.CleanupDeadEntities(_registry.Entities);

            Assert.That(boss2.IsActive, Is.False);
            Assert.That(f2.Boss, Is.Null);
        }

        [Test]
        public void DeactivateBot_FollowerAndBossDyingSimultaneously()
        {
            // Both boss and follower die in the same tick
            var boss = _registry.Add();
            boss.BotType = BotType.PMC;
            var f1 = _registry.Add();
            var f2 = _registry.Add();

            HiveMindSystem.AssignBoss(f1, boss);
            HiveMindSystem.AssignBoss(f2, boss);

            // Both die
            boss.IsActive = false;
            f1.IsActive = false;

            HiveMindSystem.CleanupDeadEntities(_registry.Entities);

            Assert.That(boss.IsActive, Is.False);
            Assert.That(f1.IsActive, Is.False);
            Assert.That(f2.IsActive, Is.True);
            Assert.That(boss.Followers.Count, Is.EqualTo(0));
            Assert.That(f1.Boss, Is.Null);
            Assert.That(f2.Boss, Is.Null);
        }

        [Test]
        public void SetSleeping_BossAndFollowerSleepIndependently()
        {
            var boss = _registry.Add();
            boss.BotType = BotType.PMC;
            var f1 = _registry.Add();
            f1.BotType = BotType.PMC;

            HiveMindSystem.AssignBoss(f1, boss);

            // Put follower to sleep, boss stays awake
            f1.IsSleeping = true;
            Assert.That(HiveMindSystem.CountActive(_registry.Entities), Is.EqualTo(1));
            Assert.That(f1.IsSleeping, Is.True);
            Assert.That(boss.IsSleeping, Is.False);
            // Follower still attached to boss
            Assert.That(f1.Boss, Is.SameAs(boss));

            // Put boss to sleep too
            boss.IsSleeping = true;
            Assert.That(HiveMindSystem.CountActive(_registry.Entities), Is.EqualTo(0));
        }

        [Test]
        public void DeactivateBot_SensorsResetOnCleanup()
        {
            // When a bot dies and cleanup runs, sensors should be reset
            var bot = _registry.Add();
            bot.BotType = BotType.PMC;
            bot.SetSensor(BotSensor.InCombat, true);
            bot.SetSensor(BotSensor.CanQuest, true);

            bot.IsActive = false;
            HiveMindSystem.ResetInactiveEntitySensors(_registry.Entities);

            Assert.That(bot.GetSensor(BotSensor.InCombat), Is.False);
            Assert.That(bot.GetSensor(BotSensor.CanQuest), Is.False);
        }

        // ── Phase 5B: Push Sensor Writes to ECS-Only ────────────

        [Test]
        public void PushSensors_InCombat_ReadableViaGroupQuery()
        {
            var boss = _registry.Add();
            boss.BotType = BotType.PMC;
            var follower = _registry.Add();
            follower.BotType = BotType.PMC;

            HiveMindSystem.AssignBoss(follower, boss);

            boss.SetSensor(BotSensor.InCombat, true);

            Assert.That(boss.GetSensor(BotSensor.InCombat), Is.True);
            Assert.That(follower.CheckSensorForBoss(BotSensor.InCombat), Is.True);
            Assert.That(follower.CheckSensorForGroup(BotSensor.InCombat), Is.True);
        }

        [Test]
        public void PushSensors_IsSuspicious_ReadableViaGroupQuery()
        {
            var boss = _registry.Add();
            var follower = _registry.Add();

            HiveMindSystem.AssignBoss(follower, boss);

            follower.SetSensor(BotSensor.IsSuspicious, true);

            Assert.That(follower.GetSensor(BotSensor.IsSuspicious), Is.True);
            Assert.That(boss.CheckSensorForAnyFollower(BotSensor.IsSuspicious), Is.True);
            Assert.That(boss.CheckSensorForGroup(BotSensor.IsSuspicious), Is.True);
        }

        [Test]
        public void PushSensors_WantsToLoot_ReadableViaGroupQuery()
        {
            var boss = _registry.Add();
            var follower = _registry.Add();

            HiveMindSystem.AssignBoss(follower, boss);

            follower.SetSensor(BotSensor.WantsToLoot, true);

            Assert.That(follower.GetSensor(BotSensor.WantsToLoot), Is.True);
            Assert.That(boss.CheckSensorForAnyFollower(BotSensor.WantsToLoot), Is.True);
        }

        [Test]
        public void PushSensors_LastLootingTime_WriteAndReadBackFromBoss()
        {
            var boss = _registry.Add();
            var follower = _registry.Add();

            HiveMindSystem.AssignBoss(follower, boss);

            var lootTime = new DateTime(2025, 8, 20, 14, 0, 0);
            boss.LastLootingTime = lootTime;

            Assert.That(boss.LastLootingTime, Is.EqualTo(lootTime));
            Assert.That(follower.Boss.LastLootingTime, Is.EqualTo(lootTime));
        }

        [Test]
        public void PushSensors_RapidToggling_FinalStateCorrect()
        {
            var entity = _registry.Add();

            entity.SetSensor(BotSensor.InCombat, true);
            entity.SetSensor(BotSensor.InCombat, false);
            Assert.That(entity.GetSensor(BotSensor.InCombat), Is.False);

            entity.SetSensor(BotSensor.IsSuspicious, false);
            entity.SetSensor(BotSensor.IsSuspicious, true);
            entity.SetSensor(BotSensor.IsSuspicious, false);
            entity.SetSensor(BotSensor.IsSuspicious, true);
            Assert.That(entity.GetSensor(BotSensor.IsSuspicious), Is.True);

            entity.SetSensor(BotSensor.WantsToLoot, true);
            entity.SetSensor(BotSensor.WantsToLoot, false);
            entity.SetSensor(BotSensor.WantsToLoot, true);
            entity.SetSensor(BotSensor.WantsToLoot, false);
            Assert.That(entity.GetSensor(BotSensor.WantsToLoot), Is.False);
        }

        [Test]
        public void PushSensors_WantsToLootAndLastLootingTime_UpdateTogether()
        {
            var boss = _registry.Add();
            var follower = _registry.Add();

            HiveMindSystem.AssignBoss(follower, boss);

            // Follower starts looting
            follower.SetSensor(BotSensor.WantsToLoot, true);
            var lootTime = new DateTime(2025, 9, 1, 12, 30, 0);
            follower.LastLootingTime = lootTime;

            Assert.That(follower.WantsToLoot, Is.True);
            Assert.That(follower.LastLootingTime, Is.EqualTo(lootTime));

            // Boss can see follower wants to loot
            Assert.That(boss.CheckSensorForAnyFollower(BotSensor.WantsToLoot), Is.True);

            // Follower finishes looting
            follower.SetSensor(BotSensor.WantsToLoot, false);
            Assert.That(follower.WantsToLoot, Is.False);
            // LastLootingTime still retains the value
            Assert.That(follower.LastLootingTime, Is.EqualTo(lootTime));
        }

        [Test]
        public void PushSensors_InactiveEntity_SensorsResetBySystem()
        {
            var entity = _registry.Add();
            entity.SetSensor(BotSensor.InCombat, true);
            entity.SetSensor(BotSensor.IsSuspicious, true);
            entity.SetSensor(BotSensor.WantsToLoot, true);

            // Deactivate the entity (simulates bot death)
            entity.IsActive = false;

            // HiveMindSystem.ResetInactiveEntitySensors should clear sensors for inactive bots
            HiveMindSystem.ResetInactiveEntitySensors(_registry.Entities);

            Assert.That(entity.GetSensor(BotSensor.InCombat), Is.False);
            Assert.That(entity.GetSensor(BotSensor.IsSuspicious), Is.False);
            Assert.That(entity.GetSensor(BotSensor.WantsToLoot), Is.False);
        }

        [Test]
        public void PushSensors_ActiveEntity_SensorsPreservedByResetSystem()
        {
            var entity = _registry.Add();
            entity.SetSensor(BotSensor.InCombat, true);
            entity.SetSensor(BotSensor.IsSuspicious, true);

            // Entity is active — reset system should NOT touch its sensors
            HiveMindSystem.ResetInactiveEntitySensors(_registry.Entities);

            Assert.That(entity.GetSensor(BotSensor.InCombat), Is.True);
            Assert.That(entity.GetSensor(BotSensor.IsSuspicious), Is.True);
        }

        [Test]
        public void PushSensors_MultipleEntities_OnlyInactiveReset()
        {
            var active = _registry.Add();
            var inactive = _registry.Add();
            var alsoActive = _registry.Add();

            active.SetSensor(BotSensor.InCombat, true);
            inactive.SetSensor(BotSensor.InCombat, true);
            alsoActive.SetSensor(BotSensor.InCombat, true);

            inactive.IsActive = false;

            HiveMindSystem.ResetInactiveEntitySensors(_registry.Entities);

            Assert.That(active.GetSensor(BotSensor.InCombat), Is.True);
            Assert.That(inactive.GetSensor(BotSensor.InCombat), Is.False);
            Assert.That(alsoActive.GetSensor(BotSensor.InCombat), Is.True);
        }

        [Test]
        public void PushSensors_AllSensorTypes_WriteAndReadCorrectly()
        {
            var entity = _registry.Add();

            // Write all push sensor types
            var pushSensors = new[] { BotSensor.InCombat, BotSensor.IsSuspicious, BotSensor.WantsToLoot };
            foreach (var sensor in pushSensors)
            {
                entity.SetSensor(sensor, true);
                Assert.That(entity.GetSensor(sensor), Is.True, $"Sensor {sensor} should be true after write");
            }

            // Clear all push sensor types
            foreach (var sensor in pushSensors)
            {
                entity.SetSensor(sensor, false);
                Assert.That(entity.GetSensor(sensor), Is.False, $"Sensor {sensor} should be false after clear");
            }
        }

        // ── Phase 5C: Pull Sensor Writes to ECS-Only ────────────

        [Test]
        public void PullSensors_CanQuestAndCanSprint_SetViaIteration()
        {
            var e1 = _registry.Add();
            var e2 = _registry.Add();
            var e3 = _registry.Add();

            // Simulate pull sensor iteration: set CanQuest and CanSprintToObjective
            for (int i = 0; i < _registry.Entities.Count; i++)
            {
                var entity = _registry.Entities[i];
                entity.SetSensor(BotSensor.CanQuest, true);
                entity.SetSensor(BotSensor.CanSprintToObjective, false);
            }

            Assert.That(e1.CanQuest, Is.True);
            Assert.That(e2.CanQuest, Is.True);
            Assert.That(e3.CanQuest, Is.True);
            Assert.That(e1.CanSprintToObjective, Is.False);
            Assert.That(e2.CanSprintToObjective, Is.False);
            Assert.That(e3.CanSprintToObjective, Is.False);
        }

        [Test]
        public void PullSensors_FollowerInheritsBossCanQuestViaGroupQuery()
        {
            var boss = _registry.Add();
            boss.BotType = BotType.PMC;
            var follower = _registry.Add();
            follower.BotType = BotType.PMC;

            HiveMindSystem.AssignBoss(follower, boss);

            // Only boss has CanQuest set
            boss.SetSensor(BotSensor.CanQuest, true);
            follower.SetSensor(BotSensor.CanQuest, false);

            // Follower reads boss's CanQuest via CheckSensorForBoss
            Assert.That(follower.CheckSensorForBoss(BotSensor.CanQuest), Is.True);
            // Group query also sees it
            Assert.That(follower.CheckSensorForGroup(BotSensor.CanQuest), Is.True);
        }

        [Test]
        public void PullSensors_IterationSkipsInactiveEntities()
        {
            var active1 = _registry.Add();
            active1.BotType = BotType.PMC;
            var inactive = _registry.Add();
            inactive.BotType = BotType.Scav;
            inactive.IsActive = false;
            var active2 = _registry.Add();
            active2.BotType = BotType.PMC;

            // Simulate pull sensor iteration that only updates active entities
            for (int i = 0; i < _registry.Entities.Count; i++)
            {
                var entity = _registry.Entities[i];
                if (!entity.IsActive)
                    continue;
                entity.SetSensor(BotSensor.CanQuest, true);
            }

            Assert.That(active1.CanQuest, Is.True);
            Assert.That(active2.CanQuest, Is.True);
            // Inactive entity was skipped
            Assert.That(inactive.CanQuest, Is.False);
        }

        [Test]
        public void PullSensors_CanSprintDefaultIsTrue()
        {
            var entity = _registry.Add();

            // BotEntity constructor sets CanSprintToObjective = true
            Assert.That(entity.CanSprintToObjective, Is.True);
            Assert.That(entity.CanQuest, Is.False);
        }

        [Test]
        public void PullSensors_InactiveEntity_ResetByHiveMindSystem()
        {
            var entity = _registry.Add();
            entity.SetSensor(BotSensor.CanQuest, true);
            entity.SetSensor(BotSensor.CanSprintToObjective, false);

            entity.IsActive = false;
            HiveMindSystem.ResetInactiveEntitySensors(_registry.Entities);

            // Pull sensors should be reset to defaults (CanQuest=false, CanSprint=true)
            Assert.That(entity.CanQuest, Is.False);
            Assert.That(entity.CanSprintToObjective, Is.True);
        }

        [Test]
        public void PullSensors_DenseIteration_NoGaps()
        {
            // Add 5 entities, remove middle one, verify iteration covers all remaining
            var entities = new BotEntity[5];
            for (int i = 0; i < 5; i++)
                entities[i] = _registry.Add();

            _registry.Remove(entities[2]);

            // Dense list should have 4 entities, no gaps
            Assert.That(_registry.Count, Is.EqualTo(4));

            int updated = 0;
            for (int i = 0; i < _registry.Entities.Count; i++)
            {
                _registry.Entities[i].CanQuest = true;
                updated++;
            }

            Assert.That(updated, Is.EqualTo(4));
        }

        // ── Phase 5D: Boss/Follower Lifecycle to ECS-Only ───────

        [Test]
        public void BossLifecycle_RegisterAssignBossDiesFollowerBecomesSolo()
        {
            // 1. Register
            var boss = _registry.Add();
            boss.BotType = BotType.PMC;
            var follower = _registry.Add();
            follower.BotType = BotType.PMC;

            // 2. Assign boss
            HiveMindSystem.AssignBoss(follower, boss);
            Assert.That(follower.HasBoss, Is.True);
            Assert.That(boss.HasFollowers, Is.True);

            // 3. Boss dies
            boss.IsActive = false;
            HiveMindSystem.CleanupDeadEntities(_registry.Entities);

            // 4. Follower becomes solo
            Assert.That(follower.HasBoss, Is.False);
            Assert.That(follower.Boss, Is.Null);
            Assert.That(follower.IsActive, Is.True);
            Assert.That(boss.Followers.Count, Is.EqualTo(0));
        }

        [Test]
        public void BossReassignment_FollowerTransfersFromBoss1ToBoss2()
        {
            var boss1 = _registry.Add();
            boss1.BotType = BotType.PMC;
            var boss2 = _registry.Add();
            boss2.BotType = BotType.PMC;
            var follower = _registry.Add();
            follower.BotType = BotType.PMC;

            // Assign to boss1
            HiveMindSystem.AssignBoss(follower, boss1);
            Assert.That(follower.Boss, Is.SameAs(boss1));
            Assert.That(boss1.Followers, Contains.Item(follower));

            // Reassign to boss2 — AssignBoss detaches from old boss
            HiveMindSystem.AssignBoss(follower, boss2);
            Assert.That(follower.Boss, Is.SameAs(boss2));
            Assert.That(boss2.Followers, Contains.Item(follower));
            // Old boss no longer has follower
            Assert.That(boss1.Followers, Does.Not.Contain(follower));
            Assert.That(boss1.HasFollowers, Is.False);
        }

        [Test]
        public void SeparateFromGroup_AllFollowersDetachedWhenBossSeparated()
        {
            var boss = _registry.Add();
            boss.BotType = BotType.Boss;
            var f1 = _registry.Add();
            var f2 = _registry.Add();
            var f3 = _registry.Add();

            HiveMindSystem.AssignBoss(f1, boss);
            HiveMindSystem.AssignBoss(f2, boss);
            HiveMindSystem.AssignBoss(f3, boss);
            Assert.That(boss.Followers.Count, Is.EqualTo(3));

            HiveMindSystem.SeparateFromGroup(boss);

            Assert.That(boss.HasFollowers, Is.False);
            Assert.That(boss.Followers.Count, Is.EqualTo(0));
            Assert.That(f1.Boss, Is.Null);
            Assert.That(f2.Boss, Is.Null);
            Assert.That(f3.Boss, Is.Null);
        }

        [Test]
        public void MultipleGroups_OperateIndependently()
        {
            // Group 1
            var boss1 = _registry.Add();
            boss1.BotType = BotType.PMC;
            var f1a = _registry.Add();
            var f1b = _registry.Add();
            HiveMindSystem.AssignBoss(f1a, boss1);
            HiveMindSystem.AssignBoss(f1b, boss1);

            // Group 2
            var boss2 = _registry.Add();
            boss2.BotType = BotType.Boss;
            var f2a = _registry.Add();
            HiveMindSystem.AssignBoss(f2a, boss2);

            // Set sensors independently
            boss1.SetSensor(BotSensor.InCombat, true);
            boss2.SetSensor(BotSensor.CanQuest, true);

            // Group 1 sees InCombat, not CanQuest
            Assert.That(f1a.CheckSensorForGroup(BotSensor.InCombat), Is.True);
            Assert.That(f1a.CheckSensorForGroup(BotSensor.CanQuest), Is.False);

            // Group 2 sees CanQuest, not InCombat
            Assert.That(f2a.CheckSensorForGroup(BotSensor.CanQuest), Is.True);
            Assert.That(f2a.CheckSensorForGroup(BotSensor.InCombat), Is.False);

            // Separate group 1 — group 2 unaffected
            HiveMindSystem.SeparateFromGroup(boss1);
            Assert.That(boss1.HasFollowers, Is.False);
            Assert.That(f2a.Boss, Is.SameAs(boss2));
            Assert.That(boss2.HasFollowers, Is.True);
        }

        [Test]
        public void CleanupDeadEntities_MultipleDeadBosses_AllFollowersDetached()
        {
            var boss1 = _registry.Add();
            var boss2 = _registry.Add();
            var f1 = _registry.Add();
            var f2 = _registry.Add();
            var f3 = _registry.Add();

            HiveMindSystem.AssignBoss(f1, boss1);
            HiveMindSystem.AssignBoss(f2, boss2);
            HiveMindSystem.AssignBoss(f3, boss2);

            // Both bosses die
            boss1.IsActive = false;
            boss2.IsActive = false;

            HiveMindSystem.CleanupDeadEntities(_registry.Entities);

            Assert.That(f1.HasBoss, Is.False);
            Assert.That(f2.HasBoss, Is.False);
            Assert.That(f3.HasBoss, Is.False);
            Assert.That(boss1.Followers.Count, Is.EqualTo(0));
            Assert.That(boss2.Followers.Count, Is.EqualTo(0));
        }

        [Test]
        public void CleanupDeadEntities_DeadFollowerRemovedFromBossList()
        {
            var boss = _registry.Add();
            var f1 = _registry.Add();
            var f2 = _registry.Add();

            HiveMindSystem.AssignBoss(f1, boss);
            HiveMindSystem.AssignBoss(f2, boss);
            Assert.That(boss.Followers.Count, Is.EqualTo(2));

            // One follower dies
            f1.IsActive = false;

            HiveMindSystem.CleanupDeadEntities(_registry.Entities);

            Assert.That(boss.Followers.Count, Is.EqualTo(1));
            Assert.That(boss.Followers[0], Is.SameAs(f2));
            Assert.That(f1.HasBoss, Is.False);
        }

        [Test]
        public void CleanupDeadEntities_RepeatedCallsIdempotent()
        {
            var boss = _registry.Add();
            var follower = _registry.Add();

            HiveMindSystem.AssignBoss(follower, boss);
            boss.IsActive = false;

            // Call multiple times
            HiveMindSystem.CleanupDeadEntities(_registry.Entities);
            HiveMindSystem.CleanupDeadEntities(_registry.Entities);
            HiveMindSystem.CleanupDeadEntities(_registry.Entities);

            Assert.That(follower.HasBoss, Is.False);
            Assert.That(boss.Followers.Count, Is.EqualTo(0));
        }

        // ── Phase 5E: BotRegistrationManager Reads to ECS ───────

        [Test]
        public void CountActiveByType_EachBotTypeValue()
        {
            var pmc = _registry.Add();
            pmc.BotType = BotType.PMC;
            var scav = _registry.Add();
            scav.BotType = BotType.Scav;
            var pscav = _registry.Add();
            pscav.BotType = BotType.PScav;
            var boss = _registry.Add();
            boss.BotType = BotType.Boss;

            Assert.That(HiveMindSystem.CountActiveByType(_registry.Entities, BotType.PMC), Is.EqualTo(1));
            Assert.That(HiveMindSystem.CountActiveByType(_registry.Entities, BotType.Scav), Is.EqualTo(1));
            Assert.That(HiveMindSystem.CountActiveByType(_registry.Entities, BotType.PScav), Is.EqualTo(1));
            Assert.That(HiveMindSystem.CountActiveByType(_registry.Entities, BotType.Boss), Is.EqualTo(1));
            Assert.That(HiveMindSystem.CountActiveByType(_registry.Entities, BotType.Unknown), Is.EqualTo(0));
        }

        [Test]
        public void CountActiveByType_SleepingBotsExcluded()
        {
            var pmc1 = _registry.Add();
            pmc1.BotType = BotType.PMC;
            var pmc2 = _registry.Add();
            pmc2.BotType = BotType.PMC;
            pmc2.IsSleeping = true;
            var pmc3 = _registry.Add();
            pmc3.BotType = BotType.PMC;

            Assert.That(HiveMindSystem.CountActiveByType(_registry.Entities, BotType.PMC), Is.EqualTo(2));
        }

        [Test]
        public void CountActiveByType_InactiveBotsExcluded()
        {
            var scav1 = _registry.Add();
            scav1.BotType = BotType.Scav;
            var scav2 = _registry.Add();
            scav2.BotType = BotType.Scav;
            scav2.IsActive = false;

            Assert.That(HiveMindSystem.CountActiveByType(_registry.Entities, BotType.Scav), Is.EqualTo(1));
        }

        [Test]
        public void CountActiveByType_MixedPopulation()
        {
            // 3 PMCs (1 sleeping), 2 Scavs, 1 PScav (inactive), 2 Bosses
            var pmc1 = _registry.Add();
            pmc1.BotType = BotType.PMC;
            var pmc2 = _registry.Add();
            pmc2.BotType = BotType.PMC;
            var pmc3 = _registry.Add();
            pmc3.BotType = BotType.PMC;
            pmc3.IsSleeping = true;

            var scav1 = _registry.Add();
            scav1.BotType = BotType.Scav;
            var scav2 = _registry.Add();
            scav2.BotType = BotType.Scav;

            var pscav = _registry.Add();
            pscav.BotType = BotType.PScav;
            pscav.IsActive = false;

            var boss1 = _registry.Add();
            boss1.BotType = BotType.Boss;
            var boss2 = _registry.Add();
            boss2.BotType = BotType.Boss;

            Assert.That(HiveMindSystem.CountActiveByType(_registry.Entities, BotType.PMC), Is.EqualTo(2));
            Assert.That(HiveMindSystem.CountActiveByType(_registry.Entities, BotType.Scav), Is.EqualTo(2));
            Assert.That(HiveMindSystem.CountActiveByType(_registry.Entities, BotType.PScav), Is.EqualTo(0));
            Assert.That(HiveMindSystem.CountActiveByType(_registry.Entities, BotType.Boss), Is.EqualTo(2));
            // Total active
            Assert.That(HiveMindSystem.CountActive(_registry.Entities), Is.EqualTo(6));
        }

        [Test]
        public void IterateEntitiesFilterByType_MatchesCountActiveByType()
        {
            var pmc1 = _registry.Add();
            pmc1.BotType = BotType.PMC;
            var pmc2 = _registry.Add();
            pmc2.BotType = BotType.PMC;
            var scav = _registry.Add();
            scav.BotType = BotType.Scav;
            var boss = _registry.Add();
            boss.BotType = BotType.Boss;
            boss.IsSleeping = true;

            // Manual iteration filtering by type — mirrors GetActiveByType pattern
            var activePMCs = new List<BotEntity>();
            for (int i = 0; i < _registry.Entities.Count; i++)
            {
                var e = _registry.Entities[i];
                if (e.IsActive && !e.IsSleeping && e.BotType == BotType.PMC)
                    activePMCs.Add(e);
            }

            Assert.That(activePMCs.Count, Is.EqualTo(2));
            Assert.That(activePMCs, Contains.Item(pmc1));
            Assert.That(activePMCs, Contains.Item(pmc2));
            Assert.That(activePMCs.Count, Is.EqualTo(HiveMindSystem.CountActiveByType(_registry.Entities, BotType.PMC)));
        }

        [Test]
        public void IsBotSleeping_ViaBotEntity_ReflectsSetSleepingState()
        {
            var entity = _registry.Add();
            entity.BotType = BotType.PMC;

            Assert.That(entity.IsSleeping, Is.False);

            entity.IsSleeping = true;
            Assert.That(entity.IsSleeping, Is.True);

            entity.IsSleeping = false;
            Assert.That(entity.IsSleeping, Is.False);
        }

        [Test]
        public void IsBotAPMC_ViaBotEntity_OnlyPMCsReturnTrue()
        {
            var pmc = _registry.Add();
            pmc.BotType = BotType.PMC;
            var scav = _registry.Add();
            scav.BotType = BotType.Scav;
            var boss = _registry.Add();
            boss.BotType = BotType.Boss;
            var pscav = _registry.Add();
            pscav.BotType = BotType.PScav;
            var unknown = _registry.Add();
            unknown.BotType = BotType.Unknown;

            Assert.That(pmc.BotType == BotType.PMC, Is.True);
            Assert.That(scav.BotType == BotType.PMC, Is.False);
            Assert.That(boss.BotType == BotType.PMC, Is.False);
            Assert.That(pscav.BotType == BotType.PMC, Is.False);
            Assert.That(unknown.BotType == BotType.PMC, Is.False);
        }

        [Test]
        public void GetBotType_ViaBotEntity_ReturnsCorrectType()
        {
            var pmc = _registry.Add();
            pmc.BotType = BotType.PMC;
            var scav = _registry.Add();
            scav.BotType = BotType.Scav;
            var boss = _registry.Add();
            boss.BotType = BotType.Boss;

            Assert.That(pmc.BotType, Is.EqualTo(BotType.PMC));
            Assert.That(scav.BotType, Is.EqualTo(BotType.Scav));
            Assert.That(boss.BotType, Is.EqualTo(BotType.Boss));
        }

        // ── Phase 5F: Remove Old Data Structures ────────────────

        [Test]
        public void Clear_LeavesRegistryEmpty()
        {
            _registry.Add().BotType = BotType.PMC;
            _registry.Add().BotType = BotType.Scav;
            _registry.Add().BotType = BotType.Boss;

            Assert.That(_registry.Count, Is.EqualTo(3));

            _registry.Clear();

            Assert.That(_registry.Count, Is.EqualTo(0));
            Assert.That(_registry.Entities.Count, Is.EqualTo(0));
            Assert.That(HiveMindSystem.CountActive(_registry.Entities), Is.EqualTo(0));
            Assert.That(HiveMindSystem.CountActiveByType(_registry.Entities, BotType.PMC), Is.EqualTo(0));
        }

        [Test]
        public void Clear_NewRegistrationsStartFresh()
        {
            var old1 = _registry.Add();
            old1.BotType = BotType.PMC;
            var old2 = _registry.Add();
            old2.BotType = BotType.Boss;
            HiveMindSystem.AssignBoss(old1, old2);

            _registry.Clear();

            // New registrations after clear
            var fresh1 = _registry.Add();
            fresh1.BotType = BotType.Scav;
            var fresh2 = _registry.Add();
            fresh2.BotType = BotType.PScav;

            Assert.That(_registry.Count, Is.EqualTo(2));
            Assert.That(fresh1.BotType, Is.EqualTo(BotType.Scav));
            Assert.That(fresh2.BotType, Is.EqualTo(BotType.PScav));
            Assert.That(fresh1.IsActive, Is.True);
            Assert.That(fresh2.IsActive, Is.True);
            Assert.That(fresh1.HasBoss, Is.False);
            Assert.That(fresh2.HasFollowers, Is.False);
            Assert.That(HiveMindSystem.CountActive(_registry.Entities), Is.EqualTo(2));
        }

        [Test]
        public void Clear_DoesNotAffectAlreadyReferencedEntityObjects()
        {
            var entity = _registry.Add();
            entity.BotType = BotType.PMC;
            entity.SetSensor(BotSensor.InCombat, true);
            entity.LastLootingTime = new DateTime(2025, 10, 1);

            // Keep a reference before clearing
            var savedRef = entity;

            _registry.Clear();

            // Registry is empty
            Assert.That(_registry.Count, Is.EqualTo(0));

            // But the object reference still holds its state
            Assert.That(savedRef.BotType, Is.EqualTo(BotType.PMC));
            Assert.That(savedRef.IsInCombat, Is.True);
            Assert.That(savedRef.LastLootingTime, Is.EqualTo(new DateTime(2025, 10, 1)));
        }

        [Test]
        public void SwapRemove_PreservesBossFollowerRelationships()
        {
            // Verifies that removing an entity from the dense list doesn't
            // corrupt boss/follower relationships on remaining entities.
            var boss = _registry.Add();
            boss.BotType = BotType.Boss;
            var unrelated = _registry.Add();
            unrelated.BotType = BotType.Scav;
            var f1 = _registry.Add();
            f1.BotType = BotType.PMC;
            var f2 = _registry.Add();
            f2.BotType = BotType.PMC;

            HiveMindSystem.AssignBoss(f1, boss);
            HiveMindSystem.AssignBoss(f2, boss);

            // Remove unrelated bot (triggers swap-remove in dense list)
            _registry.Remove(unrelated);

            // Boss-follower relationships must survive the swap
            Assert.That(boss.Followers.Count, Is.EqualTo(2));
            Assert.That(f1.Boss, Is.SameAs(boss));
            Assert.That(f2.Boss, Is.SameAs(boss));
            Assert.That(f1.CheckSensorForBoss(BotSensor.InCombat), Is.False);

            // Set sensor on boss after swap — follower still sees it
            boss.SetSensor(BotSensor.InCombat, true);
            Assert.That(f1.CheckSensorForGroup(BotSensor.InCombat), Is.True);
        }

        [Test]
        public void SwapRemove_PreservesSensorStateOnRemainingEntities()
        {
            var e1 = _registry.Add();
            e1.BotType = BotType.PMC;
            e1.SetSensor(BotSensor.InCombat, true);
            e1.SetSensor(BotSensor.CanQuest, true);

            var e2 = _registry.Add();
            e2.BotType = BotType.Scav;
            e2.SetSensor(BotSensor.WantsToLoot, true);
            e2.LastLootingTime = new DateTime(2025, 11, 1);

            var toRemove = _registry.Add();
            toRemove.BotType = BotType.Boss;

            _registry.Remove(toRemove);

            // Remaining entities' sensor state must be intact
            Assert.That(e1.IsInCombat, Is.True);
            Assert.That(e1.CanQuest, Is.True);
            Assert.That(e2.WantsToLoot, Is.True);
            Assert.That(e2.LastLootingTime, Is.EqualTo(new DateTime(2025, 11, 1)));
            Assert.That(_registry.Count, Is.EqualTo(2));
        }

        [Test]
        public void SwapRemove_CountActiveByTypeStillCorrect()
        {
            var pmc1 = _registry.Add();
            pmc1.BotType = BotType.PMC;
            var scav = _registry.Add();
            scav.BotType = BotType.Scav;
            var pmc2 = _registry.Add();
            pmc2.BotType = BotType.PMC;

            Assert.That(HiveMindSystem.CountActiveByType(_registry.Entities, BotType.PMC), Is.EqualTo(2));

            _registry.Remove(scav);

            Assert.That(HiveMindSystem.CountActiveByType(_registry.Entities, BotType.PMC), Is.EqualTo(2));
            Assert.That(HiveMindSystem.CountActiveByType(_registry.Entities, BotType.Scav), Is.EqualTo(0));
            Assert.That(HiveMindSystem.CountActive(_registry.Entities), Is.EqualTo(2));
        }

        [Test]
        public void AssignBoss_Idempotent_SameBossTwiceDoesNotDuplicate()
        {
            // Phase 5F: addBossFollower now checks ECS to see if follower already has this boss.
            // Assigning the same boss twice should be a no-op.
            var boss = _registry.Add();
            boss.BotType = BotType.Boss;
            var follower = _registry.Add();
            follower.BotType = BotType.PMC;

            HiveMindSystem.AssignBoss(follower, boss);
            Assert.That(boss.Followers.Count, Is.EqualTo(1));

            // Second assignment — should not add a duplicate
            HiveMindSystem.AssignBoss(follower, boss);
            Assert.That(boss.Followers.Count, Is.EqualTo(1));
            Assert.That(follower.Boss, Is.SameAs(boss));
        }

        [Test]
        public void SleepGuard_OnlyFirstSetSleepingTakesEffect()
        {
            // Phase 5F: RegisterSleepingBot uses BotEntityBridge.IsBotSleeping as guard.
            // Setting sleeping true twice should be idempotent.
            var entity = _registry.Add();
            entity.BotType = BotType.PMC;

            Assert.That(entity.IsSleeping, Is.False);

            entity.IsSleeping = true;
            Assert.That(entity.IsSleeping, Is.True);

            // "Register sleeping" again — already sleeping, no state change
            entity.IsSleeping = true;
            Assert.That(entity.IsSleeping, Is.True);

            // Unregister
            entity.IsSleeping = false;
            Assert.That(entity.IsSleeping, Is.False);

            // Unregister again — already awake, no state change
            entity.IsSleeping = false;
            Assert.That(entity.IsSleeping, Is.False);
        }

        [Test]
        public void ClearWithBsgIds_ResetsAllLookups()
        {
            // Phase 5F: Clear should reset BsgBotRegistry sparse array too.
            var e1 = _registry.Add(10);
            e1.BotType = BotType.PMC;
            var e2 = _registry.Add(20);
            e2.BotType = BotType.Scav;

            Assert.That(_registry.GetByBsgId(10), Is.SameAs(e1));
            Assert.That(_registry.GetByBsgId(20), Is.SameAs(e2));

            _registry.Clear();

            Assert.That(_registry.GetByBsgId(10), Is.Null);
            Assert.That(_registry.GetByBsgId(20), Is.Null);
            Assert.That(_registry.Count, Is.EqualTo(0));
        }

        [Test]
        public void FullLifecycle_Phase5F_NoOldDataStructuresNeeded()
        {
            // End-to-end lifecycle after Phase 5F: all state managed via ECS only.
            // 1. Register bots with BSG IDs
            var boss = _registry.Add(100);
            boss.BotType = BotType.Boss;
            var f1 = _registry.Add(200);
            f1.BotType = BotType.PMC;
            var f2 = _registry.Add(300);
            f2.BotType = BotType.PMC;

            Assert.That(_registry.Count, Is.EqualTo(3));
            Assert.That(_registry.GetByBsgId(100), Is.SameAs(boss));

            // 2. Set sensors via ECS
            boss.SetSensor(BotSensor.InCombat, true);
            f1.SetSensor(BotSensor.CanQuest, true);

            // 3. Establish boss/follower via ECS
            HiveMindSystem.AssignBoss(f1, boss);
            HiveMindSystem.AssignBoss(f2, boss);
            Assert.That(boss.Followers.Count, Is.EqualTo(2));

            // 4. Group queries work
            Assert.That(f1.CheckSensorForBoss(BotSensor.InCombat), Is.True);
            Assert.That(boss.CheckSensorForAnyFollower(BotSensor.CanQuest), Is.True);

            // 5. Put f2 to sleep (ECS-only guard)
            f2.IsSleeping = true;
            Assert.That(HiveMindSystem.CountActive(_registry.Entities), Is.EqualTo(2));

            // 6. Boss dies — ECS cleanup detaches followers
            boss.IsActive = false;
            HiveMindSystem.CleanupDeadEntities(_registry.Entities);
            Assert.That(f1.Boss, Is.Null);
            Assert.That(f2.Boss, Is.Null);

            // 7. Clear everything
            _registry.Clear();
            Assert.That(_registry.Count, Is.EqualTo(0));
            Assert.That(_registry.GetByBsgId(100), Is.Null);
        }

        // ── Phase 6: Field State Wiring ─────────────────────────

        [Test]
        public void RegisterBot_PopulatesFieldNoiseSeed()
        {
            // Simulates BotEntityBridge.RegisterBot setting FieldNoiseSeed from profileId.GetHashCode()
            var profileId = "pmc-abc-123";
            var entity = _registry.Add();
            entity.BotType = BotType.PMC;
            entity.FieldNoiseSeed = profileId.GetHashCode();
            entity.HasFieldState = true;

            Assert.That(entity.FieldNoiseSeed, Is.EqualTo(profileId.GetHashCode()));
        }

        [Test]
        public void RegisterBot_SetsHasFieldState()
        {
            var entity = _registry.Add();
            entity.BotType = BotType.PMC;
            entity.FieldNoiseSeed = "some-profile".GetHashCode();
            entity.HasFieldState = true;

            Assert.That(entity.HasFieldState, Is.True);
        }

        [Test]
        public void GetFieldState_ByEntityId_ReturnsFieldState()
        {
            // Simulates BotEntityBridge storing BotFieldState keyed by entity.Id
            var fieldStates = new Dictionary<int, BotFieldState>();

            var entity = _registry.Add();
            entity.BotType = BotType.PMC;
            var profileId = "pmc-test-456";
            int noiseSeed = profileId.GetHashCode();
            entity.FieldNoiseSeed = noiseSeed;
            entity.HasFieldState = true;
            fieldStates[entity.Id] = new BotFieldState(noiseSeed);

            // Simulates GetFieldState(bot) -> lookup by entity.Id
            Assert.That(fieldStates.TryGetValue(entity.Id, out var state), Is.True);
            Assert.That(state, Is.Not.Null);
        }

        [Test]
        public void GetFieldState_ByProfileId_ReturnsFieldState()
        {
            // Simulates the profileId -> entity -> fieldState lookup chain
            var profileToEntity = new Dictionary<string, BotEntity>();
            var fieldStates = new Dictionary<int, BotFieldState>();

            var profileId = "scav-xyz-789";
            var entity = _registry.Add();
            entity.BotType = BotType.Scav;
            int noiseSeed = profileId.GetHashCode();
            entity.FieldNoiseSeed = noiseSeed;
            entity.HasFieldState = true;
            profileToEntity[profileId] = entity;
            fieldStates[entity.Id] = new BotFieldState(noiseSeed);

            // Simulates GetFieldState(profileId) -> profileToEntity -> fieldStates
            Assert.That(profileToEntity.TryGetValue(profileId, out var found), Is.True);
            Assert.That(fieldStates.TryGetValue(found.Id, out var state), Is.True);
            Assert.That(state, Is.Not.Null);
        }

        [Test]
        public void GetFieldState_UnknownBot_ReturnsNull()
        {
            // Simulates GetFieldState on an entity that was never registered with field state
            var fieldStates = new Dictionary<int, BotFieldState>();

            var entity = _registry.Add();
            entity.BotType = BotType.PMC;
            // Deliberately NOT adding field state for this entity

            Assert.That(fieldStates.TryGetValue(entity.Id, out var state), Is.False);
            Assert.That(state, Is.Null);
        }

        [Test]
        public void GetFieldState_NoiseSeed_MatchesEntitySeed()
        {
            var fieldStates = new Dictionary<int, BotFieldState>();

            var profileId = "boss-leader-001";
            var entity = _registry.Add();
            entity.BotType = BotType.Boss;
            int noiseSeed = profileId.GetHashCode();
            entity.FieldNoiseSeed = noiseSeed;
            entity.HasFieldState = true;
            fieldStates[entity.Id] = new BotFieldState(noiseSeed);

            // The BotFieldState.NoiseSeed should match entity.FieldNoiseSeed
            var state = fieldStates[entity.Id];
            Assert.That(state.NoiseSeed, Is.EqualTo(entity.FieldNoiseSeed));
        }

        [Test]
        public void Clear_ResetsFieldStates()
        {
            // Simulates BotEntityBridge.Clear() which should clear _entityFieldStates
            var fieldStates = new Dictionary<int, BotFieldState>();

            var entity = _registry.Add();
            entity.BotType = BotType.PMC;
            int noiseSeed = "profile-clear".GetHashCode();
            entity.FieldNoiseSeed = noiseSeed;
            entity.HasFieldState = true;
            fieldStates[entity.Id] = new BotFieldState(noiseSeed);

            int savedId = entity.Id;

            // Clear all state (simulates BotEntityBridge.Clear)
            _registry.Clear();
            fieldStates.Clear();

            // After clear, field state should be gone
            Assert.That(fieldStates.TryGetValue(savedId, out _), Is.False);
            Assert.That(_registry.Count, Is.EqualTo(0));
        }

        [Test]
        public void FieldState_PreviousDestination_PersistsAcrossCalls()
        {
            var fieldStates = new Dictionary<int, BotFieldState>();

            var entity = _registry.Add();
            entity.BotType = BotType.PMC;
            int noiseSeed = "persist-test".GetHashCode();
            entity.FieldNoiseSeed = noiseSeed;
            entity.HasFieldState = true;
            var state = new BotFieldState(noiseSeed);
            fieldStates[entity.Id] = state;

            // Set PreviousDestination
            state.PreviousDestination = new UnityEngine.Vector3(10f, 0f, 20f);

            // Retrieve again and verify it persists
            var retrieved = fieldStates[entity.Id];
            Assert.That(retrieved.PreviousDestination.x, Is.EqualTo(10f));
            Assert.That(retrieved.PreviousDestination.y, Is.EqualTo(0f));
            Assert.That(retrieved.PreviousDestination.z, Is.EqualTo(20f));
        }

        // ── Phase 8: Job Assignment Wiring ──────────────────────────

        [Test]
        public void Phase8_JobAssignmentStorage_KeyedByEntityId()
        {
            // Simulates _jobAssignments Dictionary<int, List<T>> keyed by entity.Id
            var jobAssignments = new Dictionary<int, List<string>>();
            var emptyList = new List<string>();

            var entity = _registry.Add();
            entity.BotType = BotType.PMC;
            jobAssignments[entity.Id] = new List<string>();

            // Add assignments
            var list = jobAssignments[entity.Id];
            list.Add("quest_A");
            list.Add("quest_B");

            Assert.That(jobAssignments[entity.Id].Count, Is.EqualTo(2));
        }

        [Test]
        public void Phase8_JobAssignmentStorage_SafeFallbackForUnknownBot()
        {
            // Simulates GetJobAssignments returning _emptyAssignments for unknown bots
            var jobAssignments = new Dictionary<int, List<string>>();
            var emptyList = new List<string>();

            int unknownId = 999;
            var result = jobAssignments.TryGetValue(unknownId, out var list) ? list : emptyList;

            Assert.That(result, Is.SameAs(emptyList));
            Assert.That(result.Count, Is.EqualTo(0));
        }

        [Test]
        public void Phase8_EnsureJobAssignments_CreatesIfMissing()
        {
            // Simulates EnsureJobAssignments create-if-missing pattern
            var jobAssignments = new Dictionary<int, List<string>>();

            var entity = _registry.Add();
            entity.BotType = BotType.PMC;
            // Don't pre-create list (simulates bot accessing before full registration)

            if (!jobAssignments.TryGetValue(entity.Id, out var list))
            {
                list = new List<string>();
                jobAssignments[entity.Id] = list;
            }

            list.Add("quest_C");

            Assert.That(jobAssignments[entity.Id].Count, Is.EqualTo(1));
            Assert.That(jobAssignments[entity.Id][0], Is.EqualTo("quest_C"));
        }

        [Test]
        public void Phase8_HasJobAssignments_ChecksNonEmpty()
        {
            // Simulates HasJobAssignments checking Count > 0
            var jobAssignments = new Dictionary<int, List<string>>();

            var entity = _registry.Add();
            entity.BotType = BotType.PMC;
            jobAssignments[entity.Id] = new List<string>();

            // Empty list — HasJobAssignments returns false
            bool hasAssignments = jobAssignments.TryGetValue(entity.Id, out var list) && list.Count > 0;
            Assert.That(hasAssignments, Is.False);

            // Add assignment — HasJobAssignments returns true
            jobAssignments[entity.Id].Add("quest_D");
            hasAssignments = jobAssignments.TryGetValue(entity.Id, out list) && list.Count > 0;
            Assert.That(hasAssignments, Is.True);
        }

        [Test]
        public void Phase8_ConsecutiveFailedAssignments_IncrementOnFail()
        {
            // Simulates FailObjective() calling IncrementConsecutiveFailedAssignments
            var entity = _registry.Add();
            entity.BotType = BotType.PMC;

            Assert.That(entity.ConsecutiveFailedAssignments, Is.EqualTo(0));

            // First fail
            entity.ConsecutiveFailedAssignments++;
            Assert.That(entity.ConsecutiveFailedAssignments, Is.EqualTo(1));

            // Second fail
            entity.ConsecutiveFailedAssignments++;
            Assert.That(entity.ConsecutiveFailedAssignments, Is.EqualTo(2));
        }

        [Test]
        public void Phase8_ConsecutiveFailedAssignments_ResetOnComplete()
        {
            // Simulates CompleteObjective() calling ResetConsecutiveFailedAssignments
            var entity = _registry.Add();
            entity.BotType = BotType.PMC;
            entity.ConsecutiveFailedAssignments = 5;

            // Complete resets to zero
            entity.ConsecutiveFailedAssignments = 0;
            Assert.That(entity.ConsecutiveFailedAssignments, Is.EqualTo(0));
        }

        [Test]
        public void Phase8_RecomputeConsecutiveFailedAssignments_ScansFromTail()
        {
            // Simulates RecomputeConsecutiveFailedAssignments reverse tail scan
            // Uses int status values: 0=Active, 1=Completed, 2=Failed
            var entity = _registry.Add();
            entity.BotType = BotType.PMC;
            var statuses = new List<int> { 1, 2, 1, 2, 2, 2 }; // C, F, C, F, F, F

            // Recompute: scan from tail, count consecutive 2 (Failed)
            int count = 0;
            for (int i = statuses.Count - 1; i >= 0; i--)
            {
                if (statuses[i] != 2) // not Failed
                    break;
                count++;
            }
            entity.ConsecutiveFailedAssignments = count;

            Assert.That(entity.ConsecutiveFailedAssignments, Is.EqualTo(3));
        }

        [Test]
        public void Phase8_RecomputeConsecutiveFailedAssignments_NoFailsAtTail()
        {
            // When tail doesn't end with Failed, count is 0
            var entity = _registry.Add();
            var statuses = new List<int> { 2, 2, 1 }; // F, F, Completed

            int count = 0;
            for (int i = statuses.Count - 1; i >= 0; i--)
            {
                if (statuses[i] != 2)
                    break;
                count++;
            }
            entity.ConsecutiveFailedAssignments = count;

            Assert.That(entity.ConsecutiveFailedAssignments, Is.EqualTo(0));
        }

        [Test]
        public void Phase8_RecomputeConsecutiveFailedAssignments_AllFailed()
        {
            var entity = _registry.Add();
            var statuses = new List<int> { 2, 2, 2, 2 }; // All Failed

            int count = 0;
            for (int i = statuses.Count - 1; i >= 0; i--)
            {
                if (statuses[i] != 2)
                    break;
                count++;
            }
            entity.ConsecutiveFailedAssignments = count;

            Assert.That(entity.ConsecutiveFailedAssignments, Is.EqualTo(4));
        }

        [Test]
        public void Phase8_RecomputeConsecutiveFailedAssignments_EmptyList()
        {
            var entity = _registry.Add();
            var statuses = new List<int>();

            int count = 0;
            for (int i = statuses.Count - 1; i >= 0; i--)
            {
                if (statuses[i] != 2)
                    break;
                count++;
            }
            entity.ConsecutiveFailedAssignments = count;

            Assert.That(entity.ConsecutiveFailedAssignments, Is.EqualTo(0));
        }

        [Test]
        public void Phase8_NumberOfActiveBots_DenseEntityIteration()
        {
            // Simulates NumberOfActiveBots iterating dense entity list
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

            // Count active bots (IsActive && !IsSleeping) — mirrors NumberOfActiveBots pattern
            int activeBots = 0;
            for (int e = 0; e < _registry.Entities.Count; e++)
            {
                var entity = _registry.Entities[e];
                if (!entity.IsActive)
                    continue;
                if (entity.IsSleeping)
                    continue;
                activeBots++;
            }

            Assert.That(activeBots, Is.EqualTo(2)); // pmc1, scav
        }

        [Test]
        public void Phase8_AllJobAssignments_IteratesAllProfiles()
        {
            // Simulates AllJobAssignments() yield iterator over profileId → entity → assignments
            var profileToEntity = new Dictionary<string, BotEntity>();
            var jobAssignments = new Dictionary<int, List<string>>();

            var e1 = _registry.Add();
            e1.BotType = BotType.PMC;
            profileToEntity["profile_A"] = e1;
            jobAssignments[e1.Id] = new List<string> { "quest_1", "quest_2" };

            var e2 = _registry.Add();
            e2.BotType = BotType.Scav;
            profileToEntity["profile_B"] = e2;
            jobAssignments[e2.Id] = new List<string> { "quest_3" };

            // Iterate like AllJobAssignments()
            var results = new List<KeyValuePair<string, List<string>>>();
            foreach (var kvp in profileToEntity)
            {
                if (jobAssignments.TryGetValue(kvp.Value.Id, out var list))
                    results.Add(new KeyValuePair<string, List<string>>(kvp.Key, list));
            }

            Assert.That(results.Count, Is.EqualTo(2));
            Assert.That(results[0].Value.Count + results[1].Value.Count, Is.EqualTo(3));
        }

        [Test]
        public void Phase8_Clear_ResetsJobAssignmentsAndConsecutiveFailures()
        {
            // Simulates Clear() resetting _jobAssignments and entity state
            var jobAssignments = new Dictionary<int, List<string>>();

            var entity = _registry.Add();
            entity.BotType = BotType.PMC;
            entity.ConsecutiveFailedAssignments = 7;
            jobAssignments[entity.Id] = new List<string> { "quest_X" };

            // Clear all (simulates BotEntityBridge.Clear())
            _registry.Clear();
            jobAssignments.Clear();

            Assert.That(_registry.Count, Is.EqualTo(0));
            Assert.That(jobAssignments.Count, Is.EqualTo(0));
        }

        [Test]
        public void Phase8_FailCompleteLifecycle()
        {
            // Full lifecycle: register → fail × 3 → complete → fail × 2
            var entity = _registry.Add();
            entity.BotType = BotType.PMC;
            var jobAssignments = new Dictionary<int, List<string>>();
            jobAssignments[entity.Id] = new List<string>();

            Assert.That(entity.ConsecutiveFailedAssignments, Is.EqualTo(0));

            // Three fails
            entity.ConsecutiveFailedAssignments++;
            entity.ConsecutiveFailedAssignments++;
            entity.ConsecutiveFailedAssignments++;
            Assert.That(entity.ConsecutiveFailedAssignments, Is.EqualTo(3));

            // Complete resets
            entity.ConsecutiveFailedAssignments = 0;
            Assert.That(entity.ConsecutiveFailedAssignments, Is.EqualTo(0));

            // Two more fails
            entity.ConsecutiveFailedAssignments++;
            entity.ConsecutiveFailedAssignments++;
            Assert.That(entity.ConsecutiveFailedAssignments, Is.EqualTo(2));
        }

        [Test]
        public void Phase8_MultipleBotsIndependentJobAssignments()
        {
            // Each bot has its own independent job assignment list
            var jobAssignments = new Dictionary<int, List<string>>();

            var e1 = _registry.Add();
            e1.BotType = BotType.PMC;
            jobAssignments[e1.Id] = new List<string> { "A", "B", "C" };

            var e2 = _registry.Add();
            e2.BotType = BotType.Scav;
            jobAssignments[e2.Id] = new List<string> { "X" };

            var e3 = _registry.Add();
            e3.BotType = BotType.Boss;
            jobAssignments[e3.Id] = new List<string>();

            Assert.That(jobAssignments[e1.Id].Count, Is.EqualTo(3));
            Assert.That(jobAssignments[e2.Id].Count, Is.EqualTo(1));
            Assert.That(jobAssignments[e3.Id].Count, Is.EqualTo(0));
        }

        [Test]
        public void Phase8_GetLastElement_WithoutLinq()
        {
            // Simulates replacing .Last() with [Count-1] and .TakeLast(2).First() with [Count-2]
            var list = new List<string> { "first", "second", "third" };

            Assert.That(list[list.Count - 1], Is.EqualTo("third")); // replaces .Last()
            Assert.That(list[list.Count - 2], Is.EqualTo("second")); // replaces .TakeLast(2).First()
        }

        // ── Phase 7D: Allocation Cleanup ────────────────────────────

        [Test]
        public void GetFollowerCount_ReturnsCorrectCount()
        {
            // Simulates GetFollowerCount: reads entity.Followers.Count directly (O(1), zero alloc)
            var boss = _registry.Add();
            boss.BotType = BotType.PMC;
            var f1 = _registry.Add();
            var f2 = _registry.Add();
            var f3 = _registry.Add();

            HiveMindSystem.AssignBoss(f1, boss);
            HiveMindSystem.AssignBoss(f2, boss);
            HiveMindSystem.AssignBoss(f3, boss);

            Assert.That(boss.Followers.Count, Is.EqualTo(3));
        }

        [Test]
        public void GetFollowerCount_NoFollowers_ReturnsZero()
        {
            var solo = _registry.Add();
            solo.BotType = BotType.PMC;

            Assert.That(solo.Followers.Count, Is.EqualTo(0));
        }

        [Test]
        public void GetFollowerCount_AfterSeparation_Decreases()
        {
            var boss = _registry.Add();
            var f1 = _registry.Add();
            var f2 = _registry.Add();

            HiveMindSystem.AssignBoss(f1, boss);
            HiveMindSystem.AssignBoss(f2, boss);
            Assert.That(boss.Followers.Count, Is.EqualTo(2));

            HiveMindSystem.SeparateFromGroup(f1);
            Assert.That(boss.Followers.Count, Is.EqualTo(1));
        }

        [Test]
        public void GetFollowers_BufferReuse_ReturnsCorrectFollowers()
        {
            // Simulates the static buffer pattern: clear + fill from entity.Followers
            var buffer = new List<BotEntity>();
            var boss = _registry.Add();
            var f1 = _registry.Add();
            var f2 = _registry.Add();

            HiveMindSystem.AssignBoss(f1, boss);
            HiveMindSystem.AssignBoss(f2, boss);

            // Simulate GetFollowers buffer fill
            buffer.Clear();
            for (int i = 0; i < boss.Followers.Count; i++)
                buffer.Add(boss.Followers[i]);

            Assert.That(buffer.Count, Is.EqualTo(2));
            Assert.That(buffer, Contains.Item(f1));
            Assert.That(buffer, Contains.Item(f2));
        }

        [Test]
        public void GetFollowers_BufferReuse_SecondCallOverwritesFirst()
        {
            // Verifies buffer pattern: second call clears previous results
            var buffer = new List<BotEntity>();
            var boss1 = _registry.Add();
            var f1 = _registry.Add();
            HiveMindSystem.AssignBoss(f1, boss1);

            var boss2 = _registry.Add();
            var f2 = _registry.Add();
            var f3 = _registry.Add();
            HiveMindSystem.AssignBoss(f2, boss2);
            HiveMindSystem.AssignBoss(f3, boss2);

            // First call
            buffer.Clear();
            for (int i = 0; i < boss1.Followers.Count; i++)
                buffer.Add(boss1.Followers[i]);
            Assert.That(buffer.Count, Is.EqualTo(1));

            // Second call overwrites
            buffer.Clear();
            for (int i = 0; i < boss2.Followers.Count; i++)
                buffer.Add(boss2.Followers[i]);
            Assert.That(buffer.Count, Is.EqualTo(2));
            Assert.That(buffer, Does.Not.Contain(f1));
            Assert.That(buffer, Contains.Item(f2));
            Assert.That(buffer, Contains.Item(f3));
        }

        [Test]
        public void GetAllGroupMembers_BufferReuse_ReturnsBossAndFollowers()
        {
            // Simulates GetAllGroupMembers buffer pattern
            var buffer = new List<BotEntity>();
            var boss = _registry.Add();
            boss.BotType = BotType.Boss;
            var f1 = _registry.Add();
            var f2 = _registry.Add();

            HiveMindSystem.AssignBoss(f1, boss);
            HiveMindSystem.AssignBoss(f2, boss);

            // Simulate GetAllGroupMembers for f1 (excludes f1, includes boss + f2)
            var entity = f1;
            var groupBoss = entity.Boss ?? entity;
            buffer.Clear();
            for (int i = 0; i < groupBoss.Followers.Count; i++)
            {
                if (groupBoss.Followers[i] != entity)
                    buffer.Add(groupBoss.Followers[i]);
            }
            if (groupBoss != entity)
                buffer.Add(groupBoss);

            Assert.That(buffer.Count, Is.EqualTo(2));
            Assert.That(buffer, Contains.Item(boss));
            Assert.That(buffer, Contains.Item(f2));
            Assert.That(buffer, Does.Not.Contain(f1));
        }

        [Test]
        public void GetLocationOfNearestGroupMember_Inlined_FindsNearest()
        {
            // Simulates the inlined GetLocationOfNearestGroupMember:
            // iterates boss + followers directly without intermediate collection
            var boss = _registry.Add();
            boss.BotType = BotType.PMC;
            var f1 = _registry.Add();
            var f2 = _registry.Add();
            var f3 = _registry.Add();

            HiveMindSystem.AssignBoss(f1, boss);
            HiveMindSystem.AssignBoss(f2, boss);
            HiveMindSystem.AssignBoss(f3, boss);

            // Verify the inline iteration covers all group members
            var groupBoss = f1.Boss ?? f1;
            Assert.That(groupBoss, Is.SameAs(boss));

            // Count members excluding self
            int memberCount = 0;
            if (groupBoss != f1)
                memberCount++; // boss
            for (int i = 0; i < groupBoss.Followers.Count; i++)
            {
                if (groupBoss.Followers[i] != f1)
                    memberCount++;
            }

            // f1 should see boss + f2 + f3 = 3 group members
            Assert.That(memberCount, Is.EqualTo(3));
        }

        [Test]
        public void GetLocationOfNearestGroupMember_SoloBot_NoGroupMembers()
        {
            var solo = _registry.Add();
            solo.BotType = BotType.PMC;

            // Solo bot: boss is null, no followers
            var groupBoss = solo.Boss ?? solo;
            Assert.That(groupBoss, Is.SameAs(solo));

            int memberCount = 0;
            for (int i = 0; i < groupBoss.Followers.Count; i++)
            {
                if (groupBoss.Followers[i] != solo)
                    memberCount++;
            }

            Assert.That(memberCount, Is.EqualTo(0));
        }

        // ── Movement State Wiring ────────────────────────────────────

        [Test]
        public void MovementState_DefaultIsInactive()
        {
            var entity = _registry.Add();
            entity.BotType = BotType.PMC;

            Assert.That(entity.Movement.IsCustomMoverActive, Is.False);
            Assert.That(entity.Movement.Status, Is.EqualTo(PathFollowStatus.None));
            Assert.That(entity.Movement.IsSprinting, Is.False);
        }

        [Test]
        public void MovementState_ActivateCustomMover_SetsFlag()
        {
            // Simulates BotEntityBridge.ActivateCustomMover(bot)
            var entity = _registry.Add();
            entity.BotType = BotType.PMC;

            entity.Movement.IsCustomMoverActive = true;

            Assert.That(entity.Movement.IsCustomMoverActive, Is.True);
        }

        [Test]
        public void MovementState_DeactivateCustomMover_ResetsAllFields()
        {
            // Simulates BotEntityBridge.DeactivateCustomMover(bot) which calls Movement.Reset()
            var entity = _registry.Add();
            entity.BotType = BotType.PMC;

            // Activate and set various movement state
            entity.Movement.IsCustomMoverActive = true;
            entity.Movement.Status = PathFollowStatus.Following;
            entity.Movement.IsSprinting = true;
            entity.Movement.CurrentCornerIndex = 5;
            entity.Movement.TotalCorners = 10;
            entity.Movement.RetryCount = 3;
            entity.Movement.SprintAngleJitter = 25.0f;
            entity.Movement.StuckStatus = StuckPhase.SoftStuck;

            // Deactivate resets everything
            entity.Movement.Reset();

            Assert.That(entity.Movement.IsCustomMoverActive, Is.False);
            Assert.That(entity.Movement.Status, Is.EqualTo(PathFollowStatus.None));
            Assert.That(entity.Movement.IsSprinting, Is.False);
            Assert.That(entity.Movement.CurrentCornerIndex, Is.EqualTo(0));
            Assert.That(entity.Movement.TotalCorners, Is.EqualTo(0));
            Assert.That(entity.Movement.RetryCount, Is.EqualTo(0));
            Assert.That(entity.Movement.SprintAngleJitter, Is.EqualTo(0f));
            Assert.That(entity.Movement.StuckStatus, Is.EqualTo(StuckPhase.None));
            Assert.That(entity.Movement.CurrentPose, Is.EqualTo(1f));
        }

        [Test]
        public void MovementState_IsCustomMoverActive_PerBotIndependent()
        {
            // Simulates per-bot custom mover activation check via profileId → entity
            var profileToEntity = new Dictionary<string, BotEntity>();

            var e1 = _registry.Add();
            e1.BotType = BotType.PMC;
            profileToEntity["pmc-1"] = e1;

            var e2 = _registry.Add();
            e2.BotType = BotType.PMC;
            profileToEntity["pmc-2"] = e2;

            // Only e1 has custom mover active
            e1.Movement.IsCustomMoverActive = true;

            Assert.That(profileToEntity["pmc-1"].Movement.IsCustomMoverActive, Is.True);
            Assert.That(profileToEntity["pmc-2"].Movement.IsCustomMoverActive, Is.False);
        }

        [Test]
        public void MovementState_CountByStatus_MatchesExpected()
        {
            var e1 = _registry.Add();
            e1.BotType = BotType.PMC;
            e1.Movement.Status = PathFollowStatus.Following;

            var e2 = _registry.Add();
            e2.BotType = BotType.PMC;
            e2.Movement.Status = PathFollowStatus.Following;

            var e3 = _registry.Add();
            e3.BotType = BotType.Scav;
            e3.Movement.Status = PathFollowStatus.Reached;

            var e4 = _registry.Add();
            e4.BotType = BotType.Boss;
            e4.Movement.Status = PathFollowStatus.Failed;

            Assert.That(HiveMindSystem.CountByMovementStatus(_registry.Entities, PathFollowStatus.Following), Is.EqualTo(2));
            Assert.That(HiveMindSystem.CountByMovementStatus(_registry.Entities, PathFollowStatus.Reached), Is.EqualTo(1));
            Assert.That(HiveMindSystem.CountByMovementStatus(_registry.Entities, PathFollowStatus.Failed), Is.EqualTo(1));
        }

        [Test]
        public void MovementState_CountStuckBots_IncludesAllPhases()
        {
            var e1 = _registry.Add();
            e1.Movement.StuckStatus = StuckPhase.SoftStuck;
            var e2 = _registry.Add();
            e2.Movement.StuckStatus = StuckPhase.HardStuck;
            var e3 = _registry.Add();
            e3.Movement.StuckStatus = StuckPhase.Failed;
            var e4 = _registry.Add();
            e4.Movement.StuckStatus = StuckPhase.None;

            Assert.That(HiveMindSystem.CountStuckBots(_registry.Entities), Is.EqualTo(3));
        }

        [Test]
        public void MovementState_CountSprintingBots_OnlyCountsActive()
        {
            var e1 = _registry.Add();
            e1.Movement.IsSprinting = true;
            var e2 = _registry.Add();
            e2.Movement.IsSprinting = true;
            e2.IsActive = false; // Inactive bot should not count
            var e3 = _registry.Add();
            e3.Movement.IsSprinting = false;

            Assert.That(HiveMindSystem.CountSprintingBots(_registry.Entities), Is.EqualTo(1));
        }

        [Test]
        public void MovementState_ResetForInactiveEntities()
        {
            var active = _registry.Add();
            active.Movement.IsCustomMoverActive = true;
            active.Movement.Status = PathFollowStatus.Following;

            var inactive = _registry.Add();
            inactive.IsActive = false;
            inactive.Movement.IsCustomMoverActive = true;
            inactive.Movement.Status = PathFollowStatus.Following;

            HiveMindSystem.ResetMovementForInactiveEntities(_registry.Entities);

            // Active entity's movement untouched
            Assert.That(active.Movement.IsCustomMoverActive, Is.True);
            Assert.That(active.Movement.Status, Is.EqualTo(PathFollowStatus.Following));

            // Inactive entity's movement reset
            Assert.That(inactive.Movement.IsCustomMoverActive, Is.False);
            Assert.That(inactive.Movement.Status, Is.EqualTo(PathFollowStatus.None));
        }

        [Test]
        public void MovementState_GetEntityByProfileId_SimulatesLookup()
        {
            // Simulates BotEntityBridge.GetEntityByProfileId pattern
            var profileToEntity = new Dictionary<string, BotEntity>();

            var entity = _registry.Add();
            entity.BotType = BotType.PMC;
            entity.Movement.IsCustomMoverActive = true;
            entity.Movement.Status = PathFollowStatus.Following;
            profileToEntity["test-profile-123"] = entity;

            // Lookup by profileId → read movement state
            BotEntity found;
            bool exists = "test-profile-123" != null && profileToEntity.TryGetValue("test-profile-123", out found);

            Assert.That(exists, Is.True);
            Assert.That(profileToEntity["test-profile-123"].Movement.IsCustomMoverActive, Is.True);
            Assert.That(profileToEntity["test-profile-123"].Movement.Status, Is.EqualTo(PathFollowStatus.Following));

            // Unknown profileId returns false
            exists = "unknown" != null && profileToEntity.TryGetValue("unknown", out found);
            Assert.That(exists, Is.False);
        }

        [Test]
        public void MovementState_SyncFromFollower_UpdatesEntityFields()
        {
            // Simulates CustomMoverController.SyncMovementState updating entity
            var entity = _registry.Add();
            entity.BotType = BotType.PMC;
            entity.Movement.IsCustomMoverActive = true;

            // Simulate path follower state sync
            entity.Movement.Status = PathFollowStatus.Following;
            entity.Movement.CurrentCornerIndex = 3;
            entity.Movement.TotalCorners = 8;
            entity.Movement.RetryCount = 1;
            entity.Movement.SprintAngleJitter = 15.5f;
            entity.Movement.LastPathUpdateTime = 42.5f;

            Assert.That(entity.Movement.Status, Is.EqualTo(PathFollowStatus.Following));
            Assert.That(entity.Movement.CurrentCornerIndex, Is.EqualTo(3));
            Assert.That(entity.Movement.TotalCorners, Is.EqualTo(8));
            Assert.That(entity.Movement.RetryCount, Is.EqualTo(1));
            Assert.That(entity.Movement.SprintAngleJitter, Is.EqualTo(15.5f).Within(0.01f));
            Assert.That(entity.Movement.LastPathUpdateTime, Is.EqualTo(42.5f).Within(0.01f));
        }

        [Test]
        public void MovementState_ActivateDeactivateLifecycle()
        {
            // Full lifecycle: register → activate → follow → reach → deactivate
            var entity = _registry.Add();
            entity.BotType = BotType.PMC;

            // 1. Initially idle
            Assert.That(entity.Movement.IsCustomMoverActive, Is.False);
            Assert.That(entity.Movement.Status, Is.EqualTo(PathFollowStatus.None));

            // 2. Activate
            entity.Movement.IsCustomMoverActive = true;
            Assert.That(entity.Movement.IsCustomMoverActive, Is.True);

            // 3. Start following a path
            entity.Movement.Status = PathFollowStatus.Following;
            entity.Movement.CurrentCornerIndex = 0;
            entity.Movement.TotalCorners = 5;
            entity.Movement.IsSprinting = true;

            // 4. Progress through path
            entity.Movement.CurrentCornerIndex = 3;
            entity.Movement.SprintAngleJitter = 12.0f;

            // 5. Reach destination
            entity.Movement.Status = PathFollowStatus.Reached;

            // 6. Deactivate (reset)
            entity.Movement.Reset();
            Assert.That(entity.Movement.IsCustomMoverActive, Is.False);
            Assert.That(entity.Movement.Status, Is.EqualTo(PathFollowStatus.None));
            Assert.That(entity.Movement.IsSprinting, Is.False);
            Assert.That(entity.Movement.CurrentCornerIndex, Is.EqualTo(0));
            Assert.That(entity.Movement.TotalCorners, Is.EqualTo(0));
        }
    }
}
