# Home motion and project transparency

- The two-row header is sticky on all React routes. Its measured height drives `scroll-padding-top` and the information sidebar offset, so anchor headings remain below it. A demo badge remains visible even on pages without a footer.
- Home sections reveal once as they enter the viewport using IntersectionObserver, with small card/button hover movement. Content stays visible without the observer API. Reduced-motion preferences disable reveal/hover movement and smooth anchor scrolling.
- A Home banner and the section below the dark footer explicitly describe an OJT learning/demo project, not a commercial parking service. Home no longer promises an available mobile app, physical card integration, reserved real parking or real payment.
- Public information pages at `/app/thong-tin/:topic` cover `du-an`, `dieu-kien`, `quyen-rieng-tu`, `gioi-han`, `chong-gia-mao`, and `lien-he`. Each includes the demo disclosure and navigation to the other pages.
- Privacy content distinguishes simulated parking operations from actual email/account/image storage. It mentions Neon, configured image/AI/email services, sessionStorage and the absence of a published automatic deletion period. No false assurance of anonymity or automatic deletion is given.
- Contact links point to the existing project repository and Issues page. No phone number, email or business address is fabricated. Visitors are told to redact personal information and never post OTPs, tokens, secrets or connection strings.
- No external messages are submitted by these links automatically. No checkbox/contract acceptance or payment flow is added. Existing desktop-only layout and the separate 3D module are preserved.

Validation: frontend suite covers all six footer destinations, unknown topics, observer visibility/cleanup, reduced motion and missing-observer fallback. Build regenerates the backend-served frontend bundle.
