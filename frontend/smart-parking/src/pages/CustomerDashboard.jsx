import { useCallback, useEffect, useRef, useState, useSyncExternalStore } from "react";
import { Link } from "react-router-dom";
import RequireCustomer from "../components/RequireCustomer.jsx";
import ParkingSlotManagement from "./ParkingSlotManagement.jsx";
import { getAuthSession, subscribeAuthSession } from "../services/authSession.js";
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

function localDateTimeValue(date = new Date()) {
  return new Date(date.getTime() - date.getTimezoneOffset() * 60000)
    .toISOString()
    .slice(0, 16);
}

const slotLabels = {
  EMPTY: "Còn trống",
  OCCUPIED: "Đang sử dụng",
  MAINTENANCE: "Đang bảo trì",
  UNAVAILABLE: "Không khả dụng",
  RESERVED: "Đang giữ chỗ",
};

const bookingStatusLabels = {
  PENDING: "Chờ xe đến",
  CHECKED_IN: "Đã nhận xe",
  CANCELLED: "Đã hủy",
  EXPIRED: "Đã hết hạn",
};

function DashboardContent({ publicView = false }) {
  const floorLotIdRef = useRef("");
  const [revision, setRevision] = useState(0);
  const [lots, setLots] = useState([]);
  const [selectedLotId, setSelectedLotId] = useState("");
  const [floors, setFloors] = useState([]);
  const [selectedFloorId, setSelectedFloorId] = useState("");
  const [slots, setSlots] = useState([]);
  const [slotsUpdatedAt, setSlotsUpdatedAt] = useState(null);
  const [slotRevision, setSlotRevision] = useState(0);
  const [lotsLoading, setLotsLoading] = useState(true);
  const [floorsLoading, setFloorsLoading] = useState(false);
  const [slotsLoading, setSlotsLoading] = useState(false);
  const [lotsError, setLotsError] = useState("");
  const [floorsError, setFloorsError] = useState("");
  const [slotsError, setSlotsError] = useState("");
  const [capacity, setCapacity] = useState(null);
  const [capacityLoading, setCapacityLoading] = useState(false);
  const [capacityError, setCapacityError] = useState("");
  const [capacityUpdatedAt, setCapacityUpdatedAt] = useState(null);
  const [myBookings, setMyBookings] = useState([]);
  const [myBookingsLoading, setMyBookingsLoading] = useState(false);
  const [myBookingsError, setMyBookingsError] = useState("");
  const [cancellingBookingId, setCancellingBookingId] = useState(null);
  const [bookingCancelError, setBookingCancelError] = useState("");
  const [bookingOpen, setBookingOpen] = useState(false);
  const [bookingProfile, setBookingProfile] = useState(null);
  const [bookingVehicles, setBookingVehicles] = useState([]);
  const [selectedVehicleId, setSelectedVehicleId] = useState("");
  const [bookingDetailsLoading, setBookingDetailsLoading] = useState(false);
  const [bookingDetailsError, setBookingDetailsError] = useState("");
  const [bookingDetailsRevision, setBookingDetailsRevision] = useState(0);
  const [vehiclePickerOpen, setVehiclePickerOpen] = useState(false);
  const [bookingPlateNumber, setBookingPlateNumber] = useState("");
  const [bookingStartTime, setBookingStartTime] = useState("");
  const [bookingNotes, setBookingNotes] = useState("");
  const [bookingSaving, setBookingSaving] = useState(false);
  const [bookingError, setBookingError] = useState("");
  const [bookingMessage, setBookingMessage] = useState("");

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
    const lotChanged = floorLotIdRef.current !== selectedLotId;
    floorLotIdRef.current = selectedLotId;
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
      if (lotChanged) {
        setFloors([]);
        setSlots([]);
        setSelectedFloorId("");
      }
      try {
        const response = await apiRequest(
          `/v1/parking-lots/${selectedLotId}/floors?isActiveOnly=true`,
          { skipAuth: true },
        );
        if (!active) return;
        const items = readItems(response);
        setFloors(items);
        setSelectedFloorId((current) =>
          items.some((floor) => String(floor.id) === current)
            ? current
            : String(items[0]?.id || ""),
        );
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
    const timerId = window.setInterval(
      () => setSlotRevision((value) => value + 1),
      15_000,
    );
    return () => window.clearInterval(timerId);
  }, []);

  useEffect(() => {
    if (!selectedLotId) {
      setCapacity(null);
      setCapacityError("");
      setCapacityLoading(false);
      return undefined;
    }

    let active = true;
    let timeoutId;
    const controller = new AbortController();
    const loadCapacity = async () => {
      try {
        const response = await apiRequest(
          `/v1/parking-lots/${selectedLotId}/capacity`,
          { skipAuth: true, signal: controller.signal },
        );
        if (!active) return;
        const result = response?.data;
        if (!result || result.lotId !== Number(selectedLotId))
          throw new Error("Dữ liệu sức chứa bãi không hợp lệ.");
        setCapacity(result);
        setCapacityError("");
        setCapacityUpdatedAt(new Date());
      } catch (error) {
        if (active && !controller.signal.aborted)
          setCapacityError(error.message);
      } finally {
        if (active) {
          setCapacityLoading(false);
          timeoutId = window.setTimeout(loadCapacity, 15_000);
        }
      }
    };

    setCapacityLoading(capacity?.lotId !== Number(selectedLotId));
    void loadCapacity();
    return () => {
      active = false;
      controller.abort();
      window.clearTimeout(timeoutId);
    };
  }, [selectedLotId, revision]);

  useEffect(() => {
    if (publicView) return undefined;

    let active = true;
    let timeoutId;
    const controller = new AbortController();
    const loadMyBookings = async () => {
      try {
        const bookings = await collectPages(
          async (page) =>
            (
              await apiRequest(
                `/v1/bookings/my?pageNumber=${page}&pageSize=20`,
                { signal: controller.signal },
              )
            ).data,
        );
        if (active) {
          setMyBookings(bookings);
          setMyBookingsError("");
        }
      } catch (error) {
        if (active && !controller.signal.aborted)
          setMyBookingsError(error.message);
      } finally {
        if (active) {
          setMyBookingsLoading(false);
          timeoutId = window.setTimeout(loadMyBookings, 30_000);
        }
      }
    };

    setMyBookingsLoading(myBookings.length === 0);
    void loadMyBookings();
    return () => {
      active = false;
      controller.abort();
      window.clearTimeout(timeoutId);
    };
  }, [publicView, revision]);

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
        if (active) {
          setSlots(items);
          setSlotsUpdatedAt(new Date());
        }
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
  }, [selectedFloorId, revision, slotRevision]);

  useEffect(() => {
    if (!bookingOpen || publicView) return undefined;

    let active = true;
    const loadBookingDetails = async () => {
      setBookingDetailsLoading(true);
      setBookingDetailsError("");
      try {
        const [profileResponse, vehicles] = await Promise.all([
          apiRequest("/v1/users/me"),
          collectPages(
            async (page) =>
              (
                await apiRequest(
                  `/v1/vehicles?page=${page}&pageSize=100`,
                )
              ).data,
          ),
        ]);
        if (!active) return;

        const availableVehicles = vehicles.filter(
          (vehicle) => vehicle.isActive !== false,
        );
        setBookingProfile(profileResponse?.data || null);
        setBookingVehicles(availableVehicles);
        setSelectedVehicleId(
          availableVehicles.length === 1
            ? String(availableVehicles[0].id)
            : "",
        );
      } catch (error) {
        if (active) setBookingDetailsError(error.message);
      } finally {
        if (active) setBookingDetailsLoading(false);
      }
    };

    void loadBookingDetails();
    return () => {
      active = false;
    };
  }, [bookingOpen, bookingDetailsRevision, publicView]);

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
  const selectedVehicle = bookingVehicles.find(
    (vehicle) => String(vehicle.id) === selectedVehicleId,
  );
  const currentCapacity =
    capacity?.lotId === Number(selectedLotId) ? capacity : null;
  const vehicleName = (vehicle) =>
    [vehicle.brand, vehicle.model].filter(Boolean).join(" ") ||
    `Xe cá nhân`;

  const openBookingForm = () => {
    setBookingError("");
    setBookingMessage("");
    setBookingProfile(null);
    setBookingVehicles([]);
    setSelectedVehicleId("");
    setBookingDetailsError("");
    setVehiclePickerOpen(false);
    const startTime = new Date();
    setBookingStartTime(localDateTimeValue(startTime));
    setBookingOpen(true);
  };

  const submitBooking = async (event) => {
    event.preventDefault();
    setBookingSaving(true);
    setBookingError("");
    setBookingMessage("");

    try {
      const startTime = new Date(bookingStartTime);
      if (bookingVehicles.length > 0 && !selectedVehicle) {
        throw new Error("Vui lòng chọn xe bạn sẽ sử dụng.");
      }

      const response = await apiRequest("/v1/bookings", {
        method: "POST",
        body: JSON.stringify({
          lotId: Number(selectedLotId),
          ...(selectedVehicle
            ? { vehicleId: selectedVehicle.id }
            : { plateNumber: bookingPlateNumber.trim() }),
          startTime: startTime.toISOString(),
          notes: bookingNotes.trim() || null,
        }),
      });
      setBookingMessage(
        response?.data?.id
          ? `Đặt chỗ tại ${selectedLot?.name || "bãi đỗ"} thành công. Mã đặt chỗ #${response.data.id}.`
          : response?.message ||
              `Đặt chỗ tại ${selectedLot?.name || "bãi đỗ"} thành công.`,
      );
      setBookingOpen(false);
      setBookingPlateNumber("");
      setBookingNotes("");
      setRevision((value) => value + 1);
    } catch (error) {
      setBookingError(error.message);
    } finally {
      setBookingSaving(false);
    }
  };

  const cancelBooking = async (bookingId) => {
    setCancellingBookingId(bookingId);
    setBookingCancelError("");
    try {
      const response = await apiRequest(`/v1/bookings/${bookingId}/cancel`, {
        method: "POST",
        body: JSON.stringify({}),
      });
      if (response?.data?.status !== "CANCELLED") {
        throw new Error(
          response?.message || "Chưa xác nhận được việc hủy chỗ. Vui lòng tải lại.",
        );
      }
      setMyBookings((current) =>
        current.map((booking) =>
          booking.id === bookingId ? response.data : booking,
        ),
      );
      setRevision((value) => value + 1);
    } catch (error) {
      setBookingCancelError(error.message);
    } finally {
      setCancellingBookingId(null);
    }
  };

  return (
    <section className="customer-page container">
      <div className="customer-page__heading">
        <div>
          <span className="tag">
            {publicView ? "Bãi đỗ công khai" : "Khu vực khách hàng"}
          </span>
          <h1>Bãi đỗ xe</h1>
          {selectedLotId && selectedFloorId && <a className="btn btn--outline" href={`/3d-viewer.html?lotId=${encodeURIComponent(selectedLotId)}&floorId=${encodeURIComponent(selectedFloorId)}`}>Xem tầng này bằng 3D</a>}
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
        {slotsUpdatedAt && selectedFloor && (
          <span>
            Trạng thái ô {selectedFloor.name} được làm mới lúc{" "}
            {slotsUpdatedAt.toLocaleTimeString("vi-VN", {
              hour: "2-digit",
              minute: "2-digit",
            })}
            .
          </span>
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
                onChange={(event) => {
                  setSelectedLotId(event.target.value);
                  setBookingOpen(false);
                  setBookingError("");
                  setBookingMessage("");
                }}
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

      {selectedLot && (
        <div className="customer-card">
          <div className="customer-card__heading">
            <div>
              <h2>
                {selectedLot.name}
                {selectedFloor ? ` · ${selectedFloor.name}` : ""}
              </h2>
              <p className="muted">
                {selectedLot.address || "Chưa có địa chỉ bãi đỗ"}
              </p>
            </div>
            {!publicView && (
              <button
                type="button"
                className="btn btn--primary"
                onClick={bookingOpen ? () => setBookingOpen(false) : openBookingForm}
                disabled={bookingSaving}
                aria-expanded={bookingOpen}
                aria-controls="customer-booking-form"
              >
                {bookingOpen ? "Đóng form đặt chỗ" : "Đặt chỗ tại bãi này"}
              </button>
            )}
          </div>

          {bookingMessage && (
            <p className="customer-message customer-message--success" role="status">
              {bookingMessage}
            </p>
          )}

          {bookingOpen && !publicView && (
            <form
              id="customer-booking-form"
              className="customer-booking-form customer-form"
              onSubmit={(event) => void submitBooking(event)}
            >
              <div className="customer-booking-form__heading">
                <h3>Thông tin đặt chỗ</h3>
                <p className="muted">
                  Thông tin liên hệ và xe đã đăng ký sẽ được điền tự động. Chọn
                  thời gian dự kiến để hoàn tất đặt chỗ.
                </p>
              </div>
              {bookingDetailsLoading ? (
                <p className="customer-message">
                  Đang tải hồ sơ và danh sách xe...
                </p>
              ) : bookingDetailsError ? (
                <div
                  className="customer-message customer-message--error"
                  role="alert"
                >
                  <span>{bookingDetailsError}</span>
                  <button
                    type="button"
                    className="text-button"
                    onClick={() =>
                      setBookingDetailsRevision((revision) => revision + 1)
                    }
                  >
                    Tải lại thông tin
                  </button>
                </div>
              ) : (
                <>
                  <div className="customer-booking-profile">
                    <h4>Thông tin khách hàng</h4>
                    <div className="customer-booking-profile__fields">
                      <label>
                        Họ và tên
                        <input
                          value={bookingProfile?.fullName || ""}
                          readOnly
                          placeholder="Chưa cập nhật họ tên"
                        />
                      </label>
                      <label>
                        Email
                        <input
                          value={bookingProfile?.email || ""}
                          readOnly
                          placeholder="Chưa cập nhật email"
                        />
                      </label>
                      <label>
                        Số điện thoại
                        <input
                          value={bookingProfile?.phoneNumber || ""}
                          readOnly
                          placeholder="Chưa cập nhật số điện thoại"
                        />
                      </label>
                    </div>
                  </div>

                  {bookingVehicles.length === 1 && (
                    <div className="customer-booking-vehicle">
                      <span>Xe sử dụng</span>
                      <strong>{vehicleName(bookingVehicles[0])}</strong>
                      <div className="customer-booking-vehicle__details">
                        <span>
                          Biển số <strong>{bookingVehicles[0].plateNumber}</strong>
                        </span>
                        {bookingVehicles[0].color && (
                          <span>
                            Màu xe <strong>{bookingVehicles[0].color}</strong>
                          </span>
                        )}
                      </div>
                    </div>
                  )}

                  {bookingVehicles.length > 1 && (
                    <div className="customer-booking-vehicle-picker">
                      <span className="customer-booking-vehicle-picker__label">
                        Chọn xe bạn sẽ sử dụng
                      </span>
                      <button
                        type="button"
                        className="customer-booking-vehicle-picker__trigger"
                        aria-haspopup="listbox"
                        aria-expanded={vehiclePickerOpen}
                        onClick={() =>
                          setVehiclePickerOpen((isOpen) => !isOpen)
                        }
                      >
                        {selectedVehicle ? (
                          <span>
                            <strong>{vehicleName(selectedVehicle)}</strong>
                            <small>{selectedVehicle.plateNumber}</small>
                          </span>
                        ) : (
                          <span>Chọn xe của bạn</span>
                        )}
                        <span aria-hidden="true">
                          {vehiclePickerOpen ? "▲" : "▼"}
                        </span>
                      </button>
                      {vehiclePickerOpen && (
                        <div
                          className="customer-booking-vehicle-picker__options"
                          role="listbox"
                          aria-label="Danh sách xe của bạn"
                        >
                          {bookingVehicles.map((vehicle) => (
                            <button
                              key={vehicle.id}
                              type="button"
                              role="option"
                              aria-selected={
                                String(vehicle.id) === selectedVehicleId
                              }
                              onClick={() => {
                                setSelectedVehicleId(String(vehicle.id));
                                setVehiclePickerOpen(false);
                              }}
                            >
                              <strong>{vehicleName(vehicle)}</strong>
                              <small>{vehicle.plateNumber}</small>
                            </button>
                          ))}
                        </div>
                      )}
                    </div>
                  )}

                  {bookingVehicles.length === 0 && (
                    <label className="customer-booking-form__plate">
                      Biển số xe
                      <input
                        type="text"
                        name="plateNumber"
                        value={bookingPlateNumber}
                        onChange={(event) =>
                          setBookingPlateNumber(event.target.value)
                        }
                        placeholder="Ví dụ: 51F-123.45"
                        maxLength={20}
                        required
                      />
                    </label>
                  )}
                  {selectedVehicle && bookingVehicles.length > 1 && (
                    <div className="customer-booking-vehicle">
                      <span>Xe đã chọn</span>
                      <strong>{vehicleName(selectedVehicle)}</strong>
                      <div className="customer-booking-vehicle__details">
                        <span>
                          Biển số <strong>{selectedVehicle.plateNumber}</strong>
                        </span>
                        {selectedVehicle.color && (
                          <span>
                            Màu xe <strong>{selectedVehicle.color}</strong>
                          </span>
                        )}
                      </div>
                    </div>
                  )}
                </>
              )}
              <label>
                Thời gian bắt đầu
                <input
                  type="datetime-local"
                  name="startTime"
                  value={bookingStartTime}
                  min={localDateTimeValue()}
                  onChange={(event) => setBookingStartTime(event.target.value)}
                  required
                />
              </label>
              <label className="customer-booking-form__notes">
                Ghi chú (không bắt buộc)
                <textarea
                  name="notes"
                  value={bookingNotes}
                  onChange={(event) => setBookingNotes(event.target.value)}
                  maxLength={500}
                  rows={3}
                />
              </label>
              {bookingError && (
                <p className="customer-message customer-message--error" role="alert">
                  {bookingError}
                </p>
              )}
              <div className="customer-booking-form__actions">
                <button
                  type="submit"
                  className="btn btn--primary"
                  disabled={
                    bookingSaving ||
                    bookingDetailsLoading ||
                    Boolean(bookingDetailsError)
                  }
                >
                  {bookingSaving ? "Đang đặt chỗ..." : "Xác nhận đặt chỗ"}
                </button>
                <button
                  type="button"
                  className="btn btn--outline"
                  onClick={() => setBookingOpen(false)}
                  disabled={bookingSaving}
                >
                  Hủy
                </button>
              </div>
            </form>
          )}

          <div className="customer-lot-summary">
            <div>
              <strong>{currentCapacity?.availableForBooking ?? "—"}</strong>
              <span>Chỗ có thể nhận xe toàn bãi</span>
            </div>
            <div>
              <strong>{currentCapacity?.totalCapacity ?? "—"}</strong>
              <span>Tổng sức chứa</span>
            </div>
          </div>
          <div className="customer-capacity-meta" aria-live="polite">
            {capacityLoading && !currentCapacity && (
              <span>Đang tải sức chứa thực tế...</span>
            )}
            {currentCapacity && (
              <span>
                Đã tính cả {currentCapacity.pendingBookingsCount} chỗ
                đang được booking giữ.
                {capacityUpdatedAt &&
                  ` Cập nhật lúc ${capacityUpdatedAt.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" })}.`}
              </span>
            )}
            {capacityError && (
              <span className="customer-message--error" role="alert">
                Không tải được số chỗ có thể nhận xe: {capacityError}
              </span>
            )}
          </div>

          {selectedFloor && (
            <>
              {slotsError && (
                <p
                  className="customer-message customer-message--error"
                  role="alert"
                >
                  {slotsError}
                </p>
              )}
              {slotsLoading ? (
                <p className="customer-message">
                  Đang tải tình trạng vị trí...
                </p>
              ) : !slotsError ? (
                <>
                  <div
                    className="customer-stat-grid"
                    aria-label="Tổng quan tầng"
                  >
                    <div>
                      <strong>{availableCount}</strong>
                      <span>Ô trống vật lý</span>
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
            </>
          )}
        </div>
      )}

      {!publicView && (
        <section className="customer-card" aria-labelledby="my-bookings-title">
          <div className="customer-card__heading">
            <div>
              <h2 id="my-bookings-title">Đặt chỗ của tôi</h2>
              <p className="muted">
                Các lượt đặt chỗ được lưu trong tài khoản, tại mọi bãi.
              </p>
            </div>
            <button
              type="button"
              className="btn btn--outline"
              onClick={() => setRevision((value) => value + 1)}
              disabled={myBookingsLoading}
            >
              {myBookingsLoading ? "Đang tải..." : "Làm mới đặt chỗ"}
            </button>
          </div>
          {myBookingsError && (
            <p className="customer-message customer-message--error" role="alert">
              Không tải được đặt chỗ: {myBookingsError}
            </p>
          )}
          {bookingCancelError && (
            <p className="customer-message customer-message--error" role="alert">
              Không hủy được chỗ: {bookingCancelError}
            </p>
          )}
          {myBookingsLoading && myBookings.length === 0 && (
            <p className="customer-message">Đang tải đặt chỗ của bạn...</p>
          )}
          {!myBookingsLoading && !myBookingsError && myBookings.length === 0 && (
            <p className="customer-message">Bạn chưa có lượt đặt chỗ nào.</p>
          )}
          {myBookings.length > 0 && (
            <ul className="customer-booking-list" aria-label="Đặt chỗ của tôi">
              {myBookings.map((booking) => (
                <li className="customer-booking-item" key={booking.id}>
                  <div className="customer-booking-item__heading">
                    <div>
                      <strong>{booking.plateNumber}</strong>
                      <span>
                        {booking.lotName || `Bãi #${booking.lotId}`}
                        {booking.lotAddress ? ` · ${booking.lotAddress}` : ""}
                      </span>
                    </div>
                    <span
                      className={`customer-booking-status customer-booking-status--${String(booking.status).toLowerCase()}`}
                    >
                      {bookingStatusLabels[booking.status] || booking.status}
                    </span>
                  </div>
                  <dl>
                    <div>
                      <dt>Mã đặt chỗ</dt>
                      <dd>#{booking.id}</dd>
                    </div>
                    <div>
                      <dt>Dự kiến vào</dt>
                      <dd>{new Date(booking.startTime).toLocaleString("vi-VN")}</dd>
                    </div>
                    <div>
                      <dt>Dự kiến ra</dt>
                      <dd>
                        {booking.endTime
                          ? new Date(booking.endTime).toLocaleString("vi-VN")
                          : "Chưa xác định"}
                      </dd>
                    </div>
                    <div>
                      <dt>Đặt lúc</dt>
                      <dd>{new Date(booking.createdAt).toLocaleString("vi-VN")}</dd>
                    </div>
                  </dl>
                  {booking.status === "PENDING" && (
                    <button
                      type="button"
                      className="btn btn--outline"
                      onClick={() => cancelBooking(booking.id)}
                      disabled={cancellingBookingId !== null}
                    >
                      {cancellingBookingId === booking.id
                        ? "Đang hủy..."
                        : "Hủy chỗ"}
                    </button>
                  )}
                </li>
              ))}
            </ul>
          )}
        </section>
      )}

      <div className="customer-message customer-message--notice">
        <strong>Tình trạng bãi tại thời điểm cập nhật</strong>
        <span>
          Số chỗ có thể nhận xe đã trừ các booking đang giữ và xe đang ở bãi.
          Booking chưa giữ một ô vật lý cụ thể; trạng thái từng ô chỉ đổi khi
          nhân viên xác nhận xe đã đỗ thực tế.
        </span>
      </div>
    </section>
  );
}

export default function CustomerDashboard({ publicView = false }) {
  const auth = useSyncExternalStore(subscribeAuthSession, getAuthSession, getAuthSession);
  if (auth?.userProfile.role === "ADMIN" || auth?.userProfile.role === "MANAGER") return <ParkingSlotManagement role={auth.userProfile.role} />;
  if (publicView) return <DashboardContent publicView />;
  return (
    <RequireCustomer>
      <DashboardContent />
    </RequireCustomer>
  );
}
