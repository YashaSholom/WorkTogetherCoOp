> **Superseded:** the active player art is now the Bean Crew (see `BeanCrew/README.md`). This document describes the legacy worker, kept in `Art/Legacy` and as the inactive `Legacy Worker Visual` on the player prefab.

# Workshop character and props

The worker is an original low-poly character with a cap, large nose, work overalls and heavy boots. The skeleton uses rigid weights for a deliberately simple articulated style. It is a Generic Unity rig, not a Humanoid retargeting rig.

## Editing

- Blender source: `Tools/Art/WorkshopArt.blend` at the project root. Named collections contain the worker, crate, battery, spanner, generator and bench. Prop collections are hidden in the source initially; reveal them in Blender's Outliner.
- Exported models: `Assets/CoopPrototype/Art/Models`. Unity imports FBX directly, so other developers do not need Blender installed to run the game.
- Materials: `Art/Materials`; edit their URP colors in the Inspector.
- Animation clips and state machine: `Art/Animations/WorkshopWorker.controller`. Open it in Animator; edit the five `.anim` clips in the Animation window. The controller uses `Speed` (0–1) and `Carrying` (bool), plus the named `Throw` state.
- Player setup: `Prefabs/NetworkPlayer.prefab`. `PlayerAnimation` references the Animator. `PlayerThrow` exposes speed, upward boost, release delay, duration and spin. Keep the throw clip's release pose aligned with the release delay (initially 0.25 seconds).
- Camera: `NetworkPlayerMotor` exposes third-person distance, shoulder offset and the default camera mode. The hold point is a normal child Transform.

Idle, walking, carrying, walking while carrying and throwing use saved animation clips. Root motion is disabled: the server's CharacterController owns movement. Walking animation follows replicated movement; carrying follows HeldItem; throw timing follows a server-written timestamp so observers animate the same action. Changing an animation does not grant it gameplay authority.

Controls: WASD/mouse, E interact, Q drop, right mouse throw, V first/third-person view, Escape releases the cursor. Click the world to capture it again.

## Authoring utilities

`Tools/Art/create_workshop_art.py` documents the original model construction. Running it overwrites its FBX outputs and Blender source, so keep hand-edited versions separately before regenerating. Normal iteration requires no regeneration.

`Coop Prototype > Art > Apply Workshop Art Upgrade` performs the initial migration through Unity APIs. It preserves existing Art Visual children and the existing Animator controller. A copy of the old scene and prefabs is in `Tools/Art/BeforeArtUpgrade`.

The editor-only `WorkshopEditorValidation` utility accepts explicit local test requests from each editor's `Temp/WorkshopRequest.json`. It is inactive without that file and is excluded from builds. Multiplayer Play Mode is the preferred test environment.

This pass uses simple weights and fixed carry poses. It does not include hand IK for different item sizes, finger animation or Humanoid retargeting. Collision shapes remain deliberately simple for stable network physics.
