using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace SPTQuestingBots.Client.Tests.ResourceLifecycle;

/// <summary>
/// Source-scanning tests that verify event subscriptions (+= on delegates/events)
/// have corresponding unsubscriptions (-=) to prevent memory leaks and ghost callbacks.
///
/// Bug 1 (Round 16): DebugData.Awake subscribed anonymous lambdas to SettingChanged
///   but never unsubscribed. Each new raid created a new DebugData MonoBehaviour that
///   added MORE lambdas to the BepInEx ConfigEntry. Old lambdas referenced destroyed
///   DebugData instances, causing ghost callbacks and accumulating delegate chains.
///   Fix: Store delegates as named fields, unsubscribe in OnDestroy.
/// </summary>
[TestFixture]
public class EventSubscriptionTests
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
    public void DebugData_StoresSettingChangedHandlersAsFields()
    {
        string source = File.ReadAllText(Path.Combine(ClientSrcDir, "Components", "DebugData.cs"));

        // The named delegate fields must exist so they can be unsubscribed
        Assert.That(source, Does.Contain("_fontSizeChangedHandler"), "Must store font size handler as a named field");
        Assert.That(source, Does.Contain("_botFilterChangedHandler"), "Must store bot filter handler as a named field");
    }

    [Test]
    public void DebugData_UnsubscribesInOnDestroy()
    {
        string source = File.ReadAllText(Path.Combine(ClientSrcDir, "Components", "DebugData.cs"));

        Assert.That(source, Does.Contain("OnDestroy"), "DebugData must have OnDestroy");
        Assert.That(source, Does.Contain("SettingChanged -= _fontSizeChangedHandler"), "Must unsubscribe font size handler in OnDestroy");
        Assert.That(source, Does.Contain("SettingChanged -= _botFilterChangedHandler"), "Must unsubscribe bot filter handler in OnDestroy");
    }

    [Test]
    public void DebugData_DoesNotUseAnonymousLambdasForSettingChanged()
    {
        string source = File.ReadAllText(Path.Combine(ClientSrcDir, "Components", "DebugData.cs"));

        // Count lines that subscribe to SettingChanged with anonymous lambdas directly
        // (i.e. "SettingChanged += (..." without first assigning to a named field)
        var lines = source.Split('\n');
        int anonymousSubscriptions = 0;
        foreach (var line in lines)
        {
            string trimmed = line.Trim();
            // Matches: SettingChanged += ( ... => or SettingChanged += delegate
            if (
                trimmed.Contains("SettingChanged +=")
                && (trimmed.Contains("(object") || trimmed.Contains("delegate"))
                && !trimmed.Contains("Handler")
            )
            {
                anonymousSubscriptions++;
            }
        }

        Assert.That(
            anonymousSubscriptions,
            Is.EqualTo(0),
            "SettingChanged must not use anonymous lambdas directly (use named handler fields instead)"
        );
    }

    [Test]
    public void GrenadeExplosionSubscriber_HasMatchingUnsubscribe()
    {
        string source = File.ReadAllText(Path.Combine(ClientSrcDir, "Helpers", "GrenadeExplosionSubscriber.cs"));

        Assert.That(source, Does.Contain("OnGrenadeExplosive +="), "Must subscribe to OnGrenadeExplosive");
        Assert.That(source, Does.Contain("OnGrenadeExplosive -="), "Must unsubscribe from OnGrenadeExplosive");
    }

    [Test]
    public void BotHearingMonitor_HasMatchingUnsubscribeForOnSoundPlayed()
    {
        string source = File.ReadAllText(Path.Combine(ClientSrcDir, "BotLogic", "BotMonitor", "Monitors", "BotHearingMonitor.cs"));

        Assert.That(source, Does.Contain("OnSoundPlayed +="), "Must subscribe to OnSoundPlayed");
        Assert.That(source, Does.Contain("OnSoundPlayed -="), "Must unsubscribe from OnSoundPlayed");
    }

    [Test]
    public void LightkeeperIslandMonitor_HasMatchingUnsubscribe()
    {
        string source = File.ReadAllText(Path.Combine(ClientSrcDir, "Components", "LightkeeperIslandMonitor.cs"));

        Assert.That(source, Does.Contain("OnPlayerAllowStatusChanged +="), "Must subscribe");
        Assert.That(source, Does.Contain("OnPlayerAllowStatusChanged -="), "Must unsubscribe");
        Assert.That(source, Does.Contain("OnDestroy"), "Must have OnDestroy for cleanup");
    }

    [Test]
    public void LocationData_HasMatchingUnsubscribeForSwitchEvents()
    {
        string source = File.ReadAllText(Path.Combine(ClientSrcDir, "Components", "LocationData.cs"));

        Assert.That(source, Does.Contain("OnDoorStateChanged +="), "Must subscribe to switch events");
        Assert.That(source, Does.Contain("OnDoorStateChanged -="), "Must unsubscribe from switch events");
        Assert.That(source, Does.Contain("OnDestroy"), "Must have OnDestroy for cleanup");
    }
}
