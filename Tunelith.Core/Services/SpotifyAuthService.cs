namespace Tunelith.Core.Services;

public interface ISpotifyAuthService
{
    Task<string> GetAuthorizationUrlAsync();
    Task<TokenResponse> ExchangeCodeForTokenAsync(string code);
    Task<TokenResponse> RefreshTokenAsync(string refreshToken);
    bool IsTokenValid(TokenResponse token);
}

public class TokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string TokenType { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
}

public class SpotifyAuthService : ISpotifyAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ISecureStorageService _secureStorage;
    private const string ClientId = "YOUR_SPOTIFY_CLIENT_ID";
    private const string RedirectUri = "tunelith://callback";
    private const string AuthEndpoint = "https://accounts.spotify.com/authorize";
    private const string TokenEndpoint = "https://accounts.spotify.com/api/token";
    private const string Scopes = "user-library-read playlist-read-private playlist-modify-private playlist-modify-public";
    private const string VerifierKey = "pkce_code_verifier";

    public SpotifyAuthService(HttpClient httpClient, ISecureStorageService secureStorage)
    {
        _httpClient = httpClient;
        _secureStorage = secureStorage;
    }

    public async Task<string> GetAuthorizationUrlAsync()
    {
        var codeVerifier = GenerateCodeVerifier();
        await _secureStorage.SetAsync(VerifierKey, codeVerifier);

        var codeChallenge = GenerateCodeChallenge(codeVerifier);

        var parameters = new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = RedirectUri,
            ["scope"] = Scopes,
            ["code_challenge_method"] = "S256",
            ["code_challenge"] = codeChallenge
        };

        var queryString = string.Join("&", parameters.Select(p =>
            $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

        return $"{AuthEndpoint}?{queryString}";
    }

    public async Task<TokenResponse> ExchangeCodeForTokenAsync(string code)
    {
        var codeVerifier = await _secureStorage.GetAsync(VerifierKey) ?? string.Empty;

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = RedirectUri,
            ["code_verifier"] = codeVerifier
        });

        var response = await _httpClient.PostAsync(TokenEndpoint, content);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return ParseTokenResponse(json);
    }

    public async Task<TokenResponse> RefreshTokenAsync(string refreshToken)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        });

        var response = await _httpClient.PostAsync(TokenEndpoint, content);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return ParseTokenResponse(json);
    }

    public bool IsTokenValid(TokenResponse token)
    {
        return !string.IsNullOrEmpty(token.AccessToken) &&
               token.ExpiresAt > DateTime.UtcNow.AddMinutes(5);
    }

    private TokenResponse ParseTokenResponse(string json)
    {
        var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        return new TokenResponse
        {
            AccessToken = root.GetProperty("access_token").GetString() ?? string.Empty,
            RefreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() ?? string.Empty : string.Empty,
            ExpiresIn = root.GetProperty("expires_in").GetInt32(),
            ExpiresAt = DateTime.UtcNow.AddSeconds(root.GetProperty("expires_in").GetInt32()),
            TokenType = root.GetProperty("token_type").GetString() ?? string.Empty,
            Scope = root.TryGetProperty("scope", out var sc) ? sc.GetString() ?? string.Empty : string.Empty
        };
    }

    private static string GenerateCodeVerifier()
    {
        var bytes = new byte[32];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string GenerateCodeChallenge(string codeVerifier)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = sha256.ComputeHash(System.Text.Encoding.ASCII.GetBytes(codeVerifier));
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
