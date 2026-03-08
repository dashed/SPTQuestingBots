using NUnit.Framework;

namespace SPTQuestingBots.Client.Tests.Components;

/// <summary>
/// Tests for the quest completion dedup guard in BotObjectiveManager.CompleteObjective().
///
/// Bug: CompleteObjective() had no guard against null assignment or already-completed
/// assignments. Multiple callers (GoToObjectiveAction, ToggleSwitchAction, BotObjectiveLayer,
/// CloseNearbyDoorsAction, CustomLogicDelayedUpdate) could trigger duplicate completions,
/// causing double telemetry events and repeated loot behavior updates.
///
/// Fix: Added early-return guard: if (assignment == null || assignment.IsCompletedOrArchived) return;
///
/// Since BotObjectiveManager is a MonoBehaviour and BotJobAssignment requires BotOwner,
/// these tests verify the dedup guard logic pattern using a lightweight test double that
/// mirrors the JobAssignmentStatus + IsCompletedOrArchived semantics.
/// </summary>
[TestFixture]
public class CompleteObjectiveDedupTests
{
    // Mirror of the real JobAssignmentStatus enum for testing guard logic
    private enum TestStatus
    {
        NotStarted,
        Pending,
        Active,
        Completed,
        Failed,
        Archived,
    }

    /// <summary>
    /// Mirrors BotJobAssignment.IsCompletedOrArchived logic.
    /// </summary>
    private static bool IsCompletedOrArchived(TestStatus status) => status == TestStatus.Completed || status == TestStatus.Archived;

    /// <summary>
    /// Simulates the dedup guard: should return early when assignment is null or completed/archived.
    /// This mirrors: if (assignment == null || assignment.IsCompletedOrArchived) return;
    /// </summary>
    private static bool ShouldGuardBlock(TestStatus? status) => status == null || IsCompletedOrArchived(status.Value);

    private int _completionCount;

    /// <summary>
    /// Simulates CompleteObjective with the dedup guard applied.
    /// </summary>
    private void SimulateCompleteObjective(TestStatus? assignmentStatus)
    {
        if (assignmentStatus == null || IsCompletedOrArchived(assignmentStatus.Value))
        {
            return;
        }

        _completionCount++;
    }

    [SetUp]
    public void SetUp()
    {
        _completionCount = 0;
    }

    // ── Null assignment guard ───────────────────────────────────────

    [Test]
    public void CompleteObjective_NullAssignment_ReturnsEarly()
    {
        Assert.That(ShouldGuardBlock(null), Is.True);
    }

    [Test]
    public void CompleteObjective_NullAssignment_NoSideEffects()
    {
        SimulateCompleteObjective(null);
        Assert.That(_completionCount, Is.EqualTo(0), "No completion should occur for null assignment");
    }

    // ── Already-completed guard ─────────────────────────────────────

    [Test]
    public void CompleteObjective_CompletedAssignment_ReturnsEarly()
    {
        Assert.That(ShouldGuardBlock(TestStatus.Completed), Is.True);
    }

    [Test]
    public void CompleteObjective_ArchivedAssignment_ReturnsEarly()
    {
        Assert.That(ShouldGuardBlock(TestStatus.Archived), Is.True);
    }

    // ── Active assignments pass through ─────────────────────────────

    [Test]
    public void CompleteObjective_ActiveAssignment_Proceeds()
    {
        Assert.That(ShouldGuardBlock(TestStatus.Active), Is.False);
    }

    [Test]
    public void CompleteObjective_PendingAssignment_Proceeds()
    {
        Assert.That(ShouldGuardBlock(TestStatus.Pending), Is.False);
    }

    [Test]
    public void CompleteObjective_NotStartedAssignment_Proceeds()
    {
        Assert.That(ShouldGuardBlock(TestStatus.NotStarted), Is.False);
    }

    [Test]
    public void CompleteObjective_FailedAssignment_Proceeds()
    {
        Assert.That(ShouldGuardBlock(TestStatus.Failed), Is.False);
    }

    // ── Duplicate completion prevention ─────────────────────────────

    [Test]
    public void CompleteObjective_CalledTwice_OnlyFirstCompletes()
    {
        var status = TestStatus.Active;

        // First call: Active → guard allows through
        SimulateCompleteObjective(status);
        Assert.That(_completionCount, Is.EqualTo(1), "First call should complete");

        // After completion, status transitions to Completed
        status = TestStatus.Completed;

        // Second call: Completed → guard blocks
        SimulateCompleteObjective(status);
        Assert.That(_completionCount, Is.EqualTo(1), "Second call should be blocked");
    }

    [Test]
    public void CompleteObjective_CalledThreeTimes_OnlyFirstCompletes()
    {
        var status = TestStatus.Active;

        SimulateCompleteObjective(status);
        status = TestStatus.Completed;

        SimulateCompleteObjective(status);
        SimulateCompleteObjective(status);

        Assert.That(_completionCount, Is.EqualTo(1), "Only first call should produce a completion");
    }

    [Test]
    public void CompleteObjective_CalledAfterArchive_DoesNotComplete()
    {
        var status = TestStatus.Active;
        SimulateCompleteObjective(status);
        Assert.That(_completionCount, Is.EqualTo(1));

        // Status transitions to Archived (e.g., after quest cleanup)
        status = TestStatus.Archived;
        SimulateCompleteObjective(status);
        Assert.That(_completionCount, Is.EqualTo(1), "Archived assignment should not re-complete");
    }

    // ── Comprehensive status coverage ───────────────────────────────

    [TestCase(0, false)] // NotStarted
    [TestCase(1, false)] // Pending
    [TestCase(2, false)] // Active
    [TestCase(3, true)] // Completed
    [TestCase(4, false)] // Failed
    [TestCase(5, true)] // Archived
    public void DedupGuard_AllStatuses_OnlyCompletedAndArchivedBlock(int statusInt, bool expectedBlocked)
    {
        var status = (TestStatus)statusInt;
        Assert.That(ShouldGuardBlock(status), Is.EqualTo(expectedBlocked));
    }
}
