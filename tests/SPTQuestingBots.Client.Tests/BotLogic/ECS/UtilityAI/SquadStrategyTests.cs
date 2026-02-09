using System.Collections.Generic;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.UtilityAI
{
    /// <summary>
    /// Concrete test strategy for controlling scores externally.
    /// </summary>
    internal class TestSquadStrategy : SquadStrategy
    {
        private readonly Dictionary<int, float> _scores = new Dictionary<int, float>();
        public int UpdateScoresCalls;
        public int UpdateCalls;
        public int ActivateCalls;
        public int DeactivateCalls;
        public SquadEntity LastActivated;
        public SquadEntity LastDeactivated;

        public TestSquadStrategy(float hysteresis)
            : base(hysteresis) { }

        public void SetScore(int squadId, float score) => _scores[squadId] = score;

        public override void ScoreSquad(int ordinal, SquadEntity squad)
        {
            if (_scores.TryGetValue(squad.Id, out float score))
                squad.StrategyScores[ordinal] = score;
            else
                squad.StrategyScores[ordinal] = 0f;
        }

        public override void UpdateScores(int ordinal, IReadOnlyList<SquadEntity> squads)
        {
            UpdateScoresCalls++;
            base.UpdateScores(ordinal, squads);
        }

        public override void Update()
        {
            UpdateCalls++;
        }

        public override void Activate(SquadEntity squad)
        {
            base.Activate(squad);
            ActivateCalls++;
            LastActivated = squad;
        }

        public override void Deactivate(SquadEntity squad)
        {
            base.Deactivate(squad);
            DeactivateCalls++;
            LastDeactivated = squad;
        }
    }

    // ── SquadStrategy Base Tests ────────────────────────

    [TestFixture]
    public class SquadStrategyBaseTests
    {
        private SquadEntity CreateSquad(int id, int strategyCount)
        {
            return new SquadEntity(id, strategyCount, 4);
        }

        [Test]
        public void Activate_AddsSquadToActiveList()
        {
            var strategy = new TestSquadStrategy(0f);
            var squad = CreateSquad(0, 1);

            strategy.Activate(squad);

            Assert.AreEqual(1, strategy.ActiveSquadCount);
            Assert.AreSame(squad, strategy.ActiveSquads[0]);
        }

        [Test]
        public void Activate_DuplicateSquad_NoDoubleAdd()
        {
            var strategy = new TestSquadStrategy(0f);
            var squad = CreateSquad(0, 1);

            strategy.Activate(squad);
            strategy.Activate(squad);

            Assert.AreEqual(1, strategy.ActiveSquadCount);
        }

        [Test]
        public void Deactivate_RemovesSquadFromActiveList()
        {
            var strategy = new TestSquadStrategy(0f);
            var squad = CreateSquad(0, 1);

            strategy.Activate(squad);
            strategy.Deactivate(squad);

            Assert.AreEqual(0, strategy.ActiveSquadCount);
        }

        [Test]
        public void Deactivate_NonexistentSquad_NoOp()
        {
            var strategy = new TestSquadStrategy(0f);
            var squad = CreateSquad(0, 1);

            Assert.DoesNotThrow(() => strategy.Deactivate(squad));
            Assert.AreEqual(0, strategy.ActiveSquadCount);
        }

        [Test]
        public void Activate_MultipleSquads_AllTracked()
        {
            var strategy = new TestSquadStrategy(0f);
            var s1 = CreateSquad(0, 1);
            var s2 = CreateSquad(1, 1);
            var s3 = CreateSquad(2, 1);

            strategy.Activate(s1);
            strategy.Activate(s2);
            strategy.Activate(s3);

            Assert.AreEqual(3, strategy.ActiveSquadCount);
        }

        [Test]
        public void Deactivate_MiddleSquad_SwapRemoves()
        {
            var strategy = new TestSquadStrategy(0f);
            var s1 = CreateSquad(0, 1);
            var s2 = CreateSquad(1, 1);
            var s3 = CreateSquad(2, 1);

            strategy.Activate(s1);
            strategy.Activate(s2);
            strategy.Activate(s3);

            strategy.Deactivate(s2);

            Assert.AreEqual(2, strategy.ActiveSquadCount);
            Assert.AreSame(s1, strategy.ActiveSquads[0]);
            Assert.AreSame(s3, strategy.ActiveSquads[1]);
        }

        [Test]
        public void Deactivate_LastSquad_SimplePop()
        {
            var strategy = new TestSquadStrategy(0f);
            var s1 = CreateSquad(0, 1);
            var s2 = CreateSquad(1, 1);

            strategy.Activate(s1);
            strategy.Activate(s2);

            strategy.Deactivate(s2);

            Assert.AreEqual(1, strategy.ActiveSquadCount);
            Assert.AreSame(s1, strategy.ActiveSquads[0]);
        }
    }

    // ── SquadStrategyManager Tests ──────────────────────

    [TestFixture]
    public class SquadStrategyManagerTests
    {
        private SquadEntity CreateSquad(int id, int strategyCount)
        {
            return new SquadEntity(id, strategyCount, 4);
        }

        // ── PickStrategy: Basic Selection ───────────────

        [Test]
        public void PickStrategy_WithNoCurrentStrategy_SelectsHighestScoring()
        {
            var stratA = new TestSquadStrategy(0f);
            var stratB = new TestSquadStrategy(0f);
            var manager = new SquadStrategyManager(new SquadStrategy[] { stratA, stratB });
            var squad = CreateSquad(0, 2);

            stratA.SetScore(0, 0.3f);
            stratB.SetScore(0, 0.7f);
            manager.UpdateScores(new[] { squad });
            manager.PickStrategy(squad);

            Assert.AreSame(stratB, (SquadStrategy)squad.StrategyAssignment.Strategy);
            Assert.AreEqual(1, squad.StrategyAssignment.Ordinal);
        }

        [Test]
        public void PickStrategy_WithAllZeroScores_DoesNotAssign()
        {
            var strat = new TestSquadStrategy(0f);
            var manager = new SquadStrategyManager(new SquadStrategy[] { strat });
            var squad = CreateSquad(0, 1);

            manager.UpdateScores(new[] { squad });
            manager.PickStrategy(squad);

            Assert.IsNull(squad.StrategyAssignment.Strategy);
        }

        [Test]
        public void PickStrategy_SelectsFirstOnTie()
        {
            var stratA = new TestSquadStrategy(0f);
            var stratB = new TestSquadStrategy(0f);
            var manager = new SquadStrategyManager(new SquadStrategy[] { stratA, stratB });
            var squad = CreateSquad(0, 2);

            stratA.SetScore(0, 0.5f);
            stratB.SetScore(0, 0.5f);
            manager.UpdateScores(new[] { squad });
            manager.PickStrategy(squad);

            Assert.AreSame(stratA, (SquadStrategy)squad.StrategyAssignment.Strategy);
        }

        // ── PickStrategy: Hysteresis ────────────────────

        [Test]
        public void PickStrategy_WithHysteresis_CurrentGetsBonus()
        {
            var stratA = new TestSquadStrategy(0.2f);
            var stratB = new TestSquadStrategy(0f);
            var manager = new SquadStrategyManager(new SquadStrategy[] { stratA, stratB });
            var squad = CreateSquad(0, 2);

            // A wins initially
            stratA.SetScore(0, 0.5f);
            stratB.SetScore(0, 0.3f);
            manager.UpdateScores(new[] { squad });
            manager.PickStrategy(squad);
            Assert.AreSame(stratA, (SquadStrategy)squad.StrategyAssignment.Strategy);

            // B scores higher than A raw, but NOT A + hysteresis
            stratA.SetScore(0, 0.4f);
            stratB.SetScore(0, 0.55f); // 0.55 < 0.4 + 0.2 = 0.6
            manager.UpdateScores(new[] { squad });
            manager.PickStrategy(squad);

            Assert.AreSame(stratA, (SquadStrategy)squad.StrategyAssignment.Strategy);
        }

        [Test]
        public void PickStrategy_HysteresisOvercome_SwitchesToNew()
        {
            var stratA = new TestSquadStrategy(0.2f);
            var stratB = new TestSquadStrategy(0f);
            var manager = new SquadStrategyManager(new SquadStrategy[] { stratA, stratB });
            var squad = CreateSquad(0, 2);

            stratA.SetScore(0, 0.5f);
            stratB.SetScore(0, 0.3f);
            manager.UpdateScores(new[] { squad });
            manager.PickStrategy(squad);
            Assert.AreSame(stratA, (SquadStrategy)squad.StrategyAssignment.Strategy);

            // B exceeds A + hysteresis
            stratA.SetScore(0, 0.4f);
            stratB.SetScore(0, 0.65f); // > 0.4 + 0.2 = 0.6
            manager.UpdateScores(new[] { squad });
            manager.PickStrategy(squad);

            Assert.AreSame(stratB, (SquadStrategy)squad.StrategyAssignment.Strategy);
            Assert.AreEqual(1, squad.StrategyAssignment.Ordinal);
        }

        // ── Lifecycle ───────────────────────────────────

        [Test]
        public void PickStrategy_CallsActivateOnNewStrategy()
        {
            var strat = new TestSquadStrategy(0f);
            var manager = new SquadStrategyManager(new SquadStrategy[] { strat });
            var squad = CreateSquad(0, 1);

            strat.SetScore(0, 0.5f);
            manager.UpdateScores(new[] { squad });
            manager.PickStrategy(squad);

            Assert.AreEqual(1, strat.ActivateCalls);
            Assert.AreSame(squad, strat.LastActivated);
        }

        [Test]
        public void PickStrategy_CallsDeactivateOnOldStrategy()
        {
            var stratA = new TestSquadStrategy(0f);
            var stratB = new TestSquadStrategy(0f);
            var manager = new SquadStrategyManager(new SquadStrategy[] { stratA, stratB });
            var squad = CreateSquad(0, 2);

            stratA.SetScore(0, 0.5f);
            stratB.SetScore(0, 0.3f);
            manager.UpdateScores(new[] { squad });
            manager.PickStrategy(squad);

            stratA.SetScore(0, 0.3f);
            stratB.SetScore(0, 0.7f);
            manager.UpdateScores(new[] { squad });
            manager.PickStrategy(squad);

            Assert.AreEqual(1, stratA.DeactivateCalls);
            Assert.AreSame(squad, stratA.LastDeactivated);
            Assert.AreEqual(1, stratB.ActivateCalls);
        }

        [Test]
        public void PickStrategy_NoSwitch_DoesNotCallLifecycle()
        {
            var strat = new TestSquadStrategy(0.2f);
            var manager = new SquadStrategyManager(new SquadStrategy[] { strat });
            var squad = CreateSquad(0, 1);

            strat.SetScore(0, 0.5f);
            manager.UpdateScores(new[] { squad });
            manager.PickStrategy(squad);
            Assert.AreEqual(1, strat.ActivateCalls);

            manager.PickStrategy(squad);
            Assert.AreEqual(1, strat.ActivateCalls);
            Assert.AreEqual(0, strat.DeactivateCalls);
        }

        // ── Inactive Squads ─────────────────────────────

        [Test]
        public void PickStrategies_InactiveSquad_DeactivatesCurrentStrategy()
        {
            var strat = new TestSquadStrategy(0f);
            var manager = new SquadStrategyManager(new SquadStrategy[] { strat });
            var squad = CreateSquad(0, 1);

            strat.SetScore(0, 0.5f);
            manager.UpdateScores(new[] { squad });
            manager.PickStrategy(squad);
            Assert.IsNotNull(squad.StrategyAssignment.Strategy);

            squad.IsActive = false;
            manager.PickStrategies(new[] { squad });

            Assert.IsNull(squad.StrategyAssignment.Strategy);
            Assert.AreEqual(1, strat.DeactivateCalls);
        }

        [Test]
        public void PickStrategies_InactiveSquadWithNoStrategy_DoesNothing()
        {
            var strat = new TestSquadStrategy(0f);
            var manager = new SquadStrategyManager(new SquadStrategy[] { strat });
            var squad = CreateSquad(0, 1);
            squad.IsActive = false;

            manager.PickStrategies(new[] { squad });
            Assert.IsNull(squad.StrategyAssignment.Strategy);
        }

        // ── Full Cycle ──────────────────────────────────

        [Test]
        public void Update_CallsAllThreePhases()
        {
            var strat = new TestSquadStrategy(0f);
            var manager = new SquadStrategyManager(new SquadStrategy[] { strat });
            var squad = CreateSquad(0, 1);

            strat.SetScore(0, 0.5f);
            manager.Update(new[] { squad });

            Assert.AreEqual(1, strat.UpdateScoresCalls);
            Assert.AreEqual(1, strat.UpdateCalls);
            Assert.IsNotNull(squad.StrategyAssignment.Strategy);
        }

        [Test]
        public void UpdateScores_CallsAllStrategies()
        {
            var stratA = new TestSquadStrategy(0f);
            var stratB = new TestSquadStrategy(0f);
            var manager = new SquadStrategyManager(new SquadStrategy[] { stratA, stratB });
            var squad = CreateSquad(0, 2);

            manager.UpdateScores(new[] { squad });

            Assert.AreEqual(1, stratA.UpdateScoresCalls);
            Assert.AreEqual(1, stratB.UpdateScoresCalls);
        }

        [Test]
        public void UpdateStrategies_CallsAllStrategies()
        {
            var stratA = new TestSquadStrategy(0f);
            var stratB = new TestSquadStrategy(0f);
            var manager = new SquadStrategyManager(new SquadStrategy[] { stratA, stratB });

            manager.UpdateStrategies();

            Assert.AreEqual(1, stratA.UpdateCalls);
            Assert.AreEqual(1, stratB.UpdateCalls);
        }

        // ── ScoreAndPick ────────────────────────────────

        [Test]
        public void ScoreAndPick_ScoresAndSelectsBestStrategy()
        {
            var stratA = new TestSquadStrategy(0f);
            var stratB = new TestSquadStrategy(0f);
            var manager = new SquadStrategyManager(new SquadStrategy[] { stratA, stratB });
            var squad = CreateSquad(0, 2);

            stratA.SetScore(0, 0.3f);
            stratB.SetScore(0, 0.7f);

            manager.ScoreAndPick(squad);

            Assert.AreSame(stratB, (SquadStrategy)squad.StrategyAssignment.Strategy);
        }

        [Test]
        public void ScoreAndPick_InactiveSquad_DeactivatesStrategy()
        {
            var strat = new TestSquadStrategy(0f);
            var manager = new SquadStrategyManager(new SquadStrategy[] { strat });
            var squad = CreateSquad(0, 1);

            strat.SetScore(0, 0.5f);
            manager.ScoreAndPick(squad);
            Assert.IsNotNull(squad.StrategyAssignment.Strategy);

            squad.IsActive = false;
            manager.ScoreAndPick(squad);

            Assert.IsNull(squad.StrategyAssignment.Strategy);
        }

        // ── RemoveSquad ─────────────────────────────────

        [Test]
        public void RemoveSquad_DeactivatesCurrentStrategy()
        {
            var strat = new TestSquadStrategy(0f);
            var manager = new SquadStrategyManager(new SquadStrategy[] { strat });
            var squad = CreateSquad(0, 1);

            strat.SetScore(0, 0.5f);
            manager.Update(new[] { squad });
            Assert.IsNotNull(squad.StrategyAssignment.Strategy);

            manager.RemoveSquad(squad);

            Assert.IsNull(squad.StrategyAssignment.Strategy);
            Assert.AreEqual(1, strat.DeactivateCalls);
        }

        [Test]
        public void RemoveSquad_WithNoStrategy_DoesNotThrow()
        {
            var strat = new TestSquadStrategy(0f);
            var manager = new SquadStrategyManager(new SquadStrategy[] { strat });
            var squad = CreateSquad(0, 1);

            Assert.DoesNotThrow(() => manager.RemoveSquad(squad));
        }

        // ── Multi-Squad ─────────────────────────────────

        [Test]
        public void PickStrategies_MultipleSquads_IndependentScoring()
        {
            var stratA = new TestSquadStrategy(0f);
            var stratB = new TestSquadStrategy(0f);
            var manager = new SquadStrategyManager(new SquadStrategy[] { stratA, stratB });

            var squad1 = CreateSquad(0, 2);
            var squad2 = CreateSquad(1, 2);

            stratA.SetScore(0, 0.7f);
            stratA.SetScore(1, 0.3f);
            stratB.SetScore(0, 0.3f);
            stratB.SetScore(1, 0.7f);

            manager.Update(new[] { squad1, squad2 });

            Assert.AreSame(stratA, (SquadStrategy)squad1.StrategyAssignment.Strategy);
            Assert.AreSame(stratB, (SquadStrategy)squad2.StrategyAssignment.Strategy);
        }
    }

    // ── StrategyAssignment Tests ────────────────────────

    [TestFixture]
    public class StrategyAssignmentTests
    {
        [Test]
        public void Default_HasNullStrategy()
        {
            var assignment = default(StrategyAssignment);
            Assert.IsNull(assignment.Strategy);
            Assert.AreEqual(0, assignment.Ordinal);
        }

        [Test]
        public void Constructor_StoresStrategyAndOrdinal()
        {
            var strategy = new TestSquadStrategy(0.1f);
            var assignment = new StrategyAssignment(strategy, 3);

            Assert.AreSame(strategy, assignment.Strategy);
            Assert.AreEqual(3, assignment.Ordinal);
        }
    }
}
