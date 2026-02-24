using System;
using NUnit.Framework;
using SPTQuestingBots.Configuration;

namespace SPTQuestingBots.Client.Tests.Configuration;

[TestFixture]
public class MinMaxConfigTests
{
    // ── Constructor ─────────────────────────────────────────

    [Test]
    public void DefaultConstructor_SetsMinZeroMaxHundred()
    {
        var config = new MinMaxConfig();
        Assert.AreEqual(0, config.Min);
        Assert.AreEqual(100, config.Max);
    }

    [Test]
    public void ParameterizedConstructor_SetsValues()
    {
        var config = new MinMaxConfig(10, 50);
        Assert.AreEqual(10, config.Min);
        Assert.AreEqual(50, config.Max);
    }

    // ── Arithmetic operators (MinMaxConfig, MinMaxConfig) ───

    [Test]
    public void Add_MinMaxConfig_AddsAndRounds()
    {
        var a = new MinMaxConfig(10.4, 20.6);
        var b = new MinMaxConfig(5.3, 10.2);
        var result = a + b;

        Assert.AreEqual(Math.Round(10.4 + 5.3), result.Min);
        Assert.AreEqual(Math.Round(20.6 + 10.2), result.Max);
    }

    [Test]
    public void Subtract_MinMaxConfig_SubtractsAndRounds()
    {
        var a = new MinMaxConfig(20, 50);
        var b = new MinMaxConfig(5, 10);
        var result = a - b;

        Assert.AreEqual(15, result.Min);
        Assert.AreEqual(40, result.Max);
    }

    [Test]
    public void Multiply_MinMaxConfig_MultipliesAndRounds()
    {
        var a = new MinMaxConfig(3, 7);
        var b = new MinMaxConfig(4, 5);
        var result = a * b;

        Assert.AreEqual(12, result.Min);
        Assert.AreEqual(35, result.Max);
    }

    [Test]
    public void Divide_MinMaxConfig_DividesAndRounds()
    {
        var a = new MinMaxConfig(10, 21);
        var b = new MinMaxConfig(3, 4);
        var result = a / b;

        Assert.AreEqual(Math.Round(10.0 / 3.0), result.Min);
        Assert.AreEqual(Math.Round(21.0 / 4.0), result.Max);
    }

    // ── Arithmetic operators (MinMaxConfig, double) ─────────

    [Test]
    public void Add_Double_AddsAndRounds()
    {
        var a = new MinMaxConfig(10, 20);
        var result = a + 5.0;

        Assert.AreEqual(15, result.Min);
        Assert.AreEqual(25, result.Max);
    }

    [Test]
    public void Subtract_Double_SubtractsAndRounds()
    {
        var a = new MinMaxConfig(10, 20);
        var result = a - 3.0;

        Assert.AreEqual(7, result.Min);
        Assert.AreEqual(17, result.Max);
    }

    [Test]
    public void Multiply_Double_MultipliesAndRounds()
    {
        var a = new MinMaxConfig(10, 20);
        var result = a * 2.5;

        Assert.AreEqual(25, result.Min);
        Assert.AreEqual(50, result.Max);
    }

    [Test]
    public void Divide_Double_DividesAndRounds()
    {
        var a = new MinMaxConfig(10, 20);
        var result = a / 3.0;

        Assert.AreEqual(Math.Round(10.0 / 3.0), result.Min);
        Assert.AreEqual(Math.Round(20.0 / 3.0), result.Max);
    }

    // ── Division by zero guards (Bug fix: MinMaxConfig / 0) ─

    [Test]
    public void Divide_MinMaxConfig_ZeroMin_ReturnsZeroMin()
    {
        var a = new MinMaxConfig(10, 20);
        var b = new MinMaxConfig(0, 5);
        var result = a / b;

        Assert.AreEqual(0, result.Min, "Division by zero in Min should return 0, not Infinity/NaN");
        Assert.AreEqual(Math.Round(20.0 / 5.0), result.Max);
    }

    [Test]
    public void Divide_MinMaxConfig_ZeroMax_ReturnsZeroMax()
    {
        var a = new MinMaxConfig(10, 20);
        var b = new MinMaxConfig(5, 0);
        var result = a / b;

        Assert.AreEqual(Math.Round(10.0 / 5.0), result.Min);
        Assert.AreEqual(0, result.Max, "Division by zero in Max should return 0, not Infinity/NaN");
    }

