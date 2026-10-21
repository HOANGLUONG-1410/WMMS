# CLAUDE.md

<!-- >>> projectmem bridge >>> -->
## projectmem (MANDATORY)

This project uses projectmem for persistent memory + workflow rules.

This project is registered with projectmem as **wmms**.
Pass `project="wmms"` on any projectmem tool call.

SESSION START — call these three MCP tools, in this order, BEFORE
answering ANY question about this project:

  1. `get_instructions()` — loads the project's mandatory workflow
     rules. Without this you will not know how to log work
     correctly, when to use `add_note` vs `add_decision`, or how
     the event log is structured.
  2. `get_summary()` — loads project content. Do NOT answer from
     conversation history or by re-reading package.json / README /
     source files.
  3. `get_project_map()` — loads structural layout when relevant.

BEFORE modifying ANY file:
  - Call `precheck_file(file_path)` — check failure history first.

DURING work — use MCP write tools, NEVER edit `.projectmem/`
files directly via filesystem write:
  - On a bug discovery → `log_issue(summary, location)`.
  - After each fix attempt → `record_attempt(summary, outcome)`.
  - After confirmation → `record_fix(summary)`.
  - On a design choice → `add_decision(summary)`.
  - On a gotcha / setup detail → `add_note(summary)`.
  - When a new decision REPLACES an older one → pass
    `supersedes="<old event id>"` to `add_decision`. The log stays
    append-only; the old decision is tagged retired and drops out of
    summary.md, so the summary never shows two answers to the same
    question. Get ids from `get_summary()` or `search_events(query)`.

Editing `.projectmem/summary.md` or `.projectmem/PROJECT_MAP.md`
directly bypasses event logging and breaks audit replay. The
summary file regenerates from `events.jsonl` automatically — write
via the MCP tools and the summary will follow.

Do not re-scan source files when MCP tools can give you the same
answer in a few hundred tokens instead of thousands. This is not optional.
<!-- <<< projectmem bridge <<< -->

<!-- >>> ai usage logging bridge >>> -->
## AI Usage Logging Protocol (MANDATORY)

Mỗi khi thực hiện coding hoặc thay đổi quan trọng trong dự án WMMS:
1. **Ghi log vào file Excel**:
   - Đường dẫn: `D:\Các kỳ học\FALL2026\Final_Project\Tài liệu\Tài Liệu\WMMS_docs\WMMS_AI_Usage_Report.xlsx`
   - Cột `Evidence / Link`: Ghi rõ tham chiếu đến file Word `AI_Usage_Evidence.docx` kèm mục tương ứng (ví dụ: `AI_Usage_Evidence.docx - Entry #...`).
2. **Ghi nội dung chi tiết Evidence vào file Word**:
   - Đường dẫn: `D:\Các kỳ học\FALL2026\Final_Project\Tài liệu\Tài Liệu\WMMS_docs\AI_Usage_Evidence.docx`
   - Cấu trúc mỗi mục:
     + Entry / Session ID
     + Thời gian (Timestamp: YYYY-MM-DD HH:mm:ss)
     + Câu lệnh Prompt của User
     + Kết quả trả về (Tóm tắt nhưng đầy đủ các thay đổi, file tạo/sửa, migration, kết quả kiểm thử/build).
3. **Thực hiện xuyên suốt**: Luôn cập nhật cả hai file trên mỗi khi hoàn thành một tác vụ coding.
<!-- <<< ai usage logging bridge <<< -->

<!-- >>> git workflow bridge >>> -->
## Git & Branching Workflow Protocol (MANDATORY)

1. **Quy tắc phân nhánh (Branching Rule)**:
   - Mỗi thành viên làm việc trên một nhánh riêng (`luong`, `duy`, `duong`,...).
   - **Nhánh làm việc của User là `luong`**. Mọi thao tác lập trình, kiểm thử, commit và push code PHẢI thực hiện trên nhánh `luong`.
   - **Tuyệt đối KHÔNG làm việc trực tiếp hoặc push lên nhánh `main`**. Chỉ push/merge lên `main` khi có chỉ đạo rõ ràng từ User ("khi đó sẽ có lệnh up lên main").
