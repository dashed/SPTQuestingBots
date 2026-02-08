using System;
using NUnit.Framework;
using SPTQuestingBots.ZoneMovement.Fields;

namespace SPTQuestingBots.Client.Tests.ZoneMovement;

[TestFixture]
public class FieldComposerTests
{
    [Test]
    public void AllZeroInputs_ReturnsZero()
    {
        var composer = new FieldComposer();

        composer.GetCompositeDirection(0, 0, 0, 0, 0, 0, 0, out float x, out float z);

        Assert.Multiple(() =>
        {
            Assert.That(x, Is.EqualTo(0f));
            Assert.That(z, Is.EqualTo(0f));
        });
    }

    [Test]
    public void ConvergenceOnly_OutputMatchesConvergence()
    {
        // Only convergence has weight, others zero
        var composer = new FieldComposer(convergenceWeight: 1.0f, advectionWeight: 0f, momentumWeight: 0f, noiseWeight: 0f);

        composer.GetCompositeDirection(
            0,
            0, // advection
            1,
            0, // convergence: east
            0,
            0, // momentum
            0, // noise
            out float x,
            out float z
        );

        Assert.That(x, Is.GreaterThan(0.9f));
        Assert.That(Math.Abs(z), Is.LessThan(0.1f));
    }

    [Test]
    public void AdvectionOnly_OutputMatchesAdvection()
    {
        var composer = new FieldComposer(convergenceWeight: 0f, advectionWeight: 1.0f, momentumWeight: 0f, noiseWeight: 0f);

        composer.GetCompositeDirection(
            0,
            1, // advection: north
            0,
            0, // convergence
            0,
            0, // momentum
            0, // noise
            out float x,
            out float z
        );

        Assert.That(z, Is.GreaterThan(0.9f));
        Assert.That(Math.Abs(x), Is.LessThan(0.1f));
    }

    [Test]
    public void MomentumSmooths_Direction()
    {
        var composer = new FieldComposer(convergenceWeight: 1.0f, advectionWeight: 0f, momentumWeight: 1.0f, noiseWeight: 0f);

        // Convergence points east, momentum points north → composite is northeast
        composer.GetCompositeDirection(
            0,
            0, // advection
            1,
            0, // convergence: east
            0,
            1, // momentum: north
            0, // noise
            out float x,
            out float z
        );

        // Both should be positive (northeast quadrant)
        Assert.Multiple(() =>
        {
            Assert.That(x, Is.GreaterThan(0f));
            Assert.That(z, Is.GreaterThan(0f));
        });
    }

    [Test]
    public void NoiseRotation_ChangesDirection()
    {
        var composer = new FieldComposer(
            convergenceWeight: 1.0f,
            advectionWeight: 0f,
            momentumWeight: 0f,
            noiseWeight: 1.0f // Full noise weight
        );

        // Convergence points east, add 90-degree noise rotation
        float halfPi = (float)(Math.PI / 2);
        composer.GetCompositeDirection(
            0,
            0, // advection
            1,
            0, // convergence: east
            0,
            0, // momentum
            halfPi, // noise: rotate 90° (with weight 1.0)
            out float x,
            out float z
        );

        // 90° rotation of (1,0) should give approximately (0,1) — north
        Assert.That(Math.Abs(x), Is.LessThan(0.2f));
        Assert.That(z, Is.GreaterThan(0.8f));
    }

    [Test]
    public void ZeroNoise_NoRotation()
    {
        var composer = new FieldComposer(
            convergenceWeight: 1.0f,
            advectionWeight: 0f,
            momentumWeight: 0f,
            noiseWeight: 0f // No noise
        );

        // Even with noise angle, weight is 0 so no rotation
        composer.GetCompositeDirection(
            0,
            0,
            1,
            0,
            0,
            0,
            (float)Math.PI, // Large noise angle — but weight is 0
            out float x,
            out float z
        );

        Assert.That(x, Is.GreaterThan(0.9f));
    }

    [Test]
    public void OutputIsNormalized()
    {
        var composer = new FieldComposer();

        composer.GetCompositeDirection(
            0.3f,
            0.7f, // advection
            0.8f,
            -0.2f, // convergence
            0.1f,
            0.5f, // momentum
            0.5f, // noise
            out float x,
            out float z
        );

        float mag = (float)Math.Sqrt(x * x + z * z);
        Assert.That(mag, Is.EqualTo(1f).Within(0.01f).Or.EqualTo(0f).Within(0.01f));
    }

    [Test]
    public void Weights_AreStoredCorrectly()
    {
        var composer = new FieldComposer(2f, 3f, 4f, 5f);

        Assert.Multiple(() =>
        {
            Assert.That(composer.ConvergenceWeight, Is.EqualTo(2f));
            Assert.That(composer.AdvectionWeight, Is.EqualTo(3f));
            Assert.That(composer.MomentumWeight, Is.EqualTo(4f));
            Assert.That(composer.NoiseWeight, Is.EqualTo(5f));
        });
    }
}
