using NSubstitute;
using NUnit.Framework;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTQuestingBots.Server.Configuration;
using SPTQuestingBots.Server.Services;

namespace SPTQuestingBots.Server.Tests;

[TestFixture]
public class ChanceArrayValidationTests
{
    private QuestingBotsServerPlugin _plugin = null!;
    private CommonUtils _commonUtils = null!;

    [SetUp]
    public void SetUp()
    {
        // CommonUtils needs a real ISptLogger to function since LogError is not virtual
        var logger = Substitute.For<ISptLogger<CommonUtils>>();
        var configLoader = CreateConfigLoader(new QuestingBotsConfig { Enabled = true });
        _commonUtils = new CommonUtils(logger, null!, null!, configLoader);

        _plugin = new QuestingBotsServerPlugin(
            Substitute.For<ISptLogger<QuestingBotsServerPlugin>>(),
            configLoader,
            _commonUtils,
            null!, // BotLocationService not needed for array validation
            null!, // PMCConversionService not needed
            null!, // ConfigServer not needed
            Array.Empty<SptMod>()
        );
    }

    // ── Null and empty ───────────────────────────────────────────────

    [Test]
    public void NullArray_ReturnsFalse()
    {
        Assert.That(_plugin.IsChanceArrayValid(null, false), Is.False);
    }

    [Test]
    public void EmptyArray_ReturnsFalse()
    {
        Assert.That(_plugin.IsChanceArrayValid([], false), Is.False);
    }

    // ── Row length ───────────────────────────────────────────────────

    [Test]
    public void SingleRowWithTwoColumns_ReturnsTrue()
    {
        double[][] array =
        [
            [1.0, 0.5],
        ];
        Assert.That(_plugin.IsChanceArrayValid(array, false), Is.True);
    }

    [Test]
    public void RowWithOneColumn_ReturnsFalse()
    {
        double[][] array =
        [
            [1.0],
        ];
        Assert.That(_plugin.IsChanceArrayValid(array, false), Is.False);
    }

    [Test]
    public void RowWithThreeColumns_ReturnsFalse()
    {
        double[][] array =
        [
            [1.0, 0.5, 0.3],
        ];
        Assert.That(_plugin.IsChanceArrayValid(array, false), Is.False);
    }

    [Test]
    public void EmptyRow_ReturnsFalse()
    {
        double[][] array =
        [
            [],
        ];
        Assert.That(_plugin.IsChanceArrayValid(array, false), Is.False);
    }

    [Test]
    public void MixedValidAndInvalidRows_ReturnsFalse()
    {
        double[][] array =
        [
            [1.0, 0.5],
            [2.0],
        ]; // second row only 1 column
        Assert.That(_plugin.IsChanceArrayValid(array, false), Is.False);
    }

    // ── Integer constraint ───────────────────────────────────────────

    [Test]
    public void IntegerLeftColumn_WhenRequired_ReturnsTrue()
    {
        double[][] array =
        [
            [1.0, 0.5],
            [2.0, 0.3],
            [3.0, 0.2],
        ];
        Assert.That(_plugin.IsChanceArrayValid(array, shouldLeftColumnBeIntegers: true), Is.True);
    }

    [Test]
    public void NonIntegerLeftColumn_WhenRequired_ReturnsFalse()
    {
        double[][] array =
        [
            [1.5, 0.5],
        ];
        Assert.That(_plugin.IsChanceArrayValid(array, shouldLeftColumnBeIntegers: true), Is.False);
    }

    [Test]
    public void NonIntegerLeftColumn_WhenNotRequired_ReturnsTrue()
    {
        double[][] array =
        [
            [0.5, 0.75],
            [1.5, 0.25],
        ];
        Assert.That(_plugin.IsChanceArrayValid(array, shouldLeftColumnBeIntegers: false), Is.True);
    }

    [Test]
    public void ZeroLeftColumn_WhenIntegersRequired_ReturnsTrue()
    {
        double[][] array =
        [
            [0.0, 1.0],
        ];
        Assert.That(_plugin.IsChanceArrayValid(array, shouldLeftColumnBeIntegers: true), Is.True);
    }

    [Test]
    public void NegativeIntegerLeftColumn_WhenIntegersRequired_ReturnsTrue()
    {
        double[][] array =
        [
            [-1.0, 0.5],
        ];
        Assert.That(_plugin.IsChanceArrayValid(array, shouldLeftColumnBeIntegers: true), Is.True);
    }

    [Test]
    public void NegativeNonIntegerLeftColumn_WhenIntegersRequired_ReturnsFalse()
    {
        double[][] array =
        [
            [-1.5, 0.5],
        ];
        Assert.That(_plugin.IsChanceArrayValid(array, shouldLeftColumnBeIntegers: true), Is.False);
    }

    // ── Multi-row valid arrays ───────────────────────────────────────

    [Test]
    public void MultipleValidRows_ReturnsTrue()
    {
        double[][] array =
        [
            [0.0, 0.75],
            [0.5, 0.50],
            [1.0, 0.25],
        ];
        Assert.That(_plugin.IsChanceArrayValid(array, false), Is.True);
    }

    [Test]
    public void TypicalGroupDistribution_ReturnsTrue()
    {
        double[][] array =
        [
            [1, 80],
            [2, 15],
            [3, 5],
        ];
        Assert.That(_plugin.IsChanceArrayValid(array, shouldLeftColumnBeIntegers: true), Is.True);
    }

    [Test]
    public void TypicalDifficultyDistribution_ReturnsTrue()
    {
        double[][] array =
        [
            [0, 40],
            [1, 40],
            [2, 15],
            [3, 5],
        ];
        Assert.That(_plugin.IsChanceArrayValid(array, shouldLeftColumnBeIntegers: true), Is.True);
    }

    // ── Helper ───────────────────────────────────────────────────────

    private static QuestingBotsConfigLoader CreateConfigLoader(QuestingBotsConfig config)
    {
        var loader = new QuestingBotsConfigLoader(Substitute.For<ISptLogger<QuestingBotsConfigLoader>>());
        // Set the private _config field via reflection so the loader doesn't try to read from disk
        var field = typeof(QuestingBotsConfigLoader).GetField(
            "_config",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        );
        field!.SetValue(loader, config);
        return loader;
    }
}
