using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Webkit;
using AndroidX.AppCompat.App;
using Java.Interop;
using System.Text.RegularExpressions;
using Android.Views;

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

    // request codes
    public const int FILECHOOSER_RESULTCODE = 1; // kept for file input
    private const int CREATE_FILE_REQUEST_CODE = 2; // for save-as dialog

    // pending download state
    private byte[]? pendingBytes;
    private string? pendingMime;
    private string? pendingFilename;
    private string? pendingDownloadUrl;

    private int leftPanelWidth = 0;

    public bool IsLeftPanelWidthKnown => leftPanelWidth > 0;

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
            { Gravity = GravityFlags.Right }
        };

        var webClient = new MyWebViewClient(this, webView);

        container.AddView(webView);
        SetContentView(container);

        AddStatusBarPadding();
        SetupGlobalLayoutListener();

        ConfigureWebView(webClient);
        webView.LoadUrl(SiteUrl);

        TryLoadLeftPanelWidth();
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
        webSettings.SetAppCacheMaxSize(20 * 1024 * 1024);
        webSettings.CacheMode = CacheModes.Default;

        var cookieManager = CookieManager.Instance;
        cookieManager.SetAcceptCookie(true);
        CookieManager.Instance.SetAcceptThirdPartyCookies(webView, true);

        webView.SetWebViewClient(webClient);
        webView.SetWebChromeClient(new MyWebChromeClient(this));

        // Add JS interface for receiving blob data (base64) from page
        webView.AddJavascriptInterface(new BlobDownloader(this), "BlobDownloader");

        // Handle downloads (including blob: links)
        webView.SetDownloadListener(new CustomDownloadListener(this, webView));

        SetupSwipeGestures();
    }

    private void SetupSwipeGestures()
    {
        var gestureDetector = new GestureDetector(this, new SwipeGestureListener
        {
            OnSwipeRight = () =>
            {
                ChangeLeftPanelVisibility(true);
            },
            OnSwipeLeft = () =>
            {
                ChangeLeftPanelVisibility(false);
            }
        });

        webView.SetOnTouchListener(new WebViewTouchListener(gestureDetector));
    }

    private void ChangeLeftPanelVisibility(bool visible)
    {
        int addedWidth = visible ? 0 : leftPanelWidth;
        webView.LayoutParameters.Width = Resources.DisplayMetrics.WidthPixels + addedWidth;
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

    private void TryLoadLeftPanelWidth()
    {
        var prefs = GetSharedPreferences("app_prefs", FileCreationMode.Private);
        if (prefs.Contains("left_panel_width"))
        {
            leftPanelWidth = prefs.GetInt("left_panel_width", 0);
            if (webView != null)
            {
                webView.LayoutParameters.Width = Resources.DisplayMetrics.WidthPixels + leftPanelWidth;
            }
        }
    }

    public void OnLeftPanelWidthCalculated(int width)
    {
        var prefs = GetSharedPreferences("app_prefs", FileCreationMode.Private);
        var editor = prefs.Edit();

        editor.PutInt("left_panel_width", width);
        leftPanelWidth = width;
        webView.LayoutParameters.Width = Resources.DisplayMetrics.WidthPixels + leftPanelWidth;

        editor.Apply();
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

        // Existing file chooser handling (for <input type="file">)
        if (requestCode == MyWebChromeClient.FILECHOOSER_RESULTCODE)
        {
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
            return;
        }

        // Handle save-as dialog result
        if (requestCode == CREATE_FILE_REQUEST_CODE)
        {
            if (resultCode != Result.Ok || data?.Data == null)
            {
                // user cancelled
                pendingBytes = null;
                pendingDownloadUrl = null;
                pendingFilename = null;
                pendingMime = null;
                return;
            }

            var uri = data.Data;

            // If we already have bytes (blob case), write them immediately
            if (pendingBytes != null)
            {
                try
                {
                    using var outStream = ContentResolver.OpenOutputStream(uri);
                    if (outStream != null)
                        outStream.Write(pendingBytes, 0, pendingBytes.Length);
                }
                catch (System.Exception ex)
                {
                    RunOnUiThread(() => Toast.MakeText(this, "Save failed: " + ex.Message, ToastLength.Long).Show());
                }
                finally
                {
                    pendingBytes = null;
                    pendingFilename = null;
                    pendingMime = null;
                }
            }
            else if (!string.IsNullOrEmpty(pendingDownloadUrl))
            {
                // Regular URL: download and write to selected Uri asynchronously
                var url = pendingDownloadUrl;
                var mime = pendingMime ?? "*/*";
                pendingDownloadUrl = null;
                pendingMime = null;

                Task.Run(async () =>
                {
                    try
                    {
                        using var http = new HttpClient();
                        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                        response.EnsureSuccessStatusCode();
                        using var inStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

                        using var outStream = ContentResolver.OpenOutputStream(uri);
                        if (outStream == null) throw new IOException("Unable to open output stream");

                        await inStream.CopyToAsync(outStream).ConfigureAwait(false);

                        RunOnUiThread(() => Toast.MakeText(this, "Saved", ToastLength.Short).Show());
                    }
                    catch (System.Exception ex)
                    {
                        RunOnUiThread(() => Toast.MakeText(this, "Download failed: " + ex.Message, ToastLength.Long).Show());
                    }
                });
            }

            return;
        }
    }

    public class GlobalLayoutListener : Java.Lang.Object, ViewTreeObserver.IOnGlobalLayoutListener
    {
        private readonly Action? action;

        public GlobalLayoutListener(Action? action) => this.action = action;

        public void OnGlobalLayout() => action?.Invoke();
    }

    public class SwipeGestureListener : GestureDetector.SimpleOnGestureListener
    {
        private const int SWIPE_THRESHOLD = 100;        // минимальное расстояние свайпа по X
        private const int SWIPE_VELOCITY_THRESHOLD = 100; // минимальная скорость

        // Действие на свайп вправо
        public Action OnSwipeRight { get; set; }

        // Новое действие на свайп влево
        public Action OnSwipeLeft { get; set; }

        public override bool OnFling(MotionEvent e1, MotionEvent e2, float velocityX, float velocityY)
        {
            if (e1 == null || e2 == null)
                return false;

            float diffX = e2.GetX() - e1.GetX();
            float diffY = e2.GetY() - e1.GetY();

            // проверяем горизонтальный свайп
            if (Math.Abs(diffX) > Math.Abs(diffY))
            {
                // свайп вправо
                if (diffX > SWIPE_THRESHOLD && Math.Abs(velocityX) > SWIPE_VELOCITY_THRESHOLD)
                {
                    OnSwipeRight?.Invoke(); // вызываем действие свайпа вправо
                    return true;
                }

                // свайп влево
                if (diffX < -SWIPE_THRESHOLD && Math.Abs(velocityX) > SWIPE_VELOCITY_THRESHOLD)
                {
                    OnSwipeLeft?.Invoke(); // вызываем действие свайпа влево
                    return true;
                }
            }

            return false;
        }
    }

    public class WebViewTouchListener : Java.Lang.Object, View.IOnTouchListener
    {
        private readonly GestureDetector _gestureDetector;

        public WebViewTouchListener(GestureDetector gestureDetector)
        {
            _gestureDetector = gestureDetector;
        }

        public bool OnTouch(View v, MotionEvent e)
        {
            _gestureDetector.OnTouchEvent(e);
            return false; // false, чтобы WebView продолжал обрабатывать скролл
        }
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

        int cnt = 0;

        public override void OnPageFinished(WebView view, string url)
        {
            cnt++;
            base.OnPageFinished(view, url);

            if (cnt != 3) // Я глубоко извиняюсь за такой подход, но сайт судя по всему делает либо 2 редиректа, либо динамически подгружает контент
                return;

            if (((MainActivity)activity).IsLeftPanelWidthKnown)
                return;

            view.Post(async () =>
        {
            try
            {
                await Task.Delay(5000);

                string html = await GetPageHtmlAsync(view);
                if (!html.Contains("<h2 class=\"title svelte-pu1tym\">Чаты</h2>")) // это не основная страница с левым меню
                    return;

                // принудительно временно выключаем hardware layer, чтобы Draw() рисовал содержимое
                view.SetLayerType(LayerType.Software, null);

                var bitmap = Bitmap.CreateBitmap(view.Width, view.Height, Bitmap.Config.Argb8888);
                var canvas = new Canvas(bitmap);
                view.Draw(canvas);

                // восстановим аппаратный рендер (или LayerType.None)
                view.SetLayerType(LayerType.Hardware, null);

                ((MainActivity)activity).OnLeftPanelWidthCalculated(GetLeftPannelWidth(bitmap));

                //InjectBitmapToPage(view, bitmap);
            }
            catch (System.Exception ex)
            {
                Android.Util.Log.Error("WebViewBitmap", ex.ToString());
            }
        });
        }

        private Task<string> GetPageHtmlAsync(WebView webView)
        {
            var tcs = new TaskCompletionSource<string>();

            webView.EvaluateJavascript(
                "(function() { return document.documentElement.outerHTML; })();",
                new IValueCallbackImplementation(tcs)
            );

            return tcs.Task;
        }

        // Реализация IValueCallback для C#
        class IValueCallbackImplementation : Java.Lang.Object, IValueCallback
        {
            private readonly TaskCompletionSource<string> _tcs;

            public IValueCallbackImplementation(TaskCompletionSource<string> tcs)
            {
                _tcs = tcs;
            }

            public void OnReceiveValue(Java.Lang.Object value)
            {
                string result = value?.ToString() ?? string.Empty;

                // Убираем экранирование JSON-строки
                result = result.Trim('"')
                               .Replace("\\u003C", "<")
                               .Replace("\\n", "\n")
                               .Replace("\\\"", "\"");

                _tcs.TrySetResult(result);
            }
        }


        private int GetLeftPannelWidth(Bitmap bitmap)
        {
            int y = bitmap.Height / 2;
            var FirstPixel = bitmap.GetPixel(1, y);
            for (int x = 1; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y) != FirstPixel)
                {
                    return x;
                }
            }

            return 0;
        }

        private void InjectBitmapToPage(WebView view, Bitmap bitmap, int maxThumbWidth = 600)
        {
            if (view == null || bitmap == null) return;

            // Создаём thumbnail, чтобы base64 не был гигантским
            int thumbW = System.Math.Min(bitmap.Width, maxThumbWidth);
            int thumbH = (int)(bitmap.Height * (thumbW / (double)System.Math.Max(1, bitmap.Width)));
            Bitmap thumb = Bitmap.CreateScaledBitmap(bitmap, thumbW, thumbH, true);

            // Кодируем thumbnail в PNG -> base64
            string base64;
            using (var ms = new MemoryStream())
            {
                thumb.Compress(Bitmap.CompressFormat.Png, 100, ms);
                base64 = Convert.ToBase64String(ms.ToArray());
            }

            // Освобождаем thumbnail, если он не тот же объект
            if (!thumb.Equals(bitmap))
            {
                try { thumb.Recycle(); } catch { /* ignore */ }
            }

            // Экранируем апострофы и переносы строк — чтобы безопасно вставить в JS-строку
            var esc = base64.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\r", "").Replace("\n", "");

            // JS: вставляет <div id="android_screenshot_preview"> с картинкой и кнопкой закрытия
            var js = $@"(function(){{
        try {{
            var id = 'android_screenshot_preview';
            var existing = document.getElementById(id);
            if(existing) existing.parentNode.removeChild(existing);

            var wrapper = document.createElement('div');
            wrapper.id = id;
            wrapper.style.position = 'fixed';
            wrapper.style.bottom = '12px';
            wrapper.style.right = '12px';
            wrapper.style.zIndex = '2147483647';
            wrapper.style.boxShadow = '0 4px 18px rgba(0,0,0,0.35)';
            wrapper.style.borderRadius = '8px';
            wrapper.style.overflow = 'hidden';
            wrapper.style.background = '#fff';
            wrapper.style.maxWidth = '40vw';
            wrapper.style.maxHeight = '60vh';
            wrapper.style.display = 'flex';
            wrapper.style.flexDirection = 'column';
            wrapper.style.alignItems = 'flex-end';

            var ctrl = document.createElement('div');
            ctrl.style.width = '100%';
            ctrl.style.display = 'flex';
            ctrl.style.justifyContent = 'flex-end';
            ctrl.style.padding = '4px';

            var close = document.createElement('button');
            close.textContent = '✕';
            close.style.border = 'none';
            close.style.background = 'transparent';
            close.style.fontSize = '16px';
            close.style.cursor = 'pointer';
            close.onclick = function(){{ if(wrapper && wrapper.parentNode) wrapper.parentNode.removeChild(wrapper); }};

            ctrl.appendChild(close);

            var img = document.createElement('img');
            img.src = 'data:image/png;base64,{esc}';
            img.style.display = 'block';
            img.style.width = '100%';
            img.style.height = 'auto';
            img.style.objectFit = 'contain';
            img.style.borderTop = '1px solid #e6e6e6';

            wrapper.appendChild(ctrl);
            wrapper.appendChild(img);
            document.body.appendChild(wrapper);
        }} catch(e) {{ console && console.log('inject error', e); }}
    }})();";

            // Выполняем JS в UI-потоке
            view.Post(() =>
            {
                try
                {
                    view.EvaluateJavascript(js, null);
                }
                catch (System.Exception ex)
                {
                    Android.Util.Log.Error("InjectBitmap", ex.ToString());
                }
            });
        } //оставлю это здесь на случай изменения дизайна сайта


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

    // Download listener that detects blob: URLs and normal URLs
    public class CustomDownloadListener : Java.Lang.Object, IDownloadListener
    {
        private readonly MainActivity activity;
        private readonly WebView webView;

        public CustomDownloadListener(MainActivity activity, WebView webView)
        {
            this.activity = activity;
            this.webView = webView;
        }

        public void OnDownloadStart(string url, string userAgent, string contentDisposition, string mimeType, long contentLength)
        {
            // Extract filename from contentDisposition if possible
            var filename = GetFileNameFromContentDisposition(contentDisposition) ?? $"download{GetExtensionFromMime(mimeType)}";

            if (url != null && url.StartsWith("blob:", StringComparison.OrdinalIgnoreCase))
            {
                // For blob: — fetch via JS and pass base64 to Android
                var escUrl = EscapeForJs(url);
                var escName = EscapeForJs(filename);
                var escMime = EscapeForJs(mimeType ?? "");

                // JS: fetch the blob and convert to dataURL, then send base64 to BlobDownloader.save(base64, filename, mime)
                var js = $@"(function() {{
var url = '{escUrl}';
var filename = '{escName}';
var mime = '{escMime}';
try {{
  var xhr = new XMLHttpRequest();
  xhr.open('GET', url);
  xhr.responseType = 'blob';
  xhr.onload = function() {{
    var blob = xhr.response;
    var reader = new FileReader();
    reader.onloadend = function() {{
      var dataUrl = reader.result || '';
      var base64 = dataUrl.split(',')[1] || '';
      try {{ window.BlobDownloader.save(base64, filename, mime); }} catch(e) {{ console.log('BlobDownloader.save failed', e); }}
    }};
    reader.readAsDataURL(blob);
  }};
  xhr.onerror = function(e) {{ console.log('xhr error', e); }};
  xhr.send();
}} catch(e) {{ console.log('blob download js error', e); }}
}})();";

                activity.RunOnUiThread(() => webView.EvaluateJavascript(js, null));
            }
            else
            {
                // Normal URL: open save dialog first, then download and write after user picks
                activity.pendingDownloadUrl = url;
                activity.pendingMime = mimeType;
                activity.pendingFilename = filename;

                var intent = new Intent(Intent.ActionCreateDocument);
                intent.AddCategory(Intent.CategoryOpenable);
                intent.SetType(mimeType ?? "*/*");
                intent.PutExtra(Intent.ExtraTitle, filename);

                try
                {
                    activity.StartActivityForResult(intent, CREATE_FILE_REQUEST_CODE);
                }
                catch (ActivityNotFoundException)
                {
                    activity.RunOnUiThread(() => Toast.MakeText(activity, "Cannot open save dialog", ToastLength.Short).Show());
                }
            }
        }

        private static string GetFileNameFromContentDisposition(string contentDisposition)
        {
            if (string.IsNullOrEmpty(contentDisposition)) return null;
            // filename="..." or filename=...
            var m = Regex.Match(contentDisposition, "filename\\*=UTF-8''(?<fname>[^;\\r\\n]+)");
            if (m.Success) return Uri.UnescapeDataString(m.Groups["fname"].Value.Trim('"'));
            m = Regex.Match(contentDisposition, "filename\\s*=\\s*\"(?<fname>[^\"]+)\"");
            if (m.Success) return m.Groups["fname"].Value;
            m = Regex.Match(contentDisposition, "filename\\s*=\\s*(?<fname>[^;\\r\\n]+)");
            if (m.Success) return m.Groups["fname"].Value.Trim('"');
            return null;
        }

        private static string GetExtensionFromMime(string mime)
        {
            if (string.IsNullOrEmpty(mime)) return "";
            // very small mapping — expand if needed
            if (mime.Contains("pdf")) return ".pdf";
            if (mime.Contains("zip")) return ".zip";
            if (mime.Contains("json")) return ".json";
            if (mime.Contains("jpeg") || mime.Contains("jpg")) return ".jpg";
            if (mime.Contains("png")) return ".png";
            return "";
        }

        private static string EscapeForJs(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\r", "\\r").Replace("\n", "\\n");
        }
    }

    // JS bridge to receive base64 data for blob files
    public class BlobDownloader : Java.Lang.Object
    {
        private readonly MainActivity activity;

        public BlobDownloader(MainActivity activity) => this.activity = activity;

        // Exported to JS: window.BlobDownloader.save(base64, filename, mime)
        [JavascriptInterface]
        [Export("save")]
        public void Save(string base64, string filename, string mime)
        {
            try
            {
                if (string.IsNullOrEmpty(base64)) return;

                // decode
                byte[] bytes = Convert.FromBase64String(base64);

                activity.pendingBytes = bytes;
                activity.pendingFilename = string.IsNullOrEmpty(filename) ? "download" : filename;
                activity.pendingMime = string.IsNullOrEmpty(mime) ? "*/*" : mime;

                // Open standard save dialog (Storage Access Framework)
                var intent = new Intent(Intent.ActionCreateDocument);
                intent.AddCategory(Intent.CategoryOpenable);
                intent.SetType(activity.pendingMime ?? "*/*");
                intent.PutExtra(Intent.ExtraTitle, activity.pendingFilename);

                activity.RunOnUiThread(() =>
                {
                    try
                    {
                        activity.StartActivityForResult(intent, CREATE_FILE_REQUEST_CODE);
                    }
                    catch (ActivityNotFoundException)
                    {
                        Toast.MakeText(activity, "Cannot open save dialog", ToastLength.Short).Show();
                    }
                });
            }
            catch (System.Exception ex)
            {
                activity.RunOnUiThread(() => Toast.MakeText(activity, "Save failed: " + ex.Message, ToastLength.Long).Show());
            }
        }
    }
}