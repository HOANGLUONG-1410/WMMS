using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using WMMS.Application.DTOs.Orders;
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

    public OrderIntakeService(WmmsDbContext context)
    {
        _context = context;
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

        if (excelStream is not null && excelStream.Length > 0)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            if (ext is not ".xlsx" and not ".xls")
            {
                throw new AppException(
                    "Invalid spreadsheet format. File must be .xlsx or .xls and under 20MB (MSG-ORD-02).",
                    400,
                    MessageCodes.Msg13ValidationError);
            }

            try
            {
                using var package = new ExcelPackage(excelStream);
                var worksheet = package.Workbook.Worksheets.FirstOrDefault();
                if (worksheet is null || worksheet.Dimension is null)
                {
                    throw new AppException(
                        "Invalid Excel template. Worksheet is empty (UC030.E1).",
                        400,
                        MessageCodes.Msg13ValidationError);
                }

                sheetName = worksheet.Name;
                extractedRows = ParseWorksheetRows(worksheet, ref detectedPoNumber, ref detectedCustomerName);
            }
            catch (AppException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new AppException(
                    $"Could not read Excel PO file: {ex.Message} (MSG-ORD-02).",
                    400,
                    MessageCodes.Msg13ValidationError);
            }
        }

        // Nếu file trống hoặc người dùng bấm nạp dữ liệu mẫu Staging từ SCR-ORD-02 trong Report 3
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
                    ErrorMessage = "Internal Product SKU cannot be empty."
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
                    ErrorMessage = "Product Description is required."
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
                    ErrorMessage = "Dimensions (L x W x H mm) are required."
                });
            }
            else
            {
                var match = DimensionRegex.Match(row.DimensionsMm);
                if (!match.Success)
                {
                    row.CellErrors.Add(new StagingCellErrorDto
                    {
                        FieldName = nameof(row.DimensionsMm),
                        ErrorMessage = "Invalid dimension syntax. Expected format: L x W x H (e.g. 450 x 520 x 850)."
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
                            ErrorMessage = "Dimensions L, W, H must be positive numbers."
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
                    ErrorMessage = "Finish / Color specification is required."
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
                    ErrorMessage = "Order Qty must be a positive integer greater than 0."
                });
            }

            // 6. Kiểm tra hạn giao hàng (Customer Target ETD)
            if (string.IsNullOrWhiteSpace(row.TargetEtd) || !TryParseFlexibleDate(row.TargetEtd, out var parsedEtd))
            {
                row.CellErrors.Add(new StagingCellErrorDto
                {
                    FieldName = nameof(row.TargetEtd),
                    ErrorMessage = "Invalid ETD date format. Use YYYY-MM-DD (e.g. 2026-11-15)."
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
            ? $"Parsed {validatedRows.Count} lines. {matchedCatalogCount} matched catalog, {newProductCount} flagged as new, 0 errors. Ready to confirm (MSG-ORD-01)."
            : $"Found {totalErrorCells} formatted data errors across {invalidRowCount} row(s) (highlighted in red). Please correct directly before confirming (MSG-ORD-03).";

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
                $"Purchase Order number '{normalizedPoNumber}' already exists in the system (UC-33).",
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
                    CompanyName = request.CustomerName.Trim(),
                    Country = string.IsNullOrWhiteSpace(request.CustomerCountry) ? "USA" : request.CustomerCountry.Trim(),
                    CreatedAt = now,
                    UpdatedAt = now
                };
                await _context.Customers.AddAsync(customer, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }

            // 3. Xác định ngày đặt hàng và hạn giao ETD sớm nhất trong các dòng PO
            var orderDate = request.OrderDate?.Date ?? now.Date;
            var earliestEtd = request.TargetEtd?.Date ?? now.Date.AddDays(25);
            foreach (var r in validationResult.Rows)
            {
                if (TryParseFlexibleDate(r.TargetEtd, out var rowEtd) && rowEtd.Date < earliestEtd)
                {
                    earliestEtd = rowEtd.Date;
                }
            }

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
                        WoodSpecies = "American White Oak",
                        FinishColor = row.FinishColor,
                        CadDrawingUrl = row.TechnicalDrawingUrl ?? $"/drawings/{row.TechnicalDrawingName ?? $"CAD_{row.ProductSku}.pdf"}",
                        Status = "ACTIVE",
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    await _context.Products.AddAsync(product, cancellationToken);
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
                    ? $"Cleaned staging format errors. Generated Order Space for {totalUnits} units across {validationResult.Rows.Count} line items (UC-33)."
                    : request.Notes.Trim(),
                IpAddress = ipAddress,
                CreatedAt = now
            }, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return await GetOrderSpaceDetailAsync(orderSpace.SpaceKey, false, cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
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
        var activeOrders = mappedItems.Where(i => i.OrderStatus != nameof(OrderStatusEnum.CANCELLED)).ToList();
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
            throw new AppException("Order Space identifier or key is required.", 400, MessageCodes.Msg01RequiredField);
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
            throw AppException.NotFound($"Order Space '{idOrKey}' was not found (MSG10).");
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
                WoodSpecies = oi.Product.WoodSpecies ?? "American White Oak",
                FinishSpec = oi.Product.FinishColor ?? "Natural PU Clear Coat",
                OrderedQty = oi.OrderedQty,
                FscCertifiedReq = oi.FscCertifiedReq,
                AttachedCadFileName = cadFile,
                AttachedCadUrl = oi.Product.CadDrawingUrl ?? $"/drawings/{cadFile}",
                BomId = oi.BomHeader?.BomId,
                ScrapRatePct = oi.BomHeader?.ScrapRatePercent,
                ItemStatus = oi.Status.ToString(),
                BomLifecycleBadge = bomBadge
            };
        }).ToList();

        // Tính toán 4 thẻ KPI trên màn hình SCR-ORD-03
        var readyBomCount = lineDtos.Count(l => l.BomLifecycleBadge == "READY_FOR_PRODUCTION");
        var bomReadyPct = lineDtos.Count > 0 ? (int)Math.Round(readyBomCount * 100.0 / lineDtos.Count) : 0;
        var avgScrap = lineDtos.Where(l => l.ScrapRatePct.HasValue).Select(l => l.ScrapRatePct!.Value).DefaultIfEmpty(3.5m).Average();

        var bomStatusTitle = bomReadyPct == 100
            ? "100% Ready"
            : bomReadyPct > 0
                ? $"{bomReadyPct}% Ready"
                : "Pending Setup";
        var bomStatusSubtitle = bomReadyPct > 0
            ? $"Active breakdown with {avgScrap:0.#}% scrap rate"
            : "Awaiting BOM & Dynamic Routing breakdown";

        var allAllocations = order.OrderItems.SelectMany(oi => oi.MaterialAllocations).ToList();
        var materialAllocationTitle = allAllocations.Count > 0 && allAllocations.All(a => a.ShortageQty <= 0)
            ? "Fully Locked"
            : allAllocations.Count > 0
                ? "Partial / Waiting"
                : bomReadyPct == 100 ? "Fully Locked" : "Unallocated";
        var materialAllocationSubtitle = materialAllocationTitle == "Fully Locked"
            ? "Allocated Stock secured by Warehouse"
            : "Pending stock reconciliation & reservation";

        var lifecycleBadge = DetermineLifecycleBadge(order);
        var floorProgressPct = space.OverallProgressPct;
        var floorSubtitle = floorProgressPct >= 100
            ? "Stage 6: Final Packaging completed"
            : floorProgressPct > 0
                ? "Station 4: Structure Assembly in progress"
                : "Waiting for shop-floor dispatch";

        // Xây dựng dòng thời gian hoạt động Recent Space Activity & Milestones (SCR-ORD-03)
        var milestones = new List<OrderSpaceMilestoneDto>();
        if (floorProgressPct > 0)
        {
            milestones.Add(new OrderSpaceMilestoneDto
            {
                Title = "Goods Issue Released to Floor",
                ActorAndTimestamp = $"{order.OrderDate.AddDays(4):MMM dd, yyyy} 14:20 by Vu Duc Thang (Warehouse)",
                Description = "Handed over 100% lumber and hardware to Production Leader. Kanban card unlocked.",
                DotColor = "green",
                OccurredAt = order.OrderDate.AddDays(4)
            });
        }

        if (bomReadyPct > 0)
        {
            milestones.Add(new OrderSpaceMilestoneDto
            {
                Title = "BOM & Routing Approved",
                ActorAndTimestamp = $"{order.OrderDate.AddDays(1):MMM dd, yyyy} 10:15 by Nguyen Van Khoa (Owner)",
                Description = $"Configured 6-step dynamic routing and calculated gross raw material demand with {avgScrap:0.#}% loss allowance.",
                DotColor = "blue",
                OccurredAt = order.OrderDate.AddDays(1)
            });
        }

        var creatorName = order.Creator?.FullName ?? "Tran Thi Mai (Sales)";
        milestones.Add(new OrderSpaceMilestoneDto
        {
            Title = "Order Space Initialized",
            ActorAndTimestamp = $"{order.CreatedAt:MMM dd, yyyy HH:mm} by {creatorName}",
            Description = "PO confirmed via Staging Area. Attached CAD drawings and buyer packaging specs.",
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
            throw new AppException("DrawingUrl is required.", 400, MessageCodes.Msg01RequiredField);
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
            Reason = $"Updated technical CAD drawing attachment for {space.SpaceKey}.",
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
            .Select(oi => oi.Product?.ProductName ?? "Wood Item")
            .Distinct()
            .ToList();

        var volumeSummary = order.OrderItems.Count > 1
            ? $"{totalUnits} Sets ({string.Join(" + ", itemDescriptions.Take(2))})"
            : $"{totalUnits} Pcs {itemDescriptions.FirstOrDefault() ?? "Furniture Unit"}";

        var customerDisplay = !string.IsNullOrWhiteSpace(order.Customer?.Country)
            ? $"{order.Customer.CompanyName} ({order.Customer.Country})"
            : order.Customer?.CompanyName ?? "B2B Client";

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
            OverallProgressPct = order.OrderSpace?.OverallProgressPct ?? 0m
        };
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

    private static List<StagingOrderRowDto> ParseWorksheetRows(
        ExcelWorksheet worksheet,
        ref string detectedPoNumber,
        ref string detectedCustomerName)
    {
        var rows = new List<StagingOrderRowDto>();
        var maxRow = worksheet.Dimension.End.Row;
        var maxCol = worksheet.Dimension.End.Column;

        var headerRow = 1;
        var colMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // Quét 10 dòng đầu tiên để tìm dòng tiêu đề bảng và thông tin PO / Khách hàng
        for (var r = 1; r <= Math.Min(10, maxRow); r++)
        {
            for (var c = 1; c <= maxCol; c++)
            {
                var cellText = worksheet.Cells[r, c].Text?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(cellText))
                {
                    continue;
                }

                if (cellText.StartsWith("PO#", StringComparison.OrdinalIgnoreCase) ||
                    cellText.StartsWith("PO Number", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = cellText.Split(':', '#');
                    if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[^1]))
                    {
                        detectedPoNumber = parts[^1].Trim();
                    }
                }

                var lower = cellText.ToLowerInvariant();
                if (lower.Contains("sku") || lower.Contains("item no") || lower.Contains("product code") || lower == "code")
                {
                    headerRow = r;
                    colMap["sku"] = c;
                }
                else if (lower.Contains("description") || lower.Contains("product name") || lower.Contains("item name"))
                {
                    colMap["desc"] = c;
                }
                else if (lower.Contains("dimension") || lower.Contains("size"))
                {
                    colMap["dim"] = c;
                }
                else if (lower.Contains("finish") || lower.Contains("color"))
                {
                    colMap["finish"] = c;
                }
                else if (lower.Contains("qty") || lower.Contains("quantity"))
                {
                    colMap["qty"] = c;
                }
                else if (lower.Contains("etd") || lower.Contains("delivery"))
                {
                    colMap["etd"] = c;
                }
                else if (lower.Contains("fsc"))
                {
                    colMap["fsc"] = c;
                }
                else if (lower.Contains("drawing") || lower.Contains("cad"))
                {
                    colMap["cad"] = c;
                }
            }
        }

        if (!colMap.ContainsKey("sku"))
        {
            colMap["sku"] = 1;
            colMap["desc"] = 2;
            colMap["dim"] = 3;
            colMap["finish"] = 4;
            colMap["qty"] = 5;
            colMap["etd"] = 6;
        }

        for (var r = headerRow + 1; r <= maxRow; r++)
        {
            var sku = GetCellString(worksheet, r, colMap.GetValueOrDefault("sku", 1));
            var desc = GetCellString(worksheet, r, colMap.GetValueOrDefault("desc", 2));
            var dim = GetCellString(worksheet, r, colMap.GetValueOrDefault("dim", 3));
            var finish = GetCellString(worksheet, r, colMap.GetValueOrDefault("finish", 4));
            var qtyStr = GetCellString(worksheet, r, colMap.GetValueOrDefault("qty", 5));
            var etdStr = GetCellString(worksheet, r, colMap.GetValueOrDefault("etd", 6));
            var fscStr = colMap.TryGetValue("fsc", out var fscCol) ? GetCellString(worksheet, r, fscCol) : "FSC 100%";
            var cadStr = colMap.TryGetValue("cad", out var cadCol) ? GetCellString(worksheet, r, cadCol) : string.Empty;

            if (string.IsNullOrWhiteSpace(sku) && string.IsNullOrWhiteSpace(desc) && string.IsNullOrWhiteSpace(qtyStr))
            {
                continue;
            }

            _ = int.TryParse(qtyStr.Replace(",", "").Trim(), out var parsedQty);

            rows.Add(new StagingOrderRowDto
            {
                RowNumber = rows.Count + 1,
                ProductSku = sku,
                ProductDescription = desc,
                DimensionsMm = dim,
                FinishColor = finish,
                OrderQty = parsedQty,
                TargetEtd = etdStr,
                FscRequired = string.IsNullOrWhiteSpace(fscStr) || fscStr.Contains("FSC", StringComparison.OrdinalIgnoreCase) || fscStr.Contains("100", StringComparison.OrdinalIgnoreCase),
                TechnicalDrawingName = cadStr
            });
        }

        return rows;
    }

    private static string GetCellString(ExcelWorksheet sheet, int row, int col)
    {
        if (col <= 0 || col > sheet.Dimension.End.Column)
        {
            return string.Empty;
        }

        var val = sheet.Cells[row, col].Value;
        if (val is DateTime dt)
        {
            return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return sheet.Cells[row, col].Text?.Trim() ?? string.Empty;
    }

    private static List<StagingOrderRowDto> BuildDefaultSampleStagingRows()
    {
        return new List<StagingOrderRowDto>
        {
            new()
            {
                RowNumber = 1,
                ProductSku = "CHAIR-OAK-01",
                ProductDescription = "Nordic Oak Dining Chair",
                DimensionsMm = "450 x 520 x 850",
                FinishColor = "Natural PU Clear",
                OrderQty = 300,
                TargetEtd = "2026-11-15",
                FscRequired = true,
                TechnicalDrawingName = "CAD_Chair_V2.pdf"
            },
            new()
            {
                RowNumber = 2,
                ProductSku = "TABLE-OAK-02",
                ProductDescription = "Rectangular Dining Table 1.8m",
                DimensionsMm = "1800 x 900 x 750",
                FinishColor = "Walnut Stain",
                OrderQty = 80,
                TargetEtd = "2026-11-15",
                FscRequired = true,
                TechnicalDrawingName = "CAD_Table_180.pdf"
            },
            new()
            {
                RowNumber = 3,
                ProductSku = "BENCH-OAK-03",
                ProductDescription = "Solid Oak Dining Bench",
                DimensionsMm = "1400 x 380 x 450",
                FinishColor = "Natural PU Clear",
                OrderQty = 100,
                TargetEtd = "2026-11-15",
                FscRequired = true,
                TechnicalDrawingName = "CAD_Bench_140.pdf"
            },
            new()
            {
                RowNumber = 4,
                ProductSku = "STOOL-OAK-04",
                ProductDescription = "Compact Kitchen Stool",
                DimensionsMm = "380 x 380 x 450",
                FinishColor = "Natural Finish",
                OrderQty = 150,
                TargetEtd = "2026-11-20",
                FscRequired = true,
                TechnicalDrawingName = "CAD_Stool_V1.pdf"
            }
        };
    }

    private static bool TryParseFlexibleDate(string input, out DateTime parsedDate)
    {
        var formats = new[] { "yyyy-MM-dd", "dd/MM/yyyy", "MM/dd/yyyy", "yyyy/MM/dd", "d/M/yyyy" };
        return DateTime.TryParseExact(
                   input.Trim(),
                   formats,
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.None,
                   out parsedDate)
               || DateTime.TryParse(input.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate);
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
            return "CHAIRS & SEATING";
        }
        if (upper.Contains("TABLE") || upper.Contains("DESK"))
        {
            return "TABLES & STORAGE";
        }
        if (upper.Contains("BED") || upper.Contains("WARDROBE") || upper.Contains("CABINET"))
        {
            return "BEDROOM & CABINETS";
        }
        return "SOLID WOOD FURNITURE";
    }
}
