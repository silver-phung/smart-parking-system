/**
 * ==========================================================
 * Parking3DViewer Component (Three.js)
 * Sprint 3 - Days 6-9: 3D Visualization cho Hệ thống Smart Parking
 *
 * TÍNH NĂNG NÂNG CAO (Mô hình Hầm Ngầm - Underground Parking Lot):
 *   - Mô phỏng kiến trúc hầm đỗ xe ngầm Sketchfab: Tường bê tông, dầm trần,
 *     hệ thống ống thông gió HVAC, ống PCCC đỏ, đèn tuýp LED trần,
 *     cột chịu lực hazard stripes, thanh chặn bánh xe tire stoppers tại mỗi ô.
 *   - 3 BÃI MẪU:
 *       • Bãi 1: 2 Tầng (Hầm B1, Hầm B2) - Tổng slot theo DB (64 slots).
 *       • Bãi 2: 3 Tầng (Hầm B1, Hầm B2, Hầm B3) - Số slot random (82 slots).
 *       • Bãi 3: 4 Tầng (Hầm B1, Hầm B2, Hầm B3, Hầm B4) - Số slot random (112 slots).
 *   - RENDER CẦU THANG BỘ & BUỒNG THANG MÁY (Elevator & Staircase Core).
 *   - NÚT DI CHUYỂN LÊN/XUỐNG CÁC TẦNG TẠI ĐIỂM THANG MÁY:
 *       • Bảng điều khiển 3D tương tác ngay tại thang máy.
 *       • Widget nổi gắn neo 3D (Floating Elevator Call Widget) với nút ▲ Lên, ▼ Xuống và các tầng.
 *       • Hoạt cảnh chuyển tầng camera mượt mà (Elevator ride animation).
 *   - 4 TRẠNG THÁI VẬT LÝ + TRẠNG THÁI ĐẶC BIỆT:
 *       🟢 Trống (Available)
 *       🔴 Có xe (Occupied) -> Render mesh xe ô tô 3D chân thực
 *       🟡 Bảo trì (Maintenance) -> Render cọc tiêu giao thông 3D
 *       ⚪ Tạm khóa (Unavailable) -> Gạch chéo cấm đỗ
 *       ⏳ Chờ vào (Reserved) -> Viền nhấp nháy phát sáng (Pulsing highlight)
 * ==========================================================
 */

