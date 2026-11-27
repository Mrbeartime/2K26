# ProjectD flow chart

[Explore script-to-script dependencies](SCRIPT_DIAGRAM.html)

![Campaign navigation and turn loop](PROJECT_FLOW.svg)

Read the overview from Start to a campaign level. The diagrams below expand [navigation](#navigation), [player actions](#player-actions), [round resolution](#round-resolution), [world interactions](#world-interactions), [HUD and feedback](#hud-and-feedback), [data and progression](#data-and-progression), and [editor tooling](#editor-tooling). Solid arrows show an action, decision or transfer of control. Dotted arrows show a data dependency or notification. The scope is the current five-level campaign and its supporting systems.

## Navigation

```mermaid
flowchart TD
    Boot["Unity launch: preload SceneCatalog and Input Actions"] --> Start["Start · MenuScreen"]
    Start -->|Play| Select["LevelSelect · quest cards"]
    Start -->|Guide| Guide["GuideScene · guide pages"]
    Guide -->|Back| Start
    Start -->|Settings| Settings{"Settings panel assigned?"}
    Settings -->|Yes| Panel["Show settings overlay"]
    Settings -->|No| Add["Load Setting additively via InGameSettingsSession"]
    Panel -->|Close| Caller["Restore calling UI and focus"]
    Add -->|Close| Caller
    Start -->|Exit| Quit["Quit player / leave Play Mode"]
    Catalog["SceneCatalog: scene paths and level IDs"] -.-> Select
    Save["LevelProgressStore: completion and unlocks"] -.-> Select
    Select -->|Back with paper closed| Start
    Select --> Card["Select a quest and open its paper"]
    Card --> Gate{"Level unlocked?"}
    Gate -->|No| Select
    Gate -->|Play unlocked level| Fade["GameSceneManager → SceneFade"]
    Fade --> Loading["Loading scene · load destination asynchronously"]
    Loading --> Level["Level1 / Level2 / Level3 / Level4 / Level5"]
    Level --> Result["Win or lose → result UI"]
    Result -->|Retry| Reload["Fade and reload current level directly"]
    Reload --> Level
    Result -->|Level Select / Return to Guild on win| Select
```

The build has ten enabled scenes. The standalone Setting scene can be loaded additively to preserve its caller. Ordinary scene changes and retries bypass Loading. The result HUD’s `OpenNextLevel` callback returns to the guild in the current implementation. There are no configured secret levels. [Scene list](../ProjectSettings/EditorBuildSettings.asset) · [Navigation](../Assets/_Project/Core/SceneFlow/GameSceneManager.cs) · [Settings session](../Assets/_Project/Core/SceneFlow/InGameSettingsSession.cs) · [Result callbacks](../Assets/_Project/UI/HUD/LevelCompleteUI.cs)

## Player actions

```mermaid
flowchart TD
    Enter["Level loads"] --> Setup["TurnGameManager reads LevelTurnRules, refreshes schedule and collects active players"]
    Setup --> Intro{"Entrance cutscene configured and enabled?"}
    Intro -->|Yes| Film["Play entrance; suspend input and HUD"]
    Intro -->|No| Pick
    Film --> Pick["Choose an eligible hero"]
    Pick --> Act{"Command from world input or HUD"}
    Act -->|Move| Path["Check reachable tile, movement budget, doors and walls"]
    Path -->|Invalid / cancel| Act
    Path -->|Valid| Walk["PlayerMove follows path; tiles and interactions update"]
    Walk -->|Movement completed| Exit{"Living hero reached WinBox?"}
    Exit -->|Yes| Won["Victory → save completion and show result"]
    Exit -->|No, action available| Act
    Exit -->|No usable action left| Done
    Act -->|Skill| Aim["Preview and validate the same SkillTargets query"]
    Aim -->|Cancel / invalid| Act
    Aim -->|Successful sword, bow or lockpick| Done["Finish this hero’s action"]
    Act -->|Interact| React{"Interaction on current tile?"}
    React -->|Diary| Read["Pause world, read or page, close diary"]
    Read --> Act
    React -->|Switch activated| Done
    React -->|Unavailable| Act
    Act -->|Skip hero| Done
    Done --> Available{"Any unfinished heroes?"}
    Available -->|Yes| Pick
    Available -->|No| Resolve["Start enemy-phase sequence"]
    Act -->|End turn| Resolve
    Resolve -->|Round survives and turns remain| Pick
```

Movement can precede a skill or interaction. A successful skill or switch interaction finishes the hero’s action. Losing the last living player can end the game before the normal round boundary. [Turn coordinator](../Assets/_Project/Gameplay/Turns/TurnGameManager.cs) · [World input](../Assets/_Project/Input/PlayerController.cs) · [Movement](../Assets/_Project/Gameplay/PlayerMove.cs) · [Skill targeting](../Assets/_Project/Gameplay/Grid/SkillTargets.cs)

## Round resolution

```mermaid
flowchart TD
    Start["All heroes finished, or End Turn accepted"] --> Delay["Lock turn commands; wait configured phase delay"]
    Delay --> Shot["Wait for outstanding player projectiles"]
    Shot --> View{"Gameplay HUD registered in this scene?"}
    View -->|Yes| Bar["Await turn-progress animation"]
    View -->|No| Traps
    Bar --> Traps["Resolve each active scene trap for the current round"]
    Traps --> TrapShots["Wait for trap projectiles"]
    TrapShots --> Enemy["Enemy.RunEnemyPhase: living enemies react"]
    Enemy --> EnemyShots["Yield a frame; wait for enemy projectiles"]
    EnemyShots --> Corpses["Start due corpse shatters; wait for active effects"]
    Corpses --> Clock["Decrement turns left once"]
    Clock --> Left{"Turns left?"}
    Left -->|Yes| Next["Reset per-round action state; shake / sound; select a hero"]
    Left -->|No| Lose["Turn limit reached → defeat"]
    Traps -.->|Terminal event| Stop["GameEnded during resolution"]
    EnemyShots -.->|Terminal event| Stop
    Stop -.-> Abort["Stop sequence without starting another round"]
```

The phase uses the elapsed round number for traps and a separate countdown for defeat. Levels 1–4 start with **7 turns**, Level 5 with **13**. Both rules assets specify a **0.35 s** phase delay. Corpse cleanup becomes due at least one round after death. [Phase coroutine](../Assets/_Project/Gameplay/Turns/EnemyPhaseSequence.cs) · [Standard rules](../Assets/_Project/Data/StandardTurnRules.asset) · [Final level rules](../Assets/_Project/Data/FinalLevelTurnRules.asset) · [Corpse cleanup](../Assets/_Project/Gameplay/Characters/PlayerCorpseShatter.cs)

## World interactions

```mermaid
flowchart LR
    Move["PlayerMove"] --> Grid["GridManager + Pathfinder: occupancy, reachable tiles, route"]
    Grid --> Tile["Enter tile"]
    Tile --> Chest["Character.Interact → open chest on tile"]
    Tile --> Plate["Occupied pressure plate → update linked doors"]
    Tile --> Spike["Trigger or contact hazards where configured"]
    Move -->|Move finished| Goal["WinBox overlap check"]
    Interact["React command"] --> Switch["DoorSwitch / FloorSwitch → activate linked mechanism"]
    Interact --> Diary["DiaryInteractable → DiaryUI"]
    Skill["SkillTargets"] --> Sword["SwordMan: adjacent unblocked tile → slash"]
    Skill --> Archer["ArcherMan: straight firing lane → ArrowProjectile"]
    Skill --> Rogue["Rogue: tile-distance check → unlock RogueDoor"]
    Enemy["Enemy / RangedEnemy"] --> Strike["React to eligible targets; attack or shoot"]
    Schedule["Round and trap schedules"] --> Trap["TurnSpikeTrap / TurnArrowTrap → resolve hazard"]
    Strike --> Hit["Entity receives hit"]
    Sword --> Hit
    Archer --> Hit
    Trap --> Hit
    Spike --> Hit
    Hit --> Death["If lethal: release tile occupancy; play death feedback"]
    Death -->|Player| Defeat["Notify turn coordinator; check remaining heroes"]
    Death -->|Eligible player corpse| Cleanup["Register delayed crystal shatter"]
```

Switch targets are wired per scene or prefab. Doors and walls affect graph traversal and firing lanes; the rogue’s lockpick range uses Manhattan distance. [Character interactions](../Assets/_Project/Gameplay/Characters/Character.cs) · [Pressure plates](../Assets/_Project/Gameplay/Interactables/PressurePlate.cs) · [Grid](../Assets/_Project/Gameplay/Grid/GridManager.cs) · [WinBox](../Assets/_Project/Gameplay/Interactables/WinBox.cs)

## HUD and feedback

```mermaid
flowchart TD
    Device["Mouse / keyboard / mapped controls"] --> Input["PlayerController · world input"]
    Device --> HUD["NewUISet prefab · InGameUIManager"]
    Input --> Commands["ITurnCommands"]
    HUD --> Commands
    Commands --> Turns["TurnGameManager"]
    Turns -.->|StateChanged / GameEnded| State["IReadOnlyTurnState"]
    State -.-> HUD
    HUD --> Counter["TurnCounterText + TurnBarPresenter"]
    HUD --> Timeline["HazardTimeline: markers and trap focus"]
    HUD --> Select["Character icons, actions, hold buttons, key tips"]
    HUD --> Pause["Pause, guide and settings overlays"]
    Pause -->|Close / restore input| HUD
    Session["TurnSession · bindings for this scene"] -.-> Input
    Session -.-> HUD
    Turns -.->|Selection and highlight requests| Highlights["World highlights / range preview"]
    Turns -.->|ActionPerformed| Camera["CameraFocusManager: action zoom"]
    Timeline --> Camera
    Turns -.->|RoundAdvanced| Audio["GameAudioManager: mixer, music and SFX"]
    Feedback["Buttons, character animations, doors and traps"] --> Audio
    Turns -.->|GameEnded| Result["LevelCompleteUI → chest / result sequence and navigation"]
```

The HUD supplies `IGameplayOverlay` for input gating and the awaitable turn bar. `IWorldHighlights` exposes previews to the HUD. Camera, particles, materials, sprites, animations and audio present outcomes; board and turn code determines whether commands succeed. [Scene contracts](../Assets/_Project/Gameplay/Turns/TurnSession.cs) · [Active HUD](../Assets/_Project/UI/HUD/InGameUIManager.cs) · [Audio](../Assets/_Project/Audio/Runtime/GameAudioManager.cs)

## Data and progression

```mermaid
flowchart LR
    Quest["Resources: quest catalog and localized text"] --> Catalog["Preloaded SceneCatalog"]
    Catalog --> Cards["LevelSelect quest cards and detail paper"]
    Rules["LevelTurnRules assets"] --> Runtime["TurnGameManager: turn limit and delay"]
    Objects["Scene-authored scheduled objects"] --> Schedule["TurnScheduleData: round states and trap types"]
    Runtime -->|Current round| Schedule
    Schedule --> Markers["HUD hazard timeline and alerts"]
    Goal["WinBox / victory"] --> Complete["GameSceneManager records level completion"]
    Complete --> Progress["LevelProgressStore updates completion and unlocks"]
    Progress --> Disk["JSON save + backup"]
    Disk -->|Load / recover| Progress
    Progress --> Cards
    Language["Language selection"] --> Locale["GameLanguage + Unity Localization"]
    Locale --> UI["Menu labels, guide and quest text"]
    Settings["Audio / camera preferences"] --> Prefs["GameSettings"]
    Prefs --> Apply["Apply mixer and camera preferences"]
```

The Editor save lives under `Assets/_Project/Data/Progress/`. The player build writes to `Application.persistentDataPath`. Build settings register Input Actions, Localization and Addressables settings; Resources keys supply runtime-loaded text, shaders and the audio-manager prefab. [Catalog](../Assets/Resources/SceneCatalog.asset) · [Progress store](../Assets/_Project/Core/SceneFlow/LevelProgressStore.cs) · [Language](../Assets/_Project/Data/Localization/GameLanguage.cs)

## Editor tooling

```mermaid
flowchart LR
    Author["Edit levels, prefabs and configuration"] --> Catalog["SceneCatalog inspectors: edit quest text and sync build scenes"]
    Author --> Schedule["TurnScheduleDataEditor: rebuild schedule from scene objects"]
    Author --> Art["Setup tools: props, diary model, torch and sword trail"]
    Author --> Text["Scan Thai text → translation CSV → apply translations"]
    Author --> Fonts["TMP font creation and assignment"]
    Author --> Validate["Validate HUD wiring, round behavior, sword trail and corpse effects"]
    Catalog -.-> Runtime["Build scenes, data assets and prefabs used at runtime"]
    Schedule -.-> Runtime
    Art -.-> Runtime
    Text -.-> Runtime
    Fonts -.-> Runtime
    Runtime -.-> Validate
```

The diagrams follow the current build list, serialized configuration and C# paths. Quarantined assets do not form a campaign step. The retired HUD is in the Unity-ignored `LegacyHUD~/` archive. [Editor tools](../Assets/_Project/Core/Editor) · [Catalog editor](../Assets/Resources/SceneCatalogEditor.cs)

## Script index

These groups cover all C# source files in the project-owned domains and the Resources catalog. The links open the implementation behind each diagram.

**Menus, scene transitions and settings** · [GameSceneManager](../Assets/_Project/Core/SceneFlow/GameSceneManager.cs), [InGameSettingsSession](../Assets/_Project/Core/SceneFlow/InGameSettingsSession.cs), [LevelProgressStore](../Assets/_Project/Core/SceneFlow/LevelProgressStore.cs), [SceneFade](../Assets/_Project/Core/SceneFlow/SceneFade.cs), [GameSettings](../Assets/_Project/Core/Settings/GameSettings.cs), [LoadingScreen](../Assets/_Project/UI/Menus/LoadingScreen.cs), [MenuScreen](../Assets/_Project/UI/Menus/MenuScreen.cs), [MenuScreen.LevelSelect](../Assets/_Project/UI/Menus/MenuScreen.LevelSelect.cs), [MenuScreen.QuestPaper](../Assets/_Project/UI/Menus/MenuScreen.QuestPaper.cs), [MenuScreen.Settings](../Assets/_Project/UI/Menus/MenuScreen.Settings.cs), [QuestPaperLevelCard](../Assets/_Project/UI/Menus/QuestPaperLevelCard.cs), [QuestPaperPool](../Assets/_Project/UI/Menus/QuestPaperPool.cs), [AudioSettingsUI](../Assets/_Project/UI/Settings/AudioSettingsUI.cs), [CameraSettingsUI](../Assets/_Project/UI/Settings/CameraSettingsUI.cs).

**Input and turn orchestration** · [MenuInput](../Assets/_Project/Input/MenuInput.cs), [PlayerController](../Assets/_Project/Input/PlayerController.cs), [ProjectDInputAction](../Assets/_Project/Input/ProjectDInputAction.cs), [EnemyPhaseSequence](../Assets/_Project/Gameplay/Turns/EnemyPhaseSequence.cs), [GameResultController](../Assets/_Project/Gameplay/Turns/GameResultController.cs), [TrapCountdownAlert](../Assets/_Project/Gameplay/Turns/TrapCountdownAlert.cs), [TrapCountdownSchedule](../Assets/_Project/Gameplay/Turns/TrapCountdownSchedule.cs), [TurnDebugPanel](../Assets/_Project/Gameplay/Turns/TurnDebugPanel.cs), [TurnGameManager](../Assets/_Project/Gameplay/Turns/TurnGameManager.cs), [TurnScheduleData](../Assets/_Project/Gameplay/Turns/TurnScheduleData.cs), [TurnSession](../Assets/_Project/Gameplay/Turns/TurnSession.cs).

**Board, units and world objects** · [AllOutlineColor](../Assets/_Project/Gameplay/AllOutlineColor.cs), [CharacterFacing](../Assets/_Project/Gameplay/CharacterFacing.cs), [ArcherMan](../Assets/_Project/Gameplay/Characters/ArcherMan.cs), [ArrowProjectile](../Assets/_Project/Gameplay/Characters/ArrowProjectile.cs), [Character](../Assets/_Project/Gameplay/Characters/Character.cs), [CharacterAnimationController](../Assets/_Project/Gameplay/Characters/CharacterAnimationController.cs), [CrystalShatterVisual](../Assets/_Project/Gameplay/Characters/CrystalShatterVisual.cs), [Enemy](../Assets/_Project/Gameplay/Characters/Enemy.cs), [EnemyAnimationController](../Assets/_Project/Gameplay/Characters/EnemyAnimationController.cs), [EnemyDustVisual](../Assets/_Project/Gameplay/Characters/EnemyDustVisual.cs), [PlayerCorpseShatter](../Assets/_Project/Gameplay/Characters/PlayerCorpseShatter.cs), [RangedEnemy](../Assets/_Project/Gameplay/Characters/RangedEnemy.cs), [Rogue](../Assets/_Project/Gameplay/Characters/Rogue.cs), [SwordMan](../Assets/_Project/Gameplay/Characters/SwordMan.cs), [SwordSlashTrail](../Assets/_Project/Gameplay/Characters/SwordSlashTrail.cs), [DiaryInteractable](../Assets/_Project/Gameplay/DiaryInteractable.cs), [Entity](../Assets/_Project/Gameplay/Entity.cs), [GridManager](../Assets/_Project/Gameplay/Grid/GridManager.cs), [Pathfinder](../Assets/_Project/Gameplay/Grid/Pathfinder.cs), [SkillTargets](../Assets/_Project/Gameplay/Grid/SkillTargets.cs), [Tile](../Assets/_Project/Gameplay/Grid/Tile.cs), [TurnTile](../Assets/_Project/Gameplay/Grid/TurnTile.cs), [Arrow](../Assets/_Project/Gameplay/Interactables/Arrow.cs), [ArrowTutorial](../Assets/_Project/Gameplay/Interactables/ArrowTutorial.cs), [Chest](../Assets/_Project/Gameplay/Interactables/Chest.cs), [DoorController](../Assets/_Project/Gameplay/Interactables/DoorController.cs), [DoorSwitch](../Assets/_Project/Gameplay/Interactables/DoorSwitch.cs), [DoorSwitchHoverBlink](../Assets/_Project/Gameplay/Interactables/DoorSwitchHoverBlink.cs), [FloorSwitch](../Assets/_Project/Gameplay/Interactables/FloorSwitch.cs), [PressurePlate](../Assets/_Project/Gameplay/Interactables/PressurePlate.cs), [PropInteractAnimation](../Assets/_Project/Gameplay/Interactables/PropInteractAnimation.cs), [RogueDoor](../Assets/_Project/Gameplay/Interactables/RogueDoor.cs), [SpikeTrap](../Assets/_Project/Gameplay/Interactables/SpikeTrap.cs), [SpikeTrapEvents](../Assets/_Project/Gameplay/Interactables/SpikeTrapEvents.cs), [SwitchSpikeTrap](../Assets/_Project/Gameplay/Interactables/SwitchSpikeTrap.cs), [TorchFire](../Assets/_Project/Gameplay/Interactables/TorchFire.cs), [Trap](../Assets/_Project/Gameplay/Interactables/Trap.cs), [TreasureChestAnimation](../Assets/_Project/Gameplay/Interactables/TreasureChestAnimation.cs), [TurnArrowTrap](../Assets/_Project/Gameplay/Interactables/TurnArrowTrap.cs), [TurnSpikeTrap](../Assets/_Project/Gameplay/Interactables/TurnSpikeTrap.cs), [TurnTrap](../Assets/_Project/Gameplay/Interactables/TurnTrap.cs), [TurnTrapVisuals](../Assets/_Project/Gameplay/Interactables/TurnTrapVisuals.cs), [WinBox](../Assets/_Project/Gameplay/Interactables/WinBox.cs), [PlayerMove](../Assets/_Project/Gameplay/PlayerMove.cs), [TEMPORARYMENUFIXER](../Assets/_Project/Gameplay/TEMPORARYMENUFIXER.cs).

**HUD, guide, results and diary** · [BackdropBlurCapture](../Assets/_Project/UI/HUD/BackdropBlurCapture.cs), [CharacterIconBurst](../Assets/_Project/UI/HUD/CharacterIconBurst.cs), [GuideTextDatabase](../Assets/_Project/UI/HUD/GuideTextDatabase.cs), [GuideUIManager](../Assets/_Project/UI/HUD/GuideUIManager.cs), [HazardTimeline](../Assets/_Project/UI/HUD/HazardTimeline.cs), [HoldButton](../Assets/_Project/UI/HUD/HoldButton.cs), [InGameUIManager](../Assets/_Project/UI/HUD/InGameUIManager.cs), [KeysTip](../Assets/_Project/UI/HUD/KeysTip.cs), [LevelCompleteCharacterIcons](../Assets/_Project/UI/HUD/LevelCompleteCharacterIcons.cs), [LevelCompleteSequence](../Assets/_Project/UI/HUD/LevelCompleteSequence.cs), [LevelCompleteUI](../Assets/_Project/UI/HUD/LevelCompleteUI.cs), [NewCharacterIcon](../Assets/_Project/UI/HUD/NewCharacterIcon.cs), [OverlayUI](../Assets/_Project/UI/HUD/OverlayUI.cs), [ParallaxMenuBackground](../Assets/_Project/UI/HUD/ParallaxMenuBackground.cs), [PauseUI](../Assets/_Project/UI/HUD/PauseUI.cs), [QuestPaperDecoration](../Assets/_Project/UI/HUD/QuestPaperDecoration.cs), [SelectedButtonBold](../Assets/_Project/UI/HUD/SelectedButtonBold.cs), [SpriteOutlinePadding](../Assets/_Project/UI/HUD/SpriteOutlinePadding.cs), [TurnBarPresenter](../Assets/_Project/UI/HUD/TurnBarPresenter.cs), [TurnCounterText](../Assets/_Project/UI/HUD/TurnCounterText.cs), [DiaryUI](../Assets/_Project/UI/Diary/DiaryUI.cs).

**Camera and audio** · [CameraFocusManager](../Assets/_Project/Art/Camera/CameraFocusManager.cs), [CutSceneCharacter](../Assets/_Project/Art/Camera/CutSceneCharacter.cs), [DoorCutScene](../Assets/_Project/Art/Camera/DoorCutScene.cs), [TurnScreenShake](../Assets/_Project/Art/Camera/TurnScreenShake.cs), [AudioCue](../Assets/_Project/Audio/Runtime/AudioCue.cs), [ButtonAudioFeedback](../Assets/_Project/Audio/Runtime/ButtonAudioFeedback.cs), [CharacterAudioEvents](../Assets/_Project/Audio/Runtime/CharacterAudioEvents.cs), [GameAudioManager](../Assets/_Project/Audio/Runtime/GameAudioManager.cs).

**Rules, localization and scene catalog** · [LevelTurnRules](../Assets/_Project/Data/LevelTurnRules.cs), [GameLanguage](../Assets/_Project/Data/Localization/GameLanguage.cs), [LanguageToggleButton](../Assets/_Project/Data/Localization/LanguageToggleButton.cs), [SceneCatalog](../Assets/Resources/SceneCatalog.cs), [SceneCatalogEditor](../Assets/Resources/SceneCatalogEditor.cs), [SceneCatalogQuestData](../Assets/Resources/SceneCatalogQuestData.cs), [SceneCatalogQuestTextEditor](../Assets/Resources/SceneCatalogQuestTextEditor.cs).

**Editor authoring and validation** · [CorpseShatterValidation](../Assets/_Project/Core/Editor/CorpseShatterValidation.cs), [CreateThaiEnglishTMPFonts](../Assets/_Project/Core/Editor/CreateThaiEnglishTMPFonts.cs), [DialPrefabValidation](../Assets/_Project/Core/Editor/DialPrefabValidation.cs), [DiaryModelSetup](../Assets/_Project/Core/Editor/DiaryModelSetup.cs), [ApplyTranslations](../Assets/_Project/Core/Editor/Localization/ApplyTranslations.cs), [LanguageTestMenu](../Assets/_Project/Core/Editor/Localization/LanguageTestMenu.cs), [ThaiTextScanner](../Assets/_Project/Core/Editor/Localization/ThaiTextScanner.cs), [PropInteractSetup](../Assets/_Project/Core/Editor/PropInteractSetup.cs), [SetTMPFontToNotoThai](../Assets/_Project/Core/Editor/SetTMPFontToNotoThai.cs), [SwordTrailProjectSetup](../Assets/_Project/Core/Editor/SwordTrailProjectSetup.cs), [TorchFireSetup](../Assets/_Project/Core/Editor/TorchFireSetup.cs), [TurnAndSlashVerification](../Assets/_Project/Core/Editor/TurnAndSlashVerification.cs), [TurnScheduleDataEditor](../Assets/_Project/Core/Editor/TurnScheduleDataEditor.cs).
