# Assembly Field Manifest

Generated from `Assembly-CSharp.dll` using AssemblyInspector.
Baseline for detecting field name changes across game updates.

## Summary

- **35** game types inspected
- **1309** total fields cataloged
- **12** actively referenced fields (via ReflectionHelper, Harmony `___param`, or AccessTools)
- **25** potentially useful fields identified for future improvements (8 high, 11 medium, 6 low priority)
- Generated: 2026-02-11

## Legend

- **[WATCHED]** = Field actively referenced by our code (via reflection or Harmony field injection). Must be monitored for renames across game updates.
- **Referenced by** = Which of our source files use this type.

---

## Types with Watched Fields

These types have fields we access via reflection or Harmony `___param` injection.
Any rename here will break functionality at runtime.

### BotSpawner

**Full name:** EFT.BotSpawner | **Base:** System.Object | **Fields:** 27
**Referenced by:** ReflectionHelper (`Bots`, `OnBotRemoved`, `AllPlayers`), BotDiedPatch (`___Bots`, `___OnBotRemoved`), GetAllBossPlayersPatch (`___AllPlayers`), TryToSpawnInZoneAndDelayPatch, TrySpawnFreeAndDelayPatch, BotDiedPatch

| # | Vis | Type | Name | Status |
|---|-----|------|------|--------|
| 0 | public | BossSpawnerClass | BossSpawner | |
| 1 | public | BotsClass | Bots | **[WATCHED]** |
| 2 | public | BotZone[] | AllBotZones | |
| 3 | public | BotZone[] | OpenedZones | |
| 4 | public | Dictionary\<PatrolPoint, BotZone\> | ZonesPatrols | |
| 5 | public | Dictionary\<PatrolPoint, BotZone\> | ZonesPatrolsSnipe | |
| 6 | public | GClass1885 | SpawnDelaysService | |
| 7 | public | List\<Player\> | AllPlayers | **[WATCHED]** |
| 8 | public | bool | GameEnd | |
| 9 | public | bool | FreeForAll | |
| 10 | public | IBotCreator | BotCreator | |
| 11 | public | DeadBodiesController | DeadBodiesController | |
| 12 | public | ISpawnSystem | SpawnSystem | |
| 13 | public | int | AllBotsCount | |
| 14 | public | int | FollowersBotsCount | |
| 15 | public | int | BossBotsCount | |
| 16 | public | int | InSpawnProcess | |
| 17 | public | HashSet\<string\> | DeletedPlayers | |
| 18 | public | HashSet\<string\> | AddedPlayers | |
| 19 | public | HashSet\<WildSpawnType\> | BlockedRoles | |
| 20 | public | CancellationTokenSource | CancellationTokenSource | |
| 21 | private | Action\<BotOwner\> | OnBotCreated | |
| 22 | private | Action\<BotOwner\> | OnBotRemoved | **[WATCHED]** |
| 23 | private | Action\<GClass1888\> | OnSpawnedWave | |
| 24 | public | int | \<MaxBots\>k\_\_BackingField | |
| 25 | public | BotZoneGroupsDictionary | \<Groups\>k\_\_BackingField | |
| 26 | public | IBotGame | \<BotGame\>k\_\_BackingField | |

---

### BossGroup

**Full name:** BossGroup | **Base:** System.Object | **Fields:** 1
**Referenced by:** ReflectionHelper (`Boss_1`), SetNewBossPatch (`___Boss_1`)

| # | Vis | Type | Name | Status |
|---|-----|------|------|--------|
| 0 | public | BotOwner | Boss_1 | **[WATCHED]** |

---

### AirdropLogicClass

**Full name:** AirdropLogicClass | **Base:** System.Object | **Fields:** 26
**Referenced by:** ReflectionHelper (`AirdropSynchronizableObject_0`), AirdropLandPatch (`___AirdropSynchronizableObject_0`)

| # | Vis | Type | Name | Status |
|---|-----|------|------|--------|
| 0 | public | OfflineAirdropServerLogicClass | OfflineServerLogic | |
| 1 | public | bool | offlineMode | |
| 2 | public | string | String_0 | |
| 3 | public | int | Int_0 | |
| 4 | public | Vector3 | Vector3_0 | |
| 5 | public | Quaternion | Quaternion_0 | |
| 6 | public | SynchronizableObjectType | SynchronizableObjectType_0 | |
| 7 | public | EAirdropFallingStage | EairdropFallingStage_0 | |
| 8 | public | EAirdropFallingStage | EairdropFallingStage_1 | |
| 9 | public | AirdropSynchronizableObject | AirdropSynchronizableObject_0 | **[WATCHED]** |
| 10 | public | bool | Bool_0 | |
| 11 | public | GameObject | GameObject_0 | |
| 12 | public | Material | Material_0 | |
| 13 | public | GameObject | GameObject_1 | |
| 14 | public | GameObject | GameObject_2 | |
| 15 | public | bool | Bool_1 | |
| 16 | public | List\<Collider\> | List_0 | |
| 17 | public | ESurfaceSound | EsurfaceSound_0 | |
| 18 | public | bool | Bool_2 | |
| 19 | public | RaycastHit | RaycastHit_0 | |
| 20 | public | AirdropSurfaceSet | AirdropSurfaceSet_0 | |
| 21 | public | Dictionary\<ESurfaceSound, AirdropSurfaceSet\> | Dictionary_0 | |
| 22 | public | BetterSource | BetterSource_0_1 | |
| 23 | public | Coroutine | Coroutine_0 | |
| 24 | public | Renderer[] | Renderer_0 | |
| 25 | public | SkinnedMeshRenderer[] | SkinnedMeshRenderer_0 | |

---

### LighthouseTraderZone

**Full name:** EFT.Interactive.LighthouseTraderZone | **Base:** EFT.Interactive.BaseRestrictableZone | **Fields:** 5
**Referenced by:** ReflectionHelper (`physicsTriggerHandler_0`), LighthouseTraderZonePlayerAttackPatch (`___physicsTriggerHandler_0`), LighthouseTraderZoneAwakePatch (`___physicsTriggerHandler_0`)

| # | Vis | Type | Name | Status |
|---|-----|------|------|--------|
| 0 | private | PhysicsTriggerHandler | physicsTriggerHandler_0 | **[WATCHED]** |
| 1 | private | Action\<string, bool\> | action_0 | |
| 2 | private | EquipmentSlot[] | equipmentSlot_0 | |
| 3 | private | WildSpawnType[] | wildSpawnType_0 | |
| 4 | private | string | string_0 | |

---

### BotCurrentPathAbstractClass

**Full name:** BotCurrentPathAbstractClass | **Base:** System.Object | **Fields:** 5
**Referenced by:** ReflectionHelper (`Vector3_0`), BotPathingHelpers

| # | Vis | Type | Name | Status |
|---|-----|------|------|--------|
| 0 | public | int | Int_0 | |
| 1 | public | Vector3[] | Vector3_0 | **[WATCHED]** |
| 2 | public | Vector3 | Vector3_1 | |
| 3 | public | float | Float_0 | |
| 4 | public | Action\<int, Vector3[]\> | onPathCornerChanged | |

