import { useCallback, useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import RequireRole from "../components/RequireRole.jsx";
import { apiRequest } from "../services/api.js";
import { operationsApi } from "../services/operationsApi.js";
import { collectPages } from "../services/staffApi.js";
import "../workspace.css";

const STORAGE_PREFIX = "smart-parking.lot-policy-details.";

export const DEFAULT_POLICY_TEMPLATE = {
  // Nhóm 1: Đặt chỗ trước (Booking)
  earlyArrivalMinutes: 15,
  lateArrivalMinutes: 10,
  holdMinutes: 15,
  cancellationNoticeMinutes: 30,
  depositAmount: 0,
  nearFullThreshold: 5,
  noShowAction: "EXPIRE",
  cancellationAction: "ALLOW",

  // Nhóm 2: Giá cước & Thời gian tính phí
  freeGraceMinutes: 10,
  feeUnitMinutes: 60,
  standardCarFeePerBlock: 25000,
  overnightSurcharge: 100000,
  postPaymentGraceMinutes: 15,
  feeRounding: "CEILING",
  rateSelection: "CHECK_IN",

  // Nhóm 3: Chế tài vi phạm & Bồi hoàn vé/thẻ
  wrongSlotPenaltyFee: 50000,
  lostTicketFee: 50000,
  maxUnattendedDays: 7,

  // Nhóm 4: An toàn giao thông, PCCC & Tuân thủ pháp luật Nhà nước
  maxSpeedKmh: 10,
  fireSafetyRule:
    "Nghiêm cấm mang chất dễ cháy nổ, hóa chất độc hại vào hầm đỗ xe; phương tiện rò rỉ nhiên liệu hoặc cải tạo hệ thống nhiên liệu LPG/CNG trái phép không được phép xuống tầng hầm kín (Tuân thủ Luật PCCC & QCVN 13:2018/BXD).",
  liabilityRule:
    "Đơn vị vận hành bãi đỗ chịu trách nhiệm trông giữ phương tiện và bồi thường thiệt hại nếu xảy ra mất cắp, hư hỏng xe có vé/phiên gửi xe hợp lệ theo Điều 554–561 Bộ luật Dân sự 2015. Khách hàng tự bảo quản tài sản, tư trang cá nhân để rời bên trong xe.",
  priceLawRule:
    "Bảng giá dịch vụ trông giữ xe được niêm yết công khai, minh bạch tại cổng trạm và trên hệ thống đặt chỗ trực tuyến, đã bao gồm thuế GTGT theo đúng Luật Giá 2023 và khung giá của UBND Tỉnh/Thành phố.",

  // Nhóm 5: Điều khoản bổ sung có thể thêm/sửa/xóa
  customRules: [
    {
      id: "rule-ev",
      title: "Quy định sử dụng ô sạc xe điện (EV)",
      content: "Ô đỗ có trụ sạc ưu tiên cho xe thuần điện (EV) và Hybrid sạc ngoài; phụ phí chiếm chỗ sau khi sạc đầy quá 30 phút: 30.000 VNĐ/giờ.",
    },
    {
      id: "rule-dispatch",
      title: "Chấp hành điều phối của nhân viên trực khu vực",
      content: "Chủ phương tiện có trách nhiệm đỗ đúng ô được phân công trên hệ thống và tắt máy, kéo phanh tay sau khi xe đã vào đúng vị trí.",
    },
  ],
};

function parseExtendedFromDecision(decisionReference) {
  if (!decisionReference || typeof decisionReference !== "string") return null;
  const marker = "||EXT:";
  const idx = decisionReference.indexOf(marker);
  if (idx === -1) return null;
  try {
    return JSON.parse(decisionReference.slice(idx + marker.length));
  } catch {
    return null;
  }
}

function buildDecisionReference(policy) {
  const summary = `Quy định vận hành chuẩn: Tới sớm ${policy.earlyArrivalMinutes}p, tới trễ ${policy.lateArrivalMinutes}p, phạt đỗ sai ô ${Number(policy.wrongSlotPenaltyFee).toLocaleString("vi-VN")}đ, phí mất thẻ ${Number(policy.lostTicketFee).toLocaleString("vi-VN")}đ, miễn phí ${policy.freeGraceMinutes}p đầu, tốc độ tối đa ${policy.maxSpeedKmh}km/h.`;
  return summary.slice(0, 490);
}

function loadSavedPolicyForLot(lotId, serverPolicies = []) {
  let savedLocal = null;
  try {
    const raw = localStorage.getItem(`${STORAGE_PREFIX}${lotId}`);
    if (raw) savedLocal = JSON.parse(raw);
  } catch {
    savedLocal = null;
  }

  const latestApproved =
    serverPolicies.find((p) => p.approvedAt) || serverPolicies[0] || null;

  const parsedFromNote = parseExtendedFromDecision(latestApproved?.decisionReference);

  return {
    ...DEFAULT_POLICY_TEMPLATE,
    ...(parsedFromNote || {}),
    ...(savedLocal || {}),
    ...(latestApproved
      ? {
          holdMinutes: latestApproved.holdMinutes ?? DEFAULT_POLICY_TEMPLATE.holdMinutes,
          earlyArrivalMinutes:
            latestApproved.earlyArrivalMinutes ?? DEFAULT_POLICY_TEMPLATE.earlyArrivalMinutes,
          lateArrivalMinutes:
            latestApproved.lateArrivalMinutes ?? DEFAULT_POLICY_TEMPLATE.lateArrivalMinutes,
          cancellationNoticeMinutes:
            latestApproved.cancellationNoticeMinutes ??
            DEFAULT_POLICY_TEMPLATE.cancellationNoticeMinutes,
          depositAmount:
            latestApproved.depositAmount ?? DEFAULT_POLICY_TEMPLATE.depositAmount,
          nearFullThreshold:
            latestApproved.nearFullThreshold ?? DEFAULT_POLICY_TEMPLATE.nearFullThreshold,
          feeUnitMinutes:
            latestApproved.feeUnitMinutes ?? DEFAULT_POLICY_TEMPLATE.feeUnitMinutes,
          feeRounding:
            latestApproved.feeRounding || DEFAULT_POLICY_TEMPLATE.feeRounding,
          noShowAction:
            latestApproved.noShowAction || DEFAULT_POLICY_TEMPLATE.noShowAction,
          cancellationAction:
            latestApproved.cancellationAction ||
            DEFAULT_POLICY_TEMPLATE.cancellationAction,
          rateSelection:
            latestApproved.rateSelection || DEFAULT_POLICY_TEMPLATE.rateSelection,
        }
      : {}),
  };
}

function formatCurrency(val) {
  const num = Number(val);
  if (!Number.isFinite(num)) return "0 VNĐ";
  return `${num.toLocaleString("vi-VN")} VNĐ`;
}

function PoliciesContent() {
  const [params] = useSearchParams();
  const [lots, setLots] = useState([]);
  const [lotsLoading, setLotsLoading] = useState(true);
  const [lotsError, setLotsError] = useState("");
  const [selectedLotId, setSelectedLotId] = useState(params.get("lotId") || "");

  const [serverPolicies, setServerPolicies] = useState([]);
  const [policyLoading, setPolicyLoading] = useState(false);
  const [policyError, setPolicyError] = useState("");
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState("");

  const [draft, setDraft] = useState(DEFAULT_POLICY_TEMPLATE);
  const [newRuleTitle, setNewRuleTitle] = useState("");
  const [newRuleContent, setNewRuleContent] = useState("");

  const loadLots = useCallback(async () => {
    setLotsLoading(true);
    setLotsError("");
    try {
      const items = await collectPages((page) =>
        apiRequest(`/v1/parking-lots?pageNumber=${page}&pageSize=100`).then((r) => r.data)
      );
      setLots(items);
      setSelectedLotId((current) => {
        if (current && items.some((l) => String(l.id) === String(current))) return String(current);
        const preferred = items.find((l) => (l.totalFloors || l.physicalSlotsCount) > 0) || items[0];
        return preferred ? String(preferred.id) : "";
      });
    } catch (err) {
      setLotsError(err.message || "Không thể tải danh sách bãi đỗ.");
    } finally {
      setLotsLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadLots();
  }, [loadLots]);

  const currentLot = lots.find((l) => String(l.id) === String(selectedLotId)) || lots[0];

  const loadLotPolicies = useCallback(async (lotId) => {
    if (!lotId) return;
    setPolicyLoading(true);
    setPolicyError("");
    try {
      const list = await operationsApi(lotId, "policies");
      const safeList = Array.isArray(list) ? list : [];
      setServerPolicies(safeList);
      setDraft(loadSavedPolicyForLot(lotId, safeList));
    } catch (err) {
      setPolicyError(err.message || "Không thể tải chính sách của bãi.");
      setDraft(loadSavedPolicyForLot(lotId, []));
    } finally {
      setPolicyLoading(false);
    }
  }, []);

  useEffect(() => {
    if (currentLot?.id) {
      setMessage("");
      void loadLotPolicies(currentLot.id);
    }
  }, [currentLot?.id, loadLotPolicies]);

  const patch = (key, value) => {
    setDraft((prev) => ({ ...prev, [key]: value }));
    setMessage("");
  };

  const handleSavePolicy = async (e) => {
    e.preventDefault();
    if (!currentLot) return;
    setSaving(true);
    setPolicyError("");
    setMessage("");
    try {
      const normalized = {
        ...draft,
        earlyArrivalMinutes: Math.max(0, Number(draft.earlyArrivalMinutes) || 0),
        lateArrivalMinutes: Math.max(0, Number(draft.lateArrivalMinutes) || 0),
        holdMinutes: Math.max(1, Number(draft.holdMinutes) || 15),
        cancellationNoticeMinutes: Math.max(0, Number(draft.cancellationNoticeMinutes) || 0),
        depositAmount: Math.max(0, Number(draft.depositAmount) || 0),
        nearFullThreshold: Math.max(0, Number(draft.nearFullThreshold) || 0),
        freeGraceMinutes: Math.max(0, Number(draft.freeGraceMinutes) || 0),
        feeUnitMinutes: Math.max(1, Number(draft.feeUnitMinutes) || 60),
        standardCarFeePerBlock: Math.max(0, Number(draft.standardCarFeePerBlock) || 0),
        overnightSurcharge: Math.max(0, Number(draft.overnightSurcharge) || 0),
        postPaymentGraceMinutes: Math.max(0, Number(draft.postPaymentGraceMinutes) || 0),
        wrongSlotPenaltyFee: Math.max(0, Number(draft.wrongSlotPenaltyFee) || 0),
        lostTicketFee: Math.max(0, Number(draft.lostTicketFee) || 0),
        maxUnattendedDays: Math.max(1, Number(draft.maxUnattendedDays) || 7),
        maxSpeedKmh: Math.max(5, Number(draft.maxSpeedKmh) || 10),
      };

      try {
        localStorage.setItem(`${STORAGE_PREFIX}${currentLot.id}`, JSON.stringify(normalized));
      } catch {
        // Ignore storage quota errors
      }

      const apiPayload = {
        zoneId: null,
        effectiveFrom: new Date(Date.now() - 60000).toISOString(),
        approve: true,
        decisionReference: buildDecisionReference(normalized),
        holdMinutes: normalized.holdMinutes,
        earlyArrivalMinutes: normalized.earlyArrivalMinutes,
        lateArrivalMinutes: normalized.lateArrivalMinutes,
        cancellationNoticeMinutes: normalized.cancellationNoticeMinutes,
        depositAmount: normalized.depositAmount,
        nearFullThreshold: normalized.nearFullThreshold,
        feeUnitMinutes: normalized.feeUnitMinutes,
        feeRounding: normalized.feeRounding || "CEILING",
        noShowAction: normalized.noShowAction || "EXPIRE",
        cancellationAction: normalized.cancellationAction || "ALLOW",
        rateSelection: normalized.rateSelection || "CHECK_IN",
      };

      await operationsApi(currentLot.id, "policies", apiPayload, "POST");
      const updatedList = await operationsApi(currentLot.id, "policies");
      setServerPolicies(Array.isArray(updatedList) ? updatedList : []);
      setDraft(normalized);
      setMessage(`Đã lưu và ban hành chính sách vận hành cho bãi "${currentLot.name}".`);
    } catch (err) {
      setPolicyError(err.message || "Không thể lưu chính sách.");
    } finally {
      setSaving(false);
    }
  };

  const handleRestoreDefaults = () => {
    setDraft(DEFAULT_POLICY_TEMPLATE);
    setMessage("Đã điền lại bộ chính sách mặc định chuẩn. Bấm 'Lưu & Áp dụng chính sách' để ban hành.");
  };

  const handleAddCustomRule = () => {
    if (!newRuleTitle.trim() || !newRuleContent.trim()) return;
    setDraft((prev) => ({
      ...prev,
      customRules: [
        ...(prev.customRules || []),
        {
          id: `rule-${Date.now()}`,
          title: newRuleTitle.trim(),
          content: newRuleContent.trim(),
        },
      ],
    }));
    setNewRuleTitle("");
    setNewRuleContent("");
    setMessage("");
  };

  const handleRemoveCustomRule = (id) => {
    setDraft((prev) => ({
      ...prev,
      customRules: (prev.customRules || []).filter((r) => r.id !== id),
    }));
  };

  return (
    <section className="workspace container">
      <header className="workspace-heading">
        <div>
          <span className="tag">Quản trị hệ thống</span>
          <h1>Chính sách bãi đỗ xe</h1>
          <p>
            Thiết lập các quy định vận hành, thời lượng đặt trước, mức phí dịch vụ, chế tài vi phạm nội quy và tuân thủ pháp luật hiện hành.
          </p>
        </div>
        <div className="workspace-actions">
          <button
            type="button"
            className="btn btn--outline"
            disabled={saving || policyLoading}
            onClick={handleRestoreDefaults}
          >
            ↺ Khôi phục mặc định chuẩn
          </button>
        </div>
      </header>

      {(lotsError || policyError) && (
        <p className="workspace-error" role="alert">
          {lotsError || policyError}
        </p>
      )}
      {message && (
        <p className="workspace-success" role="status">
          {message}
        </p>
      )}

      <section className="workspace-panel">
        <div className="workspace-heading">
          <div>
            <h2>Chọn bãi đỗ áp dụng chính sách</h2>
            <p className="workspace-note">
              Mỗi bãi đỗ có sẵn bộ chính sách mặc định phổ biến, có thể tùy chỉnh riêng và áp dụng ngay cho khách đặt chỗ.
            </p>
          </div>
          {currentLot && (
            <span
              style={{
                padding: "6px 12px",
                borderRadius: "999px",
                fontSize: "13px",
                fontWeight: 600,
                background: serverPolicies.some((p) => p.approvedAt) ? "#dcfce7" : "#dbeafe",
                color: serverPolicies.some((p) => p.approvedAt) ? "#166534" : "#1e40af",
              }}
            >
              {serverPolicies.some((p) => p.approvedAt)
                ? "✓ Đã ban hành chính sách hiệu lực"
                : "Đang dùng bộ chính sách mặc định chuẩn"}
            </span>
          )}
        </div>
        <div className="workspace-filters">
          <label>
            Bãi đỗ xe
            <select
              aria-label="Bãi đỗ áp dụng chính sách"
              disabled={lotsLoading || saving}
              value={currentLot?.id || ""}
              onChange={(e) => setSelectedLotId(e.target.value)}
            >
              {lots.map((l) => (
                <option key={l.id} value={l.id}>
                  {l.name} {l.address ? `(${l.address})` : ""}
                </option>
              ))}
            </select>
          </label>
        </div>

        {/* Tóm tắt nhanh các chỉ số chính sách nổi bật */}
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(185px, 1fr))",
            gap: "12px",
            marginTop: "16px",
          }}
        >
          <div style={{ padding: "12px 14px", borderRadius: "10px", background: "#f8fafc", border: "1px solid #e2e8f0" }}>
            <div style={{ fontSize: "12px", color: "#64748b" }}>Đặt trước tới sớm tối đa</div>
            <strong style={{ fontSize: "18px", color: "#0f172a" }}>{draft.earlyArrivalMinutes} phút</strong>
          </div>
          <div style={{ padding: "12px 14px", borderRadius: "10px", background: "#f8fafc", border: "1px solid #e2e8f0" }}>
            <div style={{ fontSize: "12px", color: "#64748b" }}>Đặt trước tới trễ tối đa</div>
            <strong style={{ fontSize: "18px", color: "#0f172a" }}>{draft.lateArrivalMinutes} phút</strong>
          </div>
          <div style={{ padding: "12px 14px", borderRadius: "10px", background: "#fef2f2", border: "1px solid #fecaca" }}>
            <div style={{ fontSize: "12px", color: "#991b1b" }}>Phạt xe đậu sai chỗ</div>
            <strong style={{ fontSize: "18px", color: "#b91c1c" }}>{formatCurrency(draft.wrongSlotPenaltyFee)}</strong>
          </div>
          <div style={{ padding: "12px 14px", borderRadius: "10px", background: "#fffbeb", border: "1px solid #fde68a" }}>
            <div style={{ fontSize: "12px", color: "#92400e" }}>Phí bồi hoàn mất thẻ/vé</div>
            <strong style={{ fontSize: "18px", color: "#b45309" }}>{formatCurrency(draft.lostTicketFee)}</strong>
          </div>
          <div style={{ padding: "12px 14px", borderRadius: "10px", background: "#f0fdf4", border: "1px solid #bbf7d0" }}>
            <div style={{ fontSize: "12px", color: "#166534" }}>Miễn phí ra vào nhanh</div>
            <strong style={{ fontSize: "18px", color: "#15803d" }}>{draft.freeGraceMinutes} phút đầu</strong>
          </div>
        </div>
      </section>

      {currentLot && (
        <form onSubmit={handleSavePolicy} style={{ display: "grid", gap: "20px" }}>
          {/* NHÓM 1: ĐẶT CHỖ TRƯỚC */}
          <section className="workspace-panel">
            <div>
              <h2>1. Chính sách Đặt chỗ trước (Booking & Giữ chỗ)</h2>
              <p className="workspace-note">
                Quy định khung thời gian nhận xe đặt qua web/app và điều kiện hủy lịch hẹn (bảo vệ quyền lợi khách hàng theo Luật Bảo vệ quyền lợi người tiêu dùng).
              </p>
            </div>
            <fieldset disabled={saving || policyLoading} className="workspace-form">
              <label>
                Thời lượng xe đặt trước tới sớm (phút)
                <input
                  type="number"
                  min={0}
                  max={180}
                  required
                  value={draft.earlyArrivalMinutes}
                  onChange={(e) => patch("earlyArrivalMinutes", e.target.value)}
                />
                <small style={{ color: "#64748b" }}>Khách đến sớm trong khoảng này được vào bãi nhận chỗ ngay (mặc định: 15 phút).</small>
              </label>

              <label>
                Thời lượng xe đặt trước tới trễ tối đa (phút)
                <input
                  type="number"
                  min={0}
                  max={180}
                  required
                  value={draft.lateArrivalMinutes}
                  onChange={(e) => patch("lateArrivalMinutes", e.target.value)}
                />
                <small style={{ color: "#64748b" }}>Quá thời gian này chưa vào cổng, hệ thống tự động hủy giữ chỗ (mặc định: 10 phút).</small>
              </label>

              <label>
                Thời gian khóa giữ chỗ tiêu chuẩn (phút)
                <input
                  type="number"
                  min={1}
                  max={240}
                  required
                  value={draft.holdMinutes}
                  onChange={(e) => patch("holdMinutes", e.target.value)}
                />
              </label>

              <label>
                Thời hạn báo hủy đặt chỗ miễn phí trước giờ hẹn (phút)
                <input
                  type="number"
                  min={0}
                  max={1440}
                  required
                  value={draft.cancellationNoticeMinutes}
                  onChange={(e) => patch("cancellationNoticeMinutes", e.target.value)}
                />
              </label>

              <label>
                Tiền đặt cọc khi đặt chỗ trước (VNĐ)
                <input
                  type="number"
                  min={0}
                  step={1000}
                  required
                  value={draft.depositAmount}
                  onChange={(e) => patch("depositAmount", e.target.value)}
                />
              </label>

              <label>
                Ngưỡng cảnh báo bãi sắp đầy ngừng nhận đặt online (số ô trống)
                <input
                  type="number"
                  min={0}
                  max={100}
                  required
                  value={draft.nearFullThreshold}
                  onChange={(e) => patch("nearFullThreshold", e.target.value)}
                />
              </label>

              <label>
                Xử lý khi quá giờ trễ mà xe không đến
                <select value={draft.noShowAction} onChange={(e) => patch("noShowAction", e.target.value)}>
                  <option value="EXPIRE">Tự động hết hạn đặt chỗ & giải phóng sức chứa</option>
                </select>
              </label>

              <label>
                Chính sách hủy lịch đặt trước của khách
                <select value={draft.cancellationAction} onChange={(e) => patch("cancellationAction", e.target.value)}>
                  <option value="ALLOW">Cho phép hủy theo thời hạn quy định</option>
                  <option value="DENY">Không cho phép hủy sau khi đã xác nhận</option>
                </select>
              </label>
            </fieldset>
          </section>

          {/* NHÓM 2: CHẾ TÀI VI PHẠM & BỒI HOÀN */}
          <section className="workspace-panel">
            <div>
              <h2>2. Chính sách Chế tài vi phạm nội quy & Bồi hoàn thẻ/vé</h2>
              <p className="workspace-note">
                Áp dụng công khai theo nội quy bãi đỗ xe và quy định hợp đồng gửi giữ tài sản (Bộ luật Dân sự 2015).
              </p>
            </div>
            <fieldset disabled={saving || policyLoading} className="workspace-form">
              <label>
                Tiền phạt / phụ phí điều phối khi xe đậu sai chỗ quy định (VNĐ)
                <input
                  type="number"
                  min={0}
                  step={5000}
                  required
                  value={draft.wrongSlotPenaltyFee}
                  onChange={(e) => patch("wrongSlotPenaltyFee", e.target.value)}
                />
                <small style={{ color: "#64748b" }}>Phụ phí điều phối khi xe cố tình đỗ sai ô đã chỉ định hoặc đỗ lấn 2 ô (mặc định: 50.000 VNĐ).</small>
              </label>

              <label>
                Phí bồi hoàn khi làm mất hoặc hỏng thẻ/vé kiểm soát (VNĐ)
                <input
                  type="number"
                  min={0}
                  step={5000}
                  required
                  value={draft.lostTicketFee}
                  onChange={(e) => patch("lostTicketFee", e.target.value)}
                />
                <small style={{ color: "#64748b" }}>Chi phí phôi thẻ từ & đối chiếu giấy đăng ký xe chính chủ khi khách báo mất vé (mặc định: 50.000 VNĐ).</small>
              </label>

              <label>
                Thời hạn tối đa lưu xe liên tục không thông báo (ngày)
                <input
                  type="number"
                  min={1}
                  max={90}
                  required
                  value={draft.maxUnattendedDays}
                  onChange={(e) => patch("maxUnattendedDays", e.target.value)}
                />
                <small style={{ color: "#64748b" }}>Quá số ngày này không liên hệ được chủ xe, Ban quản lý lập biên bản báo cơ quan công an địa phương.</small>
              </label>
            </fieldset>
          </section>

          {/* NHÓM 3: GIÁ CƯỚC & THỜI GIAN TÍNH PHÍ */}
          <section className="workspace-panel">
            <div>
              <h2>3. Chính sách Giá cước & Thời gian tính phí</h2>
              <p className="workspace-note">
                Niêm yết công khai theo Luật Giá 2023 và biểu khung giá dịch vụ trông giữ phương tiện của địa phương.
              </p>
            </div>
            <fieldset disabled={saving || policyLoading} className="workspace-form">
              <label>
                Thời gian miễn phí ra vào bãi đón/trả khách nhanh (phút)
                <input
                  type="number"
                  min={0}
                  max={60}
                  required
                  value={draft.freeGraceMinutes}
                  onChange={(e) => patch("freeGraceMinutes", e.target.value)}
                />
                <small style={{ color: "#64748b" }}>Xe vào và ra khỏi cổng trong thời gian này được miễn phí 100% (mặc định: 10 phút).</small>
              </label>

              <label>
                Đơn vị / Block thời gian tính phí (phút)
                <input
                  type="number"
                  min={15}
                  max={1440}
                  required
                  value={draft.feeUnitMinutes}
                  onChange={(e) => patch("feeUnitMinutes", e.target.value)}
                />
              </label>

              <label>
                Giá cước gửi xe ô tô tiêu chuẩn mỗi block (VNĐ)
                <input
                  type="number"
                  min={0}
                  step={1000}
                  required
                  value={draft.standardCarFeePerBlock}
                  onChange={(e) => patch("standardCarFeePerBlock", e.target.value)}
                />
              </label>

              <label>
                Phụ phí gửi xe qua đêm 22:00 – 06:00 (VNĐ / đêm)
                <input
                  type="number"
                  min={0}
                  step={5000}
                  required
                  value={draft.overnightSurcharge}
                  onChange={(e) => patch("overnightSurcharge", e.target.value)}
                />
              </label>

              <label>
                Thời gian ân hạn di chuyển ra cổng sau khi thanh toán (phút)
                <input
                  type="number"
                  min={5}
                  max={60}
                  required
                  value={draft.postPaymentGraceMinutes}
                  onChange={(e) => patch("postPaymentGraceMinutes", e.target.value)}
                />
              </label>

              <label>
                Nguyên tắc làm tròn thời gian tính phí
                <select value={draft.feeRounding} onChange={(e) => patch("feeRounding", e.target.value)}>
                  <option value="CEILING">Làm tròn lên theo block kế tiếp</option>
                  <option value="FLOOR">Làm tròn xuống</option>
                  <option value="NEAREST">Làm tròn gần nhất</option>
                </select>
              </label>
            </fieldset>
          </section>

          {/* NHÓM 4: AN TOÀN, PCCC & PHÁP LUẬT NHÀ NƯỚC */}
          <section className="workspace-panel">
            <div>
              <h2>4. Quy định An toàn giao thông, PCCC & Tuân thủ Pháp luật Nhà nước</h2>
              <p className="workspace-note">
                Căn cứ Bộ luật Dân sự 2015, Luật Phòng cháy chữa cháy, Luật Giá 2023 và Quy chuẩn kỹ thuật quốc gia QCVN 13:2018/BXD về Gara ô tô.
              </p>
            </div>
            <fieldset disabled={saving || policyLoading} style={{ border: 0, padding: 0, margin: 0, display: "grid", gap: "14px" }}>
              <label style={{ display: "grid", gap: "6px", fontWeight: 600 }}>
                Giới hạn tốc độ tối đa trong hầm / khuôn viên bãi (km/h)
                <input
                  type="number"
                  min={5}
                  max={30}
                  required
                  value={draft.maxSpeedKmh}
                  onChange={(e) => patch("maxSpeedKmh", e.target.value)}
                  style={{ maxWidth: "240px" }}
                />
              </label>

              <label style={{ display: "grid", gap: "6px", fontWeight: 600 }}>
                Quy định an toàn Phòng cháy chữa cháy (PCCC) & điều kiện phương tiện
                <textarea
                  rows={2}
                  required
                  value={draft.fireSafetyRule}
                  onChange={(e) => patch("fireSafetyRule", e.target.value)}
                />
              </label>

              <label style={{ display: "grid", gap: "6px", fontWeight: 600 }}>
                Trách nhiệm pháp lý về trông giữ và bồi thường tài sản (Điều 554–561 Bộ luật Dân sự 2015)
                <textarea
                  rows={2}
                  required
                  value={draft.liabilityRule}
                  onChange={(e) => patch("liabilityRule", e.target.value)}
                />
              </label>

              <label style={{ display: "grid", gap: "6px", fontWeight: 600 }}>
                Cam kết niêm yết giá và hóa đơn (Luật Giá 2023 & Nghị định 123/2020/NĐ-CP)
                <textarea
                  rows={2}
                  required
                  value={draft.priceLawRule}
                  onChange={(e) => patch("priceLawRule", e.target.value)}
                />
              </label>
            </fieldset>
          </section>

          {/* NHÓM 5: CÁC QUY ĐỊNH BỔ SUNG TÙY CHỈNH */}
          <section className="workspace-panel">
            <div>
              <h2>5. Các điều khoản & chính sách bổ sung tại bãi</h2>
              <p className="workspace-note">
                Có thể chỉnh sửa nội dung các quy định hiện có hoặc thêm quy định riêng phù hợp với đặc thù từng tòa nhà/bãi đỗ.
              </p>
            </div>

            <div style={{ display: "grid", gap: "12px", marginBottom: "16px" }}>
              {(draft.customRules || []).map((rule, index) => (
                <div
                  key={rule.id}
                  style={{
                    padding: "14px",
                    borderRadius: "10px",
                    border: "1px solid #e2e8f0",
                    background: "#f8fafc",
                    display: "grid",
                    gap: "8px",
                  }}
                >
                  <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: "12px" }}>
                    <input
                      type="text"
                      value={rule.title}
                      disabled={saving}
                      onChange={(e) => {
                        const updated = [...draft.customRules];
                        updated[index] = { ...rule, title: e.target.value };
                        patch("customRules", updated);
                      }}
                      style={{ flex: 1, fontWeight: 600 }}
                    />
                    <button
                      type="button"
                      className="btn btn--outline"
                      disabled={saving}
                      onClick={() => handleRemoveCustomRule(rule.id)}
                    >
                      Xóa điều khoản
                    </button>
                  </div>
                  <textarea
                    rows={2}
                    value={rule.content}
                    disabled={saving}
                    onChange={(e) => {
                      const updated = [...draft.customRules];
                      updated[index] = { ...rule, content: e.target.value };
                      patch("customRules", updated);
                    }}
                  />
                </div>
              ))}
            </div>

            <div
              style={{
                padding: "14px",
                borderRadius: "10px",
                border: "1px dashed #cbd5e1",
                display: "grid",
                gap: "10px",
              }}
            >
              <strong>+ Thêm chính sách / quy định mới</strong>
              <input
                type="text"
                placeholder="Tên quy định (VD: Quy định chiều cao xe giới hạn 2.1m)"
                value={newRuleTitle}
                disabled={saving}
                onChange={(e) => setNewRuleTitle(e.target.value)}
              />
              <textarea
                rows={2}
                placeholder="Nội dung chi tiết của chính sách..."
                value={newRuleContent}
                disabled={saving}
                onChange={(e) => setNewRuleContent(e.target.value)}
              />
              <div>
                <button
                  type="button"
                  className="btn btn--outline"
                  disabled={saving || !newRuleTitle.trim() || !newRuleContent.trim()}
                  onClick={handleAddCustomRule}
                >
                  + Thêm vào danh sách chính sách
                </button>
              </div>
            </div>
          </section>

          <section className="workspace-panel" style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "12px" }}>
            <div>
              <strong>Ban hành và áp dụng ngay cho bãi &ldquo;{currentLot.name}&rdquo;</strong>
              <p className="workspace-note" style={{ margin: 0 }}>
                Khi bấm Lưu, chính sách mới sẽ có hiệu lực ngay lập tức cho luồng Đặt chỗ trước và vận hành tại bãi.
              </p>
            </div>
            <div className="workspace-actions">
              <button type="button" className="btn btn--outline" disabled={saving} onClick={handleRestoreDefaults}>
                ↺ Mặc định chuẩn
              </button>
              <button type="submit" className="btn btn--primary" disabled={saving || policyLoading}>
                {saving ? "Đang lưu chính sách…" : "💾 Lưu & Áp dụng chính sách"}
              </button>
            </div>
          </section>
        </form>
      )}

      {currentLot && serverPolicies.length > 0 && (
        <section className="workspace-panel">
          <h2>Lịch sử phiên bản chính sách đã ban hành ({serverPolicies.length})</h2>
          <div style={{ display: "grid", gap: "8px", marginTop: "10px" }}>
            {serverPolicies.slice(0, 5).map((p) => (
              <div
                key={p.id}
                style={{
                  padding: "10px 14px",
                  borderRadius: "8px",
                  border: "1px solid #e2e8f0",
                  background: "#fff",
                  display: "flex",
                  justifyContent: "space-between",
                  alignItems: "center",
                  flexWrap: "wrap",
                  gap: "8px",
                }}
              >
                <div>
                  <strong>Phiên bản #{p.id}</strong> ·{" "}
                  <span style={{ color: p.approvedAt ? "#166534" : "#64748b", fontWeight: 600 }}>
                    {p.approvedAt ? "Đã duyệt & áp dụng" : "Bản nháp"}
                  </span>
                  <div style={{ fontSize: "13px", color: "#475569", marginTop: "2px" }}>
                    {p.decisionReference || "Chính sách vận hành bãi đỗ"}
                  </div>
                </div>
                <small style={{ color: "#64748b" }}>
                  Hiệu lực: {new Date(p.effectiveFrom).toLocaleString("vi-VN")}
                </small>
              </div>
            ))}
          </div>
        </section>
      )}
    </section>
  );
}

export default function AdminPolicies() {
  return (
    <RequireRole role={["ADMIN", "MANAGER"]}>
      <PoliciesContent />
    </RequireRole>
  );
}

