using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS.Systems;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.Systems;

[TestFixture]
public class CommunicationRangeTests
{
    [Test]
    public void BothEarpiece_WithinRange_ReturnsTrue()
    {
        // earpieceRange=100, sqrDist=50*50=2500, rangeSqr=100*100=10000
        bool result = CommunicationRange.IsInRange(true, true, 2500f, 50f, 100f);
        Assert.IsTrue(result);
    }

    [Test]
    public void BothEarpiece_OutOfRange_ReturnsFalse()
    {
        // earpieceRange=100, sqrDist=150*150=22500, rangeSqr=100*100=10000
        bool result = CommunicationRange.IsInRange(true, true, 22500f, 50f, 100f);
        Assert.IsFalse(result);
    }

    [Test]
    public void NoEarpiece_WithinRange_ReturnsTrue()
    {
        // noEarpieceRange=50, sqrDist=30*30=900, rangeSqr=50*50=2500
        bool result = CommunicationRange.IsInRange(false, false, 900f, 50f, 100f);
        Assert.IsTrue(result);
    }

    [Test]
    public void NoEarpiece_OutOfRange_ReturnsFalse()
    {
        // noEarpieceRange=50, sqrDist=60*60=3600, rangeSqr=50*50=2500
        bool result = CommunicationRange.IsInRange(false, false, 3600f, 50f, 100f);
        Assert.IsFalse(result);
    }

    [Test]
    public void OneEarpiece_FallsBackToNoEarpieceRange()
    {
        // Only A has earpiece — falls back to noEarpieceRange=50
        // sqrDist=60*60=3600 > 50*50=2500 → false
        bool result = CommunicationRange.IsInRange(true, false, 3600f, 50f, 100f);
        Assert.IsFalse(result);

        // Reverse: only B has earpiece — also falls back to noEarpieceRange
        bool result2 = CommunicationRange.IsInRange(false, true, 3600f, 50f, 100f);
        Assert.IsFalse(result2);
    }

    [Test]
    public void ZeroRange_SamePosition_ReturnsTrue()
    {
        // sqrDist=0, range=0 → 0 <= 0 → true
        bool result = CommunicationRange.IsInRange(false, false, 0f, 0f, 0f);
        Assert.IsTrue(result);
    }

    [Test]
    public void ZeroRange_AnyDistance_ReturnsFalse()
    {
        // sqrDist=1, range=0 → 1 <= 0 → false
        bool result = CommunicationRange.IsInRange(false, false, 1f, 0f, 0f);
        Assert.IsFalse(result);
    }

    [Test]
    public void ExactlyAtRange_ReturnsTrue()
    {
        // sqrDist = range*range → boundary, should be true (<=)
        float range = 75f;
        float sqrDist = range * range; // 5625
        bool result = CommunicationRange.IsInRange(false, false, sqrDist, range, 100f);
        Assert.IsTrue(result);
    }

    [Test]
    public void NegativeRange_ReturnsFalse()
    {
        // Negative range: (-5)^2 = 25, sqrDist=100 > 25 → false
        bool result = CommunicationRange.IsInRange(false, false, 100f, -5f, -5f);
        Assert.IsFalse(result);
    }

    [Test]
    public void MixedEarpiece_SecondHasEarpiece_UsesNoEarpieceRange()
    {
        // Only B has earpiece — uses noEarpieceRange=30
        // sqrDist=20*20=400, rangeSqr=30*30=900 → 400 <= 900 → true
        bool result = CommunicationRange.IsInRange(false, true, 400f, 30f, 200f);
        Assert.IsTrue(result);
    }
}
