# Co-op Workshop

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
- Click the world to capture the mouse. WASD moves, mouse looks, E interacts, Q drops, right mouse throws, V toggles first/third person (first person is the default), C changes outfit, Escape releases the mouse. Sessions continue when unfocused.
- Rebuild after edits via **Coop Prototype > Build Windows Prototype**. The create-scene command opens an existing workshop and never overwrites your edits.

## Art

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
