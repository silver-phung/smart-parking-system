/**
 * ==========================================================
 * Parking3DViewer Component (Three.js)
 * Sprint 3 - Days 6-9: 3D Visualization cho Hệ thống Smart Parking
 *
 * TÁI HIỆN CHÂN THỰC THEO MÔ HÌNH HẦM NGẦM SKETCHFAB (Underground Parking Lot):
 *   1. Tường bê tông công nghiệp phân ô (Concrete Panel Tiles) với số thứ tự
 *      ô đỗ dạng Stencil quân sự/kho bãi phong trần (Distressed Industrial Stencil Numbers: 1, 2, 3... 16)
 *      sơn trực tiếp lên tường sau lưng mỗi vị trí đỗ.
 *   2. Cột bê tông chịu lực vuông vức có đế giật cấp (Stepped Plinth Base) và phần thân
 *      dưới bọc vạch sơn sọc chéo Vàng - Đen cảnh báo an toàn 45° (Diagonal Hazard Warning Stripes).
 *   3. Kết cấu trần hầm thấp chịu lực với các dầm bê tông ngang (Transverse Beams),
 *      hệ thống đường ống thông gió/PCCC chạy dọc (Longitudinal Pipes), và dải đèn tuýp
 *      LED công nghiệp hầm ngầm (Fluorescent Light Fixtures) rọi vệt sáng chữ nhật xuống sàn.
 *   4. Mặt sàn bê tông nhựa Asphalt tối màu có vân hạt đá dăm (Granular Bitumen Texture),
 *      vạch tim đường đôi màu trắng nét liền (Double Solid White Centerlines) cùng mũi tên chỉ hướng,
 *      vạch phân cách ô đỗ kẻ đôi (Double Parallel Bay Dividers) và thanh chặn bánh xe cao su (Tire Curbs).
 *   5. Chế độ Camera góc nhìn thực tế người đi bộ / người lái xe trong hầm (Eye-Level Garage Perspective)
 *      kết hợp chuyển đổi mượt mà sang góc nhìn toàn cảnh (Overview 3D) và mặt bằng (Top-down 2D).
 *      Tự động ẩn trần khi ở trên cao để dễ dàng quản lý theo dõi slot đỗ.
 *   6. 3 BÃI MẪU:
 *       • Bãi 1: 2 Tầng (Hầm B1, Hầm B2) - Tổng slot theo DB (64 slots).
 *       • Bãi 2: 3 Tầng (Hầm B1, Hầm B2, Hầm B3) - Số slot random (82 slots).
 *       • Bãi 3: 4 Tầng (Hầm B1, Hầm B2, Hầm B3, Hầm B4) - Số slot random (112 slots).
 *   7. CỤM THANG MÁY KÍNH & CẦU THANG BỘ THOÁT HIỂM:
 *       • Bảng nút bấm 3D Lên/Xuống tương tác ngay tại cửa thang.
 *       • Widget nổi gắn neo 3D (Floating Elevator Call Widget) chuyển tầng mượt mà.
 *       • Hoạt cảnh camera di chuyển theo phương đứng mô phỏng thang máy.
 *   8. 4 TRẠNG THÁI VẬT LÝ + ĐẶC BIỆT:
 *       🟢 Trống (Available)
 *       🔴 Có xe (Occupied) -> Mesh ô tô 3D chân thực, đèn xe, biển số
 *       🟡 Bảo trì (Maintenance) -> Cọc tiêu phản quang 3D
 *       ⚪ Tạm khóa (Unavailable) -> Gạch chéo cảnh báo
 *       ⏳ Chờ vào (Reserved) -> Khung viền nhấp nháy phát sáng & Beacon 3D
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
      name: 'Bãi 1: Hầm ngầm FPT Q9 (Mô hình Chuẩn)',
      desc: 'Mô hình 2 tầng hầm chuẩn DB (B1: 32 ô, B2: 32 ô = 64 ô)',
      floors: ['B1', 'B2'],
      floorLabels: { B1: 'Hầm B1', B2: 'Hầm B2' }
    },
    2: {
      id: 2,
      name: 'Bãi 2: Vincom Landmark 81 Underground',
      desc: 'Mô hình 3 tầng hầm (B1, B2, B3) số slot ngẫu nhiên (82 ô)',
      floors: ['B1', 'B2', 'B3'],
      floorLabels: { B1: 'Hầm B1', B2: 'Hầm B2', B3: 'Hầm B3' }
    },
    3: {
      id: 3,
      name: 'Bãi 3: Aeon Mall Mega Parking Hub',
      desc: 'Mô hình 4 tầng ngầm (B1, B2, B3, B4) số slot ngẫu nhiên (112 ô)',
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
    this.currentViewMode = 'FIRST_PERSON'; // 'FIRST_PERSON' | 'DRIVEWAY' | 'OVERVIEW' | 'TOP_DOWN'
    this.isTransitioning = false;

    // Vị trí cụm thang máy / cầu thang trong không gian 3D
    this.elevatorWorldPos = (typeof THREE !== 'undefined') ? new THREE.Vector3(28, 2.5, 0) : { x: 28, y: 2.5, z: 0 };

    // Tọa độ các góc nhìn Camera
    this.camViews = {
      FIRST_PERSON: { cam: { x: -8, y: 1.75, z: 1.4 }, target: { x: 14, y: 1.55, z: -0.6 } },
      DRIVEWAY:     { cam: { x: -25, y: 1.7, z: 0 }, target: { x: 8, y: 1.5, z: 0 } },
      OVERVIEW:     { cam: { x: 0, y: 32, z: 36 }, target: { x: 0, y: 0, z: 0 } },
      TOP_DOWN:     { cam: { x: 0, y: 46, z: 0.1 }, target: { x: 0, y: 0, z: 0 } }
    };

    this.defaultCamPos = this.camViews.FIRST_PERSON.cam;
    this.defaultTarget = this.camViews.FIRST_PERSON.target;

    this.clock = null;
    this.animId = null;
    this.raycaster = null;
    this.mouse = null;
    this.hoveredSlot = null;
    this.structureGroup = null;
    this.ceilingGroup = null;
    this.slotsGroup = null;
    this.elevatorGroup = null;

    this._ensureDependencies(function () {
      this._buildUI();
      this._initThree();
      this.loadSlots(this.options.floorId);
    }.bind(this));
  }

  Parking3DViewer.prototype._ensureDependencies = function (callback) {
    if (typeof THREE !== 'undefined' && typeof THREE.OrbitControls !== 'undefined') {
      callback();
      return;
    }
    var checkInterval = setInterval(function () {
      if (typeof THREE !== 'undefined' && typeof THREE.OrbitControls !== 'undefined') {
        clearInterval(checkInterval);
        callback();
      }
    }, 50);
    setTimeout(function () {
      clearInterval(checkInterval);
      if (typeof THREE === 'undefined') {
        console.error('[Parking3DViewer] Three.js not loaded after 5s.');
      }
    }, 5000);
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
    this.loaderEl.innerHTML = '<div class="p3d-spinner"></div><span>Đang dựng không gian hầm đỗ xe 3D chuẩn Sketchfab...</span>';
    this.container.appendChild(this.loaderEl);

    // Top HUD
    if (this.options.showStats) {
      this.hudTop = document.createElement('div');
      this.hudTop.className = 'p3d-hud-top';
      this.container.appendChild(this.hudTop);
      this._renderTopHUD();
    }

    // Bottom HUD: Bộ chọn góc nhìn Camera & Gợi ý phím
    if (this.options.showControls) {
      this.hudBottom = document.createElement('div');
      this.hudBottom.className = 'p3d-hud-bottom';
      this.hudBottom.innerHTML = [
        '<div class="p3d-btn-group">',
        '  <button type="button" class="p3d-btn p3d-view-btn active" id="p3dViewFPBtn" title="Góc nhìn người đi bộ trong hầm ngầm (giống Sketchfab)">',
        '    👁️ Hầm ngầm',
        '  </button>',
        '  <button type="button" class="p3d-btn p3d-view-btn" id="p3dViewDriveBtn" title="Góc nhìn người lái xe dọc lối đi trung tâm">',
        '    🚗 Lối chạy xe',
        '  </button>',
        '  <button type="button" class="p3d-btn p3d-view-btn" id="p3dViewIsoBtn" title="Góc nhìn toàn cảnh 3D trên cao">',
        '    🏢 Toàn cảnh 3D',
        '  </button>',
        '  <button type="button" class="p3d-btn p3d-view-btn" id="p3dViewTopBtn" title="Góc nhìn mặt bằng thẳng đứng 2D">',
        '    🔝 Mặt bằng 2D',
        '  </button>',
        '</div>',
        '<div class="p3d-hints">',
        '  <span>🛗 Bấm ▲ ▼ tại thang máy để đổi tầng</span>',
        '  <span>🖱️ Chuột trái: Xoay</span>',
        '  <span>🔍 Cuộn: Zoom</span>',
        '  <span>👆 Phải: Dời</span>',
        '</div>'
      ].join('');
      this.container.appendChild(this.hudBottom);

      var viewBtns = this.hudBottom.querySelectorAll('.p3d-view-btn');
      function setActiveBtn(btn) {
        viewBtns.forEach(function (b) { b.classList.remove('active'); });
        if (btn) btn.classList.add('active');
      }

      var btnFP = this.hudBottom.querySelector('#p3dViewFPBtn');
      if (btnFP) {
        btnFP.addEventListener('click', function () {
          setActiveBtn(btnFP);
          self.setCameraView('FIRST_PERSON');
        });
      }

      var btnDrive = this.hudBottom.querySelector('#p3dViewDriveBtn');
      if (btnDrive) {
        btnDrive.addEventListener('click', function () {
          setActiveBtn(btnDrive);
          self.setCameraView('DRIVEWAY');
        });
      }

      var btnIso = this.hudBottom.querySelector('#p3dViewIsoBtn');
      if (btnIso) {
        btnIso.addEventListener('click', function () {
          setActiveBtn(btnIso);
          self.setCameraView('OVERVIEW');
        });
      }

      var btnTop = this.hudBottom.querySelector('#p3dViewTopBtn');
      if (btnTop) {
        btnTop.addEventListener('click', function () {
          setActiveBtn(btnTop);
          self.setCameraView('TOP_DOWN');
        });
      }
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

    var lotSel = this.hudTop.querySelector('#p3dLotSelect');
    lotSel.addEventListener('change', function (e) {
      self.setLot(parseInt(e.target.value, 10));
    });

    var floorSel = this.hudTop.querySelector('#p3dFloorSelect');
    floorSel.addEventListener('change', function (e) {
      self.setFloor(e.target.value);
    });

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
   * Khởi tạo Three.js Scene, Camera và Renderer
   */
  Parking3DViewer.prototype._initThree = function () {
    var width = this.canvasWrap.clientWidth || 800;
    var height = this.canvasWrap.clientHeight || 520;

    this.clock = new THREE.Clock();

    // Scene & Fog ngầm
    this.scene = new THREE.Scene();
    this.scene.background = new THREE.Color(0x0e1117);
    this.scene.fog = new THREE.FogExp2(0x0e1117, 0.007);

    // Camera: FOV 50 tạo cảm giác chiều sâu kiến trúc hầm chuẩn xác
    this.camera = new THREE.PerspectiveCamera(50, width / height, 0.1, 1000);
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
    this.controls.maxPolarAngle = Math.PI / 2.02;
    this.controls.minDistance = 1.5;
    this.controls.maxDistance = 120;
    this.controls.target.set(this.defaultTarget.x, this.defaultTarget.y, this.defaultTarget.z);

    this._setupLighting();
    this._buildUndergroundArchitecture();

    this.raycaster = new THREE.Raycaster();
    this.mouse = new THREE.Vector2();

    this._bindEvents();
    this._animate();
  };

  /**
   * Ánh sáng không gian hầm ngầm:
   *   - Ambient tối trầm công nghiệp
   *   - HemiLight dịu nhẹ phản chiếu sàn
   *   - Dàn đèn huỳnh quang công nghiệp rọi sáng cục bộ tạo vệt sáng trên sàn bê tông
   */
  Parking3DViewer.prototype._setupLighting = function () {
    var ambientLight = new THREE.AmbientLight(0xffffff, 0.38);
    this.scene.add(ambientLight);

    var hemiLight = new THREE.HemisphereLight(0x94a3b8, 0x111317, 0.45);
    hemiLight.position.set(0, 30, 0);
    this.scene.add(hemiLight);

    // Đèn định hướng nhẹ tạo bóng đổ mềm
    var dirLight = new THREE.DirectionalLight(0xf8fafc, 0.55);
    dirLight.position.set(15, 25, 10);
    dirLight.castShadow = true;
    dirLight.shadow.mapSize.width = 1024;
    dirLight.shadow.mapSize.height = 1024;
    dirLight.shadow.camera.near = 5;
    dirLight.shadow.camera.far = 80;
    var d = 36;
    dirLight.shadow.camera.left = -d;
    dirLight.shadow.camera.right = d;
    dirLight.shadow.camera.top = d;
    dirLight.shadow.camera.bottom = -d;
    dirLight.shadow.bias = -0.0005;
    this.scene.add(dirLight);
  };

  /**
   * TẠO PROCEDURAL TEXTURE: Bê tông tấm công nghiệp (Concrete Panels)
   */
  Parking3DViewer.prototype._createConcreteTexture = function (w, h, isWall) {
    var canvas = document.createElement('canvas');
    canvas.width = w || 512;
    canvas.height = h || 512;
    var ctx = canvas.getContext('2d');

    // Nền bê tông sẫm màu
    ctx.fillStyle = '#2d3036';
    ctx.fillRect(0, 0, canvas.width, canvas.height);

    // Nhiễu hạt bê tông (Perlin/Random noise flecks)
    var imgData = ctx.getImageData(0, 0, canvas.width, canvas.height);
    var data = imgData.data;
    for (var i = 0; i < data.length; i += 4) {
      var n = (Math.random() - 0.5) * 32;
      data[i] = Math.min(255, Math.max(0, data[i] + n));
      data[i + 1] = Math.min(255, Math.max(0, data[i + 1] + n));
      data[i + 2] = Math.min(255, Math.max(0, data[i + 2] + n));
    }
    ctx.putImageData(imgData, 0, 0);

    // Vẽ mạch ghép tấm bê tông đúc sẵn (Precast Concrete Slab Seams)
    if (isWall) {
      ctx.strokeStyle = 'rgba(18, 20, 24, 0.9)';
      ctx.lineWidth = 3;
      var panelW = canvas.width / 4;
      var panelH = canvas.height / 3;

      for (var x = panelW; x < canvas.width; x += panelW) {
        ctx.beginPath();
        ctx.moveTo(x, 0);
        ctx.lineTo(x, canvas.height);
        ctx.stroke();

        ctx.strokeStyle = 'rgba(90, 98, 110, 0.35)';
        ctx.lineWidth = 1;
        ctx.beginPath();
        ctx.moveTo(x + 2, 0);
        ctx.lineTo(x + 2, canvas.height);
        ctx.stroke();
        ctx.strokeStyle = 'rgba(18, 20, 24, 0.9)';
        ctx.lineWidth = 3;
      }

      for (var y = panelH; y < canvas.height; y += panelH) {
        ctx.beginPath();
        ctx.moveTo(0, y);
        ctx.lineTo(canvas.width, y);
        ctx.stroke();

        ctx.strokeStyle = 'rgba(90, 98, 110, 0.35)';
        ctx.lineWidth = 1;
        ctx.beginPath();
        ctx.moveTo(0, y + 2);
        ctx.lineTo(canvas.width, y + 2);
        ctx.stroke();
        ctx.strokeStyle = 'rgba(18, 20, 24, 0.9)';
        ctx.lineWidth = 3;
      }

      // Lỗ neo cốp pha (Form-tie holes)
      ctx.fillStyle = 'rgba(14, 16, 19, 0.75)';
      for (var px = 0; px <= canvas.width; px += panelW) {
        for (var py = 0; py <= canvas.height; py += panelH) {
          ctx.beginPath();
          ctx.arc(px + 12, py + 12, 3.5, 0, Math.PI * 2);
          ctx.fill();
          ctx.beginPath();
          ctx.arc(px + panelW - 12, py + 12, 3.5, 0, Math.PI * 2);
          ctx.fill();
        }
      }
    }

    var tex = new THREE.CanvasTexture(canvas);
    tex.wrapS = THREE.RepeatWrapping;
    tex.wrapT = THREE.RepeatWrapping;
    return tex;
  };

  /**
   * TẠO PROCEDURAL TEXTURE: Vạch sọc chéo Vàng - Đen cảnh báo an toàn 45°
   */
  Parking3DViewer.prototype._createHazardTexture = function () {
    var canvas = document.createElement('canvas');
    canvas.width = 512;
    canvas.height = 512;
    var ctx = canvas.getContext('2d');

    // Nền đen sẫm
    ctx.fillStyle = '#141517';
    ctx.fillRect(0, 0, 512, 512);

    // Dải sọc vàng an toàn 45 độ
    ctx.fillStyle = '#eab308';
    var stripeW = 46;
    for (var x = -512; x < 1024; x += stripeW * 2) {
      ctx.beginPath();
      ctx.moveTo(x, 0);
      ctx.lineTo(x + stripeW, 0);
      ctx.lineTo(x + stripeW + 512, 512);
      ctx.lineTo(x + 512, 512);
      ctx.closePath();
      ctx.fill();
    }

    // Nhiễu bụi mờ
    var imgData = ctx.getImageData(0, 0, 512, 512);
    var data = imgData.data;
    for (var i = 0; i < data.length; i += 4) {
      var n = (Math.random() - 0.5) * 20;
      data[i] = Math.min(255, Math.max(0, data[i] + n));
      data[i + 1] = Math.min(255, Math.max(0, data[i + 1] + n));
      data[i + 2] = Math.min(255, Math.max(0, data[i + 2] + n));
    }
    ctx.putImageData(imgData, 0, 0);

    var tex = new THREE.CanvasTexture(canvas);
    tex.wrapS = THREE.RepeatWrapping;
    tex.wrapT = THREE.RepeatWrapping;
    tex.repeat.set(2, 1);
    return tex;
  };

  /**
   * TẠO PROCEDURAL TEXTURE: Mặt sàn bê tông nhựa Asphalt sẫm màu
   */
  Parking3DViewer.prototype._createAsphaltTexture = function () {
    var canvas = document.createElement('canvas');
    canvas.width = 1024;
    canvas.height = 1024;
    var ctx = canvas.getContext('2d');

    // Nền nhựa đường xám đen
    ctx.fillStyle = '#1f2227';
    ctx.fillRect(0, 0, 1024, 1024);

    // Hạt đá dăm dăm li ti
    var imgData = ctx.getImageData(0, 0, 1024, 1024);
    var data = imgData.data;
    for (var i = 0; i < data.length; i += 4) {
      var r = Math.random();
      var delta = 0;
      if (r < 0.12) {
        delta = 26 + Math.random() * 32; // Hạt đá dăm sáng màu
      } else if (r < 0.32) {
        delta = -18 - Math.random() * 22; // Vết nhựa bitum sẫm
      } else {
        delta = (Math.random() - 0.5) * 16;
      }
      data[i] = Math.min(255, Math.max(0, data[i] + delta));
      data[i + 1] = Math.min(255, Math.max(0, data[i + 1] + delta));
      data[i + 2] = Math.min(255, Math.max(0, data[i + 2] + delta * 1.05));
    }
    ctx.putImageData(imgData, 0, 0);

    var tex = new THREE.CanvasTexture(canvas);
    tex.wrapS = THREE.RepeatWrapping;
    tex.wrapT = THREE.RepeatWrapping;
    tex.repeat.set(16, 6);
    return tex;
  };

  /**
   * TẠO MESH SỐ THỨ TỰ SƠN TƯỜNG DẠNG STENCIL PHONG TRẦN (Distressed Wall Stencil Number):
   * Giống hệt các số 1, 2, 3... 15 in trên tường trong ảnh mẫu Sketchfab!
   */
  Parking3DViewer.prototype._createWallStencilMesh = function (numberStr, x, y, z, rotY) {
    var canvas = document.createElement('canvas');
    canvas.width = 512;
    canvas.height = 384;
    var ctx = canvas.getContext('2d');

    ctx.clearRect(0, 0, 512, 384);

    // Font Stencil quân sự/công nghiệp to bản
    ctx.font = '900 240px "Impact", "Arial Black", sans-serif';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillStyle = '#f1f5f9';
    ctx.fillText(numberStr, 256, 192);

    // Cắt rãnh Stencil (Stencil Bridge Slits)
    ctx.globalCompositeOperation = 'destination-out';
    ctx.fillStyle = '#000000';
    ctx.fillRect(246, 60, 16, 260); // Rãnh dọc giữa
    if (numberStr.length > 1) {
      ctx.fillRect(155, 80, 14, 220);
      ctx.fillRect(345, 80, 14, 220);
    }

    // Hiệu ứng bong tróc / phong trần (Grunge distress scratches)
    for (var k = 0; k < 360; k++) {
      var sx = Math.random() * 512;
      var sy = Math.random() * 384;
      var sr = 0.8 + Math.random() * 3.2;
      ctx.beginPath();
      ctx.arc(sx, sy, sr, 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.globalCompositeOperation = 'source-over';

    var texture = new THREE.CanvasTexture(canvas);
    var mat = new THREE.MeshBasicMaterial({
      map: texture,
      transparent: true,
      opacity: 0.9,
      depthWrite: false,
      side: THREE.DoubleSide
    });

    var mesh = new THREE.Mesh(new THREE.PlaneGeometry(2.1, 1.55), mat);
    mesh.position.set(x, y, z);
    mesh.rotation.y = rotY || 0;
    return mesh;
  };

  /**
   * DỰNG KIẾN TRÚC HẦM XE SKETCHFAB NGUYÊN BẢN:
   *   - Mặt sàn Asphalt xám sẫm
   *   - Tường bê tông tấm modular
   *   - Số thứ tự Stencil in trực tiếp trên tường sau lưng mỗi ô (1, 2, 3... 16)
   *   - Cột vuông có đế giật cấp và dải sơn sọc chéo Vàng - Đen 45°
   *   - Vạch kẻ tim đường đôi nét liền & Mũi tên chỉ hướng
   *   - Dầm trần bê tông, ống thông gió HVAC, ống PCCC
   *   - Đèn tuýp LED chiếu sáng rọi vệt sáng chữ nhật
   *   - Cụm thang máy & cầu thang bộ thoát hiểm
   */
  Parking3DViewer.prototype._buildUndergroundArchitecture = function () {
    this.structureGroup = new THREE.Group();

    var floorW = 68;
    var floorL = 24;
    var ceilingH = 4.8;

    // 1. Mặt sàn bê tông nhựa Asphalt
    var asphaltTex = this._createAsphaltTexture();
    var floorGeo = new THREE.BoxGeometry(floorW, 0.6, floorL);
    var floorMat = new THREE.MeshStandardMaterial({
      map: asphaltTex,
      roughness: 0.92,
      metalness: 0.08
    });
    var floorMesh = new THREE.Mesh(floorGeo, floorMat);
    floorMesh.position.y = -0.3;
    floorMesh.receiveShadow = true;
    this.structureGroup.add(floorMesh);

    // 2. Tường bê tông 4 phía (Perimeter Concrete Walls)
    var wallTex = this._createConcreteTexture(512, 512, true);
    var wallMat = new THREE.MeshStandardMaterial({
      map: wallTex,
      roughness: 0.88,
      metalness: 0.12
    });

    // Tường Bắc (Z = -floorL/2 = -12)
    var wallNorth = new THREE.Mesh(new THREE.BoxGeometry(floorW, ceilingH, 0.8), wallMat);
    wallNorth.position.set(0, ceilingH / 2, -floorL / 2);
    wallNorth.receiveShadow = true;
    this.structureGroup.add(wallNorth);

    // Tường Nam (Z = floorL/2 = +12)
    var wallSouth = new THREE.Mesh(new THREE.BoxGeometry(floorW, ceilingH, 0.8), wallMat);
    wallSouth.position.set(0, ceilingH / 2, floorL / 2);
    wallSouth.receiveShadow = true;
    this.structureGroup.add(wallSouth);

    // Tường Tây (X = -floorW/2 = -34)
    var wallWest = new THREE.Mesh(new THREE.BoxGeometry(0.8, ceilingH, floorL), wallMat);
    wallWest.position.set(-floorW / 2, ceilingH / 2, 0);
    this.structureGroup.add(wallWest);

    // Tường Đông (X = floorW/2 = +34)
    var wallEast = new THREE.Mesh(new THREE.BoxGeometry(0.8, ceilingH, floorL), wallMat);
    wallEast.position.set(floorW / 2, ceilingH / 2, 0);
    this.structureGroup.add(wallEast);

    // 3. SỐ THỨ TỰ STENCIL IN TRÊN TƯỜNG (Giống ảnh 1, 2, 3: các số 1..16 trên tường Bắc và 17..32 trên tường Nam)
    var spacing = 3.2;
    var startX = -((16 - 1) * spacing) / 2.0;

    // Tường Bắc (Dãy ô A: số 1 đến 16)
    for (var n = 1; n <= 16; n++) {
      var slotX = startX + (n - 1) * spacing;
      var stencilNorth = this._createWallStencilMesh(n.toString(), slotX, 1.95, -floorL / 2 + 0.42, 0);
      this.structureGroup.add(stencilNorth);
    }

    // Tường Nam (Dãy ô B: số 17 đến 32)
    for (var m = 1; m <= 16; m++) {
      var slotX_s = startX + (m - 1) * spacing;
      var stencilSouth = this._createWallStencilMesh((16 + m).toString(), slotX_s, 1.95, floorL / 2 - 0.42, Math.PI);
      this.structureGroup.add(stencilSouth);
    }

    // 4. LỐI XE CHẠY TRUNG TÂM (Driveway Lane) & VẠCH KẺ ĐÔI TIM ĐƯỜNG (Ảnh 2)
    var laneMarkMat = new THREE.MeshBasicMaterial({ color: 0xffffff });
    var laneLen = 56;

    // Vạch tim đường ĐÔI màu trắng nét liền chạy dọc hầm (Double solid white centerline)
    var lineCenter1 = new THREE.Mesh(new THREE.PlaneGeometry(laneLen, 0.12), laneMarkMat);
    lineCenter1.rotation.x = -Math.PI / 2;
    lineCenter1.position.set(-2, 0.015, -0.09);
    this.structureGroup.add(lineCenter1);

    var lineCenter2 = new THREE.Mesh(new THREE.PlaneGeometry(laneLen, 0.12), laneMarkMat);
    lineCenter2.rotation.x = -Math.PI / 2;
    lineCenter2.position.set(-2, 0.015, 0.09);
    this.structureGroup.add(lineCenter2);

    // Vạch biên giới hạn mép làn chạy (Driveway aisle boundary lines)
    var lineEdgeN = new THREE.Mesh(new THREE.PlaneGeometry(laneLen, 0.12), laneMarkMat);
    lineEdgeN.rotation.x = -Math.PI / 2;
    lineEdgeN.position.set(-2, 0.015, -3.8);
    this.structureGroup.add(lineEdgeN);

    var lineEdgeS = new THREE.Mesh(new THREE.PlaneGeometry(laneLen, 0.12), laneMarkMat);
    lineEdgeS.rotation.x = -Math.PI / 2;
    lineEdgeS.position.set(-2, 0.015, 3.8);
    this.structureGroup.add(lineEdgeS);

    // Mũi tên dẫn hướng sơn trên làn chạy (Directional forward arrows)
    this._addDirectionalArrow(-16, -1.8, false);
    this._addDirectionalArrow(4, -1.8, false);
    this._addDirectionalArrow(-6, 1.8, true);
    this._addDirectionalArrow(14, 1.8, true);

    // Gờ giảm tốc màu vàng đen (Speed bumps)
    this._addSpeedBump(-22, 0);
    this._addSpeedBump(18, 0);

    // 5. CỘT BÊ TÔNG CHỊU LỰC VUÔNG VỨC & VẠCH SỌC CHÉO VÀNG - ĐEN 45° (Ảnh 1, 2, 3)
    var hazardTex = this._createHazardTexture();
    var hazardMat = new THREE.MeshStandardMaterial({
      map: hazardTex,
      roughness: 0.6,
      metalness: 0.2
    });

    var pillarConcreteMat = new THREE.MeshStandardMaterial({
      map: wallTex,
      roughness: 0.85
    });

    // Các vị trí đặt cột ngăn giữa các cụm 3 ô đỗ
    var colXList = [-22.4, -12.8, -3.2, 6.4, 16.0, 25.6];

    for (var ci = 0; ci < colXList.length; ci++) {
      var cx = colXList[ci];

      // Hàng cột phía Bắc (Z = -3.8) và hàng cột phía Nam (Z = +3.8)
      var zPositions = [-3.8, 3.8];
      for (var zi = 0; zi < zPositions.length; zi++) {
        var cz = zPositions[zi];
        var pillarGroup = new THREE.Group();
        pillarGroup.position.set(cx, 0, cz);

        // a) Đế bê tông giật cấp (Stepped base plinth)
        var plinthGeo = new THREE.BoxGeometry(1.5, 0.22, 1.5);
        var plinth = new THREE.Mesh(plinthGeo, pillarConcreteMat);
        plinth.position.y = 0.11;
        pillarGroup.add(plinth);

        // b) Thân cột dưới phủ vạch sơn sọc chéo Vàng - Đen (cao 1.25m)
        var hazardGeo = new THREE.BoxGeometry(1.24, 1.25, 1.24);
        var hazardMesh = new THREE.Mesh(hazardGeo, hazardMat);
        hazardMesh.position.y = 0.22 + 1.25 / 2;
        hazardMesh.castShadow = true;
        hazardMesh.receiveShadow = true;
        pillarGroup.add(hazardMesh);

        // c) Thân cột trên bằng bê tông thô (tới dầm trần)
        var upperH = ceilingH - (0.22 + 1.25);
        var upperGeo = new THREE.BoxGeometry(1.18, upperH, 1.18);
        var upperMesh = new THREE.Mesh(upperGeo, pillarConcreteMat);
        upperMesh.position.y = (0.22 + 1.25) + upperH / 2;
        upperMesh.castShadow = true;
        upperMesh.receiveShadow = true;
        pillarGroup.add(upperMesh);

        this.structureGroup.add(pillarGroup);
      }
    }

    // 6. HỆ THỐNG KỸ THUẬT TRẦN HẦM (Ceiling Beams, HVAC Ducts, Pipes & LED Fixtures)
    this._buildCeilingInfrastructure(floorW, floorL, ceilingH, colXList, wallTex);

    // 7. KHỐI CẦU THANG BỘ & BUỒNG THANG MÁY (Elevator & Staircase Core)
    this._buildElevatorAndStaircase(28, 0, ceilingH);

    // 8. Biển báo hầm ngầm treo trần
    this._addCeilingSign(-12, ceilingH - 0.75, 0, '⚠️ CHIỀU CAO TỐI ĐA 2.2M · TỐC ĐỘ 5 KM/H', '#facc15');
    this._addCeilingSign(8, ceilingH - 0.75, 0, '🟢 LỐI RA CỔNG / EXIT →', '#34d399');

    this.scene.add(this.structureGroup);
  };

  /**
   * Dựng hệ thống kỹ thuật trần hầm xe ngầm:
   *   - Dầm bê tông ngang chịu lực (Transverse Beams) tại mỗi vị trí cột
   *   - Trần bê tông tấm đậy nóc
   *   - Ống thông gió HVAC và ống PCCC đôi chạy dọc (Longitudinal Pipes)
   *   - Đèn tuýp LED công nghiệp chiếu sáng rọi vệt sáng xuống sàn
   */
  Parking3DViewer.prototype._buildCeilingInfrastructure = function (width, length, height, colXList, wallTex) {
    this.ceilingGroup = new THREE.Group();

    var beamMat = new THREE.MeshStandardMaterial({
      map: wallTex,
      roughness: 0.9
    });
    var ductMat = new THREE.MeshStandardMaterial({ color: 0x94a3b8, metalness: 0.75, roughness: 0.35 });
    var firePipeMat = new THREE.MeshStandardMaterial({ color: 0xdc2626, metalness: 0.6, roughness: 0.4 });
    var ledBoxMat = new THREE.MeshStandardMaterial({ color: 0x1e293b, metalness: 0.8 });
    var ledGlowMat = new THREE.MeshBasicMaterial({ color: 0xffffff });

    // 1. Tấm trần bê tông che nóc (Ceiling Slab)
    var ceilingTex = this._createConcreteTexture(512, 512, true);
    var ceilingGeo = new THREE.PlaneGeometry(width, length);
    var ceilingMat = new THREE.MeshStandardMaterial({
      map: ceilingTex,
      roughness: 0.9,
      side: THREE.DoubleSide
    });
    var ceilingMesh = new THREE.Mesh(ceilingGeo, ceilingMat);
    ceilingMesh.rotation.x = Math.PI / 2;
    ceilingMesh.position.y = height;
    this.ceilingGroup.add(ceilingMesh);

    // 2. Dầm bê tông ngang chịu lực bắc ngang qua trần hầm tại mỗi vị trí cột
    for (var bi = 0; bi < colXList.length; bi++) {
      var bx = colXList[bi];
      var beam = new THREE.Mesh(new THREE.BoxGeometry(1.15, 0.65, length), beamMat);
      beam.position.set(bx, height - 0.325, 0);
      beam.castShadow = true;
      this.ceilingGroup.add(beam);
    }

    // 3. Đường ống thông gió HVAC chạy dọc trần
    var hvacDuct1 = new THREE.Mesh(new THREE.BoxGeometry(width - 6, 0.5, 1.0), ductMat);
    hvacDuct1.position.set(0, height - 0.7, -3.8);
    this.ceilingGroup.add(hvacDuct1);

    var hvacDuct2 = new THREE.Mesh(new THREE.BoxGeometry(width - 6, 0.5, 1.0), ductMat);
    hvacDuct2.position.set(0, height - 0.7, 3.8);
    this.ceilingGroup.add(hvacDuct2);

    // 4. Đường ống đôi PCCC màu đỏ rực chạy dọc hầm (Ảnh 2)
    var pipeGeo = new THREE.CylinderGeometry(0.09, 0.09, width - 6, 12);

    var pipe1 = new THREE.Mesh(pipeGeo, firePipeMat);
    pipe1.rotation.z = Math.PI / 2;
    pipe1.position.set(0, height - 1.05, -3.4);
    this.ceilingGroup.add(pipe1);

    var pipe2 = new THREE.Mesh(pipeGeo, firePipeMat);
    pipe2.rotation.z = Math.PI / 2;
    pipe2.position.set(0, height - 1.05, 3.4);
    this.ceilingGroup.add(pipe2);

    // 5. Máng đèn tuýp LED trần công nghiệp (Fluorescent Fixtures) rọi sáng mặt sàn
    var ledPositions = [
      { x: -20, z: 0 },
      { x: -10, z: 0 },
      { x: 0,   z: 0 },
      { x: 10,  z: 0 },
      { x: 20,  z: 0 },
      { x: -16, z: -7.8 },
      { x: 0,   z: -7.8 },
      { x: 16,  z: -7.8 },
      { x: -16, z: 7.8 },
      { x: 0,   z: 7.8 },
      { x: 16,  z: 7.8 }
    ];

    for (var li = 0; li < ledPositions.length; li++) {
      var lp = ledPositions[li];
      var fixtureGroup = new THREE.Group();
      fixtureGroup.position.set(lp.x, height - 0.35, lp.z);

      // Máng kim loại sẫm màu
      var casing = new THREE.Mesh(new THREE.BoxGeometry(3.4, 0.14, 0.28), ledBoxMat);
      fixtureGroup.add(casing);

      // Mặt đèn tuýp phát sáng trắng (Glowing Emissive Diffuser)
      var tube = new THREE.Mesh(new THREE.BoxGeometry(3.3, 0.04, 0.22), ledGlowMat);
      tube.position.y = -0.08;
      fixtureGroup.add(tube);

      // Điểm sáng rọi xuống tạo vệt sáng trên sàn bê tông
      var spotLight = new THREE.PointLight(0xf1f5f9, 0.65, 14, 1.8);
      spotLight.position.y = -0.6;
      fixtureGroup.add(spotLight);

      this.ceilingGroup.add(fixtureGroup);
    }

    this.structureGroup.add(this.ceilingGroup);
  };

  /**
   * DỰNG CỤM CẦU THANG BỘ & BUỒNG THANG MÁY 3D:
   *   - Buồng thang máy kính & khung kim loại
   *   - Bảng LED hiển thị tầng trên cửa thang
   *   - Bậc thang bê tông & tay vịn inox
   *   - Nút gọi tầng 3D (▲ Lên, ▼ Xuống)
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

    // Buồng thang máy kính
    var shaft = new THREE.Mesh(new THREE.BoxGeometry(4.2, height, 4.2), glassMat);
    shaft.position.y = height / 2;
    this.elevatorGroup.add(shaft);

    // Khung viền thép góc
    var frameMat = new THREE.MeshStandardMaterial({ color: 0x0f172a, metalness: 0.9 });
    for (var cx = -2.1; cx <= 2.1; cx += 4.2) {
      for (var cz = -2.1; cz <= 2.1; cz += 4.2) {
        var post = new THREE.Mesh(new THREE.BoxGeometry(0.25, height, 0.25), frameMat);
        post.position.set(cx, height / 2, cz);
        this.elevatorGroup.add(post);
      }
    }

    // Cabin thang máy bên trong
    var cabin = new THREE.Mesh(new THREE.BoxGeometry(3.6, 2.5, 3.6), metalMat);
    cabin.position.set(0, 1.35, 0);
    this.elevatorGroup.add(cabin);

    // Cửa trượt thang máy inox
    var doorL = new THREE.Mesh(new THREE.BoxGeometry(0.08, 2.3, 1.1), doorMat);
    doorL.position.set(-2.12, 1.25, -0.6);
    var doorR = new THREE.Mesh(new THREE.BoxGeometry(0.08, 2.3, 1.1), doorMat);
    doorR.position.set(-2.12, 1.25, 0.6);
    this.elevatorGroup.add(doorL);
    this.elevatorGroup.add(doorR);

    // Màn hình LED điện tử hiển thị tầng
    this.elevScreenSprite = this._addTextSprite('🛗 [ ' + this.options.floorId + ' ] ▲', -2.15, 2.7, 0, '#38bdf8', 2.0);
    this.elevScreenSprite.rotation.y = -Math.PI / 2;
    this.elevatorGroup.add(this.elevScreenSprite);

    // Khối cầu thang bộ
    var stairGroup = new THREE.Group();
    stairGroup.position.set(0, 0, -4.8);

    var stairWall = new THREE.Mesh(new THREE.BoxGeometry(4.2, height, 3.8), new THREE.MeshStandardMaterial({ color: 0x334155, roughness: 0.9 }));
    stairWall.position.y = height / 2;
    stairGroup.add(stairWall);

    var stepMat = new THREE.MeshStandardMaterial({ color: 0x475569 });
    for (var s = 0; s < 8; s++) {
      var step = new THREE.Mesh(new THREE.BoxGeometry(1.6, 0.2, 0.4), stepMat);
      step.position.set(-0.6, s * 0.22 + 0.11, -1.2 + s * 0.35);
      stairGroup.add(step);
    }

    var rail = new THREE.Mesh(new THREE.CylinderGeometry(0.04, 0.04, 3.2), doorMat);
    rail.rotation.x = Math.PI / 3.8;
    rail.position.set(0.25, 1.3, 0.1);
    stairGroup.add(rail);

    var fireDoor = new THREE.Mesh(new THREE.BoxGeometry(0.08, 2.3, 1.4), new THREE.MeshStandardMaterial({ color: 0x16a34a }));
    fireDoor.position.set(-2.12, 1.25, 0);
    stairGroup.add(fireDoor);

    var exitSign = this._addTextSprite('EXIT / CẦU THANG', -2.15, 2.7, 0, '#4ade80', 1.8);
    exitSign.rotation.y = -Math.PI / 2;
    stairGroup.add(exitSign);

    this.elevatorGroup.add(stairGroup);

    // Bảng nút bấm gọi tầng 3D trực tiếp tại cửa thang máy
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
    ctx.fillStyle = 'rgba(15, 23, 42, 0.92)';
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

  /**
   * Mũi tên dẫn hướng sơn trắng trên mặt đường
   */
  Parking3DViewer.prototype._addDirectionalArrow = function (x, z, reverse) {
    var shape = new THREE.Shape();
    shape.moveTo(0, 1.3);
    shape.lineTo(0.7, 0.45);
    shape.lineTo(0.32, 0.45);
    shape.lineTo(0.32, -1.1);
    shape.lineTo(-0.32, -1.1);
    shape.lineTo(-0.32, 0.45);
    shape.lineTo(-0.7, 0.45);
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
    ctx.fillStyle = 'rgba(15, 23, 42, 0.85)';
    ctx.fillRect(0, 0, 256, 64);
    ctx.strokeStyle = color || '#38bdf8';
    ctx.lineWidth = 2;
    ctx.strokeRect(1, 1, 254, 62);
    ctx.fillStyle = color || '#ffffff';
    ctx.font = 'bold 26px "JetBrains Mono", sans-serif';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText(text, 128, 32);

    var texture = new THREE.CanvasTexture(canvas);
    var spriteMat = new THREE.SpriteMaterial({ map: texture });
    var sprite = new THREE.Sprite(spriteMat);
    sprite.position.set(x, y, z);
    sprite.scale.set(scale, scale * 0.25, 1);
    return sprite;
  };

  /**
   * Chuyển Bãi đỗ xe (1, 2 hoặc 3)
   */
  Parking3DViewer.prototype.setLot = function (lotId) {
    var lotCfg = LOT_CONFIGS[lotId];
    if (!lotCfg) return;

    this.options.lotId = lotId;
    this.options.lotName = lotCfg.name;
    this.options.floorId = lotCfg.floors[0];

    if (this.hudTop) this._renderTopHUD();
    this._updateElevatorWidget();
    this.loadSlots(this.options.floorId);
  };

  /**
   * Chuyển tầng hầm
   */
  Parking3DViewer.prototype.setFloor = function (floorId, direction) {
    var lotCfg = LOT_CONFIGS[this.options.lotId] || LOT_CONFIGS[1];
    if (lotCfg.floors.indexOf(floorId) === -1) return;

    var currentIdx = lotCfg.floors.indexOf(this.options.floorId);
    var targetIdx = lotCfg.floors.indexOf(floorId);
    if (!direction) {
      direction = targetIdx < currentIdx ? 'UP' : 'DOWN';
    }

    this.options.floorId = floorId;
    var floorSel = this.hudTop ? this.hudTop.querySelector('#p3dFloorSelect') : null;
    if (floorSel) floorSel.value = floorId;

    this._updateElevatorWidget();
    this._showFloorTransitionToast(lotCfg.floorLabels[floorId] || floorId, direction);

    if (this.elevScreenSprite && this.elevatorGroup) {
      this.elevatorGroup.remove(this.elevScreenSprite);
      var arrowIcon = direction === 'UP' ? '▲' : '▼';
      this.elevScreenSprite = this._addTextSprite('🛗 [ ' + floorId + ' ] ' + arrowIcon, -2.15, 2.7, 0, '#38bdf8', 2.0);
      this.elevScreenSprite.rotation.y = -Math.PI / 2;
      this.elevatorGroup.add(this.elevScreenSprite);
    }

    this._animateElevatorRide(direction, function () {
      this.loadSlots(floorId);
    }.bind(this));

    if (this.options.onFloorChange) {
      this.options.onFloorChange(floorId);
    }
  };

  Parking3DViewer.prototype._showFloorTransitionToast = function (floorLabel, direction) {
    var existing = this.container.querySelector('.p3d-floor-toast');
    if (existing) existing.remove();

    var toast = document.createElement('div');
    toast.className = 'p3d-floor-toast';
    var icon = direction === 'UP' ? '▲' : '▼';
    toast.innerHTML = '<span>' + icon + ' Đang di chuyển đến <b>' + floorLabel + '</b></span>';
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
    var targetY = direction === 'UP' ? startY + 8 : startY - 8;
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
   * Tải danh sách ô đỗ
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
        if (!res.ok) throw new Error('API Error: ' + res.status);
        return res.json();
      })
      .then(function (data) {
        var slotsList = data.slots || data.data || (Array.isArray(data) ? data : []);
        if (slotsList.length === 0) {
          self._renderFallbackSlots(self.options.lotId, targetFloor);
        } else {
          self._renderSlots(slotsList);
        }
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
      countPerRow = floorId === 'B1' ? 14 : (floorId === 'B2' ? 13 : 14);
    } else if (lotId === 3) {
      countPerRow = floorId === 'B1' ? 14 : (floorId === 'B2' ? 14 : (floorId === 'B3' ? 14 : 14));
    }

    var spacing = 3.2;
    var startX = -((countPerRow - 1) * spacing) / 2.0;

    // Dãy A: Phía Bắc (Z = -7.8), quay đầu về phía lối đi (+Z)
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
        positionZ: -7.8,
        rotationY: 0,
        currentCarPlate: stA === 'OCCUPIED' ? '51F-123.' + (i * 11) : (stA === 'RESERVED' ? '72A-888.99' : null),
        vehicleModel: stA === 'OCCUPIED' ? 'Toyota Vios' : null,
        vehicleColor: (i % 2 === 0) ? 'white' : 'black'
      });
    }

    // Dãy B: Phía Nam (Z = +7.8), quay đầu về phía lối đi (-Z)
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
        positionZ: 7.8,
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
   * TẠO MESH Ô ĐỖ VỚI VẠCH KẺ ĐÔI VÀ THANH CHẶN BÁNH XE CAO SU (Ảnh 1, 2, 3):
   *   - Vạch kẻ ô đỗ song song (Double white bay lines)
   *   - Thanh chặn bánh xe cao su chống va chạm tường (Rubber tire stoppers)
   *   - Mesh xe ô tô 3D chân thực khi OCCUPIED
   *   - Khung viền nhấp nháy phát sáng khi RESERVED
   *   - Cọc tiêu an toàn khi MAINTENANCE
   */
  Parking3DViewer.prototype._createSlotMesh = function (slot) {
    var group = new THREE.Group();
    group.position.set(slot.positionX || 0, slot.positionY || 0, slot.positionZ || 0);
    group.rotation.y = (slot.rotationY || 0) * (Math.PI / 180);
    group.userData = { slotData: slot };

    var width = 2.85;
    var length = 5.4;
    var status = (slot.physicalStatus || 'EMPTY').toUpperCase();
    var statusCfg = STATUS_COLORS[status] || STATUS_COLORS.EMPTY;

    // 1. Mặt đệm sàn ô đỗ (subtle colored status pad)
    var padGeo = new THREE.PlaneGeometry(width - 0.2, length - 0.2);
    var padMat = new THREE.MeshStandardMaterial({
      color: statusCfg.hex,
      roughness: 0.8,
      metalness: 0.1,
      transparent: true,
      opacity: status === 'OCCUPIED' ? 0.25 : (status === 'RESERVED' ? 0.38 : (status === 'EMPTY' ? 0.15 : 0.35))
    });
    var pad = new THREE.Mesh(padGeo, padMat);
    pad.rotation.x = -Math.PI / 2;
    pad.position.y = 0.012;
    pad.receiveShadow = true;
    group.add(pad);

    // 2. VẠCH KẺ Ô ĐỖ TRẮNG KẺ ĐÔI (Double Parallel White Bay Lines)
    var lineMat = new THREE.MeshBasicMaterial({
      color: status === 'RESERVED' ? 0x06b6d4 : (slot.vehicleType === 'ev' ? 0x10b981 : 0xffffff)
    });
    var lineWidth = 0.12;

    // Vạch biên trái
    var lLeft = new THREE.Mesh(new THREE.PlaneGeometry(lineWidth, length), lineMat);
    lLeft.rotation.x = -Math.PI / 2;
    lLeft.position.set(-width / 2 + 0.06, 0.018, 0);
    group.add(lLeft);

    // Vạch biên phải
    var lRight = new THREE.Mesh(new THREE.PlaneGeometry(lineWidth, length), lineMat);
    lRight.rotation.x = -Math.PI / 2;
    lRight.position.set(width / 2 - 0.06, 0.018, 0);
    group.add(lRight);

    // Vạch đáy ô (phía sát tường hầm)
    var lBack = new THREE.Mesh(new THREE.PlaneGeometry(width, lineWidth), lineMat);
    lBack.rotation.x = -Math.PI / 2;
    lBack.position.set(0, 0.018, -length / 2 + 0.06);
    group.add(lBack);

    // 3. THANH CHẶN BÁNH XE CAO SU (Rubber Tire Curbs / Stoppers - Ảnh 1)
    var stopperMat = new THREE.MeshStandardMaterial({ color: 0x111827, roughness: 0.95 });
    var stopperStripeMat = new THREE.MeshBasicMaterial({ color: 0xfacc15 });

    var stp1 = new THREE.Mesh(new THREE.BoxGeometry(0.85, 0.14, 0.22), stopperMat);
    stp1.position.set(-0.7, 0.07, -length / 2 + 0.65);
    group.add(stp1);
    var stp1Stripe = new THREE.Mesh(new THREE.BoxGeometry(0.3, 0.142, 0.225), stopperStripeMat);
    stp1Stripe.position.set(-0.7, 0.071, -length / 2 + 0.65);
    group.add(stp1Stripe);

    var stp2 = new THREE.Mesh(new THREE.BoxGeometry(0.85, 0.14, 0.22), stopperMat);
    stp2.position.set(0.7, 0.07, -length / 2 + 0.65);
    group.add(stp2);
    var stp2Stripe = new THREE.Mesh(new THREE.BoxGeometry(0.3, 0.142, 0.225), stopperStripeMat);
    stp2Stripe.position.set(0.7, 0.071, -length / 2 + 0.65);
    group.add(stp2Stripe);

    // Trụ sạc nếu xe điện
    if (slot.vehicleType === 'ev') {
      var evCharger = this._createEVChargerPost();
      evCharger.position.set(0, 0, -length / 2 - 0.2);
      group.add(evCharger);
    }

    // Nhãn mã ô sơn mặt sàn (nhỏ gọn ở mép ngoài ô đỗ)
    var codeSprite = this._createSlotCodeSprite(slot.slotCode, statusCfg.css);
    codeSprite.position.set(0, 0.03, length / 2 - 0.55);
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
      this.pulsingSlots.push({ frame: pulseFrame, pad: pad, slotCode: slot.slotCode });

      var beacon = this._createReservedBeacon(slot.currentCarPlate || 'ĐANG ĐẾN');
      beacon.position.set(0, 1.8, 0);
      group.add(beacon);
    } else if (status === 'UNAVAILABLE') {
      var crossLines = this._createCrossMark(width, length);
      group.add(crossLines);
    }

    this.slotMeshes[slot.slotCode] = group;
    this.slotsGroup.add(group);
  };

  /**
   * Tạo Mesh ô tô 3D chân thực
   */
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
    var bodyMat = new THREE.MeshStandardMaterial({ color: color, metalness: 0.65, roughness: 0.3 });
    var body = new THREE.Mesh(bodyGeo, bodyMat);
    body.position.y = 0.65;
    body.castShadow = true;
    body.receiveShadow = true;
    car.add(body);

    var cabinGeo = new THREE.BoxGeometry(carWidth * 0.86, 0.65, carLength * 0.55);
    var glassMat = new THREE.MeshStandardMaterial({ color: 0x0f172a, roughness: 0.1, metalness: 0.9, transparent: true, opacity: 0.85 });
    var cabin = new THREE.Mesh(cabinGeo, glassMat);
    cabin.position.set(0, 1.3, 0.2);
    cabin.castShadow = true;
    car.add(cabin);

    var wheelGeo = new THREE.CylinderGeometry(0.35, 0.35, 0.28, 16);
    var wheelMat = new THREE.MeshStandardMaterial({ color: 0x111827, roughness: 0.95 });
    var rimMat = new THREE.MeshStandardMaterial({ color: 0xe2e8f0, metalness: 0.85, roughness: 0.2 });

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

    // Đèn pha trước (hướng ra lối xe chạy: +Z)
    var hl1 = new THREE.Mesh(new THREE.BoxGeometry(0.35, 0.18, 0.1), headLightMat);
    hl1.position.set(-0.65, 0.75, carLength / 2 + 0.01);
    var hl2 = hl1.clone();
    hl2.position.x = 0.65;
    car.add(hl1);
    car.add(hl2);

    // Đèn hậu sau (sát tường hầm: -Z)
    var tl1 = new THREE.Mesh(new THREE.BoxGeometry(0.4, 0.18, 0.1), tailLightMat);
    tl1.position.set(-0.65, 0.75, -carLength / 2 - 0.01);
    var tl2 = tl1.clone();
    tl2.position.x = 0.65;
    car.add(tl1);
    car.add(tl2);

    // Biển số xe trước
    if (slot.currentCarPlate) {
      var plateSprite = this._createLicensePlateSprite(slot.currentCarPlate);
      plateSprite.position.set(0, 0.55, carLength / 2 + 0.06);
      car.add(plateSprite);
    }

    return car;
  };

  Parking3DViewer.prototype._createTrafficConeMesh = function () {
    var coneGroup = new THREE.Group();
    var base = new THREE.Mesh(new THREE.BoxGeometry(0.8, 0.08, 0.8), new THREE.MeshStandardMaterial({ color: 0xf97316, roughness: 0.5 }));
    base.position.y = 0.04;
    coneGroup.add(base);

    var cone = new THREE.Mesh(new THREE.ConeGeometry(0.32, 1.2, 16), new THREE.MeshStandardMaterial({ color: 0xf97316, roughness: 0.5 }));
    cone.position.y = 0.68;
    coneGroup.add(cone);

    var stripe = new THREE.Mesh(new THREE.CylinderGeometry(0.24, 0.28, 0.25, 16), new THREE.MeshBasicMaterial({ color: 0xffffff }));
    stripe.position.y = 0.65;
    coneGroup.add(stripe);

    return coneGroup;
  };

  Parking3DViewer.prototype._createEVChargerPost = function () {
    var charger = new THREE.Group();
    var post = new THREE.Mesh(new THREE.BoxGeometry(0.35, 1.6, 0.28), new THREE.MeshStandardMaterial({ color: 0x0284c7, roughness: 0.3 }));
    post.position.y = 0.8;
    charger.add(post);

    var screen = new THREE.Mesh(new THREE.PlaneGeometry(0.25, 0.35), new THREE.MeshBasicMaterial({ color: 0x10b981 }));
    screen.position.set(0, 1.1, 0.145);
    charger.add(screen);

    var lightBar = new THREE.Mesh(new THREE.BoxGeometry(0.35, 0.08, 0.08), new THREE.MeshBasicMaterial({ color: 0x38bdf8 }));
    lightBar.position.set(0, 1.55, 0.12);
    charger.add(lightBar);

    return charger;
  };

  Parking3DViewer.prototype._createPulsingFrame = function (width, length) {
    var geo = new THREE.BufferGeometry();
    var w2 = width / 2;
    var l2 = length / 2;
    var pts = [
      new THREE.Vector3(-w2, 0.05, -l2),
      new THREE.Vector3(w2, 0.05, -l2),
      new THREE.Vector3(w2, 0.05, l2),
      new THREE.Vector3(-w2, 0.05, l2),
      new THREE.Vector3(-w2, 0.05, -l2)
    ];
    geo.setFromPoints(pts);

    var mat = new THREE.LineBasicMaterial({ color: 0x06b6d4, linewidth: 3, transparent: true, opacity: 0.8 });
    return new THREE.Line(geo, mat);
  };

  Parking3DViewer.prototype._createReservedBeacon = function (plateText) {
    var beacon = new THREE.Group();
    var ringGeo = new THREE.RingGeometry(0.4, 0.6, 32);
    var ringMat = new THREE.MeshBasicMaterial({ color: 0x06b6d4, side: THREE.DoubleSide });
    var ring = new THREE.Mesh(ringGeo, ringMat);
    ring.rotation.x = -Math.PI / 2;
    beacon.add(ring);

    var canvas = document.createElement('canvas');
    canvas.width = 256;
    canvas.height = 64;
    var ctx = canvas.getContext('2d');
    ctx.fillStyle = 'rgba(6, 182, 212, 0.9)';
    ctx.fillRect(0, 0, 256, 64);
    ctx.fillStyle = '#090d16';
    ctx.font = 'bold 22px "JetBrains Mono", sans-serif';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText('⏳ ' + plateText, 128, 32);

    var texture = new THREE.CanvasTexture(canvas);
    var sprite = new THREE.Sprite(new THREE.SpriteMaterial({ map: texture }));
    sprite.position.y = 0.8;
    sprite.scale.set(2.4, 0.6, 1);
    beacon.add(sprite);

    return beacon;
  };

  Parking3DViewer.prototype._createCrossMark = function (width, length) {
    var group = new THREE.Group();
    var lineMat = new THREE.MeshBasicMaterial({ color: 0x6b7280 });
    var w2 = width / 2 - 0.2;
    var l2 = length / 2 - 0.2;

    var cross1 = new THREE.Mesh(new THREE.PlaneGeometry(0.12, Math.sqrt(width * width + length * length) - 0.6), lineMat);
    cross1.rotation.x = -Math.PI / 2;
    cross1.rotation.z = Math.atan2(length, width);
    cross1.position.y = 0.02;
    group.add(cross1);

    var cross2 = new THREE.Mesh(new THREE.PlaneGeometry(0.12, Math.sqrt(width * width + length * length) - 0.6), lineMat);
    cross2.rotation.x = -Math.PI / 2;
    cross2.rotation.z = -Math.atan2(length, width);
    cross2.position.y = 0.02;
    group.add(cross2);

    return group;
  };

  Parking3DViewer.prototype._createSlotCodeSprite = function (code, color) {
    var canvas = document.createElement('canvas');
    canvas.width = 160;
    canvas.height = 48;
    var ctx = canvas.getContext('2d');
    ctx.fillStyle = color || '#38bdf8';
    ctx.font = 'bold 28px "JetBrains Mono", sans-serif';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText(code, 80, 24);

    var texture = new THREE.CanvasTexture(canvas);
    var mesh = new THREE.Mesh(new THREE.PlaneGeometry(1.6, 0.48), new THREE.MeshBasicMaterial({ map: texture, transparent: true }));
    return mesh;
  };

  Parking3DViewer.prototype._createLicensePlateSprite = function (plate) {
    var canvas = document.createElement('canvas');
    canvas.width = 160;
    canvas.height = 48;
    var ctx = canvas.getContext('2d');
    ctx.fillStyle = '#ffffff';
    ctx.fillRect(0, 0, 160, 48);
    ctx.strokeStyle = '#000000';
    ctx.lineWidth = 3;
    ctx.strokeRect(2, 2, 156, 44);
    ctx.fillStyle = '#000000';
    ctx.font = 'bold 24px "JetBrains Mono", sans-serif';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText(plate || '51F-123.45', 80, 24);

    var texture = new THREE.CanvasTexture(canvas);
    var sprite = new THREE.Sprite(new THREE.SpriteMaterial({ map: texture }));
    sprite.scale.set(0.9, 0.28, 1);
    return sprite;
  };

  /**
   * Cập nhật số liệu thống kê
   */
  Parking3DViewer.prototype._updateStats = function () {
    var total = this.slots.length;
    var free = 0;
    var occ = 0;
    var res = 0;
    var blk = 0;

    for (var i = 0; i < total; i++) {
      var s = (this.slots[i].physicalStatus || 'EMPTY').toUpperCase();
      if (s === 'EMPTY') free++;
      else if (s === 'OCCUPIED') occ++;
      else if (s === 'RESERVED') res++;
      else if (s === 'MAINTENANCE' || s === 'UNAVAILABLE') blk++;
    }

    var elTotal = document.getElementById('statTotal');
    if (elTotal) elTotal.textContent = total;
    var elFree = document.getElementById('statFree');
    if (elFree) elFree.textContent = free;
    var elOcc = document.getElementById('statOcc');
    if (elOcc) elOcc.textContent = occ;
    var elRes = document.getElementById('statRes');
    if (elRes) elRes.textContent = res;
    var elBlk = document.getElementById('statBlk');
    if (elBlk) elBlk.textContent = blk;
  };

  /**
   * Lọc hiển thị theo trạng thái
   */
  Parking3DViewer.prototype.filterStatus = function (statusFilter) {
    this.activeFilter = statusFilter;
    var self = this;
    if (!this.slotsGroup) return;

    this.slots.forEach(function (slot) {
      var mesh = self.slotMeshes[slot.slotCode];
      if (!mesh) return;

      var st = (slot.physicalStatus || 'EMPTY').toUpperCase();
      if (statusFilter === 'ALL' || st === statusFilter) {
        mesh.visible = true;
      } else {
        mesh.visible = false;
      }
    });
  };

  /**
   * Cập nhật realtime trạng thái một ô đỗ
   */
  Parking3DViewer.prototype.updateSlotStatus = function (slotCode, newStatus, details) {
    var slot = null;
    for (var i = 0; i < this.slots.length; i++) {
      if (this.slots[i].slotCode === slotCode) {
        slot = this.slots[i];
        break;
      }
    }
    if (!slot) return;

    slot.physicalStatus = newStatus;
    if (details) {
      if (details.carPlate) slot.currentCarPlate = details.carPlate;
      if (details.vehicleModel) slot.vehicleModel = details.vehicleModel;
      if (details.vehicleColor) slot.vehicleColor = details.vehicleColor;
    }

    var oldMesh = this.slotMeshes[slotCode];
    if (oldMesh && this.slotsGroup) {
      this.slotsGroup.remove(oldMesh);
    }
    this.pulsingSlots = this.pulsingSlots.filter(function (p) { return p.slotCode !== slotCode; });

    this._createSlotMesh(slot);
    this._updateStats();
  };

  /**
   * Chuyển đổi góc nhìn Camera linh hoạt:
   *   - 'FIRST_PERSON': Góc nhìn thực tế người đi bộ trong hầm ngầm (giống Sketchfab)
   *   - 'DRIVEWAY': Góc nhìn người lái xe dọc lối đi trung tâm
   *   - 'OVERVIEW': Góc nhìn toàn cảnh 3D trên cao (tự động ẩn trần)
   *   - 'TOP_DOWN': Góc nhìn mặt bằng 2D
   */
  Parking3DViewer.prototype.setCameraView = function (mode) {
    var viewCfg = this.camViews[mode] || this.camViews.FIRST_PERSON;
    this.currentViewMode = mode;

    var self = this;
    var startCam = { x: this.camera.position.x, y: this.camera.position.y, z: this.camera.position.z };
    var startTarget = { x: this.controls.target.x, y: this.controls.target.y, z: this.controls.target.z };

    var endCam = viewCfg.cam;
    var endTarget = viewCfg.target;

    var startTime = performance.now();
    var duration = 650;

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
  };

  Parking3DViewer.prototype.resetView = function () {
    this.setCameraView('FIRST_PERSON');
  };

  Parking3DViewer.prototype.toggleTopDownView = function () {
    if (this.currentViewMode === 'TOP_DOWN') {
      this.setCameraView('FIRST_PERSON');
    } else {
      this.setCameraView('TOP_DOWN');
    }
  };

  /**
   * Đăng ký sự kiện
   */
  Parking3DViewer.prototype._bindEvents = function () {
    var self = this;
    var dom = this.renderer.domElement;

    var isDragging = false;
    var startMouseX = 0;
    var startMouseY = 0;

    dom.addEventListener('mousedown', function (e) {
      isDragging = false;
      startMouseX = e.clientX;
      startMouseY = e.clientY;
    });

    dom.addEventListener('mousemove', function (e) {
      if (Math.abs(e.clientX - startMouseX) > 4 || Math.abs(e.clientY - startMouseY) > 4) {
        isDragging = true;
      }
      var rect = dom.getBoundingClientRect();
      self.mouse.x = ((e.clientX - rect.left) / rect.width) * 2 - 1;
      self.mouse.y = -((e.clientY - rect.top) / rect.height) * 2 + 1;
      self._checkHover(e.clientX, e.clientY);
    });

    dom.addEventListener('mouseup', function (e) {
      if (!isDragging) {
        self._checkClick();
      }
    });

    dom.addEventListener('mouseleave', function () {
      self._hideTooltip();
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
        var action = elevHits[j].object.userData.action;
        if (action === 'ELEV_UP') {
          var lotCfg = LOT_CONFIGS[this.options.lotId] || LOT_CONFIGS[1];
          var idx = lotCfg.floors.indexOf(this.options.floorId);
          if (idx > 0) this.setFloor(lotCfg.floors[idx - 1], 'UP');
          return;
        } else if (action === 'ELEV_DOWN') {
          var lotCfg2 = LOT_CONFIGS[this.options.lotId] || LOT_CONFIGS[1];
          var idx2 = lotCfg2.floors.indexOf(this.options.floorId);
          if (idx2 < lotCfg2.floors.length - 1) this.setFloor(lotCfg2.floors[idx2 + 1], 'DOWN');
          return;
        }
      }
    }

    // 2. Kiểm tra click vào ô đỗ xe
    if (!this.slotsGroup) return;
    var intersects = this.raycaster.intersectObjects(this.slotsGroup.children, true);
    if (intersects.length > 0) {
      var obj = intersects[0].object;
      while (obj && !obj.userData.slotData && obj.parent) {
        obj = obj.parent;
      }
      if (obj && obj.userData.slotData) {
        if (this.options.onSlotClick) {
          this.options.onSlotClick(obj.userData.slotData);
        }
      }
    }
  };

  Parking3DViewer.prototype._checkHover = function (clientX, clientY) {
    if (!this.slotsGroup) return;

    this.raycaster.setFromCamera(this.mouse, this.camera);
    var intersects = this.raycaster.intersectObjects(this.slotsGroup.children, true);

    var slotHit = null;
    for (var i = 0; i < intersects.length; i++) {
      var obj = intersects[i].object;
      while (obj && !obj.userData.slotData && obj.parent) {
        obj = obj.parent;
      }
      if (obj && obj.userData.slotData) {
        slotHit = obj.userData.slotData;
        break;
      }
    }

    if (slotHit) {
      this.hoveredSlot = slotHit;
      this._showTooltip(slotHit, clientX, clientY);
      if (this.options.onSlotHover) this.options.onSlotHover(slotHit);
    } else {
      this.hoveredSlot = null;
      this._hideTooltip();
    }
  };

  Parking3DViewer.prototype._showTooltip = function (slot, clientX, clientY) {
    if (!this.tooltip) return;

    var status = (slot.physicalStatus || 'EMPTY').toUpperCase();
    var statusCfg = STATUS_COLORS[status] || STATUS_COLORS.EMPTY;

    var html = [
      '<div class="p3d-tooltip-header">',
      '  <span class="p3d-tooltip-code">' + slot.slotCode + '</span>',
      '  <span class="p3d-tooltip-badge badge-' + statusCfg.code + '">' + statusCfg.label + '</span>',
      '</div>',
      '<div class="p3d-tooltip-body">',
      '  <div class="p3d-tooltip-row"><span class="label">Loại xe:</span><span class="val">' + (slot.vehicleType === 'ev' ? '⚡ Xe điện' : '🚗 Ô tô 4-7 chỗ') + '</span></div>',
      slot.currentCarPlate ? '  <div class="p3d-tooltip-row"><span class="label">Biển số:</span><span class="val" style="font-family:monospace;color:#38bdf8">' + slot.currentCarPlate + '</span></div>' : '',
      slot.vehicleModel ? '  <div class="p3d-tooltip-row"><span class="label">Dòng xe:</span><span class="val">' + slot.vehicleModel + '</span></div>' : '',
      '  <div class="p3d-tooltip-row"><span class="label">Tầng:</span><span class="val">' + this.options.floorId + '</span></div>',
      '</div>'
    ].join('');

    this.tooltip.innerHTML = html;
    this.tooltip.style.left = (clientX + 16) + 'px';
    this.tooltip.style.top = (clientY + 16) + 'px';
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

    // Tự động ẩn trần hầm khi Camera nhìn từ trên cao (Y > 5.5m) để dễ quan sát slot
    if (this.camera && this.ceilingGroup) {
      this.ceilingGroup.visible = (this.camera.position.y <= 5.5);
    }

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
