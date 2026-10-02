# Velora art direction

The current checkpoint and crew lobby use the September 30 reference direction: warm porcelain and orange station hardware, graphite inserts, cyan portal energy, amber task lighting, and a lavender/peach planet with sandstone mesas and floating islands.

Saved assets are editable. `PlanetArtRedesign` authors GameWorld, MainMenu and CheckpointNetworkTest, rounds the existing primitive prop meshes without changing their colliders or component references, and adds station details. The original scenes before this art pass are copied to `Tools/Checkpoint/BeforePlanetRedesign`. CoopWorkshop remains the legacy regression scene.

`PlanetCrew.fbx` contains four astronaut suit meshes built against the existing BeanCrew skeleton. The authoring tool transfers those meshes onto the existing renderers and animation rig, retaining outfit selection, the network player prefab and its controller. The Blender construction source is `Tools/Art/build_planet_crew.py`.

Editor menu: Coop Prototype > Art > Apply Velora Planet Redesign. The existing editor mailbox also supports `planetinspect`, `planetpreview` (wide, close, menu), `planetrefine`, `planetcrew`, `planetarms` and `planetvalidate`. These commands are editor-only and are not shipped as gameplay endpoints. The apply command is a first-time authoring pass; refine updates the landscape, lighting and equipment finish without rebuilding gameplay objects.

The procedural sky and portal surface use saved shaders under Art/Checkpoint. Portal movement and energy are cosmetic. Landscape objects do not alter the station's movement boundaries. Camera post-processing uses the saved Planet grading VolumeProfile.

`PlanetArms.fbx` and `Tools/Art/build_planet_arms.py` supply matching first-person sleeves, white cuffs and black gloves. Existing first-person arm pivots, outfit selection and item presentation scripts remain unchanged. The original Tencent `CharacterCoOp` experiment is preserved, inactive in MainMenu, rather than deleted or silently substituted for the playable rig.

Validation (2026-10-01): the active Unity 6000.3.10f1 editor compiled and executed the authoring pass. Checkpoint asset/reference checks passed for all three scenes, documents and the conveyor/X-ray setup. All four new crew meshes were baked at three times in each existing controller clip (Idle, Walk, Carry Walk, Throw); bone/bindpose counts and finite deformation bounds passed, and close-up renders were inspected. A single-editor host started through MainMenu, loaded GameWorld, moved, received a visitor and spawned both requested documents. The updated first-person arms were rendered in play mode. The Unity Console reported no errors. New two-peer/late-join testing and performance profiling were not run in this visual-only pass.

Actual Unity renders and the crew check report are in `TestResults/PlanetRedesign`. This is a stylized, editable art-direction pass, not a claim of cinematic reference fidelity. The remaining difference is primarily bespoke high-detail prop modelling, richer surface textures and final lighting polish.
