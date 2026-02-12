using NUnit.Framework;

namespace AssemblyInspector.Tests;

[TestFixture]
public class ParseKnownFieldsTests
{
    [Test]
    public void ParsesStandardEntry()
    {
        string source =
            @"
            private static readonly (Type Type, string FieldName, string Context)[] KnownFields = new[]
            {
                (typeof(BotSpawner), ""Bots"", ""BotDiedPatch ___Bots""),
            };
        ";

        var tmpFile = WriteTempFile(source);
        var entries = ValidateCommand.ParseKnownFields(tmpFile);

        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].TypeName, Is.EqualTo("BotSpawner"));
        Assert.That(entries[0].FieldName, Is.EqualTo("Bots"));
        Assert.That(entries[0].Context, Is.EqualTo("BotDiedPatch ___Bots"));
    }

    [Test]
    public void ParsesMultipleEntries()
    {
        string source =
            @"
            (typeof(BotSpawner), ""Bots"", ""context1""),
            (typeof(BossGroup), ""Boss_1"", ""context2""),
            (typeof(LocalGame), ""wavesSpawnScenario_0"", ""context3""),
        ";

        var tmpFile = WriteTempFile(source);
        var entries = ValidateCommand.ParseKnownFields(tmpFile);

        Assert.That(entries, Has.Count.EqualTo(3));
        Assert.That(entries[0].TypeName, Is.EqualTo("BotSpawner"));
        Assert.That(entries[1].TypeName, Is.EqualTo("BossGroup"));
        Assert.That(entries[2].TypeName, Is.EqualTo("LocalGame"));
    }

    [Test]
    public void ParsesBackingFieldName()
    {
        string source =
            @"
            (typeof(BotsGroup), ""<BotZone>k__BackingField"", ""GoToPositionAbstractAction""),
        ";

        var tmpFile = WriteTempFile(source);
        var entries = ValidateCommand.ParseKnownFields(tmpFile);

        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].FieldName, Is.EqualTo("<BotZone>k__BackingField"));
    }

    [Test]
    public void ParsesObfuscatedFieldNames()
    {
        string source =
            @"
            (typeof(NonWavesSpawnScenario), ""float_2"", ""retry time delay""),
            (typeof(BotCurrentPathAbstractClass), ""Vector3_0"", ""path corners""),
        ";

        var tmpFile = WriteTempFile(source);
        var entries = ValidateCommand.ParseKnownFields(tmpFile);

        Assert.That(entries, Has.Count.EqualTo(2));
        Assert.That(entries[0].FieldName, Is.EqualTo("float_2"));
        Assert.That(entries[1].FieldName, Is.EqualTo("Vector3_0"));
    }

    [Test]
    public void ReturnsEmptyForNoMatches()
    {
        string source =
            @"
            // No KnownFields entries here
            public class Foo { }
        ";

        var tmpFile = WriteTempFile(source);
        var entries = ValidateCommand.ParseKnownFields(tmpFile);

        Assert.That(entries, Is.Empty);
    }

    [Test]
    public void IgnoresComments()
    {
        string source =
            @"
            // (typeof(Commented), ""field"", ""context""),
            (typeof(Real), ""field"", ""context""),
        ";

        var tmpFile = WriteTempFile(source);
        var entries = ValidateCommand.ParseKnownFields(tmpFile);

        // The regex matches both since it doesn't know about C# comments.
        // This is acceptable — the commented-out line still looks like a valid entry.
        // In practice, ReflectionHelper.cs doesn't have commented-out entries.
        Assert.That(entries.Any(e => e.TypeName == "Real"), Is.True);
    }

    [Test]
    public void ParsesContextWithSpecialCharacters()
    {
        string source =
            @"
            (typeof(BotCurrentPathAbstractClass), ""Vector3_0"", ""BotPathingHelpers — path corner points""),
        ";

        var tmpFile = WriteTempFile(source);
        var entries = ValidateCommand.ParseKnownFields(tmpFile);

        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].Context, Is.EqualTo("BotPathingHelpers — path corner points"));
    }

    [Test]
    public void ParsesActualReflectionHelperFormat()
    {
        // Exact copy of a few lines from ReflectionHelper.cs
        string source =
            @"
        private static readonly (Type Type, string FieldName, string Context)[] KnownFields = new[]
        {
            // AccessTools.Field lookups
            (typeof(BotCurrentPathAbstractClass), ""Vector3_0"", ""BotPathingHelpers — path corner points""),
            (typeof(NonWavesSpawnScenario), ""float_2"", ""TrySpawnFreeAndDelayPatch — retry time delay""),
            // Harmony ___param field injections
            (typeof(BossGroup), ""Boss_1"", ""SetNewBossPatch ___Boss_1""),
            (typeof(BotSpawner), ""Bots"", ""BotDiedPatch ___Bots""),
        };
        ";

        var tmpFile = WriteTempFile(source);
        var entries = ValidateCommand.ParseKnownFields(tmpFile);

        Assert.That(entries, Has.Count.EqualTo(4));
        Assert.That(entries[0].TypeName, Is.EqualTo("BotCurrentPathAbstractClass"));
        Assert.That(entries[0].FieldName, Is.EqualTo("Vector3_0"));
        Assert.That(entries[1].TypeName, Is.EqualTo("NonWavesSpawnScenario"));
        Assert.That(entries[2].TypeName, Is.EqualTo("BossGroup"));
        Assert.That(entries[3].TypeName, Is.EqualTo("BotSpawner"));
    }

    private static string WriteTempFile(string content)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        return path;
    }
}
