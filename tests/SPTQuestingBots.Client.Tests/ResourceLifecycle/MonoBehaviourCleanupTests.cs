using System.IO;
using System.Linq;
using NUnit.Framework;

namespace SPTQuestingBots.Client.Tests.ResourceLifecycle;

/// <summary>
/// Tests that MonoBehaviour subclasses properly implement OnDestroy to clean up
/// event subscriptions, references, and coroutines.
/// </summary>
[TestFixture]
public class MonoBehaviourCleanupTests
{
    private static readonly string ClientSrcDir = Path.Combine(FindRepoRoot(), "src", "SPTQuestingBots.Client");

    private static string FindRepoRoot()
    {
        var dir = TestContext.CurrentContext.TestDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        return TestContext.CurrentContext.TestDirectory;
    }

    [Test]
    public void DebugData_ImplementsOnDestroy()
    {
        string source = File.ReadAllText(Path.Combine(ClientSrcDir, "Components", "DebugData.cs"));

        Assert.That(source, Does.Contain("void OnDestroy()"), "DebugData must implement OnDestroy");
    }

    [Test]
    public void LightkeeperIslandMonitor_ImplementsOnDestroy()
    {
        string source = File.ReadAllText(Path.Combine(ClientSrcDir, "Components", "LightkeeperIslandMonitor.cs"));

        Assert.That(source, Does.Contain("void OnDestroy()"), "LightkeeperIslandMonitor must implement OnDestroy");
    }

    [Test]
    public void LocationData_ImplementsOnDestroy()
    {
        string source = File.ReadAllText(Path.Combine(ClientSrcDir, "Components", "LocationData.cs"));

        Assert.That(source, Does.Contain("void OnDestroy()"), "LocationData must implement OnDestroy");
    }

    [Test]
    public void BotMonitorController_ImplementsOnDestroy()
    {
        string source = File.ReadAllText(Path.Combine(ClientSrcDir, "BotLogic", "BotMonitor", "BotMonitorController.cs"));

        Assert.That(source, Does.Contain("void OnDestroy()"), "BotMonitorController must implement OnDestroy");
    }

    /// <summary>
    /// All MonoBehaviours that subscribe to events in Awake/Start must unsubscribe in OnDestroy.
    /// This test checks DebugData specifically since it was the bug found in Round 16.
    /// </summary>
    [Test]
    public void DebugData_OnDestroy_UnsubscribesAllEvents()
    {
        string source = File.ReadAllText(Path.Combine(ClientSrcDir, "Components", "DebugData.cs"));

        // Count event subscriptions (+= on SettingChanged)
        int subscriptionCount = source.Split('\n').Count(line => line.Contains("SettingChanged +="));
        int unsubscriptionCount = source.Split('\n').Count(line => line.Contains("SettingChanged -="));

        Assert.That(unsubscriptionCount, Is.GreaterThanOrEqualTo(subscriptionCount), "Every SettingChanged += must have a matching -=");
    }

    /// <summary>
    /// LocationData subscribes to switch OnDoorStateChanged events.
    /// OnDestroy must unsubscribe from all of them.
    /// </summary>
    [Test]
    public void LocationData_OnDestroy_UnsubscribesFromSwitchEvents()
    {
        string source = File.ReadAllText(Path.Combine(ClientSrcDir, "Components", "LocationData.cs"));

        // Must iterate switches and unsubscribe
        Assert.That(
            source,
            Does.Contain("OnDoorStateChanged -= reportSwitchChange"),
            "OnDestroy must unsubscribe from switch OnDoorStateChanged"
        );
    }

    /// <summary>
    /// Verify that BotMonitorController's OnDestroy propagates to all monitors.
    /// </summary>
    [Test]
    public void BotMonitorController_OnDestroy_PropagatesCleanup()
    {
        string source = File.ReadAllText(Path.Combine(ClientSrcDir, "BotLogic", "BotMonitor", "BotMonitorController.cs"));

        // OnDestroy should call OnDestroy on all registered monitors
        Assert.That(
            source,
            Does.Contain("monitor.OnDestroy()").Or.Contain("monitor => monitor.OnDestroy()"),
            "BotMonitorController.OnDestroy must propagate to all monitors"
        );
    }

    /// <summary>
    /// LoggingController's dedicated StreamWriter must be properly disposed.
    /// </summary>
    [Test]
    public void LoggingController_StreamWriter_IsDisposed()
    {
        string source = File.ReadAllText(Path.Combine(ClientSrcDir, "Controllers", "LoggingController.cs"));

        Assert.That(source, Does.Contain("_fileWriter.Dispose()"), "StreamWriter must be Dispose'd");
        Assert.That(source, Does.Contain("_fileWriter.Flush()"), "StreamWriter must be Flush'd before Dispose");
        Assert.That(source, Does.Contain("_fileWriter = null"), "StreamWriter reference must be nulled after Dispose");
    }

    /// <summary>
    /// BotHearingMonitor subscribes to OnSoundPlayed and must clean up in OnDestroy.
    /// </summary>
    [Test]
    public void BotHearingMonitor_OnDestroy_RemovesSoundPlayedEvent()
    {
        string source = File.ReadAllText(Path.Combine(ClientSrcDir, "BotLogic", "BotMonitor", "Monitors", "BotHearingMonitor.cs"));

        Assert.That(source, Does.Contain("override void OnDestroy()"), "BotHearingMonitor must override OnDestroy");
        Assert.That(source, Does.Contain("removeSoundPlayedEvent"), "OnDestroy must call removeSoundPlayedEvent");
    }
}
