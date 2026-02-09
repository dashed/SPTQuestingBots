using System;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS.Systems;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.Systems
{
    [TestFixture]
    public class QuestScorerTests
    {
        private static readonly QuestScoringConfig DefaultConfig = new QuestScoringConfig(
            distanceWeighting: 1.0f,
            desirabilityWeighting: 1.0f,
            exfilDirectionWeighting: 0f,
            distanceRandomness: 0,
            desirabilityRandomness: 0,
            maxExfilAngle: 45f,
            desirabilityActiveQuestMultiplier: 2.0f
        );

        private System.Random FixedRng => new System.Random(42);

        // ── QuestScoringConfig ─────────────────────────────────────

        [Test]
        public void QuestScoringConfig_StoresAllFields()
        {
            var config = new QuestScoringConfig(1.5f, 2.5f, 0.5f, 10, 20, 60f, 3.0f);

            Assert.AreEqual(1.5f, config.DistanceWeighting);
            Assert.AreEqual(2.5f, config.DesirabilityWeighting);
            Assert.AreEqual(0.5f, config.ExfilDirectionWeighting);
            Assert.AreEqual(10, config.DistanceRandomness);
            Assert.AreEqual(20, config.DesirabilityRandomness);
            Assert.AreEqual(60f, config.MaxExfilAngle);
            Assert.AreEqual(3.0f, config.DesirabilityActiveQuestMultiplier);
        }

        // ── ScoreQuest: Distance ───────────────────────────────────

        [Test]
        public void ScoreQuest_CloserQuest_ScoresHigher()
        {
            var config = new QuestScoringConfig(1f, 0f, 0f, 0, 0, 45f, 1f);

            double closeScore = QuestScorer.ScoreQuest(10f, 100f, 0, 50f, false, 0f, config, FixedRng);
            double farScore = QuestScorer.ScoreQuest(90f, 100f, 0, 50f, false, 0f, config, FixedRng);

            Assert.Greater(closeScore, farScore);
        }

        [Test]
        public void ScoreQuest_ZeroDistance_ScoresOne()
        {
            var config = new QuestScoringConfig(1f, 0f, 0f, 0, 0, 45f, 1f);

            double score = QuestScorer.ScoreQuest(0f, 100f, 0, 0f, false, 0f, config, FixedRng);

            Assert.AreEqual(1.0, score, 0.001);
        }

        [Test]
        public void ScoreQuest_MaxDistance_ScoresZero()
        {
            var config = new QuestScoringConfig(1f, 0f, 0f, 0, 0, 45f, 1f);

            double score = QuestScorer.ScoreQuest(100f, 100f, 0, 0f, false, 0f, config, FixedRng);

            Assert.AreEqual(0.0, score, 0.001);
        }

        [Test]
        public void ScoreQuest_ZeroMaxOverallDistance_DistanceFractionIsOne()
        {
            var config = new QuestScoringConfig(1f, 0f, 0f, 0, 0, 45f, 1f);

            // When maxOverallDistance is 0, distance fraction defaults to 1.0
            double score = QuestScorer.ScoreQuest(0f, 0f, 0, 0f, false, 0f, config, FixedRng);

            Assert.AreEqual(1.0, score, 0.001);
        }

        // ── ScoreQuest: Desirability ───────────────────────────────

        [Test]
        public void ScoreQuest_HigherDesirability_ScoresHigher()
        {
            var config = new QuestScoringConfig(0f, 1f, 0f, 0, 0, 45f, 1f);

            double highScore = QuestScorer.ScoreQuest(50f, 100f, 0, 80f, false, 0f, config, FixedRng);
            double lowScore = QuestScorer.ScoreQuest(50f, 100f, 0, 20f, false, 0f, config, FixedRng);

            Assert.Greater(highScore, lowScore);
        }

        [Test]
        public void ScoreQuest_DesirabilityNormalized_FiftyGivesPointFive()
        {
            var config = new QuestScoringConfig(0f, 1f, 0f, 0, 0, 45f, 1f);

            double score = QuestScorer.ScoreQuest(50f, 100f, 0, 50f, false, 0f, config, FixedRng);

            Assert.AreEqual(0.5, score, 0.001);
        }

        [Test]
        public void ScoreQuest_ActiveForPlayer_MultiplierApplied()
        {
            var config = new QuestScoringConfig(0f, 1f, 0f, 0, 0, 45f, 2.0f);

            double activeScore = QuestScorer.ScoreQuest(50f, 100f, 0, 50f, true, 0f, config, FixedRng);
            double inactiveScore = QuestScorer.ScoreQuest(50f, 100f, 0, 50f, false, 0f, config, FixedRng);

            // Active: (50 * 2) / 100 = 1.0; Inactive: 50 / 100 = 0.5
            Assert.AreEqual(1.0, activeScore, 0.001);
            Assert.AreEqual(0.5, inactiveScore, 0.001);
        }

        // ── ScoreQuest: Exfil Angle ────────────────────────────────

        [Test]
        public void ScoreQuest_AngleBelowMax_NoPenalty()
        {
            var config = new QuestScoringConfig(0f, 0f, 1f, 0, 0, 45f, 1f);

            // Angle 30 < MaxExfilAngle 45 → no penalty → score = 0 (no distance/desirability)
            double score = QuestScorer.ScoreQuest(50f, 100f, 0, 0f, false, 30f, config, FixedRng);

            Assert.AreEqual(0.0, score, 0.001);
        }

        [Test]
        public void ScoreQuest_AngleAboveMax_PenaltyApplied()
        {
            var config = new QuestScoringConfig(0f, 0f, 1f, 0, 0, 45f, 1f);

            // Angle 90 > MaxExfilAngle 45 → penalty = (90 - 45) / (180 - 45) = 45/135 = 0.333
            // Score = -0.333 * 1.0 = -0.333
            double score = QuestScorer.ScoreQuest(50f, 100f, 0, 0f, false, 90f, config, FixedRng);

            Assert.AreEqual(-1.0 / 3.0, score, 0.001);
        }

        [Test]
        public void ScoreQuest_Angle180_MaxPenalty()
        {
            var config = new QuestScoringConfig(0f, 0f, 1f, 0, 0, 45f, 1f);

            // Angle 180 → penalty = (180 - 45) / (180 - 45) = 1.0
            // Score = -1.0 * 1.0 = -1.0
            double score = QuestScorer.ScoreQuest(50f, 100f, 0, 0f, false, 180f, config, FixedRng);

            Assert.AreEqual(-1.0, score, 0.001);
        }

        [Test]
        public void ScoreQuest_MaxExfilAngle180_NoPenaltyEver()
        {
            // When maxExfilAngle = 180, denominator is 0, so factor is 0
            var config = new QuestScoringConfig(0f, 0f, 1f, 0, 0, 180f, 1f);

            double score = QuestScorer.ScoreQuest(50f, 100f, 0, 0f, false, 180f, config, FixedRng);

            Assert.AreEqual(0.0, score, 0.001);
        }

        // ── ScoreQuest: Composite ──────────────────────────────────

        [Test]
        public void ScoreQuest_AllFactorsCombine()
        {
            var config = new QuestScoringConfig(
                distanceWeighting: 2f,
                desirabilityWeighting: 3f,
                exfilDirectionWeighting: 1f,
                distanceRandomness: 0,
                desirabilityRandomness: 0,
                maxExfilAngle: 0f,
                desirabilityActiveQuestMultiplier: 1f
            );

            // dist = 1 - 50/100 = 0.5 → weighted = 0.5 * 2 = 1.0
            // desir = 60/100 = 0.6 → weighted = 0.6 * 3 = 1.8
            // angle = max(0, 90-0)/(180-0) = 0.5 → weighted = 0.5 * 1 = 0.5
            // total = 1.0 + 1.8 - 0.5 = 2.3
            double score = QuestScorer.ScoreQuest(50f, 100f, 0, 60f, false, 90f, config, FixedRng);

            Assert.AreEqual(2.3, score, 0.001);
        }

        // ── ScoreQuest: Randomness ─────────────────────────────────

        [Test]
        public void ScoreQuest_WithRandomness_VariesBetweenCalls()
        {
            var config = new QuestScoringConfig(1f, 1f, 0f, 20, 20, 45f, 1f);

            var rng = new System.Random(1);
            double score1 = QuestScorer.ScoreQuest(50f, 100f, 10, 50f, false, 0f, config, rng);
            double score2 = QuestScorer.ScoreQuest(50f, 100f, 10, 50f, false, 0f, config, rng);

            // With randomness, two calls with the same rng should produce different scores
            Assert.AreNotEqual(score1, score2);
        }

        [Test]
        public void ScoreQuest_ZeroRandomness_Deterministic()
        {
            var config = new QuestScoringConfig(1f, 1f, 0f, 0, 0, 45f, 1f);

            double score1 = QuestScorer.ScoreQuest(50f, 100f, 0, 50f, false, 0f, config, new System.Random(1));
            double score2 = QuestScorer.ScoreQuest(50f, 100f, 0, 50f, false, 0f, config, new System.Random(99));

            Assert.AreEqual(score1, score2, 0.001);
        }

        // ── SelectHighestIndex ─────────────────────────────────────

        [Test]
        public void SelectHighestIndex_EmptyArray_ReturnsNegativeOne()
        {
            Assert.AreEqual(-1, QuestScorer.SelectHighestIndex(new double[0], 0));
        }

        [Test]
        public void SelectHighestIndex_SingleElement_ReturnsZero()
        {
            Assert.AreEqual(0, QuestScorer.SelectHighestIndex(new[] { 5.0 }, 1));
        }

        [Test]
        public void SelectHighestIndex_HighestAtEnd()
        {
            var scores = new[] { 1.0, 2.0, 3.0, 4.0, 5.0 };
            Assert.AreEqual(4, QuestScorer.SelectHighestIndex(scores, 5));
        }

        [Test]
        public void SelectHighestIndex_HighestAtStart()
        {
            var scores = new[] { 10.0, 2.0, 3.0, 4.0, 5.0 };
            Assert.AreEqual(0, QuestScorer.SelectHighestIndex(scores, 5));
        }

        [Test]
        public void SelectHighestIndex_HighestInMiddle()
        {
            var scores = new[] { 1.0, 2.0, 99.0, 4.0, 5.0 };
            Assert.AreEqual(2, QuestScorer.SelectHighestIndex(scores, 5));
        }

        [Test]
        public void SelectHighestIndex_RespectsCount_IgnoresTail()
        {
            // Array has 5 elements but count=3, so only first 3 are considered
            var scores = new[] { 1.0, 2.0, 3.0, 100.0, 200.0 };
            Assert.AreEqual(2, QuestScorer.SelectHighestIndex(scores, 3));
        }

        [Test]
        public void SelectHighestIndex_Tie_ReturnsFirstOccurrence()
        {
            var scores = new[] { 5.0, 5.0, 5.0 };
            Assert.AreEqual(0, QuestScorer.SelectHighestIndex(scores, 3));
        }

        [Test]
        public void SelectHighestIndex_NegativeScores()
        {
            var scores = new[] { -10.0, -5.0, -20.0 };
            Assert.AreEqual(1, QuestScorer.SelectHighestIndex(scores, 3));
        }

        [Test]
        public void SelectHighestIndex_ZeroCount_ReturnsNegativeOne()
        {
            var scores = new[] { 1.0, 2.0, 3.0 };
            Assert.AreEqual(-1, QuestScorer.SelectHighestIndex(scores, 0));
        }
    }
}
