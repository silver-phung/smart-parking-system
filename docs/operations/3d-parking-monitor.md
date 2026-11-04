# 3D Parking Monitor

The React application exposes the underground parking monitor at `/app/parking/3d`.

## Current layout

- Procedural cutaway garage preserving the original Three.js visual language while loading a floor-specific traffic layout.
- B1 is reserved for monthly parking. Its check-in booth sits inside the basement after the ground ramp; transient vehicles use a physically separated bypass lane and continue directly down to B2.
- B2 and lower levels are transient-parking floors. B2 contains the transient checkout before the up ramp; no card scan is repeated on deeper floors.
- One-way up/down ramps are grouped at the end of the driving route. The B1 exit combines the monthly flow and the flow arriving from B2 into two marked lanes before the shared ramp to ground level.
- Direction boards at each arrival point identify monthly parking, transient parking and the next ramp.
- Concrete structure, collision markings, wheel stops, lane arrows and zone signs.
- Elevator lobby, emergency exit, stairs, fire cabinets and protective bollards.
- Ventilation ducts, sprinkler pipes, ceiling lights and infrastructure layer controls.
- Overview, plan, driving-lane, entrance and elevator camera presets.
- Selectable spaces with status, zone, floor, sensor and source identifier details.

No mesh or texture from the linked commercial reference products is copied into the repository. The scene is generated from original Three.js geometry so a licensed GLB/GLTF asset can replace an individual module later.

## Demonstration profiles

| Parking lot | Floors | Slot distribution | Total |
| --- | --- | --- | ---: |
| Hầm ngầm FPT Q9 | B1–B2 | 32 + 32 | 64 |
| Vincom Landmark 81 | B1–B3 | 28 + 27 + 27 | 82 |
| Aeon Mall Mega Hub | B1–B4 | 28 + 28 + 28 + 28 | 112 |

B1 is the monthly floor in every profile; B2 and lower floors are transient floors. The slot distributions are deterministic so the UI, automated tests and operational walkthrough always agree with the approved totals.

## Data behavior

The monitor reads active lots, floors and spaces from the existing public parking APIs. Space state is refreshed every 30 seconds. If the backend is unavailable or a floor contains no spaces, the page explicitly enters simulation mode and never writes mock state to the API.

The current scene maps up to 48 API spaces from the selected floor into a four-zone procedural layout. The bundled demonstrations use their exact configured per-floor counts. Before production use, replace the procedural coordinates with the approved engineering floor plan so geometry and database slots have a one-to-one physical mapping.

## Development

```bash
npm --prefix frontend/smart-parking install
npm --prefix frontend/smart-parking run dev
```

Open `http://localhost:5173/app/parking/3d`.

Production assets are built into `backend/src/SmartParking.Api/wwwroot/app`:

```bash
npm --prefix frontend/smart-parking run build
```
