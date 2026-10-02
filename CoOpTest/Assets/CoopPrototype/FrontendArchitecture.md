# Frontend architecture

## Decision: explicit, manual dependency injection

DI fits the menu/session boundary: presentation, local preferences and authoritative network state have different responsibilities and lifetimes. A container such as Zenject/VContainer is unnecessary at this project size. No package was added. Inspector references and small Initialize methods make dependencies visible without introducing a service locator or rewriting the existing interaction components.

- `MenuCompositionRoot` creates `LocalPlayerProfile` (behind `IPlayerPreferences`) and injects it, the session and backdrop into `GameFlow` / `MenuPresenter`.
- `MenuPresenter` binds editable UI Toolkit controls to commands and renders state. It never writes shared NetworkVariables.
- `GameFlow` owns connection, party membership, all-ready checks, coordinated scene loading and session teardown. The persistent Session Scope contains the manager, transports, flow and menu UI.
- `LobbyMember` is NGO's lightweight player prefab during the menu. The NGO spawn callback resolves its own manager's GameFlow once, then receives its dependencies. Owner-only RPCs validate character/name/readiness on the server; name, character and readiness are server-written NetworkVariables.
- `GameSceneContext` supplies authored spawn points and loading camera after scene load. The server waits for NGO's load completion, replaces lobby players with gameplay players and applies the selected profiles. Clients cannot launch the round.
- Existing movement, interaction dispatch, pickup, sockets and state presentations retain their separate server-authoritative components. No container is needed for local component composition or Inspector UnityEvents.

This is deliberately a modest DI boundary, not a claim that every MonoBehaviour is container-managed. Additional interchangeable local services can use interfaces and be supplied by the composition root. Additional world interactions still extend NetworkInteractable/composed actions; they do not depend on menu services.

## Editable assets

- `Scenes/MainMenu.unity`: entry scene, Session Scope and composition references, animated crew previews, camera, lights, trees, grass and rocks.
- `Scenes/GameWorld.unity`: independently saved workshop copy with GameSceneContext; it has no duplicate session manager. Load it through MainMenu to play.
- `Scenes/CoopWorkshop.unity`: preserved legacy direct-play scene, including late-join testing. Future edits to one workshop scene do not automatically update the other.
- `UI/MainMenu.uxml`, `UI/MainMenu.uss`, `UI/MainMenuPanel.asset`: screens, layout, styling and scaling, editable in UI Builder/Inspector.
- `Prefabs/LobbyMember.prefab`, `Prefabs/NetworkPlayer.prefab`, `Prefabs/NetworkPrefabs.asset`: registered network assets.
- Character preview meshes/controller are copied from the existing Bean Crew visual hierarchy. Materials, animation and preview wave interval remain editable.
- Volume, sensitivity, name and character use local PlayerPrefs. MPPM processes share the same PlayerPrefs store, so a new session may start with the last saved selection from either editor; connected players still have independent replicated selections.

The one-time MainMenuSetup editor authoring tool opens existing menu content instead of regenerating it. Runtime code does not generate the level. Build settings start with MainMenu. MainMenu supports 2–4 configured capacity (host may start alone if ready); four characters can be chosen without enforcing uniqueness.

## Player flow

Choose character / Settings → Start playing → Direct IP / LAN or Steam friends → Host / Connect → party roster → everyone ready → host Start game → synchronized GameWorld.

Changing character clears that player's ready flag. A joining player starts unready. Admission closes during loading and gameplay; this party flow has no mid-round joining, reconnection or host migration. Legacy CoopWorkshop still supports persistent-state late joining. Escape releases the cursor so Leave game (in Settings) can be clicked; leaving replaces the session scope and returns to MainMenu. Host departure returns clients to the menu.

Direct localhost testing uses MPPM Player 2 and 127.0.0.1:7777 without builds. LAN friends use the host machine's reachable LAN IP. Steam uses the existing friends lobby/P2P implementation; Host then Invite friends lists online friends, and accepting an invite connects to the lobby. No invites are sent automatically. Test App ID 480 requires the game already open on both accounts. Steam P2P cannot be tested between MPPM instances sharing one account.

## Validation (2026-09-28)

Executed in the main editor and MPPM Player 2, without standalone builds:

- Both peers connected in MainMenu with replicated roster, character and ready state.
- Host start rejected before all ready; client start rejected even when all ready.
- Character change replicated and reset readiness; host could start after both readied again.
- NGO loaded GameWorld on both peers, exactly one gameplay player per peer, selected character indices preserved.
- Client movement observed on host. Pickup/carry, competing pickup rejection, shelf placement and removal observed on client. After correcting the Battery category, insertion and generator powered state were observed on the client.
- Client Leave returned to MainMenu; attempts to join an active round were rejected. Host Leave returned the remaining client to MainMenu. Re-host/reconnect and a second shared game start succeeded.
- Start playing and Host buttons exercised through the visible UI; main menu and party presentation visually checked.

Compile/runtime checks use each peer's Editor log/Console. Existing Unity AI/license-service entitlement messages are unrelated to this feature. No standalone build or two-account Steam invitation round trip was executed for this change. Four-peer presentation, unusual aspect ratios and gamepad navigation remain additional coverage to add.

A regression check found the existing Battery ItemDefinition had category `Prop`, while the generator required `Battery`. Its category/display name were restored through Unity's asset API; no generator-specific player logic was added.

## Voice and shared settings

The composition root now also injects a session-scoped ProximityVoice component and local VoicePreferences. SettingsPanel binds the same settings controls in the main menu, lobby and GameWorld. Escape opens/closes Settings during gameplay; the local player's input and microphone transmission are suppressed while it is open, without pausing the shared world. See `VoiceChat.md` for audio responsibilities, host rules and executed tests.
