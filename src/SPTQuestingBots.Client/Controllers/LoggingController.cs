using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using EFT;
using Newtonsoft.Json;
using UnityEngine;

namespace SPTQuestingBots.Controllers
{
    /// <summary>
    /// Centralized logging controller for QuestingBots. Provides:
    /// <list type="bullet">
    ///   <item>BepInEx <see cref="BepInEx.Logging.ManualLogSource"/> integration (shared LogOutput.log)</item>
    ///   <item>Dedicated per-mod log file at <c>BepInEx/plugins/DanW-SPTQuestingBots/log/QuestingBots.log</c></item>
    ///   <item>Dual-destination logging to both client and SPT server console</item>
    ///   <item>Frame-stamped log lines (<c>F{frameCount}</c>) for timing correlation (inspired by Phobos)</item>
    ///   <item>Debug gating via <c>Config.Debug.Enabled</c> to suppress verbose output in production</item>
    /// </list>
    /// </summary>
    public static class LoggingController
    {
        public static BepInEx.Logging.ManualLogSource Logger { get; set; } = null;

        private static StreamWriter _fileWriter;
        private static readonly object _fileLock = new object();
        private static bool _fileLoggingEnabled;
        private static string _logFilePath;

        public static string GetText(this IEnumerable<Player> players) => string.Join(",", players.Select(b => b?.GetText()));

        public static string GetText(this IEnumerable<IPlayer> players) => string.Join(",", players.Select(b => b?.GetText()));

        public static string GetText(this IEnumerable<BotOwner> bots) => string.Join(",", bots.Select(b => b?.GetText()));

        public static string GetText(this BotOwner bot)
        {
            if (bot == null)
            {
                return "[NULL BOT]";
            }

            return bot.GetPlayer.GetText();
        }

        public static string GetText(this Player player)
        {
            if (player == null)
            {
                return "[NULL BOT]";
            }

            return player.Profile.Nickname + " (Name: " + player.name + ", Level: " + player.Profile.Info.Level.ToString() + ")";
        }

        public static string GetText(this IPlayer player)
        {
            if (player == null)
            {
                return "[NULL BOT]";
            }

            return player.Profile.Nickname + " (Name: ???, Level: " + player.Profile.Info.Level.ToString() + ")";
        }

        public static string Abbreviate(this string fullID, int startChars = 5, int endChars = 5)
        {
            if (fullID.Length <= startChars + endChars + 3)
            {
                return fullID;
            }

            return fullID.Substring(0, startChars) + "..." + fullID.Substring(fullID.Length - endChars, endChars);
        }

        // ── Dedicated File Logger ────────────────────────────────────────

        /// <summary>
        /// Initializes the dedicated log file at
        /// <c>BepInEx/plugins/DanW-SPTQuestingBots/log/QuestingBots.log</c>.
        /// Truncates any existing file so each game session starts fresh.
        /// Call once from <see cref="QuestingBotsPlugin.Awake"/> after config is loaded.
        /// </summary>
        public static void InitFileLogger()
        {
            try
            {
                if (ConfigController.Config == null || !ConfigController.Config.Debug.DedicatedLogFile)
                {
                    _fileLoggingEnabled = false;
                    return;
                }

                string logDir = AppDomain.CurrentDomain.BaseDirectory + ConfigController.ModPathRelative + "/log/";
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                _logFilePath = logDir + "QuestingBots.log";
                _fileWriter = new StreamWriter(_logFilePath, append: false, encoding: Encoding.UTF8) { AutoFlush = true };
                _fileLoggingEnabled = true;

                WriteToFile("INFO", "=== QuestingBots dedicated log started ===");
                Logger?.LogInfo("[LoggingController] Dedicated log file initialized: " + _logFilePath);
            }
            catch (Exception e)
            {
                _fileLoggingEnabled = false;
                Logger?.LogWarning(
                    "[LoggingController] Failed to initialize dedicated log file, continuing without file logging: " + e.Message
                );
            }
        }

