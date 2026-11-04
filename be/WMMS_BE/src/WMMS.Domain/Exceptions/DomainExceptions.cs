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
