using Tunelith.Core.Models;
using Tunelith.Core.Services;
using Tunelith.Data;

namespace Tunelith.Maui.ViewModels;

public class LoginViewModel : ViewModelBase
{
    private readonly ISpotifyAuthService _authService;
    private readonly TunelithDbContext _dbContext;

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public AsyncRelayCommand LoginCommand { get; }

    public LoginViewModel(ISpotifyAuthService authService, TunelithDbContext dbContext)
    {
        _authService = authService;
        _dbContext = dbContext;
        LoginCommand = new AsyncRelayCommand(LoginAsync);
    }

    private async Task LoginAsync()
    {
        IsLoading = true;
        StatusMessage = "Connecting to Spotify...";

        try
        {
            var authUrl = _authService.GetAuthorizationUrl();

            var callbackUrl = new Uri("tunelith://callback");
            var result = await WebAuthenticator.AuthenticateAsync(
                new Uri(authUrl), callbackUrl);

            if (result?.Properties.TryGetValue("code", out var code) == true)
            {
                StatusMessage = "Authenticating...";

                var codeVerifier = await SecureStorage.GetAsync("pkce_code_verifier") ?? string.Empty;
                var token = await _authService.ExchangeCodeForTokenAsync(code, codeVerifier);

                await SecureStorage.SetAsync("spotify_access_token", token.AccessToken);
                await SecureStorage.SetAsync("spotify_refresh_token", token.RefreshToken);

                StatusMessage = "Welcome to Tunelith!";

                await Shell.Current.GoToAsync("//LibraryPage");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Login failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
