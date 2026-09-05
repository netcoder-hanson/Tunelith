using Microsoft.Extensions.Logging;
using Tunelith.Core.Services;
using Tunelith.Data;
using Tunelith.Maui.ViewModels;
using Tunelith.Maui.Views;

namespace Tunelith.Maui;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		var dbPath = Path.Combine(FileSystem.AppDataDirectory, "tunelith.db3");
		builder.Services.AddSingleton(new TunelithDbContext(dbPath));

		builder.Services.AddSingleton<RateLimitHandler>();
		builder.Services.AddHttpClient<ISpotifyAuthService, SpotifyAuthService>();
		builder.Services.AddHttpClient<ISpotifyApiClient, SpotifyApiClient>();
		builder.Services.AddHttpClient<IGeminiService, GeminiService>();

		builder.Services.AddSingleton<CategorizationEngine>();
		builder.Services.AddSingleton<DuplicateDetector>();

		builder.Services.AddTransient<LoginViewModel>();
		builder.Services.AddTransient<LibraryViewModel>();
		builder.Services.AddTransient<CategorizationViewModel>();
		builder.Services.AddTransient<ChangeReportViewModel>();

		builder.Services.AddTransient<LoginPage>();
		builder.Services.AddTransient<LibraryPage>();
		builder.Services.AddTransient<CategorizationPage>();
		builder.Services.AddTransient<ChangeReportPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
