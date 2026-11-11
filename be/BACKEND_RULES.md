# QUY CHUẨN PHÁT TRIỂN BACKEND & BỘ NÃO AI (BACKEND RULES)
## Hệ thống Quản lý Sản xuất Xưởng gỗ (WMMS - SEP490_G122)

> **Tài liệu quy chuẩn kiến trúc và nguyên tắc phát triển chung cho toàn bộ đội ngũ Backend**  
> **Áp dụng cho mọi công cụ AI:** Cursor, Windsurf, GitHub Copilot, Claude Code, Google Antigravity, ChatGPT.

---

## 1. Nguyên tắc Phân chia Phạm vi (Scope Isolation)
- **Chỉ làm việc trong `be/` (`WMMS_BE`):** Mọi công việc backend chỉ thêm, sửa trong thư mục `be/`.
- **Tuyệt đối không sửa `fe/`:** Frontend đã có bộ quy chuẩn riêng (`.projectmem/WMMS_Frontend_UI_Guidelines.md`). Không sửa code hay can thiệp vào `fe/`.
- **Tuyệt đối không tạo project test trong repository:** Thư mục git code chính `be/` phải luôn sạch sẽ. Toàn bộ Unit Test và Integration Test phải được tổ chức trong project test nằm độc lập bên ngoài repository (ví dụ: `WMMS.UnitTests`).

---

## 2. Kiến trúc 4 Tầng Bắt buộc (Clean Architecture)

```
[WMMS.API]               --> [WMMS.Application]      --> [WMMS.Domain]
(Controllers, DI, MW)        (Services, DTOs, Valid)     (Entities, Enums, Const)
         |                           ^                          ^
         +---------------------------+                          |
         |                                                      |
         +--> [WMMS.Infrastructure] ----------------------------+
              (DbContext, Repositories, UnitOfWork)
```

### 2.1. `WMMS.Domain` (Tầng lõi - Core)
- **Thư mục:** `be/WMMS_BE/src/WMMS.Domain/`
- **Chứa:** Entities (`CustomerOrder`, `BomHeader`, `MaterialStock`, `PurchaseRequest`, `ProductionBatch`, v.v.), Enums, Constants, Domain Exceptions.
- **Quy tắc:** KHÔNG cài đặt bất kỳ package EF Core hay Web API nào. Tên class đặt theo PascalCase, bám sát Class Diagram Report 4.

### 2.2. `WMMS.Application` (Tầng nghiệp vụ - Business Logic)
- **Thư mục:** `be/WMMS_BE/src/WMMS.Application/`
- **Chứa:** Service interfaces (`IPurchaseRequestService`), Service implementations (`PurchaseRequestService`), DTOs (Request/Response), Validators (FluentValidation), Mappers.
- **Quy tắc:**
  - **CẤM tiệt inject `WmmsDbContext` vào Application Services.** Bắt buộc inject `IUnitOfWork` và các repository interface (`IMaterialStockRepository`, `IPurchaseRequestRepository`, `IProductionBatchRepository`, v.v.).
  - Mọi hàm Service thao tác ghi CSDL phải gọi qua `_unitOfWork.CommitAsync()` hoặc `_unitOfWork.SaveChangesAsync()`.
  - Mọi Request DTO phải có class Validator kế thừa `AbstractValidator<TRequest>`.

### 2.3. `WMMS.Infrastructure` (Tầng hạ tầng & CSDL)
- **Thư mục:** `be/WMMS_BE/src/WMMS.Infrastructure/`
- **Chứa:** `WmmsDbContext`, EF Core Configurations (`IEntityTypeConfiguration<T>`), Repositories (`GenericRepository<T>`, `PurchaseRequestRepository`, v.v.), UnitOfWork, Excel Parser, Token Generator.
- **Quy tắc:**
  - Bảng CSDL vật lý bắt buộc ánh xạ sang **snake_case** theo Report 4 ERD (ví dụ: `ToTable("customer_orders")`, `ToTable("inventory_stocks")`, `ToTable("production_batches")`).
  - Các bảng có giao dịch đồng thời cao (`inventory_stocks`, `consolidated_prs`) phải cấu hình `IsRowVersion()` cho trường `RowVersion`.

### 2.4. `WMMS.API` (Tầng hiển thị & Endpoints)
- **Thư mục:** `be/WMMS_BE/src/WMMS.API/`
- **Chứa:** Controllers (`[ApiController]`, `[Route("api/v1/[controller]")]`), SignalR Hubs (`WmmsNotificationHub`), Middlewares (`GlobalExceptionMiddleware`), cấu hình DI (`Program.cs`).
- **Quy tắc:**
  - Controller phải mỏng (Thin Controller): chỉ nhận request, gọi Service, trả về kết quả. Không tính toán logic hay gọi repository trong Controller.
  - Chuẩn hóa đầu ra 100% bằng `ApiResponse<T>`:
    ```csharp
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(id);
        return Ok(ApiResponse<MyDto>.Ok(result, "Lấy thông tin thành công"));
    }
    ```

---

## 3. Bản địa hóa Tiếng Việt (i18n) & Bắt buộc Thông báo Chuẩn
- **100% Thông báo trả về người dùng và FE phải là Tiếng Việt.**
- Không viết tiếng Anh cứng trong code: `"Purchase request not found"` $\to$ Đổi thành: `"Đề xuất mua hàng không tồn tại"`.
- Bắt buộc kiểm tra độ dài lý do từ chối: tối thiểu 10 ký tự, tối đa 500 ký tự:
  ```csharp
  RuleFor(x => x.RejectionReason)
      .NotEmpty().WithMessage("Lý do từ chối không được để trống")
      .MinimumLength(10).WithMessage("Lý do từ chối phải có độ dài từ 10 đến 500 ký tự")
      .MaximumLength(500).WithMessage("Lý do từ chối không được vượt quá 500 ký tự");
  ```

