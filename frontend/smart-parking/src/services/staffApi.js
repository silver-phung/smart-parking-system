import { apiRequest } from "./api.js";

export const normalizePlate = (value = "") =>
  value.trim().toUpperCase().replace(/\s+/g, " ");
export const plateKey = (value = "") =>
  normalizePlate(value).replace(/[^A-Z0-9]/g, "");
export const platesMatch = (a, b) =>
  Boolean(plateKey(a)) && plateKey(a) === plateKey(b);
export const isOpenSession = (session) =>
  ["ACTIVE", "PAID"].includes(session?.status);

export function validatePlate(value) {
  const plate = normalizePlate(value);
  if (
    plate.length < 3 ||
    plateKey(plate).length < 3 ||
    plate.length > 20 ||
    !/^[A-Z0-9 .-]+$/.test(plate)
  ) {
    throw new Error(
      "Nhập biển số từ 3 đến 20 ký tự, chỉ gồm chữ, số, dấu cách, chấm hoặc gạch nối.",
    );
  }
  return plate;
}

export function validatePhoto(file) {
  if (!file || !["image/jpeg", "image/png", "image/webp"].includes(file.type)) {
    throw new Error("Chọn ảnh JPEG, PNG hoặc WebP.");
  }
  if (!file.size || file.size > 10 * 1024 * 1024) {
    throw new Error("Ảnh cần có dữ liệu và không vượt quá 10 MB.");
  }
}

const query = (values) =>
  new URLSearchParams(
    Object.entries(values).filter(
      ([, value]) => value !== undefined && value !== null && value !== "",
    ),
  ).toString();

const data = async (path, options) => (await apiRequest(path, options))?.data;
const post = (path, body) =>
  apiRequest(path, { method: "POST", body: JSON.stringify(body) });

export function readPage(value) {
  if (Array.isArray(value))
    return {
      items: value,
      totalCount: value.length,
      totalPages: 1,
      pageNumber: 1,
    };
  if (!value || !Array.isArray(value.items))
    throw new Error("Không tải được danh sách. Vui lòng thử lại.");
  return {
    ...value,
    totalPages:
      value.totalPages ?? Math.ceil(value.totalCount / (value.pageSize || 100)),
  };
}

export async function collectPages(loadPage) {
  const first = readPage(await loadPage(1));
  const items = [...first.items];
  for (let page = 2; page <= first.totalPages; page++) {
    items.push(...readPage(await loadPage(page)).items);
  }
  return items;
}

export function slotIsSelectable(slot, session, sessions, mode = "verify") {
  if (
    !slot?.isActive ||
    slot.lotId !== session?.lotId ||
    !isOpenSession(session)
  )
    return false;
  if (["MAINTENANCE", "UNAVAILABLE"].includes(slot.physicalStatus))
    return false;
  if (slot.currentSessionId && slot.currentSessionId !== session.id)
    return false;
  if (
    sessions.some(
      (other) =>
        other.id !== session.id &&
        isOpenSession(other) &&
        other.currentSlotId === slot.id,
    )
  )
    return false;
  if (mode === "assign")
    return (
      slot.physicalStatus === "EMPTY" &&
      (slot.isAvailable ||
        slot.currentSessionId === session.id ||
        session.currentSlotId === slot.id)
    );
  return session.status === "ACTIVE";
}

export function staffError(error) {
  if (error.status === 401)
    return "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại để tiếp tục.";
  if (error.status === 403)
    return (
      error.message ||
      "Bạn chưa được phân công nghiệp vụ này tại bãi. Vui lòng liên hệ quản lý."
    );
  return error.message || "Không thực hiện được thao tác. Vui lòng thử lại.";
}

