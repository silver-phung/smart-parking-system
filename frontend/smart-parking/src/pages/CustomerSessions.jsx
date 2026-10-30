import { useState } from "react";
import { Link } from "react-router-dom";
import RequireCustomer from "../components/RequireCustomer.jsx";
import {
  Feedback,
  Pager,
  dateLabel,
  itemsOf,
  query,
  statusLabel,
  useResource,
} from "../components/WorkspaceUI.jsx";

function Sessions() {
  const [page, setPage] = useState(1);
  const [filter, setFilter] = useState({
    status: "",
    fromDate: "",
    toDate: "",
  });
  const [revision, refresh] = useState(0);
  const [selected, setSelected] = useState(null);
  const invalidDates = Boolean(
    filter.fromDate && filter.toDate && filter.fromDate > filter.toDate,
  );
  const list = useResource(
    invalidDates
      ? null
      : `/v1/parking-sessions/my-sessions?${query({ ...filter, fromDate: filter.fromDate ? new Date(`${filter.fromDate}T00:00:00`).toISOString() : "", toDate: filter.toDate ? new Date(`${filter.toDate}T23:59:59.999`).toISOString() : "", page, pageSize: 10 })}`,
    revision,
  );
  const detail = useResource(
    selected ? `/v1/parking-sessions/${selected}` : null,
    revision,
  );
  const update = (key, value) => {
    setFilter((f) => ({ ...f, [key]: value }));
    setPage(1);
    setSelected(null);
  };
  const money = (value) =>
    new Intl.NumberFormat("vi-VN", {
      style: "currency",
      currency: "VND",
    }).format(value || 0);
  return (
    <section className="workspace container">
      <header className="workspace-heading">
        <div>
          <span className="tag">Khách hàng</span>
          <h1>Phiên gửi xe của tôi</h1>
          <p>Theo dõi xe đang gửi và lịch sử ra vào của bạn.</p>
        </div>
        <button
          className="btn btn--outline"
          disabled={list.loading}
          onClick={() => refresh((n) => n + 1)}
        >
          Làm mới
        </button>
      </header>
      <nav className="workspace-tabs">
        <Link to="/customer">Bãi đỗ</Link>
        <Link className="active" to="/customer/sessions">
          Phiên gửi xe
        </Link>
        <Link to="/customer/profile">Hồ sơ</Link>
      </nav>
      <section className="workspace-panel">
        <div className="workspace-filters">
          <label>
            Trạng thái
            <select
              value={filter.status}
              onChange={(e) => update("status", e.target.value)}
            >
              <option value="">Tất cả phiên</option>
              <option value="ACTIVE">Đang gửi</option>
              <option value="PAID">Đã thanh toán</option>
              <option value="COMPLETED">Đã kết thúc</option>
              <option value="CANCELLED">Đã hủy</option>
            </select>
          </label>
          <label>
            Từ ngày
            <input
              type="date"
              value={filter.fromDate}
              onChange={(e) => update("fromDate", e.target.value)}
            />
          </label>
          <label>
            Đến ngày
            <input
              type="date"
              value={filter.toDate}
              onChange={(e) => update("toDate", e.target.value)}
            />
          </label>
        </div>
        <Feedback
          loading={list.loading}
          error={
            invalidDates
              ? "Ngày kết thúc phải từ ngày bắt đầu trở đi."
              : list.error
          }
        />
        {!list.loading &&
          !list.error &&
          !invalidDates &&
          itemsOf(list.data).length === 0 && (
            <div className="workspace-empty">
              <h2>Chưa có phiên phù hợp</h2>
              <p>
                Phiên gửi xe sẽ xuất hiện khi xe thuộc tài khoản của bạn được
                ghi nhận vào bãi.
              </p>
              <Link to="/customer/profile">Kiểm tra hồ sơ và xe của tôi</Link>
            </div>
          )}
        <div className="workspace-table">
          <table>
            <thead>
              <tr>
                <th>Xe / Bãi đỗ</th>
                <th>Vị trí</th>
                <th>Thời gian</th>
                <th>Trạng thái</th>
                <th>Chi tiết</th>
              </tr>
            </thead>
            <tbody>
              {itemsOf(list.data).map((s) => (
                <tr key={s.id}>
                  <td>
                    <strong>{s.plateNumber}</strong>
                    <small>{s.lotName}</small>
                  </td>
                  <td>
                    {s.slotCode || "Đang xác minh ô"}
                    {s.floorNumber != null && (
                      <small>Tầng {s.floorNumber}</small>
                    )}
                  </td>
                  <td>
                    {dateLabel(s.checkInTime)}
                    <small>Ra: {dateLabel(s.checkOutTime)}</small>
                  </td>
                  <td>
                    <span className="workspace-badge">
                      {statusLabel(s.status)}
                    </span>
                  </td>
                  <td>
                    <button
                      className="btn btn--outline"
                      onClick={() => setSelected(s.id)}
                    >
                      Xem phiên #{s.id}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <Pager
          page={page}
          data={list.data}
          onChange={setPage}
          disabled={list.loading || invalidDates}
        />
      </section>
      {selected && (
        <section className="workspace-panel">
          <div className="workspace-heading">
            <h2>Phiên #{selected}</h2>
            <button
              className="btn btn--outline"
              onClick={() => setSelected(null)}
            >
              Đóng chi tiết
            </button>
          </div>
          <Feedback loading={detail.loading} error={detail.error} />
          {detail.data && (
            <>
              <dl className="workspace-detail">
                {Object.entries({
                  "Biển số": detail.data.plateNumber,
                  "Bãi đỗ": detail.data.lotName,
                  "Ô hiện tại": detail.data.slotCode || "Đang xác minh",
                  "Trạng thái": statusLabel(detail.data.status),
                  "Vào bãi": dateLabel(detail.data.checkInTime),
                  "Ra bãi": dateLabel(detail.data.checkOutTime),
                  "Phí ghi nhận": money(detail.data.totalFee),
                  "Thanh toán lúc": dateLabel(detail.data.paidAt),
                }).map(([k, v]) => (
                  <div key={k}>
                    <dt>{k}</dt>
                    <dd>{v}</dd>
                  </div>
                ))}
              </dl>
              <div className="workspace-detail">
                {[
                  ["Ảnh vào bãi", detail.data.checkInImageUrl],
                  ["Ảnh ra bãi", detail.data.checkOutImageUrl],
                ].map(([label, url]) => (
                  <div key={label}>
                    <h3>{label}</h3>
                    {url ? (
                      <img
                        src={url}
                        alt={label}
                        loading="lazy"
                        referrerPolicy="no-referrer"
                      />
                    ) : (
                      <p>Chưa có ảnh.</p>
                    )}
                  </div>
                ))}
              </div>
              <p className="workspace-note">
                Phí hiển thị theo dữ liệu đã ghi nhận. Liên hệ nhân viên tại
                cổng để được hỗ trợ thanh toán và ra bãi.
              </p>
            </>
          )}
        </section>
      )}
    </section>
  );
}
export default function CustomerSessions() {
  return (
    <RequireCustomer>
      <Sessions />
    </RequireCustomer>
  );
}
