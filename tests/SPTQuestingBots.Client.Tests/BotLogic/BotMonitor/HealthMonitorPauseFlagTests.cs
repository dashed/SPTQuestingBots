using NUnit.Framework;

namespace SPTQuestingBots.Client.Tests.BotLogic.BotMonitor;

/// <summary>
/// Tests for BotHealthMonitor.PauseReason flag-based pause tracking.
/// Bug: BotCombatMonitor and BotHearingMonitor both independently called
/// PauseHealthMonitoring/ResumeHealthMonitoring. When a bot was suspicious
/// but NOT in combat, the hearing monitor paused monitoring, then the combat
/// monitor immediately resumed it — overriding the pause.
/// Fix: Use [Flags] enum so each caller sets/clears its own reason flag.
/// Monitoring only resumes when ALL flags are cleared.
///
/// This test mirrors the PauseReason enum from BotHealthMonitor to validate
/// the flag logic without requiring game assembly dependencies.
/// </summary>
[TestFixture]
public class HealthMonitorPauseFlagTests
{
    /// <summary>
    /// Mirror of BotHealthMonitor.PauseReason for testing the flag pattern.
    /// Must match the values in the production code.
    /// </summary>
    [System.Flags]
    private enum PauseReason
    {
        None = 0,
        Combat = 1 << 0,
        Suspicious = 1 << 1,
    }

    /// <summary>
    /// Simulates the production IsMonitoringPaused computed property.
    /// </summary>
    private static bool IsPaused(PauseReason flags) => flags != PauseReason.None;

    [Test]
    public void PauseReason_None_MonitoringIsNotPaused()
    {
        var flags = PauseReason.None;
        Assert.That(IsPaused(flags), Is.False);
    }

    [Test]
    public void PauseReason_Combat_HasCombatFlag()
    {
        var flags = PauseReason.Combat;
        Assert.That(flags.HasFlag(PauseReason.Combat), Is.True);
        Assert.That(flags.HasFlag(PauseReason.Suspicious), Is.False);
        Assert.That(IsPaused(flags), Is.True);
    }

    [Test]
    public void PauseReason_Suspicious_HasSuspiciousFlag()
    {
        var flags = PauseReason.Suspicious;
        Assert.That(flags.HasFlag(PauseReason.Suspicious), Is.True);
        Assert.That(flags.HasFlag(PauseReason.Combat), Is.False);
        Assert.That(IsPaused(flags), Is.True);
    }

    [Test]
    public void PauseReason_CombatAndSuspicious_HasBothFlags()
    {
        var flags = PauseReason.Combat | PauseReason.Suspicious;
        Assert.That(flags.HasFlag(PauseReason.Combat), Is.True);
        Assert.That(flags.HasFlag(PauseReason.Suspicious), Is.True);
        Assert.That(IsPaused(flags), Is.True);
    }

    [Test]
    public void PauseReason_ClearCombat_StillPausedBySuspicious()
    {
        // Simulate: hearing monitor pauses (Suspicious), combat monitor pauses (Combat),
        // then combat monitor resumes (clears Combat). Suspicious flag should remain.
        var flags = PauseReason.Suspicious | PauseReason.Combat;

        // Combat monitor resumes — mirrors ResumeHealthMonitoring(PauseReason.Combat)
        flags &= ~PauseReason.Combat;

        Assert.That(IsPaused(flags), Is.True, "Should still be paused: Suspicious flag remains");
        Assert.That(flags.HasFlag(PauseReason.Suspicious), Is.True);
        Assert.That(flags.HasFlag(PauseReason.Combat), Is.False);
    }

    [Test]
    public void PauseReason_ClearSuspicious_StillPausedByCombat()
    {
        var flags = PauseReason.Suspicious | PauseReason.Combat;

        // Hearing monitor resumes — mirrors ResumeHealthMonitoring(PauseReason.Suspicious)
        flags &= ~PauseReason.Suspicious;

        Assert.That(IsPaused(flags), Is.True, "Should still be paused: Combat flag remains");
        Assert.That(flags.HasFlag(PauseReason.Combat), Is.True);
        Assert.That(flags.HasFlag(PauseReason.Suspicious), Is.False);
    }

    [Test]
    public void PauseReason_ClearBoth_MonitoringResumed()
    {
        var flags = PauseReason.Suspicious | PauseReason.Combat;

        flags &= ~PauseReason.Combat;
        flags &= ~PauseReason.Suspicious;

        Assert.That(IsPaused(flags), Is.False, "All flags cleared: monitoring should resume");
        Assert.That(flags, Is.EqualTo(PauseReason.None));
    }

    /// <summary>
    /// Regression test: the exact scenario that caused the bug.
    /// Hearing monitor pauses (Suspicious), then combat monitor clears
    /// its own flag — monitoring should REMAIN paused.
    /// </summary>
    [Test]
    public void SuspiciousButNotInCombat_MonitoringStaysPaused()
    {
        var flags = PauseReason.None;

        // Step 1: Hearing monitor detects something suspicious
        // mirrors PauseHealthMonitoring(PauseReason.Suspicious)
        flags |= PauseReason.Suspicious;
        Assert.That(IsPaused(flags), Is.True, "Should be paused after hearing pause");

        // Step 2: Combat monitor updates — bot is NOT in combat, so it clears Combat flag
        // mirrors ResumeHealthMonitoring(PauseReason.Combat)
        flags &= ~PauseReason.Combat;

        // Before the fix, this would have set IsMonitoringPaused = false.
        // With flags, monitoring should STILL be paused because Suspicious is set.
        Assert.That(IsPaused(flags), Is.True, "Should remain paused: Suspicious flag still set");
        Assert.That(flags.HasFlag(PauseReason.Suspicious), Is.True);
    }

    /// <summary>
    /// Verify the reverse scenario: combat pauses, hearing resumes its flag,
    /// monitoring stays paused because combat is still active.
    /// </summary>
    [Test]
    public void InCombatButNotSuspicious_MonitoringStaysPaused()
    {
        var flags = PauseReason.None;

        // Step 1: Combat monitor detects combat
        flags |= PauseReason.Combat;

        // Step 2: Hearing monitor: bot is NOT suspicious, clears Suspicious flag
        flags &= ~PauseReason.Suspicious;

        Assert.That(IsPaused(flags), Is.True, "Should remain paused: Combat flag still set");
    }

    /// <summary>
    /// Double-set of the same flag should be idempotent.
    /// </summary>
    [Test]
    public void DoublePause_SameReason_IsIdempotent()
    {
        var flags = PauseReason.None;

        flags |= PauseReason.Combat;
        flags |= PauseReason.Combat;

        Assert.That(flags, Is.EqualTo(PauseReason.Combat));

        // Single clear should fully clear
        flags &= ~PauseReason.Combat;
        Assert.That(flags, Is.EqualTo(PauseReason.None));
        Assert.That(IsPaused(flags), Is.False);
    }
}
