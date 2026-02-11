using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.Game.Spawning;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.Components;
using SPTQuestingBots.Configuration;
using SPTQuestingBots.Controllers;
using SPTQuestingBots.ZoneMovement.Core;
using SPTQuestingBots.ZoneMovement.Fields;
using SPTQuestingBots.ZoneMovement.Selection;
using UnityEngine;
using UnityEngine.AI;

namespace SPTQuestingBots.ZoneMovement.Integration;

/// <summary>
/// MonoBehaviour orchestrator for the zone-based movement system. Creates the world grid
/// on <see cref="Awake"/>, populates it with POIs and zone sources, and periodically
/// refreshes the convergence field toward human players.
/// <para>
/// This component should be attached to the <c>GameWorld</c> object during
/// <see cref="LocationData.Awake()"/>, before <see cref="BotQuestBuilder"/> is created.
/// </para>
/// </summary>
public class WorldGridManager : MonoBehaviour
{
    /// <summary>Whether the grid has been fully initialized and is ready to use.</summary>
    public bool IsInitialized { get; private set; }

    /// <summary>The world grid partitioning the map into cells.</summary>
    public WorldGrid Grid { get; private set; }

    private AdvectionField advectionField;
    private ConvergenceField convergenceField;
    private FieldComposer fieldComposer;
    private CellScorer cellScorer;
    private DestinationSelector destinationSelector;
    private float convergenceUpdateInterval;
    private float lastConvergenceUpdate;

    /// <summary>Pre-allocated array for combat pull points (avoids per-frame allocation).</summary>
    private CombatPullPoint[] combatPullBuffer = new CombatPullPoint[CombatEventRegistry.DefaultCapacity];
    private int combatPullCount;

    /// <summary>Combat convergence config cached from ZoneMovementConfig.</summary>
    private float combatConvergenceDecaySec;
    private float combatConvergenceRadius;
    private float combatConvergenceForce;

    /// <summary>
    /// Normalized raid time (0.0 = start, 1.0 = end). Updated externally by the caller.
    /// Defaults to 0.5 (mid-raid) if not set.
    /// </summary>
    public float RaidTimeNormalized { get; set; } = 0.5f;

    /// <summary>Cached human player positions, refreshed each convergence update.</summary>
    private readonly List<Vector3> cachedPlayerPositions = new List<Vector3>();

    /// <summary>Cached bot positions, refreshed each convergence update.</summary>
    private readonly List<Vector3> cachedBotPositions = new List<Vector3>();

    /// <summary>Zone source positions discovered during initialization (for debug visualization).</summary>
    private readonly List<(Vector3 position, float strength)> discoveredZoneSources = new List<(Vector3, float)>();

    /// <summary>Advection field instance (for debug visualization).</summary>
    public AdvectionField Advection => advectionField;

    /// <summary>Convergence field instance (for debug visualization).</summary>
    public ConvergenceField Convergence => convergenceField;

    /// <summary>Cached human player positions from last update.</summary>
    public IReadOnlyList<Vector3> CachedPlayerPositions => cachedPlayerPositions;

    /// <summary>Cached bot positions from last update.</summary>
    public IReadOnlyList<Vector3> CachedBotPositions => cachedBotPositions;

    /// <summary>Zone source positions discovered during initialization.</summary>
    public IReadOnlyList<(Vector3 position, float strength)> ZoneSources => discoveredZoneSources;

