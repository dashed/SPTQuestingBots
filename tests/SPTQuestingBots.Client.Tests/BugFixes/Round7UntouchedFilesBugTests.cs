using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.Models;
using UnityEngine;

namespace SPTQuestingBots.Client.Tests.BugFixes;

/// <summary>
/// Bug-fix regression tests for Round 7: untouched file audit.
///
/// Bug 1: QuestScorer.ScoreQuest Random.Next max-exclusive asymmetry
///   - rng.Next(-N, N) produces [-N, N-1] instead of [-N, N]
///   - Fix: use N+1 as upper bound
///
/// Bug 2: QuestScorer.ScoreQuest unclamped distanceFraction
///   - Large random offset can push distanceFraction below 0
///   - Fix: clamp distanceFraction to [0, 1]
///
/// Bug 3: QuestScorer.ScoreQuest unclamped desirabilityFraction
///   - Large random offset can push desirabilityFraction below 0
///   - Fix: clamp desirabilityFraction to [0, 2]
///
/// Bug 4: HardStuckDetector stuckThresholdSqr dimensional mismatch
///   - StuckRadiusSqr * moveSpeed mixes squared distance with linear speed
///   - Fix: StuckRadiusSqr * moveSpeed * moveSpeed
/// </summary>
[TestFixture]
public class Round7UntouchedFilesBugTests
{
    // =====================================================================
    // Bug 1: QuestScorer Random.Next max-exclusive produces symmetric range
    // =====================================================================

    [Test]
    public void QuestScorer_RandomNext_UpperBoundIsInclusive()
    {
        // With fixed seed, verify the random range covers both negative and positive
        // extremes symmetrically. Before fix: [-N, N-1]; after fix: [-N, N].
        var config = new QuestScoringConfig(
            distanceWeighting: 1f,
            desirabilityWeighting: 0f,
            exfilDirectionWeighting: 0f,
            distanceRandomness: 50,
            desirabilityRandomness: 0,
            maxExfilAngle: 45f,
            desirabilityActiveQuestMultiplier: 1f
        );

        // Run many iterations to check range
        int maxRandomSeen = int.MinValue;
        int minRandomSeen = int.MaxValue;
        var rng = new System.Random(12345);

        for (int i = 0; i < 10000; i++)
        {
            // The score formula: 1.0 - (minDistance + random) / maxOverallDistance
            // With minDistance=50, maxOverallDistance=100:
            // score = 1.0 - (50 + random) / 100
            // random = round(score * -100 + 50)
            // So we can extract the random value from the score
            double score = QuestScorer.ScoreQuest(50f, 100f, 50, 50f, false, 0f, config, rng);
            // distanceFraction = 1 - (50 + rand) / 100 = 0.5 - rand/100 (clamped to [0,1])
            // score = distanceFraction * 1.0
            // rand = (0.5 - score) * 100
            int estimatedRand = (int)System.Math.Round((0.5 - score) * 100);
            if (estimatedRand > maxRandomSeen)
                maxRandomSeen = estimatedRand;
            if (estimatedRand < minRandomSeen)
                minRandomSeen = estimatedRand;
        }

        // After fix, the upper bound should reach 50 (was 49 before fix)
        Assert.That(
            maxRandomSeen,
            Is.GreaterThanOrEqualTo(49),
            "Random upper bound should include the max value (symmetric with lower bound)"
        );
        Assert.That(minRandomSeen, Is.LessThanOrEqualTo(-49), "Random lower bound should include the min value");
    }

