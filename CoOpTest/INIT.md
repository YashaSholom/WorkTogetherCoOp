# Project initialization and orientation

## Project

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
6. Click the world to capture the mouse. WASD moves, mouse looks, E interacts, Q drops, right mouse throws, C changes outfit, V toggles camera. **T** is push-to-talk. **Escape** opens/closes gameplay Settings and releases the cursor. **Leave party** returns to a fresh menu/session.
7. Stop Play mode in the main editor when finished. Edit and save assets/code in the main editor, allow compilation to finish, then run again.

Switch focus using **Ctrl+F9** (main player), **Ctrl+F10** (Player 2), **Ctrl+F11** (Player 3), or activate the Player 2 window directly. In an additional editor, the **Layout** dropdown can expose its Console, Hierarchy and Inspector for debugging.

The prototype uses manual Host/Client buttons. Scenario tags or roles alone do not call NGO's StartHost/StartClient in this project. No Relay, Unity Services sign-in, or Dedicated Server package is required for localhost testing.

The MainMenu party flow closes admission when a round starts. For persistent-world late-join testing, open the preserved `CoopWorkshop.unity` direct-play scene and configure two additional editors. Start the host and Player 2 client, leave Player 3 at the connection menu, change world state, then click **Start Client** in Player 3. Verify existing item placements, generator lamps, and door state.

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
