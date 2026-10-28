/* SmartPark Admin - logic cổng quản trị */
(function () {
  'use strict';
  var S = window.SP, esc = S.esc;
  var sess = S.auth.guard('admin', '../');   // chưa đăng nhập hoặc không phải admin thì tự chuyển trang
  if (!sess) return;
  var $ = function (s, r) { return (r || document).querySelector(s); };
  var PAGES = { dash: ['Tổng quan', 'grid'], live: ['Sơ đồ trực tiếp', 'map'], view3d: ['Sơ đồ 3D', 'map'], heat: ['Heatmap', 'flame'], rev: ['Doanh thu', 'chart'], price: ['Giá vé', 'tag'], alerts: ['Cảnh báo', 'bell'] };
  var DOW = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'];
  var ui = { page: 'dash', lot: 'all', liveLot: 'L1', liveFloor: null, sel: null, heatMetric: 'occ', revDays: 14, priceScope: 'def', afStatus: 'open', afSev: 'all', sim: true, simT: 0 };
  var toastT;
  function toast(m) { var t = $('#toast'); t.textContent = m; t.hidden = false; clearTimeout(toastT); toastT = setTimeout(function () { t.hidden = true; }, 3000); }
  function lotsSel() { return ui.lot === 'all' ? S.LOTS : [S.lot(ui.lot)]; }
  function lotName(id) { return S.lot(id).name; }
  function sum(a, f) { return a.reduce(function (s, x) { return s + f(x); }, 0); }
  function pct(a, b) { return b ? Math.round(a / b * 100) : 0; }
  function todayDow() { return (new Date().getDay() + 6) % 7; }

  /* ---------- Tổng hợp số liệu ----------
     Đọc trạng thái và lịch sử từ SP; gộp theo bộ lọc bãi xe đang chọn.
     Dữ liệu lịch sử hiện là mẫu, doanh thu hôm nay được cộng thêm từ vé thực tế trong localStorage.
  */
  function agg(lots) {
    var o = { total: 0, free: 0, occ: 0, res: 0, blk: 0 };
    lots.forEach(function (l) { var c = S.counts(l.id); ['total', 'free', 'occ', 'res', 'blk'].forEach(function (k) { o[k] += c[k]; }); });
    o.fill = pct(o.occ + o.res, o.total - o.blk);
    return o;
  }
  function liveRev(lots) {
    var ids = lots.map(function (l) { return l.id; }), d0 = new Date(); d0.setHours(0, 0, 0, 0);
    var o = { car: 0, moto: 0, ev: 0 };
    S.state.bookings.forEach(function (b) { if (b.paid && b.createdAt >= d0.getTime() && ids.indexOf(b.lotId) > -1 && b.status !== 'cancelled') o[b.vehicle] += b.finalPrice || b.price; });
    return o;
  }
  function dailyAgg(lots) {
    var out = [];
    for (var i = 0; i < 30; i++) {
      var e = { d: lots[0] ? S.hist.daily[lots[0].id][i].d : '', car: 0, moto: 0, ev: 0, trips: 0 };
      lots.forEach(function (l) { var x = S.hist.daily[l.id][i]; ['car', 'moto', 'ev'].forEach(function (t) { e[t] += x.rev[t]; e.trips += x.trips[t]; }); });
      out.push(e);
    }
    var lr = liveRev(lots), last = out[29]; ['car', 'moto', 'ev'].forEach(function (t) { last[t] += lr[t]; });
    return out;
  }
  function heatAvg(lots, d, h) {
    var w = sum(lots, function (l) { return S.counts(l.id).total; });
    return sum(lots, function (l) { return S.hist.heat[l.id][d][h] * S.counts(l.id).total; }) / (w || 1);
  }

  /* ---------- Biểu đồ SVG ----------
     Tạo biểu đồ bằng SVG thuần, không cần thư viện vẽ bên ngoài.
  */
  function lineChart(vals, labels) {
    var W = 620, H = 210, l = 36, r = 12, t = 12, b = 26, n = vals.length, iw = W - l - r, ih = H - t - b;
    var X = function (i) { return l + iw * i / (n - 1); }, Y = function (v) { return t + ih * (1 - v / 100); };
    var g = '', i;
    [0, 25, 50, 75, 100].forEach(function (v) { g += '<line class="ch-grid" x1="' + l + '" x2="' + (W - r) + '" y1="' + Y(v) + '" y2="' + Y(v) + '"/><text class="ch-t" x="' + (l - 6) + '" y="' + (Y(v) + 4) + '" text-anchor="end">' + v + '%</text>'; });
    var pts = vals.map(function (v, k) { return X(k).toFixed(1) + ',' + Y(v).toFixed(1); });
    var xl = '';
    for (i = 0; i < n; i += 2) xl += '<text class="ch-t" x="' + X(i) + '" y="' + (H - 6) + '" text-anchor="middle">' + labels[i] + '</text>';
    return '<svg class="chart" viewBox="0 0 ' + W + ' ' + H + '" role="img" aria-label="Biểu đồ mức lấp đầy 12 giờ gần nhất">' + g +
      '<path class="ch-area" d="M' + X(0) + ',' + Y(0) + ' L' + pts.join(' L') + ' L' + X(n - 1) + ',' + Y(0) + 'Z"/><polyline class="ch-line" points="' + pts.join(' ') + '"/>' + xl +
      '<circle class="ch-dot" cx="' + X(n - 1) + '" cy="' + Y(vals[n - 1]) + '" r="6"/></svg>';
  }
  function niceMax(v) { var p = Math.pow(10, Math.floor(Math.log10(v || 1))), m = v / p; return (m <= 1 ? 1 : m <= 2 ? 2 : m <= 5 ? 5 : 10) * p; }
  function stackChart(days) {
    var W = 640, H = 250, l = 46, r = 8, t = 10, b = 28, n = days.length, iw = W - l - r, ih = H - t - b;
    var mx = niceMax(Math.max.apply(null, days.map(function (d) { return d.car + d.moto + d.ev; })) * 1.05), bw = iw / n * 0.62, g = '', bars = '', i;
    var Y = function (v) { return t + ih * (1 - v / mx); };
    for (i = 0; i <= 4; i++) { var v = mx * i / 4; g += '<line class="ch-grid" x1="' + l + '" x2="' + (W - r) + '" y1="' + Y(v) + '" y2="' + Y(v) + '"/><text class="ch-t" x="' + (l - 6) + '" y="' + (Y(v) + 4) + '" text-anchor="end">' + (v ? S.moneyShort(v) : '0') + '</text>'; }
    days.forEach(function (d, k) {
      var x = l + iw / n * k + (iw / n - bw) / 2, y = Y(0), tt = d.d.slice(8) + '/' + d.d.slice(5, 7);
      ['car', 'moto', 'ev'].forEach(function (s) { var h = ih * d[s] / mx; y -= h; bars += '<rect class="b-' + s + '" x="' + x.toFixed(1) + '" y="' + y.toFixed(1) + '" width="' + bw.toFixed(1) + '" height="' + Math.max(0, h).toFixed(1) + '"><title>' + tt + ' · ' + S.VEH[s].label + ': ' + S.money(d[s]) + '</title></rect>'; });
      if (n <= 14 || k % 3 === 0) bars += '<text class="ch-t" x="' + (x + bw / 2).toFixed(1) + '" y="' + (H - 8) + '" text-anchor="middle">' + tt + '</text>';
    });
    return '<svg class="chart" viewBox="0 0 ' + W + ' ' + H + '" role="img" aria-label="Biểu đồ doanh thu theo ngày">' + g + bars + '</svg>';
  }

  /* ---------- Điều hướng và trạng thái thanh công cụ ----------
     Tạo menu/bộ lọc bãi, chuyển trang và cập nhật số cảnh báo chưa xử lý.
  */
  function buildNav() {
    $('#nav').innerHTML = Object.keys(PAGES).map(function (k) { return '<button type="button" data-act="go" data-p="' + k + '">' + S.icon(PAGES[k][1], 20) + '<span>' + PAGES[k][0] + '</span>' + (k === 'alerts' ? '<b class="nbadge" id="navBadge" hidden></b>' : '') + '</button>'; }).join('');
    $('#lotSel').innerHTML = '<option value="all">Tất cả bãi xe</option>' + S.LOTS.map(function (l) { return '<option value="' + l.id + '">' + esc(l.name) + '</option>'; }).join('');
    $('#bellBtn').innerHTML = S.icon('bell', 20) + '<b class="nbadge" id="bellBadge" hidden></b>';
    $('#logoutBtn').innerHTML = S.icon('out', 18) + 'Đăng xuất'; $('#sideLogout').innerHTML = S.icon('out', 18);
    $('#sideName').textContent = sess.name; $('#sideEmail').textContent = sess.email;
    $('#sideAvatar').textContent = sess.name.trim().split(/\s+/).slice(0, 2).map(function (w) { return w.charAt(0); }).join('').toUpperCase();
  }
  function go(p) {
    if (!PAGES[p]) p = 'dash'; ui.page = p;
    $('#pageTitle').textContent = PAGES[p][0];
    Array.prototype.forEach.call(document.querySelectorAll('#nav button'), function (b) { if (b.getAttribute('data-p') === p) b.setAttribute('aria-current', 'page'); else b.removeAttribute('aria-current'); });
    if (location.hash !== '#' + p) history.replaceState(null, '', '#' + p);
    render(); window.scrollTo(0, 0);
    /* [ANIM] hiệu ứng vào trang: chỉ chạy khi bấm đổi trang. Không đặt hiệu ứng "xuất hiện" trong render() vì trang tự vẽ lại mỗi 3 giây. */
    var pg = $('#page'); pg.classList.remove('enter'); void pg.offsetWidth; pg.classList.add('enter');
  }
  function badges() {
    var n = S.state.alerts.filter(function (a) { return a.status === 'new'; }).length;
    ['navBadge', 'bellBadge'].forEach(function (id) { var e = $('#' + id); if (e) { e.hidden = !n; e.textContent = n; } });
  }

  /* ---------- Trang: Tổng quan ----------
     KPI, mức lấp đầy, tình trạng các bãi, hoạt động gần đây và cảnh báo mở.
  */
  var EVT = { in: ['Vào', 'free'], out: ['Ra', 'acc'], book: ['Đặt', 'res'], cancel: ['Hủy', 'blk'], maint: ['Bảo trì', 'blk'], alert: ['Cảnh báo', 'occ'] };
  function pDash() {
    var lots = lotsSel(), a = agg(lots), now = new Date(), days = dailyAgg(lots), td = days[29], ys = days[28];
    var todayRev = td.car + td.moto + td.ev, ysRev = ys.car + ys.moto + ys.ev;
    var ysSame = ysRev * Math.max(0.05, (now.getHours() + now.getMinutes() / 60) / 24), delta = ysSame ? Math.round((todayRev - ysSame) / ysSame * 100) : 0;
    var h = '<div class="kpis">' +
      kpi('Chỗ trống', a.free + ' <small>/ ' + a.total + '</small>', a.res + ' chỗ đang được giữ') +
      kpi('Mức lấp đầy', a.fill + '<small>%</small>', a.blk + ' chỗ đang bảo trì') +
      kpi('Xe đang gửi', a.occ, pct(a.occ, a.total) + '% công suất') +
      kpi('Doanh thu hôm nay', S.moneyShort(todayRev) + '<small>₫</small>', (delta >= 0 ? '+' : '') + delta + '% so với hôm qua cùng giờ', delta >= 0 ? 'up' : 'down') + '</div>';
    var vals = [], labs = [], i, hh, dd;
    for (i = 11; i >= 0; i--) { hh = (now.getHours() - i + 24) % 24; dd = hh > now.getHours() ? (todayDow() + 6) % 7 : todayDow(); vals.push(Math.round(heatAvg(lots, dd, hh))); labs.push(hh + 'h'); }
    vals[11] = a.fill;
    h += '<div class="grid2"><section class="card"><div class="card-h"><h2>Mức lấp đầy 12 giờ gần nhất</h2><span class="badge acc">Điểm cuối là số liệu trực tiếp</span></div>' + lineChart(vals, labs) + '</section>';
    h += '<section class="card"><div class="card-h"><h2>Tình trạng từng bãi</h2></div>' + S.LOTS.filter(function (l) { return ui.lot === 'all' || ui.lot === l.id; }).map(function (l) {
      var c = S.counts(l.id), st = S.status(c), t = c.total || 1;
      return '<div class="lotrow"><div><b>' + esc(l.name) + '</b><small>' + c.free + ' trống / ' + c.total + ' chỗ</small></div><div class="sbar-wrap"><div class="sbar" role="img" aria-label="' + esc(l.name) + ': ' + c.occ + ' có xe, ' + c.res + ' đã đặt, ' + c.free + ' trống"><i class="b-occ" style="width:' + c.occ / t * 100 + '%"></i><i class="b-res" style="width:' + c.res / t * 100 + '%"></i><i class="b-blk" style="width:' + c.blk / t * 100 + '%"></i><i class="b-free" style="width:' + c.free / t * 100 + '%"></i></div></div><span class="badge ' + st.k + '">' + st.label + '</span></div>';
    }).join('') + '<div style="margin-top:10px">' + S.legendHtml() + '</div></section></div>';
    var ev = S.state.events.filter(function (e) { return ui.lot === 'all' || e.lotId === ui.lot; }).slice(0, 8);
    var al = S.state.alerts.filter(function (x) { return x.status !== 'done' && (ui.lot === 'all' || x.lotId === ui.lot); }).slice(0, 4);
    h += '<div class="grid2e"><section class="card"><div class="card-h"><h2>Hoạt động gần đây</h2><span class="badge free"><i class="dot"></i>Trực tiếp</span></div><div class="feed">' +
      (ev.length ? ev.map(function (e) { var m = EVT[e.type] || EVT.in; return '<div class="feed-i"><time>' + S.hm(e.t) + '</time><span class="badge ' + m[1] + '">' + m[0] + '</span><span>' + esc(e.msg) + (ui.lot === 'all' ? ' <span class="muted">· ' + esc(lotName(e.lotId)) + '</span>' : '') + '</span></div>'; }).join('') : '<p class="empty">Chưa có hoạt động nào.</p>') + '</div></section>';
    h += '<section class="card"><div class="card-h"><h2>Cảnh báo cần xử lý</h2><button class="btn sm" type="button" data-act="go" data-p="alerts">Xem tất cả</button></div><div class="alist">' +
      (al.length ? al.map(function (x) { return alertHtml(x, true); }).join('') : '<p class="empty">Không có cảnh báo nào cần xử lý.</p>') + '</div></section></div>';
    return h;
  }
  function kpi(l, v, s, cls) { return '<div class="kpi"><span class="k-l">' + l + '</span><b class="k-v">' + v + '</b><span class="k-s ' + (cls || '') + '">' + s + '</span></div>'; }

  /* ---------- Trang: Sơ đồ trực tiếp ----------
     Chọn bãi/tầng, xem trạng thái từng ô và thực hiện thao tác quản lý chỗ.
  */
  function pLive() {
    var l = S.lot(ui.liveLot), f = S.floorOf(l, ui.liveFloor) || l.floors[0]; ui.liveFloor = f.id;
    var c = S.counts(l.id, f.id);
    var h = '<div class="fbar"><div class="chips">' + S.LOTS.map(function (x) { return '<button type="button" class="chip" data-act="liveLot" data-id="' + x.id + '" aria-pressed="' + (x.id === l.id) + '">' + esc(x.name) + '</button>'; }).join('') + '</div></div>';
    h += '<div class="livegrid"><section class="card"><div class="card-h"><h2>' + esc(l.name) + '</h2>' +
      (l.floors.length > 1 ? '<div class="chips">' + l.floors.map(function (x) { return '<button type="button" class="chip" data-act="liveFloor" data-f="' + x.id + '" aria-pressed="' + (x.id === f.id) + '">' + esc(x.name) + '</button>'; }).join('') + '</div>' : '<span class="badge acc">' + esc(f.name) + '</span>') + '</div>' +
      '<div class="row wrap" style="margin-bottom:12px;gap:6px 16px"><span class="badge free">' + c.free + ' trống</span><span class="badge occ">' + c.occ + ' có xe</span><span class="badge res">' + c.res + ' đã đặt</span><span class="badge blk">' + c.blk + ' bảo trì</span></div>' +
      S.floorHtml(l, f, { cell: function (id, s) { var p = s.plate ? ' · ' + s.plate : ''; return { button: true, title: id + p, sel: !!(ui.sel && ui.sel.lot === l.id && ui.sel.id === id) }; } }) +
      '<div style="margin-top:12px">' + S.legendHtml() + '</div></section>' + slotDetail() + '</div>';
    return h;
  }
  function slotDetail() {
    if (!ui.sel || ui.sel.lot !== ui.liveLot) return '<aside class="card slotdet"><h3>Chọn một chỗ</h3><p class="muted">Bấm vào một ô trên sơ đồ để xem biển số, thời gian đỗ và thao tác nhanh.</p></aside>';
    var id = ui.sel.id, s = S.state.slots[ui.sel.lot][id]; if (!s) return '';
    var lbl = { free: 'Còn trống', occ: 'Có xe', res: 'Đã đặt', blk: 'Bảo trì' }[s.st], b = s.bk ? S.booking(s.bk) : null;
    var acts = '';
    if (s.st === 'free') acts = '<button class="btn" type="button" data-act="slotDo" data-do="blk">Đánh dấu bảo trì</button>';
    else if (s.st === 'blk') acts = '<button class="btn primary" type="button" data-act="slotDo" data-do="open">Mở lại chỗ này</button>';
    else if (s.st === 'occ' && !b) acts = '<button class="btn" type="button" data-act="slotDo" data-do="free">Xe đã rời, giải phóng chỗ</button>';
    else if (s.st === 'res' && b) acts = '<button class="btn danger" type="button" data-act="slotDo" data-do="cancel">Hủy giữ chỗ</button>';
    else if (s.st === 'res') acts = '<button class="btn" type="button" data-act="slotDo" data-do="free">Trả chỗ về trống</button>';
    return '<aside class="card slotdet"><div class="row"><h3 class="grow">' + id + '</h3><span class="badge ' + s.st + '">' + lbl + '</span></div><div class="kv">' +
      '<div><span>Loại chỗ</span><b>' + S.VEH[s.t].label + '</b></div>' + (s.plate ? '<div><span>Biển số</span><b>' + esc(s.plate) + '</b></div>' : '') +
      (s.since ? '<div><span>Đã đỗ</span><b>' + S.dur(Date.now() - s.since) + '</b></div>' : '') + (b ? '<div><span>Vé</span><b>' + b.id + '</b></div>' : '') + '</div>' + acts + '</aside>';
  }

  /* ---------- Trang: Heatmap ----------
     Tổng hợp mức lấp đầy hoặc lượt xe mẫu theo ngày trong tuần và giờ.
  */
  function pHeat() {
    var lots = lotsSel(), m = ui.heatMetric, now = new Date(), nd = todayDow(), cap = sum(lots, function (l) { return S.counts(l.id).total; });
    var grid = [], mx = 0, mn = 1e9, pk = null, lo = null, d, hr;
    for (d = 0; d < 7; d++) { grid.push([]); for (hr = 0; hr < 24; hr++) { var o = heatAvg(lots, d, hr), v = m === 'occ' ? Math.round(o) : Math.round(o / 100 * cap * 0.38); grid[d].push(v); if (v > mx) { mx = v; pk = [d, hr]; } if (hr >= 6 && hr <= 22 && v < mn) { mn = v; lo = [d, hr]; } } }
    var unit = m === 'occ' ? '%' : ' lượt', h = '<div class="fbar"><div class="seg" style="display:inline-grid;grid-auto-flow:column;gap:3px;background:var(--surface2);padding:3px;border-radius:10px"><button type="button" class="chip" data-act="heatMetric" data-m="occ" aria-pressed="' + (m === 'occ') + '">Mức lấp đầy</button><button type="button" class="chip" data-act="heatMetric" data-m="trips" aria-pressed="' + (m === 'trips') + '">Lượt xe vào mỗi giờ</button></div><span class="hint">Trung bình các tuần gần đây · ' + (ui.lot === 'all' ? 'tất cả bãi' : esc(lotName(ui.lot))) + '</span></div>';
    h += '<section class="card"><div class="card-h"><h2>Bản đồ nhiệt theo giờ trong tuần</h2></div><div class="heatwrap"><div class="heat"><span></span>';
    for (hr = 0; hr < 24; hr++) h += '<span class="hh">' + (hr % 3 === 0 ? hr : '') + '</span>';
    for (d = 0; d < 7; d++) {
      h += '<span class="hd">' + DOW[d] + '</span>';
      for (hr = 0; hr < 24; hr++) { var v2 = grid[d][hr], p = 8 + (v2 / (mx || 1)) * 92; h += '<span class="hc' + (d === nd && hr === now.getHours() ? ' now' : '') + '" style="background:color-mix(in srgb, var(--accent) ' + p.toFixed(0) + '%, var(--surface2))" title="' + DOW[d] + ' ' + S.pad(hr) + ':00 · ' + v2 + unit + '"></span>'; }
    }
    h += '</div></div><div class="hscale"><span>Thấp</span><i></i><span>Cao (' + mx + unit + ')</span><span style="margin-left:auto">Ô viền đậm là thời điểm hiện tại</span></div></section>';
    var wk = [0, 0], i; for (hr = 0; hr < 24; hr++) { wk[0] += grid[5][hr] + grid[6][hr]; wk[1] += grid[0][hr] + grid[1][hr] + grid[2][hr] + grid[3][hr] + grid[4][hr]; }
    h += '<div class="insights"><div class="insight"><small>Giờ cao điểm nhất</small><b>' + DOW[pk[0]] + ' ' + S.pad(pk[1]) + ':00 - ' + S.pad(pk[1] + 1) + ':00</b><small>' + mx + unit + '</small></div>' +
      '<div class="insight"><small>Giờ thấp nhất (6h - 22h)</small><b>' + DOW[lo[0]] + ' ' + S.pad(lo[1]) + ':00 - ' + S.pad(lo[1] + 1) + ':00</b><small>' + mn + unit + '</small></div>' +
      '<div class="insight"><small>Cuối tuần so với ngày thường</small><b>' + (wk[1] ? Math.round((wk[0] / 2) / (wk[1] / 5) * 100 - 100) : 0) + '%</b><small>trung bình mỗi ngày</small></div></div>';
    return h;
  }

  /* ---------- Trang: Doanh thu ----------
     Xem báo cáo 7/14/30 ngày; cơ cấu thanh toán là số liệu mẫu; hỗ trợ xuất CSV.
  */
  function pRev() {
    var lots = lotsSel(), n = ui.revDays, all = dailyAgg(lots), cur = all.slice(30 - n), prev = 30 - 2 * n >= 0 ? all.slice(30 - 2 * n, 30 - n) : null;
    var tot = function (a) { return sum(a, function (d) { return d.car + d.moto + d.ev; }); }, tr = sum(cur, function (d) { return d.trips; }), rev = tot(cur);
    var delta = prev ? Math.round((rev - tot(prev)) / tot(prev) * 100) : null;
    var h = '<div class="fbar"><div class="chips">' + [7, 14, 30].map(function (k) { return '<button type="button" class="chip" data-act="revDays" data-n="' + k + '" aria-pressed="' + (k === n) + '">' + k + ' ngày</button>'; }).join('') + '</div><button class="btn sm right" type="button" data-act="exportCsv">' + S.icon('download', 16) + 'Xuất CSV</button></div>';
    h += '<div class="kpis">' + kpi('Tổng doanh thu', S.moneyShort(rev) + '<small>₫</small>', delta == null ? 'Chưa đủ dữ liệu kỳ trước' : (delta >= 0 ? '+' : '') + delta + '% so với ' + n + ' ngày trước', delta == null ? '' : delta >= 0 ? 'up' : 'down') +
      kpi('Lượt xe', tr.toLocaleString('vi-VN'), 'Trung bình ' + Math.round(tr / n) + ' lượt/ngày') + kpi('Giá trị mỗi lượt', S.money(rev / (tr || 1)), 'Đã gồm phụ thu cao điểm') + kpi('Doanh thu mỗi ngày', S.moneyShort(rev / n) + '<small>₫</small>', 'Trung bình trong kỳ') + '</div>';
    h += '<section class="card"><div class="card-h"><h2>Doanh thu theo ngày</h2><div class="chart-legend">' + ['car', 'moto', 'ev'].map(function (t) { return '<span><i class="lg-' + t + '"></i>' + S.VEH[t].label + '</span>'; }).join('') + '</div></div>' + stackChart(cur) + '</section>';
    var rows = lots.map(function (l) {
      var r = 0, t = 0, i; for (i = 30 - n; i < 30; i++) { var e = S.hist.daily[l.id][i]; r += e.rev.car + e.rev.moto + e.rev.ev; t += e.trips.car + e.trips.moto + e.trips.ev; }
      var lr = liveRev([l]); r += lr.car + lr.moto + lr.ev;
      var avg = sum([0, 1, 2, 3, 4, 5, 6], function (d) { return sum([8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20], function (hh) { return S.hist.heat[l.id][d][hh]; }); }) / (7 * 13);
      return '<tr><td>' + esc(l.name) + '</td><td>' + S.money(r) + '</td><td>' + t.toLocaleString('vi-VN') + '</td><td>' + Math.round(avg) + '%</td></tr>';
    }).join('');
    h += '<div class="grid2"><section class="card"><div class="card-h"><h2>Theo bãi xe</h2></div><div class="tbl-wrap"><table class="tbl"><thead><tr><th>Bãi xe</th><th>Doanh thu</th><th>Lượt xe</th><th>Lấp đầy TB</th></tr></thead><tbody>' + rows + '</tbody></table></div></section>' +
      '<section class="card"><div class="card-h"><h2>Phương thức thanh toán</h2></div><div class="paysplit" role="img" aria-label="Tỉ lệ phương thức thanh toán">' + Object.keys(S.PAYSPLIT).map(function (k) { return '<i class="pm-' + k + '" style="width:' + S.PAYSPLIT[k] * 100 + '%"></i>'; }).join('') + '</div><div class="payleg">' +
      Object.keys(S.PAYSPLIT).map(function (k) { return '<div><i class="pm-' + k + '"></i>' + S.PAY[k] + '<b>' + S.moneyShort(rev * S.PAYSPLIT[k]) + '</b></div>'; }).join('') + '</div><p class="hint" style="margin-top:10px">Tỉ lệ phương thức thanh toán là số liệu mẫu.</p></section></div>';
    return h;
  }

  /* ---------- Trang: Giá vé ----------
     Chỉnh giá mặc định/giá riêng từng bãi, phụ thu giờ cao điểm và tính thử.
  */
  function pPrice() {
    var P = S.state.pricing, sc = ui.priceScope, own = sc !== 'def' && !!P.lots[sc], pr = sc === 'def' ? P.def : (P.lots[sc] || P.def), dis = sc !== 'def' && !own ? ' disabled' : '';
    var h = '<div class="grid2"><section class="card pform"><div class="card-h"><h2>Bảng giá</h2></div>' +
      '<div class="field"><label for="prScope">Áp dụng cho</label><select id="prScope" data-chg="priceScope"><option value="def"' + (sc === 'def' ? ' selected' : '') + '>Mặc định (mọi bãi chưa có giá riêng)</option>' + S.LOTS.map(function (l) { return '<option value="' + l.id + '"' + (sc === l.id ? ' selected' : '') + '>' + esc(l.name) + (P.lots[l.id] ? ' (giá riêng)' : '') + '</option>'; }).join('') + '</select></div>';
    if (sc !== 'def') h += '<label class="chk"><input type="checkbox" id="prOwn" data-chg="priceOwn"' + (own ? ' checked' : '') + '> Dùng giá riêng cho bãi này</label>';
    h += '<div class="tbl-wrap"><table class="ptab"><thead><tr><th>Loại xe</th><th>Giờ đầu (₫)</th><th>Mỗi giờ tiếp (₫)</th><th>Tối đa mỗi ngày (₫)</th></tr></thead><tbody>' +
      ['car', 'moto', 'ev'].map(function (v) { return '<tr><td>' + S.VEH[v].label + '</td>' + ['first', 'next', 'cap'].map(function (k) { return '<td><input type="number" min="0" step="1000" id="pr-' + v + '-' + k + '" value="' + pr[v][k] + '"' + dis + ' aria-label="' + S.VEH[v].label + ' ' + k + '"></td>'; }).join('') + '</tr>'; }).join('') + '</tbody></table></div>' +
      '<hr style="border:0;border-top:1px solid var(--line);width:100%"><div class="card-h" style="margin:0"><h2 style="font-size:15px">Phụ thu giờ cao điểm (áp dụng mọi bãi)</h2></div>' +
      '<label class="chk"><input type="checkbox" id="pkOn"' + (P.peak.on ? ' checked' : '') + '> Bật phụ thu giờ cao điểm</label>' +
      '<div class="grid2e" style="grid-template-columns:repeat(3,minmax(0,1fr))"><div class="field"><label for="pkPct">Phụ thu (%)</label><input type="number" id="pkPct" min="0" max="200" value="' + P.peak.pct + '"></div><div class="field"><label for="pkFrom">Từ giờ</label><input type="number" id="pkFrom" min="0" max="23" value="' + P.peak.from + '"></div><div class="field"><label for="pkTo">Đến giờ</label><input type="number" id="pkTo" min="0" max="24" value="' + P.peak.to + '"></div></div>' +
      '<div class="err" id="prErr"></div><div class="row wrap"><button class="btn primary" type="button" data-act="savePrice">Lưu bảng giá</button><button class="btn" type="button" data-act="resetPrice">Khôi phục giá mặc định</button></div></section>' +
      '<section class="card calc"><div class="card-h" style="margin:0"><h2>Tính thử giá</h2></div><p class="hint">Dùng bảng giá đã lưu. Khách thấy giá mới ngay trên Web Client.</p>' +
      '<div class="field"><label for="cVeh">Loại xe</label><select id="cVeh" data-chg="calc">' + ['car', 'moto', 'ev'].map(function (v) { return '<option value="' + v + '">' + S.VEH[v].label + '</option>'; }).join('') + '</select></div>' +
      '<div class="grid2e"><div class="field"><label for="cHours">Số giờ gửi</label><input type="number" id="cHours" min="1" max="72" value="3" data-chg="calc"></div><div class="field"><label for="cStart">Giờ vào</label><input type="number" id="cStart" min="0" max="23" value="9" data-chg="calc"></div></div>' +
      '<div><span class="muted">Giá vé</span><div class="res" id="cRes"></div><div class="hint" id="cNote"></div></div></section></div>';
    return h;
  }
  function calcPrice() {
    var el = $('#cRes'); if (!el) return;
    var v = $('#cVeh').value, hrs = Math.max(1, +$('#cHours').value || 1), st = new Date(); st.setHours(+$('#cStart').value || 0, 0, 0, 0);
    var lotId = ui.priceScope === 'def' ? 'L_DEF' : ui.priceScope, r = S.price(v, hrs, st.getTime(), lotId);
    el.textContent = S.money(r.amount); $('#cNote').textContent = r.peak ? 'Đã gồm phụ thu giờ cao điểm.' : 'Không phụ thu cao điểm.';
  }
  function savePrice() {
    var P = S.state.pricing, sc = ui.priceScope, err = $('#prErr'), np = { car: {}, moto: {}, ev: {} }, bad = '';
    ['car', 'moto', 'ev'].forEach(function (v) {
      ['first', 'next', 'cap'].forEach(function (k) { var x = Math.round(+$('#pr-' + v + '-' + k).value); if (!(x >= 0)) bad = 'Giá phải là số không âm.'; np[v][k] = x; });
      if (!bad && np[v].cap < np[v].first) bad = 'Giá tối đa/ngày của ' + S.VEH[v].label.toLowerCase() + ' phải lớn hơn hoặc bằng giá giờ đầu.';
    });
    var pk = { on: $('#pkOn').checked, pct: Math.round(+$('#pkPct').value), from: Math.round(+$('#pkFrom').value), to: Math.round(+$('#pkTo').value) };
    if (!bad && (!(pk.pct >= 0 && pk.pct <= 200) || !(pk.from >= 0 && pk.from <= 23) || !(pk.to >= 0 && pk.to <= 24))) bad = 'Phụ thu 0-200%, giờ bắt đầu 0-23, giờ kết thúc 0-24.';
    if (bad) { err.textContent = bad; return; }
    err.textContent = '';
    if (sc === 'def') P.def = np; else if ($('#prOwn') && $('#prOwn').checked) P.lots[sc] = np;
    P.peak = pk; S.save(); toast('Đã lưu bảng giá. Web Client dùng giá mới ngay.'); render();
  }

  /* ---------- Trang: Cảnh báo ----------
     Lọc theo trạng thái/mức độ, nhận xử lý và lưu ghi chú xử lý.
  */
  var ATYPE = { overstay: ['Đỗ quá giờ', 'clock'], sensor: ['Cảm biến', 'alert'], full: ['Gần đầy', 'car'], unauthorized: ['Xe không có vé', 'alert'], payment: ['Thanh toán', 'card'], noshow: ['Quá hạn nhận chỗ', 'clock'] };
  var SEV = { high: ['Cao', 'occ'], med: ['Trung bình', 'res'], low: ['Thấp', 'blk'] }, ASTAT = { new: ['Mới', 'occ'], ack: ['Đang xử lý', 'res'], done: ['Đã xử lý', 'free'] };
  function alertHtml(a, compact) {
    var t = ATYPE[a.type] || ['Khác', 'alert'];
    var h = '<div class="alert ' + a.sev + (a.status === 'done' ? ' done' : '') + '"><div class="top"><span class="badge ' + SEV[a.sev][1] + '">' + SEV[a.sev][0] + '</span><span class="badge">' + S.icon(t[1], 13) + t[0] + '</span><span class="badge ' + ASTAT[a.status][1] + '">' + ASTAT[a.status][0] + '</span><span class="muted" style="margin-left:auto;font-size:12.5px">' + esc(lotName(a.lotId)) + ' · ' + S.ago(a.t) + '</span></div><div class="msg">' + esc(a.msg) + '</div>';
    if (a.note) h += '<div class="note">Ghi chú: ' + esc(a.note) + '</div>';
    if (a.status !== 'done') {
      h += '<div class="acts">' + (compact ? '' : '<input type="text" id="note-' + a.id + '" placeholder="Ghi chú xử lý (không bắt buộc)" maxlength="120" aria-label="Ghi chú">') +
        (a.status === 'new' ? '<button class="btn sm" type="button" data-act="alertSet" data-id="' + a.id + '" data-s="ack">Nhận xử lý</button>' : '') + '<button class="btn sm primary" type="button" data-act="alertSet" data-id="' + a.id + '" data-s="done">Đã xử lý xong</button></div>';
    }
    return h + '</div>';
  }
  function pAlerts() {
    var al = S.state.alerts.filter(function (a) { return ui.lot === 'all' || a.lotId === ui.lot; });
    var cnt = { open: 0, new: 0, ack: 0, done: 0, all: al.length }; al.forEach(function (a) { cnt[a.status]++; if (a.status !== 'done') cnt.open++; });
    var list = al.filter(function (a) { return (ui.afStatus === 'all' || (ui.afStatus === 'open' ? a.status !== 'done' : a.status === ui.afStatus)) && (ui.afSev === 'all' || a.sev === ui.afSev); });
    var h = '<div class="fbar"><div class="chips">' + [['open', 'Cần xử lý'], ['new', 'Mới'], ['ack', 'Đang xử lý'], ['done', 'Đã xử lý'], ['all', 'Tất cả']].map(function (s) { return '<button type="button" class="chip" data-act="afStatus" data-s="' + s[0] + '" aria-pressed="' + (ui.afStatus === s[0]) + '">' + s[1] + ' (' + cnt[s[0]] + ')</button>'; }).join('') + '</div><div class="chips">' + [['all', 'Mọi mức'], ['high', 'Cao'], ['med', 'Trung bình'], ['low', 'Thấp']].map(function (s) { return '<button type="button" class="chip" data-act="afSev" data-s="' + s[0] + '" aria-pressed="' + (ui.afSev === s[0]) + '">' + s[1] + '</button>'; }).join('') + '</div></div>';
    return h + '<div class="alist">' + (list.length ? list.map(function (a) { return alertHtml(a); }).join('') : '<p class="empty card">Không có cảnh báo nào trong bộ lọc này.</p>') + '</div>';
  }

  var admin3DInstance = null;
  function pView3D() {
    var l = S.lot(ui.liveLot) || S.LOTS[0];
    var h = '<div class="fbar"><div class="chips">' + S.LOTS.map(function (x) { return '<button type="button" class="chip" data-act="liveLot" data-id="' + x.id + '" aria-pressed="' + (x.id === l.id) + '">' + esc(x.name) + '</button>'; }).join('') + '</div></div>';
    h += '<div class="card" style="padding:16px"><div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:12px"><h2>Sơ đồ không gian 3D - ' + esc(l.name) + '</h2><a href="../3d-viewer.html" target="_blank" class="btn sm primary">Mở toàn màn hình ↗</a></div><div id="admin3DContainer" style="height:580px;position:relative;border-radius:12px;overflow:hidden"></div></div>';
    setTimeout(function () {
      if (admin3DInstance) {
        admin3DInstance.destroy();
        admin3DInstance = null;
      }
      var target = document.querySelector('#admin3DContainer');
      if (target && window.Parking3DViewer) {
        admin3DInstance = new window.Parking3DViewer({
          container: target,
          lotId: l.id === 'L1' ? 1 : (l.id === 'L2' ? 2 : 3),
          lotName: l.name,
          floorId: l.floors[0] ? l.floors[0].id : 'B1'
        });
      }
    }, 50);
    return h;
  }

  /* ---------- Vẽ và làm mới giao diện ----------
     render() thay nội dung trang hiện tại; softRender() tránh làm mất nội dung đang nhập.
  */
  function render() {
    var el = $('#page'), fn = { dash: pDash, live: pLive, view3d: pView3D, heat: pHeat, rev: pRev, price: pPrice, alerts: pAlerts }[ui.page];
    var y = window.scrollY; el.innerHTML = fn(); if (ui.page === 'price') calcPrice(); window.scrollTo(0, y); badges();
  }
  function softRender() {
    var a = document.activeElement; if (a && a.closest && a.closest('#page') && (a.tagName === 'INPUT' || a.tagName === 'SELECT')) return;
    if (ui.page === 'price' || ui.page === 'rev' || ui.page === 'heat' || ui.page === 'view3d') { badges(); return; }
    render();
  }

  /* ---------- Sự kiện và thao tác quản trị ----------
     Các data-act trong HTML được ánh xạ tới thao tác; thay đổi dữ liệu được lưu qua SP.save().
  */
  function setAlert(id, st) {
    var a = S.state.alerts.filter(function (x) { return x.id === id; })[0]; if (!a) return;
    var n = $('#note-' + id); if (n && n.value.trim()) a.note = n.value.trim();
    a.status = st; if (st === 'done') S.pushEvent('alert', a.lotId, 'Đã xử lý cảnh báo: ' + a.msg.slice(0, 60)); S.save(); toast(st === 'ack' ? 'Đã nhận xử lý cảnh báo.' : 'Đã đánh dấu xử lý xong.');
  }
  function slotDo(act) {
    var o = ui.sel; if (!o) return; var s = S.state.slots[o.lot][o.id];
    if (act === 'blk' && s.st === 'free') { s.st = 'blk'; S.pushEvent('maint', o.lot, 'Chỗ ' + o.id + ' chuyển sang bảo trì'); }
    else if (act === 'open' && s.st === 'blk') { s.st = 'free'; S.pushEvent('maint', o.lot, 'Chỗ ' + o.id + ' hoạt động trở lại'); }
    else if (act === 'free') { s.st = 'free'; delete s.plate; delete s.since; S.pushEvent('out', o.lot, 'Giải phóng chỗ ' + o.id); }
    else if (act === 'cancel' && s.bk) { S.cancelBooking(s.bk); toast('Đã hủy giữ chỗ.'); return; }
    S.save();
  }
  function exportCsv() {
    var lots = lotsSel(), n = ui.revDays, rows = ['Ngay,Bai xe,O to (VND),Xe may (VND),O to dien (VND),Tong (VND),Luot xe'];
    lots.forEach(function (l) { for (var i = 30 - n; i < 30; i++) { var e = S.hist.daily[l.id][i], t = e.rev.car + e.rev.moto + e.rev.ev; rows.push([e.d, '"' + l.name + '"', e.rev.car, e.rev.moto, e.rev.ev, t, e.trips.car + e.trips.moto + e.trips.ev].join(',')); } });
    try { var a = document.createElement('a'); a.href = URL.createObjectURL(new Blob(['﻿' + rows.join('\n')], { type: 'text/csv;charset=utf-8' })); a.download = 'doanh-thu-' + n + '-ngay.csv'; document.body.appendChild(a); a.click(); a.remove(); toast('Đã xuất báo cáo doanh thu.'); } catch (e) { toast('Không xuất được tệp CSV trên trình duyệt này.'); }
  }
  var acts = {
    logout: function () { if (!window.confirm('Bạn có chắc chắn muốn đăng xuất?')) return; S.auth.logout(); location.href = '../index.html'; },
    go: function (t) { go(t.getAttribute('data-p')); }, goAlerts: function () { go('alerts'); },
    liveLot: function (t) { ui.liveLot = t.getAttribute('data-id'); ui.liveFloor = null; ui.sel = null; render(); },
    liveFloor: function (t) { ui.liveFloor = t.getAttribute('data-f'); render(); },
    slotDo: function (t) { slotDo(t.getAttribute('data-do')); },
    heatMetric: function (t) { ui.heatMetric = t.getAttribute('data-m'); render(); },
    revDays: function (t) { ui.revDays = +t.getAttribute('data-n'); render(); },
    exportCsv: exportCsv, savePrice: savePrice,
    resetPrice: function () { S.state.pricing = S.defaultPricing(); S.save(); toast('Đã khôi phục bảng giá mặc định.'); render(); },
    alertSet: function (t) { setAlert(t.getAttribute('data-id'), t.getAttribute('data-s')); },
    afStatus: function (t) { ui.afStatus = t.getAttribute('data-s'); render(); }, afSev: function (t) { ui.afSev = t.getAttribute('data-s'); render(); },
    toggleSim: function () { ui.sim = !ui.sim; paintSim(); }
  };
  document.addEventListener('click', function (e) {
    var sl = e.target.closest && e.target.closest('button.slot[data-slot]');
    if (sl && ui.page === 'live') { ui.sel = { lot: ui.liveLot, id: sl.getAttribute('data-slot') }; render(); return; }
    var t = e.target.closest && e.target.closest('[data-act]'); if (t && acts[t.getAttribute('data-act')]) acts[t.getAttribute('data-act')](t);
  });
  document.addEventListener('change', function (e) {
    var k = e.target.getAttribute && e.target.getAttribute('data-chg');
    if (e.target.id === 'lotSel') { ui.lot = e.target.value; render(); return; }
    if (k === 'priceScope') { ui.priceScope = e.target.value; render(); }
    else if (k === 'priceOwn') { var P = S.state.pricing; if (e.target.checked) P.lots[ui.priceScope] = JSON.parse(JSON.stringify(P.def)); else delete P.lots[ui.priceScope]; S.save(); render(); }
    else if (k === 'calc') calcPrice();
  });
  document.addEventListener('input', function (e) { if (e.target.getAttribute && e.target.getAttribute('data-chg') === 'calc') calcPrice(); });

  /* ---------- Giả lập cảm biến IoT ----------
     Sinh sự kiện xe vào/ra và cảnh báo mẫu định kỳ; tắt bằng nút Trực tiếp/Tạm dừng.
     Đây chỉ là bộ giả lập cho demo, không thay thế dữ liệu cảm biến thật.
  */
  function pick(a) { return a[Math.floor(Math.random() * a.length)]; }
  function paintSim() { $('#simDot').className = 'dot' + (ui.sim ? ' live' : ''); $('#simDot').style.background = ui.sim ? '' : 'var(--blk)'; $('#simTxt').textContent = ui.sim ? 'Trực tiếp' : 'Tạm dừng'; }
  function tick() {
    if (!ui.sim) return;
    var lot = pick(S.LOTS), c = S.counts(lot.id), sl = S.state.slots[lot.id], occR = (c.occ + c.res) / Math.max(1, c.total - c.blk);
    var ids = Object.keys(sl).filter(function (id) { return !sl[id].bk && (sl[id].st === 'free' || sl[id].st === 'occ'); });
    if (ids.length) {
      var wantIn = Math.random() < SP.clamp(0.55 - (occR - 0.6) * 0.8, 0.15, 0.85), pool = ids.filter(function (id) { return sl[id].st === (wantIn ? 'free' : 'occ'); });
      if (pool.length) {
        var id = pick(pool), s = sl[id];
        if (wantIn) { s.st = 'occ'; s.plate = S.plate(Math.random, s.t === 'moto'); s.since = Date.now(); S.pushEvent('in', lot.id, 'Xe ' + s.plate + ' vào bãi, chỗ ' + id); }
        else { S.pushEvent('out', lot.id, 'Xe ' + s.plate + ' ra bãi, chỗ ' + id); s.st = 'free'; delete s.plate; delete s.since; }
      }
    }
    ui.simT++;
    var c2 = S.counts(lot.id), has = S.state.alerts.some(function (a) { return a.type === 'full' && a.lotId === lot.id && a.status !== 'done'; });
    if (c2.free / Math.max(1, c2.total) < 0.08 && !has) S.addAlert('med', 'full', lot.id, 'Bãi ' + lot.name + ' còn dưới 8% chỗ trống');
    else if (ui.simT % 9 === 0) {
      var r = pick(['overstay', 'sensor', 'unauthorized', 'payment']), occIds = Object.keys(sl).filter(function (i) { return sl[i].st === 'occ'; }), sid = pick(Object.keys(sl)), pl = S.plate(Math.random, false);
      if (r === 'overstay' && occIds.length) S.addAlert('high', r, lot.id, 'Xe ' + sl[pick(occIds)].plate + ' đỗ quá giờ đặt ' + (20 + Math.floor(Math.random() * 90)) + ' phút');
      else if (r === 'sensor') S.addAlert('high', r, lot.id, 'Cảm biến ' + sid + ' mất kết nối hơn 10 phút');
      else if (r === 'unauthorized') S.addAlert('med', r, lot.id, 'Phát hiện xe ' + pl + ' vào cổng nhưng không có vé hợp lệ');
      else S.addAlert('med', 'payment', lot.id, 'Giao dịch ' + pick(['MoMo', 'ZaloPay']) + ' #' + Math.floor(10000 + Math.random() * 89999) + ' thất bại, khách chưa ra được cổng');
    }
    S.state.bookings.forEach(function (b) {
      if (b.status === 'booked' && !b.noshow && Date.now() > b.startMs + S.CFG.graceMin * 60000) { b.noshow = true; S.addAlert('low', 'noshow', b.lotId, 'Đặt chỗ ' + b.slotId + ' (' + b.plate + ') quá hạn nhận chỗ ' + S.CFG.graceMin + ' phút'); }
    });
    S.save();
  }

  /* ---------- Khởi chạy Admin ----------
     Khởi tạo giao diện, đồng hồ, bộ giả lập và đồng bộ khi dữ liệu dùng chung thay đổi.
  */
  $('#brandName').textContent = S.CFG.brand;
  buildNav(); S.initTheme($('#themeBtn')); paintSim();
  function clock() { var d = new Date(); $('#clock').textContent = S.pad(d.getHours()) + ':' + S.pad(d.getMinutes()) + ':' + S.pad(d.getSeconds()); }
  clock(); setInterval(clock, 1000); setInterval(tick, 3200);
  S.subscribe(function () { softRender(); });
  document.addEventListener('themechange', function () { if (ui.page === 'dash' || ui.page === 'rev') render(); });
  window.addEventListener('hashchange', function () { var p = location.hash.replace('#', ''); if (PAGES[p] && p !== ui.page) go(p); });
  go((location.hash || '#dash').replace('#', ''));
})();