---

### NonWavesSpawnScenario

**Full name:** EFT.NonWavesSpawnScenario | **Base:** UnityEngine.MonoBehaviour | **Fields:** 15
**Referenced by:** ReflectionHelper (`float_2`), TrySpawnFreeAndDelayPatch, NonWavesSpawnScenarioCreatePatch

| # | Vis | Type | Name | Status |
|---|-----|------|------|--------|
| 0 | private | AbstractGame | abstractGame_0 | |
| 1 | private | Location | location_0 | |
| 2 | private | BotsController | botsController_0 | |
| 3 | private | GClass1881\<BotDifficulty\> | gclass1881_0 | |
| 4 | private | GClass1881\<WildSpawnType\> | gclass1881_1 | |
| 5 | private | GClass1876 | gclass1876_0 | |
| 6 | private | bool | bool_0 | |
| 7 | private | bool | bool_1 | |
| 8 | private | bool | bool_2 | |
| 9 | private | float? | nullable_0 | |
| 10 | private | float | float_0 | |
| 11 | private | float | float_1 | |
| 12 | private | float | float_2 | **[WATCHED]** |
| 13 | private | WaveInfoClass[] | gclass1879_0 | |
| 14 | private | bool | bool_3 | |

---

### LocalGame

**Full name:** EFT.LocalGame | **Base:** EFT.BaseLocalGame\<EFT.EftGamePlayerOwner\> | **Fields:** 4
**Referenced by:** ReflectionHelper (`wavesSpawnScenario_0`), GameStartPatch

| # | Vis | Type | Name | Status |
|---|-----|------|------|--------|
| 0 | private | BossSpawnScenario | bossSpawnScenario_0 | |
| 1 | private | WavesSpawnScenario | wavesSpawnScenario_0 | **[WATCHED]** |
| 2 | private | NonWavesSpawnScenario | nonWavesSpawnScenario_0 | |
| 3 | private | Dictionary\<string, Player\> | dictionary_2 | |

---

### BotsGroup

**Full name:** BotsGroup | **Base:** System.Object | **Fields:** 54
**Referenced by:** ReflectionHelper (`<BotZone>k__BackingField`), GoToPositionAbstractAction, BotsGroupIsPlayerEnemyPatch, AddEnemyPatch

| # | Vis | Type | Name | Status |
|---|-----|------|------|--------|
| 0 | public | IBotGame | BotGame | |
| 1 | public | Dictionary\<IPlayer, BotSettingsClass\> | Enemies | |
| 2 | public | Dictionary\<IPlayer, BotSettingsClass\> | Neutrals | |
| 3 | public | EPlayerSide | Side | |
| 4 | public | bool | \<Locked\>k\_\_BackingField | |
| 5 | public | BotCurrentEnemiesClass | CurrentEnemies | |
| 6 | public | List\<BotOwner\> | Members | |
| 7 | public | List\<GClass578\> | GrenadeSmokePlaces | |
| 8 | public | float | NextGetGoalTime | |
| 9 | public | float | NextCheckDestination | |
| 10 | public | GClass573 | GroupDangerAreas | |
| 11 | public | WildSpawnType | DefWildSpawnType | |
| 12 | public | BotGlobalsMindSettings | InitialBotMindSettings_1 | |
| 13 | public | List\<string\> | EnemyPlayerGroups | |
| 14 | public | bool | EnemyByGroupsPmcPlayers | |
| 15 | public | bool | EnemyByGroupsSavagePlayers | |
| 16 | public | BotOwner | InitialBot | |
| 17 | public | bool | IsFirstMemberAdded | |
| 18 | public | List\<IPlayer\> | RecheckPersonsAfterInit | |
| 19 | private | Action\<IPlayer, EBotEnemyCause\> | OnEnemyAdd | |
| 20 | private | Action\<IPlayer\> | OnEnemyRemove | |
| 21 | private | Action\<IPlayer\> | OnAddNeutral | |
| 22 | private | GDelegate6 | OnReportEnemy | |
| 23 | private | Action\<BotOwner\> | OnBossSetted | |
| 24 | private | Action\<BotOwner\> | OnMemberRemove | |
| 25 | private | Action\<BotOwner\> | OnMemberAdd | |
| 26 | public | WildSpawnType | \<InitialBotType\>k\_\_BackingField | |
| 27 | public | BotDifficulty | \<InitialBotDifficulty\>k\_\_BackingField | |
| 28 | public | BotSettingsComponents | \<InitialFileSettings\>k\_\_BackingField | |
| 29 | public | List\<IPlayer\> | \<Allies\>k\_\_BackingField | |
| 30 | public | BossGroup | \<BossGroup\>k\_\_BackingField | |
| 31 | public | float | \<EnemyLastSeenTimeSence\>k\_\_BackingField | |
| 32 | public | float | \<EnemyLastSeenTimeReal\>k\_\_BackingField | |
| 33 | public | BotsGroupMarkOfUnknown | \<GroupMarkOfUnknown\>k\_\_BackingField | |
| 34 | public | BotsGroupGrenade | \<GroupGrenade\>k\_\_BackingField | |
| 35 | public | BotsGroupLaying | \<GroupLaying\>k\_\_BackingField | |
| 36 | public | BotGroupTalk | \<GroupTalk\>k\_\_BackingField | |
| 37 | public | Vector3 | \<EnemyLastSeenPositionReal\>k\_\_BackingField | |
| 38 | public | Vector3 | \<EnemyLastSeenPositionSence\>k\_\_BackingField | |
| 39 | public | LastSoundsController | \<LastSoundsController\>k\_\_BackingField | |
| 40 | public | CoverPointMaster | \<CoverPointMaster\>k\_\_BackingField | |
| 41 | public | BotZone | \<BotZone\>k\_\_BackingField | **[WATCHED]** |
| 42 | public | DeadBodiesController | \<DeadBodiesController\>k\_\_BackingField | |
| 43 | public | BotGroupWarnData | \<BotGroupWarnData\>k\_\_BackingField | |
| 44 | public | string | \<Name\>k\_\_BackingField | |
| 45 | public | int | \<Id\>k\_\_BackingField | |
| 46 | public | bool | \<IsLastPositionOld\>k\_\_BackingField | |
| 47 | public | BotGroupRequestController | \<RequestsController\>k\_\_BackingField | |
| 48 | public | bool | \<ForcedAggressiveForNewPlayers\>k\_\_BackingField | |
| 49 | public | List\<PlaceForCheck\> | \<PlacesForCheck\>k\_\_BackingField | |
| 50 | public | bool | \<AnyBodyShootImmediately\>k\_\_BackingField | |
| 51 | public | int | \<TargetMembersCount\>k\_\_BackingField | |
| 52 | public | bool | \<IsFull\>k\_\_BackingField | |
| 53 | public | int | GroupsIds | |

---

### GClass680 (BotsPresets base type)

**Full name:** GClass680 | **Base:** System.Object | **Fields:** 3
**Referenced by:** PScavProfilePatch (accessed via `typeof(BotsPresets).BaseType`, field `List_0`)

