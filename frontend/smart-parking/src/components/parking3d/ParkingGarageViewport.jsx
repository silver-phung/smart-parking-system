import { useEffect, useRef } from "react";
import { createGarageScene } from "./garageScene.js";

export default function ParkingGarageViewport({
  floorLabel,
  slots,
  filter,
  cameraMode,
  cameraRevision,
  layers,
  onSelect,
  onReady,
}) {
  const hostRef = useRef(null);
  const canvasRef = useRef(null);
  const sceneRef = useRef(null);
  const onSelectRef = useRef(onSelect);
  onSelectRef.current = onSelect;

  useEffect(() => {
    const canvas = canvasRef.current;
    const host = hostRef.current;
    if (!canvas || !host) return undefined;

    let scene;
    try {
      scene = createGarageScene({
        canvas,
        floorLabel,
        slots,
        onSelect: (slot) => onSelectRef.current?.(slot),
      });
      sceneRef.current = scene;
      onReady?.(true);
    } catch (error) {
      console.error("Không thể khởi tạo bãi đỗ 3D", error);
      onReady?.(false, error);
      return undefined;
    }

    const resize = () => {
      const bounds = host.getBoundingClientRect();
      scene.resize(Math.max(bounds.width, 1), Math.max(bounds.height, 1));
    };
    resize();
    const observer = new ResizeObserver(resize);
    observer.observe(host);

    return () => {
      observer.disconnect();
      scene.destroy();
      sceneRef.current = null;
    };
  }, [floorLabel]);

  useEffect(() => {
    sceneRef.current?.updateSlots(slots, filter);
  }, [slots, filter]);

  useEffect(() => {
    sceneRef.current?.setCamera(cameraMode);
  }, [cameraMode, cameraRevision]);

  useEffect(() => {
    Object.entries(layers).forEach(([name, visible]) => sceneRef.current?.setLayer(name, visible));
  }, [layers]);

  return (
    <div className="p3d-viewport" ref={hostRef}>
      <canvas ref={canvasRef} aria-label={`Mô hình 3D ${floorLabel}`} />
      <div className="p3d-viewport__hint">
        <span>Chuột trái: xoay</span>
        <span>Chuột phải: di chuyển</span>
        <span>Cuộn: thu phóng</span>
      </div>
    </div>
  );
}
