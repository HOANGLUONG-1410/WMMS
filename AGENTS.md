# AGENTS.md

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
<<<<<<< Updated upstream
=======

<!-- >>> projectmem local isolation bridge >>> -->
## .projectmem Local-Only Isolation Rule (MANDATORY)
- Thư mục `.projectmem/` chứa cơ sở dữ liệu và bộ nhớ AI cục bộ, tuyệt đối KHÔNG commit hoặc push lên Git.
- Thư mục `.projectmem/` đã được đưa vào `.gitignore` để cách ly hoàn toàn.
- Tuyệt đối không dùng `git add .projectmem` hoặc stage bất kỳ file nào trong thư mục này để tránh xung đột hoặc làm bẩn nhánh khi các thành viên khác (duy, duong, cuong, hunganh) merge nhánh.
<!-- <<< projectmem local isolation bridge <<< -->

<!-- >>> backend & testing scope bridge >>> -->
## Backend-Only Scope & External Testing Rule (MANDATORY)
- **Chỉ làm việc trong `be/` (`WMMS_BE`)**: Toàn bộ chức năng backend chỉ thêm/sửa trong `be/`.
- **Tuyệt đối không đụng vào `fe/`**: Frontend được quản trị riêng biệt, không chỉnh sửa file hay cấu hình bên `fe/`.
- **Thư mục kiểm thử biệt lập**: Toàn bộ Unit Test, Integration Test và Mock Data được chạy và lưu trữ độc lập tại thư mục bên ngoài (`app test AI`), tuyệt đối không tạo file test hoặc file rác trong repo `be/`.
<!-- <<< backend & testing scope bridge <<< -->

<!-- >>> red to green test logging bridge >>> -->
## Red-to-Green Defect Logging Protocol (MANDATORY)
- **Ghi nhận lỗi đỏ trước khi sửa**: Trong quá trình thực hiện công việc, mỗi lần chạy test hoặc build có lỗi phát sinh (Build Failed, Test Failed / Unsuccessful), **TUYỆT ĐỐI KHÔNG âm thầm sửa ngay**.
- Bắt buộc ghi nhận đầy đủ chi tiết lỗi vào file nhật ký kiểm thử:
  + Mã Test Case / Lỗi build
  + File / Dòng phát sinh
  + Chi tiết lỗi thực tế (Exception, Assertion Mismatch, HTTP Status)
  + Nguyên nhân gốc rễ (Root Cause)
- **Ghi nhận kết quả xanh**: Sau khi sửa và chạy lại test đạt trạng thái xanh (PASS 100%), ghi nhận tóm tắt giải pháp khắc phục.
<!-- <<< red to green test logging bridge <<< -->

<!-- >>> code documentation and comments bridge >>> -->
## Code Documentation & XML Comments (MANDATORY)
- Mọi hàm, Class, Interface, DTO, Controller tạo mới hoặc chỉnh sửa phải có XML Comments (`/// <summary>`) đầy đủ, giải thích rõ nghiệp vụ và mã Use Case (`UC-...`), Business Rule (`GBR-...`, `BR-...`).
- Tất cả các thông báo trả về (Message trong `ApiResponse<T>`, Validation Messages trong FluentValidation) bắt buộc phải là **Tiếng Việt chuẩn**.
<!-- <<< code documentation and comments bridge <<< -->
>>>>>>> Stashed changes
