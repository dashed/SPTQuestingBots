using NSubstitute;
using NUnit.Framework;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTQuestingBots.Server.Configuration;
using SPTQuestingBots.Server.Services;

namespace SPTQuestingBots.Server.Tests;

[TestFixture]
public class ChanceArraySemanticValidationTests
{
    private QuestingBotsServerPlugin _plugin = null!;

    [SetUp]
    public void SetUp()
    {
        var logger = Substitute.For<ISptLogger<CommonUtils>>();
        var configLoader = CreateConfigLoader(new QuestingBotsConfig { Enabled = true });
        var commonUtils = new CommonUtils(logger, null!, null!, configLoader);

        _plugin = new QuestingBotsServerPlugin(configLoader, commonUtils, null!, null!, null!, Array.Empty<SptMod>());
    }

    [Test]
    public void InterpolatedArray_UnsortedX_ReturnsFalse()
    {
        double[][] array =
        [
            [0.5, 10],
            [0.25, 20],
        ];

        Assert.That(_plugin.IsInterpolatedArrayValid(array, shouldLeftColumnBeIntegers: false, minX: 0, maxX: 1, minY: 0), Is.False);
    }

    [Test]
    public void InterpolatedArray_DuplicateX_ReturnsFalse()
    {
        double[][] array =
        [
            [0.0, 10],
            [0.0, 20],
        ];

        Assert.That(_plugin.IsInterpolatedArrayValid(array, shouldLeftColumnBeIntegers: false, minX: 0, maxX: 1, minY: 0), Is.False);
    }

    [Test]
    public void InterpolatedArray_XOutsideBounds_ReturnsFalse()
    {
        double[][] array =
        [
            [-0.1, 10],
            [0.5, 20],
        ];

        Assert.That(_plugin.IsInterpolatedArrayValid(array, shouldLeftColumnBeIntegers: false, minX: 0, maxX: 1, minY: 0), Is.False);
    }

    [Test]
    public void InterpolatedArray_YOutsideBounds_ReturnsFalse()
    {
        double[][] array =
        [
            [0.0, 10],
            [1.0, 120],
        ];

        Assert.That(
            _plugin.IsInterpolatedArrayValid(array, shouldLeftColumnBeIntegers: false, minX: 0, maxX: 1, minY: 0, maxY: 100),
            Is.False
        );
    }

    [Test]
    public void InterpolatedArray_NonFiniteValue_ReturnsFalse()
    {
        double[][] array =
        [
            [0.0, 10],
            [0.5, double.PositiveInfinity],
        ];

        Assert.That(
            _plugin.IsInterpolatedArrayValid(array, shouldLeftColumnBeIntegers: false, minX: 0, maxX: 1, minY: 0, maxY: 100),
            Is.False
        );
    }

    [Test]
    public void InterpolatedArray_ValidCurveWithinBounds_ReturnsTrue()
    {
        double[][] array =
        [
            [0.0, 50],
            [0.5, 20],
            [1.0, 0],
        ];

        Assert.That(
            _plugin.IsInterpolatedArrayValid(array, shouldLeftColumnBeIntegers: false, minX: 0, maxX: 1, minY: 0, maxY: 100),
            Is.True
        );
    }

    [Test]
    public void WeightedDistribution_NegativeWeight_ReturnsFalse()
    {
        double[][] array =
        [
            [1, 80],
            [2, -5],
        ];

        Assert.That(_plugin.IsWeightedDistributionValid(array, shouldLeftColumnBeIntegers: true, minX: 1), Is.False);
    }

    [Test]
    public void WeightedDistribution_ZeroTotalWeight_ReturnsFalse()
    {
        double[][] array =
        [
            [1, 0],
            [2, 0],
        ];

        Assert.That(_plugin.IsWeightedDistributionValid(array, shouldLeftColumnBeIntegers: true, minX: 1), Is.False);
    }

    [Test]
    public void WeightedDistribution_XOutsideBounds_ReturnsFalse()
    {
        double[][] array =
        [
            [0, 50],
            [1, 50],
        ];

        Assert.That(_plugin.IsWeightedDistributionValid(array, shouldLeftColumnBeIntegers: true, minX: 1), Is.False);
    }

    [Test]
    public void WeightedDistribution_NonFiniteWeight_ReturnsFalse()
    {
        double[][] array =
        [
            [1, 80],
            [2, double.NaN],
        ];

        Assert.That(_plugin.IsWeightedDistributionValid(array, shouldLeftColumnBeIntegers: true, minX: 1), Is.False);
    }

    [Test]
    public void WeightedDistribution_ValidPositiveWeights_ReturnsTrue()
    {
        double[][] array =
        [
            [1, 75],
            [2, 20],
            [3, 5],
        ];

        Assert.That(_plugin.IsWeightedDistributionValid(array, shouldLeftColumnBeIntegers: true, minX: 1), Is.True);
    }

    private static QuestingBotsConfigLoader CreateConfigLoader(QuestingBotsConfig config)
    {
        var loader = new QuestingBotsConfigLoader(Substitute.For<ISptLogger<QuestingBotsConfigLoader>>());
        var field = typeof(QuestingBotsConfigLoader).GetField(
            "_config",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        );
        field!.SetValue(loader, config);
        return loader;
    }
}