    /// <summary>
    /// Initializes the grid, POIs, zone sources, and field components.
    /// </summary>
    protected void Awake()
    {
        try
        {
            var config = ConfigController.Config.Questing.ZoneMovement;

            // 1. Get spawn points for bounds detection and zone discovery
            SpawnPointParams[] spawnPoints = Singleton<GameWorld>.Instance.GetComponent<LocationData>().GetAllValidSpawnPointParams();

            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                LoggingController.LogError("[ZoneMovement] No spawn points found. Grid not created.");
                return;
            }

            // 2. Detect map bounds from spawn points
            Vector3[] positions = spawnPoints.Select(sp => sp.Position.ToUnityVector3()).ToArray();
            var (min, max) = MapBoundsDetector.DetectBounds(positions, config.BoundsPadding);

            // 3. Create world grid with auto-sized cells
            Grid = new WorldGrid(min, max, config.TargetCellCount);
            LoggingController.LogInfo(
                $"[ZoneMovement] Grid created: {Grid.Cols}x{Grid.Rows} ({Grid.Cols * Grid.Rows} cells), cell size: {Grid.CellSize:F1}m"
            );

            // 4. Scan scene for POIs and add them to the grid
            List<PointOfInterest> pois = PoiScanner.ScanScene();
            foreach (var poi in pois)
            {
                Grid.AddPoi(poi);
            }

            // 5. Add spawn points as POIs
            foreach (var sp in spawnPoints)
            {
                Grid.AddPoi(new PointOfInterest(sp.Position.ToUnityVector3(), PoiCategory.SpawnPoint));
            }

            // 6. Discover zones and populate advection field
            advectionField = new AdvectionField(config.CrowdRepulsionStrength);
            var zones = ZoneDiscovery.DiscoverZones(spawnPoints);
            foreach (var (position, strength) in zones)
            {
                advectionField.AddZone(position, strength);
                discoveredZoneSources.Add((position, strength));
            }

            // 6b. Resolve map ID and inject per-map bounded advection zones
            string mapId = Singleton<GameWorld>.Instance.GetComponent<LocationData>().CurrentLocation.Id;
            var builtinZoneCentroids = ZoneDiscovery.DiscoverZoneCentroids(spawnPoints);
            Dictionary<string, AdvectionMapZones> zoneOverrides = null;
            if (config.AdvectionZonesPerMap != null && config.AdvectionZonesPerMap.Count > 0)
            {
                zoneOverrides = new Dictionary<string, AdvectionMapZones>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in config.AdvectionZonesPerMap)
                {
                    var builtins = new Dictionary<string, BuiltinZoneEntry>(StringComparer.OrdinalIgnoreCase);
                    foreach (var bkvp in kvp.Value.BuiltinZones)
                    {
                        builtins[bkvp.Key] = new BuiltinZoneEntry(
                            bkvp.Key,
                            bkvp.Value.ForceMin,
                            bkvp.Value.ForceMax,
                            bkvp.Value.Radius,
                            bkvp.Value.Decay,
                            bkvp.Value.EarlyMultiplier,
                            bkvp.Value.LateMultiplier,
                            bkvp.Value.BossAliveMultiplier
                        );
                    }
                    var customs = new List<CustomZoneEntry>();
                    foreach (var c in kvp.Value.CustomZones)
                    {
                        customs.Add(
                            new CustomZoneEntry(
                                c.X,
                                c.Z,
                                c.ForceMin,
                                c.ForceMax,
                                c.Radius,
                                c.Decay,
                                c.EarlyMultiplier,
                                c.LateMultiplier,
                                c.BossAliveMultiplier
                            )
                        );
                    }
                    zoneOverrides[kvp.Key] = new AdvectionMapZones(builtins, customs);
                }
            }
            int boundedCount = AdvectionZoneLoader.LoadAndInjectZones(advectionField, builtinZoneCentroids, mapId, zoneOverrides, 0f);
            LoggingController.LogInfo($"[ZoneMovement] Injected {boundedCount} bounded advection zones for map '{mapId}'");

            // 7. Synthetic fill: add NavMesh-validated synthetic POIs to empty cells
            int syntheticCount = 0;
            for (int col = 0; col < Grid.Cols; col++)
            {
                for (int row = 0; row < Grid.Rows; row++)
                {
                    var cell = Grid.GetCell(col, row);
                    if (cell.IsNavigable)
                        continue;

                    if (NavMesh.SamplePosition(cell.Center, out NavMeshHit hit, Grid.CellSize, NavMesh.AllAreas))
                    {
                        Grid.AddPoi(new PointOfInterest(hit.position, PoiCategory.Synthetic));
                        syntheticCount++;
                    }
                }
            }
            LoggingController.LogInfo($"[ZoneMovement] Added {syntheticCount} synthetic POIs to empty cells");

