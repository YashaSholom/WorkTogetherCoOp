# Co-op Workshop

## Alien checkpoint prototype

Read the project-root `ALIEN_CHECKPOINT.md` for the brief, live checklist, validation evidence and cross-account handoff. MainMenu is now a spaceport crew lobby, and GameWorld is a saved 30×30 m orbital checkpoint. Three authored visitors arrive sequentially in a hovering shuttle. Talk to the driver with **E**, request papers, collect the two separate documents, and share the work with your crew. Right mouse brings a held object close to the camera for inspection; hold and drag it to rotate, and scroll to zoom. **Q** drops and **F** throws. Held documents turn toward the player like a book cover and show their details on the physical document. **E** on a sorting/scanning tray places or retrieves the held paper. Put each document on a scanner tray, then use **E** on the computer to read scanned documents in the shared case interface and approve or reject. Approval sends the shuttle forward; rejection triggers a giant boot and returns it to the portal. The next visit starts after old papers are cleaned up.

Escape closes an inspection/dialogue/terminal first; otherwise it opens gameplay settings.

**Game loop (work day, payouts, shop).** A giant station clock runs the day from 8 AM to 4 PM (1 real second per game minute; `WorkShift`). At 4 PM no new visitors arrive; the visitor being processed stays until cleared, then the day closes, everyone's pending pay is banked and a summary appears. **START NEXT DAY** is the button on the left corner of the inspection terminal desk. Visitors are random (shuttle or cargo truck). About 40% of visitors have one document flaw (expired ID, wrong ID photo, vehicle registered to someone else, registration number not matching the plate, or a forged ID that only a UV lamp reveals); `CheckpointSession` rolls it per visit and all document views print it. About 2.5 s after Approve/Reject the crew sees the case verdict and per-player payouts: fees for each document check (credited to whoever put the paper in a scanner tray) and each cargo X-ray (whoever pressed Scan), a bonus for contraband found, a bonus for a correct decision, fines for admitting bad papers or contraband, for missing contraband in X-rayed cargo, and a big fine for turning away a valid visitor. All amounts are Inspector fields on `CheckpointSession`. Credits are per player (`PlayerWallet` on the player prefab; 60 to start) and only banked credits can be spent. The **Crew Supply** ATM (`ShopTerminal` + `ShopPanel`) sells the X-ray belt's second slot (120), a UV lamp (80) and coffee (15); offers are an Inspector list. Players carry up to three items (`PlayerInteractor.Slots`); the active slot is in the hands, the others are stowed and hidden. The first-day values (prices, fees, day length, flaw chance) are placeholders for tuning.

**Cargo trucks and the X-ray conveyor.** Truck visitors carry crates (`CargoPackage`, authored per case in `TravellerCase.packages`, with a contraband flag). Put crates in the conveyor's load slot(s) and press SCAN. The crates are real physics objects: they are released onto the belt one at a time (nearest the tunnel first), the belt pushes each into the X-ray tunnel and stops while the monitor shows its X-ray (a camera that only draws the `XRay` layer), then carries it out and drops it into the tilted OUT bin at the far end. The station shows each crate's result and, at the end of the run, VALID or CONTRABAND DETECTED. A crate can't be grabbed while it is being carried in or scanned. Crates arrive loaded in amber cargo spots on the parked truck's bed (`CheckpointSession.cargoSpots`, world-fixed at the stop and active only while a truck is parked). A decision can be made as soon as the visitor has stopped; papers need not be requested or scanned. **Approve is refused until every crate is back in a spot** (the terminal shows "cargo n/m on truck"); turning a truck away confiscates its cargo, so nothing has to go back. Approving bad papers or contraband is still possible, because the crew can't know the answer in advance, and it is fined in the verdict. The belt's second slot is a shop upgrade (`ContrabandScanner.slots` / `UnlockedSlots`).