---

## 4. Xử lý Tranh chấp Đồng thời (Concurrency) & Audit Log
- **Mã lỗi 409 Conflict (`UC047.E1`):** Khi thao tác thay đổi trạng thái (Duyệt PR, Nhập kho GRN, Chuyển cột Kanban), nếu bản ghi đã bị thay đổi bởi người khác, phải ném ra `ConflictException` (HTTP 409):
  ```csharp
  if (pr.Status != PurchaseRequestStatus.SUBMITTED)
  {
      throw new ConflictException("Yêu cầu đã được phê duyệt hoặc từ chối trước đó (xung đột dữ liệu)");
  }
  ```
- **Nhật ký Kiểm toán Bất biến (`GBR-05`):** Mọi hành động duyệt, từ chối, chuyển trạng thái phải tự động append vào bảng `system_audit_logs` gồm: `UserId`, `Action`, `EntityName`, `EntityId`, `OldValues`, `NewValues`, `TimestampUtc`.

---

## 5. Quy trình Bắt buộc: Ghi nhận Lỗi đỏ trước khi Sửa code (Red-to-Green Test Logging)

> **NGUYÊN TẮC VÀNG:**  
> Không thể có chuyện chạy test một phát là "xanh 100%" ngay từ đầu. Để tạo bằng chứng kiểm thử chân thực nộp hội đồng bảo vệ đồ án, **mọi lỗi đỏ (test fail) đều là tài sản quý giá chứng minh quy trình làm việc thực tế**.

### 5.1. Chu trình 3 bước xử lý lỗi kiểm thử
1. **BƯỚC 1 - LƯU VẾT LỖI ĐỎ (Log Defect Evidence):**  
   Khi chạy test gặp lỗi đỏ, **TUYỆT ĐỐI KHÔNG ĐƯỢC âm thầm sửa code ngay**. Bắt buộc ghi lại vào file nhật ký kiểm thử:
   - **Mã lỗi / Mã Test Case:** Ví dụ: `UT-PO-02`, `UT-GRN-05`, `UT-MRP-04`.
   - **Vị trí phát sinh:** File mã nguồn logic, tên class, phương thức, dòng lỗi.
   - **Chi tiết lỗi thực tế:** Thông báo Exception, Stack Trace, hoặc Assertion Mismatch (*"Expected status 200 but received 409 Conflict"* / *"Expected Quantity 126.0 but found 125.999"*).
   - **Phân tích nguyên nhân:** Chỉ rõ tại sao code chưa đúng (thiếu validate, sai công thức, thiếu seed data, concurrency race condition).
2. **BƯỚC 2 - THỰC HIỆN SỬA CODE (Fix & Refactor):**  
   Sau khi đã lưu bằng chứng lỗi, tiến hành điều chỉnh mã nguồn hoặc dữ liệu kiểm thử.
3. **BƯỚC 3 - GHI NHẬN KẾT QUẢ XANH (Log Green Resolution):**  
   Chạy lại test $\to$ pass xanh $\to$ ghi lại tóm tắt giải pháp khắc phục và xác nhận kết quả đạt chuẩn.

### 5.2. Tính linh hoạt về Nơi lưu trữ & Cảnh báo thành viên
- **Linh hoạt nơi lưu trữ:** Mỗi thành viên có thể lưu file nhật ký riêng theo môi trường cá nhân (`.docx`, `.xlsx` hoặc `.md`).
- **Quy tắc Cảnh báo của AI:** Nếu thành viên yêu cầu AI sửa lỗi test nhưng chưa cung cấp hoặc chưa có file lưu vết lỗi kiểm thử, **AI BẮT BUỘC phải thông báo nhắc nhở thành viên tạo file log lỗi trước khi bắt tay vào sửa code**.

---

## 6. Mẫu Tham Chiếu Kiến Trúc Cho Các Thành Viên Backend

| Module phụ trách | Đặc tả cốt lõi | Mẫu code chuẩn trong dự án để tham khảo |
| :--- | :--- | :--- |
| **MRP & Phân bổ tồn kho** | **SC-14** (Tính MRP, Scrap rate `BR-BOM-02`, Soft-allocation `BR-INV-02`) | [`MrpAllocationService.cs`](file:///d:/WMMS/be/WMMS_BE/src/WMMS.Application/Services/InventoryPr/MrpAllocationService.cs) |
| **Yêu cầu mua sắm PR** | **SC-15** (Duyệt/từ chối PR, phân quyền Owner, 409 Conflict, Audit log) | [`PurchaseRequestService.cs`](file:///d:/WMMS/be/WMMS_BE/src/WMMS.Application/Services/InventoryPr/PurchaseRequestService.cs) |
| **Đơn mua PO & Kanban** | **SC-16** (PO theo NCC & ETA), **SC-19** (Tách lô & Kanban 100% Pass) | [`ProductionOrderService.cs`](file:///d:/WMMS/be/WMMS_BE/src/WMMS.Application/Services/KanbanQc/ProductionOrderService.cs) |
| **Nhập kho & QC Gate** | **SC-17** (Nhập kho GRN & Auto-allocation), **SC-20b** (QC Gate 97%-3%) | Giao dịch nguyên tử `_unitOfWork.BeginTransactionAsync()` |
