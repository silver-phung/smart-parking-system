# Manager and desktop layout — Sprint 3

## Manager

Open `/app/manager` with a MANAGER account assigned to at least one lot.

- Overview selects an assigned lot and shows acceptance status, capacity, active sessions and sessions awaiting slot verification.
- Open/close acceptance requires a reason and confirmation. Closing a lot does not hide it from its assigned personnel; it remains selectable so the Manager can reopen it.
- `/app/manager?view=slots` lists active floors/slots with pagination. Manager can select EMPTY, MAINTENANCE or UNAVAILABLE and confirm with a reason. OCCUPIED and held EMPTY slots are disabled. The UI rereads the slot and all active sessions before writing, and the server continues to enforce authorization and business rules.
- `/app/manager?view=sessions` shows paginated active sessions in the selected lot and authorized session details. Switching lot or tab clears the previous detail and pending confirmation.
- Account creation and staff assignment stay in Admin. No 3D or payment module is introduced.

## Backend support

`my-lots` includes closed lots with an active assignment. Lot detail permits reading a closed lot when `HasLotAccessAsync` confirms the account's current access. Anonymous and unassigned accounts still receive 404 for a closed lot. The public lot list remains restricted to open lots. HTTP tests cover access before assignment, after assignment and after revocation.

## Navigation and layout

The header has a common two-row desktop grid: brand/account actions, then role-specific navigation. Current navigation reflects the query tab. Wrong-role routes return users to their own workspace; unknown routes display a missing-page state. Home anchors use router hash navigation instead of a timer.

The React application uses a desktop canvas with a minimum width of 1280px and common container maximum of 1440px, including 32px side gutters. Titles, panel padding, form controls, table columns and action rows share alignment rules in `src/desktop.css`. Width-based media queries have been removed from `index.css`, `staff.css` and `workspace.css`; the reduced-motion accessibility query remains. Narrow screens keep the desktop canvas and may scroll horizontally. Mobile layout work is deferred to a later sprint. Standalone legacy/3D pages are unchanged.

## Validation

- `npm --prefix frontend/smart-parking test`
- `npm --prefix frontend/smart-parking run build`
- `dotnet test backend/tests/SmartParking.Tests/SmartParking.Tests.csproj`

Frontend HTTP mocks cover close/reopen, maintenance, occupied-slot protection, permission failure drafts, lot switching, active navigation and role redirects. Backend tests use isolated InMemory data; they do not write to the team's database. Registration/OTP and Staff regression suites remain enabled.
