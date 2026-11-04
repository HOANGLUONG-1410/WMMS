namespace WMMS.Application.Interfaces.Repositories;

public interface ISystemConfigRepository
{
    Task<decimal> GetDecimalConfigAsync(string key, decimal defaultValue);
    Task<string> GetStringConfigAsync(string key, string defaultValue);
    Task<int> GetIntConfigAsync(string key, int defaultValue);
}
