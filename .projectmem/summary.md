# projectmem - WMMS

_Last updated: 2026-10-21_

## Project purpose
Replace this placeholder with a concise description of what this project does, who it serves, and the main technologies or runtime assumptions.

## Recent issues
- [DONE] #0002 Missing seed data causes auth, admin, order tests to fail in virtual test harness. [c:\Users\cuong\OneDrive\Tài liệu\New folder\app test AI\app test AI\Program.cs] -> All 22 tests in app test AI pass with 0 failures. (fixed)
- [DONE] #0001 Authentication fails: no owner user seeded; system admin expects >=8 users; audit logs missing; order spaces missing. [DbInitializer.cs] -> Resolved missing seed data in virtual test harness app test AI without modifying main WMMS DbInitializer.cs; all 22 API tests across Auth, Admin, Order Intake, and Material Breakdown & BOM pass. (fixed)

## Decisions
- Implement Order Intake & Order Space Initialization module (ORD - BF-01, UC-30..UC-35) via OrdersController and OrderIntakeService, and extend AuditLogsController with CSV export (SCR-ADM-02) and SystemConfig management (UC-15), alongside ReactJS SPA frontend screens for Section III.1, III.2, III.3, and III.4.

## Notes
- Order Intake staging validation (BR-ORD-01, BR-ORD-02, BR-ORD-03) strips Container No, Shipping Port, PI, and Unit Prices (CBD), highlights invalid dimensions/ETD/quantities as red cells, and blocks ConfirmSalesOrder until ErrorCellCount == 0.
- Updated app test AI (Program.cs) with SeedTestDataAsync (8 users, 5 audit logs, 4 B2B customer orders & order spaces) and added full test suite for Module 5 Material Breakdown & BOM (BomRoutingController). All 22/22 API tests pass on SQLite In-Memory with zero files modified in WMMS_BE.

## Key files
- `UC-30..UC`
- `III.1`
- `III.2`
- `III.3`
- `III.4`
- `Program.cs`
- `DbInitializer.cs`

## Open questions
- None logged yet.
