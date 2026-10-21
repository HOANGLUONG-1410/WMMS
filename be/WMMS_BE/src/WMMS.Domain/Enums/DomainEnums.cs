namespace WMMS.Domain.Enums;

public enum UserStatusEnum
{
    ACTIVE = 1,
    INACTIVE = 2,
    BANNED = 3,
    LOCKED = 4
}

public enum OrderStatusEnum
{
    CONFIRMED = 1,
    IN_PLANNING = 2,
    IN_PRODUCTION = 3,
    COMPLETED = 4,
    CANCELLED = 5
}

public enum SpaceStatusEnum
{
    ACTIVE = 1,
    ARCHIVED = 2
}

public enum OrderItemStatusEnum
{
    PENDING_BOM = 1,
    BOM_DRAFT = 2,
    WAITING_MAT = 3,
    READY_PROD = 4,
    IN_PROD = 5,
    DONE = 6
}

public enum BomStatusEnum
{
    DRAFT = 1,
    WAITING_FOR_MATERIALS = 2,
    READY_FOR_PRODUCTION = 3
}

public enum MaterialGroupEnum
{
    LUMBER = 1,
    HARDWARE = 2,
    FINISH_PAINT = 3,
    PACKAGING = 4
}

public enum AllocationStatusEnum
{
    RESERVED = 1,
    ISSUED_TO_SHOP = 2,
    RELEASED = 3
}

public enum PrStatusEnum
{
    PENDING_APPROVAL = 1,
    APPROVED = 2,
    REJECTED = 3,
    PO_ISSUED = 4
}

public enum PoStatusEnum
{
    ISSUED = 1,
    PARTIALLY_RECEIVED = 2,
    FULFILLED = 3,
    CANCELLED = 4
}

public enum GrnStatusEnum
{
    COMMITTED = 1,
    AUTO_ALLOCATED = 2
}

public enum InventoryTransactionTypeEnum
{
    IN_RECEIPT = 1,
    OUT_ISSUE = 2,
    ADJUST = 3
}

public enum BatchTypeEnum
{
    MAIN_CARD = 1,
    REWORK_SUB_CARD = 2,
    REPLENISH_LOT = 3
}

public enum BatchStatusEnum
{
    LOCKED = 1,
    IN_PROGRESS = 2,
    QC_PENDING = 3,
    HOLD_POINT_BLOCKED = 4,
    DONE = 5
}

public enum StepStatusEnum
{
    WAITING = 1,
    IN_PROGRESS = 2,
    QC_PENDING = 3,
    PASSED = 4,
    BLOCKED = 5
}

public enum QcResultStatusEnum
{
    FULL_PASS_100 = 1,
    BRANCHED_PARTIAL = 2,
    FULL_REJECT = 3
}

public enum DefectDispositionEnum
{
    REWORK_SUB_CARD = 1,
    SCRAP_REPLENISH = 2
}
