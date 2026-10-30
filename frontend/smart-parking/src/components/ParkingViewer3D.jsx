import { useEffect, useRef } from "react";
import { getAuthSession } from "../services/authSession.js";

const SCRIPT_SRC = "/assets/js/parking-3d-viewer.js";
const CSS_HREF = "/assets/css/parking-3d-viewer.css";

let loadPromise;

function loadViewerEngine() {
  if (window.Parking3DViewer) return Promise.resolve(window.Parking3DViewer);
  if (loadPromise) return loadPromise;

  if (!document.querySelector(`link[href="${CSS_HREF}"]`)) {
    const link = document.createElement("link");
    link.rel = "stylesheet";
    link.href = CSS_HREF;
    document.head.appendChild(link);
  }

  loadPromise = new Promise((resolve, reject) => {
    const script = document.createElement("script");
    script.src = SCRIPT_SRC;
    script.onload = () => resolve(window.Parking3DViewer);
    script.onerror = () => {
      loadPromise = undefined;
      reject(new Error("Không tải được bộ máy 3D."));
    };
    document.head.appendChild(script);
  });
  return loadPromise;
}

export default function ParkingViewer3D({ lot, onSlotClick, onError }) {
  const containerRef = useRef(null);
  const callbacks = useRef({ onSlotClick, onError });
  callbacks.current = { onSlotClick, onError };

  useEffect(() => {
    let viewer;
    let cancelled = false;

    loadViewerEngine()
      .then((Viewer) => {
        if (cancelled || !containerRef.current) return;
        viewer = new Viewer({
          container: containerRef.current,
          lotId: lot.lotId,
          lotName: lot.lotName,
          floorId: lot.floors[0]?.floorId,
          floors: lot.floors,
          getHeaders: () => {
            const token = getAuthSession()?.accessToken;
            return token ? { Authorization: ["Bearer", token].join(" ") } : {};
          },
          onSlotClick: (slot) => callbacks.current.onSlotClick?.(slot),
          onError: (err) => callbacks.current.onError?.(err),
        });
      })
      .catch((err) => callbacks.current.onError?.(err));

    return () => {
      cancelled = true;
      viewer?.destroy();
    };
  }, [lot]);

  return <div ref={containerRef} className="viewer3d__canvas" />;
}