| # | Vis | Type | Name | Status |
|---|-----|------|------|--------|
| 0 | public | int | BACKEND_MAX | |
| 1 | public | List\<Profile\> | List_0 | **[WATCHED]** |
| 2 | public | bool | Bool_0 | |

---

### AICoreStrategyAbstractClass\<T\>

**Full name:** AICoreStrategyAbstractClass | **Base:** System.Object | **Fields:** 5
**Referenced by:** LogicLayerMonitor (field `List_0` for brain layer list)

| # | Vis | Type | Name | Status |
|---|-----|------|------|--------|
| 0 | public | Dictionary\<int, AICoreLayerClass\<T\>\> | Dictionary_0 | |
| 1 | public | List\<AICoreLayerClass\<T\>\> | List_0 | **[WATCHED]** |
| 2 | public | GClass32 | Gclass32_0 | |
| 3 | public | Action\<AICoreLayerClass\<T\>\> | Action_0 | |
| 4 | public | AICoreLayerClass\<T\> | Gclass35_0 | |

---

## Patch-Target Types (No Field Injection)

These types are Harmony patch targets only. We patch their methods but do not
access their fields via reflection or `___param` injection. Included for
completeness; field renames here do not directly break our code (method renames would).

### Player

**Full name:** EFT.Player | **Base:** UnityEngine.MonoBehaviour | **Fields:** 410
**Referenced by:** OnMakingShotPatch, OnBeenKilledByAggressorPatch, EnableVaultPatch

*Field listing omitted (410 fields). Run `make inspect TYPE=Player` to view.*

---

### TarkovApplication

**Full name:** EFT.TarkovApplication | **Base:** EFT.CommonClientApplication\<ISession\> | **Fields:** 25
**Referenced by:** TarkovInitPatch

| # | Vis | Type | Name |
|---|-----|------|------|
| 0 | private | SplashScreenPanel | _splashScreenPanel |
| 1 | private | Camera | _temporaryCamera |
| 2 | private | GameDateTime | _localGameDateTime |
| 3 | public | bool | UnlockAndShowAllLocations |
| 4 | private | bool | _customRaidSettings |
| 5 | private | RaidSettings | _raidSettings |
| 6 | public | FastGameInfo | FastGameInfo_EXPERIMENTAL |
| 7 | private | LocalRaidSettings | localRaidSettings_0 |
| 8 | private | HideoutClass | hideoutClass |
| 9 | private | UIInputRoot | uiinputRoot_0 |
| 10 | private | EmptyInputNode | emptyInputNode_0 |
| 11 | private | MainMenuControllerClass | mainMenuControllerClass |
| 12 | private | string | string_0 |
| 13 | private | bool | bool_3 |
| 14 | private | Task | task_0 |
| 15 | private | WaitForSeconds | waitForSeconds_0 |
| 16 | private | Coroutine | coroutine_0 |
| 17 | private | string | string_1 |
| 18 | private | int | int_0 |
| 19 | private | int | int_1 |
| 20 | private | GClass2301 | gclass2301_0 |
| 21 | private | GClass2302 | gclass2302_0 |
| 22 | private | Action | action_0 |
| 23 | private | CompositeDisposableClass | compositeDisposableClass |
| 24 | public | TransitionStatusStruct | transitionStatus |

---

### BotOwner

**Full name:** EFT.BotOwner | **Base:** UnityEngine.MonoBehaviour | **Fields:** 138
**Referenced by:** BotOwnerBrainActivatePatch (`method_10`), BotOwnerSprintPatch (`Sprint`)

*Field listing abbreviated. Key fields:*

| # | Vis | Type | Name |
|---|-----|------|------|
| 6 | private | Action\<BotOwner\> | _botDiedCallback |
| 16 | public | BotMemoryClass | Memory |
| 17 | public | BotDifficultySettingsClass | Settings |
| 21 | private | StandartBotBrain | \<Brain\>k\_\_BackingField |
| 34 | private | BotBoss | \<Boss\>k\_\_BackingField |
| 97 | private | BotWeaponManager | \<WeaponManager\>k\_\_BackingField |
| 121 | private | BotMover | \<Mover\>k\_\_BackingField |
| 125 | private | BotsGroup | \<BotsGroup\>k\_\_BackingField |
| 130 | private | Player | \<GetPlayer\>k\_\_BackingField |

*Run `make inspect TYPE=BotOwner` to view all 138 fields.*

---

### GameWorld

**Full name:** EFT.GameWorld | **Base:** UnityEngine.MonoBehaviour | **Fields:** 81
**Referenced by:** ShrinkDoorNavMeshCarversPatch (`OnGameStarted`)

*Field listing abbreviated. Key fields:*

| # | Vis | Type | Name |
|---|-----|------|------|
| 16 | public | GameDateTime | GameDateTime |
| 35 | public | List\<IPlayer\> | RegisteredPlayers |
| 40 | public | List\<Player\> | AllAlivePlayersList |
| 48 | public | Player | MainPlayer |
| 61 | public | GClass705 | MineManager |

*Run `make inspect TYPE=GameWorld` to view all 81 fields.*

---

### BotsController

**Full name:** EFT.BotsController | **Base:** System.Object | **Fields:** 26
**Referenced by:** BotsControllerSetSettingsPatch, BotsControllerStopPatch, ActivateBossesByWavePatch

| # | Vis | Type | Name |
|---|-----|------|------|
| 0 | public | GClass3597 | OnlineDependenceSettings |
| 1 | public | WildSpawnType[] | AllTypes_1 |
| 2 | public | BotsClass | Bots |
| 3 | public | bool | CanSpawn |
| 4 | public | BotSpawner | BotSpawner_1 |
| 5 | public | AICoreControllerClass | AICoreController |
| 6 | public | AITaskManager | \<AiTaskManager\>k\_\_BackingField |
| 7 | public | AIStationaryController | StationaryWeapons |
| 8 | public | BotsEventsController | \<EventsController\>k\_\_BackingField |
| 9 | public | GClass412 | Connections |
| 10 | public | AICoversData | CoversData_1 |
| 11 | public | IBotGame | \<BotGame\>k\_\_BackingField |
| 12 | public | ZoneLeaveControllerClass | ZonesLeaveController |
| 13 | public | GClass1874 | ArtilleryZonesController |
| 14 | public | Dictionary\<GameObject, ELookObstacleType\> | AILayerLookObstaclesCache |
| 15 | public | int | MaxCount |
| 16 | public | BotLocationModifier | BotLocationModifier |
| 17 | public | GClass636 | CutController |
| 18 | public | GClass678 | SpawnControlScenario |
| 19 | public | BotPresetClass[] | BotPresets |
| 20 | public | GClass612[] | BotScatterings |
| 21 | public | IPlayersCollection | \<Players\>k\_\_BackingField |
| 22 | public | BotsPlantedMinesController | \<PlantedMines\>k\_\_BackingField |
| 23 | public | BotTradersServices | BotTradersServices_1 |
| 24 | public | BotSpawnLimiter | \<BotSpawnLimiter\>k\_\_BackingField |
| 25 | public | BotsSmokesVisionSystem | \<BotSmokesVisionSystem\>k\_\_BackingField |

---

### BotsPresets

