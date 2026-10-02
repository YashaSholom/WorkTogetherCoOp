# Feature implementation checklist

Applies to every new gameplay feature, system or content type in this project. Read it with `AGENTS.md` before starting, and tick it off before calling a feature done.

## 1. Before building

- Read `AGENTS.md`, `INIT.md`, `ALIEN_CHECKPOINT.md` and `Assets/CoopPrototype/README.md`. Inspect the systems you will touch.
- Decide what is **server state** (NetworkVariable / NetworkList written by the server) and what is **local presentation** rebuilt from it, so late joiners see the same thing.
- Keep `PlayerInteractor` generic: the behaviour belongs on the world object (a `NetworkInteractable`, `PickupItem`, socket, panel...).

## 2. While building

- Normal editable Unity content: saved scenes and prefabs, ScriptableObject data, Inspector fields for every tunable value (prices, timers, chances, fines).
- Authoring tools are additive and idempotent (`Coop Prototype > Checkpoint > ...` menu items, plus an editor-mailbox command). Back up a scene before the first destructive pass (`Tools/Checkpoint/Before...`). Preserve `.meta` files and GUIDs.
- Every client request is validated on the server (sender, reach/obstruction, current visit/phase, rate limit).
- Log meaningful state changes with a `[Checkpoint]` / `[Coop]` prefix, not per-frame activity.

## 3. Debug panel (required)

Every feature is **evaluated for the F1 debug panel** and, if it has anything worth reaching quickly, added to it. Ask:

1. Can a tester reach the feature's state in one click instead of playing up to it? (call a specific visitor, jump the clock, grant money, unlock an upgrade)
2. Is there hidden state the tester needs to see? (the current flaw, contraband, guilt, timers) → add it to the status block.
3. Is there a time gate or random roll? → add a button that skips it or forces each outcome.
4. Does it spend or earn resources? → make it free or grantable.
5. Does it have a cleanup / reset path? → add it (release all, send away, reset credits).

How to add an entry:

- **Server action:** add a `case "yourcommand":` to `DebugTools.Execute` (`Assets/CoopPrototype/Scripts/Debugging/DebugTools.cs`). Call the same public APIs gameplay uses, and put any new debug-only hook on the owning component as a `Debug...` method (see `CheckpointSession.DebugFinishArrival`, `WorkShift.DebugAddMinutes`, `HoldingCell.ReleaseAll`). Return a short human-readable result; it is shown to the caller and logged as `[Debug]`.
- **Button:** add it to the section of `DebugMenu` that fits (Visitors, Decide, Day / Police, Money, Player) or add a new section. Use `tools.Run("yourcommand", arg, option)`. Local-only views (no server change) can be drawn directly in the menu.
- **Status:** if the feature has hidden or timing state, add a line to `DebugMenu.Status()`.
- **Gate:** never bypass `DebugTools.Allowed` (Editor or Development build only); the server rechecks it for every request.
- **Record it** in the table below, and mention it in the feature's section of `ALIEN_CHECKPOINT.md`.

If a feature truly has nothing to debug (e.g. pure art), write "no debug entry: reason" in its log entry instead of skipping silently.

## 4. Verifying

- Compile with no Console errors; run the feature's validation command.
- Play through MainMenu → host → GameWorld. Use the debug panel to reach each state; screenshot UI with the mailbox `cpscreen` command.
- Multiplayer Play Mode with two peers for networked changes, and a late join when persistent state changed (see `INIT.md`). Report what was actually run separately from what was not.
- Update `ALIEN_CHECKPOINT.md` (Hebrew working log), `README.md` / `INIT.md` for controls or setup changes, and the table below.

## Debug panel registry

| Feature | Section | Buttons / status | Command(s) |
|---|---|---|---|
| Station art / live terminal readout | Existing Visitors, Money and Navigation | No new server action: static art; readout uses existing phase/day/scan state. Existing arrival/papers/shop controls exercise it | Read-only editor probe `cpstationart` |
| Document holo-pad presentation | Existing Visitors / Money | No new server action; arrival/papers, forced Forged flaw and spawn UV lamp cover the existing item behavior | Saved document prefab, holo meshes/materials/shader; no retained rebuild command |
| Visitors & vehicles | Visitors | Random / Shuttle / Truck / With companions / Carrying contraband, every specific traveller, forced flaw selector | `visitor`, `visitorkind`, `visitorcompanions`, `visitorcontraband`, `visitorcase` (option = flaw) |
| Modular vehicle / alien presentation | Visitors | Existing vehicle/companion/arrival buttons; local seated occupant gaze target status (5 m) | Local status; editor probe `cpvisitorart` |
| Visit flow | Visitors | Stop vehicle now, Papers into slots, Scan all papers, Send visitor away, flaw chance 0/40/100% | `arrive`, `papers`, `scanall`, `sendaway`, `flawchance` |
| Document flaws & cargo truth | Status | Current flaw, contraband crates, VALID/INVALID | (local) |
| Decisions | Decide | Approve, Reject, Arrest as the caller | `approve`, `reject`, `arrest` |
| Holding cell & police | Decide | Police visit now (all prisoners), Release all | `police`, `releaseall` |
| Work day / clock | Day / Police | +1 hour, jump to 3:55 PM, close day (payout), start next day | `hour`, `endsoon`, `closeday`, `nextday` |
| Wallets | Money | ±credits, +today's pay, everyone +100, reset | `credits`, `pending`, `creditsall`, `resetcredits` |
| Shop items & upgrades | Money | Spawn each shop item free, unlock X-ray belt slot 2, toggle coffee speed | `spawnitem`, `upgradebelt`, `coffee` |
| Running (Shift) | Player | Running now / run speed status, Toggle always run (runs whenever moving forward) | `alwaysrun` |
| Suit colours (unique per game) | Player | Next free suit colour (same as C) | `colour` |
| Navigation | Player | Teleport to terminal, paper shelf, X-ray, ATM, cell tablet, cell view, spawn (editable points in GameWorld) | `teleport` |

Editor-mailbox equivalents for automated tests: `cpdebug` (target = command, player = arg, position.x = option), `cpdebugreply`, `cpdebugmenu` (player = section).
