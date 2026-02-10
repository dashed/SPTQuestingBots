using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS.Systems;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.Systems
{
    [TestFixture]
    public class ObjectiveSharingCalculatorTests
    {
        // ── Shared test helpers ──────────────────────────────────────

        private const float NoEarpieceRange = 35f;
        private const float EarpieceRange = 200f;

        // Leader at origin
        private const float LeaderX = 0f;
        private const float LeaderY = 0f;
        private const float LeaderZ = 0f;

        // ── AssignTiers tests ────────────────────────────────────────

        [Test]
        public void AssignTiers_AllInRange_TrustedCount2_ClosestAreTier1_RestTier2()
        {
            // 3 followers, all within 10m of leader, trust=2
            float[] fx = { 5f, 3f, 8f };
            float[] fy = { 0f, 0f, 0f };
            float[] fz = { 0f, 0f, 0f };
            bool[] fe = { false, false, false };
            byte[] tiers = new byte[3];

            ObjectiveSharingCalculator.AssignTiers(
                LeaderX,
                LeaderY,
                LeaderZ,
                false,
                fx,
                fy,
                fz,
                fe,
                3,
                2,
                NoEarpieceRange,
                EarpieceRange,
                true,
                tiers
            );

            // Sorted by distance: idx=1(3m), idx=0(5m), idx=2(8m)
            // Trust=2: idx=1 → Tier1, idx=0 → Tier1, idx=2 → Tier2
            Assert.That(tiers[0], Is.EqualTo(ObjectiveSharingCalculator.TierDirect));
            Assert.That(tiers[1], Is.EqualTo(ObjectiveSharingCalculator.TierDirect));
            Assert.That(tiers[2], Is.EqualTo(ObjectiveSharingCalculator.TierRelayed));
        }

        [Test]
        public void AssignTiers_FollowerOutOfLeaderRange_NotTier1_ButRelayable()
        {
            // 2 followers: one at 10m (in range), one at 40m (out of 35m no-earpiece range)
            // Follower 1 is 30m from follower 0 → within relay range (35m)
            float[] fx = { 10f, 40f };
            float[] fy = { 0f, 0f };
            float[] fz = { 0f, 0f };
            bool[] fe = { false, false };
            byte[] tiers = new byte[2];

            ObjectiveSharingCalculator.AssignTiers(
                LeaderX,
                LeaderY,
                LeaderZ,
                false,
                fx,
                fy,
                fz,
                fe,
                2,
                2,
                NoEarpieceRange,
                EarpieceRange,
                true,
                tiers
            );

            Assert.That(tiers[0], Is.EqualTo(ObjectiveSharingCalculator.TierDirect));
            // idx=1 (40m) out of leader range (35m), but within follower 0 relay range (30m < 35m) → Tier2
            Assert.That(tiers[1], Is.EqualTo(ObjectiveSharingCalculator.TierRelayed));
        }

        [Test]
        public void AssignTiers_Tier2FollowerOutOfTier1Range_RemainsNone()
        {
            // Leader at origin, follower 0 at 10m (in range, Tier1)
            // Follower 1 at 100m (out of leader + out of follower 0 range)
            float[] fx = { 10f, 100f };
            float[] fy = { 0f, 0f };
            float[] fz = { 0f, 0f };
            bool[] fe = { false, false };
            byte[] tiers = new byte[2];

            ObjectiveSharingCalculator.AssignTiers(
                LeaderX,
                LeaderY,
                LeaderZ,
                false,
                fx,
                fy,
                fz,
                fe,
                2,
                1,
                NoEarpieceRange,
                EarpieceRange,
                true,
                tiers
            );

            Assert.That(tiers[0], Is.EqualTo(ObjectiveSharingCalculator.TierDirect));
            // 100m - 10m = 90m > 35m → out of range
            Assert.That(tiers[1], Is.EqualTo(ObjectiveSharingCalculator.TierNone));
        }

        [Test]
        public void AssignTiers_TrustedCountZero_AllRemainNone()
        {
            float[] fx = { 5f };
            float[] fy = { 0f };
            float[] fz = { 0f };
            bool[] fe = { false };
            byte[] tiers = new byte[1];

            ObjectiveSharingCalculator.AssignTiers(
                LeaderX,
                LeaderY,
                LeaderZ,
                false,
                fx,
                fy,
                fz,
                fe,
                1,
                0,
                NoEarpieceRange,
                EarpieceRange,
                true,
                tiers
            );

            // Trust=0 means nobody gets Tier1, so nobody can relay → all TierNone
            Assert.That(tiers[0], Is.EqualTo(ObjectiveSharingCalculator.TierNone));
        }

        [Test]
        public void AssignTiers_TrustedCountExceedsFollowerCount_AllTier1()
        {
            float[] fx = { 5f, 10f };
            float[] fy = { 0f, 0f };
            float[] fz = { 0f, 0f };
            bool[] fe = { false, false };
            byte[] tiers = new byte[2];

            ObjectiveSharingCalculator.AssignTiers(
                LeaderX,
                LeaderY,
                LeaderZ,
                false,
                fx,
                fy,
                fz,
                fe,
                2,
                10,
                NoEarpieceRange,
                EarpieceRange,
                true,
                tiers
            );

            Assert.That(tiers[0], Is.EqualTo(ObjectiveSharingCalculator.TierDirect));
            Assert.That(tiers[1], Is.EqualTo(ObjectiveSharingCalculator.TierDirect));
        }

        [Test]
        public void AssignTiers_SingleFollower_GetsTier1()
        {
            float[] fx = { 5f };
            float[] fy = { 0f };
            float[] fz = { 0f };
            bool[] fe = { false };
            byte[] tiers = new byte[1];

            ObjectiveSharingCalculator.AssignTiers(
                LeaderX,
                LeaderY,
                LeaderZ,
                false,
                fx,
                fy,
                fz,
                fe,
                1,
                1,
                NoEarpieceRange,
                EarpieceRange,
                true,
                tiers
            );

            Assert.That(tiers[0], Is.EqualTo(ObjectiveSharingCalculator.TierDirect));
        }

        [Test]
        public void AssignTiers_ZeroFollowers_NoOp()
        {
            float[] fx = { };
            float[] fy = { };
            float[] fz = { };
            bool[] fe = { };
            byte[] tiers = new byte[0];

            // Should not throw
            ObjectiveSharingCalculator.AssignTiers(
                LeaderX,
                LeaderY,
                LeaderZ,
                false,
                fx,
                fy,
                fz,
                fe,
                0,
                1,
                NoEarpieceRange,
                EarpieceRange,
                true,
                tiers
            );
        }

        [Test]
        public void AssignTiers_CommRangeDisabled_AllAssignedByProximity()
        {
            // Followers at 1000m — normally out of range, but comm range disabled
            float[] fx = { 1000f, 500f };
            float[] fy = { 0f, 0f };
            float[] fz = { 0f, 0f };
            bool[] fe = { false, false };
            byte[] tiers = new byte[2];

            ObjectiveSharingCalculator.AssignTiers(
                LeaderX,
                LeaderY,
                LeaderZ,
                false,
                fx,
                fy,
                fz,
                fe,
                2,
                1,
                NoEarpieceRange,
                EarpieceRange,
                false,
                tiers
            );

            // Sorted: idx=1(500m) → Tier1 (trust=1), idx=0(1000m) → Tier2 (relay from idx=1)
            Assert.That(tiers[1], Is.EqualTo(ObjectiveSharingCalculator.TierDirect));
            Assert.That(tiers[0], Is.EqualTo(ObjectiveSharingCalculator.TierRelayed));
        }

        [Test]
        public void AssignTiers_MixedEarpieceStates()
        {
            // Leader has earpiece, followers at 40m and 180m
            // Follower 0 (40m, no earpiece): uses noEarpiece range (35m) → out of range
            // Follower 1 (30m, has earpiece): uses earpiece range (200m) → in range
            float[] fx = { 40f, 30f };
            float[] fy = { 0f, 0f };
            float[] fz = { 0f, 0f };
            bool[] fe = { false, true };
            byte[] tiers = new byte[2];

            ObjectiveSharingCalculator.AssignTiers(
                LeaderX,
                LeaderY,
                LeaderZ,
                true,
                fx,
                fy,
                fz,
                fe,
                2,
                2,
                NoEarpieceRange,
                EarpieceRange,
                true,
                tiers
            );

            // idx=1(30m, earpiece) → Tier1 (in earpiece range)
            // idx=0(40m, no earpiece) → leader has earpiece but follower doesn't → noEarpiece range (35m) → out of range
            // But idx=0 can relay from idx=1: dist=10m, idx=1 has earpiece, idx=0 doesn't → noEarpiece range → in range
            Assert.That(tiers[1], Is.EqualTo(ObjectiveSharingCalculator.TierDirect));
            Assert.That(tiers[0], Is.EqualTo(ObjectiveSharingCalculator.TierRelayed));
        }

        [Test]
        public void AssignTiers_ThreeFollowers_Trust1_OneTier1_TwoTier2()
        {
            float[] fx = { 10f, 5f, 15f };
            float[] fy = { 0f, 0f, 0f };
            float[] fz = { 0f, 0f, 0f };
            bool[] fe = { false, false, false };
            byte[] tiers = new byte[3];

            ObjectiveSharingCalculator.AssignTiers(
                LeaderX,
                LeaderY,
                LeaderZ,
                false,
                fx,
                fy,
                fz,
                fe,
                3,
                1,
                NoEarpieceRange,
                EarpieceRange,
                true,
                tiers
            );

            // Sorted: idx=1(5m) → Tier1 (closest, trust=1)
            // idx=0(10m) → relay from idx=1 (5m apart, in range) → Tier2
            // idx=2(15m) → relay from idx=1 (10m apart, in range) → Tier2
            Assert.That(tiers[1], Is.EqualTo(ObjectiveSharingCalculator.TierDirect));
            Assert.That(tiers[0], Is.EqualTo(ObjectiveSharingCalculator.TierRelayed));
            Assert.That(tiers[2], Is.EqualTo(ObjectiveSharingCalculator.TierRelayed));
        }

        [Test]
        public void AssignTiers_FollowerCountClampedToMaxMembers()
        {
            // Pass followerCount > MaxMembers, should not crash
            int max = 6; // SquadObjective.MaxMembers
            float[] fx = new float[max + 2];
            float[] fy = new float[max + 2];
            float[] fz = new float[max + 2];
            bool[] fe = new bool[max + 2];
            byte[] tiers = new byte[max + 2];

            for (int i = 0; i < max + 2; i++)
            {
                fx[i] = (i + 1) * 2f;
            }

            ObjectiveSharingCalculator.AssignTiers(
                LeaderX,
                LeaderY,
                LeaderZ,
                false,
                fx,
                fy,
                fz,
                fe,
                max + 2,
                max,
                NoEarpieceRange,
                EarpieceRange,
                true,
                tiers
            );

            // First 6 should be assigned, indices 6+ should remain 0 (never touched)
            int assigned = 0;
            for (int i = 0; i < max; i++)
            {
                if (tiers[i] != ObjectiveSharingCalculator.TierNone)
                    assigned++;
            }

            Assert.That(assigned, Is.GreaterThan(0));
            Assert.That(tiers[max], Is.EqualTo(ObjectiveSharingCalculator.TierNone));
            Assert.That(tiers[max + 1], Is.EqualTo(ObjectiveSharingCalculator.TierNone));
        }

        [Test]
        public void AssignTiers_LeaderOutOfRange_NoTier1_NoRelayPossible()
        {
            // All followers far from leader (50m > 35m no-earpiece range)
            // No earpieces → all out of leader range → no Tier1 → no Tier2
            float[] fx = { 50f, 55f };
            float[] fy = { 0f, 0f };
            float[] fz = { 0f, 0f };
            bool[] fe = { false, false };
            byte[] tiers = new byte[2];

            ObjectiveSharingCalculator.AssignTiers(
                LeaderX,
                LeaderY,
                LeaderZ,
                false,
                fx,
                fy,
                fz,
                fe,
                2,
                2,
                NoEarpieceRange,
                EarpieceRange,
                true,
                tiers
            );

            Assert.That(tiers[0], Is.EqualTo(ObjectiveSharingCalculator.TierNone));
            Assert.That(tiers[1], Is.EqualTo(ObjectiveSharingCalculator.TierNone));
        }

        // ── DegradePositions tests ───────────────────────────────────

        [Test]
        public void DegradePositions_Tier1PositionsUnchanged()
        {
            byte[] tiers = { ObjectiveSharingCalculator.TierDirect };
            float[] positions = { 10f, 5f, 20f };
            var rng = new System.Random(42);

            ObjectiveSharingCalculator.DegradePositions(tiers, 1, positions, 3f, 5f, rng);

            Assert.That(positions[0], Is.EqualTo(10f));
            Assert.That(positions[1], Is.EqualTo(5f));
            Assert.That(positions[2], Is.EqualTo(20f));
        }

        [Test]
        public void DegradePositions_Tier2_XZModified()
        {
            byte[] tiers = { ObjectiveSharingCalculator.TierRelayed };
            float[] positions = { 10f, 5f, 20f };
            var rng = new System.Random(42);

            ObjectiveSharingCalculator.DegradePositions(tiers, 1, positions, 3f, 5f, rng);

            // X and Z should be modified (non-zero noise)
            Assert.That(positions[0], Is.Not.EqualTo(10f));
            Assert.That(positions[2], Is.Not.EqualTo(20f));
        }

        [Test]
        public void DegradePositions_Tier2_YUnchanged()
        {
            byte[] tiers = { ObjectiveSharingCalculator.TierRelayed };
            float[] positions = { 10f, 5f, 20f };
            var rng = new System.Random(42);

            ObjectiveSharingCalculator.DegradePositions(tiers, 1, positions, 3f, 5f, rng);

            Assert.That(positions[1], Is.EqualTo(5f));
        }

        [Test]
        public void DegradePositions_HigherCoordination_SmallerNoise()
        {
            // CoordinationLevel=5 → noiseScale = 5 * (6-5)/5 = 1
            byte[] tiers1 = { ObjectiveSharingCalculator.TierRelayed };
            float[] pos1 = { 100f, 0f, 100f };
            var rng1 = new System.Random(123);
            ObjectiveSharingCalculator.DegradePositions(tiers1, 1, pos1, 5f, 5f, rng1);
            float dx5 = System.Math.Abs(pos1[0] - 100f);

            // CoordinationLevel=1 → noiseScale = 5 * (6-1)/5 = 5
            byte[] tiers2 = { ObjectiveSharingCalculator.TierRelayed };
            float[] pos2 = { 100f, 0f, 100f };
            var rng2 = new System.Random(123);
            ObjectiveSharingCalculator.DegradePositions(tiers2, 1, pos2, 1f, 5f, rng2);
            float dx1 = System.Math.Abs(pos2[0] - 100f);

            // Same seed → same Gaussian sample, but coord=1 has 5x larger scale
            Assert.That(dx1, Is.GreaterThan(dx5));
            Assert.That(dx1, Is.EqualTo(dx5 * 5f).Within(0.001f));
        }

        [Test]
        public void DegradePositions_TierNone_PositionsUnchanged()
        {
            byte[] tiers = { ObjectiveSharingCalculator.TierNone };
            float[] positions = { 10f, 5f, 20f };
            var rng = new System.Random(42);

            ObjectiveSharingCalculator.DegradePositions(tiers, 1, positions, 3f, 5f, rng);

            Assert.That(positions[0], Is.EqualTo(10f));
            Assert.That(positions[1], Is.EqualTo(5f));
            Assert.That(positions[2], Is.EqualTo(20f));
        }

        [Test]
        public void DegradePositions_NaNPositions_Skipped()
        {
            byte[] tiers = { ObjectiveSharingCalculator.TierRelayed, ObjectiveSharingCalculator.TierRelayed };
            float[] positions = { float.NaN, 5f, float.NaN, 10f, 5f, 20f };
            var rng = new System.Random(42);

            ObjectiveSharingCalculator.DegradePositions(tiers, 2, positions, 3f, 5f, rng);

            // First follower (NaN) should be skipped
            Assert.That(float.IsNaN(positions[0]), Is.True);
            Assert.That(positions[1], Is.EqualTo(5f));
            Assert.That(float.IsNaN(positions[2]), Is.True);

            // Second follower should be modified
            Assert.That(positions[3], Is.Not.EqualTo(10f));
            Assert.That(positions[4], Is.EqualTo(5f)); // Y unchanged
        }

        [Test]
        public void DegradePositions_BaseNoiseZero_NoModification()
        {
            byte[] tiers = { ObjectiveSharingCalculator.TierRelayed };
            float[] positions = { 10f, 5f, 20f };
            var rng = new System.Random(42);

            ObjectiveSharingCalculator.DegradePositions(tiers, 1, positions, 3f, 0f, rng);

            Assert.That(positions[0], Is.EqualTo(10f));
            Assert.That(positions[1], Is.EqualTo(5f));
            Assert.That(positions[2], Is.EqualTo(20f));
        }

        [Test]
        public void DegradePositions_Deterministic_SameSeedSameResult()
        {
            byte[] tiers1 = { ObjectiveSharingCalculator.TierRelayed };
            float[] pos1 = { 50f, 10f, 50f };
            ObjectiveSharingCalculator.DegradePositions(tiers1, 1, pos1, 3f, 5f, new System.Random(999));

            byte[] tiers2 = { ObjectiveSharingCalculator.TierRelayed };
            float[] pos2 = { 50f, 10f, 50f };
            ObjectiveSharingCalculator.DegradePositions(tiers2, 1, pos2, 3f, 5f, new System.Random(999));

            Assert.That(pos1[0], Is.EqualTo(pos2[0]));
            Assert.That(pos1[1], Is.EqualTo(pos2[1]));
            Assert.That(pos1[2], Is.EqualTo(pos2[2]));
        }
    }
}
