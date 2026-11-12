# Staff workflow database migrations

The monolith uses two EF Core contexts for the staff image workflow. Configure both
`ConnectionStrings__DefaultConnection` and `ConnectionStrings__AiRecognitionConnection`
in `.env`, then load them into the current PowerShell session from the repository root:

```powershell
. .\scripts\Load-Env.ps1
```

Apply the core parking schema and the AI recognition schema separately:

```powershell
dotnet ef database update `
  --project backend\src\SmartParking.Infrastructure `
  --startup-project backend\src\SmartParking.Api `
  --context SmartParkingDbContext

dotnet ef database update `
  --project backend\src\SmartParking.Infrastructure `
  --startup-project backend\src\SmartParking.Api `
  --context AiRecognitionDbContext
```

`SmartParkingDbContext` owns the short-lived `staff_vehicle_entries` table.
`AiRecognitionDbContext` owns recognition purpose, parking-lot, and matched-session
metadata. Each context tracks its own migration history. The staff image workflow needs
both databases migrated before it can run end to end.
