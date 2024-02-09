using Microsoft.Extensions.Logging;

namespace maui19613MacCatWorkaround
{
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


#if MACCATALYST
            // register a custom MAUI handler
            builder.ConfigureMauiHandlers(handlers =>
            {
                handlers.AddHandler<Microsoft.AspNetCore.Components.WebView.Maui.BlazorWebView, Platforms.MacCatalyst.CustomBlazorWebViewHandler>();
            });
#endif

            builder.Services.AddMauiBlazorWebView();

#if DEBUG
    		builder.Services.AddBlazorWebViewDeveloperTools();
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
