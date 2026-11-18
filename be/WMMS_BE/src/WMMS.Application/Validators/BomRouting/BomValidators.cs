using FluentValidation;
using WMMS.Application.DTOs.BomRouting;

namespace WMMS.Application.Validators.BomRouting;

public class CreateBomRequestValidator : AbstractValidator<CreateBomRequestDto>
{
    public CreateBomRequestValidator()
    {
        RuleFor(x => x.OrderItemId)
            .GreaterThan(0)
            .WithMessage("Vui lòng chọn dòng sản phẩm đơn hàng hợp lệ.");

        RuleFor(x => x.ProductId)
            .GreaterThan(0)
            .WithMessage("Vui lòng chọn sản phẩm hợp lệ.");

        RuleFor(x => x.ScrapRatePercent)
            .InclusiveBetween(3.00m, 15.00m)
            .WithMessage("Tỷ lệ hao hụt tự nhiên phải nằm trong khoảng từ 3% đến 15%.");

        RuleFor(x => x.Details)
            .NotEmpty()
            .WithMessage("Định mức vật tư (BOM) phải có ít nhất một dòng nguyên vật liệu.");

        RuleForEach(x => x.Details).ChildRules(detail =>
        {
            detail.RuleFor(d => d.MaterialId)
                .GreaterThan(0)
                .WithMessage("Vui lòng chọn nguyên vật liệu hợp lệ.");

            detail.RuleFor(d => d.NetQtyPerUnit)
                .GreaterThan(0)
                .WithMessage("Định mức tiêu hao trên mỗi đơn vị sản phẩm phải lớn hơn 0.");
        });
    }
}

public class UpdateBomRequestValidator : AbstractValidator<UpdateBomRequestDto>
{
    public UpdateBomRequestValidator()
    {
        RuleFor(x => x.ScrapRatePercent)
            .InclusiveBetween(3.00m, 15.00m)
            .WithMessage("Tỷ lệ hao hụt tự nhiên phải nằm trong khoảng từ 3% đến 15%.");

        RuleFor(x => x.Details)
            .NotEmpty()
            .WithMessage("Định mức vật tư (BOM) phải có ít nhất một dòng nguyên vật liệu.");

        RuleForEach(x => x.Details).ChildRules(detail =>
        {
            detail.RuleFor(d => d.MaterialId)
                .GreaterThan(0)
                .WithMessage("Vui lòng chọn nguyên vật liệu hợp lệ.");

            detail.RuleFor(d => d.NetQtyPerUnit)
                .GreaterThan(0)
                .WithMessage("Định mức tiêu hao trên mỗi đơn vị sản phẩm phải lớn hơn 0.");
        });
    }
}

public class CalculateDemandRequestValidator : AbstractValidator<CalculateDemandRequestDto>
{
    public CalculateDemandRequestValidator()
    {
        RuleFor(x => x.OrderedQty)
            .GreaterThan(0)
            .WithMessage("Số lượng sản phẩm đặt hàng phải lớn hơn 0.");

        RuleFor(x => x.ScrapRatePercent)
            .InclusiveBetween(3.00m, 15.00m)
            .WithMessage("Tỷ lệ hao hụt tự nhiên phải nằm trong khoảng từ 3% đến 15%.");

        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("Danh sách nguyên vật liệu cần tính toán không được để trống.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.MaterialId)
                .GreaterThan(0)
                .WithMessage("Vui lòng chọn nguyên vật liệu hợp lệ.");

            item.RuleFor(i => i.NetQtyPerUnit)
                .GreaterThan(0)
                .WithMessage("Định mức tiêu hao trên mỗi đơn vị sản phẩm phải lớn hơn 0.");
        });
    }
}

public class ConfirmBomStagingRequestValidator : AbstractValidator<ConfirmBomStagingRequestDto>
{
    public ConfirmBomStagingRequestValidator()
    {
        RuleFor(x => x.OrderItemId)
            .GreaterThan(0)
            .WithMessage("Vui lòng chọn dòng sản phẩm đơn hàng hợp lệ.");

        RuleFor(x => x.ProductId)
            .GreaterThan(0)
            .WithMessage("Vui lòng chọn sản phẩm hợp lệ.");

        RuleFor(x => x.ScrapRatePercent)
            .InclusiveBetween(3.00m, 15.00m)
            .WithMessage("Tỷ lệ hao hụt tự nhiên phải nằm trong khoảng từ 3% đến 15%.");

        RuleFor(x => x.Rows)
            .NotEmpty()
            .WithMessage("Danh sách dòng dữ liệu xem trước không được để trống.");
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
            .WithMessage("Quy trình sản xuất phải có ít nhất một công đoạn gia công.");

        RuleForEach(x => x.Stages).ChildRules(stage =>
        {
            stage.RuleFor(s => s.StageSequence)
                .GreaterThan(0)
                .WithMessage("Thứ tự công đoạn phải lớn hơn 0.");

            stage.RuleFor(s => s.StageName)
                .NotEmpty()
                .WithMessage("Tên công đoạn gia công không được để trống.");
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
            .WithMessage("Thứ tự các công đoạn gia công phải liên tục từ 1 trở đi, không được ngắt quãng hoặc trùng lặp.");
    }
}
