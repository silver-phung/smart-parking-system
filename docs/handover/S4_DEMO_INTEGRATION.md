# S4 demo integration — 2026-11-11

The demo combines these source snapshots and retains their Git ancestry:

| Source branch | Commit | Main contribution |
| --- | --- | --- |
| `feat/duong/parking-verification` | `cbd343a` | Versioned policies, zones, tickets, scoped assignments, slot administration, authentication UI |
| `Song-S4` | `6be5413` | Online booking intake, customer booking dashboard, database-backed images, traffic statistics |
| `feat/Thuc/render-3d-parking` | `fae7208` | Enhanced Three.js parking renderer, floor circulation, signs, search and camera controls |

Integration starts from `origin/s4/demo` at `adbe657`. No source branch is overwritten.

## Conflict decisions

- Keep ticket/EventId intake for walk-in vehicles. It separates intake from actual plate/slot verification and preserves retry idempotency. Do not restore the old gate check-in form alongside it.
- Add Song's **Xe đặt qua web** screen to the Staff workspace. Booked arrivals use the dedicated `confirm-parked` API, rather than consuming another walk-in admission. Keep ticket lookup, slot verification, dispatch and exit views.
- Keep the Staff/Manager lot, floor and area authorization checks. Booking verification also checks the actual selected floor/area. Entry and exit duties remain distinct.
- Keep the policy version attached to the booking when creating its session. Expiration uses that version's late-arrival window; legacy bookings without a policy use the existing 30-minute rule. Expiration is evaluated on booking creation/capacity reads; this is not a scheduled background worker.
- Prefer database-backed image uploads so the new flow does not require Cloudinary. Preserve the SHA-256 content hash, image ownership checks and protection against deleting session evidence. Old Cloudinary URLs remain readable; deleting old remote assets still requires Cloudinary configuration.
- Move the image-content migration into `SmartParking.Persistence`, retaining its original migration ID. Combine its columns with the parking operations and assignment model; verify the resulting EF snapshot matches the runtime model.
- Publish both 3D pages with the API. The API viewer receives the selected lot/floor, reads every page of slots, and recognizes `RESERVED`. The enhanced renderer remains an explicitly labelled sample-data presentation.
- Rebuild the React bundle from combined source. Do not select a compiled bundle from one branch as the integration result.

## Functional inventory

| Area | Present in the merged code | Boundary / remaining work |
| --- | --- | --- |
| Authentication | Password login, refresh/logout, email registration/OTP, Google sign-in | Live SMTP and Google flows require deployment configuration and have not been exercised against real external accounts in this integration |
| Internal accounts | Admin creates Manager/Staff directly; role/status changes; audit and token/assignment revocation | Customer registration remains a separate OTP flow |
| Staff assignment | Admin/Manager scope, Staff search, lot/floor selection, entry gate / exit gate / area management; Zone required only for area management | Existing legacy assignments are preserved; revoke and recreate to change their duty |
| Parking structure | Lots, floors, normal/monthly zones; zone opening/closure | Monthly zones are not a complete subscription/card-management service |
| Policies | Draft/approved versions, effective times, booking and operation parameters | A configured lot needs an approved policy before admission/booking can proceed |
| Slot administration | Admin/Manager CRUD and EMPTY/OCCUPIED/RESERVED/MAINTENANCE; occupied/assigned/history guards | Manager is restricted to managed lots; historical slots cannot be hard-deleted |
| Walk-in operations | Ticket issue with EventId retries, queue, manual plate/slot verification, wrong-slot handling, ticket lookup/replacement, dispatch | Do not replace it with legacy check-in at a configured zoned lot |
| Online bookings | Customer creation/list/cancellation, held capacity, Staff notifications and actual-slot confirmation, no-show expiration | Booking reserves capacity rather than a specific physical slot |
| Exit | Plate comparison, ticket validation for ticketed sessions, payment-clearance check, completion and release | Manual payment clearance is present; this merge does not include the separate payment branch or a complete payment gateway/invoice flow |
| Customer views | Public capacity/slots, customer bookings, linked sessions, profile and vehicles; periodic refresh | Only sessions linked to that customer's account are returned; anonymous walk-in sessions are not automatically account-owned |
| Images | PostgreSQL bytes, authenticated content reads, old image URLs, hash and evidence protection | Database capacity grows with stored images |
| 3D API viewer | Actual slot status through public API, selected lot/floor, pagination, empty/error states | Uses Three.js/OrbitControls CDN; public slot reads do not expose private vehicle information |
| Enhanced 3D renderer | Underground/multilevel sample lots, circulation, elevator/camera controls, plate search and simulated roles | Sample vehicles/roles are not Neon records or authentication permissions; API synchronization for this renderer is still outstanding |
| AI | Existing FastAPI recognition code, Recognition API and Core client integration | No real inference/model-service end-to-end run was performed for this merge |
| IoT/events | Existing ingestion/simulator and compatibility endpoints | Real hardware and the full deployed event pipeline were not validated; the legacy `AlwaysCompatibleVehicleChecker` is still a stub |
| Reporting | Capacity, active sessions and monthly traffic statistics | Complete billing, invoices and revenue reporting are not implemented by these three branches |