**Full name:** BotsPresets | **Base:** GClass680 | **Fields:** 6
**Referenced by:** PScavProfilePatch (base type accessed), TryLoadBotsProfilesOnStartPatch

| # | Vis | Type | Name |
|---|-----|------|------|
| 0 | public | int | Int_0 |
| 1 | public | ISession | ISession |
| 2 | public | int | Int_1 |
| 3 | public | List\<WaveInfoClass\> | List_1 |
| 4 | public | List\<WaveInfoClass\> | List_2 |
| 5 | public | GClass684 | Gclass684_0 |

---

### BotBoss

**Full name:** BotBoss | **Base:** GClass429 | **Fields:** 8
**Referenced by:** IsFollowerSuitableForBossPatch (`OfferSelf`)

| # | Vis | Type | Name |
|---|-----|------|------|
| 0 | public | GClass456 | Followers_1 |
| 1 | public | GInterface10 | PatrolMove |
| 2 | private | Action\<BotOwner, List\<BotOwner\>\> | OnBossDead |
| 3 | private | Action\<BotOwner\> | OnBecomeBoss |
| 4 | private | Action\<BotOwner, FollowerStatusChange\> | OnFollowerStatusChange |
| 5 | public | bool | \<IamBoss\>k\_\_BackingField |
| 6 | public | ABossLogic | \<BossLogic\>k\_\_BackingField |
| 7 | public | bool | \<NeedProtection\>k\_\_BackingField |

---

### BossSpawnScenario

**Full name:** BossSpawnScenario | **Base:** System.Object | **Fields:** 7
**Referenced by:** InitBossSpawnLocationPatch

| # | Vis | Type | Name |
|---|-----|------|------|
| 0 | public | List\<IBotTimer\> | Timers |
| 1 | public | Action\<BossLocationSpawn\> | SpawnBossAction |
| 2 | public | List\<WaveInfoClass\> | BotsCountProfiles |
| 3 | public | BossLocationSpawn[] | \<BossSpawnWaves\>k\_\_BackingField |
| 4 | public | GClass675 | QuestsSpanws |
| 5 | public | bool | \<HaveSectants\>k\_\_BackingField |
| 6 | public | bool | IsSubscribed |

---

### MineDirectional

**Full name:** MineDirectional | **Base:** UnityEngine.MonoBehaviour | **Fields:** 6
**Referenced by:** MineDirectionalShouldExplodePatch

| # | Vis | Type | Name |
|---|-----|------|------|
| 0 | private | int | int_0 |
| 1 | public | List\<MineDirectional\> | Mines |
| 2 | private | Lazy\<ISharedBallisticsCalculator\> | lazy_0 |
| 3 | private | MineSettings | _mineData |
| 4 | private | bool | bool_0 |
| 5 | private | bool | bool_1 |

---

### BotMover

**Full name:** BotMover | **Base:** GClass429 | **Fields:** 53
**Referenced by:** BotMoverFixedUpdatePatch (`ManualFixedUpdate`)

*Field listing abbreviated. Run `make inspect TYPE=BotMover` to view all 53 fields.*

---

### MovementContext

**Full name:** EFT.MovementContext | **Base:** System.Object | **Fields:** 187
**Referenced by:** MovementContextIsAIPatch (property `IsAI`)

*Field listing omitted (187 fields). Run `make inspect TYPE=MovementContext` to view.*

---

### BotsClass

**Full name:** BotsClass | **Base:** System.Object | **Fields:** 7
**Referenced by:** GetListByZonePatch (`GetListByZone`)

| # | Vis | Type | Name |
|---|-----|------|------|
| 0 | public | HashSet\<BotOwner\> | HashSet_0 |
| 1 | public | GClass412 | Gclass412_0 |
| 2 | public | HashSet\<int\> | HashSet_1 |
| 3 | private | Action\<BotOwner\> | action_0 |
| 4 | private | Action\<BotOwner\> | action_1 |
| 5 | private | Action\<Player\> | action_2 |
| 6 | public | List\<BotOwner\> | List_0 |

---

### MatchmakerPlayerControllerClass

**Full name:** MatchmakerPlayerControllerClass | **Base:** GClass3926\<EFT.RaidSettings\> | **Fields:** 8
**Referenced by:** TimeHasComeScreenClassChangeStatusPatch (`UpdateMatchingStatus`)

| # | Vis | Type | Name |
|---|-----|------|------|
| 0 | public | ISession | ISession |
| 1 | public | ValueTuple\<string, float?\> | ValueTuple_0 |
| 2 | public | bool | Bool_9 |
| 3 | private | Action | action_17 |
| 4 | public | OnEventClass\<bool\> | Gclass1626_0 |
| 5 | public | BindableStateClass\<bool\> | Gclass1643_2 |
| 6 | private | Action\<string, float?\> | action_18 |
| 7 | private | Action | action_19 |

---

### MenuScreen

**Full name:** EFT.UI.MenuScreen | **Base:** EFT.UI.Screens.EftScreen\<...\> | **Fields:** 16
**Referenced by:** MenuShowPatch (`Show`)

| # | Vis | Type | Name |
|---|-----|------|------|
| 0 | private | DefaultUIButton | _playButton |
| 1 | private | DefaultUIButton | _playerButton |
| 2 | private | DefaultUIButton | _tradeButton |
| 3 | private | DefaultUIButton | _exitButton |
| 4 | private | DefaultUIButton | _disconnectButton |
| 5 | private | DefaultUIButton | _hideoutButton |
| 6 | private | Button | _logoutButton |
| 7 | private | ChangeGameModeButton | _toggleGameModeButton |
| 8 | private | DefaultUIButton | _hideScreenButton |
| 9 | private | GameObject | _warningGameObject |
| 10 | private | GameObject | _alphaWarningGameObject |
| 11 | private | bool | bool_1 |
| 12 | private | EnvironmentUI | environmentUI_0 |
| 13 | private | bool | bool_2 |
| 14 | private | MatchmakerPlayerControllerClass | matchmakerPlayerControllerClass |
| 15 | private | ESessionMode | esessionMode_0 |

---

### AssetPoolObject

**Full name:** EFT.AssetsManager.AssetPoolObject | **Base:** UnityEngine.MonoBehaviour | **Fields:** 14
**Referenced by:** ReturnToPoolPatch (`ReturnToPool`)

| # | Vis | Type | Name |
|---|-----|------|------|
| 0 | private | ObjectPool\<List\<AssetPoolObject\>\> | objectPool_0 |
| 1 | public | List\<string\> | PoolHistory |
| 2 | public | List\<Collider\> | Colliders |
| 3 | public | List\<GInterface504\> | Components |
| 4 | public | List\<Renderer\> | Renderers |
| 5 | private | List\<Component\> | _originallyEnabledComponents |
| 6 | private | bool | bool_0 |
| 7 | public | GClass768 | ContainerCollectionView |
| 8 | private | bool | bool_1 |
| 9 | protected | ResourceTypeStruct | ResourceType |
| 10 | private | GInterface503 | ginterface503_0 |
| 11 | private | bool | bool_2 |
| 12 | public | List\<Component\> | RegisteredComponentsToClean |
| 13 | public | List\<GClass3969\> | RegisteredCollidersToDisable |

