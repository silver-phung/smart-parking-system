/**
 * SmartPark 3D Render - bộ dựng TẦNG / BÃI (floor + lot builders)
 * Phụ thuộc: parking-3d-render-kit.js (hằng số, vật liệu, tiện ích)
 */
(function (root) {
  'use strict';
  var I = root.__P3R_INTERNAL__;
  var C = I.C, U = I.util, M = I.M, ROWS = I.ROWS, AISLE_Z = I.AISLE_Z, SLOT_X = I.SLOT_X, GAP_X = I.GAP_X, COL_X = I.COL_X;
  var FH = C.FH, CLEAR = C.CLEAR, SLAB = C.SLAB, SW = C.SW, SL = C.SL, HD = C.HD, XM = C.XM, XT = C.XT, RAMP_L = C.RAMP_L,
    RB0 = C.RB0, RB1 = C.RB1, RBC = C.RBC, ROW_LEN = C.ROW_LEN, GAPW = C.GAPW, END = C.END, MONTHLY_MAX = C.MONTHLY_MAX;
  var ZONE_X = I.ZONE_X, ENTRY_Z = I.ENTRY_Z, EXIT_Z = I.EXIT_Z;
  var T;

  /* ------------------------------------------------------------------ */
  function extraMaterials() {
    if (M.paintInst) return;
    M.paintInst = new T.MeshBasicMaterial({ color: 0xffffff, polygonOffset: true, polygonOffsetFactor: -2, polygonOffsetUnits: -2 });
    M.sepInst = new T.MeshStandardMaterial({ color: 0xffffff, roughness: 0.7 });
    M.pad = new T.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.3, depthWrite: false, polygonOffset: true, polygonOffsetFactor: -1, polygonOffsetUnits: -1 });
    M.bandBlue = new T.MeshStandardMaterial({ color: 0x1f5fae, roughness: 0.9, side: T.DoubleSide });
    M.bandGreen = new T.MeshStandardMaterial({ color: 0x1e8f4e, roughness: 0.9, side: T.DoubleSide });
    M.fin = new T.MeshStandardMaterial({ color: 0xd9dadc, roughness: 0.8 });
    M.tile = new T.MeshStandardMaterial({ color: 0xd8d3c8, roughness: 0.5 });
  }

  /* ---------- Thang máy sát tường + tiền sảnh kính ---------- */
  function buildCore(fl, gx, side) {
    var g = new T.Group();
    g.position.set(gx, 0, side > 0 ? -HD : HD);
    g.rotation.y = side > 0 ? 0 : Math.PI;           // local +z hướng ra lối đi
    var W = 5.2, H = CLEAR;
    // thân phòng thang (3m sâu) sát tường
    box3(g, 0.2, H, 3, M.plaster, -W / 2 + 0.1, H / 2, 1.5);
    box3(g, 0.2, H, 3, M.plaster, W / 2 - 0.1, H / 2, 1.5);
    box3(g, 1.6, H, 0.2, M.plaster, -1.8, H / 2, 2.9);
    box3(g, 1.6, H, 0.2, M.plaster, 1.8, H / 2, 2.9);
    box3(g, 2.0, H - 2.4, 0.2, M.plaster, 0, 2.4 + (H - 2.4) / 2, 2.9);
    // cabin sáng bên trong
    box3(g, 2.0, 2.3, 2.4, M.cabin, 0, 1.2, 1.6);
    // cửa thép trượt
    var dl = box3(g, 1.0, 2.4, 0.06, M.steel, -0.5, 1.2, 2.98);
    var dr = box3(g, 1.0, 2.4, 0.06, M.steel, 0.5, 1.2, 2.98);
    // khung cửa
    box3(g, 2.1, 0.1, 0.25, M.frame, 0, 2.45, 2.92);
    // bảng nút gọi (Lên/Xuống) sát tường
    var panel = box3(g, 0.16, 0.36, 0.05, M.dark, 1.5, 1.15, 3.03);
    box3(g, 0.07, 0.07, 0.02, M.green, 1.5, 1.25, 3.07);
    box3(g, 0.07, 0.07, 0.02, M.tail, 1.5, 1.07, 3.07);
    // sàn tiền sảnh + trần
    var tile = new T.Mesh(new T.PlaneGeometry(W, 2.0), M.tile);
    tile.rotation.x = -Math.PI / 2; tile.position.set(0, 0.012, 4.0); g.add(tile);
    box3(g, W, 0.12, 2.1, M.plaster, 0, 3.95, 4.0);
    box3(g, 1.6, 0.04, 0.3, M.lamp, 0, 3.86, 4.0);
    // KÍNH TRONG SUỐT bao ngoài tiền sảnh (2 bên + mặt trước chừa cửa)
    function pane(w, h, x, y, z, ry) {
      var m = new T.Mesh(new T.PlaneGeometry(w, h), M.glass);
      m.position.set(x, y, z); m.rotation.y = ry || 0; g.add(m);
    }
    pane(2.0, 3.9, -W / 2 + 0.02, 1.95, 4.0, Math.PI / 2);
    pane(2.0, 3.9, W / 2 - 0.02, 1.95, 4.0, Math.PI / 2);
    pane(1.7, 3.9, -1.75, 1.95, 5.0 - 0.02, 0);
    pane(1.7, 3.9, 1.75, 1.95, 5.0 - 0.02, 0);
    pane(1.8, 1.9, 0, 3.0, 5.0 - 0.02, 0);   // ô thoáng trên cửa ra vào
    // khung nhôm
    [-W / 2 + 0.03, -0.9, 0.9, W / 2 - 0.03].forEach(function (x) { box3(g, 0.06, 3.95, 0.06, M.frame, x, 1.975, 5.0 - 0.03); });
    [3.9].forEach(function (y) { box3(g, W, 0.06, 0.06, M.frame, 0, y, 5.0 - 0.03); });
    box3(g, 1.8, 0.05, 0.06, M.frame, 0, 2.05, 5.0 - 0.03);
    [3.0, 5.0].forEach(function (z) { box3(g, 0.06, 3.95, 0.06, M.frame, -W / 2 + 0.03, 1.975, z - 0.03 * (z > 4 ? 1 : -1)); box3(g, 0.06, 3.95, 0.06, M.frame, W / 2 - 0.03, 1.975, z - 0.03 * (z > 4 ? 1 : -1)); });
    // dải xanh lá chỉ lối đi bộ
    var mat = new T.Mesh(new T.PlaneGeometry(1.3, 1.8), M.paintGreen);
    mat.rotation.x = -Math.PI / 2; mat.position.set(0, 0.02, 4.0); g.add(mat);
    fl.group.add(g);
    var world = new T.Vector3(gx, fl.y, side > 0 ? -HD + 4.0 : HD - 4.0);
    fl.cores.push({ x: gx, z: world.z, side: side, doors: [dl, dr], open: 0, group: g, pos: world });
  }
  function box3(p, w, h, d, m, x, y, z) { return U.box(p, w, h, d, m, x, y, z); }

  /* ---------- Cổng quẹt thẻ + barrier ---------- */
  function readerTex(line1, line2, color) {
    return U.signTex([line1, line2], '#0a1a2a', color || '#6fe3ff', 256, 160);
  }
  function buildGate(parent, fl, o) {
    // o: x, z, heading(±1), kind, label, readerLine
    var g = new T.Group();
    g.position.set(o.x, 0, o.z);
    g.rotation.y = o.heading > 0 ? 0 : Math.PI;
    var isZone = o.kind === 'zone';
    // đảo bê tông
    box3(g, isZone ? 3.4 : 5.4, 0.18, 1.0, M.conc, 0, 0.09, 2.15);
    box3(g, isZone ? 3.4 : 5.4, 0.04, 1.02, M.hazard, 0, 0.19, 2.15);
    // tủ barrier
    box3(g, 0.42, 1.05, 0.38, M.red, 0, 0.72, 2.05);
    box3(g, 0.44, 0.06, 0.4, M.dark, 0, 1.27, 2.05);
    // đèn tín hiệu
    var redM = new T.MeshBasicMaterial({ color: 0xff2a2a }), grM = new T.MeshBasicMaterial({ color: 0x0b3d1d });
    box3(g, 0.1, 0.1, 0.02, redM, 0, 1.12, 1.85); box3(g, 0.1, 0.1, 0.02, grM, 0, 0.96, 1.85);
    // cần chắn: trục quay ngang theo hướng xe chạy (x), cần dài dọc -z
    var lift = new T.Group(); lift.position.set(0, 1.12, 1.78); g.add(lift);
    var arm = U.box(lift, 0.1, 0.11, 3.4, M.barrier, 0, 0, -1.7);
    // cột quẹt thẻ (trước barrier)
    var rd = new T.Group(); rd.position.set(isZone ? -1.2 : -1.9, 0, 2.1); g.add(rd);
    box3(rd, 0.3, 1.1, 0.3, M.dark, 0, 0.73, 0);
    box3(rd, 0.34, 0.06, 0.34, M.steel, 0, 1.31, 0);
    var scr = new T.Mesh(new T.PlaneGeometry(0.26, 0.17), U.signMat(readerTex(o.readerLine || 'QUẸT THẺ', 'TAP CARD')));
    scr.position.set(0, 1.1, -0.155); scr.rotation.y = Math.PI; rd.add(scr);
    box3(rd, 0.16, 0.05, 0.02, M.green, 0, 0.85, -0.16);
    // card reader chạm (sensor)
    box3(rd, 0.14, 0.02, 0.14, M.cabin, 0, 1.345, 0);
    if (!isZone) {
      // booth bảo vệ
      var b = new T.Group(); b.position.set(2.0, 0, 2.15); g.add(b);
      box3(b, 1.2, 1.0, 0.9, M.plaster, 0, 0.5, 0);
      var gl = new T.Mesh(new T.BoxGeometry(1.2, 1.0, 0.9), M.glass); gl.position.set(0, 1.5, 0); b.add(gl);
      [[-0.58, -0.43], [0.58, -0.43], [-0.58, 0.43], [0.58, 0.43]].forEach(function (c) { box3(b, 0.05, 1.0, 0.05, M.frame, c[0], 1.5, c[1]); });
      box3(b, 1.5, 0.1, 1.2, M.dark, 0, 2.05, 0);
      box3(b, 0.7, 0.04, 0.5, M.lamp, 0, 1.98, 0);
      // cột đèn + biển
      cyl3(b, 0.04, 1.2, M.steel, 0.55, 2.7, 0.35);
      var lbl = new T.Mesh(new T.PlaneGeometry(2.2, 0.62), U.signMat(U.signTex([o.label, o.label2 || ''], o.kind === 'entry' ? '#0b7a3b' : '#a31818', '#ffffff', 512, 160)));
      lbl.position.set(0, 3.3, 0.35);
      lbl.rotation.y = -Math.PI / 2;                 // đối diện xe chạy tới (−x local)
      b.add(lbl);
      var lbl2 = lbl.clone(); lbl2.rotation.y = Math.PI / 2; lbl2.position.set(0, 3.3, 0.35); b.add(lbl2);
    }
    parent.add(g);
    var gate = { kind: o.kind, group: g, lift: lift, open: false, manual: null, t: 0, redM: redM, grM: grM,
      pos: new T.Vector3(o.x, fl.y, o.z), label: o.label };
    arm.userData.gate = gate; scr.userData.gate = gate;
    gate.pick = [arm, scr];
    fl.gates.push(gate);
    return gate;
  }
  function cyl3(p, r, l, m, x, y, z) { return U.cyl(p, r, l, m, x, y, z); }

  /* ---------- Cửa thoát hiểm ---------- */
  function emergencyDoor(fl, x, wallZ, dir) {
    var g = new T.Group(); g.position.set(x, 0, wallZ); g.rotation.y = dir > 0 ? 0 : Math.PI;
    box3(g, 1.7, 2.35, 0.14, M.frame, 0, 1.175, 0.07);
    box3(g, 1.4, 2.2, 0.08, M.exitGreen, 0, 1.1, 0.16);
    box3(g, 0.3, 0.05, 0.05, M.steel, 0.5, 1.05, 0.22);
    var s = new T.Mesh(new T.PlaneGeometry(1.1, 0.4), U.signMat(U.signTex(['THOÁT HIỂM', 'EXIT'], '#0b8a43', '#ffffff', 256, 96)));
    s.position.set(0, 2.65, 0.1); g.add(s);
    // đèn exit phát sáng
    fl.group.add(g);
  }

  /* ---------- Chữ mã slot trên sàn (atlas) ---------- */
  function labelAtlas(fl) {
    var n = fl.slots.length, cols = 10, rows = Math.ceil(n / cols), cw = 128, ch = 64;
    var tex = U.canvasTex(cols * cw, rows * ch, function (c) {
      c.clearRect(0, 0, cols * cw, rows * ch);
      c.textAlign = 'center'; c.textBaseline = 'middle'; c.font = 'bold 34px Arial, sans-serif';
      fl.slots.forEach(function (s, i) {
        var x = (i % cols) * cw + cw / 2, y = Math.floor(i / cols) * ch + ch / 2;
        c.fillStyle = s.zone === 'MONTHLY' ? '#8ec5ff' : '#ffffff';
        c.fillText(s.code, x, y);
      });
    });
    var pos = [], uv = [], idx = [];
    fl.slots.forEach(function (s, i) {
      var f = s.face, w = 1.5, h = 0.6;
      var cx = s.x, cz = s.zc + f * (SL / 2 + 0.7);
      var rx = f > 0 ? 1 : -1, uz = f > 0 ? -1 : 1;
      var p = [[cx - rx * w / 2, cz - uz * h / 2], [cx + rx * w / 2, cz - uz * h / 2], [cx + rx * w / 2, cz + uz * h / 2], [cx - rx * w / 2, cz + uz * h / 2]];
      var u0 = (i % cols) / cols, u1 = u0 + 1 / cols, v1 = 1 - Math.floor(i / cols) / rows, v0 = v1 - 1 / rows;
      var b = pos.length / 3;
      p.forEach(function (q) { pos.push(q[0], 0.014, q[1]); });
      uv.push(u0, v0, u1, v0, u1, v1, u0, v1);
      idx.push(b, b + 1, b + 2, b, b + 2, b + 3);
    });
    var geo = new T.BufferGeometry();
    geo.setAttribute('position', new T.Float32BufferAttribute(pos, 3));
    geo.setAttribute('uv', new T.Float32BufferAttribute(uv, 2));
    geo.setIndex(idx);
    var mat = new T.MeshBasicMaterial({ map: tex, transparent: true, side: T.DoubleSide, depthWrite: false, polygonOffset: true, polygonOffsetFactor: -3, polygonOffsetUnits: -3 });
    var mesh = new T.Mesh(geo, mat);
    fl.group.add(mesh);
  }

  /* ---------- Bảng IoT hiển thị biển số (khu xe tháng) ---------- */
  function iotBoards(fl) {
    var poles = [], bodies = [];
    fl.slots.forEach(function (s) {
      if (s.zone !== 'MONTHLY') return;
      var f = s.face, back = s.zc - f * SL / 2;
      var wallMounted = (s.row === 'A' || s.row === 'D');
      var z = back + f * 0.1, y = 2.0;
      if (!wallMounted) poles.push({ x: s.x, y: 1.0, z: back + f * 0.03, sx: 0.06, sy: 2.0, sz: 0.06, color: 0x3a3f47 });
      bodies.push({ x: s.x, y: y, z: z, sx: 0.84, sy: 0.4, sz: 0.06, color: 0x16191e });
      var occupied = s.status === 'OCCUPIED';
      var tex = U.canvasTex(192, 96, function (c, w, h) {
        c.fillStyle = '#05080c'; c.fillRect(0, 0, w, h);
        c.textAlign = 'center'; c.textBaseline = 'middle';
        c.fillStyle = '#9fb3c8'; c.font = 'bold 20px Arial'; c.fillText(s.code, w / 2, 20);
        if (occupied) { c.fillStyle = '#ffd34d'; c.font = 'bold 30px Arial'; c.fillText(s.plate, w / 2, 58); c.fillStyle = '#ff5050'; c.fillRect(8, h - 14, w - 16, 6); }
        else { c.fillStyle = '#35e07a'; c.font = 'bold 34px Arial'; c.fillText('TRỐNG', w / 2, 58); c.fillStyle = '#35e07a'; c.fillRect(8, h - 14, w - 16, 6); }
      });
      var scr = new T.Mesh(new T.PlaneGeometry(0.78, 0.34), new T.MeshBasicMaterial({ map: tex }));
      scr.position.set(s.x, y, z + f * 0.034); scr.rotation.y = f > 0 ? 0 : Math.PI;
      fl.group.add(scr);
    });
    U.inst(fl.group, U.unitBox(), M.sepInst, poles);
    U.inst(fl.group, U.unitBox(), M.sepInst, bodies);
  }

  /* ================================================================== */
  /*  1 TẦNG                                                             */
  /* ================================================================== */
  function buildFloor(lot, k) {
    var def = lot.def, n = def.ids.length, fid = def.ids[k], y = lot.ys[k];
    var under = def.type === 'underground';
    var g = new T.Group(); g.position.y = y;
    var ceil = new T.Group(); g.add(ceil);
    var own = k < n - 1 ? (k % 2 === 0 ? 1 : -1) : 0;      // phía có dốc xuống tầng dưới
    var land = k >= 1 ? ((k - 1) % 2 === 0 ? 1 : -1) : 0;  // phía chân dốc từ tầng trên xuống
    var exitDir = k === 0 ? -1 : land;
    var fl = { k: k, id: fid, y: y, group: g, ceil: ceil, own: own, land: land, exitDir: exitDir,
      slots: [], cores: [], gates: [], nodeIds: [] };

    /* ---- sàn ---- */
    U.floorRect(g, -XM, XM, -HD, HD, 0);
    [-1, 1].forEach(function (s) {
      var x0 = s > 0 ? XM : -XT, x1 = s > 0 ? XT : -XM;
      if (own === s) {
        U.floorRect(g, x0, x1, -HD, RB0, 0);
        U.floorRect(g, x0, x1, RB1, HD, 0);
        [RB0, RB1].forEach(function (zz) {
          var len = x1 - x0, cx = (x0 + x1) / 2;
          U.box(g, len, 1.0, 0.25, M.conc, cx, 0.5, zz + (zz === RB0 ? 0.125 * -1 : 0.125));
          U.box(g, len, 0.12, 0.27, M.hazard, cx, 1.04, zz + (zz === RB0 ? -0.125 : 0.125));
        });
      } else {
        U.floorRect(g, x0, x1, -HD, HD, 0);
      }
    });

    /* ---- dốc xe lên/xuống tầng dưới (2 làn song song) ---- */
    if (own) {
      var s = own, th = Math.atan2(FH, RAMP_L), Lr = Math.sqrt(RAMP_L * RAMP_L + FH * FH);
      var rg = new T.Group();
      rg.position.set(s * (XM + RAMP_L / 2), -FH / 2, RBC);
      rg.rotation.z = -s * th;
      U.box(rg, Lr, 0.5, 8, M.slab, 0, -0.25, 0);
      var top = U.texPlane(Lr, 8, M.floor, Lr / 4, 2); top.rotation.x = -Math.PI / 2; top.position.y = 0.004; rg.add(top);
      var dv = U.box(rg, Lr, 0.01, 0.16, M.paintYellow, 0, 0.012, 0);
      U.box(rg, Lr, 0.18, 0.3, M.hazard, 0, 0.09, 3.85);
      U.box(rg, Lr, 0.18, 0.3, M.hazard, 0, 0.09, -3.85);
      [-6, 5].forEach(function (lx) {
        U.floorArrow(rg, lx, 0, -2, s > 0 ? -Math.PI / 2 : Math.PI / 2);   // làn xuống
        U.floorArrow(rg, lx, 0, 2, s > 0 ? Math.PI / 2 : -Math.PI / 2);    // làn lên
      });
      g.add(rg);
      // biển báo độ dốc / làn
      fl.rampInfo = { side: s };
    }

    /* ---- tường / lan can ---- */
    var gateGap = (k === 0) ? 4.4 : 0;
    if (under) {
      U.wallWithGap(g, 'x', -HD, -XT, XT, 0, CLEAR, 0, 0, 0);
      U.wallWithGap(g, 'x', HD, -XT, XT, 0, CLEAR, 0, 0, 0);
      U.wallWithGap(g, 'z', XT, -HD, HD, 0, CLEAR, ENTRY_Z, gateGap, 3.4);
      U.wallWithGap(g, 'z', -XT, -HD, HD, 0, CLEAR, EXIT_Z, gateGap, 3.4);
      if (k === 0) {
        // trần hầm
        U.box(ceil, 2 * XT, SLAB, D_(), M.ceil, 0, CLEAR + SLAB / 2, 0);
        // ánh sáng ngoài trời sau cổng
        var sky1 = new T.Mesh(new T.PlaneGeometry(9, 5), M.sky); sky1.position.set(XT + 6, 2.2, ENTRY_Z); sky1.rotation.y = Math.PI / 2; g.add(sky1);
        var sky2 = sky1.clone(); sky2.position.set(-XT - 6, 2.2, EXIT_Z); g.add(sky2);
        U.box(g, 6, 0.1, 4.4, M.road, XT + 3, -0.05, ENTRY_Z); U.box(g, 6, 0.1, 4.4, M.road, -XT - 3, -0.05, EXIT_Z);
      }
      // dải màu chân tường theo khu vực
      [[-HD + 0.03, 1], [HD - 0.03, -1]].forEach(function (zz) {
        U.wallSeg(g, -ROW_LEN / 2, zz[0], ZONE_X, zz[0], 0.02, 0.9, M.bandBlue);
        U.wallSeg(g, ZONE_X, zz[0], ROW_LEN / 2, zz[0], 0.02, 0.9, M.bandGreen);
      });
    } else {
      // bãi cao tầng: lan can bê tông + lam đứng mặt tiền
      var PH = 1.1;
      var rails = [
        { ax: 'x', c: -HD, a: -XT, b: XT, gap: 0, gc: 0 },
        { ax: 'x', c: HD, a: -XT, b: XT, gap: 0, gc: 0 },
        { ax: 'z', c: XT, a: -HD, b: HD, gap: gateGap, gc: ENTRY_Z },
        { ax: 'z', c: -XT, a: -HD, b: HD, gap: gateGap, gc: EXIT_Z }
      ];
      var fins = [];
      rails.forEach(function (r) {
        var segs = r.gap ? [[r.a, r.gc - r.gap / 2], [r.gc + r.gap / 2, r.b]] : [[r.a, r.b]];
        segs.forEach(function (sg) {
          var len = sg[1] - sg[0], mid = (sg[0] + sg[1]) / 2;
          if (r.ax === 'x') U.box(g, len, PH, 0.3, M.conc, mid, PH / 2, r.c); else U.box(g, 0.3, PH, len, M.conc, r.c, PH / 2, mid);
          for (var f = sg[0] + 0.6; f < sg[1] - 0.3; f += 1.5) {
            var fh = CLEAR - PH;
            fins.push(r.ax === 'x' ? { x: f, y: PH + fh / 2, z: r.c, sx: 0.12, sy: fh, sz: 0.34 } : { x: r.c, y: PH + fh / 2, z: f, sx: 0.34, sy: fh, sz: 0.12 });
          }
        });
      });
      U.inst(g, U.unitBox(), M.fin, fins);
      if (k === 0) {
        U.box(ceil, 2 * XT, SLAB, D_(), M.ceil, 0, CLEAR + SLAB / 2, 0);
        U.box(g, 8, 0.1, 4.4, M.road, XT + 4, -0.05, ENTRY_Z);
        U.box(g, 8, 0.1, 4.4, M.road, -XT - 4, -0.05, EXIT_Z);
      }
    }

    /* ---- dữ liệu slot (demo) ---- */
    var R = U.rng(lot.def.seed * 100 + k * 7 + 3);
    var provinces = ['51A', '51F', '51G', '59A', '59C', '50A', '51K', '52A', '60A', '61A'];
    ROWS.forEach(function (row) {
      for (var i = 0; i < 25; i++) {
        var r = R(), occ = r < 0.55, mnt = !occ && r > 0.96;
        var plate = occ ? provinces[Math.floor(R() * provinces.length)] + '-' + Math.floor(100 + R() * 899) + '.' + U.pad2(Math.floor(R() * 99)) : null;
        fl.slots.push({
          code: fid + '-' + row.id + U.pad2(i + 1), floor: fid, floorIdx: k, row: row.id, idx: i + 1,
          zone: i + 1 <= MONTHLY_MAX ? 'MONTHLY' : 'VISITOR',
          status: occ ? 'OCCUPIED' : (mnt ? 'MAINTENANCE' : 'EMPTY'),
          plate: plate, vehicle: occ ? (R() < 0.12 ? 'Ô tô điện' : 'Ô tô 4-7 chỗ') : null,
          carColor: 0,
          x: SLOT_X[i], zc: row.zc, z: row.zc - row.face * 0.12, face: row.face,
          world: new T.Vector3(SLOT_X[i], y + 0.9, row.zc)
        });
      }
    });
    fl.slots.forEach(function (s, i) { s.carColor = U.CAR_COLORS[(i * 7 + k * 3 + (s.idx * 13)) % U.CAR_COLORS.length]; });

    /* ---- vạch kẻ / bánh chặn / vách ngăn modular ---- */
    var lines = [], backs = [], stops = [], seps = [], pads = [], cones = [];
    ROWS.forEach(function (row) {
      var f = row.face, x = -ROW_LEN / 2;
      GROUPS_().forEach(function (cnt, gi) {
        for (var i = 0; i <= cnt; i++) {
          var lx = x + i * SW, mon = lx < ZONE_X;
          lines.push({ x: lx, y: 0.012, z: row.zc, sx: 0.1, sy: 0.01, sz: SL, color: mon ? 0x4aa3ff : 0xf0f0ea });
          seps.push({ x: lx, y: 0.3, z: row.zc - f * (SL / 2 - 0.65), sx: 0.07, sy: 0.6, sz: 1.3, color: mon ? 0x7fb8ee : 0xe2d9a8 });
        }
        for (var j = 0; j < cnt; j++) {
          var cx = x + (j + 0.5) * SW;
          var mon2 = cx < ZONE_X;
          backs.push({ x: cx, y: 0.012, z: row.zc - f * SL / 2, sx: SW, sy: 0.01, sz: 0.1, color: mon2 ? 0x4aa3ff : 0xf0f0ea });
          stops.push({ x: cx, y: 0.07, z: row.zc - f * (SL / 2 - 0.55), sx: 1.6, sy: 0.14, sz: 0.16, color: 0x9a9da1 });
        }
        x += cnt * SW + GAPW;
      });
    });
    fl.slots.forEach(function (s) {
      var col = s.status === 'OCCUPIED' ? 0xef4444 : (s.status === 'MAINTENANCE' ? 0xf59e0b : 0x10b981);
      pads.push({ x: s.x, y: 0.02, z: s.zc, sx: SW - 0.4, sy: 1, sz: SL - 0.6, color: col });
      if (s.status === 'MAINTENANCE') cones.push({ x: s.x, y: 0.35, z: s.zc, sx: 1, sy: 1, sz: 1 }, { x: s.x - 0.6, y: 0.35, z: s.zc - s.face * 1.5, sx: 1, sy: 1, sz: 1 }, { x: s.x + 0.6, y: 0.35, z: s.zc - s.face * 1.5, sx: 1, sy: 1, sz: 1 });
    });
    U.inst(g, U.unitBox(), M.paintInst, lines);
    U.inst(g, U.unitBox(), M.paintInst, backs);
    U.inst(g, U.unitBox(), M.sepInst, stops);
    U.inst(g, U.unitBox(), M.sepInst, seps);
    U.inst(g, U.flatQuad(), M.pad, pads);
    if (cones.length) U.inst(g, U.getGeo('cone', function () { return new T.ConeGeometry(0.2, 0.7, 10); }), M.cone, cones);
    labelAtlas(fl);
    U.buildCars(g, fl.slots, 0);
    iotBoards(fl);

    /* ---- lối đi: vạch giữa, mũi tên, vạch qua đường, gờ giảm tốc ---- */
    var dashes = [], zebra = [], bumps = [], greenS = [];
    for (var dx = -XM + 2; dx < XM - 2; dx += 4) dashes.push({ x: dx, y: 0.012, z: AISLE_Z[1], sx: 2, sy: 0.01, sz: 0.12 });
    AISLE_Z.forEach(function (az, ai) {
      GAP_X.forEach(function (gx) {
        for (var q = 0; q < 6; q++) zebra.push({ x: gx, y: 0.013, z: az - 2.75 + q * 1.1, sx: 4.6, sy: 0.01, sz: 0.55 });
        [-1, 1].forEach(function (sd) { bumps.push({ x: gx + sd * 4.3, y: 0.04, z: az, sx: 0.55, sy: 0.08, sz: C.AISLE - 0.3 }); });
      });
      // mũi tên chỉ hướng xe chạy
      var dir = ai === 1 ? -exitDir : exitDir;
      for (var ax = -XM + 6; ax < XM - 4; ax += 11) {
        if (own && ai === 0 && Math.abs(ax) > XM - 8) continue;
        if (Math.abs(ax - GAP_X[0]) < 5 || Math.abs(ax - GAP_X[1]) < 5 || Math.abs(ax - GAP_X[2]) < 5) continue;
        U.floorArrow(g, ax, 0, az + (ai === 1 ? (dir > 0 ? -1.6 : 1.6) : 0), dir > 0 ? -Math.PI / 2 : Math.PI / 2, 1.0, 2.0);
      }
    });
    // hành lang đi bộ giữa các dãy (qua dãy B, C)
    GAP_X.forEach(function (gx) {
      [-5.75, 5.75].forEach(function (zz) {
        greenS.push({ x: gx, y: 0.014, z: zz, sx: 1.5, sy: 0.01, sz: SL, color: 0x1fa05a });
        greenS.push({ x: gx - 2.5, y: 0.013, z: zz, sx: 0.1, sy: 0.01, sz: SL, color: 0xf2c200 });
        greenS.push({ x: gx + 2.5, y: 0.013, z: zz, sx: 0.1, sy: 0.01, sz: SL, color: 0xf2c200 });
      });
    });
    U.inst(g, U.unitBox(), M.paintInst, dashes);
    U.inst(g, U.unitBox(), M.paintInst, zebra.map(function (z) { z.color = 0xf0f0ea; return z; }));
    U.inst(g, U.unitBox(), M.paintInst, greenS);
    U.inst(g, U.unitBox(), M.hazard, bumps);
    // vạch vàng đậm tại ranh giới khu tháng/vãng lai
    AISLE_Z.forEach(function (az) {
      U.box(g, 0.3, 0.01, C.AISLE - 0.4, M.paintYellow, ZONE_X + 3.4, 0.013, az);
    });
    // mũi tên ở 2 làn ngang đầu
    [-1, 1].forEach(function (sd) {
      var lx = sd * (ROW_LEN / 2 + END / 2);
      var rot = (sd * exitDir > 0) ? Math.PI : 0;
      [-4, 4].forEach(function (zz) { U.floorArrow(g, lx, 0, zz, rot, 1.0, 2.0); });
    });

    /* ---- cột, chân sọc vàng-đen, bình chữa cháy ---- */
    var cols = [], bases = [], exts = [], extSigns = [], cctv = [];
    var extTex = U.canvasTex(64, 64, function (c) { c.fillStyle = '#d32f2f'; c.fillRect(0, 0, 64, 64); c.fillStyle = '#fff'; c.fillRect(26, 10, 12, 44); c.fillRect(10, 26, 44, 12); });
    ROWS.forEach(function (row) {
      var f = row.face, zb = row.zc - f * SL / 2 + f * 0.25;
      COL_X.forEach(function (cx, ci) {
        cols.push({ x: cx, y: CLEAR / 2, z: zb, sx: 0.5, sy: CLEAR, sz: 0.5, color: 0xffffff });
        bases.push({ x: cx, y: 0.6, z: zb, sx: 0.54, sy: 1.2, sz: 0.54, color: 0xffffff });
        if (ci % 2 === 0) {
          var dirz = (row.id === 'A' || row.id === 'D') ? f : -f;
          exts.push({ x: cx, y: 1.35, z: zb + dirz * 0.3, sx: 0.28, sy: 0.55, sz: 0.16, color: 0xffffff });
          var sp = new T.Mesh(new T.PlaneGeometry(0.32, 0.32), new T.MeshBasicMaterial({ map: extTex }));
          sp.position.set(cx, 1.95, zb + dirz * 0.26); sp.rotation.y = dirz > 0 ? 0 : Math.PI; g.add(sp);
        }
        if (ci % 3 === 1 && (row.id === 'B' || row.id === 'C')) {
          cctv.push({ x: cx, y: 3.4, z: zb + (row.id === 'B' ? -1 : 1) * 0.35 });
        }
      });
    });
    // cột chu vi ở khu dốc
    [-1, 1].forEach(function (sd) {
      [XM + RAMP_L / 2, XT - 0.3].forEach(function (cx) {
        [-HD + 0.3, HD - 0.3].forEach(function (cz) {
          if (own === sd && cz < 0) return;
          cols.push({ x: sd * cx, y: CLEAR / 2, z: cz, sx: 0.5, sy: CLEAR, sz: 0.5, color: 0xffffff });
          bases.push({ x: sd * cx, y: 0.6, z: cz, sx: 0.54, sy: 1.2, sz: 0.54, color: 0xffffff });
        });
      });
    });
    U.inst(g, U.unitBox(), M.conc, cols);
    U.inst(g, U.unitBox(), M.hazard, bases);
    U.inst(g, U.unitBox(), M.red, exts);
    var domeG = U.getGeo('dome', function () { return new T.SphereGeometry(0.13, 10, 8, 0, Math.PI * 2, 0, Math.PI / 2); });
    cctv.forEach(function (c) { var m = new T.Mesh(domeG, M.dark); m.position.set(c.x, c.y, c.z); m.rotation.x = Math.PI; g.add(m); });

    /* ---- cụm thang máy sát tường (3 hành lang x 2 phía) + cửa thoát hiểm ---- */
    GAP_X.forEach(function (gx) { buildCore(fl, gx, 1); buildCore(fl, gx, -1); });
    [-1, 1].forEach(function (sd) {
      var ex = sd * (ROW_LEN / 2 + END / 2);
      emergencyDoor(fl, ex, -HD, 1); emergencyDoor(fl, ex, HD, -1);
    });
    // cửa gió thông tầng
    [-1, 1].forEach(function (sd) {
      var v = box3(g, 1.8, 1.0, 0.12, M.dark, sd * 30, 3.2, -HD + 0.06);
      for (var q = 0; q < 5; q++) box3(g, 1.7, 0.03, 0.14, M.steel, sd * 30, 2.85 + q * 0.14, -HD + 0.07);
    });

    /* ---- hạ tầng trần (nhóm ceil: ẩn khi xem toàn cảnh) ---- */
    buildCeiling(fl, ceil, under, k);
    buildSigns(fl, ceil, k, lot);

    /* ---- cổng vào / ra + barrier phân khu ---- */
    if (k === 0) {
      buildGate(g, fl, { x: XT - 8, z: ENTRY_Z, heading: -1, kind: 'entry', label: 'LỐI VÀO', label2: 'ENTRANCE', readerLine: 'QUẸT THẺ' });
      buildGate(g, fl, { x: -XT + 8, z: EXIT_Z, heading: -1, kind: 'exit', label: 'LỐI RA', label2: 'EXIT', readerLine: 'QUẸT THẺ' });
    }
    AISLE_Z.forEach(function (az) {
      buildGate(g, fl, { x: ZONE_X + 3.4, z: az + 1.6, heading: -1, kind: 'zone', label: 'XE THÁNG', readerLine: 'XE THÁNG' });
    });

    return fl;
  }

  function D_() { return C.D; }
  function GROUPS_() { return [6, 6, 6, 7]; }

  /* ---------- trần: dầm, ống, đèn, quạt ---------- */
  function buildCeiling(fl, ceil, under, k) {
    var beams = [], lamps = [];
    COL_X.forEach(function (cx) { beams.push({ x: cx, y: CLEAR - 0.25, z: 0, sx: 0.55, sy: 0.5, sz: C.D }); });
    [-HD + 11.5 - 3.25, HD - 11.5 + 3.25].forEach(function (zz) { beams.push({ x: 0, y: CLEAR - 0.25, z: zz, sx: 2 * XM, sy: 0.5, sz: 0.5 }); });
    U.inst(ceil, U.unitBox(), M.conc, beams);
    var rowsZ = AISLE_Z.concat(ROWS.map(function (r) { return r.zc; }));
    for (var lx = -XM + 2.5; lx < XM; lx += 5) rowsZ.forEach(function (z) { lamps.push({ x: lx, y: CLEAR - 0.56, z: z, sx: 1.5, sy: 0.07, sz: 0.16 }); });
    U.inst(ceil, U.unitBox(), M.lamp, lamps);
    AISLE_Z.forEach(function (az) {
      U.box(ceil, 2 * XM, 0.5, 0.9, M.duct, 0, CLEAR - 0.8, az - 2.2);
      U.cyl(ceil, 0.06, 2 * XM, M.pipeRed, 0, CLEAR - 0.7, az + 2.4, 'x');
      U.cyl(ceil, 0.05, 2 * XM, M.pipeBlue, 0, CLEAR - 0.72, az + 2.0, 'x');
      U.box(ceil, 2 * XM, 0.05, 0.4, M.tray, 0, CLEAR - 0.6, az + 0.5);
    });
    // quạt thông gió (jet fan) treo giữa lối đi
    for (var fx = -XM + 8; fx < XM - 6; fx += 18) {
      [AISLE_Z[1] + 1.2, AISLE_Z[0] + 0.5, AISLE_Z[2] - 0.5].forEach(function (fz, fi) {
        if (fi && Math.abs(fx) > 40) return;
        var fan = U.cyl(ceil, 0.34, 1.8, M.steel, fx, CLEAR - 1.35, fz, 'x');
        U.cyl(ceil, 0.37, 0.12, M.dark, fx - 0.9, CLEAR - 1.35, fz, 'x');
        U.cyl(ceil, 0.37, 0.12, M.dark, fx + 0.9, CLEAR - 1.35, fz, 'x');
        U.box(ceil, 0.05, 0.6, 0.05, M.steel, fx - 0.5, CLEAR - 0.95, fz);
        U.box(ceil, 0.05, 0.6, 0.05, M.steel, fx + 0.5, CLEAR - 0.95, fz);
      });
    }
    // đèn thoát hiểm (xanh) gắn trần ở cổng
    if (k === 0) {
      U.box(ceil, 0.8, 0.12, 0.3, M.exitGreen, -XT + 1.2, CLEAR - 0.4, EXIT_Z);
    }
  }

  /* ---------- biển báo treo trần ---------- */
  function buildSigns(fl, ceil, k, lot) {
    var ids = lot.def.ids, d = fl.exitDir;
    var lineA = k === 0 ? 'LỐI RA / EXIT' : 'LỐI LÊN / EXIT';
    var lineB = k === 0 ? 'Cổng ra' : 'Lên tầng ' + ids[k - 1];
    var EXG = '#0b8a43';
    // (1) đối diện xe chạy tới lối ra: ▲ ; chạy ngược: ↩
    var texToward = U.signTex([lineA + '  ▲', lineB], EXG, '#fff', 512, 160);
    var texBack = U.signTex([lineA + '  ↩', 'Quay lại ' + (d < 0 ? 'phía Tây' : 'phía Đông')], '#8a1c1c', '#fff', 512, 160);
    [-54, -36, -18, 0, 18, 36, 54].forEach(function (x) {
      AISLE_Z.forEach(function (az) {
        if (fl.own && az === AISLE_Z[0] && Math.abs(x) > XM - 4) return;
        // d>0: xe đi về +x thấy mặt pháp tuyến -x
        U.hangSign(ceil, x + 3, az + (az === 0 ? 1.4 : 0), 2.6, 0.82, d > 0 ? texBack : texToward, d > 0 ? texToward : texBack, 'x', 0.35);
      });
    });
    // (2) gắn dọc theo lối đi, mũi tên chỉ đúng hướng ra
    [-31, -11, 9, 30].forEach(function (x) {
      AISLE_Z.forEach(function (az) {
        function t(nrm) { var right = (nrm > 0) ? (d > 0) : (d < 0); return U.signTex([(right ? '' : '◀ ') + (k === 0 ? 'LỐI RA' : 'LÊN ' + ids[k - 1]) + (right ? ' ▶' : ''), 'EXIT'], EXG, '#fff', 400, 150); }
        U.hangSign(ceil, x, az + 2.6, 2.0, 0.7, t(1), t(-1), 'z', 0.35);
      });
    });
    // (3) bảng chỉ đường đến thang máy (bảng nhỏ treo trần)
    GAP_X.forEach(function (gx) {
      AISLE_Z.forEach(function (az, ai) {
        var east = ai === 0 ? '◀ THANG MÁY' : ai === 2 ? 'THANG MÁY ▶' : '◀ THANG MÁY ▶';
        var west = ai === 0 ? 'THANG MÁY ▶' : ai === 2 ? '◀ THANG MÁY' : '◀ THANG MÁY ▶';
        var te = U.signTex([east], '#0b5cad', '#fff', 360, 110), tw = U.signTex([west], '#0b5cad', '#fff', 360, 110);
        U.hangSign(ceil, gx, az, 1.9, 0.58, tw, te, 'x', 0.3);   // texPos: người đi về −x ; texNeg: người đi về +x
        var ta = U.signTex(['THANG MÁY  ▲'], '#0b5cad', '#fff', 360, 110);
        U.hangSign(ceil, gx - 9, az, 1.7, 0.52, null, ta, 'x', 0.3);
        U.hangSign(ceil, gx + 9, az, 1.7, 0.52, ta, null, 'x', 0.3);
      });
      var tc = U.signTex(['▲  THANG MÁY'], '#0b5cad', '#fff', 360, 110);
      [-5.75, 5.75].forEach(function (zz) { U.hangSign(ceil, gx, zz, 1.7, 0.52, tc, tc, 'z', 0.3); });
    });
    // (4) khu vực: xe tháng / vãng lai (qua barrier)
    var toVisitor = U.signTex(['KHU VÃNG LAI', 'Slot 13 - 25'], '#1e8f4e', '#fff', 512, 160);
    var toMonthly = U.signTex(['KHU XE THÁNG', 'Slot 01 - 12 · Quẹt thẻ'], '#1f5fae', '#fff', 512, 160);
    AISLE_Z.forEach(function (az) { U.hangSign(ceil, ZONE_X + 3.4, az, 3.0, 0.9, toMonthly, toVisitor, 'x', 0.25); });
    // (5) tên dãy ở hai đầu
    [-1, 1].forEach(function (sd) {
      var lx = sd * (ROW_LEN / 2 - 1);
      [['A', 0], ['B · C', 1], ['D', 2]].forEach(function (pr) {
        var tx = U.signTex([fl.id + ' · DÃY ' + pr[0]], '#202833', '#ffd34d', 400, 110);
        U.hangSign(ceil, lx, AISLE_Z[pr[1]] - 2.4, 1.8, 0.5, tx, tx, 'x', 0.3);
      });
    });
    // (6) lối vào / lối ra tại tường
    if (k === 0) {
      U.hangSign(ceil, XT - 2.2, ENTRY_Z, 3.0, 0.9, U.signTex(['LỐI VÀO', 'ENTRANCE'], '#0b7a3b', '#fff', 480, 150), U.signTex(['LỐI VÀO', 'ENTRANCE'], '#0b7a3b', '#fff', 480, 150), 'x', 0.1);
      U.hangSign(ceil, -XT + 2.2, EXIT_Z, 3.0, 0.9, U.signTex(['LỐI RA', 'EXIT  ◀◀'], '#a31818', '#fff', 480, 150), U.signTex(['LỐI RA', 'EXIT  ◀◀'], '#a31818', '#fff', 480, 150), 'x', 0.1);
    }
    // (7) biển chỉ làn dốc
    if (fl.own) {
      var s = fl.own;
      var rt = U.signTex(['DỐC XUỐNG ' + (ids[k + 1] || ''), '▼ Làn trái · ▲ Làn phải'], '#c46a00', '#fff', 512, 160);
      U.hangSign(ceil, s * (XM + 2), RBC, 3.2, 0.9, rt, rt, 'x', 0.3);
    }
  }

  /* ================================================================== */
  /*  1 BÃI                                                              */
  /* ================================================================== */
  function buildLot(def, THREE) {
    T = THREE; I.setThree(THREE); I.initMaterials(); extraMaterials();
    var n = def.ids.length;
    var lot = { def: def, group: new T.Group(), ys: [], floors: [], nodes: [], cores: [], gates: [] };
    for (var k = 0; k < n; k++) lot.ys.push(def.type === 'underground' ? -k * FH : (n - 1 - k) * FH);
    for (var k2 = 0; k2 < n; k2++) {
      var fl = buildFloor(lot, k2);
      lot.floors.push(fl);
      lot.group.add(fl.group);
      fl.gates.forEach(function (g) { lot.gates.push(g); });
      fl.cores.forEach(function (c) { c.floorIdx = k2; lot.cores.push(c); });
    }
    if (def.type === 'multistorey') {
      var gy = lot.ys[n - 1] - SLAB - 0.02;
      var gr = new T.Mesh(new T.PlaneGeometry(600, 600), M.ground); gr.rotation.x = -Math.PI / 2; gr.position.y = gy; lot.group.add(gr);
      var rd = new T.Mesh(new T.PlaneGeometry(600, 12), M.road); rd.rotation.x = -Math.PI / 2; rd.position.set(0, gy + 0.01, HD + 14); lot.group.add(rd);
    } else {
      var ug = new T.GridHelper(400, 80, 0x1c2430, 0x151b24); ug.position.y = lot.ys[n - 1] - SLAB - 0.05; lot.group.add(ug);
    }
    buildGraph(lot);
    return lot;
  }

  /* ---------- đồ thị điểm di chuyển (Street View) ---------- */
  function fill(vals, maxGap) {
    var out = [];
    vals.sort(function (a, b) { return a - b; });
    for (var i = 0; i < vals.length; i++) {
      if (i && Math.abs(vals[i] - vals[i - 1]) < 0.5) continue;
      if (out.length) {
        var a = out[out.length - 1], b = vals[i], steps = Math.ceil((b - a) / maxGap);
        for (var s = 1; s < steps; s++) out.push(a + (b - a) * s / steps);
      }
      out.push(vals[i]);
    }
    return out;
  }
  function buildGraph(lot) {
    var nodes = lot.nodes, n = lot.floors.length;
    var vertX = GAP_X.concat([-(ROW_LEN / 2 + END / 2), ROW_LEN / 2 + END / 2, -(XT - 3), XT - 3]);
    var base = [-(XT - 3), -XM, -(ROW_LEN / 2 + END / 2), ROW_LEN / 2 + END / 2, XM, XT - 3].concat(GAP_X);
    var xs = fill(base, 9);
    function add(fl, x, y, z, type, vert, extra) {
      var node = { id: nodes.length, x: x, y: y, z: z, floorIdx: fl.k, type: type, vert: !!vert, nb: [] };
      if (extra) for (var key in extra) node[key] = extra[key];
      nodes.push(node); fl.nodeIds.push(node.id); return node;
    }
    function near(a, b) { return Math.abs(a - b) < 0.6; }
    lot.floors.forEach(function (fl) {
      // nút trên 3 lối đi
      AISLE_Z.forEach(function (az, ai) {
        xs.forEach(function (x) {
          var side = x > 0 ? 1 : -1, ax = Math.abs(x);
          if (ai === 0) {
            if (fl.own === side && ax > XM + 0.5) return;                   // phần dốc xử lý riêng
            if (fl.land === side && ax > XM + 0.5 && ax < XT - 3.5) return; // dưới chân dốc tầng trên
          }
          var isVert = vertX.some(function (v) { return near(v, x); });
          add(fl, x, fl.y, az, 'aisle', isVert);
        });
      });
      // nút dọc hành lang đi bộ: giữa các dãy + tiền sảnh thang máy
      GAP_X.forEach(function (gx) {
        [-5.75, 5.75].forEach(function (zz) { add(fl, gx, fl.y, zz, 'walk', true); });
        [1, -1].forEach(function (sd) {
          var nd = add(fl, gx, fl.y, sd > 0 ? -HD + 4.2 : HD - 4.2, 'core', true, { coreSide: sd });
          fl.cores.forEach(function (c) { if (near(c.x, gx) && c.side === sd) c.nodeId = nd.id; });
        });
      });
      // nút trên dốc đi xuống tầng dưới (làn lên)
      if (fl.own) {
        [4, 10, 16].forEach(function (off) {
          var x = fl.own * (XM + off), ny = fl.y - FH * off / RAMP_L;
          add(fl, x, ny, AISLE_Z[0], 'ramp', false, { rampOf: fl.k });
        });
      }
    });
    // kết nối
    function link(a, b) { if (a.nb.indexOf(b.id) < 0) { a.nb.push(b.id); b.nb.push(a.id); } }
    lot.floors.forEach(function (fl) {
      var list = fl.nodeIds.map(function (id) { return nodes[id]; });
      // cùng z => nối theo thứ tự x
      var byZ = {};
      list.forEach(function (nd) { var key = nd.z.toFixed(2); (byZ[key] = byZ[key] || []).push(nd); });
      Object.keys(byZ).forEach(function (key) {
        var arr = byZ[key].sort(function (a, b) { return a.x - b.x; });
        for (var i = 1; i < arr.length; i++) if (arr[i].x - arr[i - 1].x <= 9.5) link(arr[i - 1], arr[i]);
      });
      // cùng x (chỉ ở hành lang / lối ngang đầu)
      var byX = {};
      list.forEach(function (nd) { if (nd.vert && nd.type !== 'ramp') { var key = nd.x.toFixed(2); (byX[key] = byX[key] || []).push(nd); } });
      Object.keys(byX).forEach(function (key) {
        var arr = byX[key].sort(function (a, b) { return a.z - b.z; });
        for (var i = 1; i < arr.length; i++) if (arr[i].z - arr[i - 1].z <= 12.6) link(arr[i - 1], arr[i]);
      });
    });
    // nối chân dốc -> nút trên sàn tầng dưới
    lot.floors.forEach(function (fl) {
      if (!fl.own || fl.k >= n - 1) return;
      var lower = lot.floors[fl.k + 1];
      var ramp = fl.nodeIds.map(function (id) { return nodes[id]; }).filter(function (nd) { return nd.type === 'ramp'; });
      ramp.sort(function (a, b) { return Math.abs(a.x) - Math.abs(b.x); });
      var landX = fl.own * (XT - 3);
      var landing = lower.nodeIds.map(function (id) { return nodes[id]; }).filter(function (nd) { return near(nd.x, landX) && near(nd.z, AISLE_Z[0]); })[0];
      var start = fl.nodeIds.map(function (id) { return nodes[id]; }).filter(function (nd) { return nd.type === 'aisle' && near(nd.x, fl.own * XM) && near(nd.z, AISLE_Z[0]); })[0];
      if (start) link(start, ramp[0]);
      for (var i = 1; i < ramp.length; i++) link(ramp[i - 1], ramp[i]);
      if (landing) link(ramp[ramp.length - 1], landing);
    });
    // nút đặc biệt: cổng vào / ra ở tầng trên cùng
    var top = lot.floors[0];
    lot.entryNode = nearestNode(lot, 0, XT - 3, ENTRY_Z);
    lot.exitNode = nearestNode(lot, 0, -XT + 3, EXIT_Z);
  }
  function nearestNode(lot, floorIdx, x, z) {
    var best = null, bd = 1e9;
    lot.nodes.forEach(function (nd) {
      if (floorIdx != null && nd.floorIdx !== floorIdx) return;
      var d = (nd.x - x) * (nd.x - x) + (nd.z - z) * (nd.z - z);
      if (d < bd) { bd = d; best = nd; }
    });
    return best;
  }

  I.buildLot = buildLot;
  I.nearestNode = nearestNode;
})(typeof self !== 'undefined' ? self : this);
