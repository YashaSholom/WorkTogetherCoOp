# Checkpoint production art pass — 2026-10-02

GameWorld contains `Space Checkpoint / Production art pass` with editable station fittings and a mesh-based canyon vista. The old `Velora planet landscape` is preserved inactive. This is a first production-direction art pass, not a final cinematic environment.

## Station assets

- Imported Tripo benches, shelves, tool cart, utility station, alien planters, freight cases, loading plinth and ceiling fixtures are placed at uniform scale. Existing manually placed imported props are preserved.
- The imported display is now the working crew supply kiosk. The existing ShopTerminal, dispenser, prices and interaction collider stay in place. Screen glass/text are separate editable scene objects, not painted into the texture.
- The inspection desk keeps its six document sockets and next-day button. `StationTerminalReadout` displays the existing replicated phase/day/document count locally; it never changes server state.
- CargoPackage retains its original network root, GUID, collision, pickup logic, X-ray contents and contraband visibility. Its outer visual is the imported crate; a separate seal carries the case's colour.
- Saved reusable prefabs are in `Prefabs/`. These are ordinary authored assets, not runtime generators. Imported models/materials in `Assets/TripoModels` have not been globally retinted or overwritten.

## Environment

Five irregular stepped sandstone meshes, a desert basin, four floating islands with animated waterfall veils, pastel cloud sky and a peach ringed planet replace the flat split-colour landscape. No Unity Terrain is needed: this is distant non-walkable vista geometry, and deliberately has no collision. Station gameplay stays on its existing deck.

Mesh/material assets are editable in `Meshes/` and this folder. Sediment colour/frequency can be changed on the sandstone materials; sky colours on Canyon sky. Waterfall speed/shape can be refined in WaterfallVeil.shader and the saved meshes. The shader is a lightweight stylized approximation, not simulated water.

## Unity AI

Installed Unity AI Generators was queried successfully. Its `model3d-tripo-p1` accepts a transparent image reference; `AIReferences/Sandstone-mesa.png` is an isolated render of our authored rock, with transparency. A quote-only request returned **6 Unity AI points** for one FBX/mesh prefab. The permission callback canceled before generation. **No paid AI model was generated.** Ask for approval before submitting; quote again if prices/model availability change.

Suggested refinement prompt: "Stylized warm orange sandstone mesa rock formation for a cheerful alien desert spaceport. Broad terraced cliff top, irregular eroded vertical canyon walls, layered pale peach and rust sandstone strata, rounded weathered edges, standalone single rock, game environment prop, PBR material, no buildings, no ground plane, no vegetation, no text."

## Verification and editing

- Read-only editor command `cpstationart` checks saved hierarchy, references, cargo visuals/X-ray, live screen status and shader support. It does not rebuild content.
- MPPM: 19 visitor/cargo checks passed with host and Player 2. Live screen state was checked on both peers; the imported case was moved from the truck through the scanner and contraband was detected. Terminal and supply UI were opened and visually inspected.
- EditMode shader checks passed in both editors after explicitly loading shader assets (Shader.Find alone initially failed to locate them in the secondary editor). The secondary player's rendered scene was visually inspected.
- Evidence: `TestResults/ArtAdvance`, `TestResults/VisitorArt/MPPM-results.txt`, `TestResults/Checkpoint/ArtAdvance-terminal.png`, `ArtAdvance-shop.png`, `Xray.png`. No standalone build or performance-budget profiling was run.
- F1: existing Visitors/Papers/Decide/Shop/Navigation controls exercise all relevant states. No new art debug action: scenery is static authored content; the readout exposes existing state rather than new hidden state.
- Backup of pre-pass GameWorld and CargoPackage: `Tools/Art/BeforeProductionPass` (outside Assets). Source imports and original inactive landscape are also preserved.
- Only GameWorld received this art pass; MainMenu and the legacy CheckpointNetworkTest scenery were not rebuilt. Older MainMenu CharacterCoOp missing-prefab warning is pre-existing.

Next visual priorities: finer hero cliff topology/textures, sky/planet atmospheric detail, more integrated console housings, LODs for dense Tripo props, and measured lighting/performance tuning. No final-performance claim is made for this pass.