---

### EnemyInfo

**Full name:** EnemyInfo | **Base:** System.Object | **Fields:** 46
**Referenced by:** CheckLookEnemyPatch (`CheckLookEnemy`)

*Field listing abbreviated. Run `make inspect TYPE=EnemyInfo` to view all 46 fields.*

---

### BaseLocalGame\<T\>

**Full name:** EFT.BaseLocalGame\<T\> | **Base:** EFT.AbstractGame | **Fields:** 30
**Referenced by:** GameStartPatch (`vmethod_5`)

*Field listing abbreviated. Run `make inspect TYPE=BaseLocalGame` to view all 30 fields.*

---

### AbstractGame

**Full name:** EFT.AbstractGame | **Base:** UnityEngine.MonoBehaviour | **Fields:** 13
**Referenced by:** NonWavesSpawnScenarioCreatePatch (parameter type)

| # | Vis | Type | Name |
|---|-----|------|------|
| 0 | protected | string | HALLOWEEN_PREFAB_PATH |
| 1 | private | bool | bool_0 |
| 2 | private | WaitForFixedUpdate | waitForFixedUpdate_0 |
| 3 | protected | CompositeDisposableClass | CompositeDisposable |
| 4 | private | GameStatus | gameStatus_0 |
| 5 | private | GameTimerClass | gameTimerClass |
| 6 | private | EGameType | egameType_0 |
| 7 | private | float | float_0 |
| 8 | private | bool | bool_1 |
| 9 | private | EUpdateQueue | eupdateQueue_0 |
| 10 | private | Action\<EGameType\> | action_0 |
| 11 | public | float | MAX_FIXED_DELTA_TIME |
| 12 | private | Action\<string, float?\> | action_1 |

---

## Supporting Types (Parameter / Value Types)

These types appear as method parameters, field value types, or are referenced
indirectly. No fields are directly accessed via reflection.

### WavesSpawnScenario

**Full name:** EFT.WavesSpawnScenario | **Base:** UnityEngine.MonoBehaviour | **Fields:** 7
**Referenced by:** GameStartPatch (retrieved via reflection from `LocalGame.wavesSpawnScenario_0`)

| # | Vis | Type | Name |
|---|-----|------|------|
| 0 | public | BotLocationModifier | BotLocationModifier |
| 1 | private | List\<IBotTimer\> | list_0 |
| 2 | private | Func\<BotWaveDataClass, Task\> | func_0 |
| 3 | private | Dictionary\<WildSpawnType, int\> | dictionary_0 |
| 4 | public | List\<WaveInfoClass\> | BotsCountProfiles |
| 5 | private | BotWaveDataClass[] | gclass1880_0 |
| 6 | private | bool | bool_0 |

---

### AirdropSynchronizableObject

**Full name:** EFT.SynchronizableObjects.AirdropSynchronizableObject | **Base:** EFT.SynchronizableObjects.SynchronizableObject | **Fields:** 13
**Referenced by:** AirdropLandPatch (type of `___AirdropSynchronizableObject_0` field)

| # | Vis | Type | Name |
|---|-----|------|------|
| 0 | public | AirdropViewData[] | AirdropViews |
| 1 | public | EAirdropType | AirdropType |
| 2 | public | TaggedClip | SqueakClip |
| 3 | public | TaggedClip | FlareSound |
| 4 | public | GameObject | AirdropFlare |
| 5 | public | GameObject | AirdropDust |
| 6 | public | GameObject | Parachute |
| 7 | public | GameObject | ParachuteJoint |
| 8 | public | BoxCollider | CollisionCollider |
| 9 | public | MeshRenderer[] | DecalRenderers |
| 10 | public | Material | GunDecal |
| 11 | public | Material | MedicineDecal |
| 12 | public | Material | SupplyDecal |

---

### PhysicsTriggerHandler

**Full name:** PhysicsTriggerHandler | **Base:** UnityEngine.MonoBehaviour | **Fields:** 3
**Referenced by:** LighthouseTraderZonePlayerAttackPatch, LighthouseTraderZoneAwakePatch (type of `___physicsTriggerHandler_0` field)

| # | Vis | Type | Name |
|---|-----|------|------|
| 0 | private | Action\<Collider\> | action_0 |
| 1 | private | Action\<Collider\> | action_1 |
| 2 | public | Collider | trigger |

---

### BotCreationDataClass

**Full name:** BotCreationDataClass | **Base:** System.Object | **Fields:** 12
**Referenced by:** TrySpawnFreeAndDelayPatch (parameter), PScavProfilePatch (parameter), ServerRequestPatch

| # | Vis | Type | Name |
|---|-----|------|------|
| 0 | public | List\<GClass682\> | List_0 |
| 1 | public | IGetProfileData | _profileData |
| 2 | public | List\<Profile\> | List_1 |
| 3 | public | int | Id |
| 4 | public | int | Int_0 |
| 5 | public | int | ProfilesLoadingProcess |
| 6 | public | IBotCreator | IBotCreator |
| 7 | public | GInterface22 | Ginterface22_0 |
| 8 | public | HashSet\<string\> | HashSet_0 |
| 9 | public | HashSet\<string\> | HashSet_1 |
| 10 | public | List\<string\> | List_2 |
| 11 | public | CancellationTokenSource | CancellationTokenSource_0 |

---

### WaveInfoClass

**Full name:** WaveInfoClass | **Base:** System.Object | **Fields:** 3
**Referenced by:** TryLoadBotsProfilesOnStartPatch (parameter), ServerRequestPatch

| # | Vis | Type | Name |
|---|-----|------|------|
| 0 | public | WildSpawnType | Role |
| 1 | public | int | Limit |
| 2 | public | BotDifficulty | Difficulty |

---

### BossLocationSpawn

**Full name:** BossLocationSpawn | **Base:** System.Object | **Fields:** 31
**Referenced by:** ActivateBossesByWavePatch (parameter), GameStartPatch, InitBossSpawnLocationPatch

| # | Vis | Type | Name |
|---|-----|------|------|
| 0 | public | string | BossName |
| 1 | public | float | BossChance |
| 2 | public | string | BossZone |
| 3 | public | bool | BossPlayer |
| 4 | public | string | BossDifficult |
| 5 | public | string | BossEscortDifficult |
| 6 | public | string | BossEscortType |
| 7 | public | string | BossEscortAmount |
| 8 | public | float | Time |
| 9 | public | float | Delay |
| 10 | public | string | TriggerId |
| 11 | public | string | TriggerName |
| 12 | public | bool | IgnoreMaxBots |
| 13 | public | bool | ForceSpawn |
| 14 | public | bool | DependKarma |
| 15 | public | Vector3? | PerfectPos |
| 16 | public | WildSpawnSupports[] | Supports |
| 17 | public | bool | ShowOnTarkovMap |
| 18 | public | bool | ShowOnTarkovMapPvE |
| 19 | public | List\<BossLocationSpawnSubData\> | SubDatas |
| 20 | public | float | MIN_KARMA_TO_RECALC |
| 21 | public | List\<BotZone\> | PossibleShuffledZones |
| 22 | public | WildSpawnType | \<BossType\>k\_\_BackingField |
| 23 | public | WildSpawnType | \<EscortType\>k\_\_BackingField |
| 24 | public | int | \<EscortCount\>k\_\_BackingField |
| 25 | public | string | \<BornZone\>k\_\_BackingField |
| 26 | public | bool | \<ShallSpawn\>k\_\_BackingField |
| 27 | public | BotDifficulty | \<BossDif\>k\_\_BackingField |
| 28 | public | BotDifficulty | \<EscortDif\>k\_\_BackingField |
| 29 | public | SpawnTriggerType | \<TriggerType\>k\_\_BackingField |
| 30 | public | bool | \<Activated\>k\_\_BackingField |

