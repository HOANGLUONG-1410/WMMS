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
    displayName: 'System Administrator',
    roleTitle: 'System Administrator',
    badgeClass: 'bg-amber-600 text-white',
    pillClass: 'bg-purple-100 text-purple-800 border-purple-200',
    operationalScope: 'Full System Control, Multi-Role RBAC, Audit Trail',
    defaultUser: { fullName: 'Le Van Admin', initials: 'AD', email: 'admin@nguyenkhoa.vn' }
  },
  OWNER: {
    code: 'OWNER',
    badgeLabel: 'OWNER',
    displayName: 'WORKSHOP OWNER',
    roleTitle: 'Workshop Owner & Technical Lead',
    badgeClass: 'bg-amber-600 text-white',
    pillClass: 'bg-yellow-100 text-yellow-800 border-yellow-300',
    operationalScope: 'BOM Breakdown, Purchasing Approval, Dispatch Release',
    defaultUser: { fullName: 'Nguyen Van Khoa', initials: 'NK', email: 'owner@nguyenkhoa.vn' }
  },
  SALES: {
    code: 'SALES',
    badgeLabel: 'SALES',
    displayName: 'SALES STAFF',
    roleTitle: 'Sales Representative',
    badgeClass: 'bg-blue-600 text-white',
    pillClass: 'bg-blue-100 text-blue-700 border-blue-200',
    operationalScope: 'Excel PO Staging, CRUD Sanitization, Space Init',
    defaultUser: { fullName: 'Tran Thi Mai', initials: 'TM', email: 'sales@nguyenkhoa.vn' }
  },
  PLANNER: {
    code: 'PLANNER',
    badgeLabel: 'PLANNER',
    displayName: 'MATERIAL PLANNER',
    roleTitle: 'Material Planning Staff',
    badgeClass: 'bg-indigo-600 text-white',
    pillClass: 'bg-indigo-100 text-indigo-700 border-indigo-200',
    operationalScope: 'BOM Breakdown, Stock Reconciliation, Daily PR Consolidation',
    defaultUser: { fullName: 'Pham Minh Duc', initials: 'MD', email: 'planner@nguyenkhoa.vn' }
  },
  LEADER: {
    code: 'LEADER',
    badgeLabel: 'FLOOR',
    displayName: 'PRODUCTION LEADER',
    roleTitle: 'Production Leader',
    badgeClass: 'bg-orange-700 text-white',
    pillClass: 'bg-orange-100 text-orange-800 border-orange-200',
    operationalScope: 'Kanban Floor Dispatch, Overdriver Requests',
    defaultUser: { fullName: 'Pham Van Tien', initials: 'PT', email: 'leader@nguyenkhoa.vn' }
  },
  QC: {
    code: 'QC',
    badgeLabel: 'QC',
    displayName: 'QC STAFF',
    roleTitle: 'QC Inspector',
    badgeClass: 'bg-emerald-600 text-white',
    pillClass: 'bg-rose-100 text-rose-700 border-rose-200',
    operationalScope: 'Workstation Quality Gate, Defect Logging',
    defaultUser: { fullName: 'Hoang Thi Lan', initials: 'HL', email: 'qc@nguyenkhoa.vn' }
  },
  WAREHOUSE: {
    code: 'WAREHOUSE',
    badgeLabel: 'STOCK',
    displayName: 'WAREHOUSE KEEPER',
    roleTitle: 'Warehouse Keeper',
    badgeClass: 'bg-amber-700 text-white',
    pillClass: 'bg-amber-100 text-amber-800 border-amber-200',
    operationalScope: 'Goods Receipt Commit, Goods Issue Floor Handover',
    defaultUser: { fullName: 'Vu Duc Thang', initials: 'VT', email: 'warehouse@nguyenkhoa.vn' }
  },
  PURCHASING: {
    code: 'PURCHASING',
    badgeLabel: 'PURCHASING',
    displayName: 'PURCHASING STAFF',
    roleTitle: 'Purchasing Staff',
    badgeClass: 'bg-emerald-600 text-white',
    pillClass: 'bg-teal-100 text-teal-800 border-teal-200',
    operationalScope: 'Vendor Sourcing, PO Creation & ETA Management',
    defaultUser: { fullName: 'Do Quang Huy', initials: 'QH', email: 'purchaser@nguyenkhoa.vn' }
  }
};

/**
 * Danh sách Không gian Đơn hàng (Order Spaces) mặc định cho Global Space Switcher (Section III.2, III.3, III.4)
 */