## Architecture status

The repository builds a Core API, Recognition API, and YARP Gateway, alongside the existing Python AI service. The Core's code uses Application/Domain/Infrastructure/Persistence layers, but identity, Staff assignment, bookings and parking operations still share the Core application/database. This merge is not a completed split of every domain into an independent microservice. The Gateway's downstream destination must be configured before running it.

## Database deployment

All newly generated SQL is delivered in the containing workspace's single `neon/sql` folder:

1. `s4-demo-upgrade-existing-smartparking.sql`: upgrade the existing Sprint 3 `smartparking` Core database. Requires migration history through `20261029114209_AddDutyToStaffLotAssignment`; already-applied later migrations are skipped.
2. `s4-demo-core-full.sql`: full Core chain for an empty database or the same EF-managed chain. Do not use it to adopt unrelated pre-existing tables without migration history.
3. `s4-demo-verify.sql`: read-only readiness check for policies, zones, tickets, image content, assignment scope and RESERVED status.

Run the upgrade on the same Neon branch/database used by the Core API, then run the verification file and restart the API. Keep the existing accounts and data; the scripts do not create a replacement database. SQL migration sources are committed under Persistence; generated SQL delivery files are outside this Git repository in the workspace folder requested by the project owner.

No Neon database was modified as part of this merge.

## Validation

- Entire `backend/SmartParking.sln` builds, including Core API, Recognition API and Gateway.
- Backend: 451 passed, 3 skipped. The skipped tests require an explicitly configured dedicated PostgreSQL database (`PARKING_TEST_POSTGRES`) for concurrent-connection tests; Docker's database daemon was unavailable locally. No shared/Neon database was used for those tests.
- Frontend: 107 passed (21 Node tests plus 86 Vitest tests); production bundle rebuilt.
- Migration snapshot is checked against the current Npgsql model without connecting to a database.
- SQL tested with local PostgreSQL/PGlite for an empty database and a seeded Sprint 3 database. Full/upgrade scripts run twice; readiness checks pass, and existing account, slot, session and booking IDs are preserved.
- Viewer tests cover selected-floor pagination and API failure without fabricated slots; renderer JavaScript syntax checks pass.

## Entry points

- React application: `/app/`, `/app/login`, `/app/admin`, `/app/manager`, `/app/staff`, `/app/customer`, `/app/parking`.
- API viewer: `/3d-viewer.html?lotId=<id>&floorId=<id>`, also linked from the customer parking and slot-management screens.
- Sample renderer: `/3d-parking-render.html`.
- Core API documentation: `/swagger` in Development.

After upgrading the database, smoke-test one internal account, each Staff duty, one walk-in ticket/verification/paid exit, one customer booking/Staff confirmation/paid exit, protected slot edits and both 3D modes. SMTP/Google/AI/IoT live checks are separate from the automated merge checks above.