    [Test]
    public void Divide_MinMaxConfig_BothZero_ReturnsBothZero()
    {
        var a = new MinMaxConfig(10, 20);
        var b = new MinMaxConfig(0, 0);
        var result = a / b;

        Assert.AreEqual(0, result.Min, "Division by zero should return 0");
        Assert.AreEqual(0, result.Max, "Division by zero should return 0");
    }

    [Test]
    public void Divide_MinMaxConfig_ZeroDivisor_DoesNotReturnNaN()
    {
        var a = new MinMaxConfig(100, 200);
        var b = new MinMaxConfig(0, 0);
        var result = a / b;

        Assert.IsFalse(double.IsNaN(result.Min), "Min should not be NaN after divide by zero");
        Assert.IsFalse(double.IsNaN(result.Max), "Max should not be NaN after divide by zero");
        Assert.IsFalse(double.IsInfinity(result.Min), "Min should not be Infinity after divide by zero");
        Assert.IsFalse(double.IsInfinity(result.Max), "Max should not be Infinity after divide by zero");
    }

    [Test]
    public void Divide_Double_Zero_ReturnsZeroBoth()
    {
        var a = new MinMaxConfig(10, 20);
        var result = a / 0.0;

        Assert.AreEqual(0, result.Min, "Division by zero double should return 0");
        Assert.AreEqual(0, result.Max, "Division by zero double should return 0");
    }

    [Test]
    public void Divide_Double_Zero_DoesNotReturnNaN()
    {
        var a = new MinMaxConfig(100, 200);
        var result = a / 0.0;

        Assert.IsFalse(double.IsNaN(result.Min), "Min should not be NaN after divide by zero");
        Assert.IsFalse(double.IsNaN(result.Max), "Max should not be NaN after divide by zero");
        Assert.IsFalse(double.IsInfinity(result.Min), "Min should not be Infinity after divide by zero");
        Assert.IsFalse(double.IsInfinity(result.Max), "Max should not be Infinity after divide by zero");
    }

    // ── Edge cases ──────────────────────────────────────────

    [Test]
    public void Divide_MinMaxConfig_NegativeValues_WorksCorrectly()
    {
        var a = new MinMaxConfig(-10, -20);
        var b = new MinMaxConfig(2, 5);
        var result = a / b;

        Assert.AreEqual(Math.Round(-10.0 / 2.0), result.Min);
        Assert.AreEqual(Math.Round(-20.0 / 5.0), result.Max);
    }

    [Test]
    public void Divide_Double_Negative_WorksCorrectly()
    {
        var a = new MinMaxConfig(10, 20);
        var result = a / -2.0;

        Assert.AreEqual(Math.Round(10.0 / -2.0), result.Min);
        Assert.AreEqual(Math.Round(20.0 / -2.0), result.Max);
    }

    [Test]
    public void Multiply_Double_Zero_ReturnsZero()
    {
        var a = new MinMaxConfig(10, 20);
        var result = a * 0.0;

        Assert.AreEqual(0, result.Min);
        Assert.AreEqual(0, result.Max);
    }

    [Test]
    public void Subtract_LargerFromSmaller_NegativeResults()
    {
        var a = new MinMaxConfig(5, 10);
        var b = new MinMaxConfig(20, 30);
        var result = a - b;

        Assert.AreEqual(-15, result.Min);
        Assert.AreEqual(-20, result.Max);
    }

    // ── Chained operations ──────────────────────────────────

    [Test]
    public void ChainedOperations_ProduceCorrectResults()
    {
        var a = new MinMaxConfig(10, 20);
        var b = new MinMaxConfig(2, 4);
        // (10,20) * (2,4) = (20,80), then / 2.0 = (10,40)
        var result = (a * b) / 2.0;

        Assert.AreEqual(10, result.Min);
        Assert.AreEqual(40, result.Max);
    }

    [Test]
    public void Divide_MinMaxConfig_ZeroDivisorInChain_DoesNotPropagate()
    {
        // If division by zero happens in a chain, it should return 0, not NaN that propagates
        var a = new MinMaxConfig(10, 20);
        var zero = new MinMaxConfig(0, 0);
        var result = (a / zero) + new MinMaxConfig(5, 5);

        // (0, 0) + (5, 5) = (5, 5) — not NaN
        Assert.AreEqual(5, result.Min);
        Assert.AreEqual(5, result.Max);
    }
}
