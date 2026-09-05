using Tunelith.Core.Services;

namespace Tunelith.Maui.Services;

public class MauiSecureStorageService : ISecureStorageService
{
    public Task<string?> GetAsync(string key)
    {
        return SecureStorage.GetAsync(key);
    }

    public Task SetAsync(string key, string value)
    {
        return SecureStorage.SetAsync(key, value);
    }

    public Task RemoveAsync(string key)
    {
        SecureStorage.Remove(key);
        return Task.CompletedTask;
    }
}