---

### GClass699

**Full name:** GClass699 | **Base:** System.Object | **Fields:** 14
**Referenced by:** SpawnPointIsValidPatch (parameter type)

| # | Vis | Type | Name |
|---|-----|------|------|
| 0 | public | List\<GClass698\> | List_0 |
| 1 | public | List\<GClass698\> | List_1 |
| 2 | public | List\<GClass698\> | List_2 |
| 3 | public | bool | Bool_0 |
| 4 | public | Dictionary\<string, GClass693\> | Dictionary_0 |
| 5 | public | GClass693 | Gclass693_0 |
| 6 | public | GClass696 | Gclass696_0 |
| 7 | public | GClass701 | Gclass701_0 |
| 8 | public | GClass698 | Gclass698_0 |
| 9 | public | ISpawnPoint | IspawnPoint_0 |
| 10 | public | IEnumerable\<ISpawnPoint\> | Ienumerable_0 |
| 11 | public | string | String_0 |
| 12 | public | string | String_1 |
| 13 | public | ISpawnPoint | IspawnPoint_1 |

---

## Watched Fields Summary

Quick reference of all fields that must be monitored for renames across game updates.
These are accessed at runtime via `ReflectionHelper.RequireField`, `AccessTools.Field`, or Harmony `___param` injection.

| Type | Field | Access Method | Used By |
|------|-------|---------------|---------|
| BotCurrentPathAbstractClass | `Vector3_0` | RequireField | BotPathingHelpers |
| NonWavesSpawnScenario | `float_2` | RequireField | TrySpawnFreeAndDelayPatch |
| LocalGame | `wavesSpawnScenario_0` | RequireField | GameStartPatch |
| BotsGroup | `<BotZone>k__BackingField` | RequireField | GoToPositionAbstractAction |
| BossGroup | `Boss_1` | Harmony ___param | SetNewBossPatch |
| BotSpawner | `Bots` | Harmony ___param | BotDiedPatch |
| BotSpawner | `OnBotRemoved` | Harmony ___param | BotDiedPatch |
| BotSpawner | `AllPlayers` | Harmony ___param | GetAllBossPlayersPatch |
| AirdropLogicClass | `AirdropSynchronizableObject_0` | Harmony ___param | AirdropLandPatch |
| LighthouseTraderZone | `physicsTriggerHandler_0` | Harmony ___param | LighthouseTraderZonePlayerAttackPatch, LighthouseTraderZoneAwakePatch |
| GClass680 | `List_0` | RequireField | PScavProfilePatch |
| AICoreStrategyAbstractClass\<T\> | `List_0` | RequireField | LogicLayerMonitor |

All 12 watched fields are registered in `ReflectionHelper.KnownFields` (10 statically, 2 dynamically in LogicLayerMonitor/PScavProfilePatch) and validated at startup.

---

## Discovery: Potentially Useful Fields

Analysis of game type field layouts to identify fields we are NOT currently using
but COULD leverage to improve bot behavior. Organized by priority.

### High Priority (Immediate Value)

#### 1. Player.PlaceItemZone

