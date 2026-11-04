using FluentValidation;
using WMMS.Application.DTOs.BomRouting;

namespace WMMS.Application.Validators.BomRouting;

public class CreateBomRequestValidator : AbstractValidator<CreateBomRequestDto>
{
    public CreateBomRequestValidator()
    {
        RuleFor(x => x.OrderItemId)
            .GreaterThan(0)
            .WithMessage("OrderItemId must be a positive integer.");

        RuleFor(x => x.ProductId)
            .GreaterThan(0)
            .WithMessage("ProductId must be a positive integer.");

        RuleFor(x => x.ScrapRatePercent)
            .InclusiveBetween(3.00m, 15.00m)
            .WithMessage("Scrap rate percentage (BR-002) must be between 3.00% and 15.00%.");

        RuleFor(x => x.Details)
            .NotEmpty()
            .WithMessage("BOM must contain at least one material detail line.");

        RuleForEach(x => x.Details).ChildRules(detail =>
        {
            detail.RuleFor(d => d.MaterialId)
                .GreaterThan(0)
                .WithMessage("MaterialId must be greater than 0.");

            detail.RuleFor(d => d.NetQtyPerUnit)
                .GreaterThan(0)
                .WithMessage("NetQtyPerUnit must be greater than 0.");
        });
    }
}

public class UpdateBomRequestValidator : AbstractValidator<UpdateBomRequestDto>
{
    public UpdateBomRequestValidator()
    {
        RuleFor(x => x.ScrapRatePercent)
            .InclusiveBetween(3.00m, 15.00m)
            .WithMessage("Scrap rate percentage (BR-002) must be between 3.00% and 15.00%.");

        RuleFor(x => x.Details)
            .NotEmpty()
            .WithMessage("BOM must contain at least one material detail line.");

        RuleForEach(x => x.Details).ChildRules(detail =>
        {
            detail.RuleFor(d => d.MaterialId)
                .GreaterThan(0)
                .WithMessage("MaterialId must be greater than 0.");

            detail.RuleFor(d => d.NetQtyPerUnit)
                .GreaterThan(0)
                .WithMessage("NetQtyPerUnit must be greater than 0.");
        });
    }
}

public class CalculateDemandRequestValidator : AbstractValidator<CalculateDemandRequestDto>
{
    public CalculateDemandRequestValidator()
    {
        RuleFor(x => x.OrderedQty)
            .GreaterThan(0)
            .WithMessage("OrderedQty must be greater than 0.");

        RuleFor(x => x.ScrapRatePercent)
            .InclusiveBetween(3.00m, 15.00m)
            .WithMessage("Scrap rate percentage (BR-002) must be between 3.00% and 15.00%.");

        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("Items list cannot be empty for demand calculation.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.MaterialId)
                .GreaterThan(0)
                .WithMessage("MaterialId must be greater than 0.");

            item.RuleFor(i => i.NetQtyPerUnit)
                .GreaterThan(0)
                .WithMessage("NetQtyPerUnit must be greater than 0.");
        });
    }
}

public class ConfirmBomStagingRequestValidator : AbstractValidator<ConfirmBomStagingRequestDto>
{
    public ConfirmBomStagingRequestValidator()
    {
        RuleFor(x => x.OrderItemId)
            .GreaterThan(0)
            .WithMessage("OrderItemId must be a positive integer.");

        RuleFor(x => x.ProductId)
            .GreaterThan(0)
            .WithMessage("ProductId must be a positive integer.");

        RuleFor(x => x.ScrapRatePercent)
            .InclusiveBetween(3.00m, 15.00m)
            .WithMessage("Scrap rate percentage (BR-002) must be between 3.00% and 15.00%.");

        RuleFor(x => x.Rows)
            .NotEmpty()
            .WithMessage("Staging rows cannot be empty.");
    }
}

/// <summary>
/// Validator cấu hình Dynamic Routing cho sản phẩm (UC-37, BR-007, Report 4 BOM01).
/// </summary>
public class RoutingConfigValidator : AbstractValidator<RoutingConfigDto>
{
    public RoutingConfigValidator()
    {
        RuleFor(x => x.Stages)
            .NotEmpty()
            .WithMessage("Workstation routing stages cannot be empty.");

        RuleForEach(x => x.Stages).ChildRules(stage =>
        {
            stage.RuleFor(s => s.StageSequence)
                .GreaterThan(0)
                .WithMessage("StageSequence must be greater than 0.");

            stage.RuleFor(s => s.StageName)
                .NotEmpty()
                .WithMessage("StageName cannot be empty.");
        });

        RuleFor(x => x.Stages)
            .Must(stages =>
            {
                if (stages == null || !stages.Any()) return true;
                var ordered = stages.OrderBy(s => s.StageSequence).ToList();
                for (int i = 0; i < ordered.Count; i++)
                {
                    if (ordered[i].StageSequence != i + 1) return false;
                }
                return true;
            })
            .WithMessage("Workstation stages must form a continuous sequence starting from 1 without gaps or duplicates (BR-007, UC-37).");
    }
}
