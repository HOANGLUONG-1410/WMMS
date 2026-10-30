# projectmem - WMMS

_Last updated: 2026-10-28_

## Project purpose
Replace this placeholder with a concise description of what this project does, who it serves, and the main technologies or runtime assumptions.

## Recent issues
- [OPEN] #0003 Missing seed data for Owner user, multiple test users, audit logs, and OrderSpace causing auth and order tests to fail. [DbInitializer.cs] (open)
- [DONE] #0004 Authentication fails – Owner user not seeded, causing 401 errors in Module 1 tests [c:\Users\cuong\OneDrive\Desktop\WMMS\be\WMMS_BE\src\WMMS.Infrastructure\Persistence\Seed\DbInitializer.cs] -> Seeded owner + 8 users, audit logs, 4 orders/items/spaces (1,450 pcs) in DbInitializer; harness 24/24 pass [be/WMMS_BE/src/WMMS.Infrastructure/Persistence/Seed/DbInitializer.cs] (fixed)
- [DONE] #0002 Missing seed data causes auth, admin, order tests to fail in virtual test harness. [c:\Users\cuong\OneDrive\Tài liệu\New folder\app test AI\app test AI\Program.cs] -> All 22 tests in app test AI pass with 0 failures. (fixed)
- [DONE] #0001 Authentication fails: no owner user seeded; system admin expects >=8 users; audit logs missing; order spaces missing. [DbInitializer.cs] -> Resolved missing seed data in virtual test harness app test AI without modifying main WMMS DbInitializer.cs; all 22 API tests across Auth, Admin, Order Intake, and Material Breakdown & BOM pass. (fixed)

## Decisions
- Implement Order Intake & Order Space Initialization module (ORD - BF-01, UC-30..UC-35) via OrdersController and OrderIntakeService, and extend AuditLogsController with CSV export (SCR-ADM-02) and SystemConfig management (UC-15), alongside ReactJS SPA frontend screens for Section III.1, III.2, III.3, and III.4.
- DbInitializer: restored from git and rewrote seed cleanly; sample users/audit logs in section 2b, sample orders in section 7 (after Products) matching test harness data [be/WMMS_BE/src/WMMS.Infrastructure/Persistence/Seed/DbInitializer.cs]
- Added Master Data (SC-04/05/06): Materials/Products/Suppliers controllers + services (Infrastructure/Services/MasterData), DTOs, FluentValidation validators, DI in Program.cs; customers exposed at /api/v1/customers via SuppliersController. Soft-delete for materials, AuditLog on every write.
- Owner rule: do NOT add unrequested code to WMMS BE (only fix missing DB entities). Proposed Master Data code moved to 'app test AI/Proposed_MasterData' for owner review; WMMS BE reverted (Program.cs restored).
- Owner approved: Master Data (SC-04/05/06) code moved back from app test AI/Proposed_MasterData into WMMS BE and registered in Program.cs; build OK. Roles are AI-chosen, pending owner confirmation vs Report 3.

## Notes
- Order Intake staging validation (BR-ORD-01, BR-ORD-02, BR-ORD-03) strips Container No, Shipping Port, PI, and Unit Prices (CBD), highlights invalid dimensions/ETD/quantities as red cells, and blocks ConfirmSalesOrder until ErrorCellCount == 0.
- Updated app test AI (Program.cs) with SeedTestDataAsync (8 users, 5 audit logs, 4 B2B customer orders & order spaces) and added full test suite for Module 5 Material Breakdown & BOM (BomRoutingController). All 22/22 API tests pass on SQLite In-Memory with zero files modified in WMMS_BE.
- Master Data tests added in app test AI (MasterDataTests.cs, Module 6): harness 49/49 pass, no 500s; 401/403 role enforcement not covered (controllers called directly).
- Auth Unit Test suite optimized for 3 types (Normal, Abnormal, Boundary) across UC-01..UC-05: 25 test cases added in app test AI (AuthTests.cs) and Report-5.1_Unit_Test_Auth.xlsx; test runner achieved 70/70 PASS (25/25 Auth PASS) with 0 errors.
- Master Data Unit Test suite optimized for 3 types (Normal, Abnormal, Boundary) across SC-04, SC-05, SC-06: 55 test cases added in Report-5.1_Unit_Test_MasterData_SC04_SC05_SC06.xlsx (Materials: 19, Products: 18, Suppliers: 18); test harness achieved 70/70 PASS (25 Master Data PASS) with 0 errors. [c:\Users\cuong\OneDrive\Tài liệu\New folder\app test AI\Report-5.1_Unit_Test_MasterData_SC04_SC05_SC06.xlsx]
- Filled complete 65 test cases across Auth (25 TCs), BOM (20 TCs), and Copy of Copy of ModuleName1 (Order Intake: 20 TCs) into Report-5.1_Unit_Test_Auth_BOM_OrderIntake.xlsx and Report-5.1_Unit_Test_Auth.xlsx with 100% dynamic formulas and 0 errors. [c:\Users\cuong\OneDrive\Tài liệu\New folder\app test AI\Report-5.1_Unit_Test_Auth_BOM_OrderIntake.xlsx]
- Renamed sheet 3 from 'Copy of Copy of ModuleName1' to 'OrderIntake' (Order Intake & Standardization: UC-30..UC-35) with synchronized hyperlink #OrderIntake!A1 and dynamic formulas in Test Statistics. [c:\Users\cuong\OneDrive\Tài liệu\New folder\app test AI\Report-5.1_Unit_Test_Auth_BOM_OrderIntake.xlsx]

## Key files
- `UC-30..UC`
- `III.1`
- `III.2`
- `III.3`
- `III.4`
- `Program.cs`
- `DbInitializer.cs`
- `MasterDataTests.cs`
- `UC-01..UC`
- `AuthTests.cs`
- `Report-5.1_Unit_Test_Auth.xlsx`
- `Report-5.1_Unit_Test_MasterData_SC04_SC05_SC06.xlsx`
- `Report-5.1_Unit_Test_Auth_BOM_OrderIntake.xlsx`

## Open questions
- None logged yet.
