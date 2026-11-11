/**
 * Hằng số cấu hình hệ thống WMMS Frontend (Report 3 & Report 4)
 * Bao gồm: API Endpoints, Role Codes (BR-ADM-01), System Messages (MSG-AUT/ADM/ORD),
 * và dữ liệu khởi tạo fallback để UI hoạt động mượt mà ngay cả khi xem độc lập.
 */

export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000/api/v1';

/**
 * 8 Vai trò chuẩn trong hệ thống WMMS (Multi-Role RBAC - BR-ADM-01)
 */
export const ROLE_CONFIG = {
  ADMIN: {
    code: 'ADMIN',
    badgeLabel: 'ADMIN',
    displayName: 'Quản trị hệ thống',
    roleTitle: 'Quản trị hệ thống',
    badgeClass: 'bg-amber-600 text-white',
    pillClass: 'bg-purple-100 text-purple-800 border-purple-200',
    operationalScope: 'Toàn quyền hệ thống, phân quyền đa vai trò (RBAC), nhật ký kiểm toán',
    defaultUser: { fullName: 'Le Van Admin', initials: 'AD', email: 'admin@nguyenkhoa.vn' }
  },
  OWNER: {
    code: 'OWNER',
    badgeLabel: 'OWNER',
    displayName: 'CHỦ XƯỞNG',
    roleTitle: 'Chủ xưởng & Trưởng kỹ thuật',
    badgeClass: 'bg-amber-600 text-white',
    pillClass: 'bg-yellow-100 text-yellow-800 border-yellow-300',
    operationalScope: 'Bóc tách BOM, duyệt mua hàng, duyệt cấp phát',
    defaultUser: { fullName: 'Nguyen Van Khoa', initials: 'NK', email: 'owner@nguyenkhoa.vn' }
  },
  SALES: {
    code: 'SALES',
    badgeLabel: 'SALES',
    displayName: 'NHÂN VIÊN KINH DOANH',
    roleTitle: 'Nhân viên kinh doanh',
    badgeClass: 'bg-blue-600 text-white',
    pillClass: 'bg-blue-100 text-blue-700 border-blue-200',
    operationalScope: 'Nhập PO Excel qua Staging, làm sạch dữ liệu, khởi tạo Order Space',
    defaultUser: { fullName: 'Tran Thi Mai', initials: 'TM', email: 'sales@nguyenkhoa.vn' }
  },
  PLANNER: {
    code: 'PLANNER',
    badgeLabel: 'PLANNER',
    displayName: 'KẾ HOẠCH VẬT TƯ',
    roleTitle: 'Nhân viên kế hoạch vật tư',
    badgeClass: 'bg-indigo-600 text-white',
    pillClass: 'bg-indigo-100 text-indigo-700 border-indigo-200',
    operationalScope: 'Bóc tách BOM, đối chiếu tồn kho, gom PR theo ngày',
    defaultUser: { fullName: 'Pham Minh Duc', initials: 'MD', email: 'planner@nguyenkhoa.vn' }
  },
  LEADER: {
    code: 'LEADER',
    badgeLabel: 'FLOOR',
    displayName: 'QUẢN ĐỐC SẢN XUẤT',
    roleTitle: 'Quản đốc sản xuất',
    badgeClass: 'bg-orange-700 text-white',
    pillClass: 'bg-orange-100 text-orange-800 border-orange-200',
    operationalScope: 'Điều phối Kanban xưởng, yêu cầu vượt chốt (Overdriver)',
    defaultUser: { fullName: 'Pham Van Tien', initials: 'PT', email: 'leader@nguyenkhoa.vn' }
  },
  QC: {
    code: 'QC',
    badgeLabel: 'QC',
    displayName: 'NHÂN VIÊN QC',
    roleTitle: 'Kiểm soát viên QC',
    badgeClass: 'bg-emerald-600 text-white',
    pillClass: 'bg-rose-100 text-rose-700 border-rose-200',
    operationalScope: 'Cổng chất lượng tại trạm, ghi nhận lỗi',
    defaultUser: { fullName: 'Hoang Thi Lan', initials: 'HL', email: 'qc@nguyenkhoa.vn' }
  },
  WAREHOUSE: {
    code: 'WAREHOUSE',
    badgeLabel: 'STOCK',
    displayName: 'THỦ KHO',
    roleTitle: 'Thủ kho',
    badgeClass: 'bg-amber-700 text-white',
    pillClass: 'bg-amber-100 text-amber-800 border-amber-200',
    operationalScope: 'Xác nhận nhập kho, xuất kho bàn giao xuống xưởng',
    defaultUser: { fullName: 'Vu Duc Thang', initials: 'VT', email: 'warehouse@nguyenkhoa.vn' }
  },
  PURCHASING: {
    code: 'PURCHASING',
    badgeLabel: 'PURCHASING',
    displayName: 'NHÂN VIÊN MUA HÀNG',
    roleTitle: 'Nhân viên mua hàng',
    badgeClass: 'bg-emerald-600 text-white',
    pillClass: 'bg-teal-100 text-teal-800 border-teal-200',
    operationalScope: 'Tìm nhà cung cấp, lập PO và theo dõi ngày giao (ETA)',
    defaultUser: { fullName: 'Do Quang Huy', initials: 'QH', email: 'purchaser@nguyenkhoa.vn' }
  }
};