(function (root, factory) {
  if (typeof define === 'function' && define.amd) {
    define([], factory);
  } else if (typeof module === 'object' && module.exports) {
    module.exports = factory();
  } else {
    root.Parking3DViewer = factory();
  }
})(typeof self !== 'undefined' ? self : this, function () {
  'use strict';

  // Cấu hình 3 bãi đỗ xe mẫu
  var LOT_CONFIGS = {
    1: {
      id: 1,
      name: 'Bãi 1: Hầm ngầm FPT Q9 (Mô hình Sketchfab)',
      desc: 'Mô hình 2 tầng hầm chuẩn DB (B1: 32 ô, B2: 32 ô = 64 ô)',
      floors: ['B1', 'B2'],
      floorLabels: { B1: 'Hầm B1', B2: 'Hầm B2' }
    },
    2: {
      id: 2,
      name: 'Bãi 2: Vincom Landmark 81 Underground',
      desc: 'Mô hình 3 tầng hầm (B1, B2, B3) số slot ngẫu nhiên',
      floors: ['B1', 'B2', 'B3'],
      floorLabels: { B1: 'Hầm B1', B2: 'Hầm B2', B3: 'Hầm B3' }
    },
    3: {
      id: 3,
      name: 'Bãi 3: Aeon Mall Mega Parking Hub',
      desc: 'Mô hình 4 tầng ngầm (B1, B2, B3, B4) số slot ngẫu nhiên',
      floors: ['B1', 'B2', 'B3', 'B4'],
      floorLabels: { B1: 'Hầm B1', B2: 'Hầm B2', B3: 'Hầm B3', B4: 'Hầm B4' }
    }
  };

  var STATUS_COLORS = {
    EMPTY:       { hex: 0x10b981, css: '#10b981', label: 'Trống', code: 'free' },
    OCCUPIED:    { hex: 0xef4444, css: '#ef4444', label: 'Có xe', code: 'occ' },
    RESERVED:    { hex: 0x06b6d4, css: '#06b6d4', label: 'Chờ xe vào', code: 'res' },
    MAINTENANCE: { hex: 0xf59e0b, css: '#f59e0b', label: 'Bảo trì', code: 'blk' },
    UNAVAILABLE: { hex: 0x6b7280, css: '#6b7280', label: 'Tạm khóa', code: 'off' }
  };

  /**
   * Class Parking3DViewer
   */
  function Parking3DViewer(options) {
    options = options || {};
    this.container = typeof options.container === 'string'
      ? document.querySelector(options.container)
      : (options.container || document.body);

    if (!this.container) {
      console.error('[Parking3DViewer] Container not found.');
      return;
    }

    var initialLotId = options.lotId || 1;
    var lotCfg = LOT_CONFIGS[initialLotId] || LOT_CONFIGS[1];

    this.options = {
      apiEndpoint: options.apiEndpoint || '/api/ParkingSlots',
      lotId: initialLotId,
      lotName: lotCfg.name,
      floorId: options.floorId || lotCfg.floors[0],
      showStats: options.showStats !== false,
      showControls: options.showControls !== false,
      onSlotClick: options.onSlotClick || null,
      onSlotHover: options.onSlotHover || null,
      onFloorChange: options.onFloorChange || null
    };

    this.slots = [];
    this.slotMeshes = {};
    this.carMeshes = {};
    this.coneMeshes = {};
    this.pulsingSlots = [];
    this.activeFilter = 'ALL';
    this.isTopDown = false;
    this.isTransitioning = false;

    // Vị trí cụm thang máy / cầu thang trong không gian 3D
    this.elevatorWorldPos = new THREE ? new THREE.Vector3(26, 4.5, 0) : { x: 26, y: 4.5, z: 0 };

    this.defaultCamPos = { x: 0, y: 38, z: 46 };
    this.defaultTarget = { x: 0, y: 0, z: 0 };

    this.clock = null;
    this.animId = null;
    this.raycaster = null;
    this.mouse = null;
    this.hoveredSlot = null;

    this._ensureDependencies(function () {
      this.elevatorWorldPos = new THREE.Vector3(26, 4.5, 0);
      this._buildUI();
      this._initThree();
      this.loadSlots();
    }.bind(this));
  }

  Parking3DViewer.prototype._ensureDependencies = function (callback) {
    var self = this;
    if (window.THREE && window.THREE.OrbitControls) {
      callback();
      return;
    }

    var loadScript = function (src, next) {
      var s = document.createElement('script');
      s.src = src;
      s.onload = next;
      document.head.appendChild(s);
    };

    if (!window.THREE) {
      loadScript('https://cdnjs.cloudflare.com/ajax/libs/three.js/r128/three.min.js', function () {
        loadScript('https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/controls/OrbitControls.js', callback);
      });
    } else if (!window.THREE.OrbitControls) {
      loadScript('https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/controls/OrbitControls.js', callback);
    }
  };

  /**
   * Khởi tạo giao diện DOM (Canvas + HUD + Thang máy widget + Tooltip)
   */
  Parking3DViewer.prototype._buildUI = function () {
    var self = this;
    this.container.classList.add('p3d-wrapper');

    // Canvas container
    this.canvasWrap = document.createElement('div');
    this.canvasWrap.className = 'p3d-canvas-container';
    this.container.appendChild(this.canvasWrap);

    // Loader
    this.loaderEl = document.createElement('div');
    this.loaderEl.className = 'p3d-loader';
    this.loaderEl.innerHTML = '<div class="p3d-spinner"></div><span>Đang dựng không gian hầm đỗ xe 3D...</span>';
    this.container.appendChild(this.loaderEl);

    // Top HUD
    if (this.options.showStats) {
      this.hudTop = document.createElement('div');
      this.hudTop.className = 'p3d-hud-top';
      this.container.appendChild(this.hudTop);
      this._renderTopHUD();
    }

    // Bottom HUD
    if (this.options.showControls) {
      this.hudBottom = document.createElement('div');
      this.hudBottom.className = 'p3d-hud-bottom';
      this.hudBottom.innerHTML = [
        '<div class="p3d-btn-group">',
        '  <button type="button" class="p3d-btn primary" id="p3dResetBtn" title="Đưa camera về góc nhìn toàn cảnh ban đầu">',
        '    <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2"><path d="M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8"/><path d="M3 3v5h5"/></svg>',
        '    Reset View',
        '  </button>',
        '  <button type="button" class="p3d-btn" id="p3dTopDownBtn" title="Chuyển đổi góc nhìn 2D / 3D">',
        '    <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2"><rect x="3" y="3" width="18" height="18" rx="2"/><path d="M3 9h18M9 21V9"/></svg>',
        '    Góc nhìn 2D/3D',
        '  </button>',
        '</div>',
        '<div class="p3d-hints">',
        '  <span>🛗 Thang máy: Bấm ▲ ▼ chuyển tầng</span>',
        '  <span>🖱️ Chuột trái: Xoay</span>',
        '  <span>🔍 Cuộn: Zoom</span>',
        '  <span>👆 Phải: Dời</span>',
        '</div>'
      ].join('');
      this.container.appendChild(this.hudBottom);

      this.hudBottom.querySelector('#p3dResetBtn').addEventListener('click', function () {
        self.resetView();
      });
      this.hudBottom.querySelector('#p3dTopDownBtn').addEventListener('click', function () {
        self.toggleTopDownView();
      });
    }

    // Bảng gọi thang máy nổi gắn neo 3D (Elevator Call Widget)
    this.elevWidget = document.createElement('div');
    this.elevWidget.className = 'p3d-elevator-widget';
    this.container.appendChild(this.elevWidget);
    this._updateElevatorWidget();

    // Floating Tooltip
    this.tooltip = document.createElement('div');
    this.tooltip.className = 'p3d-tooltip';
    this.container.appendChild(this.tooltip);
  };

  /**
   * Render nội dung Top HUD theo bãi xe hiện tại
   */
  Parking3DViewer.prototype._renderTopHUD = function () {
    var self = this;
    var lotCfg = LOT_CONFIGS[this.options.lotId] || LOT_CONFIGS[1];

    var lotOptions = Object.keys(LOT_CONFIGS).map(function (k) {
      var c = LOT_CONFIGS[k];
      return '<option value="' + c.id + '" ' + (c.id === self.options.lotId ? 'selected' : '') + '>' + c.name + '</option>';
    }).join('');

    var floorOptions = lotCfg.floors.map(function (fid) {
      var lbl = lotCfg.floorLabels[fid] || ('Tầng ' + fid);
      return '<option value="' + fid + '" ' + (fid === self.options.floorId ? 'selected' : '') + '>' + lbl + '</option>';
    }).join('');

    this.hudTop.innerHTML = [
      '<div class="p3d-hud-group">',
      '  <select class="p3d-lot-select" id="p3dLotSelect">' + lotOptions + '</select>',
      '  <select class="p3d-floor-select" id="p3dFloorSelect">' + floorOptions + '</select>',
      '</div>',
      '<div class="p3d-hud-group p3d-stats-pills">',
      '  <span class="p3d-pill" data-filter="ALL" title="Xem tất cả">Tất cả: <b id="statTotal">0</b></span>',
      '  <span class="p3d-pill active" data-filter="EMPTY" title="Lọc ô trống"><i class="dot dot-free"></i> Trống: <b id="statFree">0</b></span>',
      '  <span class="p3d-pill" data-filter="OCCUPIED" title="Lọc ô có xe"><i class="dot dot-occ"></i> Có xe: <b id="statOcc">0</b></span>',
      '  <span class="p3d-pill" data-filter="RESERVED" title="Lọc ô đã phân chờ vào"><i class="dot dot-res"></i> Chờ vào: <b id="statRes">0</b></span>',
      '  <span class="p3d-pill" data-filter="MAINTENANCE" title="Lọc ô bảo trì"><i class="dot dot-blk"></i> Bảo trì: <b id="statBlk">0</b></span>',
      '</div>'
    ].join('');

    // Event change lot
    var lotSel = this.hudTop.querySelector('#p3dLotSelect');
    lotSel.addEventListener('change', function (e) {
      self.setLot(parseInt(e.target.value, 10));
    });

    // Event change floor
    var floorSel = this.hudTop.querySelector('#p3dFloorSelect');
    floorSel.addEventListener('change', function (e) {
      self.setFloor(e.target.value);
    });

    // Filter pills
    var pills = this.hudTop.querySelectorAll('.p3d-pill');
    pills.forEach(function (pill) {
      pill.addEventListener('click', function () {
        pills.forEach(function (p) { p.classList.remove('active'); });
        pill.classList.add('active');
        self.filterStatus(pill.getAttribute('data-filter'));
      });
    });
  };

  /**
   * Cập nhật Widget gọi thang máy
   */
  Parking3DViewer.prototype._updateElevatorWidget = function () {
    var self = this;
    var lotCfg = LOT_CONFIGS[this.options.lotId] || LOT_CONFIGS[1];
    var floors = lotCfg.floors;
    var currentIdx = floors.indexOf(this.options.floorId);
    if (currentIdx === -1) currentIdx = 0;

    var canGoUp = currentIdx > 0;
    var canGoDown = currentIdx < floors.length - 1;

    var floorButtons = floors.map(function (f) {
      var isActive = f === self.options.floorId;
      return '<button type="button" class="p3d-elev-floor-btn ' + (isActive ? 'active' : '') + '" data-floor="' + f + '">' + f + '</button>';
    }).join('');

    this.elevWidget.innerHTML = [
      '<div class="p3d-elevator-header">',
      '  <span>🛗 Thang máy & Cầu thang</span>',
      '  <span class="p3d-elevator-indicator">' + (lotCfg.floorLabels[this.options.floorId] || this.options.floorId) + '</span>',
      '</div>',
      '<div class="p3d-elevator-btns">',
      '  <button type="button" class="p3d-elev-btn" id="p3dElevUpBtn" ' + (!canGoUp ? 'disabled' : '') + ' title="Đi lên tầng trên">▲ Lên</button>',
      '  <button type="button" class="p3d-elev-btn" id="p3dElevDownBtn" ' + (!canGoDown ? 'disabled' : '') + ' title="Đi xuống tầng dưới">▼ Xuống</button>',
      '  <div class="p3d-elev-floors">' + floorButtons + '</div>',
      '</div>'
    ].join('');

    // Sự kiện nút Lên / Xuống
    var upBtn = this.elevWidget.querySelector('#p3dElevUpBtn');
    if (upBtn) {
      upBtn.addEventListener('click', function () {
        if (canGoUp) self.setFloor(floors[currentIdx - 1], 'UP');
      });
    }

    var downBtn = this.elevWidget.querySelector('#p3dElevDownBtn');
    if (downBtn) {
      downBtn.addEventListener('click', function () {
        if (canGoDown) self.setFloor(floors[currentIdx + 1], 'DOWN');
      });
    }

    // Sự kiện nút số tầng
    this.elevWidget.querySelectorAll('.p3d-elev-floor-btn').forEach(function (btn) {
      btn.addEventListener('click', function () {
        var targetF = btn.getAttribute('data-floor');
        if (targetF !== self.options.floorId) {
          var targetIdx = floors.indexOf(targetF);
          var dir = targetIdx < currentIdx ? 'UP' : 'DOWN';
          self.setFloor(targetF, dir);
        }
      });
    });
  };

  /**
   * Khởi tạo Three.js
   */
  Parking3DViewer.prototype._initThree = function () {
    var width = this.canvasWrap.clientWidth || 800;
    var height = this.canvasWrap.clientHeight || 520;

    this.clock = new THREE.Clock();

    // Scene
    this.scene = new THREE.Scene();
    this.scene.background = new THREE.Color(0x090d16);
    this.scene.fog = new THREE.FogExp2(0x090d16, 0.008);

    // Camera
    this.camera = new THREE.PerspectiveCamera(45, width / height, 0.1, 1000);
    this.camera.position.set(this.defaultCamPos.x, this.defaultCamPos.y, this.defaultCamPos.z);

    // Renderer
    this.renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true, powerPreference: 'high-performance' });
    this.renderer.setSize(width, height);
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.shadowMap.enabled = true;
    this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    this.canvasWrap.appendChild(this.renderer.domElement);

    // Controls
    this.controls = new THREE.OrbitControls(this.camera, this.renderer.domElement);
    this.controls.enableDamping = true;
    this.controls.dampingFactor = 0.05;
    this.controls.maxPolarAngle = Math.PI / 2.05;
    this.controls.minDistance = 10;
    this.controls.maxDistance = 150;
    this.controls.target.set(this.defaultTarget.x, this.defaultTarget.y, this.defaultTarget.z);

    this._setupLighting();
    this._buildUndergroundArchitecture();

    this.raycaster = new THREE.Raycaster();
    this.mouse = new THREE.Vector2();

    this._bindEvents();
    this._animate();
  };

  Parking3DViewer.prototype._setupLighting = function () {
    var ambientLight = new THREE.AmbientLight(0xffffff, 0.65);
    this.scene.add(ambientLight);

    var hemiLight = new THREE.HemisphereLight(0x38bdf8, 0x1e293b, 0.6);
    hemiLight.position.set(0, 40, 0);
    this.scene.add(hemiLight);

    var dirLight = new THREE.DirectionalLight(0xffffff, 0.75);
    dirLight.position.set(25, 40, 20);
    dirLight.castShadow = true;
    dirLight.shadow.mapSize.width = 2048;
    dirLight.shadow.mapSize.height = 2048;
    dirLight.shadow.camera.near = 10;
    dirLight.shadow.camera.far = 160;
    var d = 42;
    dirLight.shadow.camera.left = -d;
    dirLight.shadow.camera.right = d;
    dirLight.shadow.camera.top = d;
    dirLight.shadow.camera.bottom = -d;
    dirLight.shadow.bias = -0.0005;
    this.scene.add(dirLight);

    var fillLight = new THREE.DirectionalLight(0x0284c7, 0.35);
    fillLight.position.set(-25, 20, -20);
    this.scene.add(fillLight);
  };

  /**
   * DỰNG MÔ HÌNH HẦM ĐỖ XE NGẦM (Theo phong cách Sketchfab):
   *   - Sàn epoxy/bê tông công nghiệp xám sẫm
   *   - Tường bê tông bao quanh 4 phía có dải phân cách vàng/xanh
   *   - Hệ thống ống thông gió HVAC mạ kẽm chạy dọc trần
   *   - Đường ống chữa cháy PCCC màu đỏ rực
   *   - Dầm bê tông ngang chịu lực & Máng đèn tuýp LED trần công nghiệp
   *   - Gờ giảm tốc & Biển báo hầm ngầm (2.2m, 5 km/h, EXIT)
   *   - KHỐI CẦU THANG BỘ & BUỒNG THANG MÁY KÍNH
   */
  Parking3DViewer.prototype._buildUndergroundArchitecture = function () {
    this.structureGroup = new THREE.Group();

    var floorW = 68;
    var floorL = 40;
    var ceilingH = 5.2;

    // 1. Mặt sàn bê tông/epoxy công nghiệp
    var floorGeo = new THREE.BoxGeometry(floorW, 0.8, floorL);
    var floorMat = new THREE.MeshStandardMaterial({ color: 0x1e293b, roughness: 0.8, metalness: 0.15 });
    var floorMesh = new THREE.Mesh(floorGeo, floorMat);
    floorMesh.position.y = -0.4;
    floorMesh.receiveShadow = true;
    this.structureGroup.add(floorMesh);

    // 2. Tường bê tông bao quanh 4 phía (Perimeter Concrete Walls)
    var wallMat = new THREE.MeshStandardMaterial({ color: 0x334155, roughness: 0.9 });
    var wallStripeMat = new THREE.MeshBasicMaterial({ color: 0xfacc15 }); // Dải sơn vàng an toàn

    // Tường Bắc (Z = -floorL/2)
    var wallNorth = new THREE.Mesh(new THREE.BoxGeometry(floorW, ceilingH, 0.8), wallMat);
    wallNorth.position.set(0, ceilingH / 2, -floorL / 2);
    this.structureGroup.add(wallNorth);
    var stripeN = new THREE.Mesh(new THREE.BoxGeometry(floorW, 0.5, 0.85), wallStripeMat);
    stripeN.position.set(0, 1.6, -floorL / 2);
    this.structureGroup.add(stripeN);

    // Tường Nam (Z = floorL/2)
    var wallSouth = new THREE.Mesh(new THREE.BoxGeometry(floorW, ceilingH, 0.8), wallMat);
    wallSouth.position.set(0, ceilingH / 2, floorL / 2);
    this.structureGroup.add(wallSouth);
    var stripeS = new THREE.Mesh(new THREE.BoxGeometry(floorW, 0.5, 0.85), wallStripeMat);
    stripeS.position.set(0, 1.6, floorL / 2);
    this.structureGroup.add(stripeS);

    // Tường Tây (X = -floorW/2)
    var wallWest = new THREE.Mesh(new THREE.BoxGeometry(0.8, ceilingH, floorL), wallMat);
    wallWest.position.set(-floorW / 2, ceilingH / 2, 0);
    this.structureGroup.add(wallWest);

    // 3. Đường xe chạy trung tâm (Driveway Lane)
    var laneGeo = new THREE.PlaneGeometry(floorW - 8, 7.8);
    var laneMat = new THREE.MeshStandardMaterial({ color: 0x0f172a, roughness: 0.95 });
    var laneMesh = new THREE.Mesh(laneGeo, laneMat);
    laneMesh.rotation.x = -Math.PI / 2;
    laneMesh.position.y = 0.01;
    laneMesh.receiveShadow = true;
    this.structureGroup.add(laneMesh);

    // Vạch kẻ tim đường vàng đứt đoạn
    for (var x = -26; x <= 22; x += 4) {
      var dash = new THREE.Mesh(new THREE.PlaneGeometry(2, 0.25), new THREE.MeshBasicMaterial({ color: 0xfacc15 }));
      dash.rotation.x = -Math.PI / 2;
      dash.position.set(x, 0.02, 0);
      this.structureGroup.add(dash);
    }

    // 4. Mũi tên dẫn hướng trên đường chạy
    this._addDirectionalArrow(-14, 0, false);
    this._addDirectionalArrow(8, 0, false);

    // 5. Gờ giảm tốc màu vàng-đen (Speed Bumps)
    this._addSpeedBump(-20, 0);
    this._addSpeedBump(16, 0);

    // 6. Cột bê tông phân khu có sơn phản quang vàng đen
    var colCoords = [
      { x: -18, z: -4.5, label: 'A1' },
      { x: -2,  z: -4.5, label: 'A2' },
      { x: 14,  z: -4.5, label: 'A3' },
      { x: -18, z: 4.5,  label: 'B1' },
      { x: -2,  z: 4.5,  label: 'B2' },
      { x: 14,  z: 4.5,  label: 'B3' }
    ];

    var colGeo = new THREE.BoxGeometry(1.3, ceilingH, 1.3);
    var colMat = new THREE.MeshStandardMaterial({ color: 0x475569, roughness: 0.7 });
    var stripeDecalMat = new THREE.MeshBasicMaterial({ color: 0xfacc15 });

    for (var i = 0; i < colCoords.length; i++) {
      var c = colCoords[i];
      var colMesh = new THREE.Mesh(colGeo, colMat);
      colMesh.position.set(c.x, ceilingH / 2, c.z);
      colMesh.castShadow = true;
      colMesh.receiveShadow = true;
      this.structureGroup.add(colMesh);

      var decal = new THREE.Mesh(new THREE.BoxGeometry(1.35, 0.7, 1.35), stripeDecalMat);
      decal.position.set(c.x, 1.4, c.z);
      this.structureGroup.add(decal);

      this._addTextSprite(c.label, c.x, 3.8, c.z, '#38bdf8', 1.8);
    }

    // 7. HỆ THỐNG KỸ THUẬT TRẦN HẦM (Ceiling Beams, HVAC Ducts, Red Pipes, LED fixtures)
    this._buildCeilingInfrastructure(floorW, floorL, ceilingH);

    // 8. KHỐI CẦU THANG BỘ & BUỒNG THANG MÁY (Elevator & Staircase Core)
    this._buildElevatorAndStaircase(26, 0, ceilingH);

    // 9. Biển báo hầm ngầm treo trần
    this._addCeilingSign(-12, ceilingH - 0.7, 0, '⚠️ CHIỀU CAO TỐI ĐA 2.2M · TỐC ĐỘ 5 KM/H', '#facc15');
    this._addCeilingSign(10, ceilingH - 0.7, 0, '🟢 LỐI RA CỔNG / EXIT →', '#34d399');

    this.scene.add(this.structureGroup);
  };

  /**
   * Dựng hệ thống kỹ thuật trần hầm xe ngầm:
   *   - Ống thông gió HVAC mạ kẽm (Galvanized ducts)
   *   - Ống chữa cháy PCCC màu đỏ (Fire pipes)
   *   - Dầm bê tông ngang
   *   - Đèn tuýp LED chiếu sáng
   */
  Parking3DViewer.prototype._buildCeilingInfrastructure = function (width, length, height) {
    var ductMat = new THREE.MeshStandardMaterial({ color: 0x94a3b8, metalness: 0.8, roughness: 0.3 });
    var firePipeMat = new THREE.MeshStandardMaterial({ color: 0xdc2626, metalness: 0.5, roughness: 0.4 });
    var beamMat = new THREE.MeshStandardMaterial({ color: 0x334155, roughness: 0.9 });
    var ledMat = new THREE.MeshBasicMaterial({ color: 0xffffff });

    // Dầm bê tông ngang chịu lực bắc qua trần
    for (var x = -24; x <= 24; x += 12) {
      var beam = new THREE.Mesh(new THREE.BoxGeometry(0.9, 0.6, length), beamMat);
      beam.position.set(x, height - 0.3, 0);
      this.structureGroup.add(beam);
    }

    // Ống thông gió HVAC chữ nhật chạy dọc
    var hvacDuct1 = new THREE.Mesh(new THREE.BoxGeometry(width - 6, 0.55, 1.1), ductMat);
    hvacDuct1.position.set(0, height - 0.65, -3.2);
    this.structureGroup.add(hvacDuct1);

    var hvacDuct2 = new THREE.Mesh(new THREE.BoxGeometry(width - 6, 0.55, 1.1), ductMat);
    hvacDuct2.position.set(0, height - 0.65, 3.2);
    this.structureGroup.add(hvacDuct2);

    // Đường ống PCCC màu đỏ tròn chạy song song
    var firePipe1 = new THREE.Mesh(new THREE.CylinderGeometry(0.08, 0.08, width - 6, 8), firePipeMat);
    firePipe1.rotation.z = Math.PI / 2;
    firePipe1.position.set(0, height - 1.05, -3.8);
    this.structureGroup.add(firePipe1);

    var firePipe2 = new THREE.Mesh(new THREE.CylinderGeometry(0.08, 0.08, width - 6, 8), firePipeMat);
    firePipe2.rotation.z = Math.PI / 2;
    firePipe2.position.set(0, height - 1.05, 3.8);
    this.structureGroup.add(firePipe2);

    // Đèn tuýp LED trần công nghiệp
    for (var lx = -22; lx <= 22; lx += 8) {
      var ledFixture = new THREE.Mesh(new THREE.BoxGeometry(3.5, 0.1, 0.25), ledMat);
      ledFixture.position.set(lx, height - 0.4, 0);
      this.structureGroup.add(ledFixture);

      var spotLight = new THREE.PointLight(0xf8fafc, 0.4, 18);
      spotLight.position.set(lx, height - 0.8, 0);
      this.structureGroup.add(spotLight);
    }
  };

  /**
   * DỰNG CỤM CẦU THANG BỘ & BUỒNG THANG MÁY 3D (Elevator & Staircase Core):
   *   - Buồng thang máy kính & khung kim loại sang trọng
   *   - Cửa inox có đèn báo tầng LED
   *   - Cầu thang bộ có các bậc thang & tay vịn inox
   *   - Cửa thoát hiểm có đèn EXIT dạ quang
   *   - Nút gọi thang máy 3D tương tác trực tiếp
   */
  Parking3DViewer.prototype._buildElevatorAndStaircase = function (x, z, height) {
    this.elevatorGroup = new THREE.Group();
    this.elevatorGroup.position.set(x, 0, z);

    var glassMat = new THREE.MeshStandardMaterial({
      color: 0x38bdf8,
      metalness: 0.9,
      roughness: 0.1,
      transparent: true,
      opacity: 0.35
    });
    var metalMat = new THREE.MeshStandardMaterial({ color: 0x1e293b, metalness: 0.8, roughness: 0.2 });
    var doorMat = new THREE.MeshStandardMaterial({ color: 0xe2e8f0, metalness: 0.85, roughness: 0.2 });

    // 1. BUỒNG THANG MÁY (Elevator Shaft)
    // Khung kim loại & kính buồng thang máy
    var shaft = new THREE.Mesh(new THREE.BoxGeometry(4.2, height, 4.2), glassMat);
    shaft.position.y = height / 2;
    shaft.castShadow = true;
    this.elevatorGroup.add(shaft);

    // Khung viền góc thang máy bằng thép
    var frameMat = new THREE.MeshStandardMaterial({ color: 0x0f172a, metalness: 0.9 });
    for (var cx = -2.1; cx <= 2.1; cx += 4.2) {
      for (var cz = -2.1; cz <= 2.1; cz += 4.2) {
        var post = new THREE.Mesh(new THREE.BoxGeometry(0.25, height, 0.25), frameMat);
        post.position.set(cx, height / 2, cz);
        this.elevatorGroup.add(post);
      }
    }

    // Cabin thang máy bên trong
    var cabin = new THREE.Mesh(new THREE.BoxGeometry(3.6, 2.6, 3.6), metalMat);
    cabin.position.set(0, 1.4, 0);
    this.elevatorGroup.add(cabin);

    // Cửa trượt thang máy inox ở mặt trước (hướng về phía Tây: X âm)
    var doorL = new THREE.Mesh(new THREE.BoxGeometry(0.08, 2.3, 1.1), doorMat);
    doorL.position.set(-2.12, 1.25, -0.6);
    var doorR = new THREE.Mesh(new THREE.BoxGeometry(0.08, 2.3, 1.1), doorMat);
    doorR.position.set(-2.12, 1.25, 0.6);
    this.elevatorGroup.add(doorL);
    this.elevatorGroup.add(doorR);

    // Bảng LED điện tử hiển thị tầng và chiều di chuyển trên cửa thang máy
    this.elevScreenSprite = this._addTextSprite('🛗 [ ' + this.options.floorId + ' ] ▲', -2.15, 2.7, 0, '#38bdf8', 2.0);
    this.elevScreenSprite.rotation.y = -Math.PI / 2;
    this.elevatorGroup.add(this.elevScreenSprite);

    // 2. KHỐI CẦU THANG BỘ (Staircase Well)
    var stairGroup = new THREE.Group();
    stairGroup.position.set(0, 0, -4.8);

    // Tường bao buồng cầu thang
    var stairWall = new THREE.Mesh(new THREE.BoxGeometry(4.2, height, 3.8), new THREE.MeshStandardMaterial({ color: 0x334155, roughness: 0.9 }));
    stairWall.position.y = height / 2;
    stairGroup.add(stairWall);

    // Các bậc thang bê tông (Steps)
    var stepMat = new THREE.MeshStandardMaterial({ color: 0x475569 });
    for (var s = 0; s < 8; s++) {
      var step = new THREE.Mesh(new THREE.BoxGeometry(1.6, 0.2, 0.4), stepMat);
      step.position.set(-0.6, s * 0.22 + 0.11, -1.2 + s * 0.35);
      stairGroup.add(step);
    }

    // Tay vịn inox (Handrail)
    var rail = new THREE.Mesh(new THREE.CylinderGeometry(0.04, 0.04, 3.2), doorMat);
    rail.rotation.x = Math.PI / 3.8;
    rail.position.set(0.25, 1.3, 0.1);
    stairGroup.add(rail);

    // Cửa thoát hiểm chống cháy (Fire Exit Door)
    var fireDoor = new THREE.Mesh(new THREE.BoxGeometry(0.08, 2.3, 1.4), new THREE.MeshStandardMaterial({ color: 0x16a34a }));
    fireDoor.position.set(-2.12, 1.25, 0);
    stairGroup.add(fireDoor);

    var exitSign = this._addTextSprite('EXIT / CẦU THANG', -2.15, 2.7, 0, '#4ade80', 1.8);
    exitSign.rotation.y = -Math.PI / 2;
    stairGroup.add(exitSign);

    this.elevatorGroup.add(stairGroup);

    // 3. BẢNG NÚT BẤM GỌI TẦNG 3D NGAY TẠI THANG MÁY
    var callPanel = new THREE.Mesh(new THREE.BoxGeometry(0.06, 0.8, 0.45), new THREE.MeshStandardMaterial({ color: 0x0f172a }));
    callPanel.position.set(-2.13, 1.4, -1.4);
    this.elevatorGroup.add(callPanel);

    // Nút ▲ Lên 3D
    var upBtnMesh = new THREE.Mesh(new THREE.CylinderGeometry(0.07, 0.07, 0.03), new THREE.MeshBasicMaterial({ color: 0x38bdf8 }));
    upBtnMesh.rotation.z = Math.PI / 2;
    upBtnMesh.position.set(-2.16, 1.55, -1.4);
    upBtnMesh.userData = { action: 'ELEV_UP' };
    this.elevatorGroup.add(upBtnMesh);

    // Nút ▼ Xuống 3D
    var downBtnMesh = new THREE.Mesh(new THREE.CylinderGeometry(0.07, 0.07, 0.03), new THREE.MeshBasicMaterial({ color: 0xf59e0b }));
    downBtnMesh.rotation.z = Math.PI / 2;
    downBtnMesh.position.set(-2.16, 1.3, -1.4);
    downBtnMesh.userData = { action: 'ELEV_DOWN' };
    this.elevatorGroup.add(downBtnMesh);

    this.structureGroup.add(this.elevatorGroup);
  };

  /**
   * Biển báo treo trần hầm xe
   */
  Parking3DViewer.prototype._addCeilingSign = function (x, y, z, text, color) {
    var canvas = document.createElement('canvas');
    canvas.width = 512;
    canvas.height = 72;
    var ctx = canvas.getContext('2d');
    ctx.fillStyle = 'rgba(15, 23, 42, 0.9)';
    ctx.fillRect(0, 0, 512, 72);
    ctx.strokeStyle = color || '#ffffff';
    ctx.lineWidth = 4;
    ctx.strokeRect(2, 2, 508, 68);
    ctx.fillStyle = color || '#ffffff';
    ctx.font = 'bold 24px "Be Vietnam Pro", sans-serif';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText(text, 256, 36);

    var texture = new THREE.CanvasTexture(canvas);
    var board = new THREE.Mesh(new THREE.PlaneGeometry(6.4, 0.9), new THREE.MeshBasicMaterial({ map: texture, side: THREE.DoubleSide }));
    board.position.set(x, y, z);
    this.structureGroup.add(board);
  };

  /**
   * Gờ giảm tốc màu vàng đen (Speed Bump)
   */
  Parking3DViewer.prototype._addSpeedBump = function (x, z) {
    var bumpGeo = new THREE.BoxGeometry(0.45, 0.08, 7.5);
    var bumpMat = new THREE.MeshStandardMaterial({ color: 0xfacc15, roughness: 0.7 });
    var bump = new THREE.Mesh(bumpGeo, bumpMat);
    bump.position.set(x, 0.04, z);
    this.structureGroup.add(bump);
  };

  Parking3DViewer.prototype._addDirectionalArrow = function (x, z, reverse) {
    var shape = new THREE.Shape();
    shape.moveTo(0, 1.2);
    shape.lineTo(0.7, 0.4);
    shape.lineTo(0.3, 0.4);
    shape.lineTo(0.3, -1.0);
    shape.lineTo(-0.3, -1.0);
    shape.lineTo(-0.3, 0.4);
    shape.lineTo(-0.7, 0.4);
    shape.closePath();

    var geo = new THREE.ShapeGeometry(shape);
    var mat = new THREE.MeshBasicMaterial({ color: 0xffffff, side: THREE.DoubleSide });
    var mesh = new THREE.Mesh(geo, mat);
    mesh.rotation.x = -Math.PI / 2;
    mesh.rotation.z = reverse ? -Math.PI / 2 : Math.PI / 2;
    mesh.position.set(x, 0.02, z);
    this.structureGroup.add(mesh);
  };

  Parking3DViewer.prototype._addTextSprite = function (text, x, y, z, color, scale) {
    scale = scale || 2.0;
    var canvas = document.createElement('canvas');
    canvas.width = 256;
    canvas.height = 64;
    var ctx = canvas.getContext('2d');
    ctx.font = 'bold 28px "JetBrains Mono", sans-serif';
    ctx.fillStyle = color || '#ffffff';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText(text, 128, 32);

    var texture = new THREE.CanvasTexture(canvas);
    var mat = new THREE.SpriteMaterial({ map: texture, transparent: true });
    var sprite = new THREE.Sprite(mat);
    sprite.position.set(x, y, z);
    sprite.scale.set(scale * 2.2, scale * 0.55, 1);
    this.structureGroup.add(sprite);
    return sprite;
  };

  /**
   * Đổi Bãi Xe (1: 2 tầng chuẩn DB, 2: 3 tầng random, 3: 4 tầng random)
   */
  Parking3DViewer.prototype.setLot = function (lotId) {
    lotId = parseInt(lotId, 10);
    if (!LOT_CONFIGS[lotId]) lotId = 1;

    this.options.lotId = lotId;
    var lotCfg = LOT_CONFIGS[lotId];
    this.options.lotName = lotCfg.name;
    this.options.floorId = lotCfg.floors[0];

    this._renderTopHUD();
    this._updateElevatorWidget();
    this.loadSlots(this.options.floorId);
    this.resetView();
  };

  /**
   * Đổi tầng kèm hoạt cảnh di chuyển thang máy (Elevator ride animation)
   */
  Parking3DViewer.prototype.setFloor = function (floorId, direction) {
    if (this.options.floorId === floorId && !direction) return;

    var lotCfg = LOT_CONFIGS[this.options.lotId] || LOT_CONFIGS[1];
    var floors = lotCfg.floors;
    var oldIdx = floors.indexOf(this.options.floorId);
    var newIdx = floors.indexOf(floorId);

    if (!direction && newIdx !== -1 && oldIdx !== -1) {
      direction = newIdx > oldIdx ? 'DOWN' : 'UP';
    }

    this.options.floorId = floorId;
    this._renderTopHUD();
    this._updateElevatorWidget();

    // Hiển thị Toast thông báo di chuyển
    this._showFloorTransitionToast(lotCfg.floorLabels[floorId] || floorId, direction);

    // Cập nhật bảng đèn thang máy
    if (this.elevScreenSprite) {
      this.structureGroup.remove(this.elevScreenSprite);
      var arrowIcon = direction === 'UP' ? '▲' : '▼';
      this.elevScreenSprite = this._addTextSprite('🛗 [ ' + floorId + ' ] ' + arrowIcon, 23.85, 2.7, 0, '#38bdf8', 2.0);
    }

    // Hoạt cảnh camera trượt theo phương thẳng đứng (mô phỏng đi thang máy)
    this._animateElevatorRide(direction, function () {
      this.loadSlots(floorId);
    }.bind(this));

    if (this.options.onFloorChange) {
      this.options.onFloorChange(floorId);
    }
  };

  Parking3DViewer.prototype._showFloorTransitionToast = function (floorLabel, direction) {
    var oldToast = this.container.querySelector('.p3d-toast');
    if (oldToast) oldToast.remove();

    var toast = document.createElement('div');
    toast.className = 'p3d-toast';
    var icon = direction === 'UP' ? '▲' : '▼';
    toast.innerHTML = '<span>🛗</span><span>Đang ' + (direction === 'UP' ? 'đi lên' : 'đi xuống') + ' ' + floorLabel + '... ' + icon + '</span>';
    this.container.appendChild(toast);

    setTimeout(function () {
      if (toast && toast.parentNode) toast.remove();
    }, 1800);
  };

  /**
   * Hoạt cảnh camera chuyển tầng
   */
  Parking3DViewer.prototype._animateElevatorRide = function (direction, onComplete) {
    var self = this;
    if (this.isTransitioning) return;
    this.isTransitioning = true;

    var startY = this.camera.position.y;
    var targetY = direction === 'UP' ? startY + 10 : startY - 10;
    var startTime = performance.now();
    var duration = 650;

    function step(now) {
      var progress = Math.min((now - startTime) / duration, 1);
      var ease = 0.5 - 0.5 * Math.cos(progress * Math.PI);

      if (progress < 0.5) {
        self.camera.position.y = startY + (targetY - startY) * (ease * 2);
      } else {
        self.camera.position.y = targetY - (targetY - startY) * ((ease - 0.5) * 2);
      }

      if (progress < 1) {
        requestAnimationFrame(step);
      } else {
        self.camera.position.y = startY;
        self.isTransitioning = false;
        if (onComplete) onComplete();
      }
    }

    requestAnimationFrame(step);
  };

  /**
   * Tải danh sách ô đỗ từ API hoặc fallback
   */
  Parking3DViewer.prototype.loadSlots = function (floorId) {
    var self = this;
    var targetFloor = floorId || this.options.floorId;
    this.options.floorId = targetFloor;

    if (this.loaderEl) {
      this.loaderEl.style.opacity = '1';
      this.loaderEl.style.pointerEvents = 'auto';
    }

    var url = this.options.apiEndpoint + '?lotId=' + this.options.lotId + '&floorId=' + targetFloor;

    fetch(url)
      .then(function (res) {
        if (!res.ok) throw new Error('API returned ' + res.status);
        return res.json();
      })
      .then(function (data) {
        var slotsList = data.slots || data.data || (Array.isArray(data) ? data : []);
        self._renderSlots(slotsList);
      })
      .catch(function () {
        self._renderFallbackSlots(self.options.lotId, targetFloor);
      })
      .finally(function () {
        if (self.loaderEl) {
          self.loaderEl.style.opacity = '0';
          setTimeout(function () { self.loaderEl.style.pointerEvents = 'none'; }, 400);
        }
      });
  };

  /**
   * Dữ liệu fallback cho 3 bãi mẫu
   */
  Parking3DViewer.prototype._renderFallbackSlots = function (lotId, floorId) {
    var sampleSlots = [];
    var countPerRow = 16; // Bãi 1: 16 x 2 = 32 ô/tầng (chuẩn DB)

    if (lotId === 2) {
      countPerRow = floorId === 'B1' ? 14 : (floorId === 'B2' ? 12 : 15);
    } else if (lotId === 3) {
      countPerRow = floorId === 'B1' ? 13 : (floorId === 'B2' ? 15 : (floorId === 'B3' ? 12 : 16));
    }

    var spacing = 3.3;
    var startX = -((countPerRow - 1) * spacing) / 2.0;

    // Dãy A: Z = -9
    for (var i = 1; i <= countPerRow; i++) {
      var codeA = floorId + '-A' + (i < 10 ? '0' : '') + i;
      var stA = (i % 3 === 0) ? 'OCCUPIED' : (i === 3 ? 'RESERVED' : (i === 8 ? 'MAINTENANCE' : 'EMPTY'));
      sampleSlots.push({
        id: i,
        slotCode: codeA,
        physicalStatus: stA,
        vehicleType: 'car',
        positionX: startX + ((i - 1) * spacing),
        positionY: 0,
        positionZ: -9,
        rotationY: 0,
        currentCarPlate: stA === 'OCCUPIED' ? '51F-123.' + (i * 11) : (stA === 'RESERVED' ? '72A-888.99' : null),
        vehicleModel: stA === 'OCCUPIED' ? 'Toyota Vios' : null,
        vehicleColor: 'white'
      });
    }

    // Dãy B: Z = +9
    for (var j = 1; j <= countPerRow; j++) {
      var codeB = floorId + '-B' + (j < 10 ? '0' : '') + j;
      var stB = (j % 4 === 0) ? 'OCCUPIED' : (j === 5 ? 'RESERVED' : (j === 9 ? 'UNAVAILABLE' : 'EMPTY'));
      sampleSlots.push({
        id: 100 + j,
        slotCode: codeB,
        physicalStatus: stB,
        vehicleType: j <= 4 ? 'ev' : 'car',
        positionX: startX + ((j - 1) * spacing),
        positionY: 0,
        positionZ: 9,
        rotationY: 180,
        currentCarPlate: stB === 'OCCUPIED' ? '30H-999.' + (j * 11) : (stB === 'RESERVED' ? '51K-777.66' : null),
        vehicleModel: stB === 'OCCUPIED' ? 'VinFast VF8' : null,
        vehicleColor: 'emerald'
      });
    }

    this._renderSlots(sampleSlots);
  };

  /**
   * Render danh sách ô đỗ
   */
  Parking3DViewer.prototype._renderSlots = function (slotsData) {
    var self = this;
    this.slots = slotsData;

    if (this.slotsGroup) {
      this.scene.remove(this.slotsGroup);
    }
    this.slotsGroup = new THREE.Group();
    this.slotMeshes = {};
    this.carMeshes = {};
    this.coneMeshes = {};
    this.pulsingSlots = [];

    slotsData.forEach(function (slot) {
      self._createSlotMesh(slot);
    });

    this.scene.add(this.slotsGroup);
    this._updateStats();
  };

  /**
   * Tạo 3D Mesh cho một ô đỗ:
   *   - Mặt sàn ô đỗ
   *   - Vạch kẻ ô chữ U
   *   - Thanh chặn bánh xe cao su (Rubber Tire Curbs)
   *   - Xe 3D (nếu OCCUPIED)
   *   - Cọc tiêu 3D (nếu MAINTENANCE)
   *   - Viền nhấp nháy phát sáng (nếu RESERVED)
   */
  Parking3DViewer.prototype._createSlotMesh = function (slot) {
    var group = new THREE.Group();
    group.position.set(slot.positionX || 0, slot.positionY || 0, slot.positionZ || 0);
    group.rotation.y = (slot.rotationY || 0) * (Math.PI / 180);
    group.userData = { slotData: slot };

    var width = 2.8;
    var length = 5.4;
    var status = (slot.physicalStatus || 'EMPTY').toUpperCase();
    var statusCfg = STATUS_COLORS[status] || STATUS_COLORS.EMPTY;

    // 1. Mặt sàn ô đỗ
    var padGeo = new THREE.PlaneGeometry(width, length);
    var padMat = new THREE.MeshStandardMaterial({
      color: statusCfg.hex,
      roughness: 0.7,
      metalness: 0.1,
      transparent: true,
      opacity: status === 'OCCUPIED' ? 0.35 : (status === 'RESERVED' ? 0.45 : (status === 'EMPTY' ? 0.22 : 0.4))
    });
    var pad = new THREE.Mesh(padGeo, padMat);
    pad.rotation.x = -Math.PI / 2;
    pad.position.y = 0.015;
    pad.receiveShadow = true;
    group.add(pad);

    // 2. Vạch kẻ chữ U
    var lineMat = new THREE.MeshBasicMaterial({
      color: status === 'RESERVED' ? 0x06b6d4 : (slot.vehicleType === 'ev' ? 0x10b981 : 0xffffff)
    });
    var lineWidth = 0.14;

    var lLeft = new THREE.Mesh(new THREE.PlaneGeometry(lineWidth, length), lineMat);
    lLeft.rotation.x = -Math.PI / 2;
    lLeft.position.set(-width / 2, 0.02, 0);
    group.add(lLeft);

    var lRight = new THREE.Mesh(new THREE.PlaneGeometry(lineWidth, length), lineMat);
    lRight.rotation.x = -Math.PI / 2;
    lRight.position.set(width / 2, 0.02, 0);
    group.add(lRight);

    var lBack = new THREE.Mesh(new THREE.PlaneGeometry(width, lineWidth), lineMat);
    lBack.rotation.x = -Math.PI / 2;
    lBack.position.set(0, 0.02, length / 2);
    group.add(lBack);

    // 3. THANH CHẶN BÁNH XE CAO SU (Tire Curbs / Rubber Stoppers)
    var stopperMat = new THREE.MeshStandardMaterial({ color: 0x111827, roughness: 0.9 });
    var stopperStripeMat = new THREE.MeshBasicMaterial({ color: 0xfacc15 });

    var stp1 = new THREE.Mesh(new THREE.BoxGeometry(0.85, 0.14, 0.22), stopperMat);
    stp1.position.set(-0.7, 0.07, length / 2 - 0.5);
    group.add(stp1);
    var stp1Stripe = new THREE.Mesh(new THREE.BoxGeometry(0.3, 0.142, 0.225), stopperStripeMat);
    stp1Stripe.position.set(-0.7, 0.071, length / 2 - 0.5);
    group.add(stp1Stripe);

    var stp2 = new THREE.Mesh(new THREE.BoxGeometry(0.85, 0.14, 0.22), stopperMat);
    stp2.position.set(0.7, 0.07, length / 2 - 0.5);
    group.add(stp2);
    var stp2Stripe = new THREE.Mesh(new THREE.BoxGeometry(0.3, 0.142, 0.225), stopperStripeMat);
    stp2Stripe.position.set(0.7, 0.071, length / 2 - 0.5);
    group.add(stp2Stripe);

    // Trụ sạc nếu xe điện
    if (slot.vehicleType === 'ev') {
      var evCharger = this._createEVChargerPost();
      evCharger.position.set(0, 0, length / 2 + 0.4);
      group.add(evCharger);
    }

    // Nhãn mã ô sơn mặt sàn
    var codeSprite = this._createSlotCodeSprite(slot.slotCode, statusCfg.css);
    codeSprite.position.set(0, 0.03, -length / 2 + 0.7);
    codeSprite.rotation.x = -Math.PI / 2;
    group.add(codeSprite);

    // 4. Render chi tiết theo trạng thái vật lý
    if (status === 'OCCUPIED') {
      var car = this._create3DCarMesh(slot);
      car.position.set(0, 0, 0);
      group.add(car);
      this.carMeshes[slot.slotCode] = car;
    } else if (status === 'MAINTENANCE') {
      var cone = this._createTrafficConeMesh();
      cone.position.set(0, 0, 0);
      group.add(cone);
      this.coneMeshes[slot.slotCode] = cone;
    } else if (status === 'RESERVED') {
      var pulseFrame = this._createPulsingFrame(width, length);
      group.add(pulseFrame);
      this.pulsingSlots.push({ slotCode: slot.slotCode, frame: pulseFrame, pad: pad });

      var beacon = this._createReservedBeacon(slot.currentCarPlate || 'ĐANG ĐẾN');
      beacon.position.set(0, 2.2, 0);
      group.add(beacon);
    } else if (status === 'UNAVAILABLE') {
      var crossLines = this._createCrossMark(width, length);
      group.add(crossLines);
    }

    this.slotMeshes[slot.slotCode] = group;
    this.slotsGroup.add(group);
  };

  Parking3DViewer.prototype._create3DCarMesh = function (slot) {
    var car = new THREE.Group();
    var color = 0xf8fafc;
    if (slot.vehicleColor === 'black') color = 0x1e293b;
    else if (slot.vehicleColor === 'blue') color = 0x2563eb;
    else if (slot.vehicleColor === 'red') color = 0xdc2626;
    else if (slot.vehicleColor === 'emerald') color = 0x059669;

    var carWidth = 2.0;
    var carLength = 4.4;
    var carHeight = 0.85;

    var bodyGeo = new THREE.BoxGeometry(carWidth, carHeight, carLength);
    var bodyMat = new THREE.MeshStandardMaterial({ color: color, metalness: 0.6, roughness: 0.3 });
    var body = new THREE.Mesh(bodyGeo, bodyMat);
    body.position.y = 0.65;
    body.castShadow = true;
    body.receiveShadow = true;
    car.add(body);

    var cabinGeo = new THREE.BoxGeometry(carWidth * 0.85, 0.65, carLength * 0.55);
    var glassMat = new THREE.MeshStandardMaterial({ color: 0x0f172a, roughness: 0.1, metalness: 0.9, transparent: true, opacity: 0.85 });
    var cabin = new THREE.Mesh(cabinGeo, glassMat);
    cabin.position.set(0, 1.3, -0.2);
    cabin.castShadow = true;
    car.add(cabin);

    var wheelGeo = new THREE.CylinderGeometry(0.35, 0.35, 0.28, 16);
    var wheelMat = new THREE.MeshStandardMaterial({ color: 0x111827, roughness: 0.9 });
    var rimMat = new THREE.MeshStandardMaterial({ color: 0xe2e8f0, metalness: 0.8, roughness: 0.2 });

    var wheelPositions = [
      { x: -carWidth / 2 - 0.05, z: -1.3 },
      { x:  carWidth / 2 + 0.05, z: -1.3 },
      { x: -carWidth / 2 - 0.05, z:  1.3 },
      { x:  carWidth / 2 + 0.05, z:  1.3 }
    ];

    wheelPositions.forEach(function (pos) {
      var w = new THREE.Mesh(wheelGeo, wheelMat);
      w.rotation.z = Math.PI / 2;
      w.position.set(pos.x, 0.35, pos.z);
      w.castShadow = true;
      var rim = new THREE.Mesh(new THREE.CylinderGeometry(0.2, 0.2, 0.29, 8), rimMat);
      w.add(rim);
      car.add(w);
    });

    var headLightMat = new THREE.MeshBasicMaterial({ color: 0xffffff });
    var tailLightMat = new THREE.MeshBasicMaterial({ color: 0xef4444 });

    var hl1 = new THREE.Mesh(new THREE.BoxGeometry(0.35, 0.18, 0.1), headLightMat);
    hl1.position.set(-0.65, 0.75, -carLength / 2 - 0.01);
    var hl2 = hl1.clone();
    hl2.position.x = 0.65;
    car.add(hl1);
    car.add(hl2);

    var tl1 = new THREE.Mesh(new THREE.BoxGeometry(0.4, 0.18, 0.1), tailLightMat);
    tl1.position.set(-0.65, 0.75, carLength / 2 + 0.01);
    var tl2 = tl1.clone();
    tl2.position.x = 0.65;
    car.add(tl1);
    car.add(tl2);

    if (slot.currentCarPlate) {
      var plateSprite = this._createLicensePlateSprite(slot.currentCarPlate);
      plateSprite.position.set(0, 0.55, -carLength / 2 - 0.06);
      car.add(plateSprite);

      var plateRear = plateSprite.clone();
      plateRear.position.z = carLength / 2 + 0.06;
      plateRear.rotation.y = Math.PI;
      car.add(plateRear);
    }

    return car;
  };

  Parking3DViewer.prototype._createTrafficConeMesh = function () {
    var coneGroup = new THREE.Group();
    var base = new THREE.Mesh(new THREE.BoxGeometry(0.8, 0.08, 0.8), new THREE.MeshStandardMaterial({ color: 0xf97316, roughness: 0.5 }));
    base.position.y = 0.04;
    coneGroup.add(base);

    var cone = new THREE.Mesh(new THREE.ConeGeometry(0.32, 1.2, 16), new THREE.MeshStandardMaterial({ color: 0xf97316, roughness: 0.5 }));
    cone.position.y = 0.64;
    cone.castShadow = true;
    coneGroup.add(cone);

    var stripe = new THREE.Mesh(new THREE.CylinderGeometry(0.22, 0.26, 0.3, 16), new THREE.MeshBasicMaterial({ color: 0xffffff }));
    stripe.position.y = 0.65;
    coneGroup.add(stripe);

    return coneGroup;
  };

  Parking3DViewer.prototype._createEVChargerPost = function () {
    var evGroup = new THREE.Group();
    var post = new THREE.Mesh(new THREE.BoxGeometry(0.4, 1.4, 0.3), new THREE.MeshStandardMaterial({ color: 0x059669, roughness: 0.4 }));
    post.position.y = 0.7;
    evGroup.add(post);

    var screen = new THREE.Mesh(new THREE.PlaneGeometry(0.24, 0.35), new THREE.MeshBasicMaterial({ color: 0x34d399 }));
    screen.position.set(0, 0.85, -0.16);
    screen.rotation.y = Math.PI;
    evGroup.add(screen);

    return evGroup;
  };

  Parking3DViewer.prototype._createPulsingFrame = function (width, length) {
    var shape = new THREE.Shape();
    var w2 = width / 2;
    var l2 = length / 2;
    shape.moveTo(-w2, -l2);
    shape.lineTo(w2, -l2);
    shape.lineTo(w2, l2);
    shape.lineTo(-w2, l2);
    shape.closePath();

    var geo = new THREE.BufferGeometry().setFromPoints(shape.getPoints());
    var mat = new THREE.LineBasicMaterial({ color: 0x06b6d4, linewidth: 3, transparent: true, opacity: 0.9 });
    var line = new THREE.LineLoop(geo, mat);
    line.rotation.x = -Math.PI / 2;
    line.position.y = 0.04;
    return line;
  };

  Parking3DViewer.prototype._createReservedBeacon = function (plateText) {
    var beacon = new THREE.Group();
    var glow = new THREE.Mesh(new THREE.SphereGeometry(0.3, 16, 16), new THREE.MeshBasicMaterial({ color: 0x22d3ee, transparent: true, opacity: 0.8 }));
    beacon.add(glow);

    var canvas = document.createElement('canvas');
    canvas.width = 256;
    canvas.height = 64;
    var ctx = canvas.getContext('2d');
    ctx.fillStyle = 'rgba(6, 182, 212, 0.9)';
    ctx.roundRect(4, 4, 248, 56, 12);
    ctx.fill();
    ctx.fillStyle = '#ffffff';
    ctx.font = 'bold 20px sans-serif';
    ctx.textAlign = 'center';
    ctx.fillText('⏳ ' + plateText, 128, 38);

    var texture = new THREE.CanvasTexture(canvas);
    var sprite = new THREE.Sprite(new THREE.SpriteMaterial({ map: texture, transparent: true }));
    sprite.position.y = 0.8;
    sprite.scale.set(3.2, 0.8, 1);
    beacon.add(sprite);

    return beacon;
  };

  Parking3DViewer.prototype._createCrossMark = function (width, length) {
    var g = new THREE.Group();
    var mat = new THREE.MeshBasicMaterial({ color: 0x6b7280 });
    var line1 = new THREE.Mesh(new THREE.PlaneGeometry(0.12, 3.2), mat);
    line1.rotation.x = -Math.PI / 2;
    line1.rotation.z = Math.PI / 4;
    line1.position.y = 0.02;
    var line2 = line1.clone();
    line2.rotation.z = -Math.PI / 4;
    g.add(line1);
    g.add(line2);
    return g;
  };

  Parking3DViewer.prototype._createSlotCodeSprite = function (code, color) {
    var canvas = document.createElement('canvas');
    canvas.width = 128;
    canvas.height = 64;
    var ctx = canvas.getContext('2d');
    ctx.font = 'bold 26px "JetBrains Mono", sans-serif';
    ctx.fillStyle = color || '#ffffff';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText(code, 64, 32);

    var texture = new THREE.CanvasTexture(canvas);
    var geo = new THREE.PlaneGeometry(1.5, 0.75);
    var mat = new THREE.MeshBasicMaterial({ map: texture, transparent: true });
    return new THREE.Mesh(geo, mat);
  };

  Parking3DViewer.prototype._createLicensePlateSprite = function (plate) {
    var canvas = document.createElement('canvas');
    canvas.width = 128;
    canvas.height = 36;
    var ctx = canvas.getContext('2d');
    ctx.fillStyle = '#f8fafc';
    ctx.fillRect(0, 0, 128, 36);
    ctx.strokeStyle = '#0f172a';
    ctx.lineWidth = 3;
    ctx.strokeRect(1, 1, 126, 34);
    ctx.fillStyle = '#0f172a';
    ctx.font = 'bold 18px "JetBrains Mono", sans-serif';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText(plate, 64, 18);

    var texture = new THREE.CanvasTexture(canvas);
    var geo = new THREE.PlaneGeometry(0.9, 0.25);
    var mat = new THREE.MeshBasicMaterial({ map: texture });
    return new THREE.Mesh(geo, mat);
  };

  Parking3DViewer.prototype._updateStats = function () {
    var total = this.slots.length;
    var free = 0, occ = 0, res = 0, blk = 0;

    this.slots.forEach(function (s) {
      var st = (s.physicalStatus || 'EMPTY').toUpperCase();
      if (st === 'EMPTY') free++;
      else if (st === 'OCCUPIED') occ++;
      else if (st === 'RESERVED') res++;
      else if (st === 'MAINTENANCE') blk++;
    });

    var setTxt = function (id, val) {
      var el = document.getElementById(id);
      if (el) el.textContent = val;
    };

    setTxt('statTotal', total);
    setTxt('statFree', free);
    setTxt('statOcc', occ);
    setTxt('statRes', res);
    setTxt('statBlk', blk);
  };

  Parking3DViewer.prototype.filterStatus = function (statusFilter) {
    this.activeFilter = statusFilter || 'ALL';
    var self = this;

    this.slots.forEach(function (slot) {
      var mesh = self.slotMeshes[slot.slotCode];
      if (!mesh) return;

      var st = (slot.physicalStatus || 'EMPTY').toUpperCase();
      var show = (self.activeFilter === 'ALL') || (st === self.activeFilter);
      mesh.visible = show;
    });
  };

  Parking3DViewer.prototype.updateSlotStatus = function (slotCode, newStatus, details) {
    details = details || {};
    var slot = this.slots.find(function (s) {
      return s.slotCode.toUpperCase() === slotCode.toUpperCase();
    });

    if (!slot) return;

    slot.physicalStatus = newStatus.toUpperCase();
    if (details.carPlate) slot.currentCarPlate = details.carPlate;
    if (details.vehicleModel) slot.vehicleModel = details.vehicleModel;
    if (details.vehicleColor) slot.vehicleColor = details.vehicleColor;

    var oldMesh = this.slotMeshes[slot.slotCode];
    if (oldMesh) {
      this.slotsGroup.remove(oldMesh);
    }
    this.pulsingSlots = this.pulsingSlots.filter(function (p) { return p.slotCode !== slot.slotCode; });

    this._createSlotMesh(slot);
    this._updateStats();
  };

  Parking3DViewer.prototype.resetView = function () {
    var self = this;
    var startCam = { x: this.camera.position.x, y: this.camera.position.y, z: this.camera.position.z };
    var startTarget = { x: this.controls.target.x, y: this.controls.target.y, z: this.controls.target.z };

    var endCam = this.defaultCamPos;
    var endTarget = this.defaultTarget;

    var duration = 800;
    var startTime = performance.now();

    function easeInOutQuad(t) {
      return t < 0.5 ? 2 * t * t : -1 + (4 - 2 * t) * t;
    }

    function step(now) {
      var progress = Math.min((now - startTime) / duration, 1);
      var ease = easeInOutQuad(progress);

      self.camera.position.x = startCam.x + (endCam.x - startCam.x) * ease;
      self.camera.position.y = startCam.y + (endCam.y - startCam.y) * ease;
      self.camera.position.z = startCam.z + (endCam.z - startCam.z) * ease;

      self.controls.target.x = startTarget.x + (endTarget.x - startTarget.x) * ease;
      self.controls.target.y = startTarget.y + (endTarget.y - startTarget.y) * ease;
      self.controls.target.z = startTarget.z + (endTarget.z - startTarget.z) * ease;

      self.controls.update();

      if (progress < 1) {
        requestAnimationFrame(step);
      }
    }

    requestAnimationFrame(step);
    this.isTopDown = false;
  };

  Parking3DViewer.prototype.toggleTopDownView = function () {
    if (this.isTopDown) {
      this.resetView();
    } else {
      this.isTopDown = true;
      var self = this;
      var startCam = { x: this.camera.position.x, y: this.camera.position.y, z: this.camera.position.z };
      var endCam = { x: 0, y: 56, z: 0.1 };

      var startTime = performance.now();
      function step(now) {
        var progress = Math.min((now - startTime) / 600, 1);
        self.camera.position.x = startCam.x + (endCam.x - startCam.x) * progress;
        self.camera.position.y = startCam.y + (endCam.y - startCam.y) * progress;
        self.camera.position.z = startCam.z + (endCam.z - startCam.z) * progress;
        self.controls.target.set(0, 0, 0);
        self.controls.update();
        if (progress < 1) requestAnimationFrame(step);
      }
      requestAnimationFrame(step);
    }
  };

  Parking3DViewer.prototype._bindEvents = function () {
    var self = this;
    var dom = this.renderer.domElement;

    dom.addEventListener('pointermove', function (e) {
      var rect = dom.getBoundingClientRect();
      self.mouse.x = ((e.clientX - rect.left) / rect.width) * 2 - 1;
      self.mouse.y = -((e.clientY - rect.top) / rect.height) * 2 + 1;
      self._checkHover(e.clientX, e.clientY);
    });

    dom.addEventListener('pointerdown', function (e) {
      self._pointerDownPos = { x: e.clientX, y: e.clientY };
    });

    dom.addEventListener('pointerup', function (e) {
      if (self._pointerDownPos) {
        var dist = Math.hypot(e.clientX - self._pointerDownPos.x, e.clientY - self._pointerDownPos.y);
        if (dist < 5) {
          self._checkClick();
        }
      }
    });

    window.addEventListener('resize', function () {
      self._onResize();
    });
  };

  /**
   * Xử lý click: click vào ô đỗ xe HOẶC click vào nút gọi thang máy 3D
   */
  Parking3DViewer.prototype._checkClick = function () {
    this.raycaster.setFromCamera(this.mouse, this.camera);

    // 1. Kiểm tra click vào nút 3D thang máy
    if (this.elevatorGroup) {
      var elevHits = this.raycaster.intersectObjects(this.elevatorGroup.children, true);
      for (var j = 0; j < elevHits.length; j++) {
        var a = elevHits[j].object.userData ? elevHits[j].object.userData.action : null;
        if (a === 'ELEV_UP') {
          var lotCfg = LOT_CONFIGS[this.options.lotId] || LOT_CONFIGS[1];
          var idx = lotCfg.floors.indexOf(this.options.floorId);
          if (idx > 0) this.setFloor(lotCfg.floors[idx - 1], 'UP');
          return;
        } else if (a === 'ELEV_DOWN') {
          var lotCfg2 = LOT_CONFIGS[this.options.lotId] || LOT_CONFIGS[1];
          var idx2 = lotCfg2.floors.indexOf(this.options.floorId);
          if (idx2 < lotCfg2.floors.length - 1) this.setFloor(lotCfg2.floors[idx2 + 1], 'DOWN');
          return;
        }
      }
    }

    // 2. Kiểm tra click vào ô đỗ xe
    if (this.hoveredSlot && this.options.onSlotClick) {
      this.options.onSlotClick(this.hoveredSlot.userData.slotData);
    }
  };

  Parking3DViewer.prototype._checkHover = function (clientX, clientY) {
    if (!this.slotsGroup) return;

    this.raycaster.setFromCamera(this.mouse, this.camera);
    var intersects = this.raycaster.intersectObjects(this.slotsGroup.children, true);

    var slotHit = null;
    for (var i = 0; i < intersects.length; i++) {
      var obj = intersects[i].object;
      while (obj && obj !== this.slotsGroup) {
        if (obj.userData && obj.userData.slotData) {
          slotHit = obj;
          break;
        }
        obj = obj.parent;
      }
      if (slotHit) break;
    }

    if (slotHit) {
      this.hoveredSlot = slotHit;
      var data = slotHit.userData.slotData;
      this._showTooltip(data, clientX, clientY);
      if (this.options.onSlotHover) this.options.onSlotHover(data);
    } else {
      this.hoveredSlot = null;
      this._hideTooltip();
    }
  };

  Parking3DViewer.prototype._showTooltip = function (slot, clientX, clientY) {
    if (!this.tooltip) return;

    var rect = this.container.getBoundingClientRect();
    var x = clientX - rect.left;
    var y = clientY - rect.top;

    var st = (slot.physicalStatus || 'EMPTY').toUpperCase();
    var stCfg = STATUS_COLORS[st] || STATUS_COLORS.EMPTY;

    this.tooltip.innerHTML = [
      '<div class="p3d-tooltip-head">',
      '  <span class="p3d-tooltip-code">' + slot.slotCode + '</span>',
      '  <span class="p3d-tooltip-badge badge-' + stCfg.code + '">' + stCfg.label + '</span>',
      '</div>',
      '<div class="p3d-tooltip-body">',
      '  <div class="p3d-tooltip-row"><span class="label">Loại ô:</span><span class="val">' + (slot.vehicleType === 'ev' ? '⚡ Ô tô điện (EV)' : '🚗 Ô tô 4-7 chỗ') + '</span></div>',
      slot.currentCarPlate ? '  <div class="p3d-tooltip-row"><span class="label">Biển số:</span><span class="val" style="color:#38bdf8;font-family:monospace">' + slot.currentCarPlate + '</span></div>' : '',
      slot.vehicleModel ? '  <div class="p3d-tooltip-row"><span class="label">Model:</span><span class="val">' + slot.vehicleModel + '</span></div>' : '',
      '</div>'
    ].join('');

    this.tooltip.style.left = x + 'px';
    this.tooltip.style.top = y + 'px';
    this.tooltip.classList.add('visible');
  };

  Parking3DViewer.prototype._hideTooltip = function () {
    if (this.tooltip) {
      this.tooltip.classList.remove('visible');
    }
  };

  Parking3DViewer.prototype._onResize = function () {
    if (!this.renderer || !this.camera || !this.canvasWrap) return;
    var width = this.canvasWrap.clientWidth;
    var height = this.canvasWrap.clientHeight;
    this.camera.aspect = width / height;
    this.camera.updateProjectionMatrix();
    this.renderer.setSize(width, height);
  };

  /**
   * Cập nhật vị trí gắn neo 3D của Widget thang máy trên màn hình
   */
  Parking3DViewer.prototype._updateElevatorWidgetPosition = function () {
    if (!this.elevWidget || !this.camera || !this.renderer) return;

    var p = this.elevatorWorldPos.clone();
    p.project(this.camera);

    // Nếu điểm thang máy nằm ngoài vùng nhìn (ở sau lưng camera)
    if (p.z > 1) {
      this.elevWidget.style.display = 'none';
      return;
    }

    var rect = this.canvasWrap.getBoundingClientRect();
    var x = ((p.x + 1) * rect.width) / 2;
    var y = ((-p.y + 1) * rect.height) / 2;

    this.elevWidget.style.display = 'flex';
    this.elevWidget.style.left = x + 'px';
    this.elevWidget.style.top = y + 'px';
  };

  /**
   * Loop Render
   */
  Parking3DViewer.prototype._animate = function () {
    var self = this;
    this.animId = requestAnimationFrame(function () { self._animate(); });

    var delta = this.clock ? this.clock.getElapsedTime() : 0;

    // Hiệu ứng nhấp nháy phát sáng cho ô RESERVED
    if (this.pulsingSlots.length > 0) {
      var pulse = 0.5 + 0.5 * Math.sin(delta * 4.5);
      for (var i = 0; i < this.pulsingSlots.length; i++) {
        var item = this.pulsingSlots[i];
        if (item.frame && item.frame.material) {
          item.frame.material.opacity = 0.3 + 0.7 * pulse;
        }
        if (item.pad && item.pad.material) {
          item.pad.material.opacity = 0.2 + 0.45 * pulse;
        }
      }
    }

    if (this.controls) {
      this.controls.update();
    }

    // Cập nhật tọa độ neo widget thang máy
    this._updateElevatorWidgetPosition();

    if (this.renderer && this.scene && this.camera) {
      this.renderer.render(this.scene, this.camera);
    }
  };

  Parking3DViewer.prototype.destroy = function () {
    if (this.animId) cancelAnimationFrame(this.animId);
    if (this.controls) this.controls.dispose();
    if (this.renderer) {
      this.renderer.dispose();
      if (this.renderer.domElement && this.renderer.domElement.parentNode) {
        this.renderer.domElement.parentNode.removeChild(this.renderer.domElement);
      }
    }
    this.container.innerHTML = '';
  };

  return Parking3DViewer;
});
