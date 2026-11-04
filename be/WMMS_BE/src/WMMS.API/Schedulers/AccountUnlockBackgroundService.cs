using WMMS.Application.Interfaces.Services;

namespace WMMS.API.Schedulers;

/// <summary>
/// JOB-01: Tác vụ chạy ngầm định kỳ tự động mở khóa cho các tài khoản đang ở trạng thái LOCKED
/// sau khi đã hết thời gian khóa tạm thời 30 phút (<c>locked_until &lt;= DateTime.UtcNow</c>).
/// Chạy tự động mỗi 5 phút theo đặc tả UC-01 / JOB-01 trong Report 3 &amp; Report 4.
/// </summary>
public class AccountUnlockBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AccountUnlockBackgroundService> _logger;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);

    public AccountUnlockBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<AccountUnlockBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[JOB-01] AccountUnlockBackgroundService started.");

        using var timer = new PeriodicTimer(CheckInterval);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
                await authService.UnlockExpiredAccountsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "[JOB-01] Error occurred while unlocking expired user accounts.");
            }
        }
    }
}