2. **Quy trình trước khi code & commit/push**:
   - Trước mỗi lần code mới hoặc commit/push:
     + Kiểm tra trạng thái toàn bộ nhánh: `git fetch origin`, `git branch -a`.
     + Pull cập nhật mới nhất về nhánh làm việc: `git pull origin luong` (hoặc đồng bộ rebase/merge từ `main` nếu `main` có cập nhật từ team).
   - Kiểm tra `git status` và `git diff` trước khi commit, đảm bảo không commit file thừa, bí mật bảo mật (.env), hoặc file build/cache.
3. **Chuẩn hóa Commit & Code Quality**:
   - Tuân thủ quy chuẩn Conventional Commits: `feat:`, `fix:`, `refactor:`, `test:`, `docs:`, `chore:`.
   - Đảm bảo dự án build thành công (`dotnet build` 0 errors) trước khi tạo commit và push.
<!-- <<< git workflow bridge <<< -->

<!-- >>> docs synchronization bridge >>> -->
## Documentation & Specification Protocol (MANDATORY)

1. **Thư mục tài liệu gốc**:
   - Đường dẫn: `D:\Các kỳ học\FALL2026\Final_Project\Tài liệu\Tài Liệu\WMMS_docs`
   - Chứa: `WMMS_Specification.docx`, `Report-3.docx`, `Report4.docx`, `Bảng chi tiết các quy trình nghiệp vụ.xlsx`, `sampleOrder.xlsx`, `sampleBOM.xls`.
2. **Kiểm tra trước khi code**:
   - Mỗi lần thực hiện tính năng hoặc nghiệp vụ, bắt buộc đọc và rà soát lại tài liệu trong thư mục trên để cập nhật các yêu cầu/luồng mới nhất mà User đã chỉnh sửa.
3. **Đồng bộ hóa Code & Docs**:
   - Nếu trong quá trình phát triển có chức năng phát sinh hoặc logic thực tế chưa có trong docs, cần chủ động cập nhật/sửa đổi tài liệu trong `WMMS_docs` để đặc tả và mã nguồn luôn thống nhất 100%.
<!-- <<< docs synchronization bridge <<< -->

<!-- >>> code documentation and migration bridge >>> -->
## Code Documentation & EF Core Code-First Migration Protocol (MANDATORY)

1. **Ghi chú & Đặc tả trong Code (Code Comments & XML Documentation)**:
   - Mỗi khi viết hoặc sửa code (Entities, DTOs, Interfaces, Services, Controllers, Validators, Middlewares), **bắt buộc** phải bổ sung ghi chú XML (`/// <summary>`) và comment giải thích rõ nghiệp vụ, mã Use Case (`UC-01`, `UC-02`,...), và mã Business Rule (`GBR-04`, `BR-SEC-01`, `BR-ADM-01`,...) để các thành viên khác trong nhóm đọc vào hiểu ngay luồng xử lý.
   - Bật `GenerateDocumentationFile` trên project API để hiển thị đầy đủ mô tả đặc tả trên giao diện Swagger UI cho team FE dễ tích hợp.
2. **Quy trình Chỉnh sửa DB theo chuẩn Code-First (EF Core Migrations)**:
   - Tuyệt đối không sửa trực tiếp bảng trong SQL Server bằng tay.
   - Mỗi khi thêm/sửa Entity hoặc Fluent API Configuration trong `WMMS.Domain` và `WMMS.Infrastructure`:
     + Bước 1: Kiểm tra và đồng bộ nhánh Git để tránh xung đột snapshot (`ModelSnapshot`) với thành viên khác.
     + Bước 2: Tạo migration mới với tên mô tả rõ ràng: `dotnet ef migrations add <TenMigration> --project src/WMMS.Infrastructure --startup-project src/WMMS.API --output-dir Persistence/Migrations`.
     + Bước 3: Cập nhật vào database: `dotnet ef database update --project src/WMMS.Infrastructure --startup-project src/WMMS.API`.
<!-- <<< code documentation and migration bridge <<< -->
