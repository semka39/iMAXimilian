using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Webkit;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.AppCompat.Widget;
using Android.Views.InputMethods;

[Activity(Label = "WebMaxApp", MainLauncher = true,
          Theme = "@style/Theme.AppCompat.DayNight.NoActionBar",
          Name = "iMAXimilian.iMAXimilian.MainActivity",
          WindowSoftInputMode = SoftInput.AdjustResize)]
public class MainActivity : AppCompatActivity
{
    WebView webView;
    string siteUrl = "https://web.max.ru";
    private ViewTreeObserver.IOnGlobalLayoutListener globalLayoutListener;

    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        var container = new LinearLayout(this)
        {
            LayoutParameters = new ViewGroup.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.MatchParent),
            Orientation = Orientation.Vertical
        };

        webView = new WebView(this)
        {
            LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.MatchParent)
        };

        container.AddView(webView);
        SetContentView(container);

        AddStatusBarPadding();

        SetupGlobalLayoutListener();

        var webSettings = webView.Settings;
        webSettings.JavaScriptEnabled = true;
        webSettings.DomStorageEnabled = true;
        webSettings.SetSupportZoom(true);
        webSettings.LoadWithOverviewMode = true;
        webSettings.UseWideViewPort = true;

        string desktopUa = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                           "AppleWebKit/537.36 (KHTML, like Gecko) " +
                           "Chrome/120.0.0.0 Safari/537.36";
        webSettings.UserAgentString = desktopUa;

        webSettings.SetAppCacheEnabled(true);
        webSettings.SetAppCachePath(CacheDir.AbsolutePath);
        webSettings.SetAppCacheMaxSize(10 * 1024 * 1024);
        webSettings.CacheMode = CacheModes.Default;

        var cookieManager = CookieManager.Instance;
        cookieManager.SetAcceptCookie(true);
        CookieManager.Instance.SetAcceptThirdPartyCookies(webView, true);

        webView.SetWebViewClient(new MyWebViewClient(this));
        webView.SetWebChromeClient(new WebChromeClient());

        webView.LoadUrl(siteUrl);
    }

    private void SetupGlobalLayoutListener()
    {
        var rootView = FindViewById<View>(Android.Resource.Id.Content);

        globalLayoutListener = new GlobalLayoutListener(() => {
            AdjustWebViewForKeyboard();
        });

        rootView.ViewTreeObserver.AddOnGlobalLayoutListener(globalLayoutListener);
    }

    private void AdjustWebViewForKeyboard()
    {
        var rootView = FindViewById<View>(Android.Resource.Id.Content);
        var rect = new Android.Graphics.Rect();
        rootView.GetWindowVisibleDisplayFrame(rect);

        var screenHeight = rootView.Height;
        var keypadHeight = screenHeight - rect.Bottom;

        if (keypadHeight > screenHeight * 0.15)
        {
            if (webView.LayoutParameters is LinearLayout.LayoutParams layoutParams)
            {
                int statusBarHeight = GetStatusBarHeight();
                int navigationBarHeight = GetNavigationBarHeight();

                layoutParams.Height = screenHeight - keypadHeight - statusBarHeight - navigationBarHeight;
                webView.LayoutParameters = layoutParams;
            }
        }
        else
        {
            if (webView.LayoutParameters is LinearLayout.LayoutParams layoutParams)
            {
                layoutParams.Height = ViewGroup.LayoutParams.MatchParent;
                webView.LayoutParameters = layoutParams;
            }
        }
    }

    private void AddStatusBarPadding()
    {
        int statusBarHeight = GetStatusBarHeight();

        if (webView.LayoutParameters is LinearLayout.LayoutParams layoutParams)
        {
            layoutParams.TopMargin = statusBarHeight;
            webView.LayoutParameters = layoutParams;
        }
    }

    private int GetStatusBarHeight()
    {
        var resources = Resources;
        var resourceId = resources.GetIdentifier("status_bar_height", "dimen", "android");
        if (resourceId > 0)
        {
            return resources.GetDimensionPixelSize(resourceId);
        }
        return (int)(24 * Resources.DisplayMetrics.Density);
    }

    private int GetNavigationBarHeight()
    {
        var resources = Resources;
        var resourceId = resources.GetIdentifier("navigation_bar_height", "dimen", "android");
        if (resourceId > 0)
        {
            return resources.GetDimensionPixelSize(resourceId);
        }
        return 0;
    }

    protected override void OnDestroy()
    {
        if (globalLayoutListener != null)
        {
            var rootView = FindViewById<View>(Android.Resource.Id.Content);
            rootView.ViewTreeObserver.RemoveOnGlobalLayoutListener(globalLayoutListener);
        }

        if (webView != null)
        {
            webView.StopLoading();
            webView.LoadUrl("about:blank");
            webView.RemoveAllViews();
            webView.Destroy();
            webView = null;
        }
        base.OnDestroy();
    }

    protected override void OnPause()
    {
        base.OnPause();
        CookieManager.Instance.Flush();
    }

    public override void OnBackPressed()
    {
        if (webView != null && webView.CanGoBack())
            webView.GoBack();
        else
            base.OnBackPressed();
    }
}

public class GlobalLayoutListener : Java.Lang.Object, ViewTreeObserver.IOnGlobalLayoutListener
{
    private readonly Action action;

    public GlobalLayoutListener(Action action)
    {
        this.action = action;
    }

    public void OnGlobalLayout()
    {
        action?.Invoke();
    }
}

public class MyWebViewClient : WebViewClient
{
    Context ctx;
    public MyWebViewClient(Context context) { ctx = context; }

    public override void OnPageFinished(WebView view, string url)
    {
        base.OnPageFinished(view, url);
    }

    public override void OnReceivedError(WebView view, IWebResourceRequest request, WebResourceError error)
    {
        base.OnReceivedError(view, request, error);
    }
}