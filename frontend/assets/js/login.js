/* SmartPark - Logic trang đăng nhập */
(function () {
  'use strict';
  var S = window.SP, $ = function (s) { return document.querySelector(s); };
  var mode = 'login';
  Array.prototype.forEach.call(document.querySelectorAll('[data-i]'), function (el) { el.innerHTML = S.icon(el.getAttribute('data-i'), 18); });
  S.initTheme($('#themeBtn'));

  /* Phiên đăng nhập còn hiệu lực: hiện lối tắt vào đúng cổng hoặc đổi tài khoản. */
  function renderResume() {
    var s = S.auth.session(), el = $('#resume');
    if (!s) { el.hidden = true; return; }
    el.hidden = false;
    el.innerHTML = '<div><b>Bạn đang đăng nhập là ' + S.esc(s.name) + '</b><div class="hint">' + S.esc(s.email) + ' · ' + S.auth.ROLE_LABEL[s.role] + '</div></div>' +
      '<div class="row wrap"><a class="btn primary sm" href="' + S.auth.HOME[s.role] + '">Vào ' + (s.role === 'admin' ? 'cổng quản trị' : 'ứng dụng khách') + '</a><button class="btn sm" type="button" id="switchAcc">Đăng nhập tài khoản khác</button></div>';
    $('#switchAcc').addEventListener('click', function () { S.auth.logout(); renderResume(); $('#email').focus(); });
  }

  function clearErr() {
    ['emailErr', 'passErr', 'nameErr', 'confirmPassErr'].forEach(function (i) { $('#' + i).textContent = ''; });
    ['email', 'pass', 'registerName', 'confirmPass'].forEach(function (i) { $('#' + i).removeAttribute('aria-invalid'); });
    $('#msg').hidden = true;
  }
  function setMode(next) {
    mode = next;
    var registering = mode === 'register';
    $('#registerFields').hidden = !registering;
    $('#registerName').required = registering;
    $('#confirmPass').required = registering;
    $('#pass').autocomplete = registering ? 'new-password' : 'current-password';
    $('#loginTitle').textContent = registering ? 'Đăng ký tài khoản khách' : 'Đăng nhập';
    $('#submitBtn').textContent = registering ? 'Tạo tài khoản' : 'Đăng nhập';
    $('#modeBtn').textContent = registering ? 'Đã có tài khoản? Đăng nhập' : 'Chưa có tài khoản? Đăng ký';
    $('#msg').hidden = true;
    clearErr();
  }
  function fail(msg) {
    var m = $('#msg'); m.textContent = msg; m.hidden = false;
    var c = document.querySelector('.login'); c.classList.remove('shake'); void c.offsetWidth; c.classList.add('shake');
  }

  $('#modeBtn').addEventListener('click', function () {
    setMode(mode === 'login' ? 'register' : 'login');
  });
  $('#togglePw').addEventListener('click', function () { var p = $('#pass'), s = p.type === 'password'; p.type = s ? 'text' : 'password'; this.textContent = s ? 'Ẩn' : 'Hiện'; });

  /* Kiểm tra dữ liệu, xác thực tài khoản demo, sau đó chuyển tới Client hoặc Admin. */
  $('#form').addEventListener('submit', function (e) {
    e.preventDefault(); clearErr();
    var email = $('#email').value.trim(), pass = $('#pass').value, ok = true;
    if (mode === 'register') {
      var name = $('#registerName').value.trim(), confirmPass = $('#confirmPass').value;
      if (!name) { $('#nameErr').textContent = 'Nhập họ và tên.'; $('#registerName').setAttribute('aria-invalid', 'true'); ok = false; }
      if (!confirmPass) { $('#confirmPassErr').textContent = 'Nhập lại mật khẩu.'; $('#confirmPass').setAttribute('aria-invalid', 'true'); ok = false; }
      else if (pass !== confirmPass) { $('#confirmPassErr').textContent = 'Mật khẩu nhập lại chưa khớp.'; $('#confirmPass').setAttribute('aria-invalid', 'true'); ok = false; }
    }
    if (!email) { $('#emailErr').textContent = 'Nhập email của bạn.'; $('#email').setAttribute('aria-invalid', 'true'); ok = false; }
    else if (!/^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/.test(email)) { $('#emailErr').textContent = 'Email chưa đúng định dạng, ví dụ ten@example.com.'; $('#email').setAttribute('aria-invalid', 'true'); ok = false; }
    if (!pass) { $('#passErr').textContent = 'Nhập mật khẩu.'; $('#pass').setAttribute('aria-invalid', 'true'); ok = false; }
    else if (mode === 'register' && pass.length < 8) { $('#passErr').textContent = 'Mật khẩu phải có ít nhất 8 ký tự.'; $('#pass').setAttribute('aria-invalid', 'true'); ok = false; }
    if (!ok) return;
    if (mode === 'register') {
      var result = S.auth.register(name, email, pass);
      if (result.error) { fail(result.error); return; }
      var registered = S.auth.login(result.account.email, result.account.pass);
      if (!registered) { fail('Tạo tài khoản thành công nhưng không thể tạo phiên đăng nhập. Hãy đăng nhập lại.'); setMode('login'); $('#email').value = result.account.email; return; }
      $('#submitBtn').disabled = true; $('#submitBtn').textContent = 'Đang vào...';
      location.href = S.auth.HOME[registered.role];
      return;
    }
    var s = S.auth.login(email, pass);
    if (!s) { fail('Email hoặc mật khẩu chưa đúng. Hãy kiểm tra lại thông tin đăng nhập.'); return; }
    $('#submitBtn').disabled = true; $('#submitBtn').textContent = 'Đang vào...';
    location.href = S.auth.HOME[s.role];
  });

  renderResume();
})();