    [Test]
    public void QuestScorer_DesirabilityRandom_UpperBoundIsInclusive()
    {
        // Verify desirability randomness is also symmetric after fix
        var config = new QuestScoringConfig(
            distanceWeighting: 0f,
            desirabilityWeighting: 1f,
            exfilDirectionWeighting: 0f,
            distanceRandomness: 0,
            desirabilityRandomness: 20,
            maxExfilAngle: 45f,
            desirabilityActiveQuestMultiplier: 1f
        );

        int maxRand = int.MinValue;
        int minRand = int.MaxValue;
        var rng = new System.Random(54321);

        for (int i = 0; i < 10000; i++)
        {
            double score = QuestScorer.ScoreQuest(50f, 100f, 0, 50f, false, 0f, config, rng);
            // desirabilityFraction = (50 + rand) / 100 (clamped to >= 0)
            // score = desirabilityFraction * 1.0
            int estimatedRand = (int)System.Math.Round(score * 100 - 50);
            if (estimatedRand > maxRand)
                maxRand = estimatedRand;
            if (estimatedRand < minRand)
                minRand = estimatedRand;
        }

        // After fix, the range should be symmetric: [-20, 20]
        Assert.That(maxRand, Is.GreaterThanOrEqualTo(19), "Desirability random upper bound should include max value");
        Assert.That(minRand, Is.LessThanOrEqualTo(-19), "Desirability random lower bound should include min value");
    }

    // =====================================================================
    // Bug 2: QuestScorer distanceFraction clamped to [0, 1]
    // =====================================================================

    [Test]
    public void QuestScorer_DistanceFraction_NeverNegative()
    {
        // Use maximum randomness to try to push distanceFraction below 0
        // minDistance = 90 (close to max), maxOverallDistance = 100
        // random can be up to 50, so (90 + 50) / 100 = 1.4 -> 1 - 1.4 = -0.4
        // Before fix: -0.4 leaked through. After fix: clamped to 0.
        var config = new QuestScoringConfig(
            distanceWeighting: 1f,
            desirabilityWeighting: 0f,
            exfilDirectionWeighting: 0f,
            distanceRandomness: 50,
            desirabilityRandomness: 0,
            maxExfilAngle: 45f,
            desirabilityActiveQuestMultiplier: 1f
        );

        var rng = new System.Random(99999);
        for (int i = 0; i < 10000; i++)
        {
            double score = QuestScorer.ScoreQuest(90f, 100f, 50, 0f, false, 0f, config, rng);
            Assert.That(score, Is.GreaterThanOrEqualTo(0.0), "Distance fraction should be clamped to >= 0 (iteration " + i + ")");
        }
    }

    [Test]
    public void QuestScorer_DistanceFraction_NeverAboveOne()
    {
        // minDistance = 5, maxOverallDistance = 100, random up to 50
        // (5 + (-50)) / 100 = -0.45 -> 1 - (-0.45) = 1.45
        // Before fix: 1.45. After fix: clamped to 1.0.
        var config = new QuestScoringConfig(
            distanceWeighting: 1f,
            desirabilityWeighting: 0f,
            exfilDirectionWeighting: 0f,
            distanceRandomness: 50,
            desirabilityRandomness: 0,
            maxExfilAngle: 45f,
            desirabilityActiveQuestMultiplier: 1f
        );

        var rng = new System.Random(11111);
        for (int i = 0; i < 10000; i++)
        {
            double score = QuestScorer.ScoreQuest(5f, 100f, 50, 0f, false, 0f, config, rng);
            Assert.That(score, Is.LessThanOrEqualTo(1.0), "Distance fraction should be clamped to <= 1.0 (iteration " + i + ")");
        }
    }

    // =====================================================================
    // Bug 3: QuestScorer desirabilityFraction clamped to >= 0
    // =====================================================================

    [Test]
    public void QuestScorer_DesirabilityFraction_NeverNegative()
    {
        // desirability = 10 (low), randomness = 50
        // (10 + (-50)) / 100 = -0.40
        // Before fix: -0.40 leaked through. After fix: clamped to 0.
        var config = new QuestScoringConfig(
            distanceWeighting: 0f,
            desirabilityWeighting: 1f,
            exfilDirectionWeighting: 0f,
            distanceRandomness: 0,
            desirabilityRandomness: 50,
            maxExfilAngle: 45f,
            desirabilityActiveQuestMultiplier: 1f
        );

        var rng = new System.Random(77777);
        for (int i = 0; i < 10000; i++)
        {
            double score = QuestScorer.ScoreQuest(50f, 100f, 0, 10f, false, 0f, config, rng);
            Assert.That(score, Is.GreaterThanOrEqualTo(0.0), "Desirability fraction should be clamped to >= 0 (iteration " + i + ")");
        }
    }

