using System;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS
{
    [TestFixture]
    public class SquadEntityTests
    {
        [Test]
        public void Constructor_SetsIdAndDefaults()
        {
            var squad = new SquadEntity(5, 3, 4);

            Assert.AreEqual(5, squad.Id);
            Assert.IsTrue(squad.IsActive);
            Assert.IsNull(squad.Leader);
            Assert.IsNotNull(squad.Members);
            Assert.AreEqual(0, squad.Members.Count);
            Assert.AreEqual(4, squad.TargetMembersCount);
            Assert.IsNotNull(squad.Objective);
            Assert.IsNotNull(squad.StrategyScores);
            Assert.AreEqual(3, squad.StrategyScores.Length);
        }

        [Test]
        public void StrategyScores_SizedByConstructorParam()
        {
            var squad = new SquadEntity(0, 8, 2);
            Assert.AreEqual(8, squad.StrategyScores.Length);

            var squad0 = new SquadEntity(1, 0, 1);
            Assert.AreEqual(0, squad0.StrategyScores.Length);
        }

        [Test]
        public void StrategyScores_DefaultToZero()
        {
            var squad = new SquadEntity(0, 4, 2);

            for (int i = 0; i < squad.StrategyScores.Length; i++)
                Assert.AreEqual(0f, squad.StrategyScores[i]);
        }

        [Test]
        public void Size_ReflectsMembersCount()
        {
            var squad = new SquadEntity(0, 2, 4);
            Assert.AreEqual(0, squad.Size);

            squad.Members.Add(new BotEntity(0));
            Assert.AreEqual(1, squad.Size);

            squad.Members.Add(new BotEntity(1));
            Assert.AreEqual(2, squad.Size);
        }

        [Test]
        public void IsActive_CanBeToggled()
        {
            var squad = new SquadEntity(0, 2, 4);

            squad.IsActive = false;
            Assert.IsFalse(squad.IsActive);

            squad.IsActive = true;
            Assert.IsTrue(squad.IsActive);
        }

        [Test]
        public void Leader_CanBeAssigned()
        {
            var squad = new SquadEntity(0, 2, 4);
            var bot = new BotEntity(0);

            squad.Leader = bot;
            Assert.AreSame(bot, squad.Leader);
        }

        [Test]
        public void Objective_IsCreatedInConstructor()
        {
            var squad = new SquadEntity(0, 2, 4);

            Assert.IsNotNull(squad.Objective);
            Assert.IsFalse(squad.Objective.HasObjective);
            Assert.AreEqual(0, squad.Objective.Version);
        }

        [Test]
        public void StrategyAssignment_DefaultIsEmpty()
        {
            var squad = new SquadEntity(0, 2, 4);

            Assert.IsNull(squad.StrategyAssignment.Strategy);
            Assert.AreEqual(0, squad.StrategyAssignment.Ordinal);
        }

        [Test]
        public void StrategyAssignment_CanBeSet()
        {
            var squad = new SquadEntity(0, 2, 4);
            var strategy = new object();

            squad.StrategyAssignment = new StrategyAssignment(strategy, 2);

            Assert.AreSame(strategy, squad.StrategyAssignment.Strategy);
            Assert.AreEqual(2, squad.StrategyAssignment.Ordinal);
        }

        // ── Equality ────────────────────────────────────────────

        [Test]
        public void Equals_SameId_ReturnsTrue()
        {
            var a = new SquadEntity(5, 2, 4);
            var b = new SquadEntity(5, 3, 6);

            Assert.IsTrue(a.Equals(b));
            Assert.IsTrue(b.Equals(a));
        }

        [Test]
        public void Equals_DifferentId_ReturnsFalse()
        {
            var a = new SquadEntity(1, 2, 4);
            var b = new SquadEntity(2, 2, 4);

            Assert.IsFalse(a.Equals(b));
        }

        [Test]
        public void Equals_Null_ReturnsFalse()
        {
            var squad = new SquadEntity(0, 2, 4);
            Assert.IsFalse(squad.Equals(null));
        }

        [Test]
        public void Equals_Object_WorksCorrectly()
        {
            var a = new SquadEntity(3, 2, 4);
            var b = new SquadEntity(3, 2, 4);

            Assert.IsTrue(a.Equals((object)b));
            Assert.IsFalse(a.Equals("not a squad"));
            Assert.IsFalse(a.Equals((object)null));
        }

        [Test]
        public void GetHashCode_EqualsId()
        {
            var squad = new SquadEntity(42, 2, 4);
            Assert.AreEqual(42, squad.GetHashCode());
        }

        [Test]
        public void ToString_ContainsRelevantInfo()
        {
            var squad = new SquadEntity(3, 2, 4);
            var str = squad.ToString();

            Assert.That(str, Does.Contain("Id=3"));
            Assert.That(str, Does.Contain("Active=True"));
            Assert.That(str, Does.Contain("Size=0"));
            Assert.That(str, Does.Contain("Leader=none"));
            Assert.That(str, Does.Contain("Target=4"));
        }

        [Test]
        public void ToString_WithLeader_ShowsLeaderId()
        {
            var squad = new SquadEntity(0, 2, 4);
            squad.Leader = new BotEntity(10);

            var str = squad.ToString();
            Assert.That(str, Does.Contain("Leader=10"));
        }
    }
}
