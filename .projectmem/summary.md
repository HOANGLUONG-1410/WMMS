# projectmem - WMMS

_Last updated: 2026-10-20_

## Project purpose
Replace this placeholder with a concise description of what this project does, who it serves, and the main technologies or runtime assumptions.

## Recent issues
- No issues logged yet.

## Decisions
- Adopt 4-project Clean Layered Architecture in WMMS_BE.sln (WMMS.Domain, WMMS.Application, WMMS.Infrastructure, WMMS.API) mapped 1-to-1 with the 12 packages and 6 core business modules (AUT, ORD, BOM, INV, WHS, KBN) of Report 4 SDS. [be/WMMS_BE/ARCHITECTURE.md]
- Keep C# entity class names aligned with Report 4 Class Diagrams (CustomerOrder, BomHeader, MaterialStock, PurchaseRequest) and map them via EF Core Fluent API ToTable() to Report 4 Physical ERD snake_case tables (sales_orders, boms, inventory_stocks, consolidated_prs). [be/WMMS_BE/src/WMMS.Infrastructure/Persistence/Configurations/]
- Implement complete Code-First DB layer with EF Core 8 and SQL Server, 31 domain entities, snake_case table mappings, optimistic concurrency rowversion, InitialCreate migration, and DbInitializer seed data.
- Enforce dedicated branch workflow on branch luong (no direct push to main), documentation sync with WMMS_docs, and mandatory dual AI usage logging in WMMS_AI_Usage_Report.xlsx and AI_Usage_Evidence.docx.

## Notes
- gotcha: Never reconcile BOM demand against raw PhysicalQty; always enforce AvailableQty = max(0, PhysicalQty - AllocatedQty) with SQL Server rowversion optimistic concurrency on inventory_stocks, and enforce 100% QC Pass before advancing Kanban cards (or 97%-3% branching / Overdrive HoldPointStageSeq lock). [be/WMMS_BE/src/WMMS.Application/Services/]
- Updated root .gitignore to ignore Visual Studio .vs, bin/obj, node_modules, and OS caches while preserving projectmem memory rules. [.gitignore]
- Merge: chore: merge remote initial commit and keep monorepo .gitignore
- New feature: feat: initialize fe workspace with Report 4 ReactJS package skeleton [fe/README.md]
- Database successfully updated and verified on local SQL Server WMMS_DB with InitialCreate migration and automatic seed execution on startup.
- Serena installed at D:\Tools\serena with Python 3.12, registered in Antigravity mcp_config.json for WMMS, and pjm doctor verified healthy.
- Spec Kit initialized for WMMS with Gemini integration, PowerShell scripts, and 10 speckit slash commands.

## Key files
- `WMMS_BE.sln`
- `WMMS.Domain`
- `WMMS.Application`
- `WMMS.Infrastructure`
- `WMMS.API`
- `fe/README.md`
- `fe/src/assets/.gitkeep`
- `fe/src/common/.gitkeep`
- `fe/src/components/.gitkeep`
- `fe/src/constant/.gitkeep`
- `fe/src/context/.gitkeep`
- `fe/src/pages/.gitkeep`
- `fe/src/routes/.gitkeep`
- `fe/src/services/.gitkeep`
- `fe/src/utils/.gitkeep`
- `3.12`
- `mcp_config.json`
- `WMMS_AI_Usage_Report.xlsx`
- `AI_Usage_Evidence.docx`

## Open questions
- None logged yet.
