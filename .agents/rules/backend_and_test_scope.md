# Rule: Backend-Only Scope & External Unit Testing Directory

## 1. Unit Testing Rules
- Mọi thao tác viết unit test, tạo project test, chạy test (`test run`) PHẢI được đặt ở thư mục ngoài: `D:\dotnet\unitestWMMS`.
- Tuyệt đối không tạo file test, project test, file dữ liệu giả tạm bợ hoặc file rác bên trong thư mục code chính của dự án WMMS.
- Giữ sạch sẽ repository WMMS (`c:\Users\ADMIN\Documents\github\WMMS`).

## 2. Phạm vi Phát triển (Backend Only)
- Chỉ code và chỉnh sửa các file thuộc thư mục `be/` (`WMMS_BE`).
- Tuyệt đối không thao tác, không chỉnh sửa, không tạo file trong thư mục `fe/` (Frontend).
