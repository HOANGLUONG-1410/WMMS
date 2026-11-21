using FluentValidation;
using WMMS.Application.DTOs.BomRouting;

namespace WMMS.Application.Validators.BomRouting;

public class CreateBomRequestValidator : AbstractValidator<CreateBomRequestDto>
{
    public CreateBomRequestValidator()
    {
        RuleFor(x => x.OrderItemId)
            .GreaterThan(0)
            .WithMessage("Mã dòng sản phẩm đơn hàng (OrderItemId) phải là số nguyên dương.");

        RuleFor(x => x.ProductId)
            .GreaterThan(0)
            .WithMessage("Mã sản phẩm (ProductId) phải là số nguyên dương.");

        RuleFor(x => x.ScrapRatePercent)
            .InclusiveBetween(3.00m, 15.00m)
            .WithMessage("Tỷ lệ hao hụt dự phòng (BR-002) phải nằm trong khoảng từ 3.00% đến 15.00%.");

        RuleFor(x => x.Details)
            .NotEmpty()
            .WithMessage("Định mức BOM phải chứa ít nhất một dòng vật tư.");

        RuleForEach(x => x.Details).ChildRules(detail =>
        {
            detail.RuleFor(d => d.MaterialId)
                .GreaterThan(0)
                .WithMessage("Mã vật tư (MaterialId) phải lớn hơn 0.");

            detail.RuleFor(d => d.NetQtyPerUnit)
                .GreaterThan(0)
                .WithMessage("Định mức tinh trên 1 sản phẩm phải lớn hơn 0.");
        });
    }
}

public class UpdateBomRequestValidator : AbstractValidator<UpdateBomRequestDto>
{
    public UpdateBomRequestValidator()
    {
        RuleFor(x => x.ScrapRatePercent)
            .InclusiveBetween(3.00m, 15.00m)
            .WithMessage("Tỷ lệ hao hụt dự phòng (BR-002) phải nằm trong khoảng từ 3.00% đến 15.00%.");

        RuleFor(x => x.Details)
            .NotEmpty()
            .WithMessage("Định mức BOM phải chứa ít nhất một dòng vật tư.");

        RuleForEach(x => x.Details).ChildRules(detail =>
        {
            detail.RuleFor(d => d.MaterialId)
                .GreaterThan(0)
                .WithMessage("Mã vật tư (MaterialId) phải lớn hơn 0.");

            detail.RuleFor(d => d.NetQtyPerUnit)
                .GreaterThan(0)
                .WithMessage("Định mức tinh trên 1 sản phẩm phải lớn hơn 0.");
        });
    }
}

public class CalculateDemandRequestValidator : AbstractValidator<CalculateDemandRequestDto>
{
    public CalculateDemandRequestValidator()
    {
        RuleFor(x => x.OrderedQty)
            .GreaterThan(0)
            .WithMessage("Số lượng đặt hàng phải lớn hơn 0.");

        RuleFor(x => x.ScrapRatePercent)
            .InclusiveBetween(3.00m, 15.00m)
            .WithMessage("Tỷ lệ hao hụt dự phòng (BR-002) phải nằm trong khoảng từ 3.00% đến 15.00%.");

        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("Danh sách vật tư tính nhu cầu không được để trống.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.MaterialId)
                .GreaterThan(0)
                .WithMessage("Mã vật tư (MaterialId) phải lớn hơn 0.");

            item.RuleFor(i => i.NetQtyPerUnit)
                .GreaterThan(0)
                .WithMessage("Định mức tinh trên 1 sản phẩm phải lớn hơn 0.");
        });
    }
}

public class ConfirmBomStagingRequestValidator : AbstractValidator<ConfirmBomStagingRequestDto>
{
    public ConfirmBomStagingRequestValidator()
    {
        RuleFor(x => x.OrderItemId)
            .GreaterThan(0)
            .WithMessage("Mã dòng sản phẩm đơn hàng (OrderItemId) phải là số nguyên dương.");

        RuleFor(x => x.ProductId)
            .GreaterThan(0)
            .WithMessage("Mã sản phẩm (ProductId) phải là số nguyên dương.");

        RuleFor(x => x.ScrapRatePercent)
            .InclusiveBetween(3.00m, 15.00m)
            .WithMessage("Tỷ lệ hao hụt dự phòng (BR-002) phải nằm trong khoảng từ 3.00% đến 15.00%.");

        RuleFor(x => x.Rows)
            .NotEmpty()
            .WithMessage("Dữ liệu bảng tạm (Staging) định mức BOM không được để trống.");
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
            .WithMessage("Danh sách công đoạn sản xuất (Routing) không được để trống.");

        RuleForEach(x => x.Stages).ChildRules(stage =>
        {
            stage.RuleFor(s => s.StageSequence)
                .GreaterThan(0)
                .WithMessage("Số thứ tự công đoạn phải lớn hơn 0.");

            stage.RuleFor(s => s.StageName)
                .NotEmpty()
                .WithMessage("Tên công đoạn không được để trống.");
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
            .WithMessage("Thứ tự các công đoạn phải tạo thành chuỗi liên tục bắt đầu từ 1, không ngắt quãng hoặc trùng lặp (BR-007, UC-37).");
    }
}
