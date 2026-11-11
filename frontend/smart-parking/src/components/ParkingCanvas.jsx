import { useEffect, useRef, useState } from "react";
import "../../../assets/css/parking-3d-render.css";

let rendererCode;
function loadRenderer() {
  rendererCode ||= import("three").then(THREE => { window.THREE = THREE; })
    .then(() => import("../../../assets/js/parking-3d-render-kit.js"))
    .then(() => import("../../../assets/js/parking-3d-render-floor.js"))
    .then(() => import("../../../assets/js/parking-3d-render.js"));
  return rendererCode;
}

export default function ParkingCanvas({ definition, floorId, role, onSelect, onFloor, viewerRef, busy = false }) {
  const host = useRef(null);
  const selected = useRef(onSelect); selected.current = onSelect;
  const changeFloor = useRef(onFloor); changeFloor.current = onFloor;
  const currentFloor = useRef(floorId); currentFloor.current = floorId;
  const [error, setError] = useState("");
  const key = JSON.stringify(definition);
  useEffect(() => {
    if (!definition) return undefined;
    let active = true, viewer;
    setError("");
    loadRenderer().then(() => {
      if (!active) return;
      let ready = false;
      viewer = new window.ParkingRender({ container: host.current, definitions: [definition], lotId: definition.id,
        role: role === "USER" ? "user" : role ? "admin" : "guest",
        onSlotSelect: slot => selected.current(slot),
        onChange: state => { const index = state.floorIdx; if (ready && definition.liveFloors[index]) changeFloor.current(String(definition.liveFloors[index].id)); } });
      viewerRef.current = viewer;
      const index = definition.liveFloors.findIndex(f => String(f.id) === String(currentFloor.current));
      viewer.setMode("orbit"); viewer.setFloor(index >= 0 ? index : 0);
      viewer.tr = null;
      ready = true;
    }).catch(e => { if (active) { viewer?.destroy(); setError("Không mở được đồ họa 3D. Bạn vẫn có thể xem và thao tác bằng danh sách ô bên cạnh."); } });
    return () => { active = false; viewer?.destroy(); if (viewerRef.current === viewer) viewerRef.current = null; };
  }, [key, role]);
  useEffect(() => {
    const index = definition?.liveFloors.findIndex(f => String(f.id) === String(floorId));
    if (index >= 0) viewerRef.current?.setFloor(index);
  }, [floorId, key]);
  return <div className="parking-canvas-wrap" inert={busy ? "" : undefined}><div ref={host} className="parking-canvas" aria-label="Mô hình bãi đỗ 3D" />
    {error && <p className="parking-canvas-error" role="alert">{error}</p>}</div>;
}
