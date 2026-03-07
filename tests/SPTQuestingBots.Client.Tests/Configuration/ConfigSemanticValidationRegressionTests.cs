using System;
using System.IO;
using NUnit.Framework;

namespace SPTQuestingBots.Client.Tests.Configuration;

[TestFixture]
public class ConfigSemanticValidationRegressionTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static string FindRepoRoot()
    {
        var dir = TestContext.CurrentContext.TestDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        return TestContext.CurrentContext.TestDirectory;
    }

    private static string ReadConfigController()
    {
        string fullPath = Path.Combine(RepoRoot, "src", "SPTQuestingBots.Client", "Controllers", "ConfigController.cs");
        Assert.That(File.Exists(fullPath), Is.True, $"Source file not found: {fullPath}");
        return File.ReadAllText(fullPath);
    }

    private static string ExtractMethodBody(string source, string signature)
    {
        int methodStart = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.That(methodStart, Is.GreaterThanOrEqualTo(0), $"Method signature not found: {signature}");

        int braceStart = source.IndexOf('{', methodStart);
        Assert.That(braceStart, Is.GreaterThanOrEqualTo(0), $"Opening brace not found for: {signature}");

        int depth = 1;
        for (int i = braceStart + 1; i < source.Length; i++)
        {
            if (source[i] == '{')
            {
                depth++;
            }
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source.Substring(braceStart, i - braceStart + 1);
                }
            }
        }

        Assert.Fail($"Could not extract method body for: {signature}");
        return string.Empty;
    }

    [Test]
    public void InterpolateForFirstCol_UsesStrictInterpolationValidation()
    {
        string source = ReadConfigController();
        string methodBody = ExtractMethodBody(source, "public static double InterpolateForFirstCol(double[][] array, double value)");

        Assert.That(methodBody, Does.Contain("validateInterpolatedArray(array);"));
    }

    [Test]
    public void GetValueFromTotalChanceFraction_UsesWeightedValidation_WithFractionGuardrail()
    {
        string source = ReadConfigController();
        string methodBody = ExtractMethodBody(
            source,
            "public static double GetValueFromTotalChanceFraction(double[][] array, double fraction)"
        );

        Assert.That(methodBody, Does.Contain("validateWeightedArray(array, fraction);"));
    }

    [Test]
    public void ValidateInterpolatedArray_RequiresStrictlyIncreasingFirstColumn()
    {
        string source = ReadConfigController();
        string methodBody = ExtractMethodBody(source, "private static void validateInterpolatedArray(double[][] array)");

        Assert.That(methodBody, Does.Contain("if (array[i][0] <= array[i - 1][0])"));
        Assert.That(methodBody, Does.Contain("Interpolation arrays must be sorted by strictly increasing first-column values."));
    }

    [Test]
    public void ValidateWeightedArray_RejectsOutOfRangeFractions_NegativeWeights_AndZeroTotals()
    {
        string source = ReadConfigController();
        string methodBody = ExtractMethodBody(source, "private static void validateWeightedArray(double[][] array, double fraction)");

        Assert.That(methodBody, Does.Contain("if ((fraction < 0) || (fraction > 1))"));
        Assert.That(methodBody, Does.Contain("if (array[i][1] < 0)"));
        Assert.That(methodBody, Does.Contain("if (totalWeight <= 0)"));
    }

    [Test]
    public void ConfigController_NoLongerContainsUnusedAdjustPScavChanceEndpointHelper()
    {
        string source = ReadConfigController();

        Assert.That(source, Does.Not.Contain("AdjustPScavChance("));
        Assert.That(source, Does.Not.Contain("/QuestingBots/AdjustPScavChance"));
    }
}
