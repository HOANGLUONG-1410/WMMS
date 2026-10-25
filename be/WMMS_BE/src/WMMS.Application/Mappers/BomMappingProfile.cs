using AutoMapper;
using WMMS.Application.DTOs.BomRouting;
using WMMS.Domain.Entities.CatalogBom;

namespace WMMS.Application.Mappers;

public class BomMappingProfile : Profile
{
    public BomMappingProfile()
    {
        CreateMap<BomHeader, BomDto>()
            .ForMember(dest => dest.ProductSku, opt => opt.MapFrom(src => src.Product != null ? src.Product.ProductSku : string.Empty))
            .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product != null ? src.Product.ProductName : string.Empty))
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Product != null ? src.Product.CategoryName : string.Empty))
            .ForMember(dest => dest.DimensionsCm, opt => opt.MapFrom(src => src.Product != null ? src.Product.DimensionsCm : string.Empty))
            .ForMember(dest => dest.OrderedQty, opt => opt.MapFrom(src => src.OrderItem != null ? src.OrderItem.OrderedQty : 0))
            .ForMember(dest => dest.CreatorName, opt => opt.MapFrom(src => src.Creator != null ? src.Creator.FullName : null))
            .ForMember(dest => dest.BomDetails, opt => opt.MapFrom(src => src.BomDetails));

        CreateMap<BomDetail, BomDetailDto>()
            .ForMember(dest => dest.MaterialCode, opt => opt.MapFrom(src => src.Material != null ? src.Material.MaterialCode : string.Empty))
            .ForMember(dest => dest.MaterialName, opt => opt.MapFrom(src => src.Material != null ? src.Material.MaterialName : string.Empty))
            .ForMember(dest => dest.MaterialGroup, opt => opt.MapFrom(src => src.Material != null ? src.Material.MaterialGroup.ToString() : string.Empty))
            .ForMember(dest => dest.UnitOfMeasure, opt => opt.MapFrom(src => src.Material != null ? src.Material.UnitOfMeasure : string.Empty))
            .ForMember(dest => dest.DimensionSpec, opt => opt.MapFrom(src => src.Material != null ? src.Material.DimensionSpec : null))
            .ForMember(dest => dest.FscCertified, opt => opt.MapFrom(src => src.Material != null && src.Material.FscCertified));

        CreateMap<RoutingStage, RoutingStageDto>();

        CreateMap<Product, VisualCatalogProductDto>()
            .ForMember(dest => dest.HasActiveBom, opt => opt.MapFrom(src => src.BomHeaders.Any(b => b.BomStatus == Domain.Enums.BomStatusEnum.READY_FOR_PRODUCTION || b.BomStatus == Domain.Enums.BomStatusEnum.DRAFT)))
            .ForMember(dest => dest.DefaultRoutingStages, opt => opt.MapFrom(src => src.RoutingTemplates.FirstOrDefault(r => r.IsActive) != null ? src.RoutingTemplates.FirstOrDefault(r => r.IsActive)!.RoutingStages : new List<RoutingStage>()));
    }
}
