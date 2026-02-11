using System;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS.Systems;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.Systems;

[TestFixture]
public class FormationPositionUpdaterTests
{
    // --- SelectFormation ---

    [TestCase(3f, 5f, FormationType.Column)]
    [TestCase(4.99f, 5f, FormationType.Column)]
    [TestCase(5f, 5f, FormationType.Spread)]
    [TestCase(10f, 5f, FormationType.Spread)]
    public void SelectFormation_ReturnsExpectedType(float pathWidth, float switchWidth, FormationType expected)
    {
        var result = FormationPositionUpdater.SelectFormation(pathWidth, switchWidth);
        Assert.AreEqual(expected, result);
    }

    // --- ComputeHeading ---

    [Test]
    public void ComputeHeading_MovingEast_ReturnsUnitX()
    {
        bool ok = FormationPositionUpdater.ComputeHeading(0f, 0f, 10f, 0f, out float hx, out float hz);
        Assert.IsTrue(ok);
        Assert.That(hx, Is.EqualTo(1f).Within(0.001f));
        Assert.That(hz, Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void ComputeHeading_MovingNorth_ReturnsUnitZ()
    {
        bool ok = FormationPositionUpdater.ComputeHeading(0f, 0f, 0f, 10f, out float hx, out float hz);
        Assert.IsTrue(ok);
        Assert.That(hx, Is.EqualTo(0f).Within(0.001f));
        Assert.That(hz, Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void ComputeHeading_Diagonal_IsNormalized()
    {
        bool ok = FormationPositionUpdater.ComputeHeading(0f, 0f, 5f, 5f, out float hx, out float hz);
        Assert.IsTrue(ok);
        float len = (float)Math.Sqrt(hx * hx + hz * hz);
        Assert.That(len, Is.EqualTo(1f).Within(0.001f));
        Assert.That(hx, Is.EqualTo(hz).Within(0.001f)); // 45 degree: equal components
    }

    [Test]
    public void ComputeHeading_TooClose_ReturnsFalse()
    {
        // Distance = 0.05m, below 0.1m threshold
        bool ok = FormationPositionUpdater.ComputeHeading(0f, 0f, 0.03f, 0.04f, out float hx, out float hz);
        Assert.IsFalse(ok);
        Assert.AreEqual(0f, hx);
        Assert.AreEqual(0f, hz);
    }

    [Test]
    public void ComputeHeading_ExactThreshold_ReturnsTrue()
    {
        // Distance = 0.1m exactly (0.1^2 = 0.01, threshold is < 0.01 so 0.01 passes)
        bool ok = FormationPositionUpdater.ComputeHeading(0f, 0f, 0.1f, 0f, out float hx, out float hz);
        Assert.IsTrue(ok);
        Assert.That(hx, Is.EqualTo(1f).Within(0.001f));
        Assert.That(hz, Is.EqualTo(0f).Within(0.001f));
    }

    // --- ComputeColumnPositions ---

    [Test]
    public void ComputeColumn_OneFollower_DirectlyBehind()
    {
        var buf = new float[3];
        FormationPositionUpdater.ComputeColumnPositions(10f, 5f, 20f, 1f, 0f, 1, 3f, buf);

        // Heading east (1,0), so behind = west (-1,0) at distance 3
        Assert.That(buf[0], Is.EqualTo(7f).Within(0.001f)); // 10 - 1*3
        Assert.That(buf[1], Is.EqualTo(5f).Within(0.001f)); // Y preserved
        Assert.That(buf[2], Is.EqualTo(20f).Within(0.001f)); // Z unchanged
    }

    [Test]
    public void ComputeColumn_TwoFollowers_StaggeredBehind()
    {
        var buf = new float[6];
        FormationPositionUpdater.ComputeColumnPositions(0f, 0f, 0f, 1f, 0f, 2, 2f, buf);

        // First follower at 1*2 = 2m behind
        Assert.That(buf[0], Is.EqualTo(-2f).Within(0.001f));
        Assert.That(buf[1], Is.EqualTo(0f).Within(0.001f));
        Assert.That(buf[2], Is.EqualTo(0f).Within(0.001f));

        // Second follower at 2*2 = 4m behind
        Assert.That(buf[3], Is.EqualTo(-4f).Within(0.001f));
        Assert.That(buf[4], Is.EqualTo(0f).Within(0.001f));
        Assert.That(buf[5], Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void ComputeColumn_ThreeFollowers_StaggeredAtMultiples()
    {
        var buf = new float[9];
        FormationPositionUpdater.ComputeColumnPositions(0f, 0f, 0f, 0f, 1f, 3, 2.5f, buf);

        // Heading north (0,1), behind = south (0,-1)
        for (int i = 0; i < 3; i++)
        {
            float expectedZ = -(i + 1) * 2.5f;
            Assert.That(buf[i * 3], Is.EqualTo(0f).Within(0.001f), $"Follower {i} X");
            Assert.That(buf[i * 3 + 1], Is.EqualTo(0f).Within(0.001f), $"Follower {i} Y");
            Assert.That(buf[i * 3 + 2], Is.EqualTo(expectedZ).Within(0.001f), $"Follower {i} Z");
        }
    }

    [Test]
    public void ComputeColumn_DiagonalHeading_PositionsAlongNegativeHeading()
    {
        var buf = new float[3];
        float hx = (float)(1.0 / Math.Sqrt(2.0));
        float hz = (float)(1.0 / Math.Sqrt(2.0));
        FormationPositionUpdater.ComputeColumnPositions(0f, 10f, 0f, hx, hz, 1, 4f, buf);

        // Behind = -heading * 4
        Assert.That(buf[0], Is.EqualTo(-hx * 4f).Within(0.001f));
        Assert.That(buf[1], Is.EqualTo(10f).Within(0.001f)); // Y preserved
        Assert.That(buf[2], Is.EqualTo(-hz * 4f).Within(0.001f));
    }

    // --- ComputeSpreadPositions ---

    [Test]
    public void ComputeSpread_OneFollower_DirectlyBehind()
    {
        var buf = new float[3];
        FormationPositionUpdater.ComputeSpreadPositions(10f, 5f, 20f, 1f, 0f, 1, 3f, buf);

        // 1 follower: halfCount=0, lateralOffset=0 → directly behind
        Assert.That(buf[0], Is.EqualTo(7f).Within(0.001f)); // 10 - 1*3
        Assert.That(buf[1], Is.EqualTo(5f).Within(0.001f)); // Y preserved
        Assert.That(buf[2], Is.EqualTo(20f).Within(0.001f)); // Z unchanged
    }

    [Test]
    public void ComputeSpread_TwoFollowers_SpreadLeftAndRight()
    {
        var buf = new float[6];
        FormationPositionUpdater.ComputeSpreadPositions(0f, 0f, 0f, 1f, 0f, 2, 2f, buf);

        // Heading east (1,0), perp = (0,1)
        // halfCount = 0.5
        // Follower 0: lateral = (0 - 0.5)*2 = -1 → behind + perp*(-1)
        Assert.That(buf[0], Is.EqualTo(-2f).Within(0.001f)); // behindX
        Assert.That(buf[1], Is.EqualTo(0f).Within(0.001f));
        Assert.That(buf[2], Is.EqualTo(-1f).Within(0.001f)); // perpZ * -1

        // Follower 1: lateral = (1 - 0.5)*2 = 1 → behind + perp*(1)
        Assert.That(buf[3], Is.EqualTo(-2f).Within(0.001f)); // behindX
        Assert.That(buf[4], Is.EqualTo(0f).Within(0.001f));
        Assert.That(buf[5], Is.EqualTo(1f).Within(0.001f)); // perpZ * 1
    }

    [Test]
    public void ComputeSpread_ThreeFollowers_CenterLeftRight()
    {
        var buf = new float[9];
        FormationPositionUpdater.ComputeSpreadPositions(0f, 0f, 0f, 1f, 0f, 3, 3f, buf);

        // Heading east (1,0), perp = (0,1)
        // halfCount = 1
        // Follower 0: lateral = (0-1)*3 = -3
        Assert.That(buf[0], Is.EqualTo(-3f).Within(0.001f));
        Assert.That(buf[2], Is.EqualTo(-3f).Within(0.001f));

        // Follower 1: lateral = (1-1)*3 = 0 → directly behind
        Assert.That(buf[3], Is.EqualTo(-3f).Within(0.001f));
        Assert.That(buf[5], Is.EqualTo(0f).Within(0.001f));

        // Follower 2: lateral = (2-1)*3 = 3
        Assert.That(buf[6], Is.EqualTo(-3f).Within(0.001f));
        Assert.That(buf[8], Is.EqualTo(3f).Within(0.001f));
    }

    [Test]
    public void ComputeSpread_FourFollowers_EvenlySpread()
    {
        var buf = new float[12];
        FormationPositionUpdater.ComputeSpreadPositions(0f, 0f, 0f, 1f, 0f, 4, 2f, buf);

        // halfCount = 1.5
        // Offsets: -3, -1, 1, 3
        float[] expectedZ = { -3f, -1f, 1f, 3f };
        for (int i = 0; i < 4; i++)
        {
            Assert.That(buf[i * 3], Is.EqualTo(-2f).Within(0.001f), $"Follower {i} X (behind)");
            Assert.That(buf[i * 3 + 2], Is.EqualTo(expectedZ[i]).Within(0.001f), $"Follower {i} Z (lateral)");
        }
    }

    [Test]
    public void ComputeSpread_HeadingNorth_PerpIsEastWest()
    {
        var buf = new float[6];
        FormationPositionUpdater.ComputeSpreadPositions(0f, 0f, 0f, 0f, 1f, 2, 2f, buf);

        // Heading north (0,1), perp = (-1, 0)
        // Behind: (0, 0 - 1*2) = (0, -2)
        // halfCount = 0.5
        // Follower 0: lateral=-1 → behind + perp*(-1) = (0 + (-1)*(-1), -2 + 0*(-1)) = (1, -2)
        Assert.That(buf[0], Is.EqualTo(1f).Within(0.001f));
        Assert.That(buf[2], Is.EqualTo(-2f).Within(0.001f));

        // Follower 1: lateral=1 → behind + perp*(1) = (0 + (-1)*(1), -2 + 0*(1)) = (-1, -2)
        Assert.That(buf[3], Is.EqualTo(-1f).Within(0.001f));
        Assert.That(buf[5], Is.EqualTo(-2f).Within(0.001f));
    }

    [Test]
    public void ComputeSpread_YCoordinatePreserved()
    {
        var buf = new float[9];
        float bossY = 42.5f;
        FormationPositionUpdater.ComputeSpreadPositions(0f, bossY, 0f, 1f, 0f, 3, 2f, buf);

        for (int i = 0; i < 3; i++)
        {
            Assert.That(buf[i * 3 + 1], Is.EqualTo(bossY).Within(0.001f), $"Follower {i} Y");
        }
    }

    // --- ComputeFormationPositions (dispatch) ---

    [Test]
    public void ComputeFormationPositions_Column_DispatchesCorrectly()
    {
        var colBuf = new float[3];
        var dispatchBuf = new float[3];

        FormationPositionUpdater.ComputeColumnPositions(5f, 1f, 5f, 1f, 0f, 1, 2f, colBuf);
        FormationPositionUpdater.ComputeFormationPositions(FormationType.Column, 5f, 1f, 5f, 1f, 0f, 1, 2f, dispatchBuf);

        Assert.That(dispatchBuf[0], Is.EqualTo(colBuf[0]).Within(0.001f));
        Assert.That(dispatchBuf[1], Is.EqualTo(colBuf[1]).Within(0.001f));
        Assert.That(dispatchBuf[2], Is.EqualTo(colBuf[2]).Within(0.001f));
    }

    [Test]
    public void ComputeFormationPositions_Spread_DispatchesCorrectly()
    {
        var spreadBuf = new float[3];
        var dispatchBuf = new float[3];

        FormationPositionUpdater.ComputeSpreadPositions(5f, 1f, 5f, 1f, 0f, 1, 2f, spreadBuf);
        FormationPositionUpdater.ComputeFormationPositions(FormationType.Spread, 5f, 1f, 5f, 1f, 0f, 1, 2f, dispatchBuf);

        Assert.That(dispatchBuf[0], Is.EqualTo(spreadBuf[0]).Within(0.001f));
        Assert.That(dispatchBuf[1], Is.EqualTo(spreadBuf[1]).Within(0.001f));
        Assert.That(dispatchBuf[2], Is.EqualTo(spreadBuf[2]).Within(0.001f));
    }
}
