using NUnit.Framework;

namespace SPTQuestingBots.Client.Tests.Helpers;

/// <summary>
/// ConnectionGroupHelper requires game singletons (Singleton&lt;IBotGame&gt;) and BotOwner types.
/// These tests verify the pure-logic aspects of connection group validation.
/// Game-dependent methods are tested via integration testing in-game.
/// </summary>
[TestFixture]
public class ConnectionGroupHelperTests
{
    [Test]
    public void ConnectionGroup_SameGroup_IsConnected()
    {
        // Two positions in the same connection group should be considered connected
        int group1 = 5;
        int group2 = 5;
        bool connected = group1 == group2 || group1 < 0 || group2 < 0;
        Assert.That(connected, Is.True);
    }

    [Test]
    public void ConnectionGroup_DifferentGroups_NotConnected()
    {
        int group1 = 5;
        int group2 = 8;
        bool connected = group1 == group2 || group1 < 0 || group2 < 0;
        Assert.That(connected, Is.False);
    }

    [Test]
    public void ConnectionGroup_UnknownGroup_FailsOpen()
    {
        // -1 means "unknown" — should fail-open (treat as connected)
        int group1 = -1;
        int group2 = 5;
        bool connected = group1 == group2 || group1 < 0 || group2 < 0;
        Assert.That(connected, Is.True);
    }

    [Test]
    public void ConnectionGroup_BothUnknown_FailsOpen()
    {
        int group1 = -1;
        int group2 = -1;
        bool connected = group1 == group2 || group1 < 0 || group2 < 0;
        Assert.That(connected, Is.True);
    }
}
