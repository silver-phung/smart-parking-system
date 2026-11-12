import { useEffect, useRef, useState } from "react";
import { staffApi } from "../../services/staffApi.js";
import {
  formatTime,
  Notice,
  StaffError,
  useStaffAction,
} from "./StaffUI.jsx";
import PlateCapture, {
  emptyCapture,
  prepareCapture,
} from "./PlateCapture.jsx";
import {
  platesMatch,
  validatePlate,
} from "../../services/staffApi.js";

const POLL_INTERVAL_MS = 15_000;

export default function StaffBookings({
  lotId,
  lotIsActive,
  revision,
  onChanged,
  onBusyChange,
  onNewBookingsChange,
  onConfirmed,
}) {
  const [bookings, setBookings] = useState(null);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);
  const [updatedAt, setUpdatedAt] = useState(null);
  const [newBookingIds, setNewBookingIds] = useState([]);
  const [retry, setRetry] = useState(0);
  const [selectedBookingId, setSelectedBookingId] = useState(null);
  const [capture, setCapture] = useState(emptyCapture);
  const [captureBusy, setCaptureBusy] = useState(false);
  const [completedBooking, setCompletedBooking] = useState(null);
  const [slotOptions, setSlotOptions] = useState(null);
  const [slotError, setSlotError] = useState(null);
  const [slotId, setSlotId] = useState("");
  const previousIds = useRef(null);
  const dataRef = useRef(null);
  const action = useStaffAction(onBusyChange);

  useEffect(() => {
    let active = true;
    let timeoutId;
    let controller;

    const load = async () => {
      controller = new AbortController();
      try {
        const result = await staffApi.pendingBookings(lotId, controller.signal);
        if (!active) return;

        const nextIds = new Set(result.items.map((booking) => String(booking.id)));
        const unseen =
          previousIds.current === null
            ? []
            : result.items
                .filter((booking) => !previousIds.current.has(String(booking.id)))
                .map((booking) => String(booking.id));

        setNewBookingIds((current) => [
          ...new Set([
            ...current.filter((id) => nextIds.has(id)),
            ...unseen,
          ]),
        ]);
        previousIds.current = nextIds;
        dataRef.current = result;
        setBookings(result);
        setError(null);
        setUpdatedAt(new Date());
      } catch (requestError) {
        if (active && !controller.signal.aborted) setError(requestError);
      } finally {
        if (active) {
          setLoading(false);
          timeoutId = window.setTimeout(load, POLL_INTERVAL_MS);
        }
      }
    };

    setLoading(dataRef.current === null);
    load();
    return () => {
      active = false;
      controller?.abort();
      window.clearTimeout(timeoutId);
    };
  }, [lotId, revision, retry]);

  useEffect(() => {
    if (!selectedBookingId) return undefined;
    const controller = new AbortController();
    setSlotOptions(null);
    setSlotError(null);
    Promise.all([
      staffApi.lotSlots(lotId, controller.signal),
      staffApi.unmatchedSlots(lotId, controller.signal),
      staffApi.openDispatches(lotId, controller.signal),
    ])
      .then(([{ slots }, unmatchedSlots, dispatches]) => {
        if (!controller.signal.aborted) {
          const unmatchedOccupiedIds = new Set(
            unmatchedSlots.map((item) => Number(item.slotId)),
          );
          const assignedSlotIds = new Set(
            dispatches
              .filter((dispatch) => dispatch.status === "ASSIGNED")
              .map((dispatch) => Number(dispatch.assignedSlotId)),
          );
          setSlotOptions(
            slots.filter(
              (slot) =>
                slot.isActive &&
                !assignedSlotIds.has(Number(slot.id)) &&
                ((slot.physicalStatus === "EMPTY" && slot.isAvailable) ||
                  (slot.physicalStatus === "OCCUPIED" &&
                    unmatchedOccupiedIds.has(Number(slot.id)))),
            ),
          );
        }
      })
      .catch((requestError) => {
        if (!controller.signal.aborted) setSlotError(requestError);
      });
    return () => controller.abort();
  }, [lotId, selectedBookingId]);

  const items = bookings?.items || [];
  const newBookings = items.filter((booking) =>
    newBookingIds.includes(String(booking.id)),
  );
  useEffect(() => {
    onNewBookingsChange?.(newBookings.length);
  }, [newBookings.length, onNewBookingsChange]);
  const selectedBooking = items.find(
    (booking) => String(booking.id) === String(selectedBookingId),
  );
  const busy = action.busy || captureBusy;
  const beginCheckIn = (booking) => {
    action.setError(null);
    setCompletedBooking(null);
    setSelectedBookingId(String(booking.id));
    setSlotId("");
    setCapture(emptyCapture());
  };
  const confirmCheckIn = (event) => {
    event.preventDefault();
    action.run(async () => {
      if (!selectedBooking) throw new Error("Đặt chỗ này không còn chờ xử lý.");
      if (!lotIsActive)
        throw new Error("Bãi đang đóng nhận xe. Không thể check-in booking.");
      if (!capture.file)
        throw new Error("Cần chụp ảnh biển số trước khi nhận xe đã đặt chỗ.");
      if (!slotOptions?.some((slot) => String(slot.id) === String(slotId)))
        throw new Error("Chọn ô trống thực tế nơi xe đang đỗ.");

      const plate = validatePlate(capture.plate);
      if (!platesMatch(plate, selectedBooking.plateNumber))
        throw new Error(
          `Biển số đã nhập không khớp với booking (${selectedBooking.plateNumber}). Hãy đối chiếu lại xe và ảnh trước khi xác nhận.`,
        );

      const imageUrl = await prepareCapture(capture, setCapture, lotId);
      if (!imageUrl) throw new Error("Chưa lưu được ảnh biển số xe.");

      const result = await staffApi.confirmBookingParked(
        selectedBooking.id,
        plate,
        Number(slotId),
        imageUrl,
      );
      if (
        !result?.data?.id ||
        Number(result.data.bookingId) !== Number(selectedBooking.id) ||
        Number(result.data.currentSlotId) !== Number(slotId)
      )
        throw new Error(
          "Chưa xác nhận được booking và ô xe đã đỗ. Hãy tải lại danh sách trước khi thử lại.",
        );

      setCompletedBooking({
        ...selectedBooking,
        sessionId: result.data.id,
        slotCode: result.data.slotCode,
      });
      setSelectedBookingId(null);
      setCapture(emptyCapture());
      setNewBookingIds((current) =>
        current.filter((id) => id !== String(selectedBooking.id)),
      );
      onChanged?.();
      onConfirmed?.(result.data);
    });
  };

  return (
    <section className="staff-panel staff-bookings" aria-labelledby="staff-bookings-title">
      <div className="staff-panel-heading">
        <div>
          <span className="staff-eyebrow">ĐẶT CHỖ TRỰC TUYẾN</span>
          <h2 id="staff-bookings-title">Xe đặt chỗ qua web</h2>
          <p>Đối chiếu thông tin khách và biển số, rồi ghi nhận ô xe đã đỗ.</p>
        </div>
        <span className="staff-count" aria-label={`${bookings?.totalCount ?? 0} đặt chỗ chờ`}>
          {bookings?.totalCount ?? "—"}
        </span>
      </div>
      {newBookings.length > 0 && (
        <Notice kind="warning">
          Có {newBookings.length} đặt chỗ mới:{" "}
          {newBookings.map((booking) => booking.plateNumber).join(", ")}. Danh
          sách được cập nhật tự động mỗi 15 giây.
        </Notice>
      )}
      {completedBooking && (
        <Notice kind="success">
          Đã xác nhận xe {completedBooking.plateNumber} từ booking #
          {completedBooking.id} đỗ tại ô {completedBooking.slotCode}. Ảnh biển
          số đã lưu cùng phiên #{completedBooking.sessionId}; ô đã chuyển sang
          trạng thái có xe (OCCUPIED).
        </Notice>
      )}
      <StaffError error={error} />
      {error && (
        <button
          className="staff-button staff-button--secondary"
          onClick={() => setRetry((value) => value + 1)}
        >
          Thử tải lại đặt chỗ
        </button>
      )}
      {loading && !bookings && <Notice>Đang tải đặt chỗ của bãi…</Notice>}
      {bookings && items.length === 0 && (
        <p className="staff-empty-small">Hiện không có đặt chỗ đang chờ xe đến.</p>
      )}
      {items.length > 0 && (
        <div className="staff-booking-list" aria-label="Danh sách xe đã đặt chỗ">
          {items.map((booking) => {
            const isNew = newBookingIds.includes(String(booking.id));
            return (
              <article
                className={`staff-booking-item ${isNew ? "is-new" : ""}`}
                key={booking.id}
              >
                <div className="staff-booking-vehicle">
                  <div>
                    <span className="staff-eyebrow">
                      ĐẶT CHỖ #{booking.id}
                    </span>
                    <strong className="staff-plate">{booking.plateNumber}</strong>
                  </div>
                  {isNew && <span className="staff-booking-new">MỚI</span>}
                </div>
                <dl>
                  <div>
                    <dt>Người đặt</dt>
                    <dd>{booking.fullName || booking.username || "—"}</dd>
                  </div>
                  {booking.username && booking.fullName && (
                    <div>
                      <dt>Tài khoản</dt>
                      <dd>{booking.username}</dd>
                    </div>
                  )}
                  {booking.phoneNumber && (
                    <div>
                      <dt>Điện thoại</dt>
                      <dd>
                        <a href={`tel:${booking.phoneNumber}`}>
                          {booking.phoneNumber}
                        </a>
                      </dd>
                    </div>
                  )}
                  {booking.email && (
                    <div>
                      <dt>Email</dt>
                      <dd>
                        <a href={`mailto:${booking.email}`}>{booking.email}</a>
                      </dd>
                    </div>
                  )}
                  <div>
                    <dt>Đặt lúc</dt>
                    <dd>{formatTime(booking.createdAt)}</dd>
                  </div>
                  <div>
                    <dt>Dự kiến vào</dt>
                    <dd>{formatTime(booking.startTime)}</dd>
                  </div>
                </dl>
                {String(selectedBookingId) === String(booking.id) ? (
                  <form
                    className="staff-booking-checkin"
                    onSubmit={confirmCheckIn}
                    aria-label={`Xác nhận xe tại bãi booking ${booking.id}`}
                  >
                    <label className="staff-field">
                      Ô thực tế xe đang đỗ
                      <select
                        required
                        value={slotId}
                        disabled={busy || !slotOptions}
                        onChange={(event) => setSlotId(event.target.value)}
                      >
                        <option value="">
                          {!slotOptions
                            ? "Đang tải ô trống…"
                            : "Chọn ô xe đang đỗ"}
                        </option>
                        {slotOptions?.map((slot) => (
                          <option key={slot.id} value={slot.id}>
                            {slot.floorName} · {slot.slotCode}
                            {slot.physicalStatus === "OCCUPIED"
                              ? " · Cảm biến có xe"
                              : ""}
                          </option>
                        ))}
                      </select>
                    </label>
                    {slotError && <StaffError error={slotError} />}
                    {slotOptions?.length === 0 && (
                      <Notice kind="warning">
                        Không có ô trống khả dụng ở bãi này. Hãy đồng bộ trạng
                        thái bãi trước khi xác nhận xe.
                      </Notice>
                    )}
                    <PlateCapture
                      value={capture}
                      onChange={setCapture}
                      disabled={busy}
                      requiredPhoto
                      title="Ảnh biển số xe"
                      onWorking={(value) => {
                        setCaptureBusy(value);
                        onBusyChange?.(value);
                      }}
                      allowRecognition={false}
                    />
                    <p className="staff-hint">
                      Biển số đặt chỗ: <strong>{booking.plateNumber}</strong>.
                      Kiểm tra biển số trên xe và ảnh, rồi nhập đúng biển số
                      thực tế trước khi xác nhận.
                    </p>
                    <StaffError error={action.error} />
                    <div className="staff-inline-actions">
                      <button
                        type="submit"
                        className="staff-button staff-button--primary"
                        disabled={
                          busy ||
                          !lotIsActive ||
                          !capture.file ||
                          !slotId ||
                          !slotOptions?.length
                        }
                      >
                        {action.busy
                          ? "Đang xác minh và cập nhật ô…"
                          : "Xác nhận xe đã đỗ"}
                      </button>
                      <button
                        type="button"
                        className="staff-text-button"
                        disabled={busy}
                        onClick={() => {
                          setSelectedBookingId(null);
                          setSlotOptions(null);
                          setSlotId("");
                          setCapture(emptyCapture());
                          action.setError(null);
                        }}
                      >
                        Hủy
                      </button>
                    </div>
                  </form>
                ) : (
                  <button
                    type="button"
                    className="staff-button staff-button--secondary"
                    disabled={busy || !lotIsActive}
                    onClick={() => beginCheckIn(booking)}
                  >
                    Xác nhận xe đã đỗ
                  </button>
                )}
              </article>
            );
          })}
        </div>
      )}
      {updatedAt && (
        <p className="staff-booking-updated">
          Cập nhật lúc {updatedAt.toLocaleTimeString("vi-VN", {
            hour: "2-digit",
            minute: "2-digit",
          })}
        </p>
      )}
    </section>
  );
}