/**
 * Danh sách Không gian Đơn hàng (Order Spaces) mặc định cho Global Space Switcher (Section III.2, III.3, III.4)
 */
export const DEFAULT_ORDER_SPACES = [
  {
    spaceKey: 'SPACE-PO-880504',
    spaceLabel: 'SPACE-PO-880504 (Bộ bàn ăn - Ashley Mỹ)',
    customerName: 'Ashley Furniture Homestore (Mỹ)',
    poNumber: 'ASH-2026-OCT-880504',
    orderDate: '2026-10-12',
    etdDate: '2026-10-28',
    daysRemaining: 8,
    isUrgentEtd: true,
    totalOrderedUnits: 500,
    lineItemsVolumeSummary: '500 bộ (ghế + bàn)',
    lifecycleStatusBadge: 'IN_PRODUCTION',
    overallProgressPct: 62
  },
  {
    spaceKey: 'SPACE-PO-880506',
    spaceLabel: 'SPACE-PO-880506 (Bàn cà phê - JYSK Đan Mạch)',
    customerName: 'JYSK Nordic (Đan Mạch)',
    poNumber: 'JYSK-DK-880506',
    orderDate: '2026-10-14',
    etdDate: '2026-11-05',
    daysRemaining: 16,
    isUrgentEtd: false,
    totalOrderedUnits: 250,
    lineItemsVolumeSummary: '250 cái bàn cà phê gỗ sồi',
    lifecycleStatusBadge: 'IN_PRODUCTION',
    overallProgressPct: 45
  },
  {
    spaceKey: 'SPACE-PO-880507',
    spaceLabel: 'SPACE-PO-880507 (Khung giường - IKEA Úc)',
    customerName: 'IKEA Supply AG (Úc)',
    poNumber: 'IKEA-AU-880507',
    orderDate: '2026-10-17',
    etdDate: '2026-11-20',
    daysRemaining: 31,
    isUrgentEtd: false,
    totalOrderedUnits: 300,
    lineItemsVolumeSummary: '300 cái khung giường Queen',
    lifecycleStatusBadge: 'BOM_SETTING',
    overallProgressPct: 0
  },
  {
    spaceKey: 'SPACE-PO-880512',
    spaceLabel: 'SPACE-PO-880512 (Ghế đẩu gỗ - Nitori Nhật)',
    customerName: 'Nitori Holdings (Nhật Bản)',
    poNumber: 'NIT-JP-880512',
    orderDate: '2026-10-19',
    etdDate: '2026-12-10',
    daysRemaining: 51,
    isUrgentEtd: false,
    totalOrderedUnits: 400,
    lineItemsVolumeSummary: '400 cái ghế đẩu gỗ nguyên khối',
    lifecycleStatusBadge: 'NEWLY_RECEIVED',
    overallProgressPct: 0
  }
];

/**
 * Dữ liệu mặc định cho bảng Employee User Directory (SCR-ADM-01 - Section III.3.1)
 */
