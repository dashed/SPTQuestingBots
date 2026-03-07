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

    [Test]
    public void ServerStartupDiagnostics_RemainExplicitAboutBotGenerateRouteShadowing()
    {
        var repoRoot = GetRepoRoot();
        var routerSource = File.ReadAllText(
            Path.Combine(repoRoot, "src", "SPTQuestingBots.Server", "Routers", "QuestingBotGenerateRouter.cs")
        );
        var pluginSource = File.ReadAllText(Path.Combine(repoRoot, "src", "SPTQuestingBots.Server", "QuestingBotsServerPlugin.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(
                routerSource,
                Does.Contain("/client/game/bot/generate"),
                "QuestingBots must continue to intercept the shared bot-generation route explicitly."
            );
            Assert.That(
                pluginSource,
                Does.Contain("BotGenerateRoute")
                    .And.Contain("/client/game/bot/generate")
                    .And.Contain("order-sensitive")
                    .And.Contain("Detected potentially conflicting server mod"),
                "Server startup diagnostics should keep the bot-generation route conflict warning explicit."
            );
        });
    }

    private static string GetRepoRoot()
    {
        return Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));
    }
}