| Property | Value |
|----------|-------|
| **Type** | `Player` |
| **Field** | `<PlaceItemZone>k__BackingField` (field #312) |
| **Field type** | `PlaceItemTrigger` |
| **What it does** | Tracks the "place item" trigger zone the player is currently in. Set by the game when a player enters a zone where quest items can be planted. |
| **How we could use it** | The `PlantItem` action currently navigates bots to plant positions. Reading this field could let us verify a bot has actually entered the plant zone before attempting the plant interaction, reducing failed plant attempts. Could also be used to discover plant zones dynamically instead of relying on static quest position data. |
| **Priority** | **High** |

#### 2. BotOwner.Exfiltration + LeaveData

| Property | Value |
|----------|-------|
| **Type** | `BotOwner` |
| **Fields** | `<Exfiltration>k__BackingField` (BotExfiltrationData), `<LeaveData>k__BackingField` (BotLeaveData) |
| **Field types** | `BotExfiltrationData`, `BotLeaveData` |
| **What it does** | `Exfiltration` tracks the bot's assigned extraction point and extraction state. `LeaveData` manages the bot's decision to leave the raid (timer, urgency). |
| **How we could use it** | Currently our extraction logic (SAIN interop `ExtractBot`/`TrySetExfilForBot`) relies on an external mod. These built-in fields could provide a fallback extraction system or supplement SAIN's extraction by reading what extract the game assigned. Could also be used to create a "late raid" behavior where bots head to extract when `LeaveData` triggers. |
| **Priority** | **High** |

#### 3. BotsGroup.EnemyLastSeenTimeSence / EnemyLastSeenTimeReal

| Property | Value |
|----------|-------|
| **Type** | `BotsGroup` |
| **Fields** | `<EnemyLastSeenTimeSence>k__BackingField` (field #31), `<EnemyLastSeenTimeReal>k__BackingField` (field #32) |
| **Field type** | `float` (game time timestamps) |
| **What it does** | Tracks when the group last saw an enemy — `Sence` is the perceived time, `Real` is the actual time. The delta between "now" and these values tells you how long it's been since the group was in combat. |
| **How we could use it** | The Questing layer (priority 18) currently yields to higher-priority combat layers. When combat ends and the questing layer resumes, we have no information about how recently the bot was in combat. These fields would let us implement a "cool-down" period: bots could move cautiously, check corners, or wait before resuming their quest objective after a firefight. Could also be used in the `Regrouping` layer to decide when followers should regroup after combat. |
| **Priority** | **High** |

#### 4. BotsGroup.EnemyLastSeenPositionReal

| Property | Value |
|----------|-------|
| **Type** | `BotsGroup` |
| **Fields** | `<EnemyLastSeenPositionReal>k__BackingField` (field #37), `<EnemyLastSeenPositionSence>k__BackingField` (field #38) |
| **Field type** | `Vector3` |
| **What it does** | The world position where the group's enemy was last spotted. |
| **How we could use it** | After combat, bots resuming questing could use this to avoid pathing through the last known enemy position. The `GoToPositionAbstractAction` path planning could add a penalty or avoidance radius around this position. Also useful for the `Ambush` and `Snipe` actions to orient toward the threat direction. |
| **Priority** | **High** |

#### 5. BotOwner.HearingSensor

| Property | Value |
|----------|-------|
| **Type** | `BotOwner` |
| **Fields** | `<HearingSensor>k__BackingField` (field #111) |
| **Field type** | `BotHearingSensor` |
| **What it does** | The bot's hearing sensor that processes sounds (footsteps, gunshots, door openings). Tracks what the bot has heard and from which direction. |
| **How we could use it** | Questing bots currently move without reacting to nearby sounds. Reading the hearing sensor state could let questing bots pause, look toward a sound source, or switch to cautious movement when they hear something nearby. This would make quest-pathing bots feel much more aware and realistic. Could be integrated into the `CustomLogicDelayedUpdate` base class that all actions inherit from. |
| **Priority** | **High** |

#### 6. BotOwner.DangerArea + BotAvoidDangerPlaces

| Property | Value |
|----------|-------|
| **Type** | `BotOwner` |
| **Fields** | `<DangerArea>k__BackingField` (field #101), `<BotAvoidDangerPlaces>k__BackingField` (field #59) |
| **Field types** | `BotDangerArea`, `BotAvoidDangerPlaces` |
| **What it does** | `DangerArea` tracks areas the bot considers dangerous (recent gunfire, grenades, mines). `BotAvoidDangerPlaces` manages avoidance behavior for these areas. |
| **How we could use it** | Our `GoToPositionAbstractAction` currently paths bots directly to objectives without considering danger zones. Reading these fields could let us check if the planned path passes through a danger area and request a repath. The `SoftStuckDetector` and `HardStuckDetector` could also use this to distinguish between "stuck" and "intentionally avoiding danger." |
| **Priority** | **High** |

#### 7. BotMover.IsMoving + SDistDestination

| Property | Value |
|----------|-------|
| **Type** | `BotMover` |
| **Fields** | `<IsMoving>k__BackingField` (field #39), `<SDistDestination>k__BackingField` (field #41) |
| **Field types** | `bool`, `float` (squared distance) |
| **What it does** | `IsMoving` is a simple boolean for whether the bot's mover is actively moving. `SDistDestination` is the squared distance to the current movement destination. |
| **How we could use it** | Our stuck detection (`SoftStuckDetector`, `HardStuckDetector`) currently tracks position over time to detect bots that aren't progressing. `IsMoving` provides an instant check — if the mover says it's not moving but we expect movement, that's a clear stuck signal. `SDistDestination` could provide a more efficient distance check than our current `Vector3.Distance` calculations. Both are public properties, no reflection needed. |
| **Priority** | **High** |

#### 8. AbstractGame.GameTimerClass

| Property | Value |
|----------|-------|
| **Type** | `AbstractGame` |
| **Field** | `gameTimerClass` (field #5) |
| **Field type** | `GameTimerClass` |
| **What it does** | The raid timer — tracks elapsed time, remaining time, and raid end conditions. |
| **How we could use it** | Bots currently quest without awareness of how much raid time remains. Reading the game timer could enable "late raid" behaviors: bots switching from offensive questing to extraction-focused movement as time runs low, or prioritizing closer objectives when time is limited. Could also pace bot quest progression to feel more natural over the raid duration rather than completing everything immediately. |
| **Priority** | **High** |

---

### Medium Priority (Nice to Have)

#### 9. BotOwner.CoverSearchInfo + Covers

| Property | Value |
|----------|-------|
| **Type** | `BotOwner` |
| **Fields** | `<CoverSearchInfo>k__BackingField` (field #33), `<Covers>k__BackingField` (field #96) |
| **Field types** | `BotCoverSearchInfo`, `BotCoversData` |
| **What it does** | `CoverSearchInfo` manages searching for cover positions. `Covers` tracks available and assigned cover points near the bot. |
| **How we could use it** | The `Ambush` and `Snipe` actions position bots at static points. Reading cover data could let these actions choose positions with actual cover instead of arbitrary positions. Could also improve `GoToPositionAbstractAction` by having bots move cover-to-cover when in hostile areas. |
| **Priority** | **Medium** |

#### 10. BotOwner.NightVision

| Property | Value |
|----------|-------|
| **Type** | `BotOwner` |
| **Fields** | `<NightVision>k__BackingField` (field #38) |
| **Field type** | `BotNightVisionData` |
| **What it does** | Manages the bot's night vision goggles — whether equipped, toggled on/off, and lighting conditions. |
| **How we could use it** | Questing bots on night raids could toggle NVGs based on indoor/outdoor lighting while moving between objectives. Currently bots may quest in the dark without using their NVGs, making them ineffective. |
| **Priority** | **Medium** |

#### 11. BotOwner.StandBy

| Property | Value |
|----------|-------|
| **Type** | `BotOwner` |
| **Fields** | `<StandBy>k__BackingField` (field #85) |
| **Field type** | `BotStandBy` |
| **What it does** | Controls idle/standby behavior — what the bot does when it has no active objective (look around, patrol nearby, hold position). |
| **How we could use it** | When bots reach an objective and must wait (e.g., the `HoldAtPosition` or `Ambush` actions), they could use the built-in standby behavior for more natural idle animations instead of just standing still. |
| **Priority** | **Medium** |

#### 12. BotsGroup.CoverPointMaster

| Property | Value |
|----------|-------|
| **Type** | `BotsGroup` |
| **Fields** | `<CoverPointMaster>k__BackingField` (field #40) |
| **Field type** | `CoverPointMaster` |
| **What it does** | The group-level cover point system. Manages all cover points available to the group and coordinates which bots use which cover. |
| **How we could use it** | Could query available cover points along a quest path to implement cover-to-cover movement. Could also be used in the `Snipe` action to find elevated cover positions with good sight lines. |
| **Priority** | **Medium** |

#### 13. BotsGroup.Allies

| Property | Value |
|----------|-------|
| **Type** | `BotsGroup` |
| **Fields** | `<Allies>k__BackingField` (field #29) |
| **Field type** | `List<IPlayer>` |
| **What it does** | List of players/bots considered allies by this group. |
| **How we could use it** | The HiveMind system tracks boss/follower relationships. The `Allies` list provides the game's own ally tracking, which could be used to verify our HiveMind relationships are consistent, or to discover ally relationships we don't know about. Could also prevent questing bots from accidentally blocking ally paths. |
| **Priority** | **Medium** |

#### 14. Player.TriggerZones

| Property | Value |
|----------|-------|
| **Type** | `Player` |
| **Fields** | `TriggerZones` (field #236) |
| **Field type** | `List<string>` |
| **What it does** | List of trigger zone names the player is currently inside. Updated as the player enters/exits zone triggers. |
| **How we could use it** | Could verify that a bot has actually entered a quest zone (e.g., "Visit location X" quests). Currently quest completion relies on distance checks to positions. Reading `TriggerZones` would give a definitive answer about whether the bot is in the correct zone. |
| **Priority** | **Medium** |

#### 15. Player.HealthController

| Property | Value |
|----------|-------|
| **Type** | `Player` |
| **Fields** | `_healthController` (field #333) |
| **Field type** | `IHealthController` |
| **What it does** | Manages all health state — body part HP, bleeding, fractures, pain, dehydration, energy. |
| **How we could use it** | Bots could make health-aware questing decisions: pause to heal when HP is low, avoid sprinting with a fractured leg, prioritize extraction when critically wounded. Could also affect movement speed decisions in our custom mover. |
| **Priority** | **Medium** |

#### 16. BotOwner.BotTalk

| Property | Value |
|----------|-------|
| **Type** | `BotOwner` |
| **Fields** | `<BotTalk>k__BackingField` (field #122) |
| **Field type** | `BotTalk` |
| **What it does** | Controls bot voice lines — triggers contextual phrases for combat, spotting enemies, looting, etc. |
| **How we could use it** | Questing bots are currently silent while moving between objectives. Triggering occasional voice lines (idle chatter, acknowledgments, warnings) would make bot squads feel more alive. Could be triggered at quest completion, when entering new areas, or when followers regroup. |
| **Priority** | **Medium** |

#### 17. BotOwner.CallForHelp + CalledData

| Property | Value |
|----------|-------|
| **Type** | `BotOwner` |
| **Fields** | `<CallForHelp>k__BackingField` (field #91), `<CalledData>k__BackingField` (field #92) |
| **Field types** | `BotCallForHelp`, `BotCalledData` |
| **What it does** | `CallForHelp` lets a bot request assistance from nearby allies. `CalledData` tracks incoming help requests. |
| **How we could use it** | The `Following` layer (priority 19) makes followers follow their boss. If a boss under fire calls for help, followers currently in the Questing layer could check `CalledData` to interrupt questing and rush to help, then resume questing afterward. Would make squad behavior more dynamic. |
| **Priority** | **Medium** |

#### 18. BotMover.MoverStateMachine

| Property | Value |
|----------|-------|
| **Type** | `BotMover` |
| **Fields** | `MoverStateMachine` (field #17) |
| **Field type** | `GClass548` |
| **What it does** | The mover's internal state machine — tracks whether the bot is walking, sprinting, paused, stuck, rerouting, etc. |
| **How we could use it** | Our `CustomMoverController` and stuck detectors could read the mover's internal state for better decisions. If the mover knows it's rerouting, we shouldn't flag that as stuck. If the mover is paused internally, we know to wait rather than force movement. |
| **Priority** | **Medium** |

#### 19. BotSpawner.AllBotsCount + InSpawnProcess

| Property | Value |
|----------|-------|
| **Type** | `BotSpawner` |
| **Fields** | `AllBotsCount` (field #13), `InSpawnProcess` (field #16) |
| **Field types** | `int`, `int` |
| **What it does** | `AllBotsCount` is the total number of active bots. `InSpawnProcess` is how many are currently mid-spawn. |
| **How we could use it** | Our `BotGenerator` spawning system could use `InSpawnProcess` to avoid scheduling spawns during busy periods (reducing frame drops). `AllBotsCount` could provide a cross-check against our own bot tracking to detect desync. |
| **Priority** | **Medium** |

---

### Low Priority (Speculative)

#### 20. Player._awareness

| Property | Value |
|----------|-------|
| **Type** | `Player` |
| **Fields** | `_awareness` (field #347) |
| **Field type** | `float` |
| **What it does** | Unclear — likely a global awareness multiplier affecting detection range/speed. May be set by game difficulty or skills. |
| **How we could use it** | Could modulate how cautiously a bot moves during questing based on its awareness level. Speculative because the exact mechanics are unknown. |
| **Priority** | **Low** |

#### 21. BotOwner.GoToSomePointData

| Property | Value |
|----------|-------|
| **Type** | `BotOwner` |
| **Fields** | `<GoToSomePointData>k__BackingField` (field #29) |
| **Field type** | `BotGoToPointData` |
| **What it does** | Manages point-to-point navigation state — the built-in system for telling a bot to go somewhere. |
| **How we could use it** | Potentially replace or supplement our custom `GoToPositionAbstractAction` with the built-in navigation. Risk: may conflict with our BigBrain layer system. Would need careful investigation of how it interacts with custom logic. |
| **Priority** | **Low** |

#### 22. BotOwner.DecisionQueue

| Property | Value |
|----------|-------|
| **Type** | `BotOwner` |
| **Fields** | `<DecisionQueue>k__BackingField` (field #115) |
| **Field type** | `DecisionQueue` |
| **What it does** | Queue of pending decisions the bot's brain needs to process. |
| **How we could use it** | Reading the decision queue could help debug why bots sometimes fail to respond to our layer commands. Could also detect when the bot is overwhelmed with decisions and throttle our quest updates. Complex integration with unknown side effects. |
| **Priority** | **Low** |

#### 23. BotOwner.SearchData

| Property | Value |
|----------|-------|
| **Type** | `BotOwner` |
| **Fields** | `<SearchData>k__BackingField` (field #107) |
| **Field type** | `BotSearchData` |
| **What it does** | Manages search/investigation behavior — how the bot searches an area after hearing something or losing sight of an enemy. |
| **How we could use it** | Post-combat, bots could use search behavior at the last known enemy position before resuming questing. Niche use case — most combat-to-quest transitions don't need this. |
| **Priority** | **Low** |

#### 24. BotsGroup.LastSoundsController

| Property | Value |
|----------|-------|
| **Type** | `BotsGroup` |
| **Fields** | `<LastSoundsController>k__BackingField` (field #39) |
| **Field type** | `LastSoundsController` |
| **What it does** | Group-level tracking of recent sounds heard by any member. |
| **How we could use it** | Theoretically could make an entire questing squad react to sounds. Complex to integrate because it requires understanding the sound event system's data structures. HearingSensor (per-bot) is simpler and more practical. |
| **Priority** | **Low** |

#### 25. BotOwner.BotFollower

| Property | Value |
|----------|-------|
| **Type** | `BotOwner` |
| **Fields** | `<BotFollower>k__BackingField` (field #36) |
| **Field type** | `BotFollower` |
| **What it does** | The built-in follower behavior system — how followers track and follow their boss. |
| **How we could use it** | Our `Following` layer (priority 19) implements custom following logic. The built-in `BotFollower` may have smoother following behavior we could delegate to. Risk: may conflict with our HiveMind system. Would need investigation to see if it complements or conflicts. |
| **Priority** | **Low** |

---

### Discovery Summary

| Priority | Count | Top Opportunities |
|----------|-------|-------------------|
| **High** | 8 | PlaceItemZone for plant quests, combat cooldown timers, hearing reactions, danger avoidance, stuck detection, raid timer awareness |
| **Medium** | 11 | Cover-aware movement, NVG management, voice lines, health-aware decisions, squad coordination |
| **Low** | 6 | Built-in nav integration, decision queue debugging, search behavior |

**Highest-impact changes would be:**
1. **Combat-aware questing** (fields #3, #4, #6) — Bots that move cautiously after firefights and avoid danger zones. Minimal code change, reads public fields.
2. **Plant zone verification** (field #1) — More reliable PlantItem quest completion. Single field read.
3. **Raid time awareness** (field #8) — Late-raid extraction behavior. Reads game timer, adds time checks to quest priority logic.
4. **Better stuck detection** (field #7) — `IsMoving` and `SDistDestination` are public properties requiring zero reflection. Drop-in improvement.
