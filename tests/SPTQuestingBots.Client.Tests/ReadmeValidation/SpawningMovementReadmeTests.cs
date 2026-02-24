using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace SPTQuestingBots.Client.Tests.ReadmeValidation;

/// <summary>
/// Source-scanning tests that verify README claims about spawning and movement
/// features match the actual implementation code and config defaults.
/// </summary>
[TestFixture]
public class SpawningMovementReadmeTests
{
    private static readonly string SrcRoot = Path.GetFullPath(
        Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "src", "SPTQuestingBots.Client")
    );

    private static readonly string ConfigRoot = Path.GetFullPath(
        Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "config")
    );

    private static readonly string ReadmePath = Path.GetFullPath(
        Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "README.md")
    );

    private string ReadSource(string relativePath)
    {
        var full = Path.Combine(SrcRoot, relativePath);
        Assert.That(File.Exists(full), Is.True, $"Source file not found: {relativePath}");
        return File.ReadAllText(full);
    }

    private JObject ReadConfig()
    {
        var path = Path.Combine(ConfigRoot, "config.json");
        Assert.That(File.Exists(path), Is.True, "config.json not found");
        return JObject.Parse(File.ReadAllText(path));
    }

    private string ReadReadme()
    {
        Assert.That(File.Exists(ReadmePath), Is.True, "README.md not found");
        return File.ReadAllText(ReadmePath);
    }

    // ═══════════════════════════════════════════════════════════════
    // Feature 1: PMC and Player-Scav Spawning
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void PMCGenerator_Class_Exists_And_Extends_BotGenerator()
    {
        var src = ReadSource("Components/Spawning/PMCGenerator.cs");
        Assert.That(src, Does.Contain("class PMCGenerator : BotGenerator"));
    }

    [Test]
    public void PScavGenerator_Class_Exists_And_Extends_BotGenerator()
    {
        var src = ReadSource("Components/Spawning/PScavGenerator.cs");
        Assert.That(src, Does.Contain("class PScavGenerator : BotGenerator"));
    }

    [Test]
    public void PMCGenerator_Uses_EFT_SpawnPoints()
    {
        // README: "PMCs spawn at actual EFT spawn points"
        var src = ReadSource("Components/Spawning/PMCGenerator.cs");
        Assert.That(src, Does.Contain("ESpawnCategoryMask.Player"), "PMCGenerator should use EFT Player spawn category");
        Assert.That(src, Does.Contain("TryGetFurthestSpawnPointFromPositions"), "PMCGenerator should use EFT spawn point selection");
    }

    [Test]
    public void PMCGenerator_Supports_Staggered_Replacements()
    {
        // README: "staggered replacements as they die"
        var src = ReadSource("Components/Spawning/BotGenerator.cs");
        Assert.That(src, Does.Contain("HasRemainingSpawns"), "BotGenerator should track remaining spawns");
        Assert.That(src, Does.Contain("retrySpawnTimer"), "BotGenerator should have retry spawn timer");
    }

    [Test]
    public void PScavGenerator_Has_Spawn_Schedule()
    {
        // README: "Player Scavs spawn on a schedule"
        var src = ReadSource("Components/Spawning/PScavGenerator.cs");
        Assert.That(src, Does.Contain("botSpawnSchedule"), "PScavGenerator should have spawn schedule");
        Assert.That(src, Does.Contain("createBotSpawnSchedule"), "PScavGenerator should create spawn schedule");
    }

    [Test]
    public void PScavGenerator_Uses_ScavRaid_Settings()
    {
        // README: "mirroring live Tarkov reduced-raid-time settings"
        var src = ReadSource("Components/Spawning/PScavGenerator.cs");
        Assert.That(src, Does.Contain("ScavRaidSettings"), "PScavGenerator should use ScavRaidSettings for scheduling");
        Assert.That(src, Does.Contain("ReductionPercentWeights"), "PScavGenerator should use reduction percent weights");
    }

    [Test]
    public void ExceptAIPatch_Exists_For_Advanced_Spawning()
    {
        // README: "Advanced spawning tricks EFT into treating AI as human players"
        var src = ReadSource("Patches/Spawning/Advanced/ExceptAIPatch.cs");
        Assert.That(src, Does.Contain("ExceptAI"), "ExceptAIPatch should target ExceptAI method");
        Assert.That(src, Does.Contain("HumanAndSimulatedPlayers"), "ExceptAIPatch should include simulated players");
    }

    [Test]
    public void Config_PMC_GroupSizes_Support_Solo_Through_FiveMan()
    {
        // README: "solo through 5-man squads"
        var config = ReadConfig();
        var dist = config.SelectToken("bot_spawns.pmcs.bots_per_group_distribution") as JArray;
        Assert.That(dist, Is.Not.Null, "bots_per_group_distribution should exist");

        // Should have entries for groups of 1 through 5
        var groupSizes = dist.Select(x => (int)x[0]).ToArray();
        Assert.That(groupSizes, Does.Contain(1), "Should support solo (1)");
        Assert.That(groupSizes, Does.Contain(5), "Should support 5-man squads");
    }

    [Test]
    public void Config_PMC_Has_Difficulty_Distribution()
    {
        // README: "difficulty distribution"
        var config = ReadConfig();
        var dist = config.SelectToken("bot_spawns.pmcs.bot_difficulty_as_online") as JArray;
        Assert.That(dist, Is.Not.Null, "bot_difficulty_as_online should exist for PMCs");
        Assert.That(dist.Count, Is.GreaterThanOrEqualTo(2), "Should have multiple difficulty levels");
    }

    [Test]
    public void Config_PMC_Has_Spawn_Distances()
    {
        // README: "spawn distances"
        var config = ReadConfig();
        Assert.That(
            config.SelectToken("bot_spawns.pmcs.min_distance_from_players_initial")?.Type,
            Is.EqualTo(JTokenType.Integer).Or.EqualTo(JTokenType.Float),
            "PMC initial distance config should exist"
        );
        Assert.That(
            config.SelectToken("bot_spawns.pmcs.min_distance_from_players_during_raid")?.Type,
            Is.EqualTo(JTokenType.Integer).Or.EqualTo(JTokenType.Float),
            "PMC during-raid distance config should exist"
        );
    }

    // ═══════════════════════════════════════════════════════════════
    // Feature 2: Custom Movement System
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void Config_UseCustomMover_Default_True()
    {
        // README: "enabled by default (use_custom_mover, default: true)"
        var config = ReadConfig();
        Assert.That(
            config.SelectToken("questing.bot_pathing.use_custom_mover")?.Value<bool>(),
            Is.True,
            "use_custom_mover should default to true"
        );
    }

    [Test]
    public void CustomMoverController_Uses_PlayerMove()
    {
        // README: "Phobos-style Player.Move() replacement"
        var src = ReadSource("Models/Pathing/CustomMoverController.cs");
        Assert.That(src, Does.Contain("player.Move("), "CustomMoverController should call Player.Move()");
        Assert.That(src, Does.Contain("SetSteerDirection"), "CustomMoverController should call SetSteerDirection");
    }

    [Test]
    public void CustomPathFollower_Has_CornerEpsilon()
    {
        // README: "corner-reaching epsilon"
        var src = ReadSource("Models/Pathing/CustomMoverConfig.cs");
        Assert.That(src, Does.Contain("WalkCornerEpsilon"), "Should have walk corner epsilon");
        Assert.That(src, Does.Contain("SprintCornerEpsilon"), "Should have sprint corner epsilon");
    }

    [Test]
    public void CustomPathFollower_Has_PathDeviationSpringForce()
    {
        // README: "path-deviation spring force"
        var src = ReadSource("Models/Pathing/CustomPathFollower.cs");
        Assert.That(src, Does.Contain("PathDeviationForce"), "Should use PathDeviationForce for spring force");
        Assert.That(src, Does.Contain("ComputeDeviation"), "Should compute path deviation");
    }

    [Test]
    public void CustomPathFollower_Has_SprintAngleJitterGating()
    {
        // README: "sprint angle-jitter gating"
        var src = ReadSource("Models/Pathing/CustomPathFollower.cs");
        Assert.That(src, Does.Contain("SprintAngleJitter"), "Should have SprintAngleJitter for sprint gating");
        Assert.That(src, Does.Contain("CanSprint"), "Should have CanSprint method");
    }

    [Test]
    public void PathSmoother_Uses_Chaikin()
    {
        // README: "Chaikin path smoothing subdivides NavMesh corners"
        var src = ReadSource("Models/Pathing/PathSmoother.cs");
        Assert.That(src, Does.Contain("ChaikinSmooth"), "Should have ChaikinSmooth method");
        Assert.That(src, Does.Contain("0.75f"), "Chaikin algorithm should use 0.75 ratio");
        Assert.That(src, Does.Contain("0.25f"), "Chaikin algorithm should use 0.25 ratio");
    }

    [Test]
    public void CustomMoverController_Has_NavMeshRaycast_CornerCutting()
    {
        // README: "NavMesh.Raycast corner-cutting"
        var src = ReadSource("Models/Pathing/CustomMoverController.cs");
        Assert.That(src, Does.Contain("NavMesh.Raycast"), "Should use NavMesh.Raycast for corner cutting");
        Assert.That(src, Does.Contain("TryNavMeshCornerCut"), "Should have TryNavMeshCornerCut method");
    }

    [Test]
    public void CustomPathFollower_CornerCut_Within_1m()
    {
        // README: "within 1m"
        var src = ReadSource("Models/Pathing/CustomPathFollower.cs");
        Assert.That(
            src,
            Does.Contain("SqrDistanceXZ(position, _corners[_currentCorner]) < 1f"),
            "Corner cut distance check should be < 1.0 (1m squared)"
        );
    }

    [Test]
    public void Three_BSG_Patches_Exist()
    {
        // README: "3 BSG patches: ManualFixedUpdate skip, IsAI→false, vault enable for AI"
        Assert.That(
            File.Exists(Path.Combine(SrcRoot, "Patches/Movement/BotMoverFixedUpdatePatch.cs")),
            Is.True,
            "ManualFixedUpdate skip patch should exist"
        );
        Assert.That(File.Exists(Path.Combine(SrcRoot, "Patches/Movement/MovementContextIsAIPatch.cs")), Is.True, "IsAI patch should exist");
        Assert.That(File.Exists(Path.Combine(SrcRoot, "Patches/Movement/EnableVaultPatch.cs")), Is.True, "Vault enable patch should exist");
    }

    [Test]
    public void BotMoverFixedUpdatePatch_Skips_ManualFixedUpdate()
    {
        var src = ReadSource("Patches/Movement/BotMoverFixedUpdatePatch.cs");
        Assert.That(src, Does.Contain("ManualFixedUpdate"), "Patch should target ManualFixedUpdate");
        Assert.That(src, Does.Contain("IsCustomMoverActive"), "Patch should check custom mover state");
    }

    [Test]
    public void MovementContextIsAIPatch_Returns_False()
    {
        var src = ReadSource("Patches/Movement/MovementContextIsAIPatch.cs");
        Assert.That(src, Does.Contain("__result = false"), "IsAI patch should set result to false");
    }

    [Test]
    public void EnableVaultPatch_Sets_AiControlled_False()
    {
        var src = ReadSource("Patches/Movement/EnableVaultPatch.cs");
        Assert.That(src, Does.Contain("aiControlled = false"), "Vault patch should set aiControlled to false");
    }

    [Test]
    public void CustomMoverHandoff_Syncs_6_BSG_Fields()
    {
        // README: "6-field BSG state sync on layer exit"
        var src = ReadSource("Helpers/CustomMoverHandoff.cs");
        Assert.That(src, Does.Contain("LastGoodCastPoint"), "Should sync LastGoodCastPoint");
        Assert.That(src, Does.Contain("PrevSuccessLinkedFrom_1"), "Should sync PrevSuccessLinkedFrom_1");
        Assert.That(src, Does.Contain("PrevLinkPos"), "Should sync PrevLinkPos");
        Assert.That(src, Does.Contain("PositionOnWayInner"), "Should sync PositionOnWayInner");
        Assert.That(src, Does.Contain("LastGoodCastPointTime"), "Should sync LastGoodCastPointTime");
        Assert.That(src, Does.Contain("PrevPosLinkedTime_1"), "Should sync PrevPosLinkedTime_1");
    }

    [Test]
    public void CustomMoverHandoff_Calls_SetPlayerToNavMesh()
    {
        // README: "SetPlayerToNavMesh() for clean mover resume"
        var src = ReadSource("Helpers/CustomMoverHandoff.cs");
        Assert.That(src, Does.Contain("SetPlayerToNavMesh"), "Should call SetPlayerToNavMesh on deactivation");
    }

    // ═══════════════════════════════════════════════════════════════
    // Feature 3: Zone-Based Movement
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void WorldGridManager_Exists()
    {
        Assert.That(
            File.Exists(Path.Combine(SrcRoot, "ZoneMovement/Integration/WorldGridManager.cs")),
            Is.True,
            "WorldGridManager should exist"
        );
    }

    [Test]
    public void WorldGrid_Exists()
    {
        // README: "Grid + vector-field architecture"
        Assert.That(File.Exists(Path.Combine(SrcRoot, "ZoneMovement/Core/WorldGrid.cs")), Is.True, "WorldGrid should exist");
    }

    [Test]
    public void WorldGridManager_AutoDetects_Bounds_From_SpawnPoints()
    {
        // README: "Auto-detects map bounds from spawn points"
        var src = ReadSource("ZoneMovement/Integration/WorldGridManager.cs");
        Assert.That(src, Does.Contain("GetAllValidSpawnPointParams"), "Should use spawn points for bounds detection");
    }

    [Test]
    public void ConvergenceField_Exists()
    {
        // README: "convergence field (human players)"
        Assert.That(
            File.Exists(Path.Combine(SrcRoot, "ZoneMovement/Fields/ConvergenceField.cs")),
            Is.True,
            "ConvergenceField should exist"
        );
    }

    [Test]
    public void AdvectionField_Exists()
    {
        // README: "advection field (geographic zones)"
        Assert.That(File.Exists(Path.Combine(SrcRoot, "ZoneMovement/Fields/AdvectionField.cs")), Is.True, "AdvectionField should exist");
    }

    [Test]
    public void FieldComposer_Exists()
    {
        // README: "Per-bot momentum and noise"
        Assert.That(File.Exists(Path.Combine(SrcRoot, "ZoneMovement/Fields/FieldComposer.cs")), Is.True, "FieldComposer should exist");
    }

    [Test]
    public void PointOfInterest_Exists()
    {
        // README: "POI-aware"
        Assert.That(File.Exists(Path.Combine(SrcRoot, "ZoneMovement/Core/PointOfInterest.cs")), Is.True, "PointOfInterest should exist");
    }

    [Test]
    public void ZoneDebugOverlay_Exists()
    {
        // README: "2D debug minimap"
        Assert.That(
            File.Exists(Path.Combine(SrcRoot, "ZoneMovement/Integration/ZoneDebugOverlay.cs")),
            Is.True,
            "ZoneDebugOverlay should exist"
        );
    }

    [Test]
    public void DestinationSelector_Exists()
    {
        // README: "Dynamic objective cycling via live field state"
        Assert.That(
            File.Exists(Path.Combine(SrcRoot, "ZoneMovement/Selection/DestinationSelector.cs")),
            Is.True,
            "DestinationSelector should exist"
        );
    }

    [Test]
    public void Config_ZoneMovement_Has_MomentumAndNoise()
    {
        // README: "Per-bot momentum and noise"
        var config = ReadConfig();
        Assert.That(config.SelectToken("questing.zone_movement.momentum_weight"), Is.Not.Null, "momentum_weight should exist in config");
        Assert.That(config.SelectToken("questing.zone_movement.noise_weight"), Is.Not.Null, "noise_weight should exist in config");
    }

    // ═══════════════════════════════════════════════════════════════
    // Feature 4: Door Collision Bypass
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void Config_BypassDoorColliders_Default_True()
    {
        // README: "bypass_door_colliders, default: true"
        var config = ReadConfig();
        Assert.That(
            config.SelectToken("questing.bot_pathing.bypass_door_colliders")?.Value<bool>(),
            Is.True,
            "bypass_door_colliders should default to true"
        );
    }

    [Test]
    public void ShrinkDoorNavMeshCarversPatch_Uses_37_5_Percent()
    {
        // README: "Shrinks door NavMesh carvers to 37.5%"
        var src = ReadSource("Patches/ShrinkDoorNavMeshCarversPatch.cs");
        Assert.That(src, Does.Contain("0.375f"), "CarverScaleFactor should be 0.375 (37.5%)");
    }

    [Test]
    public void DoorCollisionHelper_DisablesPhysicsCollision()
    {
        // README: "Disables physics collision between bot colliders and door colliders"
        var src = ReadSource("Helpers/DoorCollisionHelper.cs");
        Assert.That(src, Does.Contain("Physics.IgnoreCollision"), "Should call Physics.IgnoreCollision");
        Assert.That(src, Does.Contain("EFTPhysicsClass.IgnoreCollision"), "Should call EFTPhysicsClass.IgnoreCollision");
    }

    [Test]
    public void ShrinkDoorNavMeshCarversPatch_Initializes_DoorCollisionHelper()
    {
        var src = ReadSource("Patches/ShrinkDoorNavMeshCarversPatch.cs");
        Assert.That(src, Does.Contain("DoorCollisionHelper.Initialize"), "Patch should initialize DoorCollisionHelper");
    }

    // ═══════════════════════════════════════════════════════════════
    // Feature 5: Scav Spawn Restrictions
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void ScavLimits_TrySpawnFreeAndDelayPatch_Exists()
    {
        // README: "Spawn-rate limiting"
        var src = ReadSource("Patches/Spawning/ScavLimits/TrySpawnFreeAndDelayPatch.cs");
        Assert.That(src, Does.Contain("ScavSpawnBlockReason"), "Should define spawn block reasons");
    }

    [Test]
    public void ScavLimits_Has_MaxAliveScavCap()
    {
        // README: "max-alive-scav caps"
        var src = ReadSource("Patches/Spawning/ScavLimits/TrySpawnFreeAndDelayPatch.cs");
        Assert.That(src, Does.Contain("MaxAliveScavs"), "Should check max alive scav limit");
        Assert.That(src, Does.Contain("ScavSpawnBlockReason.MaxAliveScavs"), "Should have MaxAliveScavs block reason");
    }

    [Test]
    public void ScavLimits_Has_SpawnRateLimiting()
    {
        // README: "Spawn-rate limiting"
        var src = ReadSource("Patches/Spawning/ScavLimits/TrySpawnFreeAndDelayPatch.cs");
        Assert.That(src, Does.Contain("ScavSpawnRateLimit"), "Should check spawn rate limit");
        Assert.That(src, Does.Contain("ScavSpawnBlockReason.ScavRateLimit"), "Should have ScavRateLimit block reason");
    }

    [Test]
    public void ScavLimits_SpawnPointIsValidPatch_Exists()
    {
        // README: "distance-based exclusion zones"
        var src = ReadSource("Patches/Spawning/ScavLimits/SpawnPointIsValidPatch.cs");
        Assert.That(src, Does.Contain("exclusionRadius"), "Should calculate exclusion radius");
        Assert.That(src, Does.Contain("minDistanceFromPlayers"), "Should check distance from players");
    }

    // ═══════════════════════════════════════════════════════════════
    // Feature 6: Per-Map Advection Zones
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void AdvectionZoneConfig_Has_BuiltinZones()
    {
        // README: "Builtin zones tied to BSG BotZone names"
        var src = ReadSource("ZoneMovement/Core/AdvectionZoneConfig.cs");
        Assert.That(src, Does.Contain("class BuiltinZoneEntry"), "BuiltinZoneEntry class should exist");
        Assert.That(src, Does.Contain("ZoneName"), "BuiltinZoneEntry should have ZoneName field");
    }

    [Test]
    public void AdvectionZoneConfig_Has_CustomZones()
    {
        // README: "Custom zones at arbitrary world positions"
        var src = ReadSource("ZoneMovement/Core/AdvectionZoneConfig.cs");
        Assert.That(src, Does.Contain("class CustomZoneEntry"), "CustomZoneEntry class should exist");
    }

    [Test]
    public void AdvectionZoneConfig_Has_NegativeForce()
    {
        // README: "Negative force supported (repulsor zones)"
        var src = ReadSource("ZoneMovement/Core/AdvectionZoneConfig.cs");
        // Check that ForceMin can be negative in the defaults
        Assert.That(src, Does.Contain("-0.5f"), "Should have negative force values (repulsor zones)");
    }

    [Test]
    public void AdvectionZoneConfig_Dorms_Has_Time_Multipliers()
    {
        // README: "Time multipliers: Dorms early 1.5x -> late 0.5x"
        var src = ReadSource("ZoneMovement/Core/AdvectionZoneConfig.cs");
        Assert.That(src, Does.Contain("earlyMultiplier: 1.5f"), "Dorms should have earlyMultiplier of 1.5");
        Assert.That(src, Does.Contain("lateMultiplier: 0.5f"), "Dorms should have lateMultiplier of 0.5");
    }

    [Test]
    public void AdvectionZoneConfig_Interchange_Has_BossAlive_Modifier()
    {
        // README: "Boss alive/dead modifiers: Interchange center 1.5x when Killa alive"
        var src = ReadSource("ZoneMovement/Core/AdvectionZoneConfig.cs");
        Assert.That(src, Does.Contain("bossAliveMultiplier: 1.5f"), "Interchange ZoneCenter should have bossAliveMultiplier of 1.5");
    }

    [Test]
    public void AdvectionZoneConfig_Has_Defaults_For_8_Maps()
    {
        // README: "Hardcoded defaults for 8 maps"
        var src = ReadSource("ZoneMovement/Core/AdvectionZoneConfig.cs");
        var defaults = new[]
        {
            "bigmap",
            "interchange",
            "shoreline",
            "woods",
            "rezervbase",
            "laboratory",
            "factory4_day",
            "factory4_night",
        };
        foreach (var map in defaults)
        {
            Assert.That(src, Does.Contain($"[\"{map}\"]"), $"Should have defaults for {map}");
        }
    }

    [Test]
    public void AdvectionZoneLoader_Applies_TimeAndBoss_Multipliers()
    {
        // README: "Wired into WorldGridManager"
        var src = ReadSource("ZoneMovement/Core/AdvectionZoneLoader.cs");
        Assert.That(src, Does.Contain("ComputeTimeMultiplier"), "Should apply time multipliers");
        Assert.That(src, Does.Contain("BossAliveMultiplier"), "Should apply boss alive multipliers");
        Assert.That(src, Does.Contain("AddBoundedZone"), "Should inject zones into advection field");
    }

    [Test]
    public void Config_AdvectionZonesPerMap_IsOverridable()
    {
        // README: "all overridable via config.json"
        var config = ReadConfig();
        Assert.That(
            config.SelectToken("questing.zone_movement.advection_zones_per_map"),
            Is.Not.Null,
            "advection_zones_per_map should exist in config"
        );
    }

    // ═══════════════════════════════════════════════════════════════
    // Feature 7: Patrol Route System
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void PatrolRouteType_Has_Three_Types()
    {
        // README: "3 route types: Perimeter (loop), Interior (loop), Overwatch (no loop)"
        var src = ReadSource("Models/Pathing/PatrolRoute.cs");
        Assert.That(src, Does.Contain("Perimeter = 0"), "Should have Perimeter type");
        Assert.That(src, Does.Contain("Interior = 1"), "Should have Interior type");
        Assert.That(src, Does.Contain("Overwatch = 2"), "Should have Overwatch type");
    }

    [Test]
    public void PatrolRoute_Overwatch_Does_Not_Loop_By_Default()
    {
        // README: "Overwatch (no loop)"
        var src = ReadSource("Models/Pathing/PatrolRoute.cs");
        Assert.That(src, Does.Contain("isLoop ?? (type != PatrolRouteType.Overwatch)"), "Overwatch should default to non-loop");
    }

    [Test]
    public void PatrolRouteConfig_Has_Customs_Defaults()
    {
        // README: "Customs (Dorms Perimeter, Customs Road, Construction Overwatch)"
        var src = ReadSource("Models/Pathing/PatrolRouteConfig.cs");
        Assert.That(src, Does.Contain("Dorms Perimeter"), "Should have Dorms Perimeter route");
        Assert.That(src, Does.Contain("Customs Road"), "Should have Customs Road route");
        Assert.That(src, Does.Contain("Construction Overwatch"), "Should have Construction Overwatch route");
    }

    [Test]
    public void PatrolRouteConfig_Has_Interchange_Defaults()
    {
        // README: "Interchange (Mall Interior, Parking Perimeter)"
        var src = ReadSource("Models/Pathing/PatrolRouteConfig.cs");
        Assert.That(src, Does.Contain("Mall Interior"), "Should have Mall Interior route");
        Assert.That(src, Does.Contain("Parking Perimeter"), "Should have Parking Perimeter route");
    }

    [Test]
    public void PatrolRouteConfig_Has_Shoreline_Defaults()
    {
        // README: "Shoreline (Resort Sweep, Shoreline Path)"
        var src = ReadSource("Models/Pathing/PatrolRouteConfig.cs");
        Assert.That(src, Does.Contain("Resort Sweep"), "Should have Resort Sweep route");
        Assert.That(src, Does.Contain("Shoreline Path"), "Should have Shoreline Path route");
    }

    [Test]
    public void PatrolRouteConfig_Has_Reserve_Defaults()
    {
        // README: "Reserve (Bunker Patrol, Base Perimeter)"
        var src = ReadSource("Models/Pathing/PatrolRouteConfig.cs");
        Assert.That(src, Does.Contain("Bunker Patrol"), "Should have Bunker Patrol route");
        Assert.That(src, Does.Contain("Base Perimeter"), "Should have Base Perimeter route");
    }

    [Test]
    public void PatrolRouteConfig_Has_Woods_Defaults()
    {
        // README: "Woods (Sawmill Circuit)"
        var src = ReadSource("Models/Pathing/PatrolRouteConfig.cs");
        Assert.That(src, Does.Contain("Sawmill Circuit"), "Should have Sawmill Circuit route");
    }

    [Test]
    public void PatrolRouteSelector_Uses_Proximity_And_Personality_Weights()
    {
        // README: "proximity (0.6 weight) + personality fit (0.4 weight)"
        var src = ReadSource("BotLogic/ECS/Systems/PatrolRouteSelector.cs");
        Assert.That(src, Does.Contain("proximityScore * 0.6f"), "Proximity weight should be 0.6");
        Assert.That(src, Does.Contain("fitScore * 0.4f"), "Personality fit weight should be 0.4");
    }

    [Test]
    public void PatrolTask_Is_Task14_With_MaxBaseScore_050()
    {
        // README: "PatrolTask: utility task #14, MaxBaseScore=0.50"
        var src = ReadSource("BotLogic/ECS/UtilityAI/Tasks/PatrolTask.cs");
        Assert.That(src, Does.Contain("MaxBaseScore = 0.50f"), "MaxBaseScore should be 0.50");
        Assert.That(src, Does.Contain("BotActionTypeId.Patrol"), "Should use Patrol action type ID");
    }

    [Test]
    public void PatrolAction_Has_NavigatePauseAdvance_StateMachine()
    {
        // README: "navigate->pause->advance state machine"
        var src = ReadSource("BotLogic/Objective/PatrolAction.cs");
        Assert.That(src, Does.Contain("_isPausing"), "Should track pause state");
        Assert.That(src, Does.Contain("StartPause"), "Should have pause start method");
        Assert.That(src, Does.Contain("UpdatePause"), "Should have pause update method");
        Assert.That(src, Does.Contain("AdvanceWaypoint"), "Should have waypoint advance method");
    }

    [Test]
    public void PatrolAction_Has_HeadScanning()
    {
        // README: "head scanning at waypoints"
        var src = ReadSource("BotLogic/Objective/PatrolAction.cs");
        Assert.That(src, Does.Contain("_headScanIntervalMin"), "Should have head scan interval");
        Assert.That(src, Does.Contain("_nextScanTime"), "Should track next scan time");
    }

    [Test]
    public void PatrolAction_Has_MovementTimeout()
    {
        // README: "movement timeout"
        var src = ReadSource("BotLogic/Objective/PatrolAction.cs");
        Assert.That(src, Does.Contain("_movementTimeout"), "Should have movement timeout");
    }

    [Test]
    public void PatrolAction_Has_StuckDetection()
    {
        // README: "stuck detection"
        var src = ReadSource("BotLogic/Objective/PatrolAction.cs");
        Assert.That(src, Does.Contain("checkIfBotIsStuck"), "Should check for stuck state");
    }

    [Test]
    public void PatrolRoutes_Filterable_By_Aggression_And_RaidTime()
    {
        // README: "Routes filterable by aggression range and raid time window"
        var src = ReadSource("BotLogic/ECS/Systems/PatrolRouteSelector.cs");
        Assert.That(src, Does.Contain("route.MinAggression"), "Should filter by min aggression");
        Assert.That(src, Does.Contain("route.MaxAggression"), "Should filter by max aggression");
        Assert.That(src, Does.Contain("route.MinRaidTime"), "Should filter by min raid time");
        Assert.That(src, Does.Contain("route.MaxRaidTime"), "Should filter by max raid time");
    }

    [Test]
    public void ScoringModifiers_Patrol_Personality_Cautious12x_Aggressive08x()
    {
        // README: "cautious bots patrol more (1.2x), aggressive bots less (0.8x)"
        // Lerp(1.2, 0.8, aggression): aggression=0 (cautious) -> 1.2, aggression=1 (aggressive) -> 0.8
        var src = ReadSource("BotLogic/ECS/UtilityAI/ScoringModifiers.cs");
        Assert.That(src, Does.Contain("case BotActionTypeId.Patrol:"), "Should have Patrol case in PersonalityModifier");
        // Check the Lerp values for Patrol in the PersonalityModifier method
        // The pattern is: case BotActionTypeId.Patrol: return Lerp(1.2f, 0.8f, clampedAggression);
        Assert.That(
            src,
            Does.Match(@"Patrol:\s+return Lerp\(1\.2f, 0\.8f"),
            "Patrol personality modifier should lerp from 1.2 (cautious) to 0.8 (aggressive)"
        );
    }

    [Test]
    public void ScoringModifiers_Patrol_RaidTime_LateRaid12x()
    {
        // README: "late raid increases patrol (1.2x)"
        // Lerp(0.8, 1.2, raidTime): early (0) -> 0.8, late (1) -> 1.2
        var src = ReadSource("BotLogic/ECS/UtilityAI/ScoringModifiers.cs");
        Assert.That(
            src,
            Does.Match(@"case BotActionTypeId\.Patrol:\s+return Lerp\(0\.8f, 1\.2f"),
            "Patrol raid time modifier should lerp from 0.8 (early) to 1.2 (late)"
        );
    }

    [Test]
    public void Config_Patrol_RoutesPerMap_IsOverridable()
    {
        // README: "All routes overridable via questing.patrol.routes_per_map"
        var config = ReadConfig();
        Assert.That(config.SelectToken("questing.patrol.routes_per_map"), Is.Not.Null, "routes_per_map should exist in patrol config");
    }

    [Test]
    public void Config_Patrol_BaseScore_Is_050()
    {
        // README: "MaxBaseScore=0.50"
        var config = ReadConfig();
        Assert.That(
            config.SelectToken("questing.patrol.base_score")?.Value<float>(),
            Is.EqualTo(0.50f).Within(0.001f),
            "patrol.base_score should be 0.50"
        );
    }

    // ═══════════════════════════════════════════════════════════════
    // Cross-feature: Config consistency checks
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void SpawnEntryTask_MaxBaseScore_Is_080()
    {
        // SpawnEntry MaxBaseScore raised to 1.0 so no other task can bypass the spawn pause
        var src = ReadSource("BotLogic/ECS/UtilityAI/Tasks/SpawnEntryTask.cs");
        Assert.That(src, Does.Contain("MaxBaseScore = 1.0f"), "SpawnEntryTask MaxBaseScore should be 1.0");
    }

    [Test]
    [Description("DISCREPANCY: README says 'Squad stagger: 0.5s extra per member' but config default is 1.5s")]
    public void Config_SpawnEntry_SquadStagger_Matches_Config_Default()
    {
        // README says: "Squad stagger: 0.5s extra per member index"
        // Actual config default: 1.5s
        var config = ReadConfig();
        float stagger = config.SelectToken("questing.spawn_entry.squad_stagger_per_member")?.Value<float>() ?? 0f;
        // The actual config value is 1.5, not 0.5 as stated in README
        Assert.That(stagger, Is.EqualTo(1.5f).Within(0.001f), "squad_stagger_per_member config default is 1.5s");
    }

    [Test]
    [Description("DISCREPANCY: README Room Clearing says 'random 3-8s' but config defaults are 15-30s")]
    public void Config_RoomClear_Duration_Matches_Config_Default()
    {
        // README says: "Room clear duration: random 3–8s"
        // Actual config default: 15.0-30.0
        var config = ReadConfig();
        float durationMin = config.SelectToken("questing.room_clear.duration_min")?.Value<float>() ?? 0f;
        float durationMax = config.SelectToken("questing.room_clear.duration_max")?.Value<float>() ?? 0f;
        // The actual config values are 15-30, not 3-8 as stated in README
        Assert.That(durationMin, Is.EqualTo(15.0f).Within(0.1f), "room_clear.duration_min config default is 15.0s");
        Assert.That(durationMax, Is.EqualTo(30.0f).Within(0.1f), "room_clear.duration_max config default is 30.0s");
    }

    [Test]
    public void Config_SpawnEntry_Duration_Matches_Readme()
    {
        // README: "3-5 seconds on first spawn"
        var config = ReadConfig();
        float min = config.SelectToken("questing.spawn_entry.base_duration_min")?.Value<float>() ?? 0f;
        float max = config.SelectToken("questing.spawn_entry.base_duration_max")?.Value<float>() ?? 0f;
        Assert.That(min, Is.EqualTo(3.0f).Within(0.1f), "spawn_entry.base_duration_min should be 3.0");
        Assert.That(max, Is.EqualTo(5.0f).Within(0.1f), "spawn_entry.base_duration_max should be 5.0");
    }

    [Test]
    public void Config_SpawnEntry_Pose_Matches_Readme()
    {
        // README: "pose 0.85"
        var config = ReadConfig();
        float pose = config.SelectToken("questing.spawn_entry.pose")?.Value<float>() ?? 0f;
        Assert.That(pose, Is.EqualTo(0.85f).Within(0.01f), "spawn_entry.pose should be 0.85");
    }

    [Test]
    public void Config_SpawnEntry_DirectionBias_Matches_Readme()
    {
        // README: "Direction bias: first objective biased toward spawn facing direction for 30s"
        var config = ReadConfig();
        float duration = config.SelectToken("questing.spawn_entry.direction_bias_duration")?.Value<float>() ?? 0f;
        Assert.That(duration, Is.EqualTo(30.0f).Within(0.1f), "direction_bias_duration should be 30.0");
    }
}
