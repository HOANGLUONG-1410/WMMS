# projectmem - WMMS

_Last updated: 2026-10-18_

## Project purpose
Replace this placeholder with a concise description of what this project does, who it serves, and the main technologies or runtime assumptions.

## Recent issues
- No issues logged yet.

## Decisions
- Adopt 4-project Clean Layered Architecture in WMMS_BE.sln (WMMS.Domain, WMMS.Application, WMMS.Infrastructure, WMMS.API) mapped 1-to-1 with the 12 packages and 6 core business modules (AUT, ORD, BOM, INV, WHS, KBN) of Report 4 SDS. [be/WMMS_BE/ARCHITECTURE.md]
- Keep C# entity class names aligned with Report 4 Class Diagrams (CustomerOrder, BomHeader, MaterialStock, PurchaseRequest) and map them via EF Core Fluent API ToTable() to Report 4 Physical ERD snake_case tables (sales_orders, boms, inventory_stocks, consolidated_prs). [be/WMMS_BE/src/WMMS.Infrastructure/Persistence/Configurations/]

## Notes
- gotcha: Never reconcile BOM demand against raw PhysicalQty; always enforce AvailableQty = max(0, PhysicalQty - AllocatedQty) with SQL Server rowversion optimistic concurrency on inventory_stocks, and enforce 100% QC Pass before advancing Kanban cards (or 97%-3% branching / Overdrive HoldPointStageSeq lock). [be/WMMS_BE/src/WMMS.Application/Services/]
- Updated root .gitignore to ignore Visual Studio .vs, bin/obj, node_modules, and OS caches while preserving projectmem memory rules. [.gitignore]

## Key files
- `WMMS_BE.sln`
- `WMMS.Domain`
- `WMMS.Application`
- `WMMS.Infrastructure`
- `WMMS.API`

## Open questions
- None logged yet.