            // 8. Resolve per-map convergence config
            Dictionary<string, ConvergenceMapConfig> mapOverrides = null;
            if (config.ConvergencePerMap != null && config.ConvergencePerMap.Count > 0)
            {
                mapOverrides = new Dictionary<string, ConvergenceMapConfig>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in config.ConvergencePerMap)
                {
                    mapOverrides[kvp.Key] = new ConvergenceMapConfig(kvp.Value.Radius, kvp.Value.Force, kvp.Value.Enabled);
                }
            }
            var mapConfig = ConvergenceMapConfig.GetForMap(mapId, mapOverrides);

            // 9. Create convergence field with per-map radius and force
            float convRadius = mapConfig.Enabled ? mapConfig.Radius : 0f;
            float convForce = mapConfig.Enabled ? mapConfig.Force : 0f;
            convergenceField = new ConvergenceField(config.ConvergenceUpdateIntervalSec, convRadius, convForce);
            convergenceUpdateInterval = config.ConvergenceUpdateIntervalSec;

            LoggingController.LogInfo(
                $"[ZoneMovement] Convergence for map '{mapId}': enabled={mapConfig.Enabled}, radius={convRadius:F0}, force={convForce:F1}"
            );

            // 10. Cache combat convergence config
            combatConvergenceDecaySec = config.CombatConvergenceDecaySec;
            combatConvergenceRadius = config.CombatConvergenceRadius;
            combatConvergenceForce = config.CombatConvergenceForce;

            fieldComposer = new FieldComposer(config.ConvergenceWeight, config.AdvectionWeight, config.MomentumWeight, config.NoiseWeight);

            cellScorer = new CellScorer(config.PoiScoreWeight);
            destinationSelector = new DestinationSelector(cellScorer);

            IsInitialized = true;
            LoggingController.LogInfo(
                $"[ZoneMovement] Initialization complete. POIs: {pois.Count + spawnPoints.Length + syntheticCount}, Zones: {zones.Count}"
            );
        }
        catch (Exception ex)
        {
            LoggingController.LogError($"[ZoneMovement] Initialization failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Periodically refreshes cached player/bot positions and combat pull points.
    /// </summary>
    protected void Update()
    {
        if (!IsInitialized)
            return;

        if (Time.time - lastConvergenceUpdate < convergenceUpdateInterval)
            return;

        lastConvergenceUpdate = Time.time;

        var allPlayers = Singleton<GameWorld>.Instance?.AllAlivePlayersList;
        if (allPlayers == null)
            return;

        cachedPlayerPositions.Clear();
        cachedBotPositions.Clear();

        for (int i = 0; i < allPlayers.Count; i++)
        {
            var player = allPlayers[i];
            if (player == null || !player.HealthController.IsAlive)
                continue;

            if (player.IsAI)
                cachedBotPositions.Add(player.Position);
            else
                cachedPlayerPositions.Add(player.Position);
        }

        // Gather combat events into pull points with linear decay
        RefreshCombatPullPoints(Time.time);

        LoggingController.LogDebug(
            "[WorldGridManager] Convergence update: bots="
                + cachedBotPositions.Count
                + " players="
                + cachedPlayerPositions.Count
                + " combatEvents="
                + combatPullCount
                + " raidTime="
                + RaidTimeNormalized.ToString("F2")
        );
    }

    /// <summary>
    /// Scans CombatEventRegistry for recent active events and populates the combat pull buffer.
    /// Each event's strength is linearly decayed based on age.
    /// </summary>
    private void RefreshCombatPullPoints(float currentTime)
    {
        combatPullCount = 0;
        if (combatConvergenceDecaySec <= 0f)
            return;

        combatPullCount = CombatEventRegistry.GatherCombatPull(
            combatPullBuffer,
            currentTime,
            combatConvergenceDecaySec,
            combatConvergenceForce
        );
    }

    /// <summary>
    /// Returns the grid cell containing the given world position.
    /// </summary>
    /// <param name="position">World-space position.</param>
    /// <returns>The grid cell, or null if the grid is not initialized.</returns>
    public GridCell GetCellForBot(Vector3 position)
    {
        if (!IsInitialized)
            return null;
        return Grid.GetCell(position);
    }

    /// <summary>
    /// Computes the best next destination for a bot using live field state.
    /// Uses cached player/bot positions from the most recent <see cref="Update"/> tick.
    /// </summary>
    /// <param name="botPosition">The bot's current world position.</param>
    /// <param name="momentumX">X component of the bot's current travel direction.</param>
    /// <param name="momentumZ">Z component of the bot's current travel direction.</param>
    /// <returns>The recommended destination position, or <c>null</c> if not initialized.</returns>
    public Vector3? GetRecommendedDestination(Vector3 botPosition, float momentumX, float momentumZ)
    {
        if (!IsInitialized)
            return null;

        var currentCell = Grid.GetCell(botPosition);
        if (currentCell == null)
            return null;

        GetCompositeDirection(botPosition, momentumX, momentumZ, cachedBotPositions, cachedPlayerPositions, out float dirX, out float dirZ);

        var targetCell = destinationSelector.SelectDestination(Grid, currentCell, dirX, dirZ, botPosition);
        return targetCell?.Center;
    }

    /// <summary>
    /// Computes the best next destination for a bot using per-bot tracked momentum and noise.
    /// Uses <see cref="BotFieldState"/> to give each bot a unique field composition result,
    /// eliminating herd movement.
    /// </summary>
    /// <param name="botProfileId">The bot's unique profile ID (for per-bot state lookup).</param>
    /// <param name="botPosition">The bot's current world position.</param>
    /// <returns>The recommended destination position, or <c>null</c> if not initialized.</returns>
    public Vector3? GetRecommendedDestination(string botProfileId, Vector3 botPosition)
    {
        if (!IsInitialized)
            return null;

        var currentCell = Grid.GetCell(botPosition);
        if (currentCell == null)
            return null;

        // Phase 6: field state stored on ECS entity, accessed via BotEntityBridge
        var state = BotEntityBridge.GetFieldState(botProfileId);
        if (state == null)
            return null;
        var (momX, momZ) = state.ComputeMomentum(botPosition);

        // Get advection (zone attraction + crowd repulsion)
        advectionField.GetAdvection(botPosition, cachedBotPositions, out float advX, out float advZ);

        // Get convergence (bot clustering + combat events)
        convergenceField.GetConvergence(
            botPosition,
            cachedBotPositions,
            combatPullBuffer,
            combatPullCount,
            Time.time,
            out float convX,
            out float convZ
        );

        // Per-bot noise instead of global random
        float noiseAngle = state.GetNoiseAngle(Time.time);

        // Apply time-based convergence multiplier
        float timeMultiplier = ConvergenceTimeWeight.ComputeMultiplier(RaidTimeNormalized);
        fieldComposer.GetCompositeDirection(
            advX,
            advZ,
            convX,
            convZ,
            momX,
            momZ,
            noiseAngle,
            timeMultiplier,
            out float dirX,
            out float dirZ
        );

        var targetCell = destinationSelector.SelectDestination(Grid, currentCell, dirX, dirZ, botPosition);
        if (targetCell != null)
        {
            state.PreviousDestination = targetCell.Center;
            return targetCell.Center;
        }

        return null;
    }

    /// <summary>
    /// Computes the composite movement direction at a position, combining advection,
    /// convergence, momentum, and noise.
    /// </summary>
    /// <param name="position">The bot's current position.</param>
    /// <param name="momentumX">X component of the bot's current travel direction.</param>
    /// <param name="momentumZ">Z component of the bot's current travel direction.</param>
    /// <param name="botPositions">Positions of other bots (for crowd repulsion and convergence clustering).</param>
    /// <param name="playerPositions">Positions of human players (unused, kept for API compatibility).</param>
    /// <param name="outX">X component of the composite direction.</param>
    /// <param name="outZ">Z component of the composite direction.</param>
    public void GetCompositeDirection(
        Vector3 position,
        float momentumX,
        float momentumZ,
        List<Vector3> botPositions,
        List<Vector3> playerPositions,
        out float outX,
        out float outZ
    )
    {
        outX = 0f;
        outZ = 0f;

        if (!IsInitialized)
            return;

        // Get advection (zone attraction + crowd repulsion)
        advectionField.GetAdvection(position, botPositions, out float advX, out float advZ);

        // Get convergence (bot clustering + combat events)
        convergenceField.GetConvergence(
            position,
            botPositions,
            combatPullBuffer,
            combatPullCount,
            Time.time,
            out float convX,
            out float convZ
        );

        // Compose with noise and time-based convergence multiplier
        float noiseAngle = UnityEngine.Random.Range(-Mathf.PI, Mathf.PI);
        float timeMultiplier = ConvergenceTimeWeight.ComputeMultiplier(RaidTimeNormalized);
        fieldComposer.GetCompositeDirection(advX, advZ, convX, convZ, momentumX, momentumZ, noiseAngle, timeMultiplier, out outX, out outZ);
    }

    /// <summary>
    /// Selects the best neighboring cell for a bot to move to.
    /// </summary>
    /// <param name="currentCell">The cell the bot is currently in.</param>
    /// <param name="compositeDirX">X component of the composite direction.</param>
    /// <param name="compositeDirZ">Z component of the composite direction.</param>
    /// <param name="botPosition">The bot's current world position.</param>
    /// <returns>The best destination cell, or currentCell if no better option.</returns>
    public GridCell SelectDestination(GridCell currentCell, float compositeDirX, float compositeDirZ, Vector3 botPosition)
    {
        if (!IsInitialized)
            return currentCell;

        return destinationSelector.SelectDestination(Grid, currentCell, compositeDirX, compositeDirZ, botPosition);
    }
}
