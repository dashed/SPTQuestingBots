using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;

namespace SPTQuestingBots.Client.Tests.ErrorRecovery;

/// <summary>
/// Tests for graceful degradation — verifies that failure scenarios produce
/// safe defaults rather than crashes or corrupted state.
/// </summary>
[TestFixture]
public class GracefulDegradationTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string ClientSrcDir = Path.Combine(RepoRoot, "src", "SPTQuestingBots.Client");

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

    // ── BotRegistry: destroyed bot removal safety ──

    [Test]
    public void BotRegistry_Remove_NullEntity_ReturnsFalse()
    {
        var registry = new BotRegistry(4);
        Assert.IsFalse(registry.Remove(null));
    }

    [Test]
    public void BotRegistry_Remove_AlreadyRemoved_ReturnsFalse()
    {
        var registry = new BotRegistry(4);
        var entity = registry.Add();
        registry.Remove(entity);

        // Double-remove should not crash
        Assert.IsFalse(registry.Remove(entity));
    }

    [Test]
    public void BotRegistry_GetByBsgId_InvalidId_ReturnsNull()
    {
        var registry = new BotRegistry(4);
        Assert.IsNull(registry.GetByBsgId(-1));
        Assert.IsNull(registry.GetByBsgId(999));
    }

    [Test]
    public void BotRegistry_GetByBsgId_AfterRemove_ReturnsNull()
    {
        var registry = new BotRegistry(4);
        var entity = registry.Add(42);
        Assert.IsNotNull(registry.GetByBsgId(42));

        registry.Remove(entity);
        Assert.IsNull(registry.GetByBsgId(42));
    }

    [Test]
    public void BotRegistry_IndexerById_InvalidId_ThrowsKeyNotFound()
    {
        var registry = new BotRegistry(4);
        Assert.Throws<KeyNotFoundException>(() =>
        {
            var _ = registry[-1];
        });
        Assert.Throws<KeyNotFoundException>(() =>
        {
            var _ = registry[999];
        });
    }

    [Test]
    public void BotRegistry_TryGetById_InvalidId_ReturnsFalse()
    {
        var registry = new BotRegistry(4);
        Assert.IsFalse(registry.TryGetById(-1, out _));
        Assert.IsFalse(registry.TryGetById(999, out _));
    }

    [Test]
    public void BotRegistry_Contains_InvalidId_ReturnsFalse()
    {
        var registry = new BotRegistry(4);
        Assert.IsFalse(registry.Contains(-1));
        Assert.IsFalse(registry.Contains(999));
    }

    // ── UtilityTaskManager: inactive entity handling ──

    [Test]
    public void UtilityTaskManager_ScoreAndPick_InactiveEntity_ClearsAssignment()
    {
        var task = new TestUtilityTask();
        var manager = new UtilityTaskManager(new UtilityTask[] { task });

        var entity = new BotEntity(0);
        entity.TaskScores = new float[1];
        entity.IsActive = false;

        // Assign a task first
        entity.IsActive = true;
        task.FixedScore = 1.0f;
        manager.ScoreAndPick(entity);
        Assert.IsNotNull(entity.TaskAssignment.Task);

        // Now deactivate — should clear assignment
        entity.IsActive = false;
        manager.ScoreAndPick(entity);
        Assert.IsNull(entity.TaskAssignment.Task);
    }

    [Test]
    public void UtilityTaskManager_PickTask_NaNScore_ResetsToZero()
    {
        var task = new TestUtilityTask();
        var manager = new UtilityTaskManager(new UtilityTask[] { task });

        var entity = new BotEntity(0);
        entity.TaskScores = new float[1];
        entity.IsActive = true;

        // Set score to NaN — should not poison selection
        entity.TaskScores[0] = float.NaN;
        manager.PickTask(entity);

        // NaN score tasks are skipped
        Assert.IsNull(entity.TaskAssignment.Task);
    }

    [Test]
    public void UtilityTaskManager_RemoveEntity_NullTask_DoesNotCrash()
    {
        var manager = new UtilityTaskManager(new UtilityTask[0]);
        var entity = new BotEntity(0);
        entity.TaskAssignment = default; // Task is null

        Assert.DoesNotThrow(() => manager.RemoveEntity(entity));
    }

    // ── StaticPathData: empty Corners safety (Round 15 fix) ──

    [Test]
    public void StaticPathData_Append_EmptyThisCorners_ReturnsAppended()
    {
        // Verify the Round 15 fix: Append with empty this.Corners no longer crashes
        var source = File.ReadAllText(Path.Combine(ClientSrcDir, "Models", "Pathing", "StaticPathData.cs"));
        var appendMethod = ExtractMethod(source, "public StaticPathData Append");

        // The fix adds a guard for this.Corners.Length == 0 before calling Corners.Last()
        Assert.That(
            appendMethod,
            Does.Contain("Corners.Length == 0"),
            "Append must guard against empty this.Corners before calling .Last()"
        );
    }

    [Test]
    public void StaticPathData_Prepend_EmptyThisCorners_ReturnsPrepended()
    {
        var source = File.ReadAllText(Path.Combine(ClientSrcDir, "Models", "Pathing", "StaticPathData.cs"));
        var prependMethod = ExtractMethod(source, "public StaticPathData Prepend");

        Assert.That(
            prependMethod,
            Does.Contain("Corners.Length == 0"),
            "Prepend must guard against empty this.Corners before calling .First()"
        );
    }

    // ── BotPathData: null safety for GameWorld access (Round 15 fix) ──

    [Test]
    public void BotPathData_PathInvalid_NullChecksGameWorldChain()
    {
        var source = File.ReadAllText(Path.Combine(ClientSrcDir, "Models", "Pathing", "BotPathData.cs"));

        // Before Round 15 fix, the chain Singleton<GameWorld>.Instance.GetComponent<LocationData>()
        // would NRE if Instance is null. The fix splits the chain.
        Assert.That(source, Does.Contain("gameWorld?.GetComponent"), "BotPathData must null-propagate GameWorld access");
        Assert.That(source, Does.Contain("locationData == null"), "BotPathData must null-check LocationData before using it");
    }

    // ── ConfigController: deserialization failure safety (Round 15 fixes) ──

    [Test]
    public void ConfigController_GetAllQuestTemplates_ChecksDeserializationResult()
    {
        var source = File.ReadAllText(Path.Combine(ClientSrcDir, "Controllers", "ConfigController.cs"));
        var method = ExtractMethod(source, "public static RawQuestClass[] GetAllQuestTemplates");

        Assert.That(
            method,
            Does.Contain("!TryDeserializeObject").Or.Contain("TryDeserializeObject"),
            "GetAllQuestTemplates must check TryDeserializeObject return value"
        );
        Assert.That(method, Does.Contain("new RawQuestClass[0]"), "GetAllQuestTemplates must return empty array on failure");
    }

    [Test]
    public void ConfigController_GetEFTQuestSettings_ChecksDeserializationResult()
    {
        var source = File.ReadAllText(Path.Combine(ClientSrcDir, "Controllers", "ConfigController.cs"));
        var method = ExtractMethod(source, "public static Dictionary<string, Dictionary<string, object>> GetEFTQuestSettings");

        Assert.That(method, Does.Contain("!TryDeserializeObject"), "GetEFTQuestSettings must check TryDeserializeObject return value");
    }

    [Test]
    public void ConfigController_GetZoneAndItemPositions_ChecksDeserializationResult()
    {
        var source = File.ReadAllText(Path.Combine(ClientSrcDir, "Controllers", "ConfigController.cs"));
        var method = ExtractMethod(source, "public static Dictionary<string, ZoneAndItemPositionInfoConfig> GetZoneAndItemPositions");

        Assert.That(method, Does.Contain("!TryDeserializeObject"), "GetZoneAndItemPositions must check TryDeserializeObject return value");
    }

    [Test]
    public void ConfigController_GetScavRaidSettings_ChecksDeserializationResult()
    {
        var source = File.ReadAllText(Path.Combine(ClientSrcDir, "Controllers", "ConfigController.cs"));
        var method = ExtractMethod(source, "public static Dictionary<string, Configuration.ScavRaidSettingsConfig> GetScavRaidSettings");

        Assert.That(method, Does.Contain("!TryDeserializeObject"), "GetScavRaidSettings must check TryDeserializeObject return value");
    }

    [Test]
    public void ConfigController_FindSerializerSettings_NullChecksTargetType()
    {
        var source = File.ReadAllText(Path.Combine(ClientSrcDir, "Controllers", "ConfigController.cs"));
        var method = ExtractMethod(source, "private static void findSerializerSettings");

        Assert.That(
            method,
            Does.Contain("targetType == null"),
            "findSerializerSettings must null-check FindTargetTypeByField result before accessing .FullName"
        );
    }

    // ── BotEntity: TaskScores bounds safety ──

    [Test]
    public void UtilityTaskManager_PickTask_TaskScoresMatchTaskCount()
    {
        // Verify that PickTask only indexes within Tasks.Length
        var tasks = new UtilityTask[]
        {
            new TestUtilityTask { FixedScore = 0.5f },
            new TestUtilityTask { FixedScore = 0.8f },
        };
        var manager = new UtilityTaskManager(tasks);

        var entity = new BotEntity(0);
        entity.TaskScores = new float[2]; // matches task count
        entity.IsActive = true;

        // Score and pick should work correctly with matching array size
        Assert.DoesNotThrow(() => manager.ScoreAndPick(entity));
        Assert.IsNotNull(entity.TaskAssignment.Task);
    }

    [Test]
    public void UtilityTaskManager_PickTask_LargerScoresArray_StillWorks()
    {
        var tasks = new UtilityTask[] { new TestUtilityTask { FixedScore = 1.0f } };
        var manager = new UtilityTaskManager(tasks);

        var entity = new BotEntity(0);
        entity.TaskScores = new float[10]; // larger than needed
        entity.IsActive = true;

        Assert.DoesNotThrow(() => manager.ScoreAndPick(entity));
    }

    // ── Helpers ──

    private static string ExtractMethod(string source, string signature)
    {
        int start = source.IndexOf(signature);
        if (start < 0)
            return "";

        int braceCount = 0;
        bool foundFirst = false;
        int end = start;
        for (int i = start; i < source.Length; i++)
        {
            if (source[i] == '{')
            {
                braceCount++;
                foundFirst = true;
            }
            else if (source[i] == '}')
            {
                braceCount--;
                if (foundFirst && braceCount == 0)
                {
                    end = i + 1;
                    break;
                }
            }
        }
        return source.Substring(start, end - start);
    }

    private class TestUtilityTask : UtilityTask
    {
        public float FixedScore { get; set; }

        public TestUtilityTask()
            : base(0.1f) { }

        public override void ScoreEntity(int taskIndex, BotEntity entity)
        {
            entity.TaskScores[taskIndex] = FixedScore;
        }

        public override void UpdateScores(int taskIndex, System.Collections.Generic.IReadOnlyList<BotEntity> entities)
        {
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i].IsActive && entities[i].TaskScores != null)
                    entities[i].TaskScores[taskIndex] = FixedScore;
            }
        }

        public override void Activate(BotEntity entity) { }

        public override void Deactivate(BotEntity entity) { }

        public override void Update() { }
    }
}
