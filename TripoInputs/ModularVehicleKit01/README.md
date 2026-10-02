# Modular Vehicle Kit 01

Ten isolated reference images for converting vehicle components to individual Tripo models. They are split into two compatible families: private vehicles and goods transport.

## Parts

### Private vehicle

| File | Part | Attach to |
| --- | --- | --- |
| `01-private-chassis-core.png` | Bare compact chassis | Cockpit, rear module, four hover pods |
| `02-private-bubble-cockpit.png` | Bubble cockpit | Front / top of private chassis |
| `03-private-hover-pod.png` | Standard round hover pod | Four underside hardpoints |
| `04-private-passenger-cabin.png` | Rear two-seat cabin | Rear hardpoint |
| `05-private-utility-roof.png` | Rack, sensor and luggage roof module | Roof hardpoint |

### Goods transport

| File | Part | Attach to |
| --- | --- | --- |
| `06-cargo-heavy-chassis-core.png` | Long heavy chassis | Cab, cargo module and six hover pods |
| `07-cargo-forward-cab.png` | Freight-driver cab | Front chassis hardpoint |
| `08-cargo-flatbed-module.png` | Empty flatbed | Rear deck hardpoint |
| `09-cargo-enclosed-box.png` | Enclosed cargo box | Rear deck hardpoint |
| `10-cargo-heavy-hover-pod.png` | Heavy hover pod | Six underside hardpoints |

## Assembly contract

All vehicle parts use the same visible connection language: **graphite rectangular docking plate, four orange corner locking collars, and a cyan circular power socket.** Keep the docking plates unmerged when you clean the Tripo models, so each attachment can snap to its matching chassis hardpoint.

## Suggested combinations

- **Personal pod:** private chassis + bubble cockpit + four standard hover pods
- **Family shuttle:** private chassis + bubble cockpit + passenger cabin + roof module + four standard hover pods
- **Cargo flatbed:** heavy chassis + freight cab + flatbed + six heavy hover pods
- **Cargo hauler:** heavy chassis + freight cab + enclosed box + six heavy hover pods

## Unity use

1. Import each Tripo result as a separate prefab.
2. Place named empty transforms at the center of every docking plate: `Mount_Front`, `Mount_Rear`, `Mount_Roof`, and `Mount_Pod_01` onward.
3. Parent the selected modules to their matching mount transforms; align docking plate faces, then set local position and rotation to zero.
4. Make a prefab variant for each vehicle combination. Keep materials shared across the kit so color variations remain cheap.

The reference images are on white studio backgrounds and use one shared world style: warm ivory, mint panels, orange safety armor, graphite mechanics, cyan energy lights, rounded construction and restrained wear.
