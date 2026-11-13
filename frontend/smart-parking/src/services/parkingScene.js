export const sceneStatuses = { EMPTY: "Trống", OCCUPIED: "Có xe", RESERVED: "Giữ chỗ", MAINTENANCE: "Bảo trì", UNAVAILABLE: "Tạm ngừng" };

export function sceneToDefinition(scene) {
  if (!scene || !Array.isArray(scene.floors) || !scene.floors.length) return null;
  const under = scene.floors.every(f => f.floorNumber < 0);
  const floors = [...scene.floors].sort((a, b) => (under ? b.floorNumber - a.floorNumber : a.floorNumber - b.floorNumber) || a.id - b.id);
  return {
    id: String(scene.id), name: scene.name, type: under ? "underground" : "multistorey",
    ids: floors.map(f => f.name),
    counts: floors.map(f => [0, 1, 2, 3].map(i => Math.floor(f.slots.length / 4) + (i < f.slots.length % 4 ? 1 : 0))),
    liveFloors: floors.map(f => ({ ...f,
      zones: f.zones || (f.zone ? [f.zone] : []),
      zoneName: (f.zones || (f.zone ? [f.zone] : [])).map(z => z.name).join(", ") || "Chưa có khu vực",
      kind: (f.zones || [f.zone]).some(z => z?.type === "FIXED") ? "MONTHLY" : (f.zones || [f.zone]).some(z => z?.type === "TRANSIENT") ? "VISITOR" : "UNASSIGNED",
      slots: f.slots.map(s => ({ ...s, floorId: f.id, zoneName: s.zoneName || (f.zones || []).find(z => z.id === s.zoneId)?.name || f.zone?.name || "Chưa có khu vực" })) })),
  };
}
