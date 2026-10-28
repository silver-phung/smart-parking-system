/**
 * ==========================================================
 * Parking3DViewer Component (Three.js)
 * Sprint 3 - Days 6-9: 3D Visualization cho Hệ thống Smart Parking
 *
 * Tính năng chính:
 *   - Scene 3D hoàn chỉnh: Sàn bãi đỗ, phân khu, đường đi xe chạy, cột trụ, ô đỗ.
 *   - OrbitControls: Hỗ trợ chuột PC (xoay, zoom, pan) & cảm ứng Mobile (touch rotate, pinch zoom, 2-finger pan).
 *   - Nút "Reset View": Đưa camera về góc nhìn toàn cảnh ban đầu với hiệu ứng chuyển cảnh mượt mà.
 *   - 4 trạng thái vật lý chuẩn:
 *       🟢 Trống (Available / EMPTY)
 *       🔴 Có xe đã đỗ (Occupied / OCCUPIED) -> Render mesh xe ô tô 3D chân thực
 *       🟡 Bảo trì (Maintenance / MAINTENANCE) -> Render mesh cọc tiêu giao thông 3D
 *       ⚪ Không khả dụng (Unavailable / UNAVAILABLE) -> Ký hiệu tạm khóa
 *   - Trạng thái đặc biệt: "Ô được phân nhưng xe chưa vào" (RESERVED) -> Nhấp nháy viền (Pulsing highlight) & nhãn riêng.
 *   - Đọc tự động từ API /api/ParkingSlots hoặc dữ liệu bãi cục bộ (SP.LOTS).
 *   - Đóng gói chuẩn Module / UMD / React-friendly.
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

  // Hằng số màu sắc trạng thái vật lý chuẩn
  var STATUS_COLORS = {
    EMPTY:       { hex: 0x10b981, css: '#10b981', label: 'Trống', code: 'free' },
    OCCUPIED:    { hex: 0xef4444, css: '#ef4444', label: 'Có xe', code: 'occ' },
    RESERVED:    { hex: 0x06b6d4, css: '#06b6d4', label: 'Chờ xe vào', code: 'res' },
    MAINTENANCE: { hex: 0xf59e0b, css: '#f59e0b', label: 'Bảo trì', code: 'blk' },
    UNAVAILABLE: { hex: 0x6b7280, css: '#6b7280', label: 'Tạm khóa', code: 'off' }
  };

  var CAR_COLORS = [0xf8fafc, 0x1e293b, 0x2563eb, 0xdc2626, 0x94a3b8, 0x059669];

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

    this.options = {
      apiEndpoint: options.apiEndpoint || '/api/ParkingSlots',
      lotId: options.lotId || 1,
      lotName: options.lotName || 'Bãi xe SmartPark',
      floorId: options.floorId || 'B1',
      showStats: options.showStats !== false,
      showControls: options.showControls !== false,
      onSlotClick: options.onSlotClick || null,
      onSlotHover: options.onSlotHover || null
    };

    this.slots = [];
    this.slotMeshes = {};        // Map: slotCode -> Three.Group / Mesh
    this.carMeshes = {};         // Map: slotCode -> Three.Group
    this.coneMeshes = {};        // Map: slotCode -> Three.Group
    this.pulsingSlots = [];      // Danh sách ô đang nhấp nháy viền (RESERVED)
    this.activeFilter = 'ALL';   // ALL, EMPTY, OCCUPIED, RESERVED, MAINTENANCE
    this.isTopDown = false;

    // Thiết lập góc nhìn camera ban đầu (Isometric View)
    this.defaultCamPos = { x: 0, y: 38, z: 46 };
    this.defaultTarget = { x: 0, y: 0, z: 0 };

    this.clock = null;
    this.animId = null;
    this.raycaster = null;
    this.mouse = null;
    this.hoveredSlot = null;

    this._ensureDependencies(function () {
      this._buildUI();
      this._initThree();
      this.loadSlots();
    }.bind(this));
  }

  /**
   * Tải động Three.js và OrbitControls nếu chưa có trong trang
   */
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
      s.onerror = function () {
        console.error('[Parking3DViewer] Không thể tải thư viện:', src);
      };
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
   * Khởi tạo giao diện DOM (Canvas + HUD + Tooltip)
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
    this.loaderEl.innerHTML = '<div class="p3d-spinner"></div><span>Đang dựng không gian bãi đỗ 3D...</span>';
    this.container.appendChild(this.loaderEl);

    // Top HUD (Bộ chọn tầng + Thống kê)
    if (this.options.showStats) {
      this.hudTop = document.createElement('div');
      this.hudTop.className = 'p3d-hud-top';
      this.hudTop.innerHTML = [
        '<div class="p3d-hud-group">',
        '  <span class="p3d-lot-title"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M19 17h2c.6 0 1-.4 1-1v-3c0-.9-.7-1.7-1.5-1.9C18.7 10.6 16 10 16 10s-1.3-1.4-2.2-2.3c-.5-.4-1.1-.7-1.8-.7H5c-.6 0-1.1.4-1.4.9l-1.4 2.9A3.7 3.7 0 0 0 2 12v4c0 .6.4 1 1 1h2"/><circle cx="7" cy="17" r="2"/><circle cx="17" cy="17" r="2"/></svg> ' + this.options.lotName + '</span>',
        '  <select class="p3d-floor-select" id="p3dFloorSelect">',
        '    <option value="B1" selected>Hầm B1</option>',
        '    <option value="B2">Hầm B2</option>',
        '    <option value="G">Tầng Trệt (G)</option>',
        '  </select>',
        '</div>',
        '<div class="p3d-hud-group p3d-stats-pills">',
        '  <span class="p3d-pill" data-filter="ALL" title="Xem tất cả">Tất cả: <b id="statTotal">0</b></span>',
        '  <span class="p3d-pill active" data-filter="EMPTY" title="Lọc ô trống"><i class="dot dot-free"></i> Trống: <b id="statFree">0</b></span>',
        '  <span class="p3d-pill" data-filter="OCCUPIED" title="Lọc ô có xe"><i class="dot dot-occ"></i> Có xe: <b id="statOcc">0</b></span>',
        '  <span class="p3d-pill" data-filter="RESERVED" title="Lọc ô đã phân chờ vào"><i class="dot dot-res"></i> Chờ vào: <b id="statRes">0</b></span>',
        '  <span class="p3d-pill" data-filter="MAINTENANCE" title="Lọc ô bảo trì"><i class="dot dot-blk"></i> Bảo trì: <b id="statBlk">0</b></span>',
        '</div>'
      ].join('');
      this.container.appendChild(this.hudTop);

      // Event change floor
      var floorSel = this.hudTop.querySelector('#p3dFloorSelect');
      floorSel.value = this.options.floorId;
      floorSel.addEventListener('change', function (e) {
        self.setFloor(e.target.value);
      });

      // Event click filter pills
      var pills = this.hudTop.querySelectorAll('.p3d-pill');
      pills.forEach(function (pill) {
        pill.addEventListener('click', function () {
          pills.forEach(function (p) { p.classList.remove('active'); });
          pill.classList.add('active');
          self.filterStatus(pill.getAttribute('data-filter'));
        });
      });
    }

    // Bottom HUD (Reset View & Trợ giúp điều hướng)
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
        '  <span>🖱️ Chuột trái: Xoay</span>',
        '  <span>🔍 Cuộn chuột: Zoom</span>',
        '  <span>👆 Chuột phải: Dời sàn</span>',
        '  <span>📱 Cảm ứng 1 & 2 ngón tay</span>',
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

    // Floating Tooltip
    this.tooltip = document.createElement('div');
    this.tooltip.className = 'p3d-tooltip';
    this.container.appendChild(this.tooltip);
  };

  /**
   * Khởi tạo Three.js: Scene, Camera, Renderer, Lights, OrbitControls
   */
  Parking3DViewer.prototype._initThree = function () {
    var width = this.canvasWrap.clientWidth || 800;
    var height = this.canvasWrap.clientHeight || 500;

    this.clock = new THREE.Clock();

    // Scene
    this.scene = new THREE.Scene();
    this.scene.background = new THREE.Color(0x0f172a);
    this.scene.fog = new THREE.FogExp2(0x0f172a, 0.007);

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

    // OrbitControls
    this.controls = new THREE.OrbitControls(this.camera, this.renderer.domElement);
    this.controls.enableDamping = true;
    this.controls.dampingFactor = 0.05;
    this.controls.maxPolarAngle = Math.PI / 2.05; // Chặn camera không lọt xuống dưới mặt đất
    this.controls.minDistance = 10;
    this.controls.maxDistance = 140;
    this.controls.target.set(this.defaultTarget.x, this.defaultTarget.y, this.defaultTarget.z);

    // Ánh sáng
    this._setupLighting();

    // Dựng kết cấu cơ sở (Sàn, Vạch đường, Cột trụ)
    this._buildParkingStructure();

    // Raycaster cho sự kiện click & hover trên các ô
    this.raycaster = new THREE.Raycaster();
    this.mouse = new THREE.Vector2();

    this._bindEvents();
    this._animate();
  };

  /**
   * Thiết lập hệ thống ánh sáng
   */
  Parking3DViewer.prototype._setupLighting = function () {
    // Ánh sáng môi trường dịu nhẹ
    var ambientLight = new THREE.AmbientLight(0xffffff, 0.7);
    this.scene.add(ambientLight);

    // Ánh sáng đèn trần bãi xe (Hemisphere light)
    var hemiLight = new THREE.HemisphereLight(0x38bdf8, 0x1e293b, 0.6);
    hemiLight.position.set(0, 50, 0);
    this.scene.add(hemiLight);

    // Đèn chiếu chính có đổ bóng mềm
    var dirLight = new THREE.DirectionalLight(0xffffff, 0.8);
    dirLight.position.set(30, 45, 25);
    dirLight.castShadow = true;
    dirLight.shadow.mapSize.width = 2048;
    dirLight.shadow.mapSize.height = 2048;
    dirLight.shadow.camera.near = 10;
    dirLight.shadow.camera.far = 150;
    var d = 40;
    dirLight.shadow.camera.left = -d;
    dirLight.shadow.camera.right = d;
    dirLight.shadow.camera.top = d;
    dirLight.shadow.camera.bottom = -d;
    dirLight.shadow.bias = -0.0005;
    this.scene.add(dirLight);

    // Đèn phụ chống tối góc khuất
    var fillLight = new THREE.DirectionalLight(0x0284c7, 0.4);
    fillLight.position.set(-30, 20, -25);
    this.scene.add(fillLight);
  };

  /**
   * Dựng kết cấu vật lý sàn bãi đỗ xe:
   *   - Mặt sàn bê tông/asphalt
   *   - Làn xe chạy (driving lanes) có vạch kẻ tim đường vàng/trắng
   *   - Mũi tên chỉ dẫn hướng xe lưu thông
   *   - Cột kết cấu chịu lực (Structural columns A1, A2, B1, B2)
   */
  Parking3DViewer.prototype._buildParkingStructure = function () {
    this.structureGroup = new THREE.Group();

    // 1. Mặt sàn bê tông chính (kích thước 60m x 40m)
    var floorGeo = new THREE.BoxGeometry(64, 0.8, 38);
    var floorMat = new THREE.MeshStandardMaterial({
      color: 0x1e293b,
      roughness: 0.85,
      metalness: 0.1
    });
    var floorMesh = new THREE.Mesh(floorGeo, floorMat);
    floorMesh.position.y = -0.4;
    floorMesh.receiveShadow = true;
    this.structureGroup.add(floorMesh);

    // 2. Viền vách tường bo xung quanh
    var borderGeo = new THREE.BoxGeometry(65, 0.6, 39);
    var borderMat = new THREE.MeshStandardMaterial({ color: 0x0f172a, roughness: 0.9 });
    var borderMesh = new THREE.Mesh(borderGeo, borderMat);
    borderMesh.position.y = -0.7;
    this.structureGroup.add(borderMesh);

    // 3. Đường xe chạy trung tâm (Driveway Lane)
    var laneGeo = new THREE.PlaneGeometry(56, 7.5);
    var laneMat = new THREE.MeshStandardMaterial({
      color: 0x0f172a,
      roughness: 0.95
    });
    var laneMesh = new THREE.Mesh(laneGeo, laneMat);
    laneMesh.rotation.x = -Math.PI / 2;
    laneMesh.position.y = 0.01;
    laneMesh.receiveShadow = true;
    this.structureGroup.add(laneMesh);

    // Vạch kẻ tim đường đứt đoạn (màu vàng)
    for (var x = -24; x <= 24; x += 4) {
      var dashGeo = new THREE.PlaneGeometry(2, 0.25);
      var dashMat = new THREE.MeshBasicMaterial({ color: 0xfacc15 });
      var dash = new THREE.Mesh(dashGeo, dashMat);
      dash.rotation.x = -Math.PI / 2;
      dash.position.set(x, 0.02, 0);
      this.structureGroup.add(dash);
    }

    // 4. Mũi tên dẫn hướng 3D trên đường chạy
    this._addDirectionalArrow(-12, 0, false);
    this._addDirectionalArrow(12, 0, false);

    // 5. Cột bê tông phân khu (A1, A2, B1, B2)
    var colCoords = [
      { x: -16, z: -4, label: 'A1' },
      { x: 0,   z: -4, label: 'A2' },
      { x: 16,  z: -4, label: 'A3' },
      { x: -16, z: 4,  label: 'B1' },
      { x: 0,   z: 4,  label: 'B2' },
      { x: 16,  z: 4,  label: 'B3' }
    ];

    var colGeo = new THREE.BoxGeometry(1.2, 5.5, 1.2);
    var colMat = new THREE.MeshStandardMaterial({ color: 0x334155, roughness: 0.7 });
    var colStripeMat = new THREE.MeshBasicMaterial({ color: 0xfacc15 }); // Sơn phản quang vàng

    for (var i = 0; i < colCoords.length; i++) {
      var c = colCoords[i];
      var colMesh = new THREE.Mesh(colGeo, colMat);
      colMesh.position.set(c.x, 2.75, c.z);
      colMesh.castShadow = true;
      colMesh.receiveShadow = true;
      this.structureGroup.add(colMesh);

      // Dải decal phản quang an toàn trên cột
      var stripe = new THREE.Mesh(new THREE.BoxGeometry(1.25, 0.6, 1.25), colStripeMat);
      stripe.position.set(c.x, 1.6, c.z);
      this.structureGroup.add(stripe);

      // Nhãn mã cột
      this._addTextSprite(c.label, c.x, 4.0, c.z, '#38bdf8', 1.8);
    }

    // 6. Biển báo Cổng Vào / Cổng Ra 3D
    this._addGateSign(-27, 0, 'CỔNG VÀO (IN)', 0x10b981);
    this._addGateSign(27, 0, 'CỔNG RA (OUT)', 0xef4444);

    this.scene.add(this.structureGroup);
  };

  /**
   * Tạo mũi tên dẫn hướng sơn trên sàn
   */
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

  /**
   * Tạo biển chỉ dẫn Cổng Vào/Ra 3D
   */
  Parking3DViewer.prototype._addGateSign = function (x, z, text, colorHex) {
    var post = new THREE.Mesh(
      new THREE.CylinderGeometry(0.12, 0.12, 4.5),
      new THREE.MeshStandardMaterial({ color: 0x475569 })
    );
    post.position.set(x, 2.25, z);
    this.structureGroup.add(post);

    this._addTextSprite(text, x, 4.6, z, '#' + new THREE.Color(colorHex).getHexString(), 2.2);
  };

  /**
   * Tạo chữ 3D thông qua Sprite Canvas 2D
   */
  Parking3DViewer.prototype._addTextSprite = function (text, x, y, z, color, scale) {
    scale = scale || 2.0;
    var canvas = document.createElement('canvas');
    canvas.width = 256;
    canvas.height = 64;
    var ctx = canvas.getContext('2d');
    ctx.font = 'bold 30px "JetBrains Mono", sans-serif';
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
   * Tải danh sách ô đỗ từ API hoặc fallback sang bộ dữ liệu mẫu
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
      .catch(function (err) {
        console.warn('[Parking3DViewer] Không thể kết nối API ' + url + ', đang fallback sang dữ liệu mẫu:', err.message);
        self._renderFallbackSlots(targetFloor);
      })
      .finally(function () {
        if (self.loaderEl) {
          self.loaderEl.style.opacity = '0';
          setTimeout(function () { self.loaderEl.style.pointerEvents = 'none'; }, 400);
        }
      });
  };

  /**
   * Tạo dữ liệu fallback nếu backend API chưa bật
   */
  Parking3DViewer.prototype._renderFallbackSlots = function (floorId) {
    var sampleSlots = [];
    var rng = function (seed) {
      return ((seed * 9301 + 49297) % 233280) / 233280;
    };

    // Dãy A: Z = -8
    for (var i = 1; i <= 10; i++) {
      var codeA = floorId + '-A' + (i < 10 ? '0' : '') + i;
      var stA = (i === 1 || i === 4 || i === 7) ? 'OCCUPIED' : (i === 3 ? 'RESERVED' : (i === 8 ? 'MAINTENANCE' : 'EMPTY'));
      sampleSlots.push({
        id: i,
        slotCode: codeA,
        physicalStatus: stA,
        vehicleType: 'car',
        positionX: -20 + (i * 3.6),
        positionY: 0,
        positionZ: -8,
        rotationY: 0,
        currentCarPlate: stA === 'OCCUPIED' ? '51F-123.' + (i * 11) : (stA === 'RESERVED' ? '72A-888.99' : null),
        vehicleModel: stA === 'OCCUPIED' ? 'Toyota Vios' : null,
        vehicleColor: 'white'
      });
    }

    // Dãy B: Z = +8
    for (var j = 1; j <= 10; j++) {
      var codeB = floorId + '-B' + (j < 10 ? '0' : '') + j;
      var stB = (j === 2 || j === 6) ? 'OCCUPIED' : (j === 5 ? 'RESERVED' : (j === 9 ? 'UNAVAILABLE' : 'EMPTY'));
      sampleSlots.push({
        id: 10 + j,
        slotCode: codeB,
        physicalStatus: stB,
        vehicleType: j <= 6 ? 'ev' : 'car',
        positionX: -20 + (j * 3.6),
        positionY: 0,
        positionZ: 8,
        rotationY: 180,
        currentCarPlate: stB === 'OCCUPIED' ? '30H-999.' + (j * 11) : (stB === 'RESERVED' ? '51K-777.66' : null),
        vehicleModel: stB === 'OCCUPIED' ? 'VinFast VF8' : null,
        vehicleColor: 'emerald'
      });
    }

    this._renderSlots(sampleSlots);
  };

  /**
   * Render danh sách ô đỗ lên 3D Scene
   */
  Parking3DViewer.prototype._renderSlots = function (slotsData) {
    var self = this;
    this.slots = slotsData;

    // Xóa các ô cũ
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
   * Tạo 3D Mesh cho một ô đỗ xe:
   *   - Mặt sàn ô đỗ có viền màu tương ứng trạng thái
   *   - Vạch kẻ ô chữ U sơn trắng/vàng
   *   - Nhãn mã ô đỗ (A1-012, B1-A01)
   *   - Mesh xe ô tô nếu OCCUPIED
   *   - Mesh cọc tiêu cảnh báo nếu MAINTENANCE
   *   - Viền nhấp nháy phát sáng nếu RESERVED (Đã phân nhưng xe chưa vào)
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

    // 1. Mặt sàn ô đỗ (Ground Pad)
    var padGeo = new THREE.PlaneGeometry(width, length);
    var padMat = new THREE.MeshStandardMaterial({
      color: statusCfg.hex,
      roughness: 0.7,
      metalness: 0.1,
      transparent: true,
      opacity: status === 'OCCUPIED' ? 0.35 : (status === 'RESERVED' ? 0.45 : (status === 'EMPTY' ? 0.25 : 0.4))
    });
    var pad = new THREE.Mesh(padGeo, padMat);
    pad.rotation.x = -Math.PI / 2;
    pad.position.y = 0.015;
    pad.receiveShadow = true;
    group.add(pad);

    // 2. Vạch kẻ biên chữ U (U-shaped Parking Lines - tiêu chuẩn bãi xe)
    var lineMat = new THREE.MeshBasicMaterial({
      color: status === 'RESERVED' ? 0x06b6d4 : (slot.vehicleType === 'ev' ? 0x10b981 : 0xffffff)
    });
    var lineWidth = 0.14;

    // Vạch trái
    var lLeft = new THREE.Mesh(new THREE.PlaneGeometry(lineWidth, length), lineMat);
    lLeft.rotation.x = -Math.PI / 2;
    lLeft.position.set(-width / 2, 0.02, 0);
    group.add(lLeft);

    // Vạch phải
    var lRight = new THREE.Mesh(new THREE.PlaneGeometry(lineWidth, length), lineMat);
    lRight.rotation.x = -Math.PI / 2;
    lRight.position.set(width / 2, 0.02, 0);
    group.add(lRight);

    // Vạch đáy chặn đuôi xe
    var lBack = new THREE.Mesh(new THREE.PlaneGeometry(width, lineWidth), lineMat);
    lBack.rotation.x = -Math.PI / 2;
    lBack.position.set(0, 0.02, length / 2);
    group.add(lBack);

    // Trạm sạc xe điện nếu ô EV
    if (slot.vehicleType === 'ev') {
      var evCharger = this._createEVChargerPost();
      evCharger.position.set(0, 0, length / 2 + 0.4);
      group.add(evCharger);
    }

    // 3. Nhãn mã ô sơn trên mặt đường
    var codeSprite = this._createSlotCodeSprite(slot.slotCode, statusCfg.css);
    codeSprite.position.set(0, 0.03, -length / 2 + 0.7);
    codeSprite.rotation.x = -Math.PI / 2;
    group.add(codeSprite);

    // 4. Render chi tiết theo 4 trạng thái vật lý:
    if (status === 'OCCUPIED') {
      // 🔴 Render MESH XE Ô TÔ 3D tại ô
      var car = this._create3DCarMesh(slot);
      car.position.set(0, 0, 0);
      group.add(car);
      this.carMeshes[slot.slotCode] = car;
    } else if (status === 'MAINTENANCE') {
      // 🟡 Render CỌC TIÊU GIAO THÔNG 3D cảnh báo bảo trì
      var cone = this._createTrafficConeMesh();
      cone.position.set(0, 0, 0);
      group.add(cone);
      this.coneMeshes[slot.slotCode] = cone;
    } else if (status === 'RESERVED') {
      // ⏳ Ô ĐƯỢC PHÂN NHƯNG XE CHƯA VÀO:
      // Tạo khung viền phát sáng nhấp nháy (Pulsing glowing border)
      var pulseFrame = this._createPulsingFrame(width, length);
      group.add(pulseFrame);
      this.pulsingSlots.push({
        slotCode: slot.slotCode,
        frame: pulseFrame,
        pad: pad
      });

      // Beacon hologram hiển thị biển số xe đang chờ đến
      var beacon = this._createReservedBeacon(slot.currentCarPlate || 'ĐANG ĐẾN');
      beacon.position.set(0, 2.2, 0);
      group.add(beacon);
    } else if (status === 'UNAVAILABLE') {
      // ⚪ Render ký hiệu gạch chéo cấm đỗ
      var crossLines = this._createCrossMark(width, length);
      group.add(crossLines);
    }

    this.slotMeshes[slot.slotCode] = group;
    this.slotsGroup.add(group);
  };

  /**
   * Tạo Mesh Xe Ô tô 3D chân thực:
   *   - Khung thân xe (Chassis) với màu sắc đa dạng
   *   - Cabin mui xe + kính chắn gió trong suốt
   *   - 4 bánh xe cao su + mâm vành
   *   - Đèn pha trước (LED trắng) + Đèn hậu (Đỏ)
   *   - Biển số xe gắn trước và sau
   */
  Parking3DViewer.prototype._create3DCarMesh = function (slot) {
    var car = new THREE.Group();

    // Lựa chọn màu xe: trắng, đen, xanh biển, đỏ, bạc
    var color = 0xf8fafc;
    if (slot.vehicleColor === 'black') color = 0x1e293b;
    else if (slot.vehicleColor === 'blue') color = 0x2563eb;
    else if (slot.vehicleColor === 'red') color = 0xdc2626;
    else if (slot.vehicleColor === 'emerald') color = 0x059669;

    var carWidth = 2.0;
    var carLength = 4.4;
    var carHeight = 0.85;

    // 1. Thân xe chính (Chassis)
    var bodyGeo = new THREE.BoxGeometry(carWidth, carHeight, carLength);
    var bodyMat = new THREE.MeshStandardMaterial({
      color: color,
      metalness: 0.6,
      roughness: 0.3
    });
    var body = new THREE.Mesh(bodyGeo, bodyMat);
    body.position.y = 0.65;
    body.castShadow = true;
    body.receiveShadow = true;
    car.add(body);

    // 2. Cabin mui xe & Kính chắn gió
    var cabinGeo = new THREE.BoxGeometry(carWidth * 0.85, 0.65, carLength * 0.55);
    var glassMat = new THREE.MeshStandardMaterial({
      color: 0x0f172a,
      roughness: 0.1,
      metalness: 0.9,
      transparent: true,
      opacity: 0.85
    });
    var cabin = new THREE.Mesh(cabinGeo, glassMat);
    cabin.position.set(0, 1.3, -0.2);
    cabin.castShadow = true;
    car.add(cabin);

    // 3. 4 Bánh xe cao su
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

    // 4. Đèn pha trước (Xenon White) & Đèn hậu (Red)
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

    // 5. Biển số xe trước & sau (License plate)
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

  /**
   * Tạo Mesh Cọc Tiêu Giao Thông 3D (Traffic Cones) cho ô Bảo Trì
   */
  Parking3DViewer.prototype._createTrafficConeMesh = function () {
    var coneGroup = new THREE.Group();

    var baseGeo = new THREE.BoxGeometry(0.8, 0.08, 0.8);
    var baseMat = new THREE.MeshStandardMaterial({ color: 0xf97316, roughness: 0.5 });
    var base = new THREE.Mesh(baseGeo, baseMat);
    base.position.y = 0.04;
    coneGroup.add(base);

    var coneGeo = new THREE.ConeGeometry(0.32, 1.2, 16);
    var coneMat = new THREE.MeshStandardMaterial({ color: 0xf97316, roughness: 0.5 });
    var cone = new THREE.Mesh(coneGeo, coneMat);
    cone.position.y = 0.64;
    cone.castShadow = true;
    coneGroup.add(cone);

    // Vạch phản quang trắng trên cọc
    var stripeGeo = new THREE.CylinderGeometry(0.22, 0.26, 0.3, 16);
    var stripeMat = new THREE.MeshBasicMaterial({ color: 0xffffff });
    var stripe = new THREE.Mesh(stripeGeo, stripeMat);
    stripe.position.y = 0.65;
    coneGroup.add(stripe);

    return coneGroup;
  };

  /**
   * Trụ sạc ô tô điện EV Charger Post
   */
  Parking3DViewer.prototype._createEVChargerPost = function () {
    var evGroup = new THREE.Group();
    var post = new THREE.Mesh(
      new THREE.BoxGeometry(0.4, 1.4, 0.3),
      new THREE.MeshStandardMaterial({ color: 0x059669, roughness: 0.4 })
    );
    post.position.y = 0.7;
    evGroup.add(post);

    var screen = new THREE.Mesh(
      new THREE.PlaneGeometry(0.24, 0.35),
      new THREE.MeshBasicMaterial({ color: 0x34d399 })
    );
    screen.position.set(0, 0.85, -0.16);
    screen.rotation.y = Math.PI;
    evGroup.add(screen);

    return evGroup;
  };

  /**
   * Viền nhấp nháy phát sáng cho ô "Đã phân nhưng xe chưa vào" (RESERVED)
   */
  Parking3DViewer.prototype._createPulsingFrame = function (width, length) {
    var shape = new THREE.Shape();
    var w2 = width / 2;
    var l2 = length / 2;
    shape.moveTo(-w2, -l2);
    shape.lineTo(w2, -l2);
    shape.lineTo(w2, l2);
    shape.lineTo(-w2, l2);
    shape.closePath();

    var points = shape.getPoints();
    var geo = new THREE.BufferGeometry().setFromPoints(points);
    var mat = new THREE.LineBasicMaterial({
      color: 0x06b6d4,
      linewidth: 3,
      transparent: true,
      opacity: 0.9
    });
    var line = new THREE.LineLoop(geo, mat);
    line.rotation.x = -Math.PI / 2;
    line.position.y = 0.04;
    return line;
  };

  /**
   * Beacon hiển thị phía trên ô RESERVED
   */
  Parking3DViewer.prototype._createReservedBeacon = function (plateText) {
    var beacon = new THREE.Group();

    // Hạt năng lượng phát sáng
    var glowGeo = new THREE.SphereGeometry(0.3, 16, 16);
    var glowMat = new THREE.MeshBasicMaterial({ color: 0x22d3ee, transparent: true, opacity: 0.8 });
    var glow = new THREE.Mesh(glowGeo, glowMat);
    beacon.add(glow);

    // Biển thông báo xe đang đến
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

  /**
   * Dấu gạch chéo cấm đỗ cho ô UNAVAILABLE
   */
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

  /**
   * Sprite mã ô đỗ xe trên mặt đất
   */
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

  /**
   * Sprite biển số xe
   */
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

  /**
   * Cập nhật số liệu thống kê trên thanh HUD
   */
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

  /**
   * Lọc ô đỗ xe theo trạng thái: ALL, EMPTY, OCCUPIED, RESERVED, MAINTENANCE
   */
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

  /**
   * Cập nhật trạng thái thời gian thực của 1 ô đỗ (Real-time Sync)
   */
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

    // Tái tạo lại mesh ô này
    var oldMesh = this.slotMeshes[slot.slotCode];
    if (oldMesh) {
      this.slotsGroup.remove(oldMesh);
    }
    // Lọc bỏ khỏi danh sách nhấp nháy cũ
    this.pulsingSlots = this.pulsingSlots.filter(function (p) { return p.slotCode !== slot.slotCode; });

    this._createSlotMesh(slot);
    this._updateStats();
  };

  /**
   * Đổi tầng bãi xe
   */
  Parking3DViewer.prototype.setFloor = function (floorId) {
    if (this.options.floorId === floorId) return;
    this.options.floorId = floorId;
    this.loadSlots(floorId);
    this.resetView();
  };

  /**
   * Reset View: Đưa camera về góc nhìn toàn cảnh ban đầu với animation mượt mà
   */
  Parking3DViewer.prototype.resetView = function () {
    var self = this;
    var startCam = { x: this.camera.position.x, y: this.camera.position.y, z: this.camera.position.z };
    var startTarget = { x: this.controls.target.x, y: this.controls.target.y, z: this.controls.target.z };

    var endCam = this.defaultCamPos;
    var endTarget = this.defaultTarget;

    var duration = 800; // ms
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

  /**
   * Chuyển đổi giữa góc nhìn 2D (từ trên nhìn thẳng xuống) và 3D Isometric
   */
  Parking3DViewer.prototype.toggleTopDownView = function () {
    if (this.isTopDown) {
      this.resetView();
    } else {
      this.isTopDown = true;
      var self = this;
      var startCam = { x: this.camera.position.x, y: this.camera.position.y, z: this.camera.position.z };
      var endCam = { x: 0, y: 55, z: 0.1 };

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

  /**
   * Lắng nghe sự kiện chuột, touch & resize
   */
  Parking3DViewer.prototype._bindEvents = function () {
    var self = this;
    var dom = this.renderer.domElement;

    // Hover
    dom.addEventListener('pointermove', function (e) {
      var rect = dom.getBoundingClientRect();
      self.mouse.x = ((e.clientX - rect.left) / rect.width) * 2 - 1;
      self.mouse.y = -((e.clientY - rect.top) / rect.height) * 2 + 1;
      self._checkHover(e.clientX, e.clientY);
    });

    // Click
    dom.addEventListener('pointerdown', function (e) {
      self._pointerDownPos = { x: e.clientX, y: e.clientY };
    });

    dom.addEventListener('pointerup', function (e) {
      // Chỉ kích hoạt click nếu không phải thao tác kéo xoay (drag)
      if (self._pointerDownPos) {
        var dist = Math.hypot(e.clientX - self._pointerDownPos.x, e.clientY - self._pointerDownPos.y);
        if (dist < 5 && self.hoveredSlot) {
          self._onSlotSelected(self.hoveredSlot.userData.slotData);
        }
      }
    });

    // Resize
    window.addEventListener('resize', function () {
      self._onResize();
    });
  };

  /**
   * Kiểm tra con trỏ chuột hover vào ô đỗ nào
   */
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

  /**
   * Hiển thị Tooltip HTML nổi tại vị trí con trỏ
   */
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

  Parking3DViewer.prototype._onSlotSelected = function (slotData) {
    if (this.options.onSlotClick) {
      this.options.onSlotClick(slotData);
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
   * Render loop: Cập nhật OrbitControls & hiệu ứng nhấp nháy cho ô RESERVED
   */
  Parking3DViewer.prototype._animate = function () {
    var self = this;
    this.animId = requestAnimationFrame(function () { self._animate(); });

    var delta = this.clock ? this.clock.getElapsedTime() : 0;

    // Hiệu ứng nhấp nháy viền (Pulsing glowing highlight) theo đúng yêu cầu nghiệp vụ:
    // "Render trạng thái 'Ô được phân nhưng xe chưa vào': Dấu hiệu riêng (nhấp nháy viền)"
    if (this.pulsingSlots.length > 0) {
      var pulse = 0.5 + 0.5 * Math.sin(delta * 4.5); // Dao động mềm từ 0 đến 1
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

    if (this.renderer && this.scene && this.camera) {
      this.renderer.render(this.scene, this.camera);
    }
  };

  /**
   * Hủy giải phóng bộ nhớ (tránh memory leak khi tháo gỡ component trong React / SPA)
   */
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
