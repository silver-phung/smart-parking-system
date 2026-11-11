/**
 * SmartPark 3D Render - ỨNG DỤNG XEM (camera Street View + toàn cảnh + UI)
 * Phụ thuộc: three.js r128 (global THREE), parking-3d-render-kit.js, parking-3d-render-floor.js
 *
 * API:  var v = new ParkingRender({ container:'#render3d', role:'guest|user|admin', lotId:'landmark81' });
 *       v.setRole('admin'); v.setLot('vincom'); v.setFloor(1); v.setMode('orbit'|'walk');
 */
(function (root) {
  'use strict';
  var I = root.__P3R_INTERNAL__, C = I.C, LOTS = I.LOTS, AISLE_Z = I.AISLE_Z;
  var T;

  var ROLE_LABEL = { guest: 'Khách vãng lai', user: 'Khách hàng (đã đăng nhập)', admin: 'Quản trị viên' };
  var STATUS_TXT = { OCCUPIED: 'Có xe', EMPTY: 'Trống', MAINTENANCE: 'Bảo trì' };

  function ease(t) { return t < 0.5 ? 2 * t * t : 1 - Math.pow(-2 * t + 2, 2) / 2; }
  function clamp(v, a, b) { return Math.max(a, Math.min(b, v)); }
  function esc(s) { return String(s).replace(/[&<>"]/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]; }); }

  function ParkingRender(opts) {
    opts = opts || {};
    T = root.THREE;
    this.el = typeof opts.container === 'string' ? document.querySelector(opts.container) : opts.container;
    if (!this.el) throw new Error('[ParkingRender] container not found');
    if (!T) { this.el.innerHTML = '<p style="color:#fca5a5;padding:24px">Không tải được three.js (cần mạng để tải CDN).</p>'; return; }
    this.role = opts.role || 'guest';
    this.mode = 'walk';
    this.lot = null; this.node = null; this.floorIdx = 0; this.selFloor = 0;
    this.walk = { pos: new T.Vector3(), yaw: Math.PI / 2, pitch: -0.04 };
    this.orbit = { theta: 0.55, phi: 0.95, r: 125, target: new T.Vector3(0, 0, 0) };
    this.hotspots = []; this.tween = null; this.tr = null; this.lifting = false;
    this.onChange = opts.onChange || null;
    this._build();
    this._init3D();
    this.setLot(opts.lotId || LOTS[0].id);
    this._loop = this._loop.bind(this);
    requestAnimationFrame(this._loop);
  }
  var P = ParkingRender.prototype;

  /* ------------------------------------------------------------------ */
  /* DOM / HUD                                                           */
  /* ------------------------------------------------------------------ */
  P._build = function () {
    var self = this;
    this.el.classList.add('p3r-root');
    this.el.innerHTML = [
      '<canvas class="p3r-canvas"></canvas>',
      '<div class="p3r-top">',
      '  <div class="p3r-group"><span class="p3r-cap">Tầng</span><div class="p3r-floors"></div></div>',
      '  <div class="p3r-group"><span class="p3r-cap">Bãi đỗ xe</span><select class="p3r-lot"></select></div>',
      '  <div class="p3r-group p3r-stats"></div>',
      '</div>',
      '<div class="p3r-where"></div>',
      '<div class="p3r-hint">Kéo chuột để nhìn quanh · Bấm vào <b>vòng tròn</b> trên sàn để di chuyển (như Street View) · Phím W/↑ đi tới, ←/→ xoay</div>',
      '<div class="p3r-lift" hidden></div>',
      '<div class="p3r-bottom">',
      '  <button class="p3r-btn on" data-mode="walk">🚶 Đi bộ (Street View)</button>',
      '  <button class="p3r-btn" data-mode="orbit">🗺 Toàn cảnh</button>',
      '  <span class="p3r-sep"></span>',
      '  <button class="p3r-btn" data-go="entry">⬇ Dốc vào</button>',
      '  <button class="p3r-btn" data-go="checkin">🛂 Booth Check-in</button>',
      '  <button class="p3r-btn" data-go="checkout">🏁 Booth Check-out &amp; Lối ra</button>',
      '  <button class="p3r-btn" data-go="ramp">↘ Dốc B2 &amp; Checkout vãng lai</button>',
      '  <button class="p3r-btn" data-go="core">🛗 Phòng thang máy</button>',
      '</div>',
      '<div class="p3r-labels"></div>',
      '<div class="p3r-fade"><span></span></div>'
    ].join('');
    var q = function (s) { return self.el.querySelector(s); };
    this.canvas = q('.p3r-canvas'); this.elFloors = q('.p3r-floors'); this.elLot = q('.p3r-lot');
    this.elStats = q('.p3r-stats'); this.elWhere = q('.p3r-where'); this.elLift = q('.p3r-lift');
    this.elFade = q('.p3r-fade'); this.elLabels = q('.p3r-labels'); this.elHint = q('.p3r-hint');
    LOTS.forEach(function (l) { var o = document.createElement('option'); o.value = l.id; o.textContent = l.name; self.elLot.appendChild(o); });
    this.elLot.addEventListener('change', function () { self.setLot(self.elLot.value); });
    this.el.querySelectorAll('[data-mode]').forEach(function (b) { b.addEventListener('click', function () { self.setMode(b.getAttribute('data-mode')); }); });
    this.el.querySelectorAll('[data-go]').forEach(function (b) { b.addEventListener('click', function () { self._goTo(b.getAttribute('data-go')); }); });
    this.labelPool = [];
    for (var i = 0; i < 9; i++) { var d = document.createElement('div'); d.className = 'p3r-label'; this.elLabels.appendChild(d); this.labelPool.push(d); }
    setTimeout(function () { self.elHint.classList.add('fade'); }, 9000);
  };

  P._renderFloorPills = function () {
    var self = this, def = this.lot.def;
    this.elFloors.innerHTML = '';
    def.ids.forEach(function (id, k) {
      var b = document.createElement('button');
      b.className = 'p3r-pill'; b.textContent = id; b.setAttribute('data-k', k);
      b.title = k === 0 ? 'Tầng ' + id + ' · Khu gửi xe THÁNG + Lane bypass vãng lai' : 'Tầng ' + id + ' · Khu gửi xe VÃNG LAI';
      b.addEventListener('click', function () { self.setFloor(k); });
      self.elFloors.appendChild(b);
    });
    this._syncFloorPills();
  };
  P._syncFloorPills = function () {
    var cur = this.mode === 'walk' ? this.floorIdx : this.selFloor;
    this.elFloors.querySelectorAll('.p3r-pill').forEach(function (b) { b.classList.toggle('on', +b.getAttribute('data-k') === cur); });
    this.el.querySelectorAll('[data-mode]').forEach(function (b) { b.classList.toggle('on', b.getAttribute('data-mode') === this.mode); }, this);
    var fl = this.lot.floors[cur], st = [0, 0, 0], totalLot = 0;
    this.lot.floors.forEach(function (f) { totalLot += f.slots.length; });
    fl.slots.forEach(function (s) {
      var i = s.status === 'OCCUPIED' ? 1 : (s.status === 'EMPTY' ? 0 : 2); st[i]++;
    });
    var kindChip = fl.kind === 'MONTHLY'
      ? '<span class="p3r-chip blue">Tầng ' + fl.id + ' · XE THÁNG (' + fl.slots.length + ' ô)</span>'
      : '<span class="p3r-chip green">Tầng ' + fl.id + ' · VÃNG LAI (' + fl.slots.length + ' ô)</span>';
    this.elStats.innerHTML = kindChip +
      '<span class="p3r-chip"><i style="background:#10b981"></i>Trống ' + st[0] + '</span>' +
      '<span class="p3r-chip"><i style="background:#ef4444"></i>Có xe ' + st[1] + '</span>' +
      '<span class="p3r-chip"><i style="background:#f59e0b"></i>Bảo trì ' + st[2] + '</span>' +
      '<span class="p3r-chip">Tổng bãi: ' + totalLot + ' slots</span>';
    if (this.onChange) this.onChange(this.getState());
  };

  /* ------------------------------------------------------------------ */
  /* THREE                                                               */
  /* ------------------------------------------------------------------ */
  P._init3D = function () {
    var self = this;
    this.renderer = new T.WebGLRenderer({ canvas: this.canvas, antialias: true, powerPreference: 'high-performance', preserveDrawingBuffer: /debug=1/.test(root.location.search) });
    this.renderer.setPixelRatio(Math.min(root.devicePixelRatio || 1, 2));
    this.scene = new T.Scene();
    this.scene.background = new T.Color(0x0a0f17);
    this.camera = new T.PerspectiveCamera(68, 1, 0.1, 700);
    this.camera.rotation.order = 'YXZ';
    this.scene.add(new T.AmbientLight(0xdce6f2, 0.56));
    this.scene.add(new T.HemisphereLight(0xeef4ff, 0x2a313a, 0.72));
    this.torch = new T.PointLight(0xfff6e5, 0.68, 45, 1.5);
    this.scene.add(this.torch);
    this.sun = new T.DirectionalLight(0xffffff, 0.28); this.sun.position.set(35, 85, 28); this.scene.add(this.sun);
    this.ray = new T.Raycaster(); this.mouse = new T.Vector2();
    this.dummy = new T.Object3D();
    this.dummy.rotation.order = 'YXZ';

    // hotspot (điểm di chuyển kiểu Street View)
    var ringG = new T.RingGeometry(0.5, 0.72, 40); ringG.rotateX(-Math.PI / 2);
    var discG = new T.CircleGeometry(0.46, 32); discG.rotateX(-Math.PI / 2);
    var chev = new T.Shape(); chev.moveTo(0, 0.42); chev.lineTo(0.34, -0.1); chev.lineTo(0.14, -0.1); chev.lineTo(0, 0.1); chev.lineTo(-0.14, -0.1); chev.lineTo(-0.34, -0.1); chev.closePath();
    var chG = new T.ShapeGeometry(chev); chG.rotateX(-Math.PI / 2);
    for (var i = 0; i < 8; i++) {
      var grp = new T.Group();
      var ringM = new T.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.92, depthWrite: false, polygonOffset: true, polygonOffsetFactor: -6, polygonOffsetUnits: -6 });
      var discM = new T.MeshBasicMaterial({ color: 0x0b1220, transparent: true, opacity: 0.45, depthWrite: false, polygonOffset: true, polygonOffsetFactor: -5, polygonOffsetUnits: -5 });
      var chM = new T.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.95, depthWrite: false, polygonOffset: true, polygonOffsetFactor: -7, polygonOffsetUnits: -7 });
      var ring = new T.Mesh(ringG, ringM), disc = new T.Mesh(discG, discM), ch = new T.Mesh(chG, chM);
      [ring, disc, ch].forEach(function (m) { m.renderOrder = 20; m.position.y = 0.05; grp.add(m); });
      var hit = new T.Mesh(new T.CircleGeometry(1.0, 12), new T.MeshBasicMaterial({ visible: false, side: T.DoubleSide }));
      hit.rotation.x = -Math.PI / 2; hit.position.y = 0.08; grp.add(hit);
      hit.userData.hotspot = i;
      grp.visible = false; grp.userData = { ring: ringM, ch: chM, hit: hit, scale: 1 };
      this.scene.add(grp); this.hotspots.push(grp);
    }
    this._bindInput();
    root.addEventListener('resize', function () { self._resize(); });
    this._resize();
  };

  P._resize = function () {
    var w = this.el.clientWidth || 800, h = this.el.clientHeight || 500;
    this.renderer.setSize(w, h, false);
    this.camera.aspect = w / h; this.camera.updateProjectionMatrix();
    this.w = w; this.h = h;
  };

  /* ------------------------------------------------------------------ */
  /* ĐIỀU KHIỂN                                                          */
  /* ------------------------------------------------------------------ */
  P._bindInput = function () {
    var self = this, cv = this.canvas, drag = null;
    cv.addEventListener('pointerdown', function (e) {
      cv.setPointerCapture(e.pointerId);
      drag = { x: e.clientX, y: e.clientY, sx: e.clientX, sy: e.clientY, t: performance.now(), moved: false };
    });
    cv.addEventListener('pointermove', function (e) {
      if (drag) {
        var dx = e.clientX - drag.x, dy = e.clientY - drag.y;
        drag.x = e.clientX; drag.y = e.clientY;
        if (Math.abs(e.clientX - drag.sx) + Math.abs(e.clientY - drag.sy) > 5) drag.moved = true;
        if (self.mode === 'walk') {
          self.walk.yaw += dx * 0.0042; self.walk.pitch = clamp(self.walk.pitch + dy * 0.0035, -1.2, 1.2);
        } else {
          self.orbit.theta -= dx * 0.006; self.orbit.phi = clamp(self.orbit.phi - dy * 0.005, 0.12, 1.5);
        }
      } else self._hover(e);
    });
    cv.addEventListener('pointerup', function (e) {
      if (drag && !drag.moved && performance.now() - drag.t < 600) self._click(e);
      drag = null;
    });
    cv.addEventListener('wheel', function (e) {
      e.preventDefault();
      if (self.mode === 'orbit') self.orbit.r = clamp(self.orbit.r * (1 + e.deltaY * 0.0012), 18, 260);
      else { self.camera.fov = clamp(self.camera.fov + e.deltaY * 0.03, 40, 90); self.camera.updateProjectionMatrix(); }
    }, { passive: false });
    root.addEventListener('keydown', function (e) {
      if (self.mode !== 'walk' || /INPUT|SELECT|TEXTAREA/.test((e.target.tagName || ''))) return;
      var k = e.key;
      if (k === 'ArrowLeft' || k === 'a') self.walk.yaw += 0.12;
      else if (k === 'ArrowRight' || k === 'd') self.walk.yaw -= 0.12;
      else if (k === 'ArrowUp' || k === 'w') self._step(1);
      else if (k === 'ArrowDown' || k === 's') self._step(-1);
    });
  };

  P._pointer = function (e) {
    var r = this.canvas.getBoundingClientRect();
    this.mouse.set(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1);
    this.ray.setFromCamera(this.mouse, this.camera);
  };
  P._hover = function (e) {
    if (this.mode !== 'walk' || this.tween) { this.canvas.style.cursor = this.mode === 'orbit' ? 'grab' : 'default'; return; }
    this._pointer(e);
    var hits = this.ray.intersectObjects(this.hotspots.filter(function (h) { return h.visible; }).map(function (h) { return h.userData.hit; }), false);
    var gate = this._pickGate();
    this.hoverHS = hits.length ? hits[0].object.userData.hotspot : -1;
    this.canvas.style.cursor = (this.hoverHS >= 0 || gate) ? 'pointer' : 'grab';
  };
  P._pickGate = function () {
    if (!this.lot) return null;
    var objs = []; this.lot.gates.forEach(function (g) { objs = objs.concat(g.pick); });
    var h = this.ray.intersectObjects(objs, false);
    return h.length && h[0].distance < 14 ? h[0].object.userData.gate : null;
  };
  P._click = function (e) {
    if (this.lifting) return;
    this._pointer(e);
    if (this.mode === 'walk' && !this.tween) {
      var hits = this.ray.intersectObjects(this.hotspots.filter(function (h) { return h.visible; }).map(function (h) { return h.userData.hit; }), false);
      if (hits.length) { var hs = this.hotspots[hits[0].object.userData.hotspot]; this._moveTo(hs.userData.nodeId); return; }
    }
    var g = this._pickGate();
    if (g) g.forced = !g.forced;
  };
  P._step = function (dir) {
    if (this.tween || !this.node) return;
    var f = this._fwd(), best = null, bd = dir > 0 ? 0.55 : 0.55, self = this;
    this.node.nb.forEach(function (id) {
      var n = self.lot.nodes[id], dx = n.x - self.node.x, dz = n.z - self.node.z, L = Math.sqrt(dx * dx + dz * dz) || 1;
      var dot = (dx * f.x + dz * f.z) / L * dir;
      if (dot > bd) { bd = dot; best = n; }
    });
    if (best) this._moveTo(best.id);
  };
  P._fwd = function () { return { x: -Math.sin(this.walk.yaw), z: -Math.cos(this.walk.yaw) }; };

  /* ------------------------------------------------------------------ */
  /* TRẠNG THÁI: BÃI / TẦNG / VỊ TRÍ                                     */
  /* ------------------------------------------------------------------ */
  P.setRole = function (role) { this.role = role; };
  P.setLot = function (id) {
    var def = LOTS.filter(function (l) { return l.id === id; })[0] || LOTS[0];
    if (this.lot) {
      this.scene.remove(this.lot.group);
      this.lot.group.traverse(function (o) { if (o.geometry && o.geometry.dispose) o.geometry.dispose(); });
    }
    this.lot = I.buildLot(def, T);
    this.scene.add(this.lot.group);
    this.elLot.value = def.id;
    this.selFloor = 0;
    this._renderFloorPills();
    var n = this.lot.entryNode;
    this._place(n, this.lot.pois && this.lot.pois.entry ? this.lot.pois.entry.yaw : Math.PI / 2, -0.04);
    this.orbit.target.set(0, this.lot.ys[0], 0);
    this.orbit.r = def.type === 'underground' ? 128 : 138;
    this._applyVisibility();
    this._syncFloorPills();
    if (this.mode === 'orbit') this._beginTransition();
  };
  P._place = function (node, yaw, pitch) {
    this.node = node; this.floorIdx = node.floorIdx;
    this.walk.pos.set(node.x, node.y + C.EYE, node.z);
    if (yaw != null) this.walk.yaw = yaw;
    if (pitch != null) this.walk.pitch = pitch;
    this.tween = null; this._showHotspots();
    this._syncFloorPills();
  };
  P._applyVisibility = function () {
    var self = this;
    this.lot.floors.forEach(function (fl) {
      if (self.mode === 'walk') { fl.group.visible = true; fl.ceil.visible = true; }
      else {
        fl.group.visible = self.lot.under ? (fl.k >= self.selFloor) : (fl.k <= self.selFloor);
        fl.ceil.visible = false;
      }
    });
    this.sun.intensity = this.mode === 'orbit' ? 0.78 : 0.28;
    this.torch.intensity = this.mode === 'walk' ? 0.68 : 0;
    this.scene.fog = this.mode === 'walk' ? new T.Fog(0x10151c, 55, 185) : null;
    this.scene.background.set(this.mode === 'walk' ? 0x10151c : 0x161f2c);
  };
  P.setMode = function (m) {
    if (m === this.mode) return;
    this._beginTransition();
    this.mode = m;
    if (m === 'orbit') {
      this.selFloor = this.floorIdx;
      this.orbit.target.set(0, this.lot.ys[this.selFloor] + 1, 0);
      this.hotspots.forEach(function (h) { h.visible = false; });
      this.elLift.hidden = true;
    } else this._showHotspots();
    this._applyVisibility();
    this.canvas.style.cursor = m === 'orbit' ? 'grab' : 'default';
    this._syncFloorPills();
  };
  P.setFloor = function (k) {
    if (this.lifting) return;
    if (this.mode === 'orbit') {
      this._beginTransition();
      this.selFloor = k;
      this.orbit.target.set(0, this.lot.ys[k] + 1, 0);
      this._applyVisibility(); this._syncFloorPills(); return;
    }
    var cur = this.node, target = I.nearestNode(this.lot, k, cur.x, cur.z);
    this._fadeTo('Đang chuyển tới tầng ' + this.lot.def.ids[k] + '…', function () { this._place(target, null, null); }.bind(this));
  };
  P._fadeTo = function (text, mid) {
    var self = this;
    this.lifting = true;
    this.elFade.firstChild.textContent = text; this.elFade.classList.add('on');
    setTimeout(function () { mid(); setTimeout(function () { self.elFade.classList.remove('on'); self.lifting = false; }, 250); }, 520);
  };
  P._goTo = function (what) {
    if (this.lifting) return;
    if (this.mode !== 'walk') this.setMode('walk');
    var self = this, node = null, yaw = null;
    if (what === 'exit') what = 'checkout';
    if (what === 'core') {
      var best = null, bd = 1e12;
      this.lot.cores.forEach(function (c) {
        if (c.floorIdx !== self.floorIdx) return;
        var d = Math.pow(c.pos.x - self.walk.pos.x, 2) + Math.pow(c.pos.z - self.walk.pos.z, 2);
        if (d < bd) { bd = d; best = c; }
      });
      if (!best) best = this.lot.cores[0];
      if (!best) return;
      node = this.lot.nodes[best.nodeId]; yaw = best.yaw;
    } else if (this.lot.pois && this.lot.pois[what] && this.lot.pois[what].node) {
      node = this.lot.pois[what].node; yaw = this.lot.pois[what].yaw;
    } else {
      node = this.lot.entryNode; yaw = Math.PI / 2;
    }
    this._fadeTo('Đang di chuyển…', function () { self._place(node, yaw, -0.05); });
  };

  P._moveTo = function (id) {
    var n = this.lot.nodes[id];
    this.tween = { from: this.walk.pos.clone(), to: new T.Vector3(n.x, n.y + C.EYE, n.z), t0: performance.now(), dur: 650, node: n };
    this.hotspots.forEach(function (h) { h.visible = false; });
  };

  P._showHotspots = function () {
    var self = this, nb = this.node ? this.node.nb : [];
    this.hotspots.forEach(function (h, i) {
      if (self.mode !== 'walk' || i >= nb.length) { h.visible = false; return; }
      var n = self.lot.nodes[nb[i]], dx = n.x - self.node.x, dz = n.z - self.node.z;
      h.visible = true; h.position.set(n.x, n.y, n.z);
      h.userData.nodeId = n.id;
      // chevron hướng về phía đi tới; vòng tròn nằm tại điểm đến
      h.rotation.y = Math.atan2(-dx, -dz);
    });
  };

  /* ------------------------------------------------------------------ */
  /* VÒNG LẶP                                                            */
  /* ------------------------------------------------------------------ */
  P._beginTransition = function () {
    this.tr = { p: this.camera.position.clone(), q: this.camera.quaternion.clone(), t0: performance.now(), dur: 1000 };
  };
  P._targetPose = function () {
    if (this.mode === 'walk') {
      this.dummy.rotation.set(this.walk.pitch, this.walk.yaw, 0, 'YXZ');
      return { pos: this.walk.pos, quat: this.dummy.quaternion.clone() };
    }
    var o = this.orbit, sp = Math.sin(o.phi), pos = new T.Vector3(o.target.x + o.r * sp * Math.sin(o.theta), o.target.y + o.r * Math.cos(o.phi), o.target.z + o.r * sp * Math.cos(o.theta));
    var m = new T.Matrix4().lookAt(pos, o.target, new T.Vector3(0, 1, 0));
    return { pos: pos, quat: new T.Quaternion().setFromRotationMatrix(m) };
  };

  P._loop = function (now) {
    if (this.destroyed) return;
    requestAnimationFrame(this._loop);
    if (!this.lot) return;
    now = now || performance.now();
    if (this.tween) {
      var tw = this.tween, t = clamp((now - tw.t0) / tw.dur, 0, 1), e = ease(t);
      this.walk.pos.lerpVectors(tw.from, tw.to, e);
      if (t >= 1) { this.tween = null; this.node = tw.node; this.floorIdx = tw.node.floorIdx; this._showHotspots(); this._syncFloorPills(); }
    }
    var tg = this._targetPose();
    if (this.tr) {
      var k = clamp((now - this.tr.t0) / this.tr.dur, 0, 1), ek = ease(k);
      this.camera.position.lerpVectors(this.tr.p, tg.pos, ek);
      this.camera.quaternion.copy(this.tr.q).slerp(tg.quat, ek);
      if (k >= 1) this.tr = null;
    } else { this.camera.position.copy(tg.pos); this.camera.quaternion.copy(tg.quat); }
    this.torch.position.copy(this.camera.position);

    this._animateProps(now);
    this._updateHotspotLook(now);
    this._updateLabels();
    this._updateLift();
    this._updateWhere();
    this.renderer.render(this.scene, this.camera);
  };

  P._animateProps = function (now) {
    var cp = this.camera.position, walk = this.mode === 'walk';
    this.lot.gates.forEach(function (g) {
      var d = Math.hypot(cp.x - g.pos.x, cp.z - g.pos.z) < 7.5 && Math.abs(cp.y - g.pos.y) < 4 && walk;
      g.open = !!g.forced || d;
      var target = g.open ? (g.openAngle || Math.PI / 2) : 0;
      g.lift.rotation.x += (target - g.lift.rotation.x) * 0.12;
      g.grM.color.setHex(g.open ? 0x22e06a : 0x0b3d1d);
      g.redM.color.setHex(g.open ? 0x4a1010 : 0xff2a2a);
    });
    this.lot.cores.forEach(function (c) {
      var d = Math.hypot(cp.x - c.pos.x, cp.z - c.pos.z) < 3.8 && Math.abs(cp.y - c.pos.y) < 4 && walk;
      c.open += ((d ? 1 : 0) - c.open) * 0.10;
      if (c.slide) {
        c.slide.forEach(function (s) { s.mesh.position.x = s.base + s.dir * s.dist * c.open; });
      }
    });
  };
  P._updateHotspotLook = function (now) {
    var self = this, pulse = 1 + Math.sin(now / 320) * 0.06;
    this.hotspots.forEach(function (h, i) {
      if (!h.visible) return;
      var hov = self.hoverHS === i;
      var s = (hov ? 1.35 : 1) * pulse; h.scale.set(s, s, s);
      h.userData.ring.color.setHex(hov ? 0x38bdf8 : 0xffffff);
      h.userData.ch.color.setHex(hov ? 0x38bdf8 : 0xffffff);
    });
  };

  /* Chỉ hiện thông tin xe/slot khi người dùng đã tới gần */
  P._plateText = function (s) {
    if (!s.plate) return '';
    if (this.role === 'admin') return s.plate;
    if (this.role === 'user') return s.plate.replace(/\d{3}\.(\d\d)$/, '***.$1');
    return 'Đăng nhập để xem biển số';
  };
  P._updateLabels = function () {
    var pool = this.labelPool, i;
    if (this.mode !== 'walk' || this.lifting) { pool.forEach(function (d) { d.style.display = 'none'; }); return; }
    var fl = this.lot.floors[this.floorIdx], cp = this.camera.position, f = this._fwd(), cand = [], self = this;
    fl.slots.forEach(function (s) {
      var dx = s.x - cp.x, dz = s.zc - cp.z, d = Math.sqrt(dx * dx + dz * dz);
      if (d > 11 || d < 0.5) return;
      var dot = (dx * f.x + dz * f.z) / d;
      if (dot < 0.35) return;
      cand.push({ s: s, d: d });
    });
    cand.sort(function (a, b) { return a.d - b.d; });
    var v = new T.Vector3();
    for (i = 0; i < pool.length; i++) {
      var el = pool[i], c = cand[i];
      if (!c) { el.style.display = 'none'; continue; }
      v.copy(c.s.world); v.y += 0.5; v.project(this.camera);
      if (v.z > 1 || Math.abs(v.x) > 1.05 || Math.abs(v.y) > 1.05) { el.style.display = 'none'; continue; }
      var near = c.d < 7, s = c.s;
      var html = '<b>' + s.code + '</b><em class="z ' + (s.zone === 'MONTHLY' ? 'm' : 'v') + '">' + (s.zone === 'MONTHLY' ? 'Xe tháng' : 'Vãng lai') + '</em>';
      if (near) {
        html += '<span class="st ' + s.status.toLowerCase() + '">' + STATUS_TXT[s.status] + '</span>';
        if (s.status === 'OCCUPIED') html += '<span class="pl">' + esc(this._plateText(s)) + '</span><small>' + esc(s.vehicle) + '</small>';
      }
      var key = html;
      if (el._k !== key) { el.innerHTML = html; el._k = key; }
      el.style.display = 'block';
      el.style.opacity = clamp(1.25 - c.d / 11, 0.35, 1);
      el.style.transform = 'translate(' + ((v.x * 0.5 + 0.5) * this.w) + 'px,' + ((-v.y * 0.5 + 0.5) * this.h) + 'px) translate(-50%,-100%)';
    }
  };

  /* Nút Lên / Xuống CHỈ hiện khi người dùng đã đi VÀO BÊN TRONG cánh cửa phòng thang máy */
  P._nearCore = function () {
    if (this.mode !== 'walk' || this.lifting || this.tween) return null;
    if (!this.node || this.node.type !== 'core') return null;
    var cp = this.walk.pos, best = null, bd = 2.0, self = this;
    this.lot.cores.forEach(function (c) {
      if (c.floorIdx !== self.floorIdx) return;
      var d = Math.hypot(cp.x - c.pos.x, cp.z - c.pos.z);
      if (d < bd) { bd = d; best = c; }
    });
    return best;
  };
  P._updateLift = function () {
    var c = this._nearCore(), el = this.elLift;
    if (!c) { if (!el.hidden) { el.hidden = true; el._k = ''; } return; }
    var def = this.lot.def, cur = this.floorIdx, key = c.id + '|' + cur;
    if (el._k !== key) {
      el._k = key; var self = this, h = '<div class="p3r-lift-t">🛗 ' + esc(c.name) + ' · Đang ở <b>' + def.ids[cur] + '</b></div><div class="p3r-lift-b">';
      var ks = def.ids.map(function (_, k) { return k; }).filter(function (k) { return k !== cur; });
      ks.forEach(function (k) {
        var up = this.lot.ys[k] > this.lot.ys[cur];
        h += '<button data-k="' + k + '" class="' + (up ? 'up' : 'down') + '">' + (up ? '▲ Lên ' : '▼ Xuống ') + def.ids[k] + '</button>';
      }, this);
      el.innerHTML = h + '</div>';
      el.querySelectorAll('button').forEach(function (b) { b.addEventListener('click', function () { self._lift(+b.getAttribute('data-k'), c); }); });
    }
    el.hidden = false;
  };
  P._lift = function (k, core) {
    var self = this, lot = this.lot, dest = null;
    lot.cores.forEach(function (c) { if (c.floorIdx === k && c.id === core.id) dest = c; });
    if (!dest) return;
    var up = lot.ys[k] > lot.ys[this.floorIdx];
    this.elLift.hidden = true; this.elLift._k = '';
    this._fadeTo((up ? '▲ Đang lên tầng ' : '▼ Đang xuống tầng ') + lot.def.ids[k] + '…', function () {
      self._place(lot.nodes[dest.nodeId], dest.yaw, -0.05);
    });
  };

  P._updateWhere = function () {
    var el = this.elWhere;
    if (this.mode !== 'walk') {
      var fl = this.lot.floors[this.selFloor];
      var t0 = 'Toàn cảnh · cắt tầng ' + fl.id + ' (' + (fl.kind === 'MONTHLY' ? 'Khu xe tháng + Lane bypass vãng lai' : 'Khu xe vãng lai') + ' · ' + fl.slots.length + ' slots)';
      if (el._k !== t0) { el.textContent = t0; el._k = t0; }
      return;
    }
    var p = this.walk.pos;
    var txt = '📍 ' + this.lot.describe(p.x, p.z, this.floorIdx);
    if (el._k !== txt) { el.textContent = txt; el._k = txt; }
  };

  P.getState = function () {
    return { lot: this.lot && this.lot.def.id, floor: this.lot && this.lot.def.ids[this.mode === 'walk' ? this.floorIdx : this.selFloor], mode: this.mode, role: this.role };
  };
  P.destroy = function () { this.destroyed = true; this.renderer.dispose(); this.el.innerHTML = ''; };

  root.ParkingRender = ParkingRender;
  root.ParkingRender.ROLE_LABEL = ROLE_LABEL;
})(typeof self !== 'undefined' ? self : this);
