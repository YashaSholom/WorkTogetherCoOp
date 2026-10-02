# Project initialization and orientation

## Project

- Document holo-pad pass (2026-10-03): `CheckpointDocument.prefab` now has a thin chamfered device shell, orange grips and a raised cyan holographic display. Live document contents and UV checks remain unchanged. Saved meshes/materials: `Assets/CoopPrototype/Art/Checkpoint/HoloDocuments`; shader: `HoloDocument.shader`. No retained authoring/rebuild tool.

- Station art pass (2026-10-02): GameWorld has imported Tripo station furniture, a working supply kiosk, imported pickup cargo case, live inspection readout and a new mesh-based canyon vista. Edit `Space Checkpoint / Production art pass` directly. Reusable prefabs and notes: `Assets/CoopPrototype/Art/Checkpoint/ProductionPass/README.md`. Read-only validation: `cpstationart`. Unity AI returned a 6-point model quote; generation is awaiting approval, not completed.

- Visitor art (2026-10-02): GameWorld uses the modular private pod and freight truck, with the imported rigged cute alien for drivers, companions and detainees. F1 > Visitors can call either vehicle and shows local gaze targets. Seated/standing idle and nearest-player head tracking within 5 m are presentation only. See README > Modular visitor vehicles and imported alien.

- Current feature: read `ALIEN_CHECKPOINT.md`. MainMenu and GameWorld now use the alien space-checkpoint setting. `CheckpointNetworkTest.unity` supports direct-host/late-join tests of the new feature; CoopWorkshop stays unchanged as a legacy regression scene.
- New controls: right mouse inspects a held item near the camera; hold and drag it to rotate, scroll to zoom, and right-click again (or Esc) to close. E talks/places/retrieves/opens the computer, Q drops, F throws. Request papers from the stopped shuttle, share them, place both on the scanner trays, then read them at the terminal and approve/reject.
- Running (2026-10-01): hold **Shift** while moving forward to run (1.65× speed, `NetworkPlayerMotor.runSpeed`; stacks with coffee). Backing up or strafing alone stays a walk. The server sets the replicated `Running` flag; `Run` / `Carry Run` animations play on every peer.
- Document slots (2026-10-01): requested papers go straight into the six document slots on the terminal desk (scanned there); there is no collection shelf any more. START NEXT DAY is on the desk's right wing.
- Debug panel (2026-10-01): **F1** in the Editor or a Development build. Every new feature must be evaluated for it; see `FEATURE_IMPLEMENTATION.md`.
- Detention and codex (2026-10-01): the terminal opens at any time and has CASE / FEDERATIONS / VEHICLES / RACES tabs; ARREST sends the visitor and companions to the holding cell in the detention wing behind the hall. E on the cell tablet lists prisoners and sets them free. The Galactic Police collect prisoners at the start of the day two days after the arrest (bounty if justified, 250 fine per innocent prisoner). Editor mailbox: `cpdetention`, `cpdetentionvalidate`, `cparrest`, `cpcell`, `cprelease`, `cptablet`, `cpforcenextday`, `cpshot`.
- MPPM environment note (updated 2026-10-01): the Player 2 clone `mppmee16feb0` still pointed its `Assets`/`ProjectSettings` junctions at the former `D:\CoOpTest` location, so it force-quit on startup ("Moving file failed ... mppmee16feb0/ProjectSettings/InputManager.asset"). Its junctions were repointed to this project and it now loads. If the project folder is moved again, run **Coop Prototype > Multiplayer Play Mode > List Clone Links** and **Repair Stale Clone Links** (editor mailbox: `cpmppmlinks`, `cpmppmrepair`) with the clone editors closed. Only the links are replaced; no files are deleted.

- Unity project root: `D:\WorkTogetherCoOp\CoOpTest` (open this folder through Unity Hub).
- Asset root: `D:\WorkTogetherCoOp\CoOpTest\Assets`.
- Editor: Unity `6000.3.10f1`.
- Entry scene: `Assets/CoopPrototype/Scenes/MainMenu.unity`; gameplay scene: `GameWorld.unity` in the same folder.
- Legacy direct-play/testing scene: `Assets/CoopPrototype/Scenes/CoopWorkshop.unity`.
- Read `AGENTS.md` before making changes and `Assets/CoopPrototype/README.md` for component details.
- Check `Packages/manifest.json` and `Packages/packages-lock.json` for current dependencies. At initialization: NGO 2.7.0, Unity Transport 2.7.3, Multiplayer Play Mode 2.0.2, Input System 1.18.0, URP 17.3.0. Multiplayer Services/Tools/Center are also installed.

## Preferred test workflow: Multiplayer Play Mode

Use the installed Multiplayer Play Mode package for routine multiplayer iteration whenever possible. A standalone rebuild is not required for each gameplay change.

