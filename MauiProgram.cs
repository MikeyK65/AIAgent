using Microsoft.Extensions.Logging;
using System.Net.Http;
using AIAgent.Services;
using AIAgent.ViewModels;

namespace AIAgent;

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

		builder.Services.AddSingleton<ISettingsService, LocalSettingsService>();
        builder.Services.AddSingleton<MockEmailProvider>();
		builder.Services.AddSingleton<OutlookGraphEmailProvider>();
		builder.Services.AddSingleton<IEmailProvider, ConfiguredEmailProvider>();
		builder.Services.AddSingleton<IMessageScorer, MessageScoringService>();
		builder.Services.AddSingleton<MessageAggregationService>();
        builder.Services.AddSingleton<IEmailSummarizer>(serviceProvider =>
			new OllamaEmailSummarizer(new HttpClient(), serviceProvider.GetRequiredService<ISettingsService>()));
		builder.Services.AddSingleton<CombinedInboxViewModel>();
		builder.Services.AddSingleton<AccountsViewModel>();
		builder.Services.AddSingleton<SettingsViewModel>();
		builder.Services.AddSingleton<MessageDetailViewModel>();
		builder.Services.AddSingleton<AppShell>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

     var app = builder.Build();
		ServiceHelper.Services = app.Services;

		return app;
	}
}
