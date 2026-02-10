using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS.Systems;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.Systems
{
    [TestFixture]
    public class SquadCalloutDeciderTests
    {
        // --- DecideBossCallout ---

        [Test]
        public void BossCallout_ObjectiveChanged_ReturnsGogogo()
        {
            Assert.AreEqual(SquadCalloutId.Gogogo, SquadCalloutDecider.DecideBossCallout(true, false));
        }

        [Test]
        public void BossCallout_BossArrived_ReturnsHoldPosition()
        {
            Assert.AreEqual(SquadCalloutId.HoldPosition, SquadCalloutDecider.DecideBossCallout(false, true));
        }

        [Test]
        public void BossCallout_NoChange_ReturnsNone()
        {
            Assert.AreEqual(SquadCalloutId.None, SquadCalloutDecider.DecideBossCallout(false, false));
        }

        [Test]
        public void BossCallout_BothTrue_ObjectiveChangedWins()
        {
            Assert.AreEqual(SquadCalloutId.Gogogo, SquadCalloutDecider.DecideBossCallout(true, true));
        }

        // --- DecideFollowerResponse to Gogogo ---

        [Test]
        public void FollowerResponse_Gogogo_EvenOrdinal_ReturnsRoger()
        {
            Assert.AreEqual(SquadCalloutId.Roger, SquadCalloutDecider.DecideFollowerResponse(SquadCalloutId.Gogogo, 0));
        }

        [Test]
        public void FollowerResponse_Gogogo_OddOrdinal_ReturnsGoing()
        {
            Assert.AreEqual(SquadCalloutId.Going, SquadCalloutDecider.DecideFollowerResponse(SquadCalloutId.Gogogo, 1));
        }

        [Test]
        public void FollowerResponse_Gogogo_EvenOrdinal2_ReturnsRoger()
        {
            Assert.AreEqual(SquadCalloutId.Roger, SquadCalloutDecider.DecideFollowerResponse(SquadCalloutId.Gogogo, 4));
        }

        // --- DecideFollowerResponse to HoldPosition ---

        [Test]
        public void FollowerResponse_HoldPosition_EvenOrdinal_ReturnsRoger()
        {
            Assert.AreEqual(SquadCalloutId.Roger, SquadCalloutDecider.DecideFollowerResponse(SquadCalloutId.HoldPosition, 2));
        }

        [Test]
        public void FollowerResponse_HoldPosition_OddOrdinal_ReturnsOnPosition()
        {
            Assert.AreEqual(SquadCalloutId.OnPosition, SquadCalloutDecider.DecideFollowerResponse(SquadCalloutId.HoldPosition, 3));
        }

        // --- DecideFollowerResponse to FollowMe (same as Gogogo) ---

        [Test]
        public void FollowerResponse_FollowMe_EvenOrdinal_ReturnsRoger()
        {
            Assert.AreEqual(SquadCalloutId.Roger, SquadCalloutDecider.DecideFollowerResponse(SquadCalloutId.FollowMe, 0));
        }

        [Test]
        public void FollowerResponse_FollowMe_OddOrdinal_ReturnsGoing()
        {
            Assert.AreEqual(SquadCalloutId.Going, SquadCalloutDecider.DecideFollowerResponse(SquadCalloutId.FollowMe, 1));
        }

        // --- DecideFollowerResponse to unknown ---

        [Test]
        public void FollowerResponse_UnknownCallout_ReturnsNone()
        {
            Assert.AreEqual(SquadCalloutId.None, SquadCalloutDecider.DecideFollowerResponse(SquadCalloutId.OnSix, 0));
        }

        [Test]
        public void FollowerResponse_NoneCallout_ReturnsNone()
        {
            Assert.AreEqual(SquadCalloutId.None, SquadCalloutDecider.DecideFollowerResponse(SquadCalloutId.None, 0));
        }

        // --- DecideArrivalCallout ---

        [Test]
        public void Arrival_JustArrived_ReturnsOnPosition()
        {
            Assert.AreEqual(SquadCalloutId.OnPosition, SquadCalloutDecider.DecideArrivalCallout(true));
        }

        [Test]
        public void Arrival_NotArrived_ReturnsNone()
        {
            Assert.AreEqual(SquadCalloutId.None, SquadCalloutDecider.DecideArrivalCallout(false));
        }

        // --- DecideEnemyDirectionCallout ---

        [Test]
        public void EnemyDirection_Behind_ReturnsOnSix()
        {
            Assert.AreEqual(SquadCalloutId.OnSix, SquadCalloutDecider.DecideEnemyDirectionCallout(-0.8f, 0.0f));
        }

        [Test]
        public void EnemyDirection_Left_ReturnsLeftFlank()
        {
            Assert.AreEqual(SquadCalloutId.LeftFlank, SquadCalloutDecider.DecideEnemyDirectionCallout(0.0f, -0.8f));
        }

        [Test]
        public void EnemyDirection_Right_ReturnsRightFlank()
        {
            Assert.AreEqual(SquadCalloutId.RightFlank, SquadCalloutDecider.DecideEnemyDirectionCallout(0.0f, 0.8f));
        }

        [Test]
        public void EnemyDirection_Front_ReturnsInTheFront()
        {
            Assert.AreEqual(SquadCalloutId.InTheFront, SquadCalloutDecider.DecideEnemyDirectionCallout(0.8f, 0.0f));
        }

        [Test]
        public void EnemyDirection_Ambiguous_ReturnsOnFirstContact()
        {
            Assert.AreEqual(SquadCalloutId.OnFirstContact, SquadCalloutDecider.DecideEnemyDirectionCallout(0.1f, 0.1f));
        }

        [Test]
        public void EnemyDirection_ExactlyMinusHalfForward_ReturnsOnFirstContact()
        {
            // -0.5 is NOT < -0.5, so falls through to ambiguous
            Assert.AreEqual(SquadCalloutId.OnFirstContact, SquadCalloutDecider.DecideEnemyDirectionCallout(-0.5f, 0.0f));
        }

        [Test]
        public void EnemyDirection_JustBelowMinusHalfForward_ReturnsOnSix()
        {
            Assert.AreEqual(SquadCalloutId.OnSix, SquadCalloutDecider.DecideEnemyDirectionCallout(-0.501f, 0.0f));
        }

        [Test]
        public void EnemyDirection_BehindAndLeft_OnSixWins()
        {
            // dotForward < -0.5 is checked first
            Assert.AreEqual(SquadCalloutId.OnSix, SquadCalloutDecider.DecideEnemyDirectionCallout(-0.7f, -0.7f));
        }

        // --- IsOnCooldown ---

        [Test]
        public void Cooldown_WithinCooldown_ReturnsTrue()
        {
            Assert.IsTrue(SquadCalloutDecider.IsOnCooldown(1.0f, 2.0f, 3.0f));
        }

        [Test]
        public void Cooldown_BeyondCooldown_ReturnsFalse()
        {
            Assert.IsFalse(SquadCalloutDecider.IsOnCooldown(1.0f, 5.0f, 3.0f));
        }

        [Test]
        public void Cooldown_ExactCooldown_ReturnsFalse()
        {
            // currentTime - lastTime == cooldown => not < cooldown => false
            Assert.IsFalse(SquadCalloutDecider.IsOnCooldown(1.0f, 4.0f, 3.0f));
        }

        [Test]
        public void Cooldown_ZeroLastTime_WithSmallCurrent()
        {
            Assert.IsTrue(SquadCalloutDecider.IsOnCooldown(0.0f, 0.5f, 2.0f));
        }
    }
}
