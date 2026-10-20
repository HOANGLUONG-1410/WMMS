namespace WMMS.Domain.Constants;

public static class RoleConstants
{
    public const string Admin = "ADMIN";
    public const string Owner = "OWNER";
    public const string Sales = "SALES";
    public const string Planner = "PLANNER";
    public const string Purchasing = "PURCHASING";
    public const string Warehouse = "WAREHOUSE";
    public const string Leader = "LEADER";
    public const string Qc = "QC";

    public static readonly IReadOnlyList<string> AllRoles = new[]
    {
        Admin, Owner, Sales, Planner, Purchasing, Warehouse, Leader, Qc
    };
}

public static class DefectCodeConstants
{
    public const string CutErr01 = "ERR_CUT_01";
    public const string WoodErr01 = "ERR_WOOD_01";
    public const string SurfErr02 = "ERR_SURF_02";
    public const string AssyErr03 = "ERR_ASSY_03";
    public const string PaintErr04 = "ERR_PAINT_04";
}

public static class SystemConfigKeys
{
    public const string DefaultScrapMin = "DEFAULT_SCRAP_MIN";
    public const string DefaultScrapMax = "DEFAULT_SCRAP_MAX";
    public const string EtdWarningDays = "ETD_WARNING_DAYS";
}

public static class BusinessRuleConstants
{
    public const string Br001ActiveBomUniqueness = "BR-001";
    public const string Br002NaturalScrapRateRange = "BR-002";
    public const string Br011BomStagingImport = "BR-011";
}