export const INITIAL_USERS_DIRECTORY = [
  {
    userId: 1,
    fullName: 'Nguyen Van Khoa',
    email: 'owner@nguyenkhoa.vn',
    primaryRole: 'OWNER',
    primaryRoleLabel: 'CHỦ XƯỞNG',
    operationalScope: 'Bóc tách BOM, duyệt mua hàng, duyệt cấp phát',
    status: 'ACTIVE'
  },
  {
    userId: 2,
    fullName: 'Tran Thi Mai',
    email: 'sales@nguyenkhoa.vn',
    primaryRole: 'SALES',
    primaryRoleLabel: 'NHÂN VIÊN KINH DOANH',
    operationalScope: 'Nhập PO Excel qua Staging, làm sạch dữ liệu, khởi tạo Order Space',
    status: 'ACTIVE'
  },
  {
    userId: 3,
    fullName: 'Pham Van Tien',
    email: 'leader@nguyenkhoa.vn',
    primaryRole: 'LEADER',
    primaryRoleLabel: 'QUẢN ĐỐC SẢN XUẤT',
    operationalScope: 'Điều phối Kanban xưởng, yêu cầu vượt chốt (Overdriver)',
    status: 'ACTIVE'
  },
  {
    userId: 4,
    fullName: 'Hoang Thi Lan',
    email: 'qc@nguyenkhoa.vn',
    primaryRole: 'QC',
    primaryRoleLabel: 'NHÂN VIÊN QC',
    operationalScope: 'Cổng chất lượng tại trạm, ghi nhận lỗi',
    status: 'ACTIVE'
  },
  {
    userId: 5,
    fullName: 'Vu Duc Thang',
    email: 'warehouse@nguyenkhoa.vn',
    primaryRole: 'WAREHOUSE',
    primaryRoleLabel: 'THỦ KHO',
    operationalScope: 'Xác nhận nhập kho, xuất kho bàn giao xuống xưởng',
    status: 'ACTIVE'
  },
  {
    userId: 6,
    fullName: 'Do Quang Huy',
    email: 'purchaser@nguyenkhoa.vn',
    primaryRole: 'PURCHASING',
    primaryRoleLabel: 'NHÂN VIÊN MUA HÀNG',
    operationalScope: 'Tìm nhà cung cấp, lập PO và theo dõi ngày giao (ETA)',
    status: 'ACTIVE'
  },
  {
    userId: 7,
    fullName: 'Nguyen Van Tam',
    email: 'tam.ex@nguyenkhoa.vn',
    primaryRole: 'APPRENTICE',
    primaryRoleLabel: 'NHÂN VIÊN THỬ VIỆC',
    operationalScope: 'Quan sát tạm thời',
    status: 'SUSPENDED'
  }
];

/**
 * Ma trận cấu hình phân quyền theo vai trò (Role Permission Configuration Matrix MOD-ADM-02 - Section III.3.3)
 */
export const ROLE_PERMISSION_MATRIX_ROWS = [
  {
    functionalDomain: 'Bóc tách BOM',
    actionScope: 'Cấu hình quy trình & % hao hụt',
    workshopOwner: { label: 'FULL', type: 'full' },
    productionLeader: { label: 'NO', type: 'no' },
    qcStaff: { label: 'NO', type: 'no' },
    warehouse: { label: 'NO', type: 'no' }
  },
  {
    functionalDomain: 'Duyệt PR',
    actionScope: 'Duyệt PR gộp theo ngày',
    workshopOwner: { label: 'FULL', type: 'full' },
    productionLeader: { label: 'NO', type: 'no' },
    qcStaff: { label: 'NO', type: 'no' },
    warehouse: { label: 'NO', type: 'no' }
  },
  {
    functionalDomain: 'Điều phối Kanban',
    actionScope: 'Chuyển lô giữa các trạm',
    workshopOwner: { label: 'MONITOR', type: 'monitor' },
    productionLeader: { label: 'ĐẦY ĐỦ (QUY TẮC 100%)', type: 'full' },
    qcStaff: { label: 'NO', type: 'no' },
    warehouse: { label: 'NO', type: 'no' }
  },
  {
    functionalDomain: 'Cổng chất lượng',
    actionScope: 'Ghi nhận Đạt / Làm lại / Phế phẩm',
    workshopOwner: { label: 'NO', type: 'no' },
    productionLeader: { label: 'NO', type: 'no' },
    qcStaff: { label: 'FULL', type: 'full' },
    warehouse: { label: 'NO', type: 'no' }
  },
  {
    functionalDomain: 'Vượt chốt (Overdriver)',
    actionScope: 'Cho phép bỏ qua Hold Point',
    workshopOwner: { label: 'DUYỆT ĐỘC QUYỀN', type: 'full' },
    productionLeader: { label: 'CHỈ ĐƯỢC YÊU CẦU', type: 'request' },
    qcStaff: { label: 'NO', type: 'no' },
    warehouse: { label: 'NO', type: 'no' }
  }
];

/**
 * Dữ liệu mặc định cho màn hình Immutable System Audit Logs (SCR-ADM-02 - Section III.3.4)
 */
