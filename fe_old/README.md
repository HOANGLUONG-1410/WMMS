# WMMS — Frontend Workspace (ReactJS SPA)

> **Hệ thống Quản lý Sản xuất Xưởng gỗ (Wooden Manufacturing Management System)**  
> **Phân hệ:** Frontend Single Page Application (ReactJS)  
> **Tài liệu tham chiếu:** `Report 4 – Software Design Specification` (Mục I.2.1, Table 1.2 & Figure 1.5)

---

## 1. Cấu trúc Gói Frontend Đề xuất (Theo Report 4 Figure 1.5)

Toàn bộ mã nguồn ứng dụng client ReactJS sẽ được nhóm FE phát triển bên trong thư mục này theo 9 gói chuẩn:

| Gói (Package) | Mô tả trách nhiệm |
| :--- | :--- |
| `src/pages/` | Các trang giao diện chính: `StagingOrderPage`, `OrderSpacePage`, `BomBuilderPage`, `StockReconcilePage`, `FactoryKanbanPage`, `ExecutiveDashboardPage` |
| `src/components/` | Các component tương tác: `StagingGridEditor`, `VisualCatalogModal`, `DynamicRoutingBuilder`, `KanbanBoard`, `QcInspectionModal` |
| `src/services/` | Module gọi REST API (Axios) và kết nối WebSocket thời gian thực (SignalR Client tới `WorkspaceHub` và `KanbanHub`) |
| `src/context/` | Quản lý state toàn cục (Auth, Multi-Role RBAC permissions, active Order Space) |
| `src/routes/` | Điều hướng URL và các `ProtectedRoute` chặn quyền theo 8 vai trò |
| `src/utils/` | Hàm tiện ích: tính toán hao hụt BOM (3%–5%), định dạng ngày ETD, kiểm tra hợp lệ |
| `src/assets/` | File tĩnh: ảnh 3D sản phẩm đồ gỗ, icons, CSS/Tailwind styles |
| `src/constant/` | Hằng số hệ thống: API base URLs, mã lỗi QC (`ERR_CUT_01`..`ERR_PAINT_04`), Status enums |
| `src/common/` | Các UI component dùng chung: Buttons, Modals, Drawers, Jira-like Sidebar, Badges |
