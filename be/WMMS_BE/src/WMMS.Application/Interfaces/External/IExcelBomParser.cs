using WMMS.Application.DTOs.BomRouting;

namespace WMMS.Application.Interfaces.External;

public interface IExcelBomParser
{
    Task<BomStagingPreviewDto> ParseBomExcelAsync(Stream fileStream, string fileName);
}
