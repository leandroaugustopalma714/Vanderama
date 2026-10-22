using Microsoft.Extensions.Logging;

using LojaLimpeza.Maui.Services;
using LojaLimpeza.Maui.Views;

namespace LojaLimpeza.Maui;

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

#if DEBUG
		builder.Logging.AddDebug();
#endif

		var databasePath = Path.Combine(FileSystem.AppDataDirectory, "loja-limpeza.db3");
		builder.Services.AddSingleton(new LocalDatabaseService(databasePath));
		builder.Services.AddSingleton(_ => new HttpClient { BaseAddress = new Uri(ApiBaseAddress), Timeout = TimeSpan.FromSeconds(12) });
		builder.Services.AddSingleton<LojaApiClient>();
		builder.Services.AddSingleton<PedidoSyncService>();
		builder.Services.AddTransient<MainPage>();
		builder.Services.AddTransient<CadastroClientePage>();
		builder.Services.AddTransient<NovoPedidoPage>();

		return builder.Build();
	}

	private static string ApiBaseAddress
	{
		get
		{
#if ANDROID
			return "http://10.0.2.2:5256/";
#else
			return "http://localhost:5256/";
#endif
		}
	}
}
