using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Webkit;
using Android.Widget;
using AndroidX.AppCompat.App;
using Java.Lang;
using System;

[Activity(Label = "WebMaxApp", MainLauncher = true,
          Theme = "@style/Theme.AppCompat.DayNight.NoActionBar",
          Name = "iMAXimilian.iMAXimilian.MainActivity",
          WindowSoftInputMode = SoftInput.AdjustResize)]
public class MainActivity : AppCompatActivity
{
    private WebView? webView;
    private GlobalLayoutListener? globalLayoutListener;

    private const string SiteUrl = "https://web.max.ru";
    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
        "AppleWebKit/537.36 (KHTML, like Gecko) " +
        "Chrome/120.0.0.0 Safari/537.36";

    protected override void OnCreate(Bundle? savedInstanceState)
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

        var webClient = new MyWebViewClient(this, webView);

        container.AddView(webView);
        SetContentView(container);

        AddStatusBarPadding();
        SetupGlobalLayoutListener();

        ConfigureWebView(webClient);
        webView.LoadUrl(SiteUrl);
    }

    private void ConfigureWebView(WebViewClient webClient)
    {
        if (webView == null) return;

        var webSettings = webView.Settings;
        webSettings.JavaScriptEnabled = true;
        webSettings.DomStorageEnabled = true;
        webSettings.SetSupportZoom(true);
        webSettings.LoadWithOverviewMode = true;
        webSettings.UseWideViewPort = true;
        webSettings.UserAgentString = DesktopUserAgent;

        webSettings.SetAppCacheEnabled(true);
        webSettings.SetAppCachePath(CacheDir.AbsolutePath);
        webSettings.SetAppCacheMaxSize(10 * 1024 * 1024);
        webSettings.CacheMode = CacheModes.Default;

        var cookieManager = CookieManager.Instance;
        cookieManager.SetAcceptCookie(true);
        CookieManager.Instance.SetAcceptThirdPartyCookies(webView, true);

        webView.SetWebViewClient(webClient);
        webView.SetWebChromeClient(new MyWebChromeClient(this));
    }

    private void SetupGlobalLayoutListener()
    {
        var rootView = FindViewById<View>(Android.Resource.Id.Content);
        if (rootView == null) return;
        globalLayoutListener = new GlobalLayoutListener(AdjustWebViewForKeyboard);
        rootView.ViewTreeObserver.AddOnGlobalLayoutListener(globalLayoutListener);
    }

    private void AdjustWebViewForKeyboard()
    {
        var rootView = FindViewById<View>(Android.Resource.Id.Content);
        if (rootView == null || webView == null) return;

        var rect = new Rect();
        rootView.GetWindowVisibleDisplayFrame(rect);

        int screenHeight = rootView.Height;
        int keypadHeight = screenHeight - rect.Bottom;

        if (webView.LayoutParameters is LinearLayout.LayoutParams layoutParams)
        {
            if (keypadHeight > screenHeight * 0.15)
            {
                int statusBarHeight = GetStatusBarHeight();
                int navigationBarHeight = GetNavigationBarHeight();
                layoutParams.Height = screenHeight - keypadHeight - statusBarHeight - navigationBarHeight;
            }
            else
            {
                layoutParams.Height = ViewGroup.LayoutParams.MatchParent;
            }

            webView.LayoutParameters = layoutParams;
        }
    }

    private void AddStatusBarPadding()
    {
        if (webView == null) return;

        int statusBarHeight = GetStatusBarHeight();
        if (webView.LayoutParameters is LinearLayout.LayoutParams layoutParams)
        {
            layoutParams.TopMargin = statusBarHeight;
            webView.LayoutParameters = layoutParams;
        }
    }

    private int GetStatusBarHeight()
        => GetSystemDimension("status_bar_height", (int)(24 * Resources.DisplayMetrics.Density));

    private int GetNavigationBarHeight()
        => GetSystemDimension("navigation_bar_height", 0);

    private int GetSystemDimension(string name, int fallback)
    {
        var resources = Resources;
        int resourceId = resources.GetIdentifier(name, "dimen", "android");
        return resourceId > 0 ? resources.GetDimensionPixelSize(resourceId) : fallback;
    }

    protected override void OnDestroy()
    {
        try
        {
            var rootView = FindViewById<View>(Android.Resource.Id.Content);
            if (globalLayoutListener != null && rootView != null)
            {
                rootView.ViewTreeObserver.RemoveOnGlobalLayoutListener(globalLayoutListener);
                globalLayoutListener = null;
            }

            if (webView != null)
            {
                webView.StopLoading();
                webView.LoadUrl("about:blank");
                webView.RemoveAllViews();

                // Ensure proper removal from parent before destroy
                if (webView.Parent is ViewGroup parent)
                    parent.RemoveView(webView);

                webView.Destroy();
                webView = null;
            }
        }
        finally
        {
            base.OnDestroy();
        }
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

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);

        if (requestCode != MyWebChromeClient.FILECHOOSER_RESULTCODE) return;
        if (webView?.WebChromeClient is not MyWebChromeClient chromeClient) return;

        var callback = chromeClient.FilePathCallback;
        if (callback == null) return;

        Android.Net.Uri[]? result = null;

        if (resultCode == Result.Ok)
        {
            if (data == null)
            {
                // Камера/другие приложения иногда возвращают null — возвращаем пустой массив
                result = Array.Empty<Android.Net.Uri>();
            }
            else if (data.ClipData != null)
            {
                int count = data.ClipData.ItemCount;
                result = new Android.Net.Uri[count];
                for (int i = 0; i < count; i++)
                    result[i] = data.ClipData.GetItemAt(i).Uri;
            }
            else if (data.Data != null)
            {
                result = new[] { data.Data };
            }
        }

        callback.OnReceiveValue(result);
        chromeClient.FilePathCallback = null;
    }
}

public class GlobalLayoutListener : Java.Lang.Object, ViewTreeObserver.IOnGlobalLayoutListener
{
    private readonly Action? action;

    public GlobalLayoutListener(Action? action) => this.action = action;

    public void OnGlobalLayout() => action?.Invoke();
}

public class MyWebViewClient : WebViewClient
{
    private readonly Activity activity;
    private readonly WebView webView;
    public event Action<double>? OnNavWidthReceived;

    public MyWebViewClient(Activity activity, WebView webView)
    {
        this.activity = activity ?? throw new ArgumentNullException(nameof(activity));
        this.webView = webView ?? throw new ArgumentNullException(nameof(webView));
    }

    public override void OnPageStarted(WebView view, string url, Android.Graphics.Bitmap? favicon)
    {
        base.OnPageStarted(view, url, favicon);
    }

    public override void OnPageFinished(WebView view, string url)
    {
        base.OnPageFinished(view, url);
    }
}

public class MyWebChromeClient : WebChromeClient
{
    public IValueCallback? FilePathCallback { get; set; }
    public const int FILECHOOSER_RESULTCODE = 1;
    private readonly Activity activity;

    public MyWebChromeClient(Activity activity) => this.activity = activity;

    public override bool OnShowFileChooser(WebView webView, IValueCallback filePathCallback, FileChooserParams fileChooserParams)
    {
        FilePathCallback = filePathCallback;

        Intent intent = fileChooserParams.CreateIntent();
        try
        {
            activity.StartActivityForResult(intent, FILECHOOSER_RESULTCODE);
        }
        catch (ActivityNotFoundException)
        {
            FilePathCallback = null;
            Toast.MakeText(activity, "Cannot open file chooser", ToastLength.Short).Show();
            return false;
        }
        return true;
    }
}