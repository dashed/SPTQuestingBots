using System;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.Configuration;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.Systems
{
    [TestFixture]
    public class CombatPositionAdjusterTests
    {
        private SquadStrategyConfig DefaultConfig()
        {
            return new SquadStrategyConfig
            {
                GuardDistance = 8f,
                FlankDistance = 15f,
                OverwatchDistance = 25f,
                EscortDistance = 5f,
            };
        }

        // ── ReassignRolesForCombat ────────────────────────

        [Test]
        public void ReassignRolesForCombat_EscortBecomesFlanker()
        {
            var current = new[] { SquadRole.Escort };
            var output = new SquadRole[1];

            CombatPositionAdjuster.ReassignRolesForCombat(current, 1, output);

            Assert.AreEqual(SquadRole.Flanker, output[0]);
        }

        [Test]
        public void ReassignRolesForCombat_GuardStaysGuard()
        {
            var current = new[] { SquadRole.Guard };
            var output = new SquadRole[1];

            CombatPositionAdjuster.ReassignRolesForCombat(current, 1, output);

            Assert.AreEqual(SquadRole.Guard, output[0]);
        }

        [Test]
        public void ReassignRolesForCombat_OverwatchStaysOverwatch()
        {
            var current = new[] { SquadRole.Overwatch };
            var output = new SquadRole[1];

            CombatPositionAdjuster.ReassignRolesForCombat(current, 1, output);

            Assert.AreEqual(SquadRole.Overwatch, output[0]);
        }

        [Test]
        public void ReassignRolesForCombat_FlankerStaysFlanker()
        {
            var current = new[] { SquadRole.Flanker };
            var output = new SquadRole[1];

            CombatPositionAdjuster.ReassignRolesForCombat(current, 1, output);

            Assert.AreEqual(SquadRole.Flanker, output[0]);
        }

        [Test]
        public void ReassignRolesForCombat_MixedRoles_OnlyEscortChanges()
        {
            var current = new[] { SquadRole.Guard, SquadRole.Escort, SquadRole.Overwatch, SquadRole.Flanker };
            var output = new SquadRole[4];

            CombatPositionAdjuster.ReassignRolesForCombat(current, 4, output);

            Assert.AreEqual(SquadRole.Guard, output[0]);
            Assert.AreEqual(SquadRole.Flanker, output[1]); // Escort → Flanker
            Assert.AreEqual(SquadRole.Overwatch, output[2]);
            Assert.AreEqual(SquadRole.Flanker, output[3]);
        }

        [Test]
        public void ReassignRolesForCombat_EmptyArray_NoCrash()
        {
            var current = new SquadRole[0];
            var output = new SquadRole[0];

            Assert.DoesNotThrow(() => CombatPositionAdjuster.ReassignRolesForCombat(current, 0, output));
        }

        [Test]
        public void ReassignRolesForCombat_AllEscorts_AllBecomeFlankers()
        {
            var current = new[] { SquadRole.Escort, SquadRole.Escort, SquadRole.Escort };
            var output = new SquadRole[3];

            CombatPositionAdjuster.ReassignRolesForCombat(current, 3, output);

            Assert.AreEqual(SquadRole.Flanker, output[0]);
            Assert.AreEqual(SquadRole.Flanker, output[1]);
            Assert.AreEqual(SquadRole.Flanker, output[2]);
        }

        // ── Guard Positions (threat-biased arc) ───────────

        [Test]
        public void Guard_SingleGuard_FacesThreat()
        {
            var config = DefaultConfig();
            var roles = new[] { SquadRole.Guard };
            var positions = new float[3];

            // Threat from +X direction (east)
            CombatPositionAdjuster.ComputeCombatPositions(0f, 0f, 0f, 1f, 0f, roles, 1, config, positions);

            // Single guard: index=0, count=1, spread = 180/1 = 180
            // angleDeg = atan2(0,1)*(180/PI) + (0 - 0)*180 = 0
            // x = 0 + 8*cos(0) = 8, z = 0 + 8*sin(0) = 0
            Assert.AreEqual(8f, positions[0], 0.01f);
            Assert.AreEqual(0f, positions[1], 0.01f);
            Assert.AreEqual(0f, positions[2], 0.01f);
        }

        [Test]
        public void Guard_TwoGuards_SpreadAroundThreat()
        {
            var config = DefaultConfig();
            var roles = new[] { SquadRole.Guard, SquadRole.Guard };
            var positions = new float[6];

            // Threat from +X direction (east)
            CombatPositionAdjuster.ComputeCombatPositions(0f, 0f, 0f, 1f, 0f, roles, 2, config, positions);

            // Guard 0: angleDeg = 0 + (0 - 0.5) * 90 = -45
            // Guard 1: angleDeg = 0 + (1 - 0.5) * 90 = +45
            float rad0 = -45f * (float)(Math.PI / 180.0);
            float rad1 = 45f * (float)(Math.PI / 180.0);

            Assert.AreEqual(8f * (float)Math.Cos(rad0), positions[0], 0.01f);
            Assert.AreEqual(8f * (float)Math.Sin(rad0), positions[2], 0.01f);
            Assert.AreEqual(8f * (float)Math.Cos(rad1), positions[3], 0.01f);
            Assert.AreEqual(8f * (float)Math.Sin(rad1), positions[5], 0.01f);
        }

        [Test]
        public void Guard_CorrectDistance()
        {
            var config = DefaultConfig();
            var roles = new[] { SquadRole.Guard };
            var positions = new float[3];

            CombatPositionAdjuster.ComputeCombatPositions(10f, 5f, 10f, 0f, 1f, roles, 1, config, positions);

            float dx = positions[0] - 10f;
            float dz = positions[2] - 10f;
            float dist = (float)Math.Sqrt(dx * dx + dz * dz);

            Assert.AreEqual(config.GuardDistance, dist, 0.01f);
            Assert.AreEqual(5f, positions[1], 0.01f); // Y preserved
        }

        // ── Flanker Positions ─────────────────────────────

        [Test]
        public void Flanker_PerpendicularToThreat()
        {
            var config = DefaultConfig();
            var roles = new[] { SquadRole.Flanker };
            var positions = new float[3];

            // Threat from +X: threatDir = (1, 0)
            // Perp = (0, 1), side = +1 (index 0 is even)
            CombatPositionAdjuster.ComputeCombatPositions(0f, 0f, 0f, 1f, 0f, roles, 1, config, positions);

            // x = 0 + 0 * 15 * 1 = 0, z = 0 + 1 * 15 * 1 = 15
            Assert.AreEqual(0f, positions[0], 0.01f);
            Assert.AreEqual(0f, positions[1], 0.01f);
            Assert.AreEqual(15f, positions[2], 0.01f);
        }

        [Test]
        public void Flanker_TwoFlankers_OppositeSides()
        {
            var config = DefaultConfig();
            var roles = new[] { SquadRole.Flanker, SquadRole.Flanker };
            var positions = new float[6];

            // Threat from +X
            CombatPositionAdjuster.ComputeCombatPositions(0f, 0f, 0f, 1f, 0f, roles, 2, config, positions);

            // Index 0 (even): side = +1 → z = +15
            Assert.AreEqual(0f, positions[0], 0.01f);
            Assert.AreEqual(15f, positions[2], 0.01f);

            // Index 1 (odd): side = -1 → z = -15
            Assert.AreEqual(0f, positions[3], 0.01f);
            Assert.AreEqual(-15f, positions[5], 0.01f);
        }

        [Test]
        public void Flanker_CorrectDistance()
        {
            var config = DefaultConfig();
            var roles = new[] { SquadRole.Flanker };
            var positions = new float[3];

            CombatPositionAdjuster.ComputeCombatPositions(5f, 2f, 5f, 0f, 1f, roles, 1, config, positions);

            float dx = positions[0] - 5f;
            float dz = positions[2] - 5f;
            float dist = (float)Math.Sqrt(dx * dx + dz * dz);

            Assert.AreEqual(config.FlankDistance, dist, 0.01f);
        }

        // ── Overwatch Positions ───────────────────────────

        [Test]
        public void Overwatch_BehindObjective()
        {
            var config = DefaultConfig();
            var roles = new[] { SquadRole.Overwatch };
            var positions = new float[3];

            // Threat from +X: overwatch goes opposite → -X
            CombatPositionAdjuster.ComputeCombatPositions(0f, 0f, 0f, 1f, 0f, roles, 1, config, positions);

            Assert.AreEqual(-25f, positions[0], 0.01f);
            Assert.AreEqual(0f, positions[1], 0.01f);
            Assert.AreEqual(0f, positions[2], 0.01f);
        }

        [Test]
        public void Overwatch_CorrectDistance()
        {
            var config = DefaultConfig();
            var roles = new[] { SquadRole.Overwatch };
            var positions = new float[3];

            CombatPositionAdjuster.ComputeCombatPositions(10f, 3f, 10f, 0f, 1f, roles, 1, config, positions);

            float dx = positions[0] - 10f;
            float dz = positions[2] - 10f;
            float dist = (float)Math.Sqrt(dx * dx + dz * dz);

            Assert.AreEqual(config.OverwatchDistance, dist, 0.01f);
            Assert.AreEqual(3f, positions[1], 0.01f); // Y preserved
        }

        // ── Integration: all roles with specific threat dirs ─

        [Test]
        public void ThreatFromNorth_AllRoles()
        {
            var config = DefaultConfig();
            // 4 members: Guard, Flanker, Overwatch, Guard
            var roles = new[] { SquadRole.Guard, SquadRole.Flanker, SquadRole.Overwatch, SquadRole.Guard };
            var positions = new float[12];

            // Threat from north: threatDir = (0, 1)
            CombatPositionAdjuster.ComputeCombatPositions(0f, 0f, 0f, 0f, 1f, roles, 4, config, positions);

            // Guard 0 (i=0, count=4): baseAngle = atan2(1,0)*180/PI = 90
            // spread = 180/4 = 45, angle = 90 + (0-1.5)*45 = 90 - 67.5 = 22.5
            float rad0 = 22.5f * (float)(Math.PI / 180.0);
            Assert.AreEqual(8f * (float)Math.Cos(rad0), positions[0], 0.1f);
            Assert.AreEqual(8f * (float)Math.Sin(rad0), positions[2], 0.1f);

            // Flanker 1 (i=1): perp of (0,1) = (-1,0), side = -1 (odd index)
            // x = 0 + (-1)*15*(-1) = 15, z = 0 + 0*15*(-1) = 0
            Assert.AreEqual(15f, positions[3], 0.01f);
            Assert.AreEqual(0f, positions[5], 0.01f);

            // Overwatch (i=2): opposite threat → (0, -25)
            Assert.AreEqual(0f, positions[6], 0.01f);
            Assert.AreEqual(-25f, positions[8], 0.01f);

            // Guard 3 (i=3, count=4): angle = 90 + (3-1.5)*45 = 90 + 67.5 = 157.5
            float rad3 = 157.5f * (float)(Math.PI / 180.0);
            Assert.AreEqual(8f * (float)Math.Cos(rad3), positions[9], 0.1f);
            Assert.AreEqual(8f * (float)Math.Sin(rad3), positions[11], 0.1f);
        }

        [Test]
        public void ThreatFromEast_AllRoles()
        {
            var config = DefaultConfig();
            var roles = new[] { SquadRole.Guard, SquadRole.Flanker, SquadRole.Overwatch };
            var positions = new float[9];

            // Threat from east: threatDir = (1, 0)
            CombatPositionAdjuster.ComputeCombatPositions(0f, 0f, 0f, 1f, 0f, roles, 3, config, positions);

            // Guard 0 (i=0, count=3): base=0, spread=60, angle = 0 + (0-1)*60 = -60
            float rad0 = -60f * (float)(Math.PI / 180.0);
            Assert.AreEqual(8f * (float)Math.Cos(rad0), positions[0], 0.1f);
            Assert.AreEqual(8f * (float)Math.Sin(rad0), positions[2], 0.1f);

            // Flanker 1 (i=1): perp of (1,0) = (0,1), side = -1 (odd)
            // x = 0, z = 0 + 1*15*(-1) = -15
            Assert.AreEqual(0f, positions[3], 0.01f);
            Assert.AreEqual(-15f, positions[5], 0.01f);

            // Overwatch 2: opposite threat → (-25, 0)
            Assert.AreEqual(-25f, positions[6], 0.01f);
            Assert.AreEqual(0f, positions[8], 0.01f);
        }

        [Test]
        public void ThreatFromSouthWest()
        {
            var config = DefaultConfig();
            var roles = new[] { SquadRole.Overwatch };
            var positions = new float[3];

            // Threat from SW: threatDir = (-0.707, -0.707)
            float d = (float)(1.0 / Math.Sqrt(2.0));
            CombatPositionAdjuster.ComputeCombatPositions(0f, 0f, 0f, -d, -d, roles, 1, config, positions);

            // Overwatch: opposite threat → +d * 25 in both axes
            Assert.AreEqual(d * 25f, positions[0], 0.1f);
            Assert.AreEqual(d * 25f, positions[2], 0.1f);
        }

        // ── Edge Cases ────────────────────────────────────

        [Test]
        public void ZeroThreatDirection_PositionsAtObjective()
        {
            var config = DefaultConfig();
            var roles = new[] { SquadRole.Guard, SquadRole.Flanker, SquadRole.Overwatch };
            var positions = new float[9];

            CombatPositionAdjuster.ComputeCombatPositions(10f, 5f, 20f, 0f, 0f, roles, 3, config, positions);

            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(10f, positions[i * 3], 0.01f);
                Assert.AreEqual(5f, positions[i * 3 + 1], 0.01f);
                Assert.AreEqual(20f, positions[i * 3 + 2], 0.01f);
            }
        }

        [Test]
        public void DefaultRole_PositionedAtObjective()
        {
            var config = DefaultConfig();
            var roles = new[] { SquadRole.None };
            var positions = new float[3];

            CombatPositionAdjuster.ComputeCombatPositions(10f, 5f, 20f, 1f, 0f, roles, 1, config, positions);

            Assert.AreEqual(10f, positions[0], 0.01f);
            Assert.AreEqual(5f, positions[1], 0.01f);
            Assert.AreEqual(20f, positions[2], 0.01f);
        }

        [Test]
        public void LeaderRole_PositionedAtObjective()
        {
            var config = DefaultConfig();
            var roles = new[] { SquadRole.Leader };
            var positions = new float[3];

            CombatPositionAdjuster.ComputeCombatPositions(10f, 5f, 20f, 1f, 0f, roles, 1, config, positions);

            Assert.AreEqual(10f, positions[0], 0.01f);
            Assert.AreEqual(5f, positions[1], 0.01f);
            Assert.AreEqual(20f, positions[2], 0.01f);
        }

        [Test]
        public void YCoordinate_AlwaysPreserved()
        {
            var config = DefaultConfig();
            var roles = new[] { SquadRole.Guard, SquadRole.Flanker, SquadRole.Overwatch };
            var positions = new float[9];

            CombatPositionAdjuster.ComputeCombatPositions(0f, 42f, 0f, 1f, 0f, roles, 3, config, positions);

            Assert.AreEqual(42f, positions[1], 0.01f);
            Assert.AreEqual(42f, positions[4], 0.01f);
            Assert.AreEqual(42f, positions[7], 0.01f);
        }

        [Test]
        public void ObjectiveOffset_PositionsRelativeToObjective()
        {
            var config = DefaultConfig();
            var roles = new[] { SquadRole.Overwatch };
            var positions = new float[3];

            // Objective at (100, 0, 100), threat from +X
            CombatPositionAdjuster.ComputeCombatPositions(100f, 0f, 100f, 1f, 0f, roles, 1, config, positions);

            // Overwatch: 100 - 1*25 = 75, z stays 100
            Assert.AreEqual(75f, positions[0], 0.01f);
            Assert.AreEqual(100f, positions[2], 0.01f);
        }
    }
}
