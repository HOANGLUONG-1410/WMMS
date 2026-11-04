using System.Globalization;
using Microsoft.EntityFrameworkCore;
using WMMS.Application.Interfaces.Repositories;
using WMMS.Infrastructure.Persistence;

namespace WMMS.Infrastructure.Repositories;

public class SystemConfigRepository : ISystemConfigRepository
{
    private readonly WmmsDbContext _context;

    public SystemConfigRepository(WmmsDbContext context)
    {
        _context = context;
    }

    public async Task<decimal> GetDecimalConfigAsync(string key, decimal defaultValue)
    {
        var config = await _context.SystemConfigs.FirstOrDefaultAsync(c => c.ConfigKey == key);
        if (config != null && decimal.TryParse(config.ConfigValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        return defaultValue;
    }

    public async Task<string> GetStringConfigAsync(string key, string defaultValue)
    {
        var config = await _context.SystemConfigs.FirstOrDefaultAsync(c => c.ConfigKey == key);
        return config?.ConfigValue ?? defaultValue;
    }

    public async Task<int> GetIntConfigAsync(string key, int defaultValue)
    {
        var config = await _context.SystemConfigs.FirstOrDefaultAsync(c => c.ConfigKey == key);
        if (config != null && int.TryParse(config.ConfigValue, out var value))
        {
            return value;
        }

        return defaultValue;
    }
}