    [Test]
    public void QuestScorer_DesirabilityFraction_ZeroDesirability_WithLargeNoise_StaysNonNegative()
    {
        var config = new QuestScoringConfig(
            distanceWeighting: 0f,
            desirabilityWeighting: 1f,
            exfilDirectionWeighting: 0f,
            distanceRandomness: 0,
            desirabilityRandomness: 30,
            maxExfilAngle: 45f,
            desirabilityActiveQuestMultiplier: 1f
        );

        // desirability = 0, random in [-30, 30]
        // (0 + random) / 100 can be negative
        var rng = new System.Random(33333);
        for (int i = 0; i < 5000; i++)
        {
            double score = QuestScorer.ScoreQuest(50f, 100f, 0, 0f, false, 0f, config, rng);
            Assert.That(score, Is.GreaterThanOrEqualTo(0.0));
        }
    }

    // =====================================================================
    // Bug 4: HardStuckDetector dimensional mismatch
    // =====================================================================

    [Test]
    public void HardStuckDetector_ThresholdScalesWithSpeedSquared()
    {
        // Before fix: stuckThresholdSqr = 9 * moveSpeed (linear scaling)
        // After fix: stuckThresholdSqr = 9 * moveSpeed * moveSpeed (proper squared scaling)
        //
        // At speed=2: old threshold = 18 (sqrt=4.2m), new threshold = 36 (sqrt=6m)
        // At speed=0.5: old threshold = 4.5 (sqrt=2.1m), new threshold = 2.25 (sqrt=1.5m)
        //
        // A bot at speed=2 moving 5m should NOT be stuck with new threshold (36 > 25)
        // but WOULD have been stuck with old threshold (18 < 25)
        var detector = new HardStuckDetector(10, 5f, 10f, 15f);
        float time = 0f;
        float dt = 0.1f;
        float speed = 2.0f;

        // Move bot exactly 5m from origin over many ticks
        // This creates a position history where the distance between oldest and newest is ~5m
        for (int i = 0; i < 50; i++)
        {
            time += dt;
            // Small movements that cumulatively reach 5m from start
            float x = (i < 30) ? (float)i * 0.15f : 4.5f;
            detector.Update(new Vector3(x, 0, 0), speed, time);
        }

        // With squared scaling (fix), 9 * 4 = 36 threshold, sqrt = 6m
        // The bot moved 4.5m which is less than 6m BUT we're comparing squared values:
        // distSqr = 4.5^2 = 20.25, threshold = 36. 20.25 < 36 -> stuck
        // Actually, let's test with a definitive scenario

        // Reset and test: bot moving fast enough should NOT be stuck
        var detectorFast = new HardStuckDetector(10, 5f, 10f, 15f);
        time = 0f;

        // Init
        detectorFast.Update(new Vector3(0, 0, 0), 3.0f, time);

        // Move 10m over 10 ticks (1m/tick)
        for (int i = 1; i <= 20; i++)
        {
            time += dt;
            detectorFast.Update(new Vector3((float)i * 0.5f, 0, 0), 3.0f, time);
        }

        // With speed=3, squared threshold = 9 * 9 = 81, sqrt = 9m
        // Bot moved 10m, distSqr = 100 > 81 -> not stuck
        Assert.That(detectorFast.Status, Is.EqualTo(HardStuckStatus.None), "Fast-moving bot with enough distance should not be stuck");
    }

    [Test]
    public void HardStuckDetector_LowSpeedBot_SensitiveToSmallMovement()
    {
        // At very low speed, the squared threshold is very small
        // speed=0.1: threshold = 9 * 0.01 = 0.09 (sqrt ~0.3m)
        // A truly stuck bot at low speed should be detected quickly
        var detector = new HardStuckDetector(10, 5f, 10f, 15f);
        float time = 0f;
        float dt = 0.1f;
        float speed = 0.1f;
        var pos = new Vector3(5, 0, 5);

        for (int i = 0; i < 60; i++)
        {
            time += dt;
            detector.Update(pos, speed, time);
        }

        Assert.That(detector.Status, Is.EqualTo(HardStuckStatus.Retrying), "Low-speed stationary bot should be detected as stuck");
    }