export const staffApi = {
  assignedLots: (signal) => data("/v1/staff-assignments/my-lots", { signal }),
  capacity: (lotId, signal) =>
    data(`/v1/parking-lots/${lotId}/capacity`, { signal }),
  session: (id, signal) => data(`/v1/parking-sessions/${id}`, { signal }),
  activePage: async (lotId, page = 1, plateNumber = "", signal) =>
    readPage(
      await data(
        `/v1/parking-sessions/active?${query({ lotId, page, pageSize: 20, plateNumber })}`,
        { signal },
      ),
    ),
  activeSessions: (lotId, signal) =>
    collectPages((page) =>
      data(
        `/v1/parking-sessions/active?${query({ lotId, page, pageSize: 100 })}`,
        { signal },
      ),
    ),
  dispatchPage: async (lotId, page = 1, status = "", signal) =>
    readPage(
      await data(
        `/v1/parking-dispatches?${query({ lotId, page, pageSize: 20, status })}`,
        { signal },
      ),
    ),
  openDispatches: async (lotId, signal) =>
    (
      await Promise.all(
        ["PENDING", "ASSIGNED"].map((status) =>
          collectPages((page) =>
            data(
              `/v1/parking-dispatches?${query({ lotId, page, pageSize: 100, status })}`,
              { signal },
            ),
          ),
        ),
      )
    ).flat(),
  unmatchedSessions: (lotId, signal) =>
    data(`/v1/parking-sessions/unmatched?${query({ lotId })}`, { signal }),
  unmatchedSlots: (lotId, signal) =>
    data(`/v1/parking-sessions/unmatched-occupied-slots?${query({ lotId })}`, {
      signal,
    }),
  slot: (id, signal) => data(`/v1/parking-slots/${id}`, { signal }),
  async lotSlots(lotId, signal) {
    const floors = await data(
      `/v1/parking-lots/${lotId}/floors?isActiveOnly=true`,
      { signal },
    );
    if (!Array.isArray(floors))
      throw new Error("Không tải được danh sách tầng.");
    const slots = (
      await Promise.all(
        floors
          .filter((floor) => floor.isActive)
          .map(async (floor) =>
            (
              await collectPages((pageNumber) =>
                data(
                  `/v1/parking-floors/${floor.id}/slots?${query({ pageNumber, pageSize: 100, isActive: true })}`,
                  { signal },
                ),
              )
            ).map((slot) => ({ ...slot, floorName: floor.name })),
          ),
      )
    ).flat();
    return { floors, slots };
  },
  async findSession(lotId, value, mode = "plate", signal) {
    let session;
    if (mode === "id") {
      if (!/^\d+$/.test(value.trim()) || Number(value) < 1)
        throw new Error("Nhập mã phiên là số nguyên dương.");
      session = await staffApi.session(value.trim(), signal);
    } else {
      const plate = validatePlate(value);
      try {
        session = await data(
          `/v1/parking-sessions/by-plate/${encodeURIComponent(plate)}/active`,
          { signal },
        );
      } catch (error) {
        if (error.status !== 404) throw error;
        session = (await staffApi.activeSessions(lotId, signal)).find((item) =>
          platesMatch(item.plateNumber, plate),
        );
      }
    }
    if (!session || session.lotId !== Number(lotId))
      throw new Error("Không tìm thấy phiên xe tại bãi đang chọn.");
    return session;
  },
  detect(file) {
    validatePhoto(file);
    const body = new FormData();
    body.append("file", file);
    return data("/v1/staff/detect", { method: "POST", body });
  },
  async upload(file) {
    validatePhoto(file);
    const body = new FormData();
    body.append("file", file);
    const image = await data("/v1/images", { method: "POST", body });
    if (!image?.url || !/^https?:\/\//i.test(image.url))
      throw new Error(
        "Chưa lưu được ảnh xe. Hãy thử lại hoặc bỏ ảnh để nhập tay.",
      );
    return image.url;
  },
  confirmDetection: (id, plateNumber, note) =>
    post(`/v1/staff/detections/${id}/confirm`, {
      plateNumber: validatePlate(plateNumber),
      note,
    }),
  checkIn: (lotId, plateNumber, imageUrl) =>
    post("/v1/parking-sessions/check-in", {
      lotId: Number(lotId),
      plateNumber: validatePlate(plateNumber),
      ...(imageUrl ? { checkInImageUrl: imageUrl } : {}),
    }),
  async checkOut(sessionId, lotId, plateNumber, imageUrl) {
    const fresh = await staffApi.session(sessionId);
    if (!fresh || fresh.lotId !== Number(lotId))
      throw new Error("Phiên xe không thuộc bãi đang chọn.");
    if (fresh.status !== "PAID")
      throw new Error(
        "Phiên xe chưa được xác nhận thanh toán. Vui lòng hoàn tất thu phí trước khi cho xe ra.",
      );
    const plate = validatePlate(plateNumber);
    if (!platesMatch(fresh.plateNumber, plate))
      throw new Error(
        "Biển số tại cổng ra không khớp với phiên xe. Hãy kiểm tra lại.",
      );
    return post(`/v1/parking-sessions/${sessionId}/check-out`, {
      confirmedPlateNumber: plate,
      ...(imageUrl ? { checkOutImageUrl: imageUrl } : {}),
    });
  },
  async slotAction({ mode, session, slotId, notes, dispatch }) {
    const [freshSession, slot, sessions] = await Promise.all([
      staffApi.session(session.id),
      staffApi.slot(slotId),
      staffApi.activeSessions(session.lotId),
    ]);
    if (
      freshSession?.lotId !== session.lotId ||
      freshSession.status !== "ACTIVE" ||
      !slotIsSelectable(slot, freshSession, sessions, mode)
    ) {
      throw new Error(
        "Ô hoặc phiên xe đã thay đổi, đang được xe khác sử dụng hoặc không còn khả dụng. Hãy làm mới và chọn lại.",
      );
    }
    if (mode === "assign") {
      return dispatch
        ? post(`/v1/parking-dispatches/${dispatch.id}/assign`, {
            slotId: Number(slotId),
            notes,
          })
        : post("/v1/parking-dispatches", {
            sessionId: session.id,
            assignedSlotId: Number(slotId),
            dispatchMode: "MANUAL",
            notes,
          });
    }
    if (mode === "wrong") {
      if (!notes.trim()) throw new Error("Nhập lý do báo sai ô.");
      return post(`/v1/parking-sessions/${session.id}/report-wrong-slot`, {
        actualSlotId: Number(slotId),
        reason: notes.trim(),
      });
    }
    return post(`/v1/parking-sessions/${session.id}/verify-slot`, {
      slotId: Number(slotId),
      notes,
    });
  },
  cancelDispatch: (id, reason) =>
    post(`/v1/parking-dispatches/${id}/cancel`, { reason }),
};
