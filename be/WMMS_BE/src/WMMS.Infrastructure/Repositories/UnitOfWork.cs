using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using WMMS.Application.Interfaces.Repositories;
using WMMS.Infrastructure.Persistence;

namespace WMMS.Infrastructure.Repositories;

/// <summary>
/// Thực thi Đơn vị Công việc (Unit of Work) cung cấp các kho lưu trữ cho toàn bộ phân hệ Iteration 1, 2 và 3.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly WmmsDbContext _context;
    private IDbContextTransaction? _transaction;

    private IBomRepository? _boms;
    private IRoutingRepository? _routings;
    private IProductRepository? _products;
    private IMaterialRepository? _materials;
    private IOrderItemRepository? _orderItems;
    private ISystemConfigRepository? _systemConfigs;
    private IMaterialStockRepository? _materialStocks;
    private IMaterialAllocationRepository? _materialAllocations;
    private IPurchaseRequestRepository? _purchaseRequests;
    private IPurchaseOrderRepository? _purchaseOrders;
    private IProductionBatchRepository? _productionBatches;
    private IQcInspectionRepository? _qcInspections;

    public UnitOfWork(WmmsDbContext context)
    {
        _context = context;
    }

    public IBomRepository Boms => _boms ??= new BomRepository(_context);
    public IRoutingRepository Routings => _routings ??= new RoutingRepository(_context);
    public IProductRepository Products => _products ??= new ProductRepository(_context);
    public IMaterialRepository Materials => _materials ??= new MaterialRepository(_context);
    public IOrderItemRepository OrderItems => _orderItems ??= new OrderItemRepository(_context);
    public ISystemConfigRepository SystemConfigs => _systemConfigs ??= new SystemConfigRepository(_context);
    public IMaterialStockRepository MaterialStocks => _materialStocks ??= new MaterialStockRepository(_context);
    public IMaterialAllocationRepository MaterialAllocations => _materialAllocations ??= new MaterialAllocationRepository(_context);
    public IPurchaseRequestRepository PurchaseRequests => _purchaseRequests ??= new PurchaseRequestRepository(_context);
    public IPurchaseOrderRepository PurchaseOrders => _purchaseOrders ??= new PurchaseOrderRepository(_context);
    public IProductionBatchRepository ProductionBatches => _productionBatches ??= new ProductionBatchRepository(_context);
    public IQcInspectionRepository QcInspections => _qcInspections ??= new QcInspectionRepository(_context);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync()
    {
        if (_transaction == null && _context.Database.IsRelational())
        {
            _transaction = await _context.Database.BeginTransactionAsync();
        }
    }

    public async Task CommitTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}