Authoring: `Coop Prototype > Checkpoint > Add Cargo Truck and Contraband Scanner` and `... > Add Work Day, Payouts and Shop` (additive; the second also rebuilds the scanner station). Purchasing additional terminals and saving progress between sessions are future work. Duplicate an Inspection Terminal and wire its CheckpointSession reference to add another working computer; scanned records and decisions are already shared. Art is an editable prototype, with portraits rendered from the actual alien prefab. Document field values are authored snapshots and do not automatically change when traveller details are edited.

**Running (2026-10-01).** Hold Shift while moving forward: the client sends the held key with its movement input, the server applies `NetworkPlayerMotor.runSpeed` (1.65×, stacks with coffee) and writes the replicated `Running` flag, and `PlayerAnimation` sets the animator's `Running` parameter and a faster `MoveRate` (`runPlaybackRate`). `BeanCrew.controller` has Run and Carry Run states with walk/run transitions kept exclusive. The clips (`Art/BeanCrew/Animations/Run.anim`, `Carry Run.anim`) are generated by `Coop Prototype > Art > Build Run Animations` from the walk cycles (amplified stride, arm pump and bounce, forward lean; Carry Run keeps the carry arms) and are normal editable clips. Debug panel: Player > Toggle always run.

**Terminal document slots (2026-10-01).** Asking the visitor for papers places them straight into the terminal's document slots (`CheckpointSession.documentSlots`, six scanner sockets along the desk and its two side wings, filled from the middle outwards), where they are scanned at once; the player who asked is credited with the document checks. Papers can still be taken out to inspect (UV lamp, close-up) or hand to the crew, and put back in any slot. The collection shelf and delivery points were removed (`documentDeliveryPoints` is now only an overflow fallback, and a warning is logged if a visitor has more papers than free slots). For new document types (receipts, permits...) raise `DocumentSlotCount` in `CheckpointDocumentDockSetup` and re-run `Coop Prototype > Checkpoint > Add Terminal Document Slots`. START NEXT DAY now sits on the right wing of the terminal desk. Backup: `Tools/Checkpoint/BeforeDocumentDock`.

**F1 debug panel (Editor / Development builds).** `DebugMenu` (on the checkpoint interface) shows the hidden truth of the current case (flaw, contraband, valid or not) and sends commands to `DebugTools` on the Checkpoint Authority, which runs them on the server: call a random / shuttle / truck / companion / contraband visitor or any specific traveller with a chosen flaw; stop the vehicle, hand over and scan papers, send a visitor away; approve, reject or arrest; +1 hour, jump to 3:55 PM, close the day, start the next one; police visit now and release all; grant credits; spawn shop items free, unlock the belt slot, toggle coffee; teleport to editable points. Release builds never show it and the server refuses the commands. Authoring: `Coop Prototype > Checkpoint > Add Debug Panel (F1)`. New features add their entries per `FEATURE_IMPLEMENTATION.md`.

**Arrests, holding cell and the terminal codex (2026-10-01).** The inspection terminal has four tabs: **CASE** (scanned papers and the decision), **FEDERATIONS** (every issuing government, its seal and ID-number prefix), **VEHICLES** (manufacturers with their badges and chassis prefixes, plus pictures of the certified models) and **RACES** (Veloran and the new Brakkan race, with pictures and identifying features). The terminal now opens between visitors so the codex can be studied; decisions still need a visitor at the desk. Besides Approve/Reject there is **ARREST**: the visitor and everyone travelling with them (`TravellerCase.companions`) go to the **holding cell** in the new detention wing behind the operations hall, the vehicle is towed away empty along the departure route and any cargo is confiscated. The case verdict for an arrest pays only the check fees; whether it was right stays secret. The Galactic Police arrive at the start of the day `HoldingCell.policeIntervalDays` (2) after the arrest: each justified prisoner pays their bounty (`TravellerDefinition.bounty`) to the arresting officer, and each innocent one costs `HoldingCell.falseArrestFine` (250). The **cell tablet** at the entrance (`HoldingCellTablet` + `HoldingCellPanel`) lists prisoners, bounties, the arresting officer and days until pickup, and can **SET FREE** anyone (no bounty, no fine). The cell holds 6 (one per `prisonerSpots` transform); Arrest is refused if the whole group doesn't fit. Prisoner figures and vehicle occupants (`VehicleOccupants`) are local visuals rebuilt from replicated state, so late joiners see them too.

