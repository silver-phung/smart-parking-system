# Admin accounts and Customer sessions

## Scope

Parts 2 and 3 of the UI plan are covered here. The 3D viewer is owned by another member and excluded. Existing standalone 3D assets and services are unchanged; no React viewer route or component is added.

## Admin

- `/app/admin/people`: search/paginate accounts, filter role/status, create STAFF or MANAGER accounts, and view details. Internal accounts are approved and email-verified by the existing backend.
- Change role or enable/disable with explicit confirmation and reason. Server rules enforce self-demotion and last-admin restrictions.
- `/app/admin/people?view=assignments`: select active personnel and lot; choose GATE, SLOT_VERIFIER or BOTH and add notes. Manager uses BOTH per server contract. Selectors load all result pages.
- Assignment history supports pagination and lot/status filters. Revoke with confirmation and reason. To change duty, revoke and create again; there is no update-duty endpoint.
- Duplicate writes are prevented. Failed drafts are retained; successful creation clears the password field.

## Public parking and Customer

- `/app/parking` requires no login, sends no bearer token, and displays active lots/floors and flat slot cards. Lot/slot pagination is collected. Refresh reloads all three levels; unavailable EMPTY slots are labeled unavailable.
- `/app/customer/sessions` requires USER and uses `parking-sessions/my-sessions`, with pagination and status/check-in date filters. Local calendar days are converted to UTC.
- Detail shows plate, lot, slot, entry/exit times, recorded fee/payment time and available images. Server authorization enforces ownership.
- A session belongs to a customer through a vehicle registered to that account. A walk-in without this relationship will not appear. Customers manage vehicles through their profile.
- Registration/OTP remains at `/app/register`. Real Gmail delivery requires the existing SMTP configuration; UI tests send no real emails.
- No payment, reservation, check-in/out or 3D implementation is added to Customer.

## Validation

Run `npm --prefix frontend/smart-parking test` and `npm --prefix frontend/smart-parking run build`.
HTTP mocks cover internal account creation, permission errors, confirmation before role/revoke writes, explicit duty assignment, Customer session ownership endpoints, date validation, public token omission/refresh and registration/OTP requests.
