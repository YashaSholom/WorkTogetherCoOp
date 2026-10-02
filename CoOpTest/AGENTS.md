# Project guidance

Applies to this entire Unity project.

## Start here

- For the alien checkpoint feature, read and maintain `ALIEN_CHECKPOINT.md`. It contains the user brief, live checklist, current implementation status and cross-account handoff instructions.

- Read `INIT.md` and `Assets/CoopPrototype/README.md` before changing the prototype.
- Every new feature follows `FEATURE_IMPLEMENTATION.md`, including evaluating it for the F1 debug panel and adding its debug commands/buttons (`Scripts/Debugging`).
- Project root is `D:\WorkTogetherCoOp\CoOpTest`; `Assets` is its asset directory, not the Unity project root.
- Inspect installed packages and existing systems before adding dependencies or duplicating functionality.
- Use the project's Unity version from `ProjectSettings/ProjectVersion.txt`.

## Testing preference

- **Prefer Multiplayer Play Mode for multiplayer testing whenever possible. Do not rebuild standalone players for every iteration.**
- Use **Window > Multiplayer > Multiplayer Play Mode** and enable **Player 2** to start the tested additional-editor workflow. This is the Virtual Players UI exposed by the installed MPPM 2.0.2 package. See `INIT.md` for setup.
- Use the existing Start Host/Start Client buttons and `127.0.0.1:7777`; tags do not automatically start networking in this project.
- For relevant networking changes, check at least two peers: movement, pickup/drop, competing interactions, slot placement/removal, and synchronized effects. Test persistent state with a late-joining peer when state handling changes.
- Check each peer's Console, fix compile/runtime errors, and distinguish executed tests from unverified behavior in reports.
- Use standalone builds for platform/build-specific checks, the optional build-based regression harness, or when MPPM cannot cover the test. State the reason for falling back.
- Do not claim the existing `Tools/Test-Coop.ps1` harness tests MPPM; it launches standalone processes.

## Implementation rules

- Keep a normal editable Unity project: saved scenes/prefabs, readable modular scripts, serialized Inspector settings, ScriptableObject item definitions, and visible UnityEvent wiring.
- Preserve existing scene/prefab edits. Do not replace authored content with opaque runtime generation.
- Clients handle their own input and request interactions; the server validates requests and changes shared state.
- Keep movement, interaction dispatch, pickup, placement, and gameplay reactions separate.
- Player interaction code must remain generic. Put item/socket/action-specific behavior on world-object components.
- Represent persistent state with server-written NetworkVariables or equivalent replicated state; initialize presentation for late joiners. Do not use one-shot RPCs as the sole source of persistent state.
- Retain request ownership checks, distance/obstruction validation, and exclusive pickup/slot transitions. Release held items on disconnect.
- Log meaningful connection, interaction and state changes, not per-frame activity.
- Preserve Unity `.meta` files and GUIDs. Do not edit generated `Library`, `Temp`, or package-cache content as project source.
- Update project documentation when changing setup, controls, architecture, or testing workflows.

## One-off setup and rebuild scripts

- Never leave a script in the project that rebuilds or regenerates authored content from hard-coded values (scene objects, splines, prefabs, animation clips, meshes, materials). The owner edits these by hand, and a re-run would overwrite those edits.
- Such a script may be created to carry out a task. Once it has run and the result is verified, delete the script, its `.meta`, its menu item and any mailbox command that calls it, in the same task. Keep what it produced.
- Do not add `[MenuItem]` entries for one-off setup or rebuild work.
- Never re-run an existing setup or rebuild script (including the older `*Setup` / `PrototypeBuilder` / `PlanetArtRedesign` editor tools) without the owner asking for it explicitly; assume the scene, prefabs and assets have been edited by hand since.
- To change something that was generated earlier, edit the existing objects and assets in place instead of regenerating them.
- Runtime components that derive presentation from authored data (for example `SplineRoad`, which builds the road mesh from the spline the owner edits) are not rebuild scripts and stay.