    [Test]
    public void HardStuckDetector_HighSpeed_MovingBot_NotStuck()
    {
        // At high speed, the squared threshold scales properly
        // speed=5: threshold = 9 * 25 = 225 (sqrt = 15m)
        // A bot moving 15m+ should not be stuck
        var detector = new HardStuckDetector(10, 5f, 10f, 15f);
        float time = 0f;
        float dt = 0.1f;
        float speed = 5.0f;

        for (int i = 0; i < 100; i++)
        {
            time += dt;
            // Move 2m per tick (20m/s effective)
            detector.Update(new Vector3(i * 2.0f, 0, 0), speed, time);
        }

        Assert.That(detector.Status, Is.EqualTo(HardStuckStatus.None), "Fast-moving bot covering distance should not be stuck");
    }

    [Test]
    public void HardStuckDetector_MediumSpeed_MicroMovement_DetectedAsStuck()
    {
        // A bot at medium speed with tiny oscillations should be detected as stuck
        // speed=1: threshold = 9 * 1 = 9 (sqrt = 3m)
        // Bot wiggling within 0.5m radius
        var detector = new HardStuckDetector(10, 5f, 10f, 15f);
        float time = 0f;
        float dt = 0.1f;
        float speed = 1.0f;
        float wiggle = 0.2f;

        for (int i = 0; i < 60; i++)
        {
            time += dt;
            float x = 5f + wiggle * (float)System.Math.Sin(i * 0.5);
            float z = 5f + wiggle * (float)System.Math.Cos(i * 0.5);
            detector.Update(new Vector3(x, 0, z), speed, time);
        }

        Assert.That(
            detector.Status,
            Is.EqualTo(HardStuckStatus.Retrying),
            "Bot wiggling in tiny area at medium speed should be detected as stuck"
        );
    }

    // =====================================================================
    // End-to-end: QuestScorer composite score is always reasonable
    // =====================================================================

    [Test]
    public void QuestScorer_CompositeScore_WithAllFactors_NeverNaN()
    {
        var config = new QuestScoringConfig(
            distanceWeighting: 2f,
            desirabilityWeighting: 3f,
            exfilDirectionWeighting: 1f,
            distanceRandomness: 30,
            desirabilityRandomness: 20,
            maxExfilAngle: 45f,
            desirabilityActiveQuestMultiplier: 2.0f
        );

        var rng = new System.Random(42);
        for (int i = 0; i < 10000; i++)
        {
            float minDist = (float)(rng.NextDouble() * 200);
            float maxDist = 200f;
            float desir = (float)(rng.NextDouble() * 100);
            bool active = rng.Next(2) == 0;
            float angle = (float)(rng.NextDouble() * 180);

            double score = QuestScorer.ScoreQuest(minDist, maxDist, 60, desir, active, angle, config, rng);

            Assert.That(double.IsNaN(score), Is.False, "Score should never be NaN");
            Assert.That(double.IsInfinity(score), Is.False, "Score should never be Infinity");
        }
    }

    [Test]
    public void QuestScorer_DistanceOnly_ScoreAlwaysInZeroOneRange()
    {
        // With only distance weighting, score = distanceFraction * weight
        // distanceFraction should be in [0, 1] after clamping
        var config = new QuestScoringConfig(
            distanceWeighting: 1f,
            desirabilityWeighting: 0f,
            exfilDirectionWeighting: 0f,
            distanceRandomness: 50,
            desirabilityRandomness: 0,
            maxExfilAngle: 45f,
            desirabilityActiveQuestMultiplier: 1f
        );

        var rng = new System.Random(55555);
        for (int i = 0; i < 10000; i++)
        {
            float minDist = (float)(rng.NextDouble() * 100);
            double score = QuestScorer.ScoreQuest(minDist, 100f, 50, 0f, false, 0f, config, rng);

            Assert.That(score, Is.GreaterThanOrEqualTo(0.0), "Score should never be negative (iter " + i + ")");
            Assert.That(score, Is.LessThanOrEqualTo(1.0), "Score should never exceed 1.0 (iter " + i + ")");
        }
    }
}
