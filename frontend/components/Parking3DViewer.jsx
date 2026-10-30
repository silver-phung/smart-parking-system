import React, { useEffect, useRef } from 'react';
import '../assets/css/parking-3d-viewer.css';
import Parking3DViewerCore from '../assets/js/parking-3d-viewer.js';

/**
 * Component React: Parking3DViewer
 * Dành riêng cho Song nhúng vào các màn hình Web React (Home, Dashboard, Booking, v.v.)
 *
 * Props:
 *   - lotId: number (mặc định 1)
 *   - lotName: string (ví dụ "Bãi xe FPT Software")
 *   - floorId: string ("B1", "B2", "G", "T1")
 *   - apiEndpoint: endpoint/template đọc snapshot slot đã lưu
 *   - lots/floors: metadata thật do màn hình cha nhận từ API
 *   - showInternalDetails: chỉ bật ở màn hình nội bộ có quyền
 *   - onSlotClick: function(slotData) -> gọi khi người dùng click chọn 1 ô đỗ
 *   - height: string (mặc định "550px")
 *   - className: string
 */
export default function Parking3DViewer({
  lotId = 1,
  lotName = 'Bãi đỗ xe',
  floorId = 'B1',
  apiEndpoint = '/api/v1/parking-floors/{floorId}/slots',
  lots = null,
  floors = null,
  showInternalDetails = false,
  onDataStateChange,
  onSlotClick,
  height = '560px',
  className = ''
}) {
  const containerRef = useRef(null);
  const viewerInstanceRef = useRef(null);

  useEffect(() => {
    if (!containerRef.current) return;

    // Khởi tạo instance 3D Viewer
    const viewer = new Parking3DViewerCore({
      container: containerRef.current,
      lotId,
      lotName,
      floorId,
      apiEndpoint,
      lots,
      floors,
      showInternalDetails,
      onDataStateChange,
      onSlotClick: (slot) => {
        if (onSlotClick) onSlotClick(slot);
      }
    });

    viewerInstanceRef.current = viewer;

    // Dọn dẹp bộ nhớ Three.js khi component unmount
    return () => {
      if (viewerInstanceRef.current) {
        viewerInstanceRef.current.destroy();
        viewerInstanceRef.current = null;
      }
    };
  }, [lotId, lotName, apiEndpoint, lots, floors, showInternalDetails, onDataStateChange]);

  // Cập nhật khi floorId thay đổi
  useEffect(() => {
    if (viewerInstanceRef.current && floorId) {
      viewerInstanceRef.current.setFloor(floorId);
    }
  }, [floorId]);

  return (
    <div
      ref={containerRef}
      className={`parking-3d-viewer-root ${className}`}
      style={{ width: '100%', height, position: 'relative' }}
    />
  );
}
