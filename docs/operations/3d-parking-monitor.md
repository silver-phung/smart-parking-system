# 3D Parking Monitor

The React application exposes the underground parking monitor at `/app/parking/3d`.

## Current layout

- Procedural cutaway garage with 48 car spaces in four zones.
- Separate entrance and exit lanes, security booth and boom barriers.
- Concrete structure, collision markings, wheel stops, lane arrows and zone signs.
- Elevator lobby, emergency exit, stairs, fire cabinets and protective bollards.
- Ventilation ducts, sprinkler pipes, ceiling lights and infrastructure layer controls.
- Overview, plan, driving-lane, entrance and elevator camera presets.
- Selectable spaces with status, zone, floor, sensor and source identifier details.

No mesh or texture from the linked commercial reference products is copied into the repository. The scene is generated from original Three.js geometry so a licensed GLB/GLTF asset can replace an individual module later.

## Data behavior

The monitor reads active lots, floors and spaces from the existing public parking APIs. Space state is refreshed every 30 seconds. If the backend is unavailable or a floor contains no spaces, the page explicitly enters simulation mode and never writes mock state to the API.

The current scene maps the first 48 spaces of the selected floor to the four-zone reference layout. Before production use, update the layout from the approved floor plan so the geometry and the database have a one-to-one slot mapping.

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
