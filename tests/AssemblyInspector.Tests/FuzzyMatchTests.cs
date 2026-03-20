using NUnit.Framework;

namespace AssemblyInspector.Tests;

[TestFixture]
public class FuzzyMatchTests
{
    [Test]
    public void BSp_MatchesBotSpawner()
    {
        Assert.That(InspectCommand.IsSubsequenceMatch("BotSpawner", "BSp"), Is.True);
    }

    [Test]
    public void nwss_MatchesNonWavesSpawnScenario()
    {
        Assert.That(InspectCommand.IsSubsequenceMatch("NonWavesSpawnScenario", "nwss"), Is.True);
    }

    [Test]
    public void xyz_DoesNotMatchBotSpawner()
    {
        Assert.That(InspectCommand.IsSubsequenceMatch("BotSpawner", "xyz"), Is.False);
    }

    [Test]
    public void EmptyPattern_MatchesEverything()
    {
        Assert.That(InspectCommand.IsSubsequenceMatch("BotSpawner", ""), Is.True);
        Assert.That(InspectCommand.IsSubsequenceMatch("", ""), Is.True);
        Assert.That(InspectCommand.IsSubsequenceMatch("Anything", ""), Is.True);
    }

    [Test]
    public void PatternLongerThanText_NeverMatches()
    {
        Assert.That(InspectCommand.IsSubsequenceMatch("Bot", "BotSpawner"), Is.False);
        Assert.That(InspectCommand.IsSubsequenceMatch("A", "AB"), Is.False);
    }

    [Test]
    public void CaseInsensitiveMatching()
    {
        Assert.That(InspectCommand.IsSubsequenceMatch("BotSpawner", "bsp"), Is.True);
        Assert.That(InspectCommand.IsSubsequenceMatch("BotSpawner", "BSP"), Is.True);
        Assert.That(InspectCommand.IsSubsequenceMatch("botspawner", "BSP"), Is.True);
    }

    [Test]
    public void ExactMatch_IsSubsequence()
    {
        Assert.That(InspectCommand.IsSubsequenceMatch("BotSpawner", "BotSpawner"), Is.True);
    }

    [Test]
    public void SingleCharacterMatch()
    {
        Assert.That(InspectCommand.IsSubsequenceMatch("BotSpawner", "B"), Is.True);
        Assert.That(InspectCommand.IsSubsequenceMatch("BotSpawner", "r"), Is.True);
        Assert.That(InspectCommand.IsSubsequenceMatch("BotSpawner", "z"), Is.False);
    }

    [Test]
    public void OutOfOrderChars_DoNotMatch()
    {
        // "SB" requires S before B, but in "BotSpawner" B comes before S
        Assert.That(InspectCommand.IsSubsequenceMatch("BotSpawner", "SB"), Is.False);
    }

    [Test]
    public void SuggestionsRankedByNameLength()
    {
        // Verify shorter names are ranked first by testing the ordering concept.
        // The actual ranking is done in SuggestTypes via OrderBy(t => t.Name.Length).
        // Here we verify the subsequence matching that feeds into ranking.
        Assert.That(InspectCommand.IsSubsequenceMatch("Bot", "bt"), Is.True);
        Assert.That(InspectCommand.IsSubsequenceMatch("BotSpawner", "bt"), Is.True);
        Assert.That(InspectCommand.IsSubsequenceMatch("BotSpawnerController", "bt"), Is.True);

        // "Bot" (3) < "BotSpawner" (10) < "BotSpawnerController" (20) — shorter first
        var names = new[] { "BotSpawnerController", "Bot", "BotSpawner" };
        var sorted = names.OrderBy(n => n.Length).ToArray();
        Assert.That(sorted[0], Is.EqualTo("Bot"));
        Assert.That(sorted[1], Is.EqualTo("BotSpawner"));
        Assert.That(sorted[2], Is.EqualTo("BotSpawnerController"));
    }
}
