import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import RequireCustomer from "../components/RequireCustomer.jsx";
import { apiRequest } from "../services/api.js";
import { updateAuthProfile } from "../services/authSession.js";

const emptyVehicleForm = { plateNumber: "", brand: "", model: "", color: "" };

function ProfileContent() {
  const [profile, setProfile] = useState(null);
  const [vehicles, setVehicles] = useState([]);
  const [profileForm, setProfileForm] = useState({ fullName: "", phoneNumber: "" });
  const [newVehicle, setNewVehicle] = useState(emptyVehicleForm);
  const [vehicleForms, setVehicleForms] = useState({});
  const [profileLoading, setProfileLoading] = useState(true);
  const [vehiclesLoading, setVehiclesLoading] = useState(true);
  const [savingProfile, setSavingProfile] = useState(false);
  const [savingVehicle, setSavingVehicle] = useState(false);
  const [addingVehicle, setAddingVehicle] = useState(false);
  const [profileError, setProfileError] = useState("");
  const [vehicleError, setVehicleError] = useState("");
  const [profileMessage, setProfileMessage] = useState("");
  const [vehicleMessage, setVehicleMessage] = useState("");

  const loadProfile = useCallback(async () => {
    setProfileLoading(true);
    setProfileError("");
    try {
      const response = await apiRequest("/v1/users/me");
      const data = response?.data;
      if (!data) throw new Error("API không trả về thông tin hồ sơ.");
      setProfile(data);
      setProfileForm({
        fullName: data.fullName || "",
        phoneNumber: data.phoneNumber || "",
      });
    } catch (error) {
      setProfileError(error.message);
    } finally {
      setProfileLoading(false);
    }
  }, []);

  const loadVehicles = useCallback(async () => {
    setVehiclesLoading(true);
    setVehicleError("");
    try {
      const response = await apiRequest("/v1/vehicles?page=1&pageSize=100");
      const data = response?.data;
      const items = Array.isArray(data) ? data : data?.items;
      if (!Array.isArray(items)) throw new Error("API không trả về danh sách phương tiện hợp lệ.");
      setVehicles(items);
      setVehicleForms(
        Object.fromEntries(
          items.map((vehicle) => [
            vehicle.id,
            {
              brand: vehicle.brand || "",
              model: vehicle.model || "",
              color: vehicle.color || "",
            },
          ])
        )
      );
    } catch (error) {
      setVehicleError(error.message);
    } finally {
      setVehiclesLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadProfile();
    void loadVehicles();
  }, [loadProfile, loadVehicles]);

  const saveProfile = async (event) => {
    event.preventDefault();
    setSavingProfile(true);
    setProfileError("");
    setProfileMessage("");
    try {
      const response = await apiRequest("/v1/users/me", {
        method: "PUT",
        body: JSON.stringify(profileForm),
      });
      if (!response?.data) throw new Error("API không trả về hồ sơ sau khi cập nhật.");
      setProfile(response.data);
      setProfileForm({
        fullName: response.data.fullName || "",
        phoneNumber: response.data.phoneNumber || "",
      });
      updateAuthProfile(response.data);
      setProfileMessage(response.message || "Đã cập nhật thông tin cá nhân.");
    } catch (error) {
      setProfileError(error.message);
    } finally {
      setSavingProfile(false);
    }
  };

  const addVehicle = async (event) => {
    event.preventDefault();
    setAddingVehicle(true);
    setVehicleError("");
    setVehicleMessage("");
    try {
      const response = await apiRequest("/v1/vehicles", {
        method: "POST",
        body: JSON.stringify(newVehicle),
      });
      if (!response?.data) throw new Error("API không trả về phương tiện mới.");
      setNewVehicle(emptyVehicleForm);
      setVehicleMessage(response.message || "Đã thêm phương tiện.");
      await loadVehicles();
    } catch (error) {
      setVehicleError(error.message);
    } finally {
      setAddingVehicle(false);
    }
  };

  const saveVehicle = async (event, vehicleId) => {
    event.preventDefault();
    setSavingVehicle(true);
    setVehicleError("");
    setVehicleMessage("");
    try {
      const response = await apiRequest(`/v1/vehicles/${vehicleId}`, {
        method: "PUT",
        body: JSON.stringify(vehicleForms[vehicleId]),
      });
      if (!response?.data) throw new Error("API không trả về phương tiện sau khi cập nhật.");
      setVehicles((current) =>
        current.map((vehicle) => vehicle.id === vehicleId ? response.data : vehicle)
      );
      setVehicleForms((current) => ({
        ...current,
        [vehicleId]: {
          brand: response.data.brand || "",
          model: response.data.model || "",
          color: response.data.color || "",
        },
      }));
      setVehicleMessage(response.message || "Đã cập nhật thông tin xe.");
    } catch (error) {
      setVehicleError(error.message);
    } finally {
      setSavingVehicle(false);
    }
  };

  return (
    <section className="customer-page container">
      <div className="customer-page__heading">
        <div>
          <span className="tag">Tài khoản khách hàng</span>
          <h1>Hồ sơ của tôi</h1>
          <p className="muted">Quản lý thông tin cá nhân và phương tiện.</p>
        </div>
        <Link to="/customer" className="btn btn--outline">Xem bãi đỗ</Link>
      </div>

      <div className="customer-card">
        <div className="customer-card__heading">
          <div>
            <h2>Thông tin cá nhân</h2>
            <p className="muted">Tên đăng nhập và email được hệ thống quản lý, không thể sửa ở đây.</p>
          </div>
        </div>
        {profileLoading ? (
          <p className="customer-message">Đang tải hồ sơ...</p>
        ) : profileError && !profile ? (
          <div className="customer-message customer-message--error" role="alert">
            <span>{profileError}</span>
            <button type="button" className="text-button" onClick={() => void loadProfile()}>Thử lại</button>
          </div>
        ) : (
          <>
            <div className="customer-readonly-grid">
              <div><span>Tên đăng nhập</span><strong>{profile?.username || "—"}</strong></div>
              <div><span>Email</span><strong>{profile?.email || "—"}</strong></div>
            </div>
            <form className="customer-form" onSubmit={saveProfile}>
              <label>
                Họ và tên
                <input
                  value={profileForm.fullName}
                  onChange={(event) => setProfileForm((current) => ({ ...current, fullName: event.target.value }))}
                  maxLength={100}
                  required
                />
              </label>
              <label>
                Số điện thoại
                <input
                  type="tel"
                  value={profileForm.phoneNumber}
                  onChange={(event) => setProfileForm((current) => ({ ...current, phoneNumber: event.target.value }))}
                  maxLength={20}
                />
              </label>
              {profileError && <p className="customer-message customer-message--error" role="alert">{profileError}</p>}
              {profileMessage && <p className="customer-message customer-message--success" role="status">{profileMessage}</p>}
              <button type="submit" className="btn btn--primary" disabled={savingProfile}>
                {savingProfile ? "Đang lưu..." : "Lưu thông tin"}
              </button>
            </form>
          </>
        )}
      </div>

      <div className="customer-card">
        <div className="customer-card__heading">
          <div>
            <h2>Phương tiện</h2>
            <p className="muted">Thêm xe và cập nhật hãng xe, dòng xe hoặc màu sắc.</p>
          </div>
          <button type="button" className="btn btn--outline" onClick={() => void loadVehicles()} disabled={vehiclesLoading}>
            {vehiclesLoading ? "Đang tải..." : "Làm mới"}
          </button>
        </div>

        {vehicleError && <p className="customer-message customer-message--error" role="alert">{vehicleError}</p>}
        {vehicleMessage && <p className="customer-message customer-message--success" role="status">{vehicleMessage}</p>}
        {vehiclesLoading ? (
          <p className="customer-message">Đang tải danh sách phương tiện...</p>
        ) : vehicles.length === 0 ? (
          <p className="customer-message">Bạn chưa đăng ký phương tiện nào.</p>
        ) : (
          <div className="customer-vehicle-list">
            {vehicles.map((vehicle) => (
              <form className="customer-vehicle" key={vehicle.id} onSubmit={(event) => void saveVehicle(event, vehicle.id)}>
                <div className="customer-vehicle__heading">
                  <strong>{vehicle.plateNumber}</strong>
                  <span>{vehicle.isActive ? "Đang hoạt động" : "Đã ngừng hoạt động"}</span>
                </div>
                <div className="customer-form customer-form--compact">
                  <label>
                    Hãng xe
                    <input
                      value={vehicleForms[vehicle.id]?.brand || ""}
                      onChange={(event) => setVehicleForms((current) => ({
                        ...current,
                        [vehicle.id]: { ...current[vehicle.id], brand: event.target.value },
                      }))}
                      maxLength={50}
                    />
                  </label>
                  <label>
                    Dòng xe
                    <input
                      value={vehicleForms[vehicle.id]?.model || ""}
                      onChange={(event) => setVehicleForms((current) => ({
                        ...current,
                        [vehicle.id]: { ...current[vehicle.id], model: event.target.value },
                      }))}
                      maxLength={50}
                    />
                  </label>
                  <label>
                    Màu xe
                    <input
                      value={vehicleForms[vehicle.id]?.color || ""}
                      onChange={(event) => setVehicleForms((current) => ({
                        ...current,
                        [vehicle.id]: { ...current[vehicle.id], color: event.target.value },
                      }))}
                      maxLength={30}
                    />
                  </label>
                </div>
                <button type="submit" className="btn btn--outline" disabled={savingVehicle}>
                  {savingVehicle ? "Đang lưu..." : "Cập nhật xe"}
                </button>
              </form>
            ))}
          </div>
        )}

        <form className="customer-add-vehicle" onSubmit={addVehicle}>
          <h3>Thêm phương tiện</h3>
          <div className="customer-form customer-form--compact">
            <label>
              Biển số xe
              <input
                value={newVehicle.plateNumber}
                onChange={(event) => setNewVehicle((current) => ({ ...current, plateNumber: event.target.value }))}
                minLength={3}
                maxLength={20}
                required
              />
            </label>
            <label>
              Hãng xe
              <input
                value={newVehicle.brand}
                onChange={(event) => setNewVehicle((current) => ({ ...current, brand: event.target.value }))}
                maxLength={50}
              />
            </label>
            <label>
              Dòng xe
              <input
                value={newVehicle.model}
                onChange={(event) => setNewVehicle((current) => ({ ...current, model: event.target.value }))}
                maxLength={50}
              />
            </label>
            <label>
              Màu xe
              <input
                value={newVehicle.color}
                onChange={(event) => setNewVehicle((current) => ({ ...current, color: event.target.value }))}
                maxLength={30}
              />
            </label>
          </div>
          <button type="submit" className="btn btn--primary" disabled={addingVehicle}>
            {addingVehicle ? "Đang thêm..." : "Thêm xe"}
          </button>
        </form>
      </div>

      <div className="customer-message customer-message--notice">
        <strong>Thông tin giấy phép lái xe (GPLX) chưa được hỗ trợ</strong>
        <span>
          API và hồ sơ backend hiện không có trường lưu số GPLX, hạng bằng, ngày hết hạn hoặc ảnh
          bằng. Chưa thể nhập/lưu thông tin này an toàn bằng giao diện hiện tại.
        </span>
      </div>
    </section>
  );
}

export default function CustomerProfile() {
  return (
    <RequireCustomer>
      <ProfileContent />
    </RequireCustomer>
  );
}