        /// <summary>
        /// Flushes and closes the dedicated log file. Call from raid-end cleanup
        /// (e.g. <c>BotsControllerStopPatch</c>).
        /// </summary>
        public static void DisposeFileLogger()
        {
            lock (_fileLock)
            {
                if (_fileWriter != null)
                {
                    try
                    {
                        WriteToFile("INFO", "=== QuestingBots dedicated log ended ===");
                        _fileWriter.Flush();
                        _fileWriter.Close();
                        _fileWriter.Dispose();
                    }
                    catch
                    {
                        // Swallow — we're shutting down
                    }
                    finally
                    {
                        _fileWriter = null;
                        _fileLoggingEnabled = false;
                    }
                }
            }
        }

        /// <summary>
        /// Writes a formatted line to the dedicated log file. Thread-safe via lock.
        /// Format: <c>[yyyy-MM-dd HH:mm:ss.fff] [LEVEL] F{frame}: message</c>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteToFile(string level, string message)
        {
            if (!_fileLoggingEnabled || _fileWriter == null)
            {
                return;
            }

            try
            {
                int frame = Time.frameCount;
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                lock (_fileLock)
                {
                    _fileWriter.Write('[');
                    _fileWriter.Write(timestamp);
                    _fileWriter.Write("] [");
                    _fileWriter.Write(level);
                    _fileWriter.Write("] F");
                    _fileWriter.Write(frame);
                    _fileWriter.Write(": ");
                    _fileWriter.WriteLine(message);
                }
            }
            catch
            {
                // Swallow file I/O errors to avoid disrupting gameplay
            }
        }

        // ── Core Log Methods ─────────────────────────────────────────────

        public static void LogDebug(string message)
        {
            if (!ConfigController.Config.Debug.Enabled)
            {
                return;
            }

            Logger.LogDebug(message);
            WriteToFile("DEBUG", message);
        }

        public static void LogInfo(string message, bool alwaysShow = false)
        {
            if (!alwaysShow && !ConfigController.Config.Debug.Enabled)
            {
                return;
            }

            Logger.LogInfo(message);
            WriteToFile("INFO", message);
        }

        public static void LogWarning(string message, bool onlyForDebug = false)
        {
            if (onlyForDebug && !ConfigController.Config.Debug.Enabled)
            {
                return;
            }

            Logger.LogWarning(message);
            WriteToFile("WARN", message);
        }

        public static void LogError(string message, bool onlyForDebug = false)
        {
            if (onlyForDebug && !ConfigController.Config.Debug.Enabled)
            {
                return;
            }

            Logger.LogError(message);
            WriteToFile("ERROR", message);
        }

        public static void LogInfoToServerConsole(string message)
        {
            LogInfo(message);
            ConfigController.ReportInfoToServer(message);
        }

        public static void LogWarningToServerConsole(string message)
        {
            LogWarning(message);
            ConfigController.ReportWarningToServer(message);
        }

        public static void LogErrorToServerConsole(string message)
        {
            LogError(message);
            ConfigController.ReportErrorToServer(message);
        }

        public static void CreateLogFile(string logName, string filename, string content)
        {
            try
            {
                if (!Directory.Exists(ConfigController.LoggingPath))
                {
                    Directory.CreateDirectory(ConfigController.LoggingPath);
                }

                File.WriteAllText(filename, content);

                LogDebug("Writing " + logName + " log file...done.");
            }
            catch (Exception e)
            {
                e.Data.Add("Filename", filename);
                LogError("Writing " + logName + " log file...failed!");
                LogError(e.ToString());
            }
        }

        public static void AppendQuestLocation(string filename, Models.Questing.StoredQuestLocation location)
        {
            try
            {
                string content = JsonConvert.SerializeObject(location, Formatting.Indented);

                if (!Directory.Exists(ConfigController.LoggingPath))
                {
                    Directory.CreateDirectory(ConfigController.LoggingPath);
                }

                if (File.Exists(filename))
                {
                    content = ",\n" + content;
                }

                File.AppendAllText(filename, content);

                LogInfo("Appended custom quest location: " + location.Name + " at " + location.Position.ToString());
            }
            catch (Exception e)
            {
                e.Data.Add("Filename", filename);
                e.Data.Add("LocationName", location.Name);
                LogError("Could not create custom quest location for " + location.Name);
                LogError(e.ToString());
            }
        }
    }
}