**Documents.** IDs show **Issued by** (the federation) instead of the place of birth, an **ID number** with the federation prefix and the federation's **seal**; registrations show **Manufacturer**, **Chassis no.**, **Passengers**, the maker's **badge** and a picture of the registered model. Both carry a holographic strip and barcode. New random flaws: a seal that belongs to another federation (`SealMismatch`), a badge from another maker (`BadgeMismatch`) and the wrong race on the ID (`RaceMismatch`), all checkable against the codex. Data lives in `CheckpointData/Codex` (`CheckpointCodex`, `FederationDefinition`, `VehicleManufacturer`, `VehicleModelDefinition`, `RaceDefinition`); `CheckpointSession.codex`/`holdingCell` link it. Emblems are PNGs in `Art/Checkpoint/Codex`; race models are `Art/Checkpoint/Race_Veloran.prefab` (copied from the styled driver) and `Race_Brakkan.prefab` (renderers named `Skin*` take each traveller's colour). Authoring: `Coop Prototype > Checkpoint > Add Holding Cell, Codex and Document Seals` (additive; GameWorld backup in `Tools/Checkpoint/BeforeHoldingCell`, which also keeps the two original cargo-visitor portraits). The old non-functional cell mock-up in the hall is hidden, not deleted. `CheckpointNetworkTest.unity` was not updated: there the codex tabs and Arrest show as unavailable.

`CheckpointNetworkTest.unity` is an additional direct-host scene for late-join testing of this feature; the normal MainMenu party still closes admission after starting. `CoopWorkshop.unity` is preserved. Previous GameWorld content is retained under an inactive `Legacy Workshop (preserved)` root, with pre-theme scene copies under `Tools/Checkpoint/BeforeSpaceTheme`.

Implementation: `CheckpointSession` owns replicated visit/phase/timestamps/scanned bits/counters; `TravellerInteractable` and `CheckpointTerminal` validate sender, current visit, reach and obstruction on the server. `DocumentItem` extends existing pickup physics. `DocumentScanner` uses an Inspector-wired socket event. `CheckpointVehicleView` reconstructs movement from server time, including for late joiners. `LocalGameplayModal`, `HeldItemInspector` and `FirstPersonArms` keep inspection generic and local: the object in the hands is the object being inspected (no copy), so the item moves toward the camera and keeps its live text. Items choose how they face the camera with `PickupItem.ViewRotation`/`ViewScale`; documents show the front face (text and portrait) to the player. No new packages were added.

Validation: Unity Editor compilation; 22 two-peer checkpoint checks, 11 late-join/disconnect checks, and 10 existing voice regression checks passed in MPPM. Input focus/escape and screenshots were checked separately; all three checkpoint/menu scenes and prefab references passed the editor validation. Reports live in `TestResults/Checkpoint` (local generated output). Reproduce with `Tools/Test-CheckpointEditors.ps1 -Client <active MPPM project path>` after both peers enter a fresh GameWorld, and `Tools/Test-CheckpointLateJoin.ps1 -Client <path>` with both editors in Play mode in a fresh CheckpointNetworkTest scene before connecting. The additional editor used here was `Library/VP/mppmbcc889cd` (Player 3); the older Player 2 clone has stale junctions to `D:\CoOpTest` and was not used. No standalone build, WAN/Steam multi-PC test or performance profiling was performed for this increment.

Open `Assets/CoopPrototype/Scenes/MainMenu.unity` for the complete menu/party/game flow. `GameWorld.unity` is the gameplay scene loaded by the host. `CoopWorkshop.unity` remains available for direct gameplay and late-join regression testing. Everything in this scene is a normal, saved Unity object. Edit components, meshes, materials, prefabs, item assets and UnityEvents directly in the Inspector. No runtime level generator or hidden dependency is required.

## Run

### Preferred: Multiplayer Play Mode (no standalone rebuild)

Use **Window > Multiplayer > Multiplayer Play Mode** (installed package version 2.0.2), then enable the **Player 2** checkbox. It starts an additional Unity editor instance; it does not require a standalone build. Press Play, choose **Start playing**, then **Host** in the main editor. Choose **Start playing > Connect** in Player 2 with Direct IP / LAN, `127.0.0.1`, port 7777. Both click **I'm ready**, then the host clicks **Start game**. Choose character previews all four characters; changing one clears readiness. Use editor instances here; Local Instances use builds.

Press Escape to release the cursor before switching windows. Ctrl+F9 focuses the main player; Ctrl+F10 focuses Player 2. For persistent-world late-join regression tests, use the legacy CoopWorkshop scene. MainMenu parties reject arrivals after a round starts. Tags do not automatically select Host/Client in this prototype.

See the root `INIT.md` for detailed setup and `AGENTS.md` for the persistent preference to use Multiplayer Play Mode whenever possible.

### Standalone builds (optional)

- Start at MainMenu, choose Start playing > Host, ready up, then Start game.
- Run `Builds/CoopWorkshop/CoopWorkshop.exe`, choose Start playing > Connect, then ready up, using `127.0.0.1`, port 7777.
- Alternatively run the executable twice: `CoopWorkshop.exe -host` and `CoopWorkshop.exe -client -address 127.0.0.1`. `-server` is for the legacy direct-play scene; MainMenu requires a participating host to start the round. `-port 7778` changes the port; both instances must match.
- Click the world to capture the mouse. WASD moves, Shift (held, moving forward) runs, mouse looks, E or left click is the primary action (interact with what you aim at, otherwise use a usable held item such as coffee, otherwise drop the held item), Q drops, F throws, hold the right mouse button to inspect the held item (move the mouse to turn it, scroll to zoom, release to put it back in your hands), 1-3 or the mouse wheel switch between the three item slots, F1 (editor/development builds) opens the debug panel, V toggles first/third person (first person is the default), C changes outfit, Escape releases the mouse. Sessions continue when unfocused.
- Rebuild after edits via **Coop Prototype > Build Windows Prototype**. The create-scene command opens an existing workshop and never overwrites your edits.

## Art

The current checkpoint layout is a saved 44×40 metre shared deck with three open inspection bays, a covered rear operations area and a descending exit road beneath the deck. The central bay uses the existing gameplay loop; the two outer bays are spatial prototypes for future simultaneous inspections. Event display, detention cells and lounge are visual prototypes. Use **Coop Prototype > Checkpoint > Apply Shared Deck Layout** to author the layout; the existing scene is preserved and the command is idempotent. See `ALIEN_CHECKPOINT.md` for validation and scope.

Characters (four Bean Crew outfits, shared rig and animations) and props: see `Art/BeanCrew/README.md`. The previous worker is kept in `Art/Legacy`.

## Steam (friends, invites) and server rules

Steamworks.NET 2025.165.0 is embedded in `Packages/com.rlabrecque.steamworks.net` (no git/OpenUPM needed). Scene objects: `Steam` (`SteamBootstrap` + `SteamLobby`) and a `SteamP2PTransport` next to the Unity Transport on the NetworkManager. Run **Coop Prototype > Steam > Add Steam Networking To Scene** once (idempotent) to add them, add `PlayerIdentity` to the player prefab and write `steam_appid.txt`.

- **App ID**: `SteamBootstrap.appId` is 480 (Valve's Spacewar test app) until you have your own. `steam_appid.txt` (project root, copied next to Windows/Linux builds) lets the Editor/builds start Steam without launching through it. Don't ship that file with a real release; turn on `restartThroughSteam` then.
- **Host**: session menu > Steam tab > *Host (friends can join)*. Netcode starts on Steam P2P (Steam Datagram Relay, no port forwarding) and a friends-only lobby advertises your SteamID.
- **Invite**: *Invite friends (Steam overlay)* / Shift+Tab, or the in-game online-friends list with *Invite* buttons (works in the Editor, where the overlay is unavailable). Friends can also use *Join Game* on your name in their Steam friends list (rich presence).
- **Join**: accepting an invite joins the lobby, reads the host SteamID and connects automatically. If the game isn't running Steam launches it with `+connect_lobby <id>`; with test App ID 480 that launches Spacewar instead, so friends should have the game already open when accepting.
- **Direct (IP)** tab is unchanged: localhost/LAN and Multiplayer Play Mode (one Steam account can't connect to itself over Steam P2P).
- **Server rules** (`NetworkSession`): connection approval rejects players when the session is full (`maxPlayers`, default 4, host included) or when `Application.version` differs; the reason is shown to the rejected player. The host's menu lists connected players with *Kick*.
- **Names**: `PlayerIdentity` replicates each player's Steam name (or "Player N" off Steam), sanitised by the server, shown as nameplates above other players and in the host list.

## Components and extension points

- **NetworkSession**: direct Unity Transport connection, host/client/server menu, spawn transforms. No online account or Relay needed.
- **SteamBootstrap / SteamLobby / SteamP2PTransport**: Steam API lifetime, friends lobby and invites, Netcode transport over Steam Networking Sockets.
- **PlayerIdentity**: server-owned display name + nameplate.
- **CharacterAppearance**: server-assigned outfit index (NetworkVariable); owner requests the next outfit with C.
- **NetworkPlayerMotor**: owner reads Input System keyboard/mouse; server simulates the CharacterController at fixed timestep. Server-authoritative NetworkTransform replicates position/rotation. Speed, sensitivity, gravity, camera and hold point are editable on the player prefab.
- **PlayerInteractor**: owner finds a collider and requests interaction by NetworkObjectReference. Owner-only RPC, request throttling, server distance and line-of-sight validation precede dispatch. It has no battery, generator, shelf or door logic.
- **NetworkInteractable**: implement `TryInteract(PlayerInteractor, out string)` for new server-side rules. Put a collider and NetworkObject on the same root. Validate state before mutation. Use NetworkVariables for persistent data.
- **PickupItem**: server-owned Rigidbody/NetworkTransform; replicated holder and socket IDs; one held item per player. Physics runs on the server, while peers use kinematic bodies. Disconnect releases a carried item.
- **ItemDefinition**: create via Assets > Create > Coop > Item Definition. Display name and category are editable.
- **ItemSocket**: accepts any item, a category, or an exact ItemDefinition. Assign a child snap point, set lock-after-insertion, and wire **On Occupancy Changed** to server state/actions. An occupied unlocked slot can be interacted with to retrieve its item.
- **ActionInteractable**: generic button/lever endpoint; **On Server Interact** invokes Inspector-composed actions on the server.
- **NetworkState**: persistent server-written boolean. `SetState(bool)` / `Toggle()` are authoritative entry points. **On State Applied** runs on every peer at spawn and whenever the value changes, so late joiners reconstruct presentation.
- **StatePresentation**: optional example adapter for lights, active visual objects, or a moving part. You can instead wire NetworkState directly to your own presentation components. Keep the NetworkObject root active; only deactivate visual children.

## Add a new object

Duplicate the Crate prefab for a physical prop and assign its ItemDefinition. Add new dynamic spawn prefabs to `Prefabs/NetworkPrefabs.asset`. Scene-placed network objects are synchronized by NGO scene management.

For a machine, duplicate a slot, change its filter and snap point, add a NetworkState, then wire slot occupancy to `NetworkState.SetState`. Wire that state's presentation event to any combination of lights, animations, doors or custom components. Gameplay-affecting consequences belong in server callbacks; local presentation callbacks must not decide shared state.

For a button, add ActionInteractable and wire its server event to a NetworkState.Toggle or another authoritative action. Additional behaviors require no edits to PlayerInteractor.

Generator wiring: battery-category socket -> NetworkState.SetState -> StatePresentation.Apply (three lamps). Door wiring: ActionInteractable -> NetworkState.Toggle -> StatePresentation.Apply (door position). Shelf slots use exactly the same ItemSocket component as the generator.

## Scope

Unity 6000.3.10f1; Netcode for GameObjects 2.7.0 added to the existing Unity Transport 2.7.3 / Input System / URP project. Existing multiplayer services and tooling are preserved. Netcode is the missing GameObject replication layer; the preinstalled Services SDK does not replicate scene objects by itself.

This is localhost/LAN prototyping: no matchmaking, Relay, authentication, save-to-disk, prediction/reconciliation, jump, inventory, or host migration. Network state lasts for the running session, including late joiners, not across application restarts. Motion waits for the server, so WAN latency is noticeable. Carrying uses a fixed hold point and simple physics; it is not a collision-aware grab simulation. Re-enter Play mode or restart the executable to start a fresh world.

`PrototypeProbe` is an opt-in development-build integration harness (`-probe PATH`); it is inactive during normal play and absent from release builds. It exercises the real interaction RPCs and writes replicated state snapshots. Its server-only arrange command exists solely to position test players.

## Verification

Run `./Tools/Test-Coop.ps1` in PowerShell after making a Development build. It launches and cleans up a host, client, and late client on port 17777. Reports and per-process logs are saved under `TestResults/`. The completed run at `TestResults/20260927-130514/results.txt` passed movement, interaction range rejection, contested pickup, drop, category filtering, placement/removal across two slots, generator lamps, locked insertion, door action, late joining, and disconnect cleanup. A separate server-only connection check passed, and the standalone Host/Client UI and remote capsule visibility were visually checked.

## Frontend and dependency injection

The saved MainMenu scene includes UI Toolkit screens, a grassy clearing, animated character previews, a server-authoritative party and ready gate. Settings persist volume, mouse sensitivity and selected character locally. See `FrontendArchitecture.md` for responsibilities, editable assets and executed MPPM tests. No DI container package was added.

## Proximity voice and gameplay settings

In the MainMenu → GameWorld flow, hold **T** for proximity voice. Press **Escape** or **Settings & voice** during gameplay to change general/audio settings; this releases the cursor and suppresses local gameplay input without pausing the shared world. The party also has a Settings button. Voice settings include microphone selection, push-to-talk/open mic, input gain, activation threshold, local mic meter, mute/deafen and voice volume. Host-only distance/lobby-chat controls apply to the session. See `VoiceChat.md` for architecture, limits and MPPM tests. The legacy direct-play CoopWorkshop scene retains its original controls.

## Player character: Player_Orange

The player prefab now uses `Assets/3DModels/space+suit+figure+3d+model/Player_Orange.fbx` (Mixamo skeleton, Generic rig) as `Orange Visual`; `Bean Visual` stays in the prefab, disabled, as a fallback.

- The FBX has no animation. `Coop Prototype > Art > Use Player_Orange As Player` (`PlayerOrangeSetup`, mailbox `cporange build`) retargets all nine crew clips (Idle, Walk, Run, Carry, Carry Walk, Carry Run, Pick Up, Throw, Wave) from the Bean skeleton and bakes them to `Art/PlayerOrange/Animations/*.anim`.
- `Art/PlayerOrange/PlayerOrange.overrideController` swaps those clips into `BeanCrew.controller`, so states, parameters and `PlayerAnimation` are shared. To use a hand-made or Mixamo clip, drop it into the override controller slot (import it as Generic with the same bone names).
- `cporange pose` renders every clip to `TestResults/OrangePose/sheet.png`.
- One outfit only (`CharacterAppearance.variants` = Orange); the C key no longer changes the look. First-person arms and the main-menu crew previews still use the old astronaut art.
- Debug panel: no new entry needed (presentation only).

### Suit colours (replaces the single-outfit note above)

- `CharacterAppearance` swaps the body material between the `PlayerMat_*` materials (Orange, Blue, Green, Yellow; any further `PlayerMat_*` is picked up by `cporange build`).
- The server keeps colours unique: a joining player gets the lobby choice if free, otherwise the next free colour; C (and F1 > Player > "Next free suit colour") skips colours other players wear.
- The main-menu party previews use the same model and materials (`cporange menu`).
- First-person arms are cut from the same model (`cporange arms` → `Art/PlayerOrange/Arms/FP_Arm_Left/Right.asset`, straightened, scaled ×0.7) and follow the suit colour; the old astronaut mitts stay in the prefab, disabled.

### Modular visitor vehicles and imported alien

`GameWorld` now uses the imported **ModularPrivatePod** for shuttle visitors and **ModularFreightTruck** for cargo visitors (`Prefabs/Vehicles`). Separate Mount children keep the GLB modules editable without stretching their meshes. The moving `Traveller Shuttle` network root, authored routes, registration text, interaction, real cargo slots and server decisions are retained. The old art and the static inspection pod remain inactive as backups. Cargo socket snap points and the bed collision have been fitted to the new platform.

All existing race entries currently reference `Art/Checkpoint/ImportedAlien/CuteAlienTraveller.prefab`, wrapping the rigged `cute_alien_creature_3d_model.fbx`. It has ordinary editable Standing Idle / Seated Idle clips and a `Traveller.controller`. `VehicleOccupants` starts the seated clip immediately for drivers and companions; detention uses standing idle. `AlienPassengerPresentation` adds a smooth, limited head turn toward the closest spawned player within 5 metres, checked locally on every peer, including remote players. No RPC or gameplay authority is added. Tune range/turn limits on the prefab; tune seating at the scene's `Occupant seats` transforms. The amber loading pads sit on the accessible bed edges; each pad's separate snap point places its crate inside the bed.

Real-model portraits and vehicle codex pictures match the new art. A distinct `CheckpointSession.mismatchedPortrait` preserves the false-ID-photo check while travellers share one portrait. Existing traveller identities and race-rule data are unchanged; the second species can get its own prefab/portrait later by changing `RaceDefinition.model`/`portrait` and the relevant travellers' portraits. Existing per-case skin colours do not recolour this textured model.

F1 > Visitors still calls Shuttle / Truck / With companions, stops an arrival and exercises decisions; the new local gaze status lists each occupant's target. Editor probe: `cpvisitorart`. After two MPPM players enter GameWorld, run `Tools/Test-VisitorArt.ps1 -Client <clone-project-path>` for the focused regression (19 checks passed in this task; all four cargo slots were also checked separately). Visual and runtime evidence is in `TestResults/VisitorArt`. Tripo's original cockpit surface irregularities remain; this integration does not remesh the imports. The legacy `CheckpointNetworkTest` scene retains its older vehicle art; use MainMenu → GameWorld for the new assembly.

Gaze rotation regression: `Tools/Test-VisitorGaze.ps1 -Client <clone-project-path>` samples each visitor's final presented head quaternion via `cpvisitorpose`. It checks sustained stationary targets, switching peers/sides, returning to neutral outside range, truck seating and standing in detention. This catches cumulative spinning that target-selection-only tests miss. The gaze offset is removed before animation each frame and reapplied once afterward; constant animation curves are not assumed to reset the bone.

## Spline roads

The checkpoint roads are built from splines (`com.unity.splines`): `Spline Roads` in GameWorld holds `Main lane`, `North lane` and `South lane`, each a `SplineContainer` + `SplineRoad` (`Scripts/World/SplineRoad.cs`).

- `SplineRoad` sweeps a cross-section (surface, glowing edge line, raised kerb, skirts, underside, centre dashes, optional guard rails) along the spline and rebuilds whenever a knot is moved in the Scene view, and on enable at runtime. The mesh is not stored in the scene.
- Kerbs and dashes are left out where they would land on a neighbouring road, so forks and merges are clean; `priority` decides which road keeps its markings.
- `Coop Prototype > Checkpoint > Build Spline Roads` (`cproad build`) recreates the three roads from the default route and disables the old `Road segment` boxes and `Exit ramp guards` (kept in the scene for rollback). It overwrites hand-edited knots. `cproad shot` renders `TestResults/Roads/*.png`.
- Vehicles still follow the straight `arrivalRoute` / `departureRoute` points; moving them onto the splines is a separate step.
- Backup: `Tools/Checkpoint/BeforeSplineRoads/GameWorld.unity`.

### Station art and canyon environment — 2026-10-02

GameWorld now uses the new Tripo props for crew seating, storage, service equipment, plants, freight dressing, ceiling fixtures and the working supply kiosk. CargoPackage's outer case is imported; its network root, X-ray contents and truck/pickup flow are retained. The inspection desk has graphite/porcelain trim and a live phase/day/document-count readout. The shared IMGUI inspection/shop panels use warmer station colours.

The flat two-colour landscape is preserved inactive and replaced with saved irregular sandstone meshes, a canyon basin, floating waterfall islands, a cloud-gradient sky and ringed peach planet. This background is mesh-based, not Unity Terrain, and has no gameplay collision. Edit the saved GameWorld hierarchy and reusable prefabs directly; there is no retained rebuild tool. Details, backups, Unity AI quote status and validation limits: `Art/Checkpoint/ProductionPass/README.md`. Validation-only command: `cpstationart`.

### Document holo-pads — 2026-10-03

All documents using `Prefabs/CheckpointDocument.prefab` now appear as slim handheld holo-pads rather than thick slabs. A chamfered porcelain shell, graphite recess, orange grips and cyan projector rails frame a slightly raised translucent display with subtle scrolling scan lines. The existing live text, portrait, seal, barcode and UV watermark references remain; document data, network identity, inspection controls and scanner-tray behavior are unchanged. The single pickup collider is thinner to match the device.

Edit the saved prefab and `Art/Checkpoint/HoloDocuments` meshes/materials directly; the custom URP display shader is `Art/Checkpoint/HoloDocument.shader`. Backup: `Tools/Art/BeforeHoloDocuments/CheckpointDocument.prefab`. The one-off authoring script and mailbox hook were removed after verification. F1 needs no new command: existing arrival/papers/flaw/spawn-UV controls exercise this presentation.

Verified: supported/error-free shader, intact live UI/UV references, single thin collider, close-up inspection, genuine UV watermark visible, forged UV watermark absent, return to scanner tray, and 19 checks in the two-editor MPPM visitor regression. Evidence: `TestResults/HoloDocuments/Pad.png` (sample-content art preview), `TestResults/Checkpoint/HoloDocument-inspection.png` (live document), and `TestResults/VisitorArt/MPPM-results.txt`. No standalone build or profiling was performed.

### Setup tools removed

The one-off builders used for the player model, run animations, arm binding and spline roads (`PlayerOrangeSetup`, `RunAnimationSetup`, `CrewBindFix`, `CrewPoseCheck`, `RoadSplineSetup`) and their `cporange` / `cprunanims` / `cpcrewbindfix` / `cpcrewpose` / `cproad` commands and menu items have been deleted so they cannot overwrite hand edits. Mentions of them above are historical; edit the prefab, clips, override controller and splines directly. See `AGENTS.md` > "One-off setup and rebuild scripts".
