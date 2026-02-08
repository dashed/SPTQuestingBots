using NUnit.Framework;

namespace SPTQuestingBots.Client.Tests;

/// <summary>
/// Documents the testability constraints of the QuestingBots client plugin.
///
/// <para>
/// The client plugin (SPTQuestingBots.Client) targets <c>netstandard2.1</c> and
/// depends heavily on Unity, EFT (Assembly-CSharp), BepInEx, and SPT assemblies.
/// These game DLLs are not available in the CI/test environment, so the test
/// project cannot directly reference the client project.
/// </para>
///
/// <para>
/// <b>What CAN be tested:</b>
/// </para>
/// <list type="bullet">
///   <item>Configuration POCO classes (if linked/copied as source files)</item>
///   <item>Pure utility functions with no game dependencies</item>
///   <item>Data model serialization/deserialization</item>
/// </list>
///
/// <para>
/// <b>What CANNOT be tested without game assemblies:</b>
/// </para>
/// <list type="bullet">
///   <item>Harmony patches (require Assembly-CSharp types)</item>
///   <item>BotLogic (requires BotOwner, CustomLayer, CustomLogic from BigBrain)</item>
///   <item>Components (require MonoBehaviour, Unity engine)</item>
///   <item>Controllers (require SPT HTTP client, game state)</item>
///   <item>Helpers (require BotOwner, Player, NavMesh, etc.)</item>
/// </list>
///
/// <para>
/// <b>Testing strategy:</b> The server-side tests
/// (<see cref="SPTQuestingBots.Server.Tests"/>) provide comprehensive coverage
/// of the shared configuration model and validation logic. Client-specific
/// testing should be done via integration tests with actual game assemblies
/// (see Task #19).
/// </para>
/// </summary>
[TestFixture]
public class ClientTestabilityTests
{
    [Test]
    public void TestInfrastructureWorks()
    {
        // Verify the test project builds and NUnit runs correctly
        Assert.That(true, Is.True);
    }

    [Test]
    public void TestProjectCanResolveNUnit()
    {
        Assert.That(typeof(TestFixtureAttribute).Assembly.GetName().Name, Is.EqualTo("nunit.framework"));
    }

    [Test]
    public void TestProjectCanResolveNSubstitute()
    {
        Assert.That(typeof(NSubstitute.Substitute).Assembly.GetName().Name, Is.EqualTo("NSubstitute"));
    }
}