export const DEFAULT_ORDER_SPACES = [
  {
    spaceKey: 'SPACE-PO-880504',
    spaceLabel: 'SPACE-PO-880504 (Dining Sets - Ashley US)',
    customerName: 'Ashley Furniture Homestore (USA)',
    poNumber: 'ASH-2026-OCT-880504',
    orderDate: '2026-10-12',
    etdDate: '2026-10-28',
    daysRemaining: 8,
    isUrgentEtd: true,
    totalOrderedUnits: 500,
    lineItemsVolumeSummary: '500 Sets (Chairs + Tables)',
    lifecycleStatusBadge: 'IN_PRODUCTION',
    overallProgressPct: 62
  },
  {
    spaceKey: 'SPACE-PO-880506',
    spaceLabel: 'SPACE-PO-880506 (Coffee Tables - JYSK DK)',
    customerName: 'JYSK Nordic (Denmark)',
    poNumber: 'JYSK-DK-880506',
    orderDate: '2026-10-14',
    etdDate: '2026-11-05',
    daysRemaining: 16,
    isUrgentEtd: false,
    totalOrderedUnits: 250,
    lineItemsVolumeSummary: '250 Pcs Oak Coffee Table',
    lifecycleStatusBadge: 'IN_PRODUCTION',
    overallProgressPct: 45
  },
  {
    spaceKey: 'SPACE-PO-880507',
    spaceLabel: 'SPACE-PO-880507 (Bed Frames - IKEA AU)',
    customerName: 'IKEA Supply AG (Australia)',
    poNumber: 'IKEA-AU-880507',
    orderDate: '2026-10-17',
    etdDate: '2026-11-20',
    daysRemaining: 31,
    isUrgentEtd: false,
    totalOrderedUnits: 300,
    lineItemsVolumeSummary: '300 Pcs Queen Bed Frame',
    lifecycleStatusBadge: 'BOM_SETTING',
    overallProgressPct: 0
  },
  {
    spaceKey: 'SPACE-PO-880512',
    spaceLabel: 'SPACE-PO-880512 (Wood Stools - Nitori JP)',
    customerName: 'Nitori Holdings (Japan)',
    poNumber: 'NIT-JP-880512',
    orderDate: '2026-10-19',
    etdDate: '2026-12-10',
    daysRemaining: 51,
    isUrgentEtd: false,
    totalOrderedUnits: 400,
    lineItemsVolumeSummary: '400 Pcs Solid Wood Stools',
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
    primaryRoleLabel: 'WORKSHOP OWNER',
    operationalScope: 'BOM Breakdown, Purchasing Approval, Dispatch Release',
    status: 'ACTIVE'
  },
  {
    userId: 2,
    fullName: 'Tran Thi Mai',
    email: 'sales@nguyenkhoa.vn',
    primaryRole: 'SALES',
    primaryRoleLabel: 'SALES STAFF',
    operationalScope: 'Excel PO Staging, CRUD Sanitization, Space Init',
    status: 'ACTIVE'
  },
  {
    userId: 3,
    fullName: 'Pham Van Tien',
    email: 'leader@nguyenkhoa.vn',
    primaryRole: 'LEADER',
    primaryRoleLabel: 'PRODUCTION LEADER',
    operationalScope: 'Kanban Floor Dispatch, Overdriver Requests',
    status: 'ACTIVE'
  },
  {
    userId: 4,
    fullName: 'Hoang Thi Lan',
    email: 'qc@nguyenkhoa.vn',
    primaryRole: 'QC',
    primaryRoleLabel: 'QC STAFF',
    operationalScope: 'Workstation Quality Gate, Defect Logging',
    status: 'ACTIVE'
  },
  {
    userId: 5,
    fullName: 'Vu Duc Thang',
    email: 'warehouse@nguyenkhoa.vn',
    primaryRole: 'WAREHOUSE',
    primaryRoleLabel: 'WAREHOUSE KEEPER',
    operationalScope: 'Goods Receipt Commit, Goods Issue Floor Handover',
    status: 'ACTIVE'
  },
  {
    userId: 6,
    fullName: 'Do Quang Huy',
    email: 'purchaser@nguyenkhoa.vn',
    primaryRole: 'PURCHASING',
    primaryRoleLabel: 'PURCHASING STAFF',
    operationalScope: 'Vendor Sourcing, PO Creation & ETA Management',
    status: 'ACTIVE'
  },
  {
    userId: 7,
    fullName: 'Nguyen Van Tam',
    email: 'tam.ex@nguyenkhoa.vn',
    primaryRole: 'APPRENTICE',
    primaryRoleLabel: 'APPRENTICE STAFF',
    operationalScope: 'Temporary Observation',
    status: 'SUSPENDED'
  }
];

/**
 * Ma trận cấu hình phân quyền theo vai trò (Role Permission Configuration Matrix MOD-ADM-02 - Section III.3.3)
 */
