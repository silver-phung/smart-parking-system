/* SmartPark Client - logic giao diện khách đỗ xe */
(function () {
  'use strict';
  var S = window.SP, esc = S.esc;
  var sess = S.auth.guard('user', '../');   // chưa đăng nhập hoặc sai quyền thì tự chuyển trang
  if (!sess) return;
  var $ = function (s, r) { return (r || document).querySelector(s); };
  var $$ = function (s, r) { return Array.prototype.slice.call((r || document).querySelectorAll(s)); };
  var ui = { tab: 'find', veh: 'all', q: '', lotId: null, map: null, markers: {}, w: null, stream: null, scanTimer: 0, busy: false };
  var ME_KEY = 'smartpark.me.' + sess.email;
  /* Hồ sơ và biển số lưu riêng theo email; vé/giá/trạng thái chỗ dùng kho chung SP.state. */
  var me = (function () { try { var o = JSON.parse(localStorage.getItem(ME_KEY)); if (o) return o; } catch (e) { } return { name: sess.name, plates: ['51F-123.45'] }; })();
  function saveMe() { try { localStorage.setItem(ME_KEY, JSON.stringify(me)); } catch (e) { } }
  function norm(s) { return String(s).normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd').toLowerCase(); }
  var toastT;
  function toast(m) { var t = $('#toast'); t.textContent = m; t.hidden = false; clearTimeout(toastT); toastT = setTimeout(function () { t.hidden = true; }, 3200); }
  var PLATE_RE = /^[0-9]{2}[A-Z]{1,2}[0-9]?-?[0-9]{3,4}(\.[0-9]{2})?$/;
  var VEH_ICON = { car: 'car', moto: 'moto', ev: 'ev' };
  var STATUS_LABEL = { booked: 'Đã đặt', active: 'Đang gửi', done: 'Hoàn tất', cancelled: 'Đã hủy' };
  var STATUS_CLS = { booked: 'res', active: 'acc', done: 'free', cancelled: 'blk' };

  /* ---------- Khởi tạo giao diện ----------
     Bảo vệ trang theo vai trò, nạp thương hiệu, biểu tượng, giao diện màu và nút đăng xuất.
  */
  $('#brandName').textContent = S.CFG.brand;
  $$('[data-i]').forEach(function (el) { el.innerHTML = S.icon(el.getAttribute('data-i'), 20); });
  S.initTheme($('#themeBtn'));
  $('#logoutBtn').innerHTML = S.icon('out', 18) + 'Đăng xuất';
  /* Chạy lại hiệu ứng vào cho một phần tử (class "in" được định nghĩa trong client.css) */
  function replay(el) { if (!el) return; el.classList.remove('in'); void el.offsetWidth; el.classList.add('in'); }

  /* ---------- Chuyển tab ----------
     Chỉ dựng nội dung của tab đang mở; dừng camera khi rời tab Quét QR.
  */
  function switchTab(t) {
    var changed = ui.tab !== t;
    ui.tab = t;
    ['find', 'tickets', 'scan', 'me'].forEach(function (n) { $('#view-' + n).hidden = n !== t; });
    if (changed) replay($('#view-' + t));   // [ANIM] hiệu ứng mờ dần khi đổi tab
    $$('#tabs button').forEach(function (b) { if (b.getAttribute('data-tab') === t) b.setAttribute('aria-current', 'page'); else b.removeAttribute('aria-current'); });
    if (t !== 'scan') stopCam();
    if (t === 'find' && ui.map) setTimeout(function () { ui.map.invalidateSize(); }, 50);
    if (t === 'tickets') renderTickets();
    if (t === 'scan') renderScan();
    if (t === 'me') renderMe();
  }

  /* ---------- Tìm bãi và xem chi tiết ----------
     Lọc theo tên/khu vực/loại xe, hiển thị chỗ trống, giá và thông tin từng bãi.
  */
  function buildPanel() {
    $('#panel').innerHTML =
      '<div class="panel-head" id="ph"><div class="search">' + S.icon('search', 18) + '<input type="search" id="q" placeholder="Tìm bãi theo tên hoặc khu vực" aria-label="Tìm bãi"></div>' +
      '<div class="chips" id="vehChips"></div></div><div id="pb"></div>';
    var chips = [['all', 'Tất cả'], ['car', 'Ô tô'], ['moto', 'Xe máy'], ['ev', 'Ô tô điện']];
    $('#vehChips').innerHTML = chips.map(function (c) { return '<button type="button" class="chip" data-act="filterVeh" data-v="' + c[0] + '" aria-pressed="' + (ui.veh === c[0]) + '">' + c[1] + '</button>'; }).join('');
    $('#q').addEventListener('input', function () { ui.q = this.value; renderPanel(); });
  }
  function startPrice(lot, veh) { var p = S.pricingFor(lot.id)[veh === 'all' ? 'car' : veh]; return p.first; }
  function renderPanel() {
    var pb = $('#pb'), ph = $('#ph');
    $$('#vehChips .chip').forEach(function (b) { b.setAttribute('aria-pressed', b.getAttribute('data-v') === ui.veh); });
    if (ui.lotId) { ph.hidden = true; pb.innerHTML = detailHtml(S.lot(ui.lotId)); return; }
    ph.hidden = false;
    var q = norm(ui.q.trim()), list = S.LOTS.filter(function (l) { return !q || norm(l.name + ' ' + l.address).indexOf(q) > -1; });
    if (!list.length) { pb.innerHTML = '<div class="lotlist"><p class="muted" style="padding:20px;text-align:center">Không tìm thấy bãi phù hợp.</p></div>'; return; }
    pb.innerHTML = '<div class="lotlist">' + list.map(function (l) {
      var c = S.counts(l.id), st = S.status(c, ui.veh);
      /* [HINH-NEN] Ảnh nhỏ của bãi: đặt tệp assets/img/lots/<mã bãi>.jpg, ví dụ L1.jpg. Chưa có ảnh thì hiện nền màu. */
      return '<button type="button" class="lotcard" data-act="pickLot" data-id="' + l.id + '"><div class="row"><span class="thumb" style="background-image:url(../assets/img/lots/' + l.id + '.jpg),var(--cover-fallback)"></span><h3 class="grow">' + esc(l.name) + '</h3><span class="badge ' + st.k + '"><i class="dot"></i>' + st.label + '</span></div>' +
        '<div class="addr">' + S.icon('pin', 14) + ' ' + esc(l.address) + '</div>' +
        '<div class="meta"><div class="free-n">' + st.free + '<small>chỗ trống</small></div><div class="muted" style="text-align:right;font-size:13px">Từ <b class="mono" style="color:var(--ink)">' + S.money(startPrice(l, ui.veh)) + '</b>/giờ</div></div></button>';
    }).join('') + '</div>';
  }
  function detailHtml(l) {
    var c = S.counts(l.id), st = S.status(c, 'all'), pr = S.pricingFor(l.id), pk = S.state.pricing.peak;
    /* [HINH-NEN] Ảnh bìa lớn của bãi: cùng tệp assets/img/lots/<mã bãi>.jpg, khung ngang cao 120px (tỉ lệ khoảng 3:1). Chưa có ảnh thì hiện nền màu. */
    var h = '<div class="detail"><div class="row"><button class="btn sm ghost" type="button" data-act="backList">' + S.icon('back', 16) + 'Danh sách</button></div>' +
      '<div class="cover" role="img" aria-label="Ảnh bãi ' + esc(l.name) + '" style="background-image:url(../assets/img/lots/' + l.id + '.jpg),var(--cover-fallback)"></div>' +
      '<div class="stack" style="gap:6px"><div class="row wrap"><h2 class="grow">' + esc(l.name) + '</h2><span class="badge ' + st.k + '"><i class="dot"></i>' + st.label + '</span></div>' +
      '<div class="muted">' + S.icon('pin', 14) + ' ' + esc(l.address) + ' · ' + esc(l.hours) + '</div></div>' +
      '<div class="stats">' + ['car', 'moto', 'ev'].map(function (v) { return '<div class="stat"><span>' + S.icon(VEH_ICON[v], 15) + S.VEH[v].label + '</span><b>' + c.byType[v].free + '</b><span>/ ' + c.byType[v].total + ' chỗ</span></div>'; }).join('') + '</div>';
    h += '<div class="floorbar">' + l.floors.map(function (f) {
      var fc = S.counts(l.id, f.id), t = fc.total || 1;
      return '<div><div class="r"><span>' + esc(f.name) + '</span><span class="mono">' + fc.free + ' trống</span></div><div class="sbar" role="img" aria-label="' + esc(f.name) + ': ' + fc.free + ' chỗ trống"><i class="b-occ" style="width:' + fc.occ / t * 100 + '%"></i><i class="b-res" style="width:' + fc.res / t * 100 + '%"></i><i class="b-blk" style="width:' + fc.blk / t * 100 + '%"></i><i class="b-free" style="width:' + fc.free / t * 100 + '%"></i></div></div>';
    }).join('') + '</div>';
    h += '<table class="pricetab"><thead><tr><th>Bảng giá</th><th>Giờ đầu</th><th>Giờ tiếp</th><th>Tối đa/ngày</th></tr></thead><tbody>' +
      ['car', 'moto', 'ev'].map(function (v) { return '<tr><td>' + S.VEH[v].label + '</td><td>' + S.moneyShort(pr[v].first) + '</td><td>' + S.moneyShort(pr[v].next) + '</td><td>' + S.moneyShort(pr[v].cap) + '</td></tr>'; }).join('') + '</tbody></table>';
    if (pk.on) h += '<p class="hint">Giờ cao điểm ' + pk.from + ':00 - ' + pk.to + ':00 phụ thu ' + pk.pct + '%.</p>';
    h += '<button class="btn primary block" type="button" data-act="openWizard" data-id="' + l.id + '"' + (c.free ? '' : ' disabled') + '>' + S.icon('ticket', 18) + (c.free ? 'Xem sơ đồ và đặt chỗ' : 'Bãi đã hết chỗ') + '</button>' +
      '<a class="btn block" target="_blank" rel="noopener" href="https://www.google.com/maps/dir/?api=1&destination=' + l.lat + ',' + l.lng + '">' + S.icon('nav', 18) + 'Chỉ đường</a></div>';
    return h;
  }

  /* ---------- Bản đồ ----------
     Hiển thị marker Leaflet; nếu thư viện/bản đồ lỗi, danh sách bãi vẫn dùng được.
  */
  function initMap() {
    if (!window.L) { $('#map').innerHTML = '<div class="map-fallback"><div><b>Không tải được bản đồ.</b><br>Kiểm tra kết nối mạng. Bạn vẫn chọn bãi được từ danh sách.</div></div>'; return; }
    ui.map = L.map('map', { zoomControl: false }).setView([10.7886, 106.688], 12);
    L.control.zoom({ position: 'topright' }).addTo(ui.map);
    L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 19, attribution: '&copy; OpenStreetMap' }).addTo(ui.map);
    S.LOTS.forEach(function (l) {
      var m = L.marker([l.lat, l.lng], { icon: mkIcon(l), title: l.name, keyboard: true }).addTo(ui.map);
      m.on('click', function () { pickLot(l.id); });
      ui.markers[l.id] = m;
    });
    fitAll();
  }
  function fitAll() {
    if (!ui.map) return;
    var small = window.innerWidth <= 860;
    ui.map.fitBounds(S.LOTS.map(function (l) { return [l.lat, l.lng]; }), { paddingTopLeft: [40, 40], paddingBottomRight: [40, small ? 280 : 40], maxZoom: 14 });
  }
  function mkIcon(l) {
    var st = S.status(S.counts(l.id), ui.veh);
    return L.divIcon({ className: 'pin ' + st.k + (ui.lotId === l.id ? ' sel' : ''), html: '<span>' + st.free + '</span>', iconSize: [44, 34], iconAnchor: [22, 38] });
  }
  function renderMarkers() { if (!ui.map) return; S.LOTS.forEach(function (l) { ui.markers[l.id].setIcon(mkIcon(l)); }); }
  function pickLot(id) {
    ui.lotId = id; renderPanel(); renderMarkers();
    var l = S.lot(id); if (ui.map) ui.map.flyTo([l.lat, l.lng], 15, { duration: 0.6 });
    $('#panel').scrollTop = 0;
  }

  /* ---------- Modal dùng chung ----------
     Mở/đóng cửa sổ đặt chỗ, xem vé và kết quả check-in/check-out.
  */
  function openModal(html) {
    var was = $('#modal').hidden;
    $('#sheet').innerHTML = html; $('#modal').hidden = false; document.body.style.overflow = 'hidden';
    if (was) replay($('#sheet'));   // [ANIM] hiệu ứng mở cửa sổ đặt chỗ, chỉ chạy lần đầu mở (không chạy lại khi sang bước khác)
    var b = $('#sheet .sh-body'); if (b) b.scrollTop = 0;
  }
  function closeModal() { $('#modal').hidden = true; document.body.style.overflow = ''; ui.w = null; ui.busy = false; }
  function head(title, sub, back) {
    return '<div class="sh-head">' + (back ? '<button class="btn icon ghost" type="button" data-act="wizBack" aria-label="Quay lại">' + S.icon('back') + '</button>' : '') +
      '<h2 id="modalTitle">' + title + (sub ? '<div class="muted" style="font-family:var(--f-body);font-size:13px;font-weight:500;margin-top:2px">' + sub + '</div>' : '') + '</h2>' +
      '<button class="btn icon ghost" type="button" data-act="closeModal" aria-label="Đóng">' + S.icon('x') + '</button></div>';
  }
  function stepBar(n) { return '<div class="steps" aria-label="Bước ' + n + ' trên 4">' + [1, 2, 3, 4].map(function (i) { return '<i class="' + (i <= n ? 'on' : '') + '"></i>'; }).join('') + '</div>'; }

  /* ---------- Luồng đặt chỗ 4 bước ----------
     Chọn ô → nhập biển số/thời gian → chọn cách thanh toán → tạo vé QR.
  */
  function openWizard(lotId) {
    var l = S.lot(lotId), veh = ui.veh === 'all' ? 'car' : ui.veh;
    ui.w = { lotId: lotId, veh: veh, floor: firstFloor(l, veh), slotId: null, plate: me.plates[0] || '', startOff: 0, hours: 2, method: 'momo', step: 1, bkId: null };
    renderWizard();
  }
  function firstFloor(l, veh) {
    var sl = S.state.slots[l.id];
    for (var i = 0; i < l.floors.length; i++) { var f = l.floors[i]; for (var k in sl) if (sl[k].f === f.id && sl[k].t === veh && sl[k].st === 'free') return f.id; }
    return l.floors[0].id;
  }
  function wStart() { return Date.now() + ui.w.startOff * 60000; }
  function wPrice() { var w = ui.w; return S.price(w.veh, w.hours, wStart(), w.lotId); }
  function renderWizard() {
    var w = ui.w; if (!w) return;
    if (w.step === 1) return openModal(step1());
    if (w.step === 2) return openModal(step2());
    if (w.step === 3) return openModal(step3());
    if (w.step === 4) return openModal(step4());
  }
  function step1() {
    var w = ui.w, l = S.lot(w.lotId), f = S.floorOf(l, w.floor);
    if (w.slotId) { var cur = S.state.slots[w.lotId][w.slotId]; if (!cur || cur.st !== 'free') { w.slotId = null; toast('Chỗ bạn chọn vừa có người đặt. Hãy chọn chỗ khác.'); } }
    var h = head('Chọn chỗ đỗ', esc(l.name)) + '<div class="sh-body">' + stepBar(1) +
      '<div class="seg" role="group" aria-label="Loại xe">' + ['car', 'moto', 'ev'].map(function (v) { return '<button type="button" data-act="wizVeh" data-v="' + v + '" aria-pressed="' + (w.veh === v) + '">' + S.icon(VEH_ICON[v], 16) + S.VEH[v].label + '</button>'; }).join('') + '</div>';
    if (l.floors.length > 1) h += '<div class="chips">' + l.floors.map(function (x) { return '<button type="button" class="chip" data-act="wizFloor" data-f="' + x.id + '" aria-pressed="' + (w.floor === x.id) + '">' + esc(x.name) + '</button>'; }).join('') + '</div>';
    h += S.legendHtml(true);
    h += S.floorHtml(l, f, { cell: function (id, s) { var ok = s.st === 'free' && s.t === w.veh; return { button: true, disabled: !ok, dim: s.st === 'free' && !ok, sel: id === w.slotId, title: id }; } });
    var c = S.counts(w.lotId, w.floor).byType[w.veh];
    h += '<p class="hint">' + c.free + ' chỗ ' + S.VEH[w.veh].label.toLowerCase() + ' còn trống trong ' + esc(f.name) + '. Chỗ khác loại xe được làm mờ.</p></div>' +
      '<div class="sh-foot"><div class="sum">' + (w.slotId ? '<small class="muted">Đã chọn</small><b>' + w.slotId + '</b>' : '<small class="muted">Chưa chọn chỗ</small>') + '</div><button class="btn primary" type="button" data-act="wizNext"' + (w.slotId ? '' : ' disabled') + '>Tiếp tục</button></div>';
    return h;
  }
  function plateOk() { return PLATE_RE.test(ui.w.plate.replace(/\s/g, '')); }
  function step2() {
    var w = ui.w, l = S.lot(w.lotId), pr = wPrice(), st = wStart();
    var h = head('Thông tin đặt chỗ', esc(l.name) + ' · chỗ ' + w.slotId, true) + '<div class="sh-body">' + stepBar(2) +
      '<div class="field"><label for="plateIn">Biển số xe</label><input class="plate-in" type="text" id="plateIn" value="' + esc(w.plate) + '" placeholder="51F-123.45" maxlength="14" autocomplete="off" autocapitalize="characters"><div class="err" id="plateErr"></div></div>';
    if (me.plates.length) h += '<div class="chips">' + me.plates.map(function (p) { return '<button type="button" class="chip" data-act="usePlate" data-p="' + esc(p) + '">' + esc(p) + '</button>'; }).join('') + '</div>';
    h += '<div class="field"><span class="lab">Thời gian đến</span><div class="chips">' + [[0, 'Ngay bây giờ'], [30, 'Sau 30 phút'], [60, 'Sau 1 giờ'], [120, 'Sau 2 giờ']].map(function (o) { return '<button type="button" class="chip" data-act="wizStart" data-m="' + o[0] + '" aria-pressed="' + (w.startOff === o[0]) + '">' + o[1] + '</button>'; }).join('') + '</div><p class="hint">Giữ chỗ tối đa ' + S.CFG.graceMin + ' phút sau giờ đến.</p></div>' +
      '<div class="field"><span class="lab">Thời lượng gửi</span><div class="stepper"><button class="btn icon" type="button" data-act="wizHours" data-d="-1" aria-label="Giảm 1 giờ"' + (w.hours <= 1 ? ' disabled' : '') + '>−</button><b>' + w.hours + ' giờ</b><button class="btn icon" type="button" data-act="wizHours" data-d="1" aria-label="Tăng 1 giờ"' + (w.hours >= 24 ? ' disabled' : '') + '>+</button></div></div>' +
      '<div class="summary"><div><span>Vào lúc</span><b class="mono">' + S.hm(st) + '</b></div><div><span>Ra dự kiến</span><b class="mono">' + S.hm(st + w.hours * 3600e3) + '</b></div>' + (pr.peak ? '<div><span>Phụ thu giờ cao điểm</span><b class="mono">+' + S.state.pricing.peak.pct + '%</b></div>' : '') + '<div class="tot"><span>Tạm tính</span><b class="mono">' + S.money(pr.amount) + '</b></div></div></div>' +
      '<div class="sh-foot"><div class="sum"><small class="muted">Tạm tính</small><b>' + S.money(pr.amount) + '</b></div><button class="btn primary" type="button" id="step2Next" data-act="wizNext"' + (plateOk() ? '' : ' disabled') + '>Tiếp tục</button></div>';
    return h;
  }
  function step3() {
    var w = ui.w, l = S.lot(w.lotId), pr = wPrice(), st = wStart(), icons = { momo: 'wallet', zalopay: 'wallet', card: 'card', cash: 'tag' };
    var h = head('Thanh toán', esc(l.name), true) + '<div class="sh-body">' + stepBar(3) +
      '<div class="summary"><div><span>Chỗ đỗ</span><b class="mono">' + w.slotId + '</b></div><div><span>Biển số</span><b class="mono">' + esc(w.plate) + '</b></div><div><span>Thời gian</span><b class="mono">' + S.hm(st) + ' - ' + S.hm(st + w.hours * 3600e3) + '</b></div><div class="tot"><span>Tổng cộng</span><b class="mono">' + S.money(pr.amount) + '</b></div></div>' +
      '<div class="stack" style="gap:8px">' + Object.keys(S.PAY).map(function (k) { return '<button type="button" class="paycard" data-act="wizMethod" data-m="' + k + '" aria-pressed="' + (w.method === k) + '"><span class="pi">' + S.icon(icons[k], 20) + '</span><span class="grow"><b>' + S.PAY[k] + '</b><div class="hint">' + (k === 'cash' ? 'Thanh toán khi ra cổng' : 'Thanh toán ngay, nhận vé QR liền') + '</div></span></button>'; }).join('') + '</div>' +
      '<p class="hint">Đây là cổng thanh toán giả lập, chưa trừ tiền thật.</p></div>' +
      '<div class="sh-foot"><div class="sum"><small class="muted">Cần thanh toán</small><b>' + S.money(w.method === 'cash' ? 0 : pr.amount) + '</b></div><button class="btn primary" type="button" id="payBtn" data-act="wizPay">' + (w.method === 'cash' ? 'Đặt chỗ' : 'Thanh toán') + '</button></div>';
    return h;
  }
  function ticketHtml(b) {
    var l = S.lot(b.lotId);
    return '<div class="ticket"><div class="ticket-top"><small>' + esc(l.name) + '</small><h3 class="mono">Chỗ ' + b.slotId + '</h3></div>' +
      '<div class="ticket-qr"><div class="qrbox">' + S.qrSvg('SPK|' + b.id + '|' + b.token) + '</div></div>' +
      '<div class="ticket-info"><div><small>Biển số</small><b>' + esc(b.plate) + '</b></div><div><small>Mã vé</small><b>' + b.id + '</b></div>' +
      '<div><small>Vào lúc</small><b>' + S.dhm(b.startMs) + '</b></div><div><small>Thời lượng</small><b>' + b.hours + ' giờ</b></div>' +
      '<div><small>Thanh toán</small><b>' + (b.paid ? S.PAY[b.method] : 'Tiền mặt khi ra') + '</b></div><div><small>Trạng thái</small><span class="badge ' + STATUS_CLS[b.status] + '">' + STATUS_LABEL[b.status] + '</span></div></div></div>';
  }
  function step4() {
    var b = S.booking(ui.w.bkId);
    return head('Vé của bạn') + '<div class="sh-body"><div class="success"><div class="tick">' + S.icon('check', 28) + '</div><h3>Đặt chỗ thành công</h3><p class="muted">Đưa mã QR này cho cổng vào để check-in.</p></div>' + ticketHtml(b) + '</div>' +
      '<div class="sh-foot"><button class="btn" type="button" data-act="closeModal">Đóng</button><button class="btn primary grow" type="button" data-act="toTickets">Xem vé của tôi</button></div>';
  }

  /* ---------- Vé của tôi ----------
     Liệt kê vé đang đặt/đang gửi; cho phép xem QR, hủy vé hoặc check-out.
  */
  function renderTickets() {
    var list = S.state.bookings, el = $('#ticketList');
    var open = list.filter(function (b) { return b.status === 'booked' || b.status === 'active'; }).length;
    var cnt = $('#ticketCount'); cnt.hidden = !open; cnt.textContent = open;
    if (ui.tab !== 'tickets') return;
    if (!list.length) { el.innerHTML = '<div class="card" style="text-align:center;padding:32px"><p class="muted">Bạn chưa có vé nào.</p><button class="btn primary" style="margin-top:12px" type="button" data-act="tab" data-tab="find">Tìm bãi đỗ</button></div>'; return; }
    el.innerHTML = list.map(function (b) {
      var l = S.lot(b.lotId);
      return '<div class="card tk"><div class="tk-head"><b class="grow" style="font-size:16px">' + esc(l.name) + '</b><span class="badge ' + STATUS_CLS[b.status] + '">' + STATUS_LABEL[b.status] + '</span></div>' +
        '<div class="row wrap muted" style="gap:4px 16px"><span class="mono">' + b.slotId + '</span><span class="mono">' + esc(b.plate) + '</span><span>' + S.dhm(b.startMs) + ' · ' + b.hours + ' giờ</span><span class="mono">' + S.money(b.finalPrice || b.price) + '</span></div>' +
        '<div class="row wrap">' + (b.status === 'booked' || b.status === 'active' ? '<button class="btn sm primary" type="button" data-act="showTicket" data-id="' + b.id + '">' + S.icon('qr', 16) + 'Hiện QR</button>' : '') +
        (b.status === 'booked' ? '<button class="btn sm danger" type="button" data-act="cancelBk" data-id="' + b.id + '">Hủy đặt chỗ</button>' : '') +
        (b.status === 'active' ? '<button class="btn sm" type="button" data-act="doOut" data-id="' + b.id + '">' + S.icon('out', 16) + 'Check-out</button>' : '') + '</div></div>';
    }).join('');
  }

  /* ---------- Quét QR và xử lý cổng ----------
     Hỗ trợ camera khi trình duyệt cho phép, nhập mã và giả lập cổng để demo.
  */
  function renderScan() {
    if (ui.tab !== 'scan') return;
    var bs = S.state.bookings.filter(function (b) { return b.status === 'booked' || b.status === 'active'; });
    $('#simBox').innerHTML = bs.length ? '<div class="stack" style="gap:8px">' + bs.map(function (b) {
      return '<div class="row wrap"><div class="grow"><b>' + esc(S.lot(b.lotId).name) + '</b><div class="hint mono">' + b.slotId + ' · ' + esc(b.plate) + '</div></div>' +
        (b.status === 'booked' ? '<button class="btn primary sm" type="button" data-act="simGate" data-g="SPGATE|' + b.lotId + '|IN">Quét cổng VÀO</button>' : '<button class="btn primary sm" type="button" data-act="simGate" data-g="SPGATE|' + b.lotId + '|OUT">Quét cổng RA</button>') + '</div>';
    }).join('') + '</div>' : '<p class="muted">Bạn chưa có vé đang hiệu lực. Hãy đặt chỗ ở tab Tìm bãi trước.</p>';
    var lid = bs.length ? bs[0].lotId : 'L1', l = S.lot(lid);
    $('#gateQr').innerHTML = '<h3>Mã QR cổng (để thử bằng camera)</h3><p class="hint">Mở trang này trên màn hình khác rồi quét bằng điện thoại. Bãi: ' + esc(l.name) + '.</p><div class="gateqr">' +
      ['IN', 'OUT'].map(function (d) { return '<figure><div class="qrbox">' + S.qrSvg('SPGATE|' + lid + '|' + d) + '</div><figcaption class="mono">' + (d === 'IN' ? 'Cổng vào' : 'Cổng ra') + '</figcaption></figure>'; }).join('') + '</div>';
  }
  function startCam() {
    if (ui.stream) return;
    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) { toast('Trình duyệt này không hỗ trợ camera. Hãy dùng phần giả lập.'); return; }
    var canDetect = 'BarcodeDetector' in window;
    navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' } }).then(function (st) {
      ui.stream = st; var v = $('#video'); v.srcObject = st; v.play(); $('#camEmpty').hidden = true;
      if (!canDetect) { toast('Camera đã bật, nhưng trình duyệt chưa tự đọc được QR. Hãy nhập mã hoặc dùng giả lập.'); return; }
      var det = new window.BarcodeDetector({ formats: ['qr_code'] });
      ui.scanTimer = setInterval(function () {
        if (!v.videoWidth) return;
        det.detect(v).then(function (r) { if (r.length) { var val = r[0].rawValue; stopCam(); processGate(val); } }).catch(function () { });
      }, 450);
    }).catch(function () { toast('Không mở được camera. Hãy cho phép quyền truy cập camera.'); });
  }
  function stopCam() {
    clearInterval(ui.scanTimer);
    if (ui.stream) { ui.stream.getTracks().forEach(function (t) { t.stop(); }); ui.stream = null; }
    var v = $('#video'); if (v) v.srcObject = null; var e = $('#camEmpty'); if (e) e.hidden = false;
  }
  function processGate(raw) {
    var m = /^SPGATE\|([A-Za-z0-9]+)\|(IN|OUT)$/i.exec(String(raw).trim());
    if (!m) { toast('Mã QR không hợp lệ. Hãy quét mã dán ở cổng bãi xe.'); return; }
    var lotId = m[1].toUpperCase(), dir = m[2].toUpperCase();
    if (!S.lot(lotId)) { toast('Không tìm thấy bãi xe của mã này.'); return; }
    if (dir === 'IN') {
      var b = S.state.bookings.filter(function (x) { return x.status === 'booked' && x.lotId === lotId; })[0];
      if (!b) { toast('Bạn chưa có vé đã đặt cho bãi này.'); return; }
      S.checkIn(b.id);
      openModal(head('Check-in thành công') + '<div class="sh-body"><div class="success"><div class="tick">' + S.icon('check', 28) + '</div><h3>Cổng vào đã mở</h3><p class="muted">Xe ' + esc(b.plate) + ' · chỗ <b class="mono">' + b.slotId + '</b></p><p class="muted">Vào lúc ' + S.hm(Date.now()) + '. Chúc bạn đỗ xe thuận lợi.</p></div></div><div class="sh-foot"><button class="btn primary grow" type="button" data-act="closeModal">Xong</button></div>');
    } else {
      var a = S.state.bookings.filter(function (x) { return x.status === 'active' && x.lotId === lotId; })[0];
      if (!a) { toast('Không có xe nào của bạn đang gửi tại bãi này.'); return; }
      openCheckout(a.id);
    }
  }
  function openCheckout(id) {
    var b = S.booking(id), q = S.quoteCheckout(id);
    openModal(head('Check-out', esc(S.lot(b.lotId).name)) + '<div class="sh-body"><div class="summary"><div><span>Xe</span><b class="mono">' + esc(b.plate) + '</b></div><div><span>Chỗ đỗ</span><b class="mono">' + b.slotId + '</b></div><div><span>Thời gian gửi</span><b>' + S.dur(q.usedMs) + '</b></div><div><span>Đã thanh toán trước</span><b class="mono">' + S.money(q.prepaid) + '</b></div><div class="tot"><span>Phát sinh cần thanh toán</span><b class="mono">' + S.money(q.extra) + '</b></div></div>' +
      (q.extra > 0 ? '<p class="hint">Phí phát sinh do quá giờ đặt hoặc thanh toán tiền mặt. Thanh toán giả lập, chưa trừ tiền thật.</p>' : '<p class="hint">Không có phí phát sinh. Bạn có thể ra ngay.</p>') + '</div>' +
      '<div class="sh-foot"><button class="btn" type="button" data-act="closeModal">Để sau</button><button class="btn primary grow" type="button" data-act="confirmOut" data-id="' + id + '">' + (q.extra > 0 ? 'Thanh toán ' + S.money(q.extra) + ' và ra cổng' : 'Mở cổng ra') + '</button></div>');
  }

  /* ---------- Tài khoản khách ----------
     Sửa tên hiển thị và quản lý danh sách biển số lưu trên trình duyệt.
  */
  function renderMe() {
    $('#meName').value = me.name;
    $('#meAvatar').textContent = (me.name.trim().charAt(0) || '?').toUpperCase();
    $('#meEmailName').textContent = me.name; $('#meEmail').textContent = sess.email + ' · ' + S.auth.ROLE_LABEL[sess.role];
    $('#plateList').innerHTML = me.plates.length ? me.plates.map(function (p, i) { return '<span class="chip on mono" style="display:inline-flex;gap:8px;align-items:center">' + esc(p) + '<button type="button" data-act="delPlate" data-i="' + i + '" aria-label="Xóa biển số ' + esc(p) + '" style="border:0;background:transparent;color:inherit;padding:0;line-height:1">' + S.icon('x', 14) + '</button></span>'; }).join('') : '<span class="hint">Chưa lưu biển số nào.</span>';
  }

  /* ---------- Sự kiện giao diện ----------
     Điều phối các nút qua data-act; các bước nghiệp vụ được xử lý ở SP trong core.js.
  */
  var acts = {
    logout: function () { if (!window.confirm('Bạn có chắc chắn muốn đăng xuất?')) return; S.auth.logout(); location.href = '../index.html'; },
    tab: function (t) { switchTab(t.getAttribute('data-tab')); },
    filterVeh: function (t) { ui.veh = t.getAttribute('data-v'); renderPanel(); renderMarkers(); },
    pickLot: function (t) { pickLot(t.getAttribute('data-id')); },
    backList: function () { ui.lotId = null; renderPanel(); renderMarkers(); fitAll(); },
    openWizard: function (t) { openWizard(t.getAttribute('data-id')); },
    closeModal: closeModal,
    wizVeh: function (t) { var w = ui.w; w.veh = t.getAttribute('data-v'); w.slotId = null; w.floor = firstFloor(S.lot(w.lotId), w.veh); renderWizard(); },
    wizFloor: function (t) { ui.w.floor = t.getAttribute('data-f'); renderWizard(); },
    wizNext: function () { var w = ui.w; if (w.step === 1 && !w.slotId) return; if (w.step === 2 && !plateOk()) return; w.step++; renderWizard(); },
    wizBack: function () { if (ui.w.step > 1) { ui.w.step--; renderWizard(); } },
    usePlate: function (t) { ui.w.plate = t.getAttribute('data-p'); renderWizard(); },
    wizStart: function (t) { ui.w.startOff = +t.getAttribute('data-m'); renderWizard(); },
    wizHours: function (t) { ui.w.hours = S.clamp(ui.w.hours + +t.getAttribute('data-d'), 1, 24); renderWizard(); },
    wizMethod: function (t) { ui.w.method = t.getAttribute('data-m'); renderWizard(); },
    wizPay: function (t) {
      if (ui.busy) return; ui.busy = true; t.disabled = true; t.textContent = 'Đang xử lý...';
      var w = ui.w;
      setTimeout(function () {
        if (!ui.w) { ui.busy = false; return; }
        var r = S.createBooking({ lotId: w.lotId, slotId: w.slotId, vehicle: w.veh, plate: w.plate.replace(/\s/g, '').toUpperCase(), startMs: wStart(), hours: w.hours, method: w.method });
        ui.busy = false;
        if (r.error) { toast(r.error); w.slotId = null; w.step = 1; renderWizard(); return; }
        if (me.plates.indexOf(r.booking.plate) < 0) { me.plates.unshift(r.booking.plate); me.plates = me.plates.slice(0, 5); saveMe(); }
        w.bkId = r.booking.id; w.step = 4; renderWizard();
      }, 1100);
    },
    toTickets: function () { closeModal(); switchTab('tickets'); },
    showTicket: function (t) { var b = S.booking(t.getAttribute('data-id')); openModal(head('Vé QR của bạn', esc(S.lot(b.lotId).name)) + '<div class="sh-body">' + ticketHtml(b) + '<p class="hint" style="text-align:center">' + (b.status === 'booked' ? 'Quét mã này ở cổng vào để check-in.' : 'Quét mã này ở cổng ra để check-out.') + '</p></div><div class="sh-foot"><button class="btn primary grow" type="button" data-act="closeModal">Đóng</button></div>'); },
    cancelBk: function (t) { if (S.cancelBooking(t.getAttribute('data-id'))) toast('Đã hủy đặt chỗ. Chỗ đỗ được trả về trạng thái trống.'); },
    doOut: function (t) { openCheckout(t.getAttribute('data-id')); },
    confirmOut: function (t) {
      var id = t.getAttribute('data-id'), b = S.booking(id), q = S.quoteCheckout(id);
      if (!S.completeCheckout(id)) { toast('Không thể check-out vé này.'); return; }
      openModal(head('Check-out thành công') + '<div class="sh-body"><div class="success"><div class="tick">' + S.icon('check', 28) + '</div><h3>Cổng ra đã mở</h3><p class="muted">Xe ' + esc(b.plate) + ' · tổng chi phí <b class="mono">' + S.money(q.total) + '</b></p><p class="muted">Cảm ơn bạn đã dùng ' + esc(S.CFG.brand) + '.</p></div></div><div class="sh-foot"><button class="btn primary grow" type="button" data-act="closeModal">Xong</button></div>');
    },
    camOn: startCam, camOff: stopCam,
    simGate: function (t) { processGate(t.getAttribute('data-g')); },
    gateSubmit: function () { var i = $('#gateInput'); processGate(i.value); i.value = ''; },
    addPlate: function () {
      var i = $('#mePlate'), v = i.value.trim().toUpperCase().replace(/\s/g, '');
      if (!PLATE_RE.test(v)) { toast('Biển số chưa đúng định dạng, ví dụ 51F-123.45.'); return; }
      if (me.plates.indexOf(v) < 0) me.plates.push(v); saveMe(); i.value = ''; renderMe();
    },
    delPlate: function (t) { me.plates.splice(+t.getAttribute('data-i'), 1); saveMe(); renderMe(); },
    resetDemo: function () { S.reset(); ui.lotId = null; renderPanel(); renderMarkers(); renderTickets(); toast('Đã đặt lại dữ liệu demo.'); }
  };
  document.addEventListener('click', function (e) {
    var slot = e.target.closest && e.target.closest('button.slot[data-slot]');
    if (slot && ui.w && ui.w.step === 1 && !slot.disabled) { ui.w.slotId = slot.getAttribute('data-slot'); renderWizard(); return; }
    var t = e.target.closest && e.target.closest('[data-act]');
    if (!t) { if (e.target.id === 'modal') closeModal(); return; }
    var fn = acts[t.getAttribute('data-act')]; if (fn) fn(t, e);
  });
  document.addEventListener('input', function (e) {
    if (e.target.id === 'plateIn' && ui.w) {
      var v = e.target.value.toUpperCase(); e.target.value = v; ui.w.plate = v;
      var ok = plateOk(), b = $('#step2Next'); if (b) b.disabled = !ok;
      $('#plateErr').textContent = v && !ok ? 'Biển số chưa đúng định dạng, ví dụ 51F-123.45.' : '';
    }
    if (e.target.id === 'meName') { me.name = e.target.value; saveMe(); }
  });
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && !$('#modal').hidden) closeModal();
    if (e.key === 'Enter' && e.target.id === 'gateInput') acts.gateSubmit();
  });

  /* ---------- Đồng bộ dữ liệu ----------
     Cập nhật bản đồ, danh sách vé và QR khi kho chung đổi ở tab hiện tại hoặc Admin.
  */
  S.subscribe(function () {
    renderMarkers(); renderPanel(); renderTickets(); renderScan();
    if (ui.w && ui.w.step === 1 && !$('#modal').hidden) { var sb = $('#sheet .sh-body'), top = sb ? sb.scrollTop : 0; renderWizard(); var nb = $('#sheet .sh-body'); if (nb) nb.scrollTop = top; }
  });

  /* ---------- Khởi chạy Client ----------
     Vẽ tab tìm bãi mặc định, khởi tạo bản đồ và khôi phục tab từ URL nếu có.
  */
  buildPanel(); renderPanel(); initMap(); renderTickets();
  var h = (location.hash || '').replace('#', ''); if (['tickets', 'scan', 'me'].indexOf(h) > -1) switchTab(h);
  window.addEventListener('resize', function () { if (ui.map) ui.map.invalidateSize(); });
})();
