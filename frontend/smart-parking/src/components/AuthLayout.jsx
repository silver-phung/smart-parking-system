export default function AuthLayout({ children }) {
  return (
    <section className="auth-page">
      <div className="auth-layout">
        <aside className="auth-aside">
          <span className="auth-aside__badge">
            <span aria-hidden="true">✦</span> SMART PARKING
          </span>
          <div className="auth-aside__copy">
            <p className="auth-aside__eyebrow">ĐỖ XE THÔNG MINH</p>
            <h2>Chỗ đỗ tốt, bắt đầu hành trình tốt.</h2>
            <p>
              Tìm chỗ trống nhanh chóng, đặt chỗ thuận tiện và an tâm trên mọi
              hành trình của bạn.
            </p>
          </div>
          <div className="parking-visual" aria-hidden="true">
            <div className="parking-visual__top">
              <span>ĐANG HOẠT ĐỘNG</span>
              <span className="parking-visual__live">
                <i /> TRỰC TUYẾN
              </span>
            </div>
            <div className="parking-visual__lot">
              <div className="parking-visual__space parking-visual__space--free">
                <span>P</span>
                <small>CÒN TRỐNG</small>
              </div>
              <div className="parking-visual__space">
                <span>P</span>
                <small>ĐÃ ĐẶT</small>
              </div>
              <div className="parking-visual__car">▰</div>
              <div className="parking-visual__space">
                <span>P</span>
                <small>ĐÃ ĐẶT</small>
              </div>
              <div className="parking-visual__space parking-visual__space--free">
                <span>P</span>
                <small>CÒN TRỐNG</small>
              </div>
            </div>
            <div className="parking-visual__bottom">
              <span>Chỗ đỗ xe gần bạn</span>
              <strong>Luôn sẵn sàng <span aria-hidden="true">↗</span></strong>
            </div>
          </div>
          <p className="auth-aside__footer">Di chuyển dễ dàng hơn, mỗi ngày.</p>
        </aside>
        <div className="auth-content">{children}</div>
      </div>
    </section>
  );
}
