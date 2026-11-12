namespace WMMS.Domain.Exceptions;

public class DomainException : Exception
{
    public string ErrorCode { get; }

    public DomainException(string message, string errorCode = "DOMAIN_ERROR") : base(message)
    {
        ErrorCode = errorCode;
    }
}

public class NotFoundException : DomainException
{
    public NotFoundException(string message) : base(message, "NOT_FOUND")
    {
    }
}

public class InvalidScrapRateException : DomainException
{
    public InvalidScrapRateException(string message) : base(message, "BR_002_INVALID_SCRAP_RATE")
    {
    }
}

public class ActiveBomConflictException : DomainException
{
    public ActiveBomConflictException(string message) : base(message, "BR_001_ACTIVE_BOM_CONFLICT")
    {
    }
}

public class StagingValidationException : DomainException
{
    public StagingValidationException(string message) : base(message, "BR_011_STAGING_VALIDATION_ERROR")
    {
    }
}

/// <summary>
/// Ngoại lệ chặn thẻ Lô sản xuất khi vi phạm chốt kiểm soát trạm Hold Point (SC-20, KBN02).
/// </summary>
public class HoldPointLockedException : DomainException
{
    public int StageSequence { get; }
    public string StageName { get; }

    public HoldPointLockedException(string message, int stageSequence = 0, string stageName = "")
        : base(message, "HOLD_POINT_LOCKED")
    {
        StageSequence = stageSequence;
        StageName = stageName;
    }
}

/// <summary>
/// Ngoại lệ chặn chuyển công đoạn khi số lượng nghiệm thu QC chưa đạt 100% (SC-18, SC-20, KBN01).
/// </summary>
public class InsufficientQcPassException : DomainException
{
    public int RequiredQty { get; }
    public int PassedQty { get; }

    public InsufficientQcPassException(string message, int requiredQty = 0, int passedQty = 0)
        : base(message, "INSUFFICIENT_QC_PASS")
    {
        RequiredQty = requiredQty;
        PassedQty = passedQty;
    }
}
