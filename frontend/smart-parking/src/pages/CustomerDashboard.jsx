import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import RequireCustomer from "../components/RequireCustomer.jsx";
import { apiRequest } from "../services/api.js";
import { collectPages } from "../services/staffApi.js";
import "../workspace.css";

function readData(response) {
  return response?.data;
}

function readItems(response) {
  const data = readData(response);
  return Array.isArray(data) ? data : data?.items || [];
}

const slotLabels = {
  EMPTY: "Còn trống",
  OCCUPIED: "Đang sử dụng",
  MAINTENANCE: "Đang bảo trì",
  UNAVAILABLE: "Không khả dụng",
};

function DashboardContent({ publicView = false }) {
  const [revision, setRevision] = useState(0);
  const [lots, setLots] = useState([]);
  const [selectedLotId, setSelectedLotId] = useState("");
  const [floors, setFloors] = useState([]);
  const [selectedFloorId, setSelectedFloorId] = useState("");
  const [slots, setSlots] = useState([]);
  const [lotsLoading, setLotsLoading] = useState(true);
  const [floorsLoading, setFloorsLoading] = useState(false);
  const [slotsLoading, setSlotsLoading] = useState(false);
  const [lotsError, setLotsError] = useState("");
  const [floorsError, setFloorsError] = useState("");
  const [slotsError, setSlotsError] = useState("");

  const loadLots = useCallback(async () => {
    setLotsLoading(true);
    setLotsError("");
    try {
      const items = await collectPages(
        async (page) =>
          (
            await apiRequest(
              `/v1/parking-lots?pageNumber=${page}&pageSize=100&isActive=true`,
              { skipAuth: true },
            )
          ).data,
      );
      setLots(items);
      setSelectedLotId((current) =>
        items.some((lot) => String(lot.id) === current)
          ? current
          : String(items[0]?.id || ""),
      );
    } catch (error) {
      setLotsError(error.message);
    } finally {
      setLotsLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadLots();
  }, [loadLots]);

  useEffect(() => {
    if (!selectedLotId) {
      setFloors([]);
      setSelectedFloorId("");
      setSlots([]);
      return;
    }

    let active = true;
    const loadFloors = async () => {
      setFloorsLoading(true);
      setFloorsError("");
      setFloors([]);
      setSlots([]);
      setSelectedFloorId("");
      try {
        const response = await apiRequest(
          `/v1/parking-lots/${selectedLotId}/floors?isActiveOnly=true`,
          { skipAuth: true },
        );
        if (!active) return;
        const items = readItems(response);
        setFloors(items);
        setSelectedFloorId(String(items[0]?.id || ""));
      } catch (error) {
        if (active) setFloorsError(error.message);
      } finally {
        if (active) setFloorsLoading(false);
      }
    };

    void loadFloors();
    return () => {
      active = false;
    };
  }, [selectedLotId, revision]);

  useEffect(() => {
    if (!selectedFloorId) {
      setSlots([]);
      return;
    }

    let active = true;
    const loadSlots = async () => {
      setSlotsLoading(true);
      setSlotsError("");
      try {
        const items = await collectPages(
          async (page) =>
            (
              await apiRequest(
                `/v1/parking-floors/${selectedFloorId}/slots?pageNumber=${page}&pageSize=100`,
                { skipAuth: true },
              )
            ).data,
        );
        if (active) setSlots(items);
      } catch (error) {
        if (active) setSlotsError(error.message);
      } finally {
        if (active) setSlotsLoading(false);
      }
    };

    void loadSlots();
    return () => {
      active = false;
    };
  }, [selectedFloorId, revision]);

  const availableCount = slots.filter(
    (slot) => slot.physicalStatus === "EMPTY" && slot.isAvailable,
  ).length;
  const maintenanceCount = slots.filter(
    (slot) => slot.physicalStatus === "MAINTENANCE",
  ).length;
  const occupiedCount = slots.filter(
    (slot) => slot.physicalStatus === "OCCUPIED",
  ).length;
  const selectedLot = lots.find((lot) => String(lot.id) === selectedLotId);
  const selectedFloor = floors.find(
    (floor) => String(floor.id) === selectedFloorId,
  );

  return (
    <section className="customer-page container">
      <div className="customer-page__heading">
        <div>
          <span className="tag">
            {publicView ? "Bãi đỗ công khai" : "Khu vực khách hàng"}
          </span>
          <h1>Bãi đỗ xe</h1>
          <p className="muted">
            Xem sức chứa và trạng thái từng vị trí từ dữ liệu hệ thống.
          </p>
        </div>
        <Link
          to={publicView ? "/login" : "/customer/sessions"}
          className="btn btn--primary"
        >
          {publicView
            ? "Đăng nhập để xem phiên của tôi"
            : "Phiên gửi xe của tôi"}
        </Link>
      </div>

      <div className="customer-card">
        <div className="customer-card__heading">
          <div>
            <h2>Chọn bãi và tầng</h2>
            <p className="muted">
              Danh sách chỉ bao gồm các bãi đang hoạt động.
            </p>
          </div>
          <button
            type="button"
            className="btn btn--outline"
            onClick={() => {
              void loadLots();
              setRevision((n) => n + 1);
            }}
            disabled={lotsLoading || floorsLoading || slotsLoading}
          >
            {lotsLoading ? "Đang tải..." : "Làm mới"}
          </button>
        </div>

        {lotsError && (
          <p className="customer-message customer-message--error" role="alert">
            {lotsError}
          </p>
        )}

        {!lotsLoading && !lotsError && lots.length === 0 && (
          <p className="customer-message">
            Hiện chưa có bãi đỗ nào đang hoạt động.
          </p>
        )}

        {lots.length > 0 && (
          <div className="customer-select-grid">
            <label>
              Bãi đỗ xe
              <select
                value={selectedLotId}
                onChange={(event) => setSelectedLotId(event.target.value)}
              >
                {lots.map((lot) => (
                  <option value={lot.id} key={lot.id}>
                    {lot.name}
                    {lot.address ? ` · ${lot.address}` : ""}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Tầng
              <select
                value={selectedFloorId}
                onChange={(event) => setSelectedFloorId(event.target.value)}
                disabled={floorsLoading || floors.length === 0}
              >
                {floors.map((floor) => (
                  <option value={floor.id} key={floor.id}>
                    {floor.name}
                  </option>
                ))}
              </select>
            </label>
          </div>
        )}

        {floorsLoading && (
          <p className="customer-message">Đang tải danh sách tầng...</p>
        )}
        {floorsError && (
          <p className="customer-message customer-message--error" role="alert">
            {floorsError}
          </p>
        )}
        {!floorsLoading &&
          !floorsError &&
          selectedLotId &&
          floors.length === 0 && (
            <p className="customer-message">
              Bãi này chưa có tầng đang hoạt động.
            </p>
          )}
      </div>

      {selectedLot && selectedFloor && (
        <div className="customer-card">
          <div className="customer-card__heading">
            <div>
              <h2>
                {selectedLot.name} · {selectedFloor.name}
              </h2>
              <p className="muted">
                {selectedLot.address || "Chưa có địa chỉ bãi đỗ"}
              </p>
            </div>
          </div>

          <div className="customer-lot-summary">
            <div>
              <strong>{selectedLot.availableSlotsCount ?? "—"}</strong>
              <span>Chỗ trống toàn bãi</span>
            </div>
            <div>
              <strong>{selectedLot.totalCapacity ?? "—"}</strong>
              <span>Tổng sức chứa</span>
            </div>
          </div>

          {slotsError && (
            <p
              className="customer-message customer-message--error"
              role="alert"
            >
              {slotsError}
            </p>
          )}
          {slotsLoading ? (
            <p className="customer-message">Đang tải tình trạng vị trí...</p>
          ) : !slotsError ? (
            <>
              <div className="customer-stat-grid" aria-label="Tổng quan tầng">
                <div>
                  <strong>{availableCount}</strong>
                  <span>Chỗ trống</span>
                </div>
                <div>
                  <strong>{occupiedCount}</strong>
                  <span>Đang sử dụng</span>
                </div>
                <div>
                  <strong>{maintenanceCount}</strong>
                  <span>Đang bảo trì</span>
                </div>
                <div>
                  <strong>{slots.length}</strong>
                  <span>Tổng vị trí</span>
                </div>
              </div>

              {slots.length === 0 ? (
                <p className="customer-message">
                  Tầng này chưa có dữ liệu vị trí.
                </p>
              ) : (
                <ul
                  className="customer-slot-grid"
                  aria-label="Danh sách vị trí đỗ xe"
                >
                  {slots.map((slot) => {
                    const effectiveStatus =
                      slot.physicalStatus === "EMPTY" && !slot.isAvailable
                        ? "UNAVAILABLE"
                        : slot.physicalStatus;
                    const status =
                      slotLabels[effectiveStatus] || effectiveStatus;
                    return (
                      <li
                        key={slot.id}
                        className={`customer-slot customer-slot--${String(effectiveStatus).toLowerCase()}`}
                      >
                        <strong>{slot.slotCode}</strong>
                        <span>{status}</span>
                      </li>
                    );
                  })}
                </ul>
              )}
            </>
          ) : null}
        </div>
      )}

      <div className="customer-message customer-message--notice">
        <strong>Tình trạng bãi tại thời điểm cập nhật</strong>
        <span>
          Chỗ trống hiển thị chưa được giữ cho bạn. Tình trạng có thể thay đổi
          khi xe vào hoặc ra bãi.
        </span>
      </div>
    </section>
  );
}

export default function CustomerDashboard({ publicView = false }) {
  if (publicView) return <DashboardContent publicView />;
  return (
    <RequireCustomer>
      <DashboardContent />
    </RequireCustomer>
  );
}
