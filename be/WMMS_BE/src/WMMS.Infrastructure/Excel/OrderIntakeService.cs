using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using WMMS.Application.DTOs.Orders;
using WMMS.Application.Interfaces.External;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Constants;
using WMMS.Domain.Entities.CatalogBom;
using WMMS.Domain.Entities.Identity;
using WMMS.Domain.Entities.Orders;
using WMMS.Domain.Enums;
using WMMS.Domain.Exceptions;
using WMMS.Infrastructure.Persistence;

namespace WMMS.Infrastructure.Excel;

/// <summary>
/// Triển khai Dịch vụ Tiếp nhận Đơn hàng, Phân tích Excel PO trên Staging Grid &amp; Khởi tạo Order Space (Phân hệ ORD - BF-01, UC-30 đến UC-35).
/// Tuân thủ đầy đủ quy tắc nghiệp vụ:
/// - BR-ORD-01: Tự động loại bỏ các trường thương mại/vận tải quốc tế (Container No, Shipping Port, Proforma Invoice PI, Unit Prices CBD).
/// - BR-ORD-02: Kiểm tra định dạng từng ô (SKU, Kích thước L x W x H mm, Order Qty > 0, ETD hợp lệ) và chặn Confirm nếu còn bất kỳ ô lỗi đỏ nào.
/// - BR-ORD-03: Giao dịch nguyên tử tạo <c>sales_orders</c>, <c>order_items</c>, khởi tạo <c>order_spaces</c> độc lập và ghi <c>audit_logs</c>.
/// - BR-ORD-04: Phân tách quyền Chỉ xem (View-only) đối với Material Planning Staff trong Order Space.
/// </summary>
public class OrderIntakeService : IOrderIntakeService
{
    private static readonly Regex DimensionRegex = new(
        @"^\s*(?<L>\d+(?:\.\d+)?)\s*[xX\*×]\s*(?<W>\d+(?:\.\d+)?)\s*[xX\*×]\s*(?<H>\d+(?:\.\d+)?)\s*(?:mm|cm)?\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly WmmsDbContext _context;
    private readonly IExcelOrderParser _excelOrderParser;
    private readonly INotificationHubService? _notificationHubService;

    /// <summary>Đường dẫn thư mục gốc <c>wwwroot</c> do tầng API cấu hình nhằm lưu ảnh bản vẽ đúng vị trí phục vụ tệp tĩnh.</summary>
    public static string? ConfiguredWebRootPath { get; set; }

    public OrderIntakeService(
        WmmsDbContext context,
        IExcelOrderParser? excelOrderParser = null,
        INotificationHubService? notificationHubService = null)
    {
        _context = context;
        _excelOrderParser = excelOrderParser ?? new ExcelOrderParser();
        _notificationHubService = notificationHubService;
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    /// <inheritdoc />
    public async Task<StagingOrderGridResponseDto> ParseExcelPoToStagingGridAsync(
        Stream? excelStream,
        string fileName,
        string? poNumberOverride,
        long? customerIdOverride,
        CancellationToken cancellationToken = default)
    {
        var extractedRows = new List<StagingOrderRowDto>();
        var detectedPoNumber = !string.IsNullOrWhiteSpace(poNumberOverride) ? poNumberOverride.Trim() : ExtractPoNumberFromFileName(fileName);
        var detectedCustomerName = "Target Home US / Ashley Furniture";
        var sheetName = "OrderDetails";
        ExcelOrderParseResultDto? parsedExcel = null;

        if (excelStream is not null)
        {
            if (excelStream.Length == 0)
            {
                throw new AppException(
                    "Tệp bảng tính tải lên rỗng (0 bytes). Vui lòng chọn tệp Excel hợp lệ có nội dung.",
                    400,
                    MessageCodes.Msg13ValidationError);
            }

            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            if (ext is not ".xlsx" and not ".xls")
            {
                throw new AppException(
                    "Định dạng tệp bảng tính không hợp lệ. Vui lòng chọn tệp .xlsx hoặc .xls có dung lượng dưới 20MB.",
                    400,
                    MessageCodes.Msg13ValidationError);
            }

            try
            {
                parsedExcel = await _excelOrderParser.ParseOrderExcelAsync(excelStream, fileName, cancellationToken);
                sheetName = parsedExcel.SheetName;
                extractedRows = parsedExcel.Rows;

                if (string.IsNullOrWhiteSpace(poNumberOverride) && !string.IsNullOrWhiteSpace(parsedExcel.DetectedPoNumber))
                {
                    detectedPoNumber = parsedExcel.DetectedPoNumber;
                }

                if (!string.IsNullOrWhiteSpace(parsedExcel.DetectedCustomerName))
                {
                    detectedCustomerName = parsedExcel.DetectedCustomerName;
                }
            }
            catch (AppException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new AppException(
                    $"Không thể đọc tệp Excel đơn hàng: {ex.Message}",
                    400,
                    MessageCodes.Msg13ValidationError);
            }
        }

        // Nếu người dùng tải lên file Excel nhưng không bóc tách được dòng sản phẩm nào -> báo lỗi 400
        if (excelStream is not null && excelStream.Length > 0 && extractedRows.Count == 0)
        {
            throw new AppException(
                "Tệp Excel tải lên không chứa dòng sản phẩm hợp lệ nào. Vui lòng kiểm tra lại cấu trúc bảng đơn đặt hàng (PO).",
                400,
                MessageCodes.Msg13ValidationError);
        }

        // Nếu không truyền file (người dùng bấm nạp dữ liệu mẫu Staging từ SCR-ORD-02)
        if (extractedRows.Count == 0)
        {
            extractedRows = BuildDefaultSampleStagingRows();
        }

        Customer? customer = null;
        if (customerIdOverride.HasValue)
        {
            customer = await _context.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CustomerId == customerIdOverride.Value, cancellationToken);
        }

        var validateRequest = new ValidateStagingGridRequestDto
        {
            FileName = string.IsNullOrWhiteSpace(fileName) ? "PO_Target_Home_US_880515.xlsx" : fileName,
            PoNumber = detectedPoNumber,
            CustomerId = customer?.CustomerId,
            CustomerName = customer?.CompanyName ?? detectedCustomerName,
            Rows = extractedRows
        };

        var result = await ValidateStagingGridAsync(validateRequest, cancellationToken);
        result.SheetName = sheetName;
        result.DetectedTargetEtd = parsedExcel?.DetectedTargetEtd ?? result.Rows.FirstOrDefault()?.TargetEtd;
        result.TotalOrderQty = parsedExcel?.TotalOrderQty ?? result.Rows.Sum(r => r.OrderQty);
        result.TotalCartons = parsedExcel?.TotalCartons;
        result.TotalCbm = parsedExcel?.TotalCbm;
        result.ContainerSpec = parsedExcel?.ContainerSpec;
        if (parsedExcel?.StrippedCommercialFields is { Count: > 0 })
        {
            result.StrippedCommercialFields = parsedExcel.StrippedCommercialFields;
        }
        return result;
    }

    /// <inheritdoc />
    public async Task<StagingOrderGridResponseDto> ValidateStagingGridAsync(
        ValidateStagingGridRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var catalogProducts = await _context.Products
            .AsNoTracking()
            .Where(p => p.Status == "ACTIVE")
            .ToListAsync(cancellationToken);

        var productBySku = catalogProducts
            .GroupBy(p => p.ProductSku.Trim().ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.First());

        var validatedRows = new List<StagingOrderRowDto>();
        var totalErrorCells = 0;
        var invalidRowCount = 0;
        var matchedCatalogCount = 0;
        var newProductCount = 0;

        for (var i = 0; i < request.Rows.Count; i++)
        {
            var row = request.Rows[i];
            row.RowNumber = i + 1;
            row.CellErrors = new List<StagingCellErrorDto>();

            // 1. Kiểm tra mã SKU (Internal Product SKU)
            if (string.IsNullOrWhiteSpace(row.ProductSku))
            {
                row.CellErrors.Add(new StagingCellErrorDto
                {
                    FieldName = nameof(row.ProductSku),
                    ErrorMessage = "Mã sản phẩm (SKU) không được để trống."
                });
            }
            else
            {
                row.ProductSku = row.ProductSku.Trim().ToUpperInvariant();
            }

            // 2. Kiểm tra tên mô tả sản phẩm (Product Description)
            if (string.IsNullOrWhiteSpace(row.ProductDescription))
            {
                row.CellErrors.Add(new StagingCellErrorDto
                {
                    FieldName = nameof(row.ProductDescription),
                    ErrorMessage = "Tên / mô tả sản phẩm không được để trống."
                });
            }
            else
            {
                row.ProductDescription = row.ProductDescription.Trim();
            }

            // 3. Kiểm tra quy cách kích thước (Dimensions L x W x H mm) theo BR-ORD-02
            if (string.IsNullOrWhiteSpace(row.DimensionsMm))
            {
                row.CellErrors.Add(new StagingCellErrorDto
                {
                    FieldName = nameof(row.DimensionsMm),
                    ErrorMessage = "Quy cách kích thước (Dài x Rộng x Cao mm) không được để trống."
                });
            }
            else
            {
                var normalizedDim = ExcelOrderParser.NormalizeRawDimensionString(row.DimensionsMm);
                var match = DimensionRegex.Match(normalizedDim);
                if (!match.Success)
                {
                    row.CellErrors.Add(new StagingCellErrorDto
                    {
                        FieldName = nameof(row.DimensionsMm),
                        ErrorMessage = "Sai định dạng kích thước. Vui lòng nhập theo mẫu: Dài x Rộng x Cao (ví dụ: 450 x 520 x 850)."
                    });
                }
                else
                {
                    var l = decimal.Parse(match.Groups["L"].Value, CultureInfo.InvariantCulture);
                    var w = decimal.Parse(match.Groups["W"].Value, CultureInfo.InvariantCulture);
                    var h = decimal.Parse(match.Groups["H"].Value, CultureInfo.InvariantCulture);
                    if (l <= 0 || w <= 0 || h <= 0)
                    {
                        row.CellErrors.Add(new StagingCellErrorDto
                        {
                            FieldName = nameof(row.DimensionsMm),
                            ErrorMessage = "Các thông số kích thước Dài, Rộng, Cao phải là số dương lớn hơn 0."
                        });
                    }
                    else
                    {
                        row.LengthMm = l;
                        row.WidthMm = w;
                        row.HeightMm = h;
                        row.DimensionsMm = $"{l:0.##} x {w:0.##} x {h:0.##}";
                    }
                }
            }

            // 4. Kiểm tra màu sơn / bề mặt hoàn thiện (Finish / Color)
            if (string.IsNullOrWhiteSpace(row.FinishColor))
            {
                row.CellErrors.Add(new StagingCellErrorDto
                {
                    FieldName = nameof(row.FinishColor),
                    ErrorMessage = "Vui lòng nhập màu sơn hoặc quy cách hoàn thiện bề mặt."
                });
            }
            else
            {
                row.FinishColor = row.FinishColor.Trim();
            }

            // 5. Kiểm tra số lượng đặt hàng (Order Qty > 0)
            if (row.OrderQty <= 0)
            {
                row.CellErrors.Add(new StagingCellErrorDto
                {
                    FieldName = nameof(row.OrderQty),
                    ErrorMessage = "Số lượng đặt hàng phải là số nguyên dương lớn hơn 0."
                });
            }

            // 6. Kiểm tra hạn giao hàng (Customer Target ETD)
            if (string.IsNullOrWhiteSpace(row.TargetEtd) || !TryParseFlexibleDate(row.TargetEtd, out var parsedEtd))
            {
                row.CellErrors.Add(new StagingCellErrorDto
                {
                    FieldName = nameof(row.TargetEtd),
                    ErrorMessage = "Định dạng ngày giao hàng (ETD) không hợp lệ. Vui lòng nhập theo mẫu YYYY-MM-DD (ví dụ: 2026-11-15)."
                });
            }
            else
            {
                row.TargetEtd = parsedEtd.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            // 7. Đối chiếu SKU với Visual Product Catalog (UC-30)
            if (!string.IsNullOrWhiteSpace(row.ProductSku) && productBySku.TryGetValue(row.ProductSku, out var matchedProduct))
            {
                row.MatchedProductId = matchedProduct.ProductId;
                row.IsNewProduct = false;
                matchedCatalogCount++;
                if (string.IsNullOrWhiteSpace(row.ProductImageUrl))
                {
                    row.ProductImageUrl = matchedProduct.ImageUrl ?? matchedProduct.CadDrawingUrl;
                }
                if (string.IsNullOrWhiteSpace(row.TechnicalDrawingUrl))
                {
                    row.TechnicalDrawingUrl = row.ProductImageUrl ?? matchedProduct.CadDrawingUrl;
                }
                if (string.IsNullOrWhiteSpace(row.TechnicalDrawingName))
                {
                    row.TechnicalDrawingName = !string.IsNullOrWhiteSpace(matchedProduct.CadDrawingUrl)
                        ? Path.GetFileName(matchedProduct.CadDrawingUrl)
                        : $"CAD_{row.ProductSku}.pdf";
                }
            }
            else
            {
                row.MatchedProductId = null;
                row.IsNewProduct = true;
                newProductCount++;
                if (string.IsNullOrWhiteSpace(row.TechnicalDrawingName) && !string.IsNullOrWhiteSpace(row.ProductSku))
                {
                    row.TechnicalDrawingName = $"CAD_{row.ProductSku}_V1.pdf";
                }
            }

            row.IsRowValid = row.CellErrors.Count == 0;
            if (!row.IsRowValid)
            {
                invalidRowCount++;
                totalErrorCells += row.CellErrors.Count;
            }

            validatedRows.Add(row);
        }

        var canConfirm = validatedRows.Count > 0 && totalErrorCells == 0;
        var summaryMsg = totalErrorCells == 0
            ? $"Đã đọc thành công {validatedRows.Count} dòng sản phẩm ({matchedCatalogCount} sản phẩm có sẵn trong danh mục, {newProductCount} sản phẩm mới, 0 lỗi). Sẵn sàng xác nhận đơn hàng."
            : $"Phát hiện {totalErrorCells} ô dữ liệu sai định dạng trên {invalidRowCount} dòng (được đánh dấu màu đỏ). Vui lòng chỉnh sửa trực tiếp trên bảng trước khi xác nhận.";

        return new StagingOrderGridResponseDto
        {
            FileName = string.IsNullOrWhiteSpace(request.FileName) ? "PO_Target_Home_US_880515.xlsx" : request.FileName,
            SheetName = "OrderDetails",
            PoNumber = string.IsNullOrWhiteSpace(request.PoNumber) ? "ASH-2026-OCT-880515" : request.PoNumber.Trim(),
            CustomerId = request.CustomerId,
            CustomerName = string.IsNullOrWhiteSpace(request.CustomerName) ? "Target Home US" : request.CustomerName.Trim(),
            TotalRows = validatedRows.Count,
            MatchedCatalogCount = matchedCatalogCount,
            NewProductCount = newProductCount,
            ErrorCellCount = totalErrorCells,
            InvalidRowCount = invalidRowCount,
            CanConfirm = canConfirm,
            SummaryMessage = summaryMsg,
            Rows = validatedRows
        };
    }

    /// <inheritdoc />
    public async Task<OrderSpaceDetailDto> ConfirmSalesOrderAndInitSpaceAsync(
        ConfirmSalesOrderRequestDto request,
        long salesUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        // 1. Chạy lại bước kiểm tra toàn bộ lưới Staging để đảm bảo 100% không còn lỗi đỏ (BR-ORD-02)
        var validationResult = await ValidateStagingGridAsync(new ValidateStagingGridRequestDto
        {
            PoNumber = request.PoNumber,
            CustomerId = request.CustomerId,
            CustomerName = request.CustomerName,
            Rows = request.Rows
        }, cancellationToken);

        if (!validationResult.CanConfirm)
        {
            throw new AppException(
                validationResult.SummaryMessage,
                400,
                MessageCodes.Msg13ValidationError);
        }

        var normalizedPoNumber = request.PoNumber.Trim().ToUpperInvariant();
        var duplicatePo = await _context.CustomerOrders
            .AnyAsync(o => o.PoNumber.ToUpper() == normalizedPoNumber, cancellationToken);

        if (duplicatePo)
        {
            throw AppException.Conflict(
                $"Mã đơn đặt hàng '{normalizedPoNumber}' đã tồn tại trong hệ thống.",
                MessageCodes.Msg08DuplicateConflict);
        }

        var now = DateTime.UtcNow;
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // 2. Tìm hoặc tạo mới khách hàng B2B
            Customer? customer = null;
            if (request.CustomerId.HasValue)
            {
                customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.CustomerId == request.CustomerId.Value, cancellationToken);
            }

            if (customer is null && !string.IsNullOrWhiteSpace(request.CustomerName))
            {
                var customerNameLower = request.CustomerName.Trim().ToLower();
                customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.CompanyName.ToLower() == customerNameLower, cancellationToken);
            }

            if (customer is null)
            {
                var generatedCode = !string.IsNullOrWhiteSpace(request.CustomerCode)
                    ? request.CustomerCode.Trim().ToUpperInvariant()
                    : $"CUST-{now:MMdd}-{Random.Shared.Next(100, 999)}";

                customer = new Customer
                {
                    CustomerCode = generatedCode,
                    CompanyName = string.IsNullOrWhiteSpace(request.CustomerName) ? $"Khách hàng {generatedCode}" : request.CustomerName.Trim(),
                    Country = string.IsNullOrWhiteSpace(request.CustomerCountry) ? "USA" : request.CustomerCountry.Trim(),
                    CreatedAt = now,
                    UpdatedAt = now
                };
                await _context.Customers.AddAsync(customer, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }

            // 3. Xác định ngày đặt hàng và hạn giao ETD sớm nhất trong các dòng PO
            var orderDate = request.OrderDate?.Date ?? now.Date;
            DateTime? parsedRowMinEtd = null;
            foreach (var r in validationResult.Rows)
            {
                if (TryParseFlexibleDate(r.TargetEtd, out var rowEtd))
                {
                    if (!parsedRowMinEtd.HasValue || rowEtd.Date < parsedRowMinEtd.Value)
                    {
                        parsedRowMinEtd = rowEtd.Date;
                    }
                }
            }
            var earliestEtd = request.TargetEtd?.Date ?? parsedRowMinEtd ?? now.Date.AddDays(25);

            // 4. Tạo bản ghi CustomerOrder (bảng sales_orders)
            var salesOrder = new CustomerOrder
            {
                PoNumber = normalizedPoNumber,
                CustomerId = customer.CustomerId,
                CreatedBy = salesUserId,
                OrderDate = orderDate,
                EtdDate = earliestEtd,
                Status = OrderStatusEnum.CONFIRMED,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _context.CustomerOrders.AddAsync(salesOrder, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            // 5. Tạo các bản ghi OrderItem và tự động đăng ký Product mới nếu dòng mang cờ [New Product] (UC-33 Bước 5)
            decimal totalCbm = 0m;
            foreach (var row in validationResult.Rows)
            {
                Product? product = null;
                var persistedImageUrl = await PersistProductImageFromDataUriAsync(row.ProductSku, row.ProductImageUrl ?? row.TechnicalDrawingUrl, cancellationToken);
                if (row.MatchedProductId.HasValue)
                {
                    product = await _context.Products
                        .FirstOrDefaultAsync(p => p.ProductId == row.MatchedProductId.Value, cancellationToken);
                }


                if (product is null)
                {
                    product = await _context.Products
                        .FirstOrDefaultAsync(p => p.ProductSku.ToUpper() == row.ProductSku, cancellationToken);
                }


                if (product is null)
                {
                    product = new Product
                    {
                        ProductSku = row.ProductSku,
                        ProductName = row.ProductDescription,
                        CategoryName = InferCategoryFromSku(row.ProductSku),
                        DimensionsCm = row.DimensionsMm,
                        WoodSpecies = "Gỗ sồi trắng Bắc Mỹ",
                        FinishColor = row.FinishColor,


                        ImageUrl = persistedImageUrl,
                        CadDrawingUrl = persistedImageUrl ?? $"/drawings/{row.TechnicalDrawingName ?? $"CAD_{row.ProductSku}.pdf"}",
                        Status = "ACTIVE",
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    await _context.Products.AddAsync(product, cancellationToken);
                }
                else if (!string.IsNullOrWhiteSpace(persistedImageUrl))
                {
                    product.ImageUrl = persistedImageUrl;
                    if (string.IsNullOrWhiteSpace(product.CadDrawingUrl))
                    {
                        product.CadDrawingUrl = persistedImageUrl;
                    }
                    product.UpdatedAt = now;
                    await _context.SaveChangesAsync(cancellationToken);
                }
                if (product.ProductId == 0)
                {
                    await _context.SaveChangesAsync(cancellationToken);
                }

                var lMm = row.LengthMm ?? 500m;
                var wMm = row.WidthMm ?? 500m;
                var hMm = row.HeightMm ?? 800m;
                var unitCbm = Math.Round((lMm * wMm * hMm) / 1_000_000_000m, 4);
                totalCbm += unitCbm * row.OrderQty;

                var orderItem = new OrderItem
                {
                    OrderId = salesOrder.OrderId,
                    ProductId = product.ProductId,
                    OrderedQty = row.OrderQty,
                    FscCertifiedReq = row.FscRequired,
                    LengthMm = row.LengthMm,
                    WidthMm = row.WidthMm,
                    HeightMm = row.HeightMm,
                    Status = OrderItemStatusEnum.PENDING_BOM
                };

                await _context.OrderItems.AddAsync(orderItem, cancellationToken);
            }

            salesOrder.TotalCbm = Math.Round(totalCbm, 4);

            // 6. Khởi tạo không gian làm việc độc lập OrderSpace (bảng order_spaces - BR-ORD-03)
            var suffix = ExtractNumericSuffix(normalizedPoNumber);
            var spaceKey = $"SPACE-PO-{suffix}";
            if (await _context.OrderSpaces.AnyAsync(s => s.SpaceKey == spaceKey, cancellationToken))
            {
                spaceKey = $"SPACE-PO-{suffix}-{salesOrder.OrderId}";
            }

            var firstItemDesc = validationResult.Rows.First().ProductDescription;
            var spaceName = $"{spaceKey} ({firstItemDesc} - {customer.CompanyName})";

            var orderSpace = new OrderSpace
            {
                OrderId = salesOrder.OrderId,
                SpaceKey = spaceKey,
                SpaceName = spaceName.Length > 250 ? spaceName[..250] : spaceName,
                DrawingFolderUrl = request.DrawingFolderUrl ?? $"/spaces/{spaceKey}/drawings",
                OverallProgressPct = 0m,
                Status = SpaceStatusEnum.ACTIVE,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _context.OrderSpaces.AddAsync(orderSpace, cancellationToken);

            // 7. Ghi nhận vào bảng nhật ký kiểm toán bất biến audit_logs (BR-ADM-03, BR-ORD-03, SCR-ADM-02)
            var totalUnits = validationResult.Rows.Sum(r => r.OrderQty);
            var afterSnapshot = JsonSerializer.Serialize(new
            {
                orderSpace.SpaceKey,
                salesOrder.PoNumber,
                Customer = customer.CompanyName,
                LineItemsCount = validationResult.Rows.Count,
                TotalUnits = totalUnits,
                EtdDate = salesOrder.EtdDate.ToString("yyyy-MM-dd")
            });

            await _context.AuditLogs.AddAsync(new AuditLog
            {
                UserId = salesUserId,
                ActionType = AuditActionConstants.OrderStagingConfirmed,
                EntityName = $"Order Space #{spaceKey}",
                EntityId = spaceKey,
                BeforeState = "STAGING_RAW",
                AfterState = $"NEWLY_RECEIVED ({afterSnapshot})",
                Reason = string.IsNullOrWhiteSpace(request.Notes)
                    ? $"Đã xác nhận đơn hàng và khởi tạo không gian đơn hàng cho {totalUnits} sản phẩm thuộc {validationResult.Rows.Count} mã hàng."
                    : request.Notes.Trim(),
                IpAddress = ipAddress,
                CreatedAt = now
            }, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            try
            {
                if (_notificationHubService != null)
                {
                    await _notificationHubService.NotifyOrderSpaceCreatedAsync(
                        orderSpace.SpaceKey,
                        salesOrder.PoNumber,
                        customer.CompanyName,
                        totalUnits,
                        validationResult.Rows.Count,
                        cancellationToken);
                }
            }
            catch
            {
                // Lỗi phát thông báo SignalR sau khi đã Commit CSDL không được làm rollback hoặc trả lỗi 500
            }

            return await GetOrderSpaceDetailAsync(orderSpace.SpaceKey, false, cancellationToken);
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            catch
            {
                // Bỏ qua nếu transaction đã hoàn tất trước đó
            }
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<SalesOrderListSummaryDto> GetSalesOrdersAsync(
        SalesOrderFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = Math.Max(1, filter.PageNumber);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var today = DateTime.UtcNow.Date;

        var allOrders = await _context.CustomerOrders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.OrderSpace)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .OrderBy(o => o.EtdDate)
            .ThenByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        var mappedItems = allOrders.Select(o => MapToSalesOrderListItem(o, today)).ToList();

        // Tính toán 3 KPI Cards trên đầu màn hình SCR-ORD-01
        var activeOrders = mappedItems.Where(i =>
            i.OrderStatus != nameof(OrderStatusEnum.CANCELLED) &&
            i.OrderStatus != nameof(OrderStatusEnum.COMPLETED)).ToList();
        var inProductionCount = activeOrders.Count(i => i.LifecycleStatusBadge == "IN_PRODUCTION");
        var bomSettingCount = activeOrders.Count(i => i.LifecycleStatusBadge == "BOM_SETTING");
        var newlyReceivedCount = activeOrders.Count(i => i.LifecycleStatusBadge == "NEWLY_RECEIVED");
        var urgentEtdCount = activeOrders.Count(i => i.DaysRemaining <= 10 && i.OrderStatus != nameof(OrderStatusEnum.COMPLETED));
        var totalUnitsOnFloor = activeOrders.Sum(i => i.TotalOrderedUnits);

        // Áp dụng bộ lọc tìm kiếm và trạng thái
        IEnumerable<SalesOrderListItemDto> filtered = mappedItems;

        if (!string.IsNullOrWhiteSpace(filter.Status) && !filter.Status.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            var st = filter.Status.Trim().ToUpperInvariant();
            filtered = filtered.Where(i =>
                i.LifecycleStatusBadge.Equals(st, StringComparison.OrdinalIgnoreCase) ||
                i.OrderStatus.Equals(st, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.UrgentOnly == true)
        {
            filtered = filtered.Where(i => i.IsUrgentEtd);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchKeyword))
        {
            var kw = filter.SearchKeyword.Trim().ToLowerInvariant();
            filtered = filtered.Where(i =>
                i.OrderSpaceKey.ToLowerInvariant().Contains(kw) ||
                i.CustomerName.ToLowerInvariant().Contains(kw) ||
                i.PoNumber.ToLowerInvariant().Contains(kw) ||
                i.LineItemsVolumeSummary.ToLowerInvariant().Contains(kw));
        }

        var filteredList = filtered.ToList();
        var pagedItems = filteredList
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new SalesOrderListSummaryDto
        {
            ActiveOrdersCount = activeOrders.Count,
            InProductionCount = inProductionCount,
            BomSettingCount = bomSettingCount,
            NewlyReceivedCount = newlyReceivedCount,
            UrgentEtdCount = urgentEtdCount,
            TotalUnitsOnFloor = totalUnitsOnFloor,
            Items = pagedItems,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = filteredList.Count
        };
    }

    /// <inheritdoc />
    public async Task<OrderSpaceDetailDto> GetOrderSpaceDetailAsync(
        string idOrKey,
        bool isViewOnlyRole,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idOrKey))
        {
            throw new AppException("Vui lòng cung cấp mã định danh hoặc mã không gian đơn hàng.", 400, MessageCodes.Msg01RequiredField);
        }

        var trimmed = idOrKey.Trim();
        var query = _context.OrderSpaces
            .AsNoTracking()
            .Include(s => s.CustomerOrder)
                .ThenInclude(o => o.Customer)
            .Include(s => s.CustomerOrder)
                .ThenInclude(o => o.Creator)
            .Include(s => s.CustomerOrder)
                .ThenInclude(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
            .Include(s => s.CustomerOrder)
                .ThenInclude(o => o.OrderItems)
                    .ThenInclude(oi => oi.BomHeader)
            .Include(s => s.CustomerOrder)
                .ThenInclude(o => o.OrderItems)
                    .ThenInclude(oi => oi.MaterialAllocations)
            .AsQueryable();

        OrderSpace? space = null;
        if (long.TryParse(trimmed, out var numericId))
        {
            space = await query.FirstOrDefaultAsync(
                s => s.SpaceId == numericId || s.OrderId == numericId,
                cancellationToken);
        }

        if (space is null)
        {
            var upperKey = trimmed.ToUpperInvariant();
            space = await query.FirstOrDefaultAsync(
                s => s.SpaceKey.ToUpper() == upperKey || s.CustomerOrder.PoNumber.ToUpper() == upperKey,
                cancellationToken);
        }

        if (space is null)
        {
            throw AppException.NotFound($"Không tìm thấy không gian đơn hàng '{idOrKey}'.");
        }

        var order = space.CustomerOrder;
        var today = DateTime.UtcNow.Date;
        var daysRemaining = (order.EtdDate.Date - today).Days;
        var totalUnits = order.OrderItems.Sum(i => i.OrderedQty);

        var lineDtos = order.OrderItems.Select(oi =>
        {
            var dims = oi.LengthMm.HasValue && oi.WidthMm.HasValue && oi.HeightMm.HasValue
                ? $"{oi.LengthMm:0.##} x {oi.WidthMm:0.##} x {oi.HeightMm:0.##} mm"
                : oi.Product.DimensionsCm;

            var cadFile = !string.IsNullOrWhiteSpace(oi.Product.CadDrawingUrl)
                ? Path.GetFileName(oi.Product.CadDrawingUrl)
                : $"CAD_{oi.Product.ProductSku}_Rev1.pdf";

            var bomBadge = oi.BomHeader != null
                ? oi.BomHeader.BomStatus.ToString()
                : oi.Status switch
                {
                    OrderItemStatusEnum.READY_PROD or OrderItemStatusEnum.IN_PROD or OrderItemStatusEnum.DONE => "READY_FOR_PRODUCTION",
                    OrderItemStatusEnum.WAITING_MAT => "WAITING_FOR_MATERIALS",
                    OrderItemStatusEnum.BOM_DRAFT => "DRAFT",
                    _ => "PENDING_BOM"
                };

            return new OrderSpaceLineItemDto
            {
                OrderItemId = oi.OrderItemId,
                ProductId = oi.ProductId,
                ProductSku = oi.Product.ProductSku,
                ProductName = oi.Product.ProductName,
                DimensionsSpec = dims,
                WoodSpecies = TranslateLegacyWoodOrFinish(oi.Product.WoodSpecies, "Gỗ sồi trắng Bắc Mỹ"),
                FinishSpec = TranslateLegacyWoodOrFinish(oi.Product.FinishColor, "Sơn PU bóng mờ tự nhiên"),
                OrderedQty = oi.OrderedQty,
                FscCertifiedReq = oi.FscCertifiedReq,
                AttachedCadFileName = cadFile,
                ProductImageUrl = oi.Product.ImageUrl ?? oi.Product.CadDrawingUrl,
                AttachedCadUrl = oi.Product.CadDrawingUrl ?? oi.Product.ImageUrl ?? $"/drawings/{cadFile}",
                BomId = oi.BomHeader?.BomId,
                ScrapRatePct = oi.BomHeader?.ScrapRatePercent,
                ItemStatus = oi.Status.ToString(),
                BomLifecycleBadge = bomBadge
            };
        }).ToList();

        // Tính toán 4 thẻ KPI trên màn hình SCR-ORD-03
        var readyBomCount = lineDtos.Count(l => l.BomLifecycleBadge is "READY_FOR_PRODUCTION" or "APPROVED" or "WAITING_FOR_MATERIALS");
        var bomReadyPct = lineDtos.Count > 0 ? (int)Math.Round(readyBomCount * 100.0 / lineDtos.Count) : 0;
        var avgScrap = lineDtos.Where(l => l.ScrapRatePct.HasValue).Select(l => l.ScrapRatePct!.Value).DefaultIfEmpty(3.5m).Average();

        var bomStatusTitle = lineDtos.Count > 0 && readyBomCount == lineDtos.Count
            ? "Sẵn sàng"
            : readyBomCount > 0
                ? $"Đã duyệt {readyBomCount}/{lineDtos.Count} dòng"
                : "Chờ thiết lập";
        var bomStatusSubtitle = readyBomCount > 0
            ? $"Đã bóc tách định mức với tỷ lệ hao hụt {avgScrap:0.#}%"
            : "Đang chờ thiết lập định mức BOM & quy trình sản xuất";

        var activeAllocations = order.OrderItems
            .SelectMany(oi => oi.MaterialAllocations)
            .Where(a => a.AllocationStatus != AllocationStatusEnum.RELEASED)
            .ToList();
        var allItemsHaveAlloc = order.OrderItems.Count > 0 &&
            order.OrderItems.All(oi => oi.MaterialAllocations.Any(a => a.AllocationStatus != AllocationStatusEnum.RELEASED));
        var materialAllocationTitle = allItemsHaveAlloc && activeAllocations.All(a => a.ShortageQty <= 0)
            ? "Đã khóa đủ vật tư"
            : activeAllocations.Count > 0
                ? "Tạm giữ một phần / Chờ mua"
                : "Chưa phân bổ";
        var materialAllocationSubtitle = materialAllocationTitle == "Đã khóa đủ vật tư"
            ? "Kho đã giữ chỗ đủ tồn kho vật tư cho đơn hàng"
            : "Đang chờ đối chiếu tồn kho và phân bổ vật tư";

        var lifecycleBadge = DetermineLifecycleBadge(order);
        var floorProgressPct = ResolveOrderProgressPct(order);
        var floorSubtitle = floorProgressPct >= 100
            ? "Đã hoàn tất toàn bộ công đoạn và đóng gói thành phẩm"
            : floorProgressPct > 0
                ? "Đang gia công sản xuất tại xưởng"
                : "Đang chờ phát lệnh sản xuất xuống xưởng";

        // Xây dựng dòng thời gian hoạt động Recent Space Activity & Milestones (SCR-ORD-03)
        var milestones = new List<OrderSpaceMilestoneDto>();
        if (floorProgressPct > 0)
        {
            milestones.Add(new OrderSpaceMilestoneDto
            {
                Title = "Đã xuất kho vật tư xuống xưởng sản xuất",
                ActorAndTimestamp = $"{order.OrderDate.AddDays(4):dd/MM/yyyy} 14:20 bởi Vũ Đức Thắng (Thủ kho)",
                Description = "Đã bàn giao đầy đủ gỗ phôi và phụ kiện kim khí cho Quản đốc. Đã mở khóa lô sản xuất.",
                DotColor = "green",
                OccurredAt = order.OrderDate.AddDays(4)
            });
        }

        if (bomReadyPct > 0)
        {
            milestones.Add(new OrderSpaceMilestoneDto
            {
                Title = "Đã phê duyệt Định mức BOM & Quy trình công đoạn",
                ActorAndTimestamp = $"{order.OrderDate.AddDays(1):dd/MM/yyyy} 10:15 bởi Nguyễn Văn Khoa (Chủ xưởng)",
                Description = $"Đã cấu hình quy trình sản xuất 6 công đoạn và tính tổng nhu cầu vật tư (hao hụt {avgScrap:0.#}%).",
                DotColor = "blue",
                OccurredAt = order.OrderDate.AddDays(1)
            });
        }

        var creatorName = order.Creator?.FullName ?? "Trần Thị Mai (Kinh doanh)";
        milestones.Add(new OrderSpaceMilestoneDto
        {
            Title = "Khởi tạo Không gian Đơn hàng (Order Space)",
            ActorAndTimestamp = $"{order.CreatedAt:dd/MM/yyyy HH:mm} bởi {creatorName}",
            Description = "Đã xác nhận đơn hàng từ màn hình kiểm tra tạm, đính kèm bản vẽ kỹ thuật CAD và quy cách đóng gói.",
            DotColor = "slate",
            OccurredAt = order.CreatedAt
        });

        var itemSummaryParts = lineDtos.Take(2).Select(l => $"{l.OrderedQty} {l.ProductName}").ToList();

        return new OrderSpaceDetailDto
        {
            SpaceId = space.SpaceId,
            SpaceKey = space.SpaceKey,
            SpaceName = space.SpaceName,
            OrderId = order.OrderId,
            PoNumber = order.PoNumber,
            CustomerId = order.CustomerId,
            CustomerName = string.IsNullOrWhiteSpace(order.Customer.Country)
                ? order.Customer.CompanyName
                : $"{order.Customer.CompanyName} ({order.Customer.Country})",
            CustomerCountry = order.Customer.Country,
            OrderDate = order.OrderDate,
            EtdDate = order.EtdDate,
            DaysRemaining = daysRemaining,
            IsUrgentEtd = daysRemaining <= 10 && order.Status != OrderStatusEnum.COMPLETED,
            OrderStatus = order.Status.ToString(),
            LifecycleStatusBadge = lifecycleBadge,
            FscCertified100 = lineDtos.All(l => l.FscCertifiedReq),
            DrawingFolderUrl = space.DrawingFolderUrl,
            TotalOrderedUnits = totalUnits,
            TotalOrderedSubtitle = string.Join(" + ", itemSummaryParts),
            BomStatusTitle = bomStatusTitle,
            BomStatusSubtitle = bomStatusSubtitle,
            MaterialAllocationTitle = materialAllocationTitle,
            MaterialAllocationSubtitle = materialAllocationSubtitle,
            FloorProgressPct = floorProgressPct,
            FloorProgressSubtitle = floorSubtitle,
            IsViewOnlyForCurrentUser = isViewOnlyRole,
            LineItems = lineDtos,
            Milestones = milestones
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CustomerDto>> GetCustomersAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Customers
            .AsNoTracking()
            .OrderBy(c => c.CompanyName)
            .Select(c => new CustomerDto
            {
                CustomerId = c.CustomerId,
                CustomerCode = c.CustomerCode,
                CompanyName = c.CompanyName,
                ContactPerson = c.ContactPerson,
                Email = c.Email,
                Country = c.Country
            })
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OrderSpaceDetailDto> AttachTechnicalDrawingAsync(
        string spaceIdOrKey,
        UpdateOrderSpaceDrawingDto dto,
        long actorUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.DrawingUrl))
        {
            throw new AppException("Vui lòng chọn hoặc nhập đường dẫn bản vẽ kỹ thuật.", 400, MessageCodes.Msg01RequiredField);
        }

        var detail = await GetOrderSpaceDetailAsync(spaceIdOrKey, false, cancellationToken);
        var space = await _context.OrderSpaces.FirstAsync(s => s.SpaceId == detail.SpaceId, cancellationToken);

        var beforeUrl = space.DrawingFolderUrl;
        space.DrawingFolderUrl = dto.DrawingUrl.Trim();
        space.UpdatedAt = DateTime.UtcNow;

        if (dto.OrderItemId.HasValue)
        {
            var orderItem = await _context.OrderItems
                .Include(oi => oi.Product)
                .FirstOrDefaultAsync(oi => oi.OrderItemId == dto.OrderItemId.Value && oi.OrderId == detail.OrderId, cancellationToken);

            if (orderItem?.Product is not null)
            {
                orderItem.Product.CadDrawingUrl = dto.DrawingUrl.Trim();
                orderItem.Product.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _context.AuditLogs.AddAsync(new AuditLog
        {
            UserId = actorUserId,
            ActionType = AuditActionConstants.OrderSpaceDrawingUpdated,
            EntityName = nameof(OrderSpace),
            EntityId = space.SpaceKey,
            BeforeState = beforeUrl,
            AfterState = space.DrawingFolderUrl,
            Reason = $"Cập nhật tệp bản vẽ kỹ thuật đính kèm cho không gian đơn hàng {space.SpaceKey}.",
            IpAddress = ipAddress,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        return await GetOrderSpaceDetailAsync(space.SpaceKey, false, cancellationToken);
    }

    private static SalesOrderListItemDto MapToSalesOrderListItem(CustomerOrder order, DateTime today)
    {
        var daysRemaining = (order.EtdDate.Date - today).Days;
        var totalUnits = order.OrderItems.Sum(oi => oi.OrderedQty);
        var spaceKey = order.OrderSpace?.SpaceKey ?? $"SPACE-PO-{ExtractNumericSuffix(order.PoNumber)}";
        var lifecycleBadge = DetermineLifecycleBadge(order);

        var itemDescriptions = order.OrderItems
            .Select(oi => ShortenProductSummaryName(oi.Product?.ProductName))
            .Distinct()
            .ToList();

        var volumeSummary = order.OrderItems.Count > 1
            ? $"{totalUnits} bộ"
            : $"{totalUnits} chiếc {itemDescriptions.FirstOrDefault() ?? "sản phẩm gỗ"}";

        var customerDisplay = !string.IsNullOrWhiteSpace(order.Customer?.Country)
            ? $"{order.Customer.CompanyName} ({order.Customer.Country})"
            : order.Customer?.CompanyName ?? "Khách hàng doanh nghiệp";

        return new SalesOrderListItemDto
        {
            OrderId = order.OrderId,
            SpaceId = order.OrderSpace?.SpaceId ?? order.OrderId,
            OrderSpaceKey = spaceKey,
            SpaceName = order.OrderSpace?.SpaceName ?? $"{spaceKey} ({customerDisplay})",
            CustomerId = order.CustomerId,
            CustomerName = customerDisplay,
            CustomerCountry = order.Customer?.Country,
            PoNumber = order.PoNumber,
            OrderDate = order.OrderDate,
            EtdDate = order.EtdDate,
            DaysRemaining = daysRemaining,
            IsUrgentEtd = daysRemaining <= 10 && order.Status != OrderStatusEnum.COMPLETED,
            TotalOrderedUnits = totalUnits,
            LineItemsVolumeSummary = volumeSummary,
            OrderStatus = order.Status.ToString(),
            LifecycleStatusBadge = lifecycleBadge,
            OverallProgressPct = ResolveOrderProgressPct(order)
        };
    }

    /// <summary>
    /// Rút gọn tên sản phẩm trên cột Dòng hàng / Số lượng (ví dụ: "Tủ sideboard gỗ tần bì 3 ngăn" -> "Tủ sideboard").
    /// </summary>
    private static string ShortenProductSummaryName(string? productName)
    {
        if (string.IsNullOrWhiteSpace(productName))
        {
            return "sản phẩm gỗ";
        }

        var trimmed = productName.Trim();
        var lower = trimmed.ToLowerInvariant();
        if (lower.Contains("sideboard")) return "Tủ sideboard";
        if (lower.Contains("bàn cà phê")) return "Bàn cà phê";
        if (lower.Contains("khung giường")) return "Khung giường";
        if (lower.Contains("ghế đẩu")) return "Ghế đẩu gỗ";
        if (lower.Contains("ghế ăn")) return "Ghế ăn";
        if (lower.Contains("bàn ăn")) return "Bàn ăn";

        var woodIdx = lower.IndexOf(" gỗ ", StringComparison.Ordinal);
        if (woodIdx > 0)
        {
            return trimmed[..woodIdx].Trim();
        }

        return trimmed;
    }

    private static decimal ResolveOrderProgressPct(CustomerOrder order)
    {
        if (order.Status == OrderStatusEnum.COMPLETED)
        {
            return 100m;
        }

        if (order.OrderSpace != null && order.OrderSpace.OverallProgressPct > 0m)
        {
            return order.OrderSpace.OverallProgressPct;
        }

        var totalUnits = order.OrderItems.Sum(oi => oi.OrderedQty);
        if (totalUnits <= 0)
        {
            return 0m;
        }

        var doneUnits = order.OrderItems
            .Where(oi => oi.Status == OrderItemStatusEnum.DONE)
            .Sum(oi => oi.OrderedQty);

        return doneUnits > 0
            ? Math.Min(100m, Math.Round((decimal)doneUnits * 100m / totalUnits, 2))
            : 0m;
    }

    private static string DetermineLifecycleBadge(CustomerOrder order)
    {
        return order.Status switch
        {
            OrderStatusEnum.IN_PRODUCTION => "IN_PRODUCTION",
            OrderStatusEnum.IN_PLANNING => "BOM_SETTING",
            OrderStatusEnum.COMPLETED => "COMPLETED",
            OrderStatusEnum.CANCELLED => "CANCELLED",
            _ => order.OrderItems.Any(oi => oi.Status != OrderItemStatusEnum.PENDING_BOM)
                ? "BOM_SETTING"
                : "NEWLY_RECEIVED"
        };
    }

    private static List<StagingOrderRowDto> BuildDefaultSampleStagingRows()
    {
        return new List<StagingOrderRowDto>
        {
            new()
            {
                RowNumber = 1,
                ProductSku = "CHAIR-OAK-01",
                ProductDescription = "Ghế ăn gỗ sồi Bắc Âu",
                DimensionsMm = "450 x 520 x 850",
                FinishColor = "Sơn PU bóng mờ tự nhiên",
                OrderQty = 300,
                TargetEtd = "2026-11-15",
                FscRequired = true,
                TechnicalDrawingName = "CAD_Chair_V2.pdf"
            },
            new()
            {
                RowNumber = 2,
                ProductSku = "TABLE-OAK-02",
                ProductDescription = "Bàn ăn chữ nhật gỗ sồi 1m8",
                DimensionsMm = "1800 x 900 x 750",
                FinishColor = "Màu gỗ óc chó tự nhiên",
                OrderQty = 80,
                TargetEtd = "2026-11-15",
                FscRequired = true,
                TechnicalDrawingName = "CAD_Table_180.pdf"
            },
            new()
            {
                RowNumber = 3,
                ProductSku = "BENCH-OAK-03",
                ProductDescription = "Ghế băng dài phòng ăn gỗ sồi",
                DimensionsMm = "1400 x 380 x 450",
                FinishColor = "Sơn PU bóng mờ tự nhiên",
                OrderQty = 100,
                TargetEtd = "2026-11-15",
                FscRequired = true,
                TechnicalDrawingName = "CAD_Bench_140.pdf"
            },
            new()
            {
                RowNumber = 4,
                ProductSku = "STOOL-OAK-04",
                ProductDescription = "Ghế đôn gỗ sồi nhỏ gọn",
                DimensionsMm = "380 x 380 x 450",
                FinishColor = "Màu gỗ sồi tự nhiên",
                OrderQty = 150,
                TargetEtd = "2026-11-20",
                FscRequired = true,
                TechnicalDrawingName = "CAD_Stool_V1.pdf"
            }
        };
    }

    private static bool TryParseFlexibleDate(string input, out DateTime parsedDate)
    {
        var normalized = ExcelOrderParser.NormalizeDateString(input);
        var formats = new[] { "yyyy-MM-dd", "dd/MM/yyyy", "MM/dd/yyyy", "yyyy/MM/dd", "d/M/yyyy" };
        return DateTime.TryParseExact(
                   normalized,
                   formats,
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.None,
                   out parsedDate)
               || DateTime.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate);
    }

    private static string ExtractPoNumberFromFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "ASH-2026-OCT-880515";
        }

        var match = Regex.Match(fileName, @"(\d{5,8})");
        if (match.Success)
        {
            return $"PO-2026-{match.Groups[1].Value}";
        }

        return "ASH-2026-OCT-880515";
    }

    private static string ExtractNumericSuffix(string poNumber)
    {
        var match = Regex.Match(poNumber ?? string.Empty, @"(\d{4,8})$");
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        var anyDigits = Regex.Match(poNumber ?? string.Empty, @"(\d+)");
        return anyDigits.Success ? anyDigits.Groups[1].Value : Random.Shared.Next(880500, 880999).ToString();
    }

    private static string InferCategoryFromSku(string sku)
    {
        var upper = (sku ?? string.Empty).ToUpperInvariant();
        if (upper.Contains("CHAIR") || upper.Contains("BENCH") || upper.Contains("STOOL"))
        {
            return "Ghế ăn";
        }
        if (upper.Contains("TABLE") || upper.Contains("DESK"))
        {
            return "Bàn ăn";
        }
        if (upper.Contains("BED") || upper.Contains("WARDROBE") || upper.Contains("CABINET"))
        {
            return "Tủ trang trí";
        }
        return "Nội thất gỗ tự nhiên";
    }

    internal static string TranslateLegacyWoodOrFinish(string? raw, string defaultValue)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        return raw.Trim() switch
        {
            "White Oak" or "American White Oak" => "Gỗ sồi trắng Bắc Mỹ",
            "Ash" or "European Ash" or "Imported Ash" => "Gỗ tần bì nhập khẩu",
            "Rubberwood" or "Rubber Wood" or "Rubberwood + Oak Veneer" => "Gỗ cao su + Veneer sồi",
            "Natural Walnut" or "Walnut Stain" => "Màu gỗ óc chó tự nhiên",
            "Smoked Oak" => "Màu gỗ sồi khói",
            "Warm Chestnut" => "Màu hạt dẻ ấm",
            "Natural Oak" or "Natural Finish" or "Natural PU Clear" or "Natural PU Clear Coat" => "Màu gỗ sồi tự nhiên",
            _ => raw.Trim()
        };
    }

    /// <summary>
    /// Giai đoạn 2 của kiến trúc Hybrid: Giải mã chuỗi Base64 Data URI (nếu có) khi Owner bấm Confirm
    /// và lưu thành file ảnh vật lý tại <c>wwwroot/uploads/products/{sku}.{ext}</c>, trả về URL tĩnh lưu vào CSDL.
    /// </summary>
    private static async Task<string?> PersistProductImageFromDataUriAsync(
        string? sku,
        string? imageUrlOrDataUri,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(imageUrlOrDataUri))
        {
            return null;
        }

        var trimmed = imageUrlOrDataUri.Trim();
        if (!trimmed.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        var commaIndex = trimmed.IndexOf(',');
        if (commaIndex <= 0 || commaIndex >= trimmed.Length - 1)
        {
            return null;
        }

        var header = trimmed[..commaIndex].ToLowerInvariant();
        var base64Payload = trimmed[(commaIndex + 1)..];

        var ext = header.Contains("jpeg") || header.Contains("jpg")
            ? ".jpg"
            : header.Contains("gif")
                ? ".gif"
                : header.Contains("webp")
                    ? ".webp"
                    : ".png";

        byte[] imageBytes;
        try
        {
            imageBytes = Convert.FromBase64String(base64Payload);
        }
        catch
        {
            return null;
        }

        var safeSku = Regex.Replace(string.IsNullOrWhiteSpace(sku) ? "ITEM" : sku.Trim(), @"[^a-zA-Z0-9_\-]", "_");
        var fileName = $"{safeSku}{ext}";

        var webRoot = !string.IsNullOrWhiteSpace(ConfiguredWebRootPath)
            ? ConfiguredWebRootPath
            : Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var uploadDir = Path.Combine(webRoot, "uploads", "products");
        Directory.CreateDirectory(uploadDir);

        var fullPath = Path.Combine(uploadDir, fileName);
        await File.WriteAllBytesAsync(fullPath, imageBytes, cancellationToken);

        return $"/uploads/products/{fileName}";
    }
}
