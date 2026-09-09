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

    private bool _termsAccepted;
    public bool TermsAccepted
    {
        get => _termsAccepted;
        set
        {
            if (SetProperty(ref _termsAccepted, value))
                OnPropertyChanged(nameof(CanConnect));
        }
    }

    private bool _showTermsModal;
    public bool ShowTermsModal
    {
        get => _showTermsModal;
        set => SetProperty(ref _showTermsModal, value);
    }

    public bool CanConnect => TermsAccepted;

    public AsyncRelayCommand LoginCommand { get; }
    public AsyncRelayCommand ShowTermsCommand { get; }
    public AsyncRelayCommand AcceptTermsCommand { get; }
    public AsyncRelayCommand CloseTermsCommand { get; }

    public LoginViewModel(ISpotifyAuthService authService, TunelithDbContext dbContext)
    {
        _authService = authService;
        _dbContext = dbContext;
        LoginCommand = new AsyncRelayCommand(LoginAsync);
        ShowTermsCommand = new AsyncRelayCommand(() => { ShowTermsModal = true; return Task.CompletedTask; });
        AcceptTermsCommand = new AsyncRelayCommand(AcceptTermsAsync);
        CloseTermsCommand = new AsyncRelayCommand(() => { ShowTermsModal = false; return Task.CompletedTask; });
    }

    public async Task CheckTermsAcceptedAsync()
    {
        var accepted = await _dbContext.GetUserPreferenceAsync("terms_accepted");
        if (accepted == "true")
        {
            TermsAccepted = true;
        }
    }

    private async Task AcceptTermsAsync()
    {
        TermsAccepted = true;
        ShowTermsModal = false;
        await _dbContext.SetUserPreferenceAsync("terms_accepted", "true");
    }

    private async Task LoginAsync()
    {
        if (!TermsAccepted) return;

        IsLoading = true;
        StatusMessage = "Connecting to Spotify...";

        try
        {
            var authUrl = await _authService.GetAuthorizationUrlAsync();

            var callbackUrl = new Uri("tunelith://callback");
            var result = await WebAuthenticator.AuthenticateAsync(
                new Uri(authUrl), callbackUrl);

            if (result?.Properties.TryGetValue("code", out var code) == true)
            {
                StatusMessage = "Authenticating...";

                var token = await _authService.ExchangeCodeForTokenAsync(code);

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
