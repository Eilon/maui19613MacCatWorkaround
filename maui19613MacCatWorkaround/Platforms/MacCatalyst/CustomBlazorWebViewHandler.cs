using Foundation;
using Microsoft.AspNetCore.Components.WebView;
using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UIKit;
using WebKit;

namespace maui19613MacCatWorkaround.Platforms.MacCatalyst
{
    internal class CustomBlazorWebViewHandler : BlazorWebViewHandler
    {
        internal const string AppHostAddress = "0.0.0.0";

        internal const string AppOrigin = "app://" + AppHostAddress + "/";
        internal static readonly Uri AppOriginUri = new(AppOrigin);

        private bool DeveloperToolsEnabled = true;

        private const string BlazorInitScript = @"
			window.__receiveMessageCallbacks = [];
			window.__dispatchMessageCallback = function(message) {
				window.__receiveMessageCallbacks.forEach(function(callback) { callback(message); });
			};
			window.external = {
				sendMessage: function(message) {
					window.webkit.messageHandlers.webwindowinterop.postMessage(message);
				},
				receiveMessage: function(callback) {
					window.__receiveMessageCallbacks.push(callback);
				}
			};

			Blazor.start();

			(function () {
				window.onpageshow = function(event) {
					if (event.persisted) {
						window.location.reload();
					}
				};
			})();
		";


        protected override WKWebView CreatePlatformView()
        {
            //Logger.CreatingWebKitWKWebView();
            var config = new WKWebViewConfiguration();

            // By default, setting inline media playback to allowed, including autoplay
            // and picture in picture, since these things MUST be set during the webview
            // creation, and have no effect if set afterwards.
            // A custom handler factory delegate could be set to disable these defaults
            // but if we do not set them here, they cannot be changed once the
            // handler's platform view is created, so erring on the side of wanting this
            // capability by default.
            if (OperatingSystem.IsMacCatalystVersionAtLeast(10) || OperatingSystem.IsIOSVersionAtLeast(10))
            {
                config.AllowsPictureInPictureMediaPlayback = true;
                config.AllowsInlineMediaPlayback = true;
                config.MediaTypesRequiringUserActionForPlayback = WKAudiovisualMediaTypes.None;
            }

            VirtualView.BlazorWebViewInitializing(new BlazorWebViewInitializingEventArgs()
            {
                Configuration = config
            });

            // use private reflection to get the MessageReceived method from the base class instance
            var messageReceivedMethod = typeof(BlazorWebViewHandler).GetMethod("MessageReceived", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var messageReceivedDelegate = (Action<Uri, string>)messageReceivedMethod.CreateDelegate(typeof(Action<Uri, string>), this);

            config.UserContentController.AddScriptMessageHandler(new WebViewScriptMessageHandler(messageReceivedDelegate), "webwindowinterop");
            config.UserContentController.AddUserScript(new WKUserScript(
                new NSString(BlazorInitScript), WKUserScriptInjectionTime.AtDocumentEnd, true));
            // iOS WKWebView doesn't allow handling 'http'/'https' schemes, so we use the fake 'app' scheme

            var schemeHandlerType = typeof(BlazorWebViewHandler).Assembly.GetType("Microsoft.AspNetCore.Components.WebView.Maui.BlazorWebViewHandler+SchemeHandler")!;

            var schemeHandlerInstance = (IWKUrlSchemeHandler)Activator.CreateInstance(schemeHandlerType, this)!;

            config.SetUrlSchemeHandler(schemeHandlerInstance, urlScheme: "app");

            var webview = new WKWebView(RectangleF.Empty, config)
            {
                BackgroundColor = UIColor.Clear,
                AutosizesSubviews = true
            };

            if (DeveloperToolsEnabled)
            {
                // Legacy Developer Extras setting.
                config.Preferences.SetValueForKey(NSObject.FromObject(true), new NSString("developerExtrasEnabled"));

                if (OperatingSystem.IsIOSVersionAtLeast(16, 4) || OperatingSystem.IsMacCatalystVersionAtLeast(16, 6))
                {
                    // Enable Developer Extras for iOS builds for 16.4+ and Mac Catalyst builds for 16.6 (macOS 13.5)+
                    webview.SetValueForKey(NSObject.FromObject(true), new NSString("inspectable"));
                }
            }

            VirtualView.BlazorWebViewInitialized(new BlazorWebViewInitializedEventArgs
            {
                // NOTE: This is internal so can't be set here
                //WebView = webview
            });
            //Logger.CreatedWebKitWKWebView();
            return webview;
        }

        private sealed class WebViewScriptMessageHandler : NSObject, IWKScriptMessageHandler
        {
            private Action<Uri, string> _messageReceivedAction;

            public WebViewScriptMessageHandler(Action<Uri, string> messageReceivedAction)
            {
                _messageReceivedAction = messageReceivedAction ?? throw new ArgumentNullException(nameof(messageReceivedAction));
            }

            public void DidReceiveScriptMessage(WKUserContentController userContentController, WKScriptMessage message)
            {
                if (message is null)
                {
                    throw new ArgumentNullException(nameof(message));
                }
                _messageReceivedAction(AppOriginUri, ((NSString)message.Body).ToString());
            }
        }
    }
}
