using Microsoft.AspNetCore.Components.WebView.Maui;
using LCP.Mobile.Components.Extensions;

namespace LCP.Mobile;

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
			});

		builder.Services.AddMauiBlazorWebView();
#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
#endif

		builder.Services.AddHttpClient();
		var apiBaseUrl = DeviceInfo.Platform == DevicePlatform.Android 
			? "http://10.0.2.2:5152" 
			: "http://localhost:5152";
		builder.Services.AddLcpMobileComponents(apiBaseUrl);

		return builder.Build();
	}
}
