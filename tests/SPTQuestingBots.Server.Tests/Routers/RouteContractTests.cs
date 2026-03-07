using NUnit.Framework;

namespace SPTQuestingBots.Server.Tests.Routers;

[TestFixture]
public class RouteContractTests
{
    [Test]
    public void ServerRouters_DoNotExposeRemovedAdjustPScavChanceEndpoint()
    {
        var routerDirectory = Path.Combine(GetRepoRoot(), "src", "SPTQuestingBots.Server", "Routers");
        var routerSources = Directory.GetFiles(routerDirectory, "*.cs", SearchOption.TopDirectoryOnly).Select(File.ReadAllText);

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(Path.Combine(routerDirectory, "QuestingBotsDynamicRouter.cs")), Is.False);
            Assert.That(
                routerSources.Any(source => source.Contains("/QuestingBots/AdjustPScavChance", StringComparison.Ordinal)),
                Is.False
            );
        });
    }

    private static string GetRepoRoot()
    {
        return Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));
    }
}
