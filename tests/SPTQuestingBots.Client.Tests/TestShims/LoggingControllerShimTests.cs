using NUnit.Framework;
using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.Client.Tests.TestShims
{
    /// <summary>
    /// Verifies that the LoggingController test shim is a proper no-op.
    /// This ensures linked source files that call LoggingController methods
    /// won't throw during tests.
    /// </summary>
    [TestFixture]
    public class LoggingControllerShimTests
    {
        [Test]
        public void LogDebug_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => LoggingController.LogDebug("test debug message"));
        }

        [Test]
        public void LogInfo_NoParams_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => LoggingController.LogInfo("test info message"));
        }

        [Test]
        public void LogInfo_AlwaysShow_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => LoggingController.LogInfo("test info message", true));
            Assert.DoesNotThrow(() => LoggingController.LogInfo("test info message", false));
        }

        [Test]
        public void LogWarning_NoParams_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => LoggingController.LogWarning("test warning"));
        }

        [Test]
        public void LogWarning_OnlyForDebug_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => LoggingController.LogWarning("test warning", true));
            Assert.DoesNotThrow(() => LoggingController.LogWarning("test warning", false));
        }

        [Test]
        public void LogError_NoParams_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => LoggingController.LogError("test error"));
        }

        [Test]
        public void LogError_OnlyForDebug_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => LoggingController.LogError("test error", true));
            Assert.DoesNotThrow(() => LoggingController.LogError("test error", false));
        }

        [Test]
        public void LogDebug_NullMessage_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => LoggingController.LogDebug(null));
        }

        [Test]
        public void LogInfo_NullMessage_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => LoggingController.LogInfo(null));
        }

        [Test]
        public void LogWarning_NullMessage_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => LoggingController.LogWarning(null));
        }

        [Test]
        public void LogError_NullMessage_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => LoggingController.LogError(null));
        }

        [Test]
        public void AllMethods_EmptyString_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                LoggingController.LogDebug("");
                LoggingController.LogInfo("");
                LoggingController.LogWarning("");
                LoggingController.LogError("");
            });
        }

        [Test]
        public void RapidCalls_DoNotThrow()
        {
            // Simulate hot-path logging that would happen per-tick
            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    LoggingController.LogDebug("tick " + i);
                }
            });
        }
    }
}
