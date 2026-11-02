# projectmem - WMMS

_Last updated: 2026-11-02_

## Project purpose
Replace this placeholder with a concise description of what this project does, who it serves, and the main technologies or runtime assumptions.

## Recent issues
- [DONE] #0001 Add unit test file for InventoryController APIs -> Removed unauthorized be/WMMS_BE.Tests directory from main codebase; unit tests are strictly isolated in external 'app test AI' harness. [be/WMMS_BE.Tests] (fixed)

## Decisions
- Do not create unit test projects (e.g. WMMS_BE.Tests or InventoryControllerTests.cs) inside the main WMMS repository; keep all unit tests isolated in the external 'app test AI' project. [be/]

## Notes
- Started adding unit tests for InventoryController APIs (SC-12, SC-13).
- Creating test project for InventoryController APIs (SC-12, SC-13).
- Completed InventoryController, InventoryService, IInventoryService, and InventoryDtos (SC-12, SC-13) matching 14 initial stock & ledger records from WMMS_DB_Test_Iteration1.sql. All 81 automated tests (including 11 Inventory tests UT-INV-01..UT-INV-11) passed with 0 failures.

## Key files
- `WMMS_DB_Test_Iteration1.sql`
- `UT-INV-01..UT`
- `be/WMMS_BE.Tests`
- `e.g`
- `WMMS_BE.Tests`
- `InventoryControllerTests.cs`

## Open questions
- None logged yet.
