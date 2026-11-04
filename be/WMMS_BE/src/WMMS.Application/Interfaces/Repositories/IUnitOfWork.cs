namespace WMMS.Application.Interfaces.Repositories;

/// <summary>
/// Giao diện Đơn vị Công việc (Unit of Work) điều phối giao dịch nguyên tử (ACID Transaction)
/// trên toàn bộ các kho lưu trữ dữ liệu thuộc Iteration 1, Iteration 2 và Iteration 3.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    IBomRepository Boms { get; }
    IRoutingRepository Routings { get; }
    IProductRepository Products { get; }
    IMaterialRepository Materials { get; }
    IOrderItemRepository OrderItems { get; }
    ISystemConfigRepository SystemConfigs { get; }
    IMaterialStockRepository MaterialStocks { get; }
    IMaterialAllocationRepository MaterialAllocations { get; }
    IPurchaseRequestRepository PurchaseRequests { get; }
    IPurchaseOrderRepository PurchaseOrders { get; }
    IProductionBatchRepository ProductionBatches { get; }
    IQcInspectionRepository QcInspections { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}
