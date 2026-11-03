# projectmem - WMMS

_Last updated: 2026-11-03_

## Project purpose
Replace this placeholder with a concise description of what this project does, who it serves, and the main technologies or runtime assumptions.

## Recent issues
- [DONE] #0001 OrderIntakeService mixes Excel parsing with business logic and misparses sampleOrder.xlsx (overwrites SKU col with HS Code col 18, misses footer ETD row 13, and reads footer summary rows as product items). [be/WMMS_BE/src/WMMS.Infrastructure/Excel/OrderIntakeService.cs:934] -> Extracted IExcelOrderParser and ExcelOrderParser from OrderIntakeService to cleanly separate 4 blocks of Customer PO Excel files (Header PO#, Production Line Items, BR-ORD-01 Stripped Commercial Columns, and Footer Summary/ETD) and registered IExcelOrderParser in DI. (fixed)

## Decisions
- Adopt 4-project Clean Layered Architecture in WMMS_BE.sln (WMMS.Domain, WMMS.Application, WMMS.Infrastructure, WMMS.API) mapped 1-to-1 with the 12 packages and 6 core business modules (AUT, ORD, BOM, INV, WHS, KBN) of Report 4 SDS. [be/WMMS_BE/ARCHITECTURE.md]
- Keep C# entity class names aligned with Report 4 Class Diagrams (CustomerOrder, BomHeader, MaterialStock, PurchaseRequest) and map them via EF Core Fluent API ToTable() to Report 4 Physical ERD snake_case tables (sales_orders, boms, inventory_stocks, consolidated_prs). [be/WMMS_BE/src/WMMS.Infrastructure/Persistence/Configurations/]
- Implement complete Code-First DB layer with EF Core 8 and SQL Server, 31 domain entities, snake_case table mappings, optimistic concurrency rowversion, InitialCreate migration, and DbInitializer seed data.
- Enforce dedicated branch workflow on branch luong (no direct push to main), documentation sync with WMMS_docs, and mandatory dual AI usage logging in WMMS_AI_Usage_Report.xlsx and AI_Usage_Evidence.docx.
- Implement full Authentication, Authorization & User Management (AUT & ADM) module (UC-01 to UC-14, JOB-01) with JWT + SHA-256 Refresh Token rotation, 5-attempt 30-min lockout, 6-digit OTP + token password reset (15-min TTL, 3/10min rate limit), Multi-Role RBAC user CRUD, role permission matrix config, and immutable system audit log query APIs.
- Reverted AGENTS.md and CLAUDE.md in repository to remove personal branch and logging rules to prevent team conflicts on git. Enforced workflow rules locally in Antigravity session only: User works strictly on branch luong (local machine only, not in shared repo docs), never pushes to main, requires diff checking and user approval before any merge.
- Enforce Testing Protocol: All API/Unit/Integration tests are isolated exclusively in D:\WMMS_TEST outside the repository, never committed to git/branches. Test files are modular per feature, require full sample seed data preparation before running, must cover complete test ranges, have clear UI/log reporting (API tested, feature tested), must be updated whenever code/logic changes, and are only executed when explicitly ordered by the user.
- Enforce Merge Notification Protocol: Whenever merging with any teammate branch (e.g. duy, duong), must inspect diffs, explain reasons for changes, and report to the user for explicit approval before executing merge.
- Enforce Dev-Branch Integration Workflow: The dev branch is now the shared staging branch where all members integrate code. Before starting any new coding or committing on branch luong, must git fetch origin and pull/rebase latest updates from origin/dev (instead of main) to ensure full synchronization with all members. Main is strictly reserved for production releases when ordered by the user.
- Merged origin/dev into local branch luong (commit 626bd8b) bringing Order Intake, BOM Routing, Repository Pattern, and React Frontend into luong. Retained AGENTS.md for projectmem compatibility. Successfully verified dotnet build with 0 errors, and pushed to origin/luong.
- Enforce Frontend Error Handling Protocol: Khi kiem tra ma nguon neu phat hien loi Frontend (FE) thi tuyet doi KHONG duoc tu y sua code; chi ghi nhan, phan tich nguyen nhan va bao cao chi tiet danh sach loi cho nguoi dung.
- Adopt Hybrid 2-Stage Product Image Architecture for Excel PO Intake: Stage 1 (Staging Preview) extracts embedded OpenXML ExcelPicture drawings by row into Base64 Data URIs (ProductImageUrl / TechnicalDrawingUrl) for immediate UI rendering without disk garbage; Stage 2 (Order Confirm) decodes and persists confirmed images to wwwroot/uploads/products/{sku}.{ext}, stores static URLs in Product.ImageUrl, and serves them via UseStaticFiles(). [be/WMMS_BE/src/WMMS.Infrastructure/Excel/ExcelOrderParser.cs]
- Implement industrial MES Session vs Persistent Cookie architecture in AuthController & AuthService: when RememberMe=false (default for shop-floor/kiosk), issue Session Cookie (Expires=null) that auto-destroys on browser close; when RememberMe=true, issue 30-day Persistent Cookie; preserve RememberMe state across RefreshToken rotation and allow anonymous cookie-based logout cleanup.

## Notes
- Merge: docs(projectmem): update summary
- Generated comprehensive Dual Demo Scripts document (WMMS_Iteration1_Demo_Scripts.docx) covering both Mentor/Instructor business flow perspective and MES Technical Expert deep-dive architecture (OpenXML Excel parsing, Base64 drawing extraction, UoM volumetric conversion, 3-tier BOM, dynamic routing with Critical Hold Point, and Session vs Persistent Cookie authentication).
- Merge: Merge remote-tracking branch 'origin/hunganh' into luong
- Merged origin/hunganh commit 545dab6 (feat(fe/inventory): add 3-tier stock grid and electronic stock ledger) into branch luong (commit 6b511d3) with 0 conflicts. Verified dotnet build 0 errors, and synced to origin/luong and origin/dev.
- Merge: docs(projectmem): sync merge note for hunganh inventory
- Updated WMMS_Iteration1_Demo_Scripts.docx to version 1.1: synchronized 100% of demo accounts with Database seed data (operational factory staff accounts: admin, owner, sales01, sales02, planner01, purchasing01, warehouse01, leader01, qc01 and 5 SEP490_G122 team multi-role accounts: luonghd, duynd, duongnt, cuonghd, anhth, all with password Admin@123).
- Merge: docs(projectmem): sync update for demo script db accounts
- Updated WMMS_Iteration1_Demo_Scripts.docx to version 1.2: added explicit 'Mật khẩu' (Password) column with value Admin@123 for all rows in both Seed Data tables (Operational Staff and Team Member Multi-Roles).
- Merge: docs(projectmem): sync password column note
- Created comprehensive Business Requirements Document (BRD) at WMMS_BRD_Business_Requirements_Document.docx containing 8 detailed chapters: Project Vision & As-Is/To-Be, User Personas & RACI Matrix, Core Business Rules (BR-ORD-01/02, BR-BOM-01/02, BR-INV-01/02, BR-PUR-01, BR-PRD-01/02, BR-SEC-01), 6 End-to-End Business Process Flows, Non-Functional Requirements, State Machine, QC Defect Catalog, and Acceptance Criteria.

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
- `AGENTS.md`

## Open questions
- None logged yet.