export const INITIAL_AUDIT_LOGS = [
  {
    logId: 101,
    timestamp: '2026-10-20 09:15:22',
    actorName: 'Nguyen Van Khoa',
    actorRole: 'OWNER',
    actionType: 'AUTHORIZE_OVERDRIVER',
    targetEntity: 'Lô #LOT-880504-01',
    stateDelta: 'LOCKED_WAITING_MAT → OVERDRIVER_RUNNING',
    reason: '"Khách yêu cầu giao gấp (ETD: 28/10). Thiếu bản lề. Cho phép sơ chế đến Hold Point lắp ráp."'
  },
  {
    logId: 102,
    timestamp: '2026-10-20 08:30:10',
    actorName: 'Tran Thi Mai',
    actorRole: 'SALES',
    actionType: 'CONFIRM_PO_STAGING',
    targetEntity: 'Order Space #SPACE-880507',
    stateDelta: 'STAGING_RAW → NEWLY_RECEIVED',
    reason: '"Đã sửa 4 lỗi ô gộp. Đã đổi inch sang mm. Đã tạo Order Space cho 300 khung giường."'
  },
  {
    logId: 103,
    timestamp: '2026-10-19 16:45:00',
    actorName: 'Nguyen Van Khoa',
    actorRole: 'OWNER',
    actionType: 'APPROVE_SCRAP_REPLACEMENT',
    targetEntity: 'Đơn #PO-880506',
    stateDelta: 'SCRAP_PENDING → SUB_BATCH_ISSUED',
    reason: '"QC báo 3 chi tiết mộng bị gãy ở công đoạn lắp ráp. Đã phát lệnh sản xuất bù 3 chân bàn."'
  },
  {
    logId: 104,
    timestamp: '2026-10-19 14:12:44',
    actorName: 'Vu Duc Thang',
    actorRole: 'WAREHOUSE',
    actionType: 'COMMIT_GOODS_RECEIPT',
    targetEntity: 'PO-MAT-2026-042',
    stateDelta: 'IN_TRANSIT → RECEIVED_COMMITTED',
    reason: '"Nhận 500 tấm gỗ sồi từ NCC Hoàng Thông. Tự động phân bổ 320 tấm cho đơn 880504 theo ưu tiên ETD."'
  },
  {
    logId: 105,
    timestamp: '2026-10-19 11:05:18',
    actorName: 'Nguyen Van Khoa',
    actorRole: 'OWNER',
    actionType: 'APPROVE_CONSOLIDATED_PR',
    targetEntity: 'PR gộp ngày 2026-10-19',
    stateDelta: 'DRAFT_PR → APPROVED_FOR_PURCHASING',
    reason: '"Đã gom thiếu hụt vật tư của 3 đơn trong ngày. Đã phê duyệt 1.200 m ván và 4.000 vít."'
  }
];

/**
 * Dữ liệu mặc định cho bảng Safe Excel PO Staging Grid (SCR-ORD-02 - Section III.4.2)
 */
export const INITIAL_STAGING_GRID_ROWS = [
  {
    rowNumber: 1,
    productSku: 'CHAIR-OAK-01',
    productDescription: 'Ghế ăn gỗ sồi phong cách Bắc Âu',
    dimensionsMm: '450 x 520 x 850',
    finishColor: 'PU trong tự nhiên',
    orderQty: 300,
    targetEtd: '2026-11-15',
    fscRequired: true,
    technicalDrawingName: 'CAD_Chair_V2.pdf',
    isRowValid: true,
    cellErrors: []
  },
  {
    rowNumber: 2,
    productSku: 'TABLE-OAK-02',
    productDescription: 'Bàn ăn chữ nhật 1,8 m',
    dimensionsMm: '1800 x 900 x 750',
    finishColor: 'Nhuộm màu óc chó',
    orderQty: 80,
    targetEtd: '2026-11-15',
    fscRequired: true,
    technicalDrawingName: 'CAD_Table_180.pdf',
    isRowValid: true,
    cellErrors: []
  },
  {
    rowNumber: 3,
    productSku: 'BENCH-OAK-03',
    productDescription: 'Ghế băng ăn gỗ sồi nguyên khối',
    dimensionsMm: '1400 x 380 x 450',
    finishColor: 'PU trong tự nhiên',
    orderQty: 100,
    targetEtd: '2026-11-15',
    fscRequired: true,
    technicalDrawingName: 'CAD_Bench_140.pdf',
    isRowValid: true,
    cellErrors: []
  },
  {
    rowNumber: 4,
    productSku: 'STOOL-OAK-04',
    productDescription: 'Ghế đẩu bếp nhỏ gọn',
    dimensionsMm: '380 x 380 x 450',
    finishColor: 'Hoàn thiện màu tự nhiên',
    orderQty: 150,
    targetEtd: '2026-11-20',
    fscRequired: true,
    technicalDrawingName: 'CAD_Stool_V1.pdf',
    isRowValid: true,
    cellErrors: []
  }
];