export const ROLE_PERMISSION_MATRIX_ROWS = [
  {
    functionalDomain: 'BOM Breakdown',
    actionScope: 'Configure routing & scrap %',
    workshopOwner: { label: 'FULL', type: 'full' },
    productionLeader: { label: 'NO', type: 'no' },
    qcStaff: { label: 'NO', type: 'no' },
    warehouse: { label: 'NO', type: 'no' }
  },
  {
    functionalDomain: 'PR Approval',
    actionScope: 'Approve daily consolidated PR',
    workshopOwner: { label: 'FULL', type: 'full' },
    productionLeader: { label: 'NO', type: 'no' },
    qcStaff: { label: 'NO', type: 'no' },
    warehouse: { label: 'NO', type: 'no' }
  },
  {
    functionalDomain: 'Kanban Dispatch',
    actionScope: 'Move lot across stations',
    workshopOwner: { label: 'MONITOR', type: 'monitor' },
    productionLeader: { label: 'FULL (100% RULE)', type: 'full' },
    qcStaff: { label: 'NO', type: 'no' },
    warehouse: { label: 'NO', type: 'no' }
  },
  {
    functionalDomain: 'Quality Gate',
    actionScope: 'Record Pass / Rework / Scrap',
    workshopOwner: { label: 'NO', type: 'no' },
    productionLeader: { label: 'NO', type: 'no' },
    qcStaff: { label: 'FULL', type: 'full' },
    warehouse: { label: 'NO', type: 'no' }
  },
  {
    functionalDomain: 'Overdriver',
    actionScope: 'Authorize Hold Point bypass',
    workshopOwner: { label: 'EXCLUSIVE APPROVE', type: 'full' },
    productionLeader: { label: 'REQUEST ONLY', type: 'request' },
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
    targetEntity: 'Batch #LOT-880504-01',
    stateDelta: 'LOCKED_WAITING_MAT → OVERDRIVER_RUNNING',
    reason: '"Customer urgent ship request (ETD: Oct 28). Missing hinges. Approved rough milling up to Assembly Hold Point."'
  },
  {
    logId: 102,
    timestamp: '2026-10-20 08:30:10',
    actorName: 'Tran Thi Mai',
    actorRole: 'SALES',
    actionType: 'CONFIRM_PO_STAGING',
    targetEntity: 'Order Space #SPACE-880507',
    stateDelta: 'STAGING_RAW → NEWLY_RECEIVED',
    reason: '"Cleaned 4 merged cell format errors. Converted inches to mm. Generated Order Space for 300 bed frames."'
  },
  {
    logId: 103,
    timestamp: '2026-10-19 16:45:00',
    actorName: 'Nguyen Van Khoa',
    actorRole: 'OWNER',
    actionType: 'APPROVE_SCRAP_REPLACEMENT',
    targetEntity: 'Order #PO-880506',
    stateDelta: 'SCRAP_PENDING → SUB_BATCH_ISSUED',
    reason: '"QC reported 3 broken mortise parts at Assembly. Released replacement job for 3 table legs."'
  },
  {
    logId: 104,
    timestamp: '2026-10-19 14:12:44',
    actorName: 'Vu Duc Thang',
    actorRole: 'WAREHOUSE',
    actionType: 'COMMIT_GOODS_RECEIPT',
    targetEntity: 'PO-MAT-2026-042',
    stateDelta: 'IN_TRANSIT → RECEIVED_COMMITTED',
    reason: '"Received 500 pcs Oak boards from Supplier Hoang Thong. Auto-allocated 320 pcs to Order 880504 by ETD priority."'
  },
  {
    logId: 105,
    timestamp: '2026-10-19 11:05:18',
    actorName: 'Nguyen Van Khoa',
    actorRole: 'OWNER',
    actionType: 'APPROVE_CONSOLIDATED_PR',
    targetEntity: 'Daily PR-2026-10-19',
    stateDelta: 'DRAFT_PR → APPROVED_FOR_PURCHASING',
    reason: '"Aggregated material deficits across 3 daily orders. Endorsed 1,200m board and 4,000 screws."'
  }
];

/**
 * Dữ liệu mặc định cho bảng Safe Excel PO Staging Grid (SCR-ORD-02 - Section III.4.2)
 */
export const INITIAL_STAGING_GRID_ROWS = [
  {
    rowNumber: 1,
    productSku: 'CHAIR-OAK-01',
    productDescription: 'Nordic Oak Dining Chair',
    dimensionsMm: '450 x 520 x 850',
    finishColor: 'Natural PU Clear',
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
    productDescription: 'Rectangular Dining Table 1.8m',
    dimensionsMm: '1800 x 900 x 750',
    finishColor: 'Walnut Stain',
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
    productDescription: 'Solid Oak Dining Bench',
    dimensionsMm: '1400 x 380 x 450',
    finishColor: 'Natural PU Clear',
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
    productDescription: 'Compact Kitchen Stool',
    dimensionsMm: '380 x 380 x 450',
    finishColor: 'Natural Finish',
    orderQty: 150,
    targetEtd: '2026-11-20',
    fscRequired: true,
    technicalDrawingName: 'CAD_Stool_V1.pdf',
    isRowValid: true,
    cellErrors: []
  }
];
