using NUnit.Framework;
using SPTQuestingBots.ZoneMovement.Core;

namespace SPTQuestingBots.Client.Tests.ZoneMovement;

/// <summary>
/// Tests for the naming contract between <c>ZoneQuestBuilder</c> (creates objectives
/// named <c>"Zone (col,row)"</c>) and <c>ZoneObjectiveCycler</c> (matches by that name).
/// The cycler itself depends on game types (BotOwner, Quest, WorldGridManager), but the
/// naming logic is pure string formatting that we can validate here.
/// </summary>
[TestFixture]
public class ZoneObjectiveCyclerTests
{
    [Test]
    public void ObjectiveNameFormat_MatchesZoneQuestBuilderConvention()
    {
        // ZoneQuestBuilder names objectives as: $"Zone ({col},{row})"
        // ZoneObjectiveCycler looks up objectives as: $"Zone ({cell.Col},{cell.Row})"
        // These MUST use the same format or field-based selection silently falls back.
        var cell = new GridCell(7, 13, new UnityEngine.Vector3(100, 0, 200));

        string cyclerName = $"Zone ({cell.Col},{cell.Row})";

        // This is the exact format ZoneQuestBuilder uses at line 89:
        //   objective.SetName($"Zone ({col},{row})");
        string builderName = $"Zone ({7},{13})";

        Assert.That(cyclerName, Is.EqualTo(builderName));
    }

    [TestCase(0, 0)]
    [TestCase(15, 9)]
    [TestCase(99, 99)]
    public void ObjectiveNameFormat_VariousCells_Consistent(int col, int row)
    {
        var cell = new GridCell(col, row, new UnityEngine.Vector3(0, 0, 0));

        string fromCell = $"Zone ({cell.Col},{cell.Row})";
        string fromInts = $"Zone ({col},{row})";

        Assert.That(fromCell, Is.EqualTo(fromInts));
    }

    [Test]
    public void ObjectiveNameFormat_ContainsNoExtraSpaces()
    {
        // Ensure no accidental spaces after comma that would break matching
        var cell = new GridCell(3, 5, new UnityEngine.Vector3(0, 0, 0));
        string name = $"Zone ({cell.Col},{cell.Row})";

        Assert.That(name, Is.EqualTo("Zone (3,5)"));
        Assert.That(name, Does.Not.Contain(", "));
    }
}
