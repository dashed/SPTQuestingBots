using System.Reflection;
using NSubstitute;
using NUnit.Framework;
using SPTarkov.Server.Core.Models.Utils;
using SPTQuestingBots.Server.Configuration;
using SPTQuestingBots.Server.Services;

namespace SPTQuestingBots.Server.Tests.Services;

[TestFixture]
public class CommonUtilsTests
{
    private ISptLogger<CommonUtils> _logger = null!;
    private QuestingBotsConfigLoader _configLoader = null!;
    private QuestingBotsConfig _config = null!;

    [SetUp]
    public void SetUp()
    {
        _logger = Substitute.For<ISptLogger<CommonUtils>>();
        _config = new QuestingBotsConfig { Enabled = true };
        _configLoader = CreateConfigLoader(_config);
    }

    // ── LogInfo gating ───────────────────────────────────────────────

    [Test]
    public void LogInfo_WhenEnabled_CallsLogger()
    {
        var utils = CreateCommonUtils();
        utils.LogInfo("test message");

        _logger.Received(1).Info(Arg.Is<string>(s => s.Contains("test message")));
    }

    [Test]
    public void LogInfo_WhenDisabled_DoesNotCallLogger()
    {
        _config.Enabled = false;
        var utils = CreateCommonUtils();
        utils.LogInfo("test message");

        _logger.DidNotReceive().Info(Arg.Any<string>(), Arg.Any<Exception?>());
    }

    [Test]
    public void LogInfo_WhenDisabledButAlwaysShow_CallsLogger()
    {
        _config.Enabled = false;
        var utils = CreateCommonUtils();
        utils.LogInfo("test message", alwaysShow: true);

        _logger.Received(1).Info(Arg.Is<string>(s => s.Contains("test message")));
    }

    // ── LogDebug gating ──────────────────────────────────────────────

    [Test]
    public void LogDebug_WhenEnabled_CallsLogger()
    {
        var utils = CreateCommonUtils();
        utils.LogDebug("debug msg");

        _logger.Received(1).Debug(Arg.Is<string>(s => s.Contains("debug msg")));
    }

    [Test]
    public void LogDebug_WhenDisabled_DoesNotCallLogger()
    {
        _config.Enabled = false;
        var utils = CreateCommonUtils();
        utils.LogDebug("debug msg");

        _logger.DidNotReceive().Debug(Arg.Any<string>(), Arg.Any<Exception?>());
    }

    [Test]
    public void LogDebug_WhenDisabledButAlwaysShow_CallsLogger()
    {
        _config.Enabled = false;
        var utils = CreateCommonUtils();
        utils.LogDebug("debug msg", alwaysShow: true);

        _logger.Received(1).Debug(Arg.Is<string>(s => s.Contains("debug msg")));
    }

    // ── LogWarning/LogError always shown ─────────────────────────────

    [Test]
    public void LogWarning_WhenDisabled_StillCallsLogger()
    {
        _config.Enabled = false;
        var utils = CreateCommonUtils();
        utils.LogWarning("warning msg");

        _logger.Received(1).Warning(Arg.Is<string>(s => s.Contains("warning msg")));
    }

    [Test]
    public void LogError_WhenDisabled_StillCallsLogger()
    {
        _config.Enabled = false;
        var utils = CreateCommonUtils();
        utils.LogError("error msg");

        _logger.Received(1).Error(Arg.Is<string>(s => s.Contains("error msg")));
    }

    // ── Prefix applied ───────────────────────────────────────────────

    [Test]
    public void LogInfo_PrependsPrefix()
    {
        var utils = CreateCommonUtils();
        utils.LogInfo("hello");

        _logger.Received(1).Info(Arg.Is<string>(s => s.StartsWith("[Questing Bots] ")));
    }

    [Test]
    public void LogError_PrependsPrefix()
    {
        _config.Enabled = false; // Error still shows
        var utils = CreateCommonUtils();
        utils.LogError("oops");

        _logger.Received(1).Error("[Questing Bots] oops");
    }

    [Test]
    public void LogDebug_PrependsPrefix()
    {
        var utils = CreateCommonUtils();
        utils.LogDebug("details");

        _logger.Received(1).Debug("[Questing Bots] details");
    }

    [Test]
    public void LogWarning_PrependsPrefix()
    {
        var utils = CreateCommonUtils();
        utils.LogWarning("careful");

        _logger.Received(1).Warning("[Questing Bots] careful");
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private CommonUtils CreateCommonUtils()
    {
        return new CommonUtils(_logger, null!, null!, _configLoader);
    }

    private static QuestingBotsConfigLoader CreateConfigLoader(QuestingBotsConfig config)
    {
        var loader = new QuestingBotsConfigLoader(Substitute.For<ISptLogger<QuestingBotsConfigLoader>>());
        var field = typeof(QuestingBotsConfigLoader).GetField("_config", BindingFlags.NonPublic | BindingFlags.Instance);
        field!.SetValue(loader, config);
        return loader;
    }
}
