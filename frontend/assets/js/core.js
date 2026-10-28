/* ==========================================================
   SmartPark - Lõi dữ liệu dùng chung cho Client và Admin
   Hiện tại dữ liệu là DỮ LIỆU MẪU lưu trong localStorage.
   Khi có backend, thay các hàm trong khối "NGUỒN DỮ LIỆU" bằng lời gọi API
   (ví dụ fetch('/api/lots'), WebSocket cho trạng thái chỗ đỗ real-time).
   ========================================================== */
(function () {
  'use strict';
  var KEY = 'smartpark.state.v1', THEME_KEY = 'smartpark.theme';
  var SP = window.SP = {};

  SP.CFG = { brand: 'SmartPark', city: 'TP. Hồ Chí Minh', graceMin: 15 };
  SP.VEH = { car: { label: 'Ô tô' }, moto: { label: 'Xe máy' }, ev: { label: 'Ô tô điện' } };
  SP.PAY = {
    momo: 'Ví MoMo', zalopay: 'ZaloPay', card: 'Thẻ ATM / Visa', cash: 'Tiền mặt tại bãi'
  };

  /* ---------- Biểu tượng ---------- */
  var P = {
    car: '<path d="M5 16v-5l2-5h10l2 5v5M3 16h18M7 16v2.5M17 16v2.5"/><circle cx="8" cy="13" r=".7"/><circle cx="16" cy="13" r=".7"/>',
    moto: '<circle cx="6" cy="16" r="3"/><circle cx="18" cy="16" r="3"/><path d="M6 16l3-6h5l4 6M12 10l-1-3H8.5"/>',
    ev: '<path d="M13 3 5 14h6l-1 7 8-11h-6l1-7Z"/>',
    pin: '<path d="M12 21s7-6.2 7-11a7 7 0 1 0-14 0c0 4.8 7 11 7 11Z"/><circle cx="12" cy="10" r="2.5"/>',
    qr: '<rect x="4" y="4" width="6" height="6" rx="1"/><rect x="14" y="4" width="6" height="6" rx="1"/><rect x="4" y="14" width="6" height="6" rx="1"/><path d="M14 14h2v2h-2zM18 14h2v6h-4M14 18h2v2"/>',
    ticket: '<path d="M4 8a2 2 0 0 0 0 8v2a1 1 0 0 0 1 1h14a1 1 0 0 0 1-1v-2a2 2 0 0 1 0-8V6a1 1 0 0 0-1-1H5a1 1 0 0 0-1 1v2Z"/><path d="M14 5v14" stroke-dasharray="2 2"/>',
    user: '<circle cx="12" cy="8" r="4"/><path d="M4 21c0-4 4-6 8-6s8 2 8 6"/>',
    map: '<path d="M9 4 3 6v14l6-2 6 2 6-2V4l-6 2-6-2ZM9 4v14M15 6v14"/>',
    check: '<path d="m5 12.5 4.5 4.5L19 7"/>',
    alert: '<path d="M12 3 2 20h20L12 3ZM12 10v4.5M12 17.5v.5"/>',
    chart: '<path d="M4 20V4M4 20h16M8 16v-5M12 16V8M16 16v-8"/>',
    grid: '<rect x="4" y="4" width="7" height="7" rx="1"/><rect x="13" y="4" width="7" height="7" rx="1"/><rect x="4" y="13" width="7" height="7" rx="1"/><rect x="13" y="13" width="7" height="7" rx="1"/>',
    settings: '<circle cx="12" cy="12" r="3"/><path d="M12 2.5v3M12 18.5v3M2.5 12h3M18.5 12h3M5.3 5.3l2.1 2.1M16.6 16.6l2.1 2.1M18.7 5.3l-2.1 2.1M7.4 16.6l-2.1 2.1"/>',
    clock: '<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>',
    card: '<rect x="3" y="6" width="18" height="13" rx="2"/><path d="M3 10.5h18M7 15h4"/>',
    wallet: '<path d="M4 7h14a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V7Zm0 0V6a2 2 0 0 1 2-2h10M16 14h2"/>',
    search: '<circle cx="11" cy="11" r="6"/><path d="m20 20-4.5-4.5"/>',
    x: '<path d="M6 6l12 12M18 6 6 18"/>',
    plus: '<path d="M12 5v14M5 12h14"/>',
    back: '<path d="M15 5 8 12l7 7"/>',
    tag: '<path d="M3 12V4h8l10 10-8 8L3 12Z"/><circle cx="7.5" cy="8.5" r="1"/>',
    bell: '<path d="M6 16v-5a6 6 0 1 1 12 0v5l2 2H4l2-2ZM10 21h4"/>',
    download: '<path d="M12 4v11m0 0-4-4m4 4 4-4M5 20h14"/>',
    sun: '<circle cx="12" cy="12" r="4"/><path d="M12 2v2.5M12 19.5V22M2 12h2.5M19.5 12H22M5 5l1.8 1.8M17.2 17.2 19 19M19 5l-1.8 1.8M6.8 17.2 5 19"/>',
    moon: '<path d="M20 14.5A8 8 0 0 1 9.5 4a8 8 0 1 0 10.5 10.5Z"/>',
    nav: '<path d="m4 11 16-7-7 16-2-7-7-2Z"/>',
    flame: '<path d="M12 3c1 4 5 5 5 10a5 5 0 0 1-10 0c0-2 1-3 2-4 0 2 1 3 2 3 0-3-1-6 1-9Z"/>',
    home: '<path d="M4 11 12 4l8 7v9H4v-9Z"/>',
    out: '<path d="M10 4H5v16h5M14 8l4 4-4 4M18 12H9"/>'
  };
  SP.icon = function (n, s) { s = s || 18; return '<svg class="ic" width="' + s + '" height="' + s + '" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' + (P[n] || '') + '</svg>'; };

  /* ---------- Tiện ích ---------- */
  var pad = function (n) { return (n < 10 ? '0' : '') + n; };
  SP.pad = pad;
  SP.esc = function (s) { return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; }); };
  SP.money = function (n) { return Math.round(n).toLocaleString('vi-VN') + '₫'; };
  SP.moneyShort = function (n) { return n >= 1e6 ? (n / 1e6).toFixed(n >= 1e7 ? 0 : 1).replace('.', ',') + ' tr' : n >= 1e3 ? Math.round(n / 1e3) + 'k' : String(Math.round(n)); };
  SP.hm = function (ms) { var d = new Date(ms); return pad(d.getHours()) + ':' + pad(d.getMinutes()); };
  SP.dm = function (ms) { var d = new Date(ms); return pad(d.getDate()) + '/' + pad(d.getMonth() + 1); };
  SP.dhm = function (ms) { return SP.hm(ms) + ' · ' + SP.dm(ms); };
  SP.dur = function (ms) { var m = Math.max(0, Math.round(ms / 60000)), h = Math.floor(m / 60); return h ? h + ' giờ ' + pad(m % 60) + ' phút' : m + ' phút'; };
  SP.ago = function (ms) { var m = Math.round((Date.now() - ms) / 60000); return m < 1 ? 'vừa xong' : m < 60 ? m + ' phút trước' : Math.round(m / 60) + ' giờ trước'; };
  SP.clamp = function (v, a, b) { return Math.min(b, Math.max(a, v)); };
  function rng(seed) { return function () { seed |= 0; seed = seed + 0x6D2B79F5 | 0; var t = Math.imul(seed ^ seed >>> 15, 1 | seed); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; }; }
  SP.rng = rng;
  var LET = 'ABCDEFGHKLMNPSTUVXYZ';
  function plate(r, moto) {
    var prov = 50 + Math.floor(r() * 10), L = LET[Math.floor(r() * LET.length)];
    var n3 = ('00' + Math.floor(r() * 1000)).slice(-3), n2 = ('0' + Math.floor(r() * 100)).slice(-2);
    return prov + L + (moto ? 1 + Math.floor(r() * 9) : '') + '-' + n3 + '.' + n2;
  }
  SP.plate = plate;

  /* ---------- Danh sách bãi xe (DỮ LIỆU MẪU, sửa tại đây) ----------
     Cấu hình tên, tọa độ, loại chỗ và tầng; các màn hình Client/Admin cùng đọc dữ liệu này.
  */
  function F(id, name, types, cols) { return { id: id, name: name, cols: cols, rows: types.map(function (t, i) { return { r: 'ABCDE'[i], type: t }; }) }; }
  SP.LOTS = [
    { id: 'L1', name: 'Nguyễn Huệ Central', address: 'Đường Nguyễn Huệ, Quận 1', lat: 10.7740, lng: 106.7030, hours: 'Mở cửa 24/7', busy: 0.82, kind: 'office', floors: [F('B1', 'Hầm B1', ['car', 'car', 'car', 'moto'], 8), F('B2', 'Hầm B2', ['car', 'car', 'ev', 'moto'], 8)] },
    { id: 'L2', name: 'Phạm Ngũ Lão Plaza', address: 'Phạm Ngũ Lão, Quận 1', lat: 10.7677, lng: 106.6930, hours: '06:00 - 24:00', busy: 0.58, kind: 'mall', floors: [F('G', 'Tầng trệt', ['car', 'car', 'moto', 'moto'], 10)] },
    { id: 'L3', name: 'Thảo Điền Riverside', address: 'Xuân Thủy, TP. Thủ Đức', lat: 10.8060, lng: 106.7340, hours: 'Mở cửa 24/7', busy: 0.34, kind: 'office', floors: [F('B1', 'Hầm B1', ['car', 'car', 'ev', 'moto'], 8)] },
    { id: 'L4', name: 'Tân Sơn Nhất P2', address: 'Trường Sơn, Quận Tân Bình', lat: 10.8188, lng: 106.6520, hours: 'Mở cửa 24/7', busy: 0.9, kind: 'airport', floors: [F('T1', 'Tầng 1', ['car', 'car', 'car', 'moto', 'moto'], 10)] }
  ];
  SP.lot = function (id) { return SP.LOTS.filter(function (l) { return l.id === id; })[0]; };
  SP.slotId = function (f, r, c) { return f.id + '-' + r + pad(c); };
  SP.floorOf = function (lot, fid) { return lot.floors.filter(function (f) { return f.id === fid; })[0]; };

  /* ---------- Giá vé mặc định ---------- */
  var DEF_PRICING = {
    def: { car: { first: 20000, next: 10000, cap: 120000 }, moto: { first: 5000, next: 3000, cap: 30000 }, ev: { first: 25000, next: 12000, cap: 150000 } },
    lots: {},
    peak: { on: true, pct: 20, from: 17, to: 20 }
  };
  SP.defaultPricing = function () { return JSON.parse(JSON.stringify(DEF_PRICING)); };

  /* ---------- Kho dữ liệu demo ----------
     Tạo/đọc state từ localStorage và báo thay đổi cho các màn hình cùng tab hoặc tab khác.
     Đây chưa phải kho backend; chỉ dùng cho demo trên trình duyệt.
  */
  function buildState() {
    var r = rng(20261009), now = Date.now(), st = { v: 1, slots: {}, bookings: [], alerts: [], events: [], pricing: SP.defaultPricing(), seq: 1, aseq: 1 };
    SP.LOTS.forEach(function (lot) {
      st.slots[lot.id] = {};
      lot.floors.forEach(function (f) {
        f.rows.forEach(function (row) {
          for (var c = 1; c <= f.cols; c++) {
            var s = { t: row.type, f: f.id, st: 'free' }, x = r();
            if (x < lot.busy) {
              if (r() < 0.1) s.st = 'res';
              else { s.st = 'occ'; s.plate = plate(r, row.type === 'moto'); s.since = now - Math.floor(r() * 6 * 3600e3); }
            } else if (r() < 0.025) s.st = 'blk';
            st.slots[lot.id][SP.slotId(f, row.r, c)] = s;
          }
        });
      });
    });
    var m = 60000;
    st.alerts = [
      { id: 'A1', sev: 'high', type: 'overstay', lotId: 'L1', msg: 'Xe ' + plate(r, false) + ' đỗ quá giờ đặt 3 giờ 20 phút tại Hầm B1', t: now - 14 * m, status: 'new' },
      { id: 'A2', sev: 'high', type: 'sensor', lotId: 'L4', msg: 'Cảm biến T1-C04 mất kết nối hơn 10 phút', t: now - 31 * m, status: 'new' },
      { id: 'A3', sev: 'med', type: 'full', lotId: 'L4', msg: 'Bãi Tân Sơn Nhất P2 còn dưới 10% chỗ trống', t: now - 52 * m, status: 'ack', note: 'Đã báo bảo vệ chặn xe vãng lai.' },
      { id: 'A4', sev: 'med', type: 'payment', lotId: 'L2', msg: 'Giao dịch ZaloPay #ZP88231 thất bại, khách chưa ra được cổng', t: now - 75 * m, status: 'new' },
      { id: 'A5', sev: 'low', type: 'noshow', lotId: 'L1', msg: 'Đặt chỗ B2-C03 quá hạn nhận chỗ 15 phút', t: now - 3 * 3600e3, status: 'done', note: 'Đã trả chỗ về trạng thái trống.' }
    ];
    st.aseq = 6;
    st.events = [
      { t: now - 2 * m, type: 'in', lotId: 'L1', msg: 'Xe ' + plate(r, false) + ' vào bãi' },
      { t: now - 5 * m, type: 'out', lotId: 'L2', msg: 'Xe ' + plate(r, true) + ' ra bãi' },
      { t: now - 9 * m, type: 'in', lotId: 'L4', msg: 'Xe ' + plate(r, false) + ' vào bãi' },
      { t: now - 12 * m, type: 'book', lotId: 'L3', msg: 'Đặt chỗ mới B1-C02 (ô tô điện)' }
    ];
    return st;
  }
  function parse(s) { try { var o = JSON.parse(s); return o && o.v === 1 ? o : null; } catch (e) { return null; } }
  function load() { var raw = null; try { raw = localStorage.getItem(KEY); } catch (e) { } return parse(raw) || buildState(); }
  SP.state = load();
  var subs = [];
  SP.subscribe = function (fn) { subs.push(fn); };
  function notify(src) { subs.forEach(function (f) { try { f(src); } catch (e) { console.error(e); } }); }
  SP.save = function () { try { localStorage.setItem(KEY, JSON.stringify(SP.state)); } catch (e) { } notify('local'); };
  SP.reset = function () { SP.state = buildState(); SP.save(); };
  window.addEventListener('storage', function (e) { if (e.key === KEY) { var s = parse(e.newValue); if (s) { SP.state = s; notify('remote'); } } });
  SP.pushEvent = function (type, lotId, msg) { var ev = SP.state.events; ev.unshift({ t: Date.now(), type: type, lotId: lotId, msg: msg }); if (ev.length > 60) ev.length = 60; };
  SP.addAlert = function (sev, type, lotId, msg) {
    var a = { id: 'A' + (SP.state.aseq++), sev: sev, type: type, lotId: lotId, msg: msg, t: Date.now(), status: 'new' };
    SP.state.alerts.unshift(a); if (SP.state.alerts.length > 80) SP.state.alerts.length = 80; return a;
  };

  /* ---------- Thống kê chỗ đỗ ----------
     Đếm tổng số chỗ/trạng thái và tổng hợp theo loại xe hoặc tầng.
  */
  SP.counts = function (lotId, floorId) {
    var o = { total: 0, free: 0, occ: 0, res: 0, blk: 0, byType: { car: { total: 0, free: 0 }, moto: { total: 0, free: 0 }, ev: { total: 0, free: 0 } } };
    var sl = SP.state.slots[lotId] || {};
    Object.keys(sl).forEach(function (id) {
      var s = sl[id]; if (floorId && s.f !== floorId) return;
      o.total++; o[s.st]++;
      o.byType[s.t].total++; if (s.st === 'free') o.byType[s.t].free++;
    });
    return o;
  };
  SP.allCounts = function () {
    var o = { total: 0, free: 0, occ: 0, res: 0, blk: 0 };
    SP.LOTS.forEach(function (l) { var c = SP.counts(l.id); ['total', 'free', 'occ', 'res', 'blk'].forEach(function (k) { o[k] += c[k]; }); });
    return o;
  };
  SP.status = function (c, vehicle) {
    var free = vehicle && vehicle !== 'all' ? c.byType[vehicle].free : c.free, tot = vehicle && vehicle !== 'all' ? c.byType[vehicle].total : c.total;
    if (!tot || free === 0) return { k: 'occ', label: 'Hết chỗ', free: free };
    return free / tot < 0.15 ? { k: 'res', label: 'Sắp đầy', free: free } : { k: 'free', label: 'Còn chỗ', free: free };
  };

  /* ---------- Tính giá vé ----------
     Chọn bảng giá riêng hoặc mặc định, áp trần theo ngày và phụ thu giờ cao điểm.
  */
  SP.pricingFor = function (lotId) { var p = SP.state.pricing; return p.lots[lotId] || p.def; };
  SP.price = function (vehicle, hours, startMs, lotId) {
    var pr = SP.state.pricing, p = SP.pricingFor(lotId)[vehicle];
    hours = Math.max(1, Math.ceil(hours - 1e-9));
    var days = Math.floor(hours / 24), rem = hours - days * 24;
    var base = days * p.cap + (rem ? Math.min(p.cap, p.first + (rem - 1) * p.next) : 0);
    var peak = false;
    if (pr.peak.on && startMs != null) {
      var h = new Date(startMs).getHours();
      peak = pr.peak.from <= pr.peak.to ? (h >= pr.peak.from && h < pr.peak.to) : (h >= pr.peak.from || h < pr.peak.to);
      if (peak) base = Math.round(base * (1 + pr.peak.pct / 100) / 1000) * 1000;
    }
    return { amount: base, peak: peak };
  };

  /* ---------- Vòng đời vé ----------
     Tạo/hủy đặt chỗ, check-in, báo phí phát sinh và hoàn tất check-out.
     Các hàm này là điểm tập trung để thay dữ liệu mẫu bằng API khi nối backend.
  */
  function token() { var s = ''; for (var i = 0; i < 8; i++) s += 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789'[Math.floor(Math.random() * 32)]; return s; }
  SP.createBooking = function (o) {
    var sl = SP.state.slots[o.lotId] && SP.state.slots[o.lotId][o.slotId];
    if (!sl || sl.st !== 'free') return { error: 'Chỗ này vừa có người đặt. Hãy chọn chỗ khác.' };
    var pr = SP.price(o.vehicle, o.hours, o.startMs, o.lotId), id = 'SPK' + ('0000' + (SP.state.seq++)).slice(-4);
    var bk = { id: id, token: token(), lotId: o.lotId, slotId: o.slotId, vehicle: o.vehicle, plate: o.plate, startMs: o.startMs, hours: o.hours, price: pr.amount, method: o.method, paid: o.method !== 'cash', status: 'booked', createdAt: Date.now() };
    sl.st = 'res'; sl.bk = id; sl.plate = o.plate;
    SP.state.bookings.unshift(bk);
    SP.pushEvent('book', o.lotId, 'Đặt chỗ mới ' + o.slotId + ' (' + SP.VEH[o.vehicle].label.toLowerCase() + ')');
    SP.save(); return { booking: bk };
  };
  SP.booking = function (id) { return SP.state.bookings.filter(function (b) { return b.id === id; })[0]; };
  SP.checkIn = function (id) {
    var b = SP.booking(id); if (!b || b.status !== 'booked') return false;
    var sl = SP.state.slots[b.lotId][b.slotId]; b.status = 'active'; b.checkInAt = Date.now();
    sl.st = 'occ'; sl.since = b.checkInAt; sl.plate = b.plate; sl.bk = id;
    SP.pushEvent('in', b.lotId, 'Xe ' + b.plate + ' vào bãi (QR ' + b.id + ')'); SP.save(); return true;
  };
  SP.quoteCheckout = function (id) {
    var b = SP.booking(id), now = Date.now(), used = Math.max(1, Math.ceil((now - b.checkInAt) / 3600e3));
    var total = SP.price(b.vehicle, Math.max(b.hours, used), b.startMs, b.lotId).amount;
    var extra = b.paid ? Math.max(0, total - b.price) : total;
    return { usedMs: now - b.checkInAt, usedH: used, total: total, extra: extra, prepaid: b.paid ? b.price : 0 };
  };
  SP.completeCheckout = function (id) {
    var b = SP.booking(id); if (!b || b.status !== 'active') return false;
    var q = SP.quoteCheckout(id), sl = SP.state.slots[b.lotId][b.slotId];
    b.status = 'done'; b.checkOutAt = Date.now(); b.paid = true; b.finalPrice = q.total;
    sl.st = 'free'; delete sl.plate; delete sl.since; delete sl.bk;
    SP.pushEvent('out', b.lotId, 'Xe ' + b.plate + ' ra bãi, thu ' + SP.money(q.total)); SP.save(); return true;
  };
  SP.cancelBooking = function (id) {
    var b = SP.booking(id); if (!b || b.status !== 'booked') return false;
    var sl = SP.state.slots[b.lotId][b.slotId]; b.status = 'cancelled';
    sl.st = 'free'; delete sl.plate; delete sl.bk;
    SP.pushEvent('cancel', b.lotId, 'Hủy đặt chỗ ' + b.slotId); SP.save(); return true;
  };

  /* ---------- Số liệu báo cáo mẫu ----------
     Sinh heatmap/doanh thu lịch sử có hạt giống để giao diện demo ổn định giữa các lần tải.
  */
  function g(h, mu, s) { return Math.exp(-Math.pow((h - mu) / s, 2) / 2); }
  function prof(kind, dow, h, r) {
    var wk = dow < 5, v;
    if (kind === 'office') v = wk ? 22 + 62 * g(h, 9, 2.2) + 48 * g(h, 18, 2) : 14 + 30 * g(h, 13, 3.5);
    else if (kind === 'mall') v = wk ? 16 + 52 * g(h, 19, 2.6) + 20 * g(h, 12.5, 2) : 28 + 55 * g(h, 14, 3) + 45 * g(h, 19.5, 2.4);
    else v = 42 + 30 * g(h, 6.5, 2.5) + 36 * g(h, 20, 3);
    return Math.round(SP.clamp(v + (r() - 0.5) * 8, 4, 99));
  }
  function dayKey(d) { return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate()); }
  function buildHist() {
    var h = { heat: {}, daily: {} }, now = new Date(), AVG = { car: 52000, moto: 14000, ev: 78000 }, TURN = { car: 2.4, moto: 3.2, ev: 1.6 };
    SP.LOTS.forEach(function (lot, li) {
      var r = rng(777 + li * 31), m = [];
      for (var d = 0; d < 7; d++) { m.push([]); for (var hr = 0; hr < 24; hr++) m[d].push(prof(lot.kind, d, hr, r)); }
      h.heat[lot.id] = m;
      var cap = { car: 0, moto: 0, ev: 0 };
      lot.floors.forEach(function (f) { f.rows.forEach(function (row) { cap[row.type] += f.cols; }); });
      var arr = [];
      for (var k = 29; k >= 0; k--) {
        var dt = new Date(now.getFullYear(), now.getMonth(), now.getDate() - k), dow = (dt.getDay() + 6) % 7;
        var f = (lot.kind === 'mall' ? (dow >= 5 ? 1.25 : 0.9) : lot.kind === 'airport' ? 1.05 : (dow >= 5 ? 0.6 : 1.1)) * (0.88 + r() * 0.24) * (0.4 + lot.busy * 0.7);
        if (k === 0) f *= Math.max(0.05, (now.getHours() + now.getMinutes() / 60) / 24);
        var e = { d: dayKey(dt), trips: {}, rev: {} };
        ['car', 'moto', 'ev'].forEach(function (t) { var n = Math.round(cap[t] * TURN[t] * f); e.trips[t] = n; e.rev[t] = Math.round(n * AVG[t] * (0.94 + r() * 0.12) / 1000) * 1000; });
        arr.push(e);
      }
      h.daily[lot.id] = arr;
    });
    return h;
  }
  SP.hist = buildHist();
  SP.PAYSPLIT = { momo: 0.38, zalopay: 0.22, card: 0.18, cash: 0.22 };

  /* ---------- Sinh mã QR ----------
     Ưu tiên thư viện qrcode-generator; nếu không có thì hiển thị chuỗi mã thay thế.
  */
  SP.qrSvg = function (text) {
    if (!window.qrcode) return '<div class="qr-fallback mono" style="padding:12px;background:#fff;color:#000;word-break:break-all;font-size:12px">' + SP.esc(text) + '</div>';
    var q = window.qrcode(0, 'M'); q.addData(text); q.make();
    var n = q.getModuleCount(), m = 2, d = '';
    for (var r = 0; r < n; r++) for (var c = 0; c < n; c++) if (q.isDark(r, c)) d += 'M' + (c + m) + ' ' + (r + m) + 'h1v1h-1z';
    var t = n + m * 2;
    return '<svg viewBox="0 0 ' + t + ' ' + t + '" shape-rendering="crispEdges" role="img" aria-label="Mã QR"><rect width="' + t + '" height="' + t + '" fill="#fff"/><path d="' + d + '" fill="#000"/></svg>';
  };

  /* ---------- Tài khoản và đăng nhập (DEMO phía trình duyệt) ----------
     Hai tài khoản mẫu dưới đây chỉ để chạy thử giao diện. Mật khẩu nằm ngay trong mã nguồn
     nên KHÔNG an toàn. Khi có backend, thay SP.auth.login bằng lời gọi API (POST /auth/login)
     và lưu token thay cho session này. */
  var SESS_KEY = 'smartpark.session', USERS_KEY = 'smartpark.users.v1';
  SP.DEMO_ACCOUNTS = [
    { email: 'khach@smartpark.vn', pass: 'khach123', role: 'user', name: 'Nguyễn Minh Khách' },
    { email: 'admin@smartpark.vn', pass: 'admin123', role: 'admin', name: 'Quản trị viên' }
  ];
  function readStoredAccounts() {
    var raw;
    try { raw = localStorage.getItem(USERS_KEY); } catch (e) { return { error: 'Không thể truy cập bộ nhớ tài khoản trên trình duyệt.' }; }
    if (!raw) return { accounts: [] };
    try {
      var accounts = JSON.parse(raw);
      if (!Array.isArray(accounts) || accounts.some(function (a) {
        return !a || typeof a.email !== 'string' || typeof a.pass !== 'string' || typeof a.name !== 'string' || a.role !== 'user';
      })) return { error: 'Dữ liệu tài khoản đã lưu không hợp lệ. Hãy xóa dữ liệu trang web rồi thử lại.' };
      return { accounts: accounts };
    } catch (e) {
      return { error: 'Không đọc được dữ liệu tài khoản đã lưu. Hãy xóa dữ liệu trang web rồi thử lại.' };
    }
  }
  var stored = readStoredAccounts();
  SP.ACCOUNTS = SP.DEMO_ACCOUNTS.slice().concat(stored.accounts || []);
  SP.auth = {
    ROLE_LABEL: { user: 'Khách đỗ xe', admin: 'Ban quản lý' },
    HOME: { user: 'client/index.html', admin: 'admin/index.html' },
    /* Phiên đăng nhập lưu theo TỪNG TAB (sessionStorage): mở hai tab, một tab đăng nhập khách và một tab đăng nhập admin được cùng lúc. Đóng tab là mất phiên. */
    session: function () { try { var s = JSON.parse(sessionStorage.getItem(SESS_KEY)); return s && s.role ? s : null; } catch (e) { return null; } },
    login: function (email, pass) {
      var e = String(email || '').trim().toLowerCase(), a = SP.ACCOUNTS.filter(function (x) { return x.email === e && x.pass === pass; })[0];
      if (!a) return null;
      var s = { email: a.email, name: a.name, role: a.role, at: Date.now() };
      try { sessionStorage.setItem(SESS_KEY, JSON.stringify(s)); } catch (err) { }
      return s;
    },
    logout: function () { try { sessionStorage.removeItem(SESS_KEY); } catch (e) { } },
    /* Gọi ở đầu trang Client/Admin. root là đường dẫn về thư mục gốc, ví dụ '../'. */
    guard: function (role, root) {
      var s = SP.auth.session();
      if (!s) { location.replace(root + 'index.html'); return null; }
      if (s.role !== role) { location.replace(root + SP.auth.HOME[s.role]); return null; }
      return s;
    }
  };

  /* ---------- Giao diện sáng / tối ----------
     Ghi lựa chọn theme trên trình duyệt và cập nhật biểu tượng nút chuyển theme.
  */
  SP.initTheme = function (btn) {
    var root = document.documentElement, cur = null;
    try { cur = localStorage.getItem(THEME_KEY); } catch (e) { }
    if (cur) root.setAttribute('data-theme', cur);
    function isDark() { var t = root.getAttribute('data-theme'); return t ? t === 'dark' : window.matchMedia && matchMedia('(prefers-color-scheme: dark)').matches; }
    function paint() { if (btn) { btn.innerHTML = SP.icon(isDark() ? 'sun' : 'moon', 18); btn.setAttribute('aria-label', isDark() ? 'Chuyển sang giao diện sáng' : 'Chuyển sang giao diện tối'); } }
    if (btn) btn.addEventListener('click', function () { var n = isDark() ? 'light' : 'dark'; root.setAttribute('data-theme', n); try { localStorage.setItem(THEME_KEY, n); } catch (e) { } paint(); document.dispatchEvent(new Event('themechange')); });
    paint();
  };

  /* ---------- Thành phần sơ đồ chỗ đỗ dùng chung ----------
     Tạo HTML cho một ô, sơ đồ tầng và chú giải trạng thái; được dùng ở Client/Admin.
  */
  SP.slotHtml = function (id, s, o) {
    o = o || {};
    var num = id.split('-')[1].slice(1).replace(/^0/, ''), cls = 'slot ' + s.st + (o.sel ? ' sel' : '') + (o.dim ? ' dim' : '');
    var ic = s.st === 'occ' ? SP.icon(s.t === 'moto' ? 'moto' : s.t === 'ev' ? 'ev' : 'car', 15) : s.st === 'res' ? SP.icon('clock', 15) : s.st === 'blk' ? SP.icon('x', 15) : (s.t === 'ev' ? SP.icon('ev', 15) : s.t === 'moto' ? SP.icon('moto', 15) : '');
    var label = 'Chỗ ' + id + ', ' + ({ free: 'còn trống', occ: 'đang có xe', res: 'đã đặt', blk: 'bảo trì' })[s.st];
    var tag = o.button ? 'button type="button"' : 'div', end = o.button ? 'button' : 'div';
    return '<' + tag + ' class="' + cls + '" data-slot="' + id + '" aria-label="' + label + '"' + (o.button && o.disabled ? ' disabled' : '') + (o.title ? ' title="' + SP.esc(o.title) + '"' : '') + '>' + ic + '<span>' + num + '</span></' + end + '>';
  };
  SP.floorHtml = function (lot, floor, opts) {
    opts = opts || {};
    var sl = SP.state.slots[lot.id], h = '<div class="lanes"><div class="lane-note"><span>Lối vào</span><span>Lối ra</span></div>';
    floor.rows.forEach(function (row) {
      h += '<div class="prow"><div class="rl">' + row.r + '</div><div class="pcells" style="grid-template-columns:repeat(' + floor.cols + ',minmax(0,1fr))">';
      for (var c = 1; c <= floor.cols; c++) {
        var id = SP.slotId(floor, row.r, c), s = sl[id];
        h += SP.slotHtml(id, s, opts.cell ? opts.cell(id, s) : {});
      }
      h += '</div></div>';
    });
    return h + '</div>';
  };
  SP.legendHtml = function (sel) {
    return '<div class="legend"><span><i class="l-free"></i>Còn trống</span><span><i class="l-res"></i>Đã đặt</span><span><i class="l-occ"></i>Có xe</span><span><i class="l-blk"></i>Bảo trì</span>' + (sel ? '<span><i class="l-sel"></i>Bạn chọn</span>' : '') + '</div>';
  };
})();