1. Open `MainMenu.unity` by itself and save changes.
2. Open **Window > Multiplayer > Multiplayer Play Mode** and enable **Player 2**. This starts an additional Unity editor; no build is needed.
3. Press Play and wait for Player 2. Both load MainMenu. **Choose character** cycles Bolt, Gus, Pip and Dot.
4. Choose **Start playing** in both. Use **Direct IP / LAN**, address `127.0.0.1`, port `7777`. Host on the main editor; Connect on Player 2.
5. Both players click **I'm ready**. The host's **Start game** enables only when everyone is ready. It loads GameWorld for both peers; selecting another character clears readiness.
6. Click the world to capture the mouse. WASD moves, Shift (held, moving forward) runs, mouse looks, E or left click is the primary action (interact with what you aim at, otherwise use a usable held item such as coffee, otherwise drop the held item), Q drops, F throws, hold the right mouse button to inspect the held item (move the mouse to turn it, scroll to zoom, release to put it back in your hands), 1-3 or the mouse wheel switch between the three item slots, F1 (editor/development builds) opens the debug panel (call any visitor type, force flaws, decide, jump the clock, end days, police visits, money, free items, teleports), C changes outfit, V toggles camera. **T** is push-to-talk. **Escape** opens/closes gameplay Settings and releases the cursor. **Leave game** (in Settings) returns to a fresh menu/session.
7. Stop Play mode in the main editor when finished. Edit and save assets/code in the main editor, allow compilation to finish, then run again.

Switch focus using **Ctrl+F9** (main player), **Ctrl+F10** (Player 2), **Ctrl+F11** (Player 3), or activate the Player 2 window directly. In an additional editor, the **Layout** dropdown can expose its Console, Hierarchy and Inspector for debugging.

The prototype uses manual Host/Client buttons. Scenario tags or roles alone do not call NGO's StartHost/StartClient in this project. No Relay, Unity Services sign-in, or Dedicated Server package is required for localhost testing.

The MainMenu party flow closes admission when a round starts. For persistent-world late-join testing, open the preserved `CoopWorkshop.unity` direct-play scene and configure two additional editors. Start the host and Player 2 client, leave Player 3 at the connection menu, change world state, then click **Start Client** in Player 3. Verify existing item placements, generator lamps, and door state.

**"Failed to bind UDP socket ... port 7777" when hosting:** usually the editor itself still holds the port from a session that was interrupted by a script recompile during Play (Unity's "Recompile And Continue Playing" drops Netcode's state but leaves the socket open until Unity restarts). Run **Coop Prototype > Multiplayer Play Mode > Who Uses Port 7777** (mailbox `cpport`); if it is the editor's own process, restart Unity. `NetworkReloadGuard` now closes all transports before any reload in Play so this should not recur; setting Preferences > General > Script Changes While Playing to *Recompile After Finished Playing* avoids it entirely.

If connection fails, confirm the host started first, all instances use this scene and the same port, and no old standalone host occupies port 7777. Check the Console in both editors. Unity documentation may also describe **Window > Play Mode > Scenarios**; use the Virtual Players window above when it is available in this editor.

## Steam testing

Steam P2P needs two Steam accounts (two PCs, or a friend). Both run the same build/Editor version with Steam open; the host chooses Start playing > Steam friends > Host, then Invite friends. Both ready up before the host starts. MPPM instances share one Steam account, so keep using the Direct (IP) tab there. See `Assets/CoopPrototype/README.md` > Steam.

## Architecture and editable content

`Assets/CoopPrototype` contains normal saved Scenes, Prefabs, Materials, Items, Scripts, and Editor tools. Tweak Inspector values and serialized UnityEvents directly. Preserve developer edits; scene generation must not overwrite existing work.

- `NetworkSession`: transport, connection UI, spawn points.
- `NetworkPlayerMotor`: owner input, server CharacterController simulation, NetworkTransform replication.
- `PlayerInteractor`: owner-only interaction RPCs, server reach/obstruction checks, generic dispatch.
- `NetworkInteractable`, `PickupItem`, `ItemSocket`, `ActionInteractable`: reusable object behavior.
- `ItemDefinition`: editable item identity/category assets.
- `NetworkState`: persistent server-written state; applies presentation on late join as well as changes.
- `StatePresentation`: example lights/visual objects/door adapter.

Generator and door behavior is composed through Inspector events; the player has no generator-specific logic. New interaction types should extend these components or implement NetworkInteractable without changing core player dispatch.

## Builds and validation

Use **Coop Prototype > Build Windows Prototype** only when a standalone/platform check is needed or editor testing is unavailable. Output: `Builds/CoopWorkshop/CoopWorkshop.exe`. The optional `Tools/Test-Coop.ps1` regression harness requires a Development build and writes reports under `TestResults/`; it is not the default iteration loop.

Existing standalone checks passed host/client movement, pickup/drop, contested pickup rejection, category filtering, socket placement/removal, generator state/lights, door state, late joining, and disconnect cleanup. This guide documents the MPPM workflow; those prior results were standalone tests, not MPPM validation.

Current scope: localhost/LAN prototype, simple carry physics, no prediction/reconciliation, matchmaking, host migration or disk persistence. Shared state lasts for the running session.

## Unity documentation

- [Create a Play Mode scenario](https://docs.unity3d.com/Packages/com.unity.multiplayer.playmode@2.0/manual/play-mode-scenario/play-mode-scenario-create.html)
- [Editor instances and focus shortcuts](https://docs.unity3d.com/Packages/com.unity.multiplayer.playmode@2.0/manual/instance-types/main-and-additional-editor-instances.html)

## Main Menu architecture

See `Assets/CoopPrototype/FrontendArchitecture.md` for the manual DI decision, session lifetime, editor wiring, validation results and limitations. GameWorld is an independent editable copy of the workshop; future scene changes do not automatically propagate between them. Build scene order starts with MainMenu.

Proximity voice and the shared in-game Settings panel are documented in `Assets/CoopPrototype/VoiceChat.md`. Voice uses the existing transport; use Direct/IP in MPPM and headphones for real microphone tests. Push-to-talk is the default; the Settings mic meter is local only.
