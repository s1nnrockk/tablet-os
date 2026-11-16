using System.Globalization;
using System.Runtime.InteropServices;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace TabletOS.Android;

[Activity(Label = "Tablet OS", MainLauncher = true, Exported = true, Theme = "@android:style/Theme.Material.Light.NoActionBar")]
[IntentFilter(new[] { Intent.ActionMain }, Categories = new[] { Intent.CategoryHome, Intent.CategoryDefault })]
public sealed class MainActivity : Activity
{
    private const int HomeRoleRequestCode = 4101;
    private const int WallpaperRequestCode = 4102;
    private const int AppBackgroundRequestCode = 4103;
    private const int LockWallpaperRequestCode = 4104;
    private const int LockForegroundRequestCode = 4105;
    private const int AppColumns = 5;

    private RoleManager? _roleManager;
    private TextView _clock = null!;
    private TextView _date = null!;
    private TextView _emptyState = null!;
    private LinearLayout _appGrid = null!;
    private LinearLayout _dock = null!;
    private EditText _search = null!;
    private Button _setLauncherButton = null!;
    private FrameLayout _root = null!;
    private FrameLayout _controlCenter = null!;
    private FrameLayout _notificationCenter = null!;
    private FrameLayout _lockScreen = null!;
    private ImageView? _lockBackground;
    private ImageView? _lockForeground;
    private TextView? _lockClock;
    private TextView? _lockDate;
    private int _lockClockPosition = 0;
    private int _lockClockStyle = 0;
    private int _lockClockFont = 0;
    private int _lockClockColor = 0;
    private float _lockTouchX;
    private float _lockTouchY;
    private string? _pendingBackgroundPackage;
    private ImageView _wallpaperView = null!;
    private bool _returnAnimationPending;
    private float _returnPivotX;
    private float _returnPivotY;
    private readonly List<AppEntry> _apps = new();

    private sealed record AppEntry(string PackageName, string Label, Drawable Icon);

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _roleManager = GetSystemService(Context.RoleService) as RoleManager;
        Window?.SetStatusBarColor(Color.ParseColor("#EAF1F8"));
        Window?.SetNavigationBarColor(Color.ParseColor("#F5F3EF"));

        BuildHomeScreen();
        LoadApps();

        if (!IsDefaultLauncher())
            RequestHomeRole();
    }

    protected override void OnResume()
    {
        base.OnResume();
        if (_returnAnimationPending && _root is not null)
            PlayGenieReturnAnimation();
        if (_clock is not null)
            UpdateClock();
        if (_appGrid is not null)
            LoadApps();
    }

    private void BuildHomeScreen()
    {
        var scroll = new ScrollView(this)
        {
            FillViewport = true,
            OverScrollMode = OverScrollMode.Never
        };
        scroll.SetBackgroundColor(Color.Transparent);

        var page = new LinearLayout(this) { Orientation = Orientation.Vertical };
        page.SetPadding(Dp(26), Dp(22), Dp(26), Dp(124));

        var topLine = new LinearLayout(this)
        {
            Orientation = Orientation.Horizontal,
            Gravity = GravityFlags.CenterVertical
        };

        var brand = MakeText("TABLET OS", 13, "#526579", true);
        var topSpacer = new Space(this);
        var wallpaperButton = MakePillButton("Обои", "#FFFFFF");
        wallpaperButton.Click += (_, _) => ChooseWallpaper();
        var lockButton = MakePillButton("Экран блокировки", "#E7F0FA");
        lockButton.Click += (_, _) => OpenLockScreen();
        var notificationButton = MakePillButton("Уведомления", "#E7F0FA");
        notificationButton.Click += (_, _) => OpenNotificationCenter();
        var centerButton = MakePillButton("Быстрые настройки", "#E7F0FA");
        centerButton.Click += (_, _) => OpenControlCenter();
        var dateTop = MakeText("", 12, "#657487", false);
        topLine.AddView(brand, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(34)));
        topLine.AddView(topSpacer, new LinearLayout.LayoutParams(0, 1, 1));
        topLine.AddView(wallpaperButton, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(34)));
        topLine.AddView(lockButton, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(34)));
        topLine.AddView(notificationButton, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(34)));
        topLine.AddView(centerButton, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(34)));
        topLine.AddView(dateTop, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(34)));
        page.AddView(topLine);

        var hero = new LinearLayout(this) { Orientation = Orientation.Vertical };
        hero.SetPadding(Dp(25), Dp(22), Dp(25), Dp(22));
        hero.Background = RoundedGradient("#DCEBFA", "#E8E4F7", 30);

        var greeting = MakeText("Добро пожаловать", 15, "#52677E", false);
        _clock = MakeText("09:41", 58, "#20364D", true);
        _clock.SetPadding(0, Dp(3), 0, 0);
        _date = MakeText("", 15, "#52677E", false);
        _date.SetPadding(0, Dp(1), 0, Dp(15));

        var heroBottom = new LinearLayout(this)
        {
            Orientation = Orientation.Horizontal,
            Gravity = GravityFlags.CenterVertical
        };
        var focus = MakeText("Ваш планшет. Ваше пространство.", 13, "#52677E", false);
        var heroSpacer = new Space(this);
        var orb = new TextView(this)
        {
            Text = "✦",
            TextSize = 20,
            Gravity = GravityFlags.Center,
            Background = Rounded("#FFFFFF", 100)
        };
        orb.SetTextColor(Color.ParseColor("#6684A5"));
        heroBottom.AddView(focus, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(34)));
        heroBottom.AddView(heroSpacer, new LinearLayout.LayoutParams(0, 1, 1));
        heroBottom.AddView(orb, new LinearLayout.LayoutParams(Dp(38), Dp(38)));

        hero.AddView(greeting);
        hero.AddView(_clock);
        hero.AddView(_date);
        hero.AddView(heroBottom);
        var heroParams = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
        heroParams.TopMargin = Dp(17);
        page.AddView(hero, heroParams);

        _search = new EditText(this)
        {
            Hint = "Поиск приложений",
            SingleLine = true,
            TextSize = 15,
            InputType = Android.Text.InputTypes.ClassText
        };
        _search.SetTextColor(Color.ParseColor("#26384A"));
        _search.SetHintTextColor(Color.ParseColor("#8492A0"));
        _search.SetPadding(Dp(18), 0, Dp(18), 0);
        _search.Background = Rounded("#FFFFFF", 19);
        _search.Elevation = Dp(2);
        _search.TextChanged += (_, e) => RenderApps(e.Text?.ToString() ?? string.Empty);
        var searchParams = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(54));
        searchParams.TopMargin = Dp(20);
        page.AddView(_search, searchParams);

        var appHeading = new LinearLayout(this)
        {
            Orientation = Orientation.Horizontal,
            Gravity = GravityFlags.CenterVertical
        };
        var headingText = MakeText("Приложения", 21, "#26384A", true);
        var appCount = MakeText("УСТАНОВЛЕННЫЕ", 10, "#8190A0", true);
        appHeading.AddView(headingText, new LinearLayout.LayoutParams(0, Dp(48), 1));
        appHeading.AddView(appCount, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(48)));
        var headingParams = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(48));
        headingParams.TopMargin = Dp(14);
        page.AddView(appHeading, headingParams);

        _appGrid = new LinearLayout(this) { Orientation = Orientation.Vertical };
        page.AddView(_appGrid, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

        _emptyState = MakeText("Приложения не найдены", 14, "#718092", false);
        _emptyState.Gravity = GravityFlags.Center;
        _emptyState.Visibility = ViewStates.Gone;
        _emptyState.SetPadding(0, Dp(28), 0, Dp(28));
        page.AddView(_emptyState);

        var footer = MakeText("Tablet OS  ·  Ваш новый главный экран", 11, "#8995A1", false);
        footer.Gravity = GravityFlags.Center;
        var footerParams = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(48));
        footerParams.TopMargin = Dp(12);
        page.AddView(footer, footerParams);

        scroll.AddView(page);
        _root = new FrameLayout(this);
        _wallpaperView = new ImageView(this) { ScaleType = ImageView.ScaleType.CenterCrop };
        _root.AddView(_wallpaperView, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
        ApplySavedWallpaper();
        _root.AddView(scroll, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
        _dock = BuildDock();
        var dockParams = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(82), GravityFlags.Bottom | GravityFlags.CenterHorizontal);
        dockParams.SetMargins(Dp(22), 0, Dp(22), Dp(16));
        _root.AddView(_dock, dockParams);
        _controlCenter = BuildControlCenter();
        _root.AddView(_controlCenter, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
        _notificationCenter = BuildNotificationCenter();
        _root.AddView(_notificationCenter, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
        _lockScreen = BuildLockScreen();
        _root.AddView(_lockScreen, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
        SetContentView(_root);
        UpdateClock();

        dateTop.Text = DateTime.Now.ToString("dddd, d MMMM", CultureInfo.GetCultureInfo("ru-RU"));
    }

    private Button MakePillButton(string text, string background)
    {
        var button = new Button(this) { Text = text, AllCaps = false, TextSize = 11 };
        button.SetTextColor(Color.ParseColor("#344B62"));
        button.Background = Rounded(background, 18);
        button.SetPadding(Dp(10), 0, Dp(10), 0);
        return button;
    }

    private FrameLayout BuildControlCenter()
    {
        var overlay = new FrameLayout(this) { Visibility = ViewStates.Gone, Clickable = true, Focusable = true };
        var scrim = new View(this);
        scrim.SetBackgroundColor(Color.ParseColor("#660D1B2A"));
        scrim.Click += (_, _) => CloseControlCenter();
        overlay.AddView(scrim, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

        var panel = new LinearLayout(this) { Orientation = Orientation.Vertical };
        panel.SetPadding(Dp(22), Dp(24), Dp(22), Dp(24));
        panel.Background = RoundedGradient("#F4FAFFFF", "#E9F0F8", 30);
        panel.Elevation = Dp(18);

        var heading = new LinearLayout(this) { Orientation = Orientation.Horizontal, Gravity = GravityFlags.CenterVertical };
        var title = MakeText("Центр управления", 23, "#20364D", true);
        heading.AddView(title, new LinearLayout.LayoutParams(0, Dp(44), 1));
        var close = MakePillButton("Готово", "#FFFFFF");
        close.Click += (_, _) => CloseControlCenter();
        heading.AddView(close, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(36)));
        panel.AddView(heading);

        var now = MakeText(DateTime.Now.ToString("dddd, d MMMM · HH:mm", CultureInfo.GetCultureInfo("ru-RU")), 13, "#718297", false);
        now.SetPadding(0, 0, 0, Dp(15));
        panel.AddView(now);

        var tiles = new LinearLayout(this) { Orientation = Orientation.Vertical };
        var tileRow1 = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        var tileRow2 = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        AddControlTile(tileRow1, "Wi-Fi", "Подключения", () => OpenSystemPanel("android.settings.panel.action.WIFI"));
        AddControlTile(tileRow1, "Bluetooth", "Устройства", () => OpenSystemPanel("android.settings.panel.action.BLUETOOTH"));
        AddControlTile(tileRow2, "Режим полёта", "Системные настройки", () => OpenSystemPanel("android.settings.AIRPLANE_MODE_SETTINGS"));
        AddControlTile(tileRow2, "Настройки", "Все параметры", () => OpenSystemPanel("android.settings.SETTINGS"));
        tiles.AddView(tileRow1, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(78)));
        tiles.AddView(tileRow2, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(78)));
        panel.AddView(tiles, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(156)));

        var volumeTitle = MakeText("Громкость", 15, "#344B62", true);
        volumeTitle.SetPadding(0, Dp(18), 0, Dp(4));
        panel.AddView(volumeTitle);
        var audio = (Android.Media.AudioManager?)GetSystemService(AudioService);
        var volume = new SeekBar(this) { Max = audio?.GetStreamMaxVolume(Android.Media.Stream.Music) ?? 15 };
        volume.Progress = audio?.GetStreamVolume(Android.Media.Stream.Music) ?? 5;
        volume.ProgressChanged += (_, e) =>
        {
            if (e.FromUser) audio?.SetStreamVolume(Android.Media.Stream.Music, e.Progress, Android.Media.VolumeNotificationFlags.RemoveSoundAndVibrate);
        };
        panel.AddView(volume, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(42)));

        var quickHint = MakeText("Уведомления доступны отдельным свайпом вниз", 12, "#718297", false);
        quickHint.SetPadding(0, Dp(18), 0, Dp(8));
        panel.AddView(quickHint);


        var panelParams = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, (int)(Resources!.DisplayMetrics!.HeightPixels * 0.88f), GravityFlags.Top);
        panelParams.SetMargins(Dp(8), Dp(8), Dp(8), 0);
        overlay.AddView(panel, panelParams);
        return overlay;
    }

    private FrameLayout BuildLockScreen()
    {
        _lockClockPosition = GetSharedPreferences("tablet_os_settings", FileCreationMode.Private)?.GetInt("lock_clock_position", _lockClockPosition) ?? _lockClockPosition;
        var overlay = new FrameLayout(this) { Visibility = ViewStates.Gone, Clickable = true, Focusable = true };
        var bg = new ImageView(this) { ScaleType = ImageView.ScaleType.CenterCrop };
        _lockBackground = bg;
        var savedWallpaper = GetSharedPreferences("tablet_os_settings", FileCreationMode.Private)?.GetString("lock_wallpaper_uri", null);
        if (!string.IsNullOrWhiteSpace(savedWallpaper))
        {
            try { bg.SetImageURI(Android.Net.Uri.Parse(savedWallpaper)); } catch { }
        }
        else
        {
            bg.SetImageDrawable(new Android.Graphics.Drawables.GradientDrawable(
                Android.Graphics.Drawables.GradientDrawable.Orientation.TlBr,
                new[] { Color.ParseColor("#183B57"), Color.ParseColor("#668DAA"), Color.ParseColor("#D8C9B7") }));
        }
        overlay.AddView(bg, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

        var shade = new View(this);
        shade.SetBackground(new Android.Graphics.Drawables.GradientDrawable(
            Android.Graphics.Drawables.GradientDrawable.Orientation.TopBottom,
            new[] { Color.ParseColor("#55091A2A"), Color.ParseColor("#00101A28"), Color.ParseColor("#77091A2A") }));
        overlay.AddView(shade, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

        var clock = new TextView(this)
        {
            Text = DateTime.Now.ToString("HH:mm"),
            TextSize = 76,
            Gravity = GravityFlags.Center,
            Typeface = Typeface.Create("sans-serif-light", TypefaceStyle.Normal),
            LetterSpacing = -0.035f,
        };
        _lockClock = clock;
        ApplyLockClockPreferences(clock);
        var clockParams = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(116), GravityFlags.Top);
        clockParams.TopMargin = Dp(_lockClockPosition == 0 ? 104 : 210);
        overlay.AddView(clock, clockParams);

        var date = MakeText(DateTime.Now.ToString("dddd, d MMMM", CultureInfo.GetCultureInfo("ru-RU")), 17, "#FFFFFFFF", false);
        date.Gravity = GravityFlags.Center;
        _lockDate = date;
        var dateParams = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(34), GravityFlags.Top);
        dateParams.TopMargin = Dp(_lockClockPosition == 0 ? 207 : 313);
        overlay.AddView(date, dateParams);

        var foreground = new ImageView(this) { ScaleType = ImageView.ScaleType.FitXY };
        _lockForeground = foreground;
        var savedForeground = GetSharedPreferences("tablet_os_settings", FileCreationMode.Private)?.GetString("lock_foreground_uri", null);
        if (!string.IsNullOrWhiteSpace(savedForeground))
        {
            try { foreground.SetImageURI(Android.Net.Uri.Parse(savedForeground)); } catch { }
        }
        // Put a transparent PNG cut-out here: it is layered above the clock to create
        // the iOS-style depth effect without pretending Android can segment any photo.
        overlay.AddView(foreground, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

        var bottom = new LinearLayout(this) { Orientation = Orientation.Vertical, Gravity = GravityFlags.Center };
        bottom.SetPadding(Dp(20), Dp(12), Dp(20), Dp(28));
        var notif = MakeText("Уведомления появятся здесь", 13, "#FFFFFFFF", false);
        notif.Gravity = GravityFlags.Center;
        var count = NotificationCaptureService.GetRecent().Count;
        notif.Text = count == 0 ? "Нет новых уведомлений" : $"{count} уведомлений";
        bottom.AddView(notif, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(42)));

        var unlock = MakePillButton("Проведите вверх, чтобы открыть", "#EFFFFFFF");
        unlock.TextSize = 14;
        unlock.SetTextColor(Color.ParseColor("#23394F"));
        unlock.Click += (_, _) => CloseLockScreen();
        bottom.AddView(unlock, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(54)));

        var customize = new LinearLayout(this) { Orientation = Orientation.Vertical, Gravity = GravityFlags.Center };
        var customizeTop = new LinearLayout(this) { Orientation = Orientation.Horizontal, Gravity = GravityFlags.Center };
        var customizeBottom = new LinearLayout(this) { Orientation = Orientation.Horizontal, Gravity = GravityFlags.Center };
        var wallpaper = MakePillButton("Обои", "#55FFFFFF");
        wallpaper.Click += (_, _) => ChooseLockWallpaper();
        var depth = MakePillButton("Слой глубины", "#55FFFFFF");
        depth.Click += (_, _) => ChooseLockForeground();
        var position = MakePillButton("Положение", "#55FFFFFF");
        position.Click += (_, _) => CycleLockClockPosition();
        var style = MakePillButton("Стиль часов", "#55FFFFFF");
        style.Click += (_, _) => ChooseLockClockStyle();
        var font = MakePillButton("Шрифт", "#55FFFFFF");
        font.Click += (_, _) => ChooseLockClockFont();
        var color = MakePillButton("Цвет", "#55FFFFFF");
        color.Click += (_, _) => ChooseLockClockColor();
        customizeTop.AddView(wallpaper, new LinearLayout.LayoutParams(0, Dp(42), 1));
        customizeTop.AddView(depth, new LinearLayout.LayoutParams(0, Dp(42), 1));
        customizeTop.AddView(position, new LinearLayout.LayoutParams(0, Dp(42), 1));
        customizeBottom.AddView(style, new LinearLayout.LayoutParams(0, Dp(42), 1));
        customizeBottom.AddView(font, new LinearLayout.LayoutParams(0, Dp(42), 1));
        customizeBottom.AddView(color, new LinearLayout.LayoutParams(0, Dp(42), 1));
        customize.AddView(customizeTop);
        customize.AddView(customizeBottom);
        bottom.AddView(customize, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(88)));

        var bottomParams = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent, GravityFlags.Bottom);
        overlay.AddView(bottom, bottomParams);

        overlay.Touch += (_, e) =>
        {
            if (e.Event is null) return;
            if (e.Event.Action == MotionEventActions.Down)
            {
                _lockTouchX = e.Event.GetX();
                _lockTouchY = e.Event.GetY();
            }
            else if (e.Event.Action == MotionEventActions.Move)
            {
                var dx = Math.Clamp((e.Event.GetX() - _lockTouchX) / Dp(18), -1.4f, 1.4f);
                var dy = Math.Clamp((e.Event.GetY() - _lockTouchY) / Dp(18), -1.4f, 1.4f);
                bg.TranslationX = dx * Dp(9);
                bg.TranslationY = dy * Dp(9);
                clock.TranslationX = -dx * Dp(5);
                clock.TranslationY = -dy * Dp(5);
                foreground.TranslationX = -dx * Dp(17);
                foreground.TranslationY = -dy * Dp(17);
            }
            else if (e.Event.Action == MotionEventActions.Up && e.Event.GetY() - _lockTouchY < -Dp(70))
                CloseLockScreen();
        };
        return overlay;
    }

    private void OpenLockScreen()
    {
        if (_lockScreen is null) return;
        _lockClock!.Text = DateTime.Now.ToString("HH:mm");
        _lockScreen.Visibility = ViewStates.Visible;
        _lockScreen.Alpha = 0f;
        _lockScreen.TranslationY = Dp(35);
        _lockScreen.Animate()?.Alpha(1f)?.TranslationY(0)?.SetDuration(420)
            ?.SetInterpolator(new Android.Views.Animations.DecelerateInterpolator(1.7f))?.Start();
    }

    private void CloseLockScreen()
    {
        if (_lockScreen is null) return;
        _lockScreen.Animate()?.Alpha(0f)?.TranslationY(-Dp(45))?.SetDuration(260)
            ?.WithEndAction(new Java.Lang.Runnable(() =>
            {
                if (_lockScreen is not null) _lockScreen.Visibility = ViewStates.Gone;
                if (_lockScreen is not null) _lockScreen.TranslationY = 0;
            }))?.Start();
    }

    private void ChooseLockWallpaper()
    {
        var picker = new Intent(Intent.ActionOpenDocument);
        picker.SetType("image/*");
        picker.AddCategory(Intent.CategoryOpenable);
        StartActivityForResult(picker, LockWallpaperRequestCode);
    }

    private void ChooseLockForeground()
    {
        var picker = new Intent(Intent.ActionOpenDocument);
        picker.SetType("image/*");
        picker.AddCategory(Intent.CategoryOpenable);
        StartActivityForResult(picker, LockForegroundRequestCode);
    }

    private void ApplyLockWallpaper(Android.Net.Uri uri)
    {
        try
        {
            ContentResolver?.TakePersistableUriPermission(uri, ActivityFlags.GrantReadUriPermission);
            GetSharedPreferences("tablet_os_settings", FileCreationMode.Private)?.Edit()?.PutString("lock_wallpaper_uri", uri.ToString())?.Apply();
            _lockBackground?.SetImageURI(uri);
            Toast.MakeText(this, "Обои экрана блокировки сохранены", ToastLength.Short)?.Show();
        }
        catch { Toast.MakeText(this, "Не удалось применить обои", ToastLength.Short)?.Show(); }
    }

    private void ApplyLockForeground(Android.Net.Uri uri)
    {
        try
        {
            ContentResolver?.TakePersistableUriPermission(uri, ActivityFlags.GrantReadUriPermission);
            GetSharedPreferences("tablet_os_settings", FileCreationMode.Private)?.Edit()?.PutString("lock_foreground_uri", uri.ToString())?.Apply();
            _lockForeground?.SetImageURI(uri);
            Toast.MakeText(this, "Слой глубины сохранён. Используй PNG с прозрачным фоном.", ToastLength.Long)?.Show();
        }
        catch { Toast.MakeText(this, "Не удалось применить слой", ToastLength.Short)?.Show(); }
    }

    private void ApplyLockClockPreferences(TextView clock)
    {
        var prefs = GetSharedPreferences("tablet_os_settings", FileCreationMode.Private);
        _lockClockStyle = prefs?.GetInt("lock_clock_style", _lockClockStyle) ?? _lockClockStyle;
        _lockClockFont = prefs?.GetInt("lock_clock_font", _lockClockFont) ?? _lockClockFont;
        _lockClockColor = prefs?.GetInt("lock_clock_color", _lockClockColor) ?? _lockClockColor;

        var styles = new[]
        {
            (Size: 76f, Family: "sans-serif-light", Weight: TypefaceStyle.Normal, Spacing: -0.035f, Format: "HH:mm"),
            (Size: 72f, Family: "sans-serif-rounded", Weight: TypefaceStyle.Normal, Spacing: -0.02f, Format: "HH:mm"),
            (Size: 72f, Family: "serif", Weight: TypefaceStyle.Normal, Spacing: 0f, Format: "HH:mm"),
            (Size: 70f, Family: "sans-serif", Weight: TypefaceStyle.Bold, Spacing: -0.025f, Format: "HH:mm"),
            (Size: 62f, Family: "monospace", Weight: TypefaceStyle.Normal, Spacing: -0.04f, Format: "HH:mm")
        };
        var fonts = new[] { "sans-serif-light", "sans-serif", "sans-serif-rounded", "serif", "monospace" };
        var colors = new[] { "#FFFFFFFF", "#FFFFF1D6", "#FFD9F0FF", "#FFD9FFE9", "#FFFFE0E8", "#FFE5DEFF", "#FF202A36", "#FF101820" };
        var style = styles[Math.Clamp(_lockClockStyle, 0, styles.Length - 1)];
        clock.TextSize = style.Size;
        clock.Typeface = Typeface.Create(fonts[Math.Clamp(_lockClockFont, 0, fonts.Length - 1)], style.Weight);
        clock.LetterSpacing = style.Spacing;
        clock.Text = DateTime.Now.ToString(style.Format);
        var customColor = prefs?.GetString("lock_clock_custom_color", "#FFFFFFFF") ?? "#FFFFFFFF";
        var selectedColor = _lockClockColor == 8 ? customColor : colors[Math.Clamp(_lockClockColor, 0, colors.Length - 1)];
        clock.SetTextColor(Color.ParseColor(selectedColor));
        clock.SetShadowLayer(12f, 0f, 2f, Color.Argb(110, 0, 0, 0));
    }

    private void ChooseLockClockStyle()
    {
        var options = new[] { "iOS тонкий", "Мягкий округлый", "Классический serif", "Жирный", "Цифровой" };
        new AlertDialog.Builder(this)
            .SetTitle("Стиль часов")
            .SetItems(options, (_, e) =>
            {
                _lockClockStyle = e.Which;
                GetSharedPreferences("tablet_os_settings", FileCreationMode.Private)?.Edit()?.PutInt("lock_clock_style", _lockClockStyle)?.Apply();
                ApplyLockClockPreferences(_lockClock!);
            })
            .SetNegativeButton("Отмена", (_, _) => { })
            .Show();
    }

    private void ChooseLockClockFont()
    {
        var options = new[] { "SF-подобный тонкий", "Системный", "Округлый", "С засечками", "Моноширинный" };
        new AlertDialog.Builder(this)
            .SetTitle("Шрифт часов")
            .SetItems(options, (_, e) =>
            {
                _lockClockFont = e.Which;
                GetSharedPreferences("tablet_os_settings", FileCreationMode.Private)?.Edit()?.PutInt("lock_clock_font", _lockClockFont)?.Apply();
                ApplyLockClockPreferences(_lockClock!);
            })
            .SetNegativeButton("Отмена", (_, _) => { })
            .Show();
    }

    private void ChooseLockClockColor()
    {
        var names = new[] { "Белый", "Тёплый кремовый", "Ледяной голубой", "Мятный", "Розовый", "Лавандовый", "Графитовый", "Почти чёрный", "Свой цвет…" };
        new AlertDialog.Builder(this)
            .SetTitle("Цвет часов")
            .SetItems(names, (_, e) =>
            {
                if (e.Which == 8)
                {
                    ShowCustomLockClockColor();
                    return;
                }
                _lockClockColor = e.Which;
                GetSharedPreferences("tablet_os_settings", FileCreationMode.Private)?.Edit()?.PutInt("lock_clock_color", _lockClockColor)?.Apply();
                ApplyLockClockPreferences(_lockClock!);
            })
            .SetNegativeButton("Отмена", (_, _) => { })
            .Show();
    }

    private void ShowCustomLockClockColor()
    {
        var prefs = GetSharedPreferences("tablet_os_settings", FileCreationMode.Private);
        var initial = prefs?.GetString("lock_clock_custom_color", "#FFFFFFFF") ?? "#FFFFFFFF";
        var parsed = Color.ParseColor(initial);
        var panel = new LinearLayout(this) { Orientation = Orientation.Vertical };
        panel.SetPadding(Dp(22), Dp(12), Dp(22), Dp(8));
        var preview = MakeText("Предпросмотр часов  09:41", 22, initial, false);
        preview.Gravity = GravityFlags.Center;
        preview.SetPadding(0, Dp(8), 0, Dp(16));
        panel.AddView(preview);
        var red = AddRgbSlider(panel, "Красный", parsed.R);
        var green = AddRgbSlider(panel, "Зелёный", parsed.G);
        var blue = AddRgbSlider(panel, "Синий", parsed.B);
        void RefreshPreview()
        {
            var hex = $"#{red.Progress:X2}{green.Progress:X2}{blue.Progress:X2}";
            preview.SetTextColor(Color.ParseColor(hex));
        }
        red.ProgressChanged += (_, e) => { if (e.FromUser) RefreshPreview(); };
        green.ProgressChanged += (_, e) => { if (e.FromUser) RefreshPreview(); };
        blue.ProgressChanged += (_, e) => { if (e.FromUser) RefreshPreview(); };

        new AlertDialog.Builder(this)
            .SetTitle("Свой цвет часов")
            .SetView(panel)
            .SetPositiveButton("Применить", (_, _) =>
            {
                var hex = $"#FF{red.Progress:X2}{green.Progress:X2}{blue.Progress:X2}";
                prefs?.Edit()?.PutString("lock_clock_custom_color", hex)?.PutInt("lock_clock_color", 8)?.Apply();
                _lockClockColor = 8;
                ApplyLockClockPreferences(_lockClock!);
            })
            .SetNegativeButton("Отмена", (_, _) => { })
            .Show();
    }

    private SeekBar AddRgbSlider(LinearLayout parent, string label, int value)
    {
        var caption = MakeText(label, 13, "#657487", true);
        parent.AddView(caption);
        var slider = new SeekBar(this) { Max = 255, Progress = value };
        parent.AddView(slider, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(42)));
        return slider;
    }

    private void CycleLockClockPosition()
    {
        _lockClockPosition = (_lockClockPosition + 1) % 3;
        GetSharedPreferences("tablet_os_settings", FileCreationMode.Private)?.Edit()?.PutInt("lock_clock_position", _lockClockPosition)?.Apply();
        var positions = new[] { 104, 210, 300 };
        var datePositions = new[] { 207, 313, 403 };
        var clockParams = (FrameLayout.LayoutParams)_lockClock!.LayoutParameters!;
        clockParams.TopMargin = Dp(positions[_lockClockPosition]);
        _lockClock.LayoutParameters = clockParams;
        var dateParams = (FrameLayout.LayoutParams)_lockDate!.LayoutParameters!;
        dateParams.TopMargin = Dp(datePositions[_lockClockPosition]);
        _lockDate.LayoutParameters = dateParams;
    }

    private FrameLayout BuildNotificationCenter()
    {
        var overlay = new FrameLayout(this) { Visibility = ViewStates.Gone, Clickable = true, Focusable = true };
        var scrim = new View(this);
        scrim.SetBackgroundColor(Color.ParseColor("#550B1726"));
        scrim.Click += (_, _) => CloseNotificationCenter();
        overlay.AddView(scrim, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

        var panel = new LinearLayout(this) { Orientation = Orientation.Vertical };
        panel.SetPadding(Dp(22), Dp(22), Dp(22), Dp(18));
        panel.Background = RoundedGradient("#F7FBFFFF", "#E7EEF7", 30);
        panel.Elevation = Dp(20);

        var header = new LinearLayout(this) { Orientation = Orientation.Horizontal, Gravity = GravityFlags.CenterVertical };
        var title = MakeText("Центр уведомлений", 23, "#20364D", true);
        header.AddView(title, new LinearLayout.LayoutParams(0, Dp(46), 1));
        var access = MakePillButton("Доступ", "#FFFFFF");
        access.Click += (_, _) =>
        {
            try { StartActivity(new Intent("android.settings.ACTION_NOTIFICATION_LISTENER_SETTINGS")); }
            catch { Toast.MakeText(this, "Разрешите доступ к уведомлениям в настройках Android", ToastLength.Long)?.Show(); }
        };
        header.AddView(access, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(36)));
        var close = MakePillButton("Готово", "#FFFFFF");
        close.Click += (_, _) => CloseNotificationCenter();
        header.AddView(close, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(36)));
        panel.AddView(header);

        var date = MakeText(DateTime.Now.ToString("dddd, d MMMM", CultureInfo.GetCultureInfo("ru-RU")), 13, "#718297", false);
        date.SetPadding(0, 0, 0, Dp(12));
        panel.AddView(date);

        var list = new LinearLayout(this) { Orientation = Orientation.Vertical };
        var notifications = NotificationCaptureService.GetRecent();
        if (notifications.Count == 0)
        {
            var empty = MakeText("Здесь будут появляться уведомления приложений", 14, "#718297", false);
            empty.SetPadding(0, Dp(18), 0, Dp(18));
            list.AddView(empty);
        }
        else
        {
            foreach (var item in notifications.Take(20))
            {
                var card = new LinearLayout(this) { Orientation = Orientation.Vertical };
                card.SetPadding(Dp(15), Dp(12), Dp(15), Dp(12));
                card.Background = Rounded("#EFFFFFFF", 20);
                var app = MakeText(item.App, 11, "#718297", true);
                var heading = MakeText(item.Title, 15, "#20364D", true);
                var body = MakeText(item.Text, 13, "#526579", false);
                body.MaxLines = 4;
                card.AddView(app);
                card.AddView(heading);
                card.AddView(body);
                var lp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
                lp.BottomMargin = Dp(9);
                list.AddView(card, lp);
            }
        }
        var scroll = new ScrollView(this) { FillViewport = true };
        scroll.AddView(list);
        panel.AddView(scroll, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1));

        var hint = MakeText("Свайп вниз от верхнего края открывает эту панель", 11, "#8291A2", false);
        hint.Gravity = GravityFlags.Center;
        hint.SetPadding(0, Dp(10), 0, 0);
        panel.AddView(hint);

        var lpPanel = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, (int)(Resources!.DisplayMetrics!.HeightPixels * 0.9f), GravityFlags.Top);
        lpPanel.SetMargins(Dp(8), Dp(8), Dp(8), 0);
        overlay.AddView(panel, lpPanel);
        return overlay;
    }

    private void OpenNotificationCenter()
    {
        if (_notificationCenter is null) return;
        _root.RemoveView(_notificationCenter);
        _notificationCenter = BuildNotificationCenter();
        _root.AddView(_notificationCenter, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
        _notificationCenter.Visibility = ViewStates.Visible;
        _notificationCenter.Alpha = 0f;
        _notificationCenter.TranslationY = -Dp(40);
        _notificationCenter.Animate()?.Alpha(1f)?.TranslationY(0)?.SetDuration(260)?.Start();
    }

    private void CloseNotificationCenter()
    {
        if (_notificationCenter is null) return;
        _notificationCenter.Animate()?.Alpha(0f)?.TranslationY(-Dp(30))?.SetDuration(180)
            ?.WithEndAction(new Java.Lang.Runnable(() =>
            {
                if (_notificationCenter is not null) _notificationCenter.Visibility = ViewStates.Gone;
            }))?.Start();
    }

    private float _gestureStartY = -1f;

    public override bool DispatchTouchEvent(MotionEvent? ev)
    {
        if (ev is not null)
        {
            if (ev.Action == MotionEventActions.Down)
                _gestureStartY = ev.Y;
            else if (ev.Action == MotionEventActions.Up && _gestureStartY >= 0)
            {
                var delta = ev.RawY - _gestureStartY;
                if (_gestureStartY <= Dp(42) && delta >= Dp(90) &&
                    _notificationCenter is not null && _notificationCenter.Visibility != ViewStates.Visible)
                {
                    OpenNotificationCenter();
                }
                _gestureStartY = -1f;
            }
            else if (ev.Action == MotionEventActions.Cancel)
                _gestureStartY = -1f;
        }
        return base.DispatchTouchEvent(ev);
    }

    private void AddControlTile(LinearLayout row, string title, string subtitle, Action action)
    {
        var tile = new LinearLayout(this) { Orientation = Orientation.Vertical, Gravity = GravityFlags.CenterVertical, Clickable = true, Focusable = true };
        tile.SetPadding(Dp(16), Dp(8), Dp(16), Dp(8));
        tile.Background = Rounded("#FFFFFF", 20);
        var titleView = MakeText(title, 15, "#20364D", true);
        var subView = MakeText(subtitle, 10, "#8190A0", false);
        tile.AddView(titleView);
        tile.AddView(subView);
        tile.Click += (_, _) => action();
        var lp = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.MatchParent, 1);
        lp.SetMargins(Dp(3), Dp(3), Dp(3), Dp(3));
        row.AddView(tile, lp);
    }

    private void OpenControlCenter()
    {
        if (_controlCenter is null) return;
        _controlCenter.RemoveAllViews();
        var fresh = BuildControlCenter();
        _root.RemoveView(_controlCenter);
        _controlCenter = fresh;
        _root.AddView(_controlCenter, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
        _controlCenter.Visibility = ViewStates.Visible;
        _controlCenter.Alpha = 0f;
        _controlCenter.TranslationY = -Dp(35);
        _controlCenter.Animate()?.Alpha(1f)?.TranslationY(0)?.SetDuration(240)?.Start();
    }

    private void CloseControlCenter()
    {
        if (_controlCenter is null) return;
        _controlCenter.Animate()?.Alpha(0f)?.TranslationY(-Dp(25))?.SetDuration(180)?.WithEndAction(new Java.Lang.Runnable(() =>
        {
            if (_controlCenter is not null) _controlCenter.Visibility = ViewStates.Gone;
        }))?.Start();
    }

    private void OpenSystemPanel(string action)
    {
        try { StartActivity(new Intent(action)); }
        catch { StartActivity(new Intent(Android.Provider.Settings.ActionSettings)); }
    }

    private void ChooseWallpaper()
    {
        var intent = new Intent(Intent.ActionOpenDocument);
        intent.SetType("image/*");
        intent.AddCategory(Intent.CategoryOpenable);
        StartActivityForResult(intent, WallpaperRequestCode);
    }

    private void ApplySavedWallpaper()
    {
        var saved = GetSharedPreferences("tablet_os_settings", FileCreationMode.Private)?.GetString("wallpaper_uri", null);
        if (string.IsNullOrWhiteSpace(saved) || _wallpaperView is null) return;
        try { _wallpaperView.SetImageURI(Android.Net.Uri.Parse(saved)); }
        catch { }
    }

    private void ApplyWallpaper(Android.Net.Uri uri)
    {
        try
        {
            ContentResolver?.TakePersistableUriPermission(uri, ActivityFlags.GrantReadUriPermission);
            GetSharedPreferences("tablet_os_settings", FileCreationMode.Private)?.Edit()?.PutString("wallpaper_uri", uri.ToString())?.Apply();
            _wallpaperView.SetImageURI(uri);
            Toast.MakeText(this, "Обои сохранены", ToastLength.Short)?.Show();
        }
        catch
        {
            Toast.MakeText(this, "Не удалось применить это изображение", ToastLength.Short)?.Show();
        }
    }

    private void LoadApps()
    {
        try
        {
            var launcherIntent = new Intent(Intent.ActionMain);
            launcherIntent.AddCategory(Intent.CategoryLauncher);

            var results = PackageManager!.QueryIntentActivities(launcherIntent, PackageInfoFlags.MatchAll);
            _apps.Clear();

            foreach (var result in results)
            {
                var activityInfo = result.ActivityInfo;
                if (activityInfo is null || string.IsNullOrWhiteSpace(activityInfo.PackageName))
                    continue;

                var packageName = activityInfo.PackageName;
                if (_apps.Any(a => a.PackageName == packageName))
                    continue;

                var label = result.LoadLabel(PackageManager)?.ToString();
                if (string.IsNullOrWhiteSpace(label))
                    label = packageName;

                _apps.Add(new AppEntry(packageName, label, result.LoadIcon(PackageManager)));
            }

            _apps.Sort((a, b) => string.Compare(a.Label, b.Label, StringComparison.CurrentCultureIgnoreCase));
            RenderApps(_search?.Text ?? string.Empty);
            RefreshDock();
        }
        catch (Exception)
        {
            _emptyState.Text = "Не удалось получить список приложений";
            _emptyState.Visibility = ViewStates.Visible;
        }
    }

    private void RenderApps(string query)
    {
        if (_appGrid is null)
            return;

        _appGrid.RemoveAllViews();
        var filtered = _apps
            .Where(a => NativeMatches(query.Trim(), a.Label))
            .ToList();

        _emptyState.Visibility = filtered.Count == 0 ? ViewStates.Visible : ViewStates.Gone;
        var columns = AppColumns;
        for (var start = 0; start < filtered.Count; start += columns)
        {
            var row = new LinearLayout(this)
            {
                Orientation = Orientation.Horizontal,
                Gravity = GravityFlags.Top
            };
            for (var col = 0; col < columns; col++)
            {
                var index = start + col;
                var cell = index < filtered.Count ? CreateAppTile(filtered[index]) : new View(this);
                var cellParams = new LinearLayout.LayoutParams(0, Dp(112), 1);
                cellParams.BottomMargin = Dp(7);
                row.AddView(cell, cellParams);
            }
            _appGrid.AddView(row, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
        }
    }

    private View CreateAppTile(AppEntry app)
    {
        // Each app tile owns its own optional image background, saved by package name.
        var tile = new FrameLayout(this) { Clickable = true, Focusable = true };
        tile.Background = Rounded("#66FFFFFF", 19);
        tile.ClipToOutline = true;

        var saved = GetSharedPreferences("tablet_os_settings", FileCreationMode.Private)
            ?.GetString("app_background:" + app.PackageName, null);
        if (!string.IsNullOrWhiteSpace(saved))
        {
            var background = new ImageView(this) { ScaleType = ImageView.ScaleType.CenterCrop };
            try { background.SetImageURI(Android.Net.Uri.Parse(saved)); } catch { }
            background.Alpha = 0.82f;
            tile.AddView(background, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
        }

        var content = new LinearLayout(this) { Orientation = Orientation.Vertical, Gravity = GravityFlags.Center };
        content.SetPadding(Dp(3), Dp(7), Dp(3), Dp(5));
        var iconFrame = new FrameLayout(this) { ForegroundGravity = GravityFlags.Center };
        var icon = new ImageView(this);
        icon.SetImageDrawable(app.Icon);
        icon.SetScaleType(ImageView.ScaleType.FitCenter);
        iconFrame.AddView(icon, new FrameLayout.LayoutParams(Dp(52), Dp(52), GravityFlags.Center));

        var label = MakeText(app.Label, 11, "#26384A", true);
        label.Gravity = GravityFlags.Center;
        label.MaxLines = 2;
        label.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
        label.SetPadding(Dp(2), Dp(4), Dp(2), 0);

        content.AddView(iconFrame, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(59)));
        content.AddView(label, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(34)));
        tile.AddView(content, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
        tile.Click += (_, _) => LaunchApp(app, tile);
        tile.LongClick += (_, _) => ChooseAppBackground(app);
        return tile;
    }

    private void ChooseAppBackground(AppEntry app)
    {
        _pendingBackgroundPackage = app.PackageName;
        var picker = new Intent(Intent.ActionOpenDocument);
        picker.SetType("image/*");
        picker.AddCategory(Intent.CategoryOpenable);
        StartActivityForResult(picker, AppBackgroundRequestCode);
    }

    private void ApplyAppBackground(string packageName, Android.Net.Uri uri)
    {
        try
        {
            ContentResolver?.TakePersistableUriPermission(uri, ActivityFlags.GrantReadUriPermission);
            GetSharedPreferences("tablet_os_settings", FileCreationMode.Private)?.Edit()
                ?.PutString("app_background:" + packageName, uri.ToString())?.Apply();
            RenderApps(_search?.Text ?? string.Empty);
            Toast.MakeText(this, "Фон приложения сохранён", ToastLength.Short)?.Show();
        }
        catch
        {
            Toast.MakeText(this, "Не удалось сохранить фон приложения", ToastLength.Short)?.Show();
        }
    }


    private LinearLayout BuildDock()
    {
        var dock = new LinearLayout(this) { Orientation = Orientation.Horizontal, Gravity = GravityFlags.Center, ClipToPadding = false, Elevation = Dp(16) };
        dock.SetPadding(Dp(10), Dp(9), Dp(10), Dp(9));
        var glass = new GradientDrawable();
        glass.SetColor(Color.ParseColor("#DDFBFDFF"));
        glass.SetCornerRadius(Dp(27));
        glass.SetStroke(Dp(1), Color.ParseColor("#F8FFFFFF"));
        dock.Background = glass;
        return dock;
    }

    private void RefreshDock()
    {
        if (_dock is null) return;
        _dock.RemoveAllViews();
        var preferred = new[] { "com.android.chrome", "com.google.android.youtube", "com.google.android.gm", "com.android.settings", "com.google.android.apps.photos", "com.android.camera2" };
        var pinned = new List<AppEntry>();
        foreach (var packageName in preferred)
        {
            var match = _apps.FirstOrDefault(a => a.PackageName == packageName);
            if (match is not null && !pinned.Any(a => a.PackageName == match.PackageName)) pinned.Add(match);
            if (pinned.Count == 5) break;
        }
        foreach (var app in _apps)
        {
            if (pinned.Count >= 5) break;
            if (!pinned.Any(a => a.PackageName == app.PackageName)) pinned.Add(app);
        }
        foreach (var app in pinned)
        {
            var item = new FrameLayout(this) { Clickable = true, Focusable = true, ForegroundGravity = GravityFlags.Center };
            item.Background = Rounded("#26FFFFFF", 18);
            var image = new ImageView(this);
            image.SetImageDrawable(app.Icon);
            image.SetScaleType(ImageView.ScaleType.FitCenter);
            item.AddView(image, new FrameLayout.LayoutParams(Dp(43), Dp(43), GravityFlags.Center));
            item.Click += (_, _) => LaunchApp(app, item);
            var itemParams = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.MatchParent, 1);
            itemParams.SetMargins(Dp(3), 0, Dp(3), 0);
            _dock.AddView(item, itemParams);
        }
    }

    // Native Android scale-up transition: the launched app grows out of the
    // exact icon/dock item the user tapped. No per-frame work runs on the CPU.
    private void LaunchApp(AppEntry app, View source)
    {
        try
        {
            var launch = PackageManager?.GetLaunchIntentForPackage(app.PackageName);
            if (launch is null) return;

            // Remember the tapped icon. When the external app returns, the
            // launcher expands back out from this exact point (Genie-style).
            if (_root is not null && source.Width > 0 && source.Height > 0)
            {
                var sourceLocation = new int[2];
                var rootLocation = new int[2];
                source.GetLocationOnScreen(sourceLocation);
                _root.GetLocationOnScreen(rootLocation);
                _returnPivotX = Math.Clamp(
                    sourceLocation[0] - rootLocation[0] + source.Width / 2f,
                    0f,
                    Math.Max(1, _root.Width));
                _returnPivotY = Math.Clamp(
                    sourceLocation[1] - rootLocation[1] + source.Height / 2f,
                    0f,
                    Math.Max(1, _root.Height));
            }

            _returnAnimationPending = true;
            try
            {
                if (source.Width > 0 && source.Height > 0)
                {
                    var options = ActivityOptions.MakeScaleUpAnimation(
                        source,
                        0,
                        0,
                        source.Width,
                        source.Height);
                    StartActivity(launch, options.ToBundle());
                }
                else
                {
                    StartActivity(launch);
                }
            }
            catch
            {
                _returnAnimationPending = false;
                throw;
            }
        }
        catch (ActivityNotFoundException)
        {
            Toast.MakeText(this, "Не удалось открыть приложение", ToastLength.Short)?.Show();
        }
    }

    // Animate the home screen outward from the icon that launched the app.
    // This is a lightweight Genie-inspired return transition rendered by Android.
    private void PlayGenieReturnAnimation()
    {
        _returnAnimationPending = false;

        _root.Animate()?.Cancel();
        _root.PivotX = _returnPivotX > 0 ? _returnPivotX : _root.Width / 2f;
        _root.PivotY = _returnPivotY > 0 ? _returnPivotY : _root.Height;
        _root.ScaleX = 0.075f;
        _root.ScaleY = 0.045f;
        _root.Alpha = 0.72f;

        _root.Animate()
            ?.ScaleX(1f)
            ?.ScaleY(1f)
            ?.Alpha(1f)
            ?.SetDuration(460)
            ?.SetInterpolator(new Android.Views.Animations.DecelerateInterpolator(2.2f))
            ?.Start();
    }

    private static bool NativeMatches(string query, string label)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        var queryPtr = Marshal.StringToCoTaskMemUTF8(query);
        var labelPtr = Marshal.StringToCoTaskMemUTF8(label);
        try { return NativeSearchMatch(queryPtr, labelPtr) != 0; }
        catch (DllNotFoundException) { return label.Contains(query, StringComparison.CurrentCultureIgnoreCase); }
        catch (EntryPointNotFoundException) { return label.Contains(query, StringComparison.CurrentCultureIgnoreCase); }
        finally
        {
            Marshal.FreeCoTaskMem(queryPtr);
            Marshal.FreeCoTaskMem(labelPtr);
        }
    }

    [DllImport("tabletos_core", EntryPoint = "tabletos_match", CallingConvention = CallingConvention.Cdecl)]
    private static extern byte NativeSearchMatch(IntPtr query, IntPtr label);

    private void UpdateClock()
    {
        _clock.Text = DateTime.Now.ToString("HH:mm");
        _date.Text = DateTime.Now.ToString("dddd, d MMMM", CultureInfo.GetCultureInfo("ru-RU"));
    }

    private bool IsDefaultLauncher() =>
        _roleManager?.IsRoleAvailable(RoleManager.RoleHome) == true &&
        _roleManager.IsRoleHeld(RoleManager.RoleHome);

    private void RequestHomeRole()
    {
        if (_roleManager is null || !_roleManager.IsRoleAvailable(RoleManager.RoleHome))
        {
            Toast.MakeText(this, "Выбор главного экрана недоступен на этом устройстве", ToastLength.Long)?.Show();
            return;
        }

        if (_roleManager.IsRoleHeld(RoleManager.RoleHome))
            return;

        try
        {
            StartActivityForResult(
                _roleManager.CreateRequestRoleIntent(RoleManager.RoleHome),
                HomeRoleRequestCode);
        }
        catch (ActivityNotFoundException)
        {
            Toast.MakeText(this, "Откройте настройки Android и выберите Tablet OS как приложение главного экрана", ToastLength.Long)?.Show();
        }
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode == HomeRoleRequestCode && !IsDefaultLauncher())
            Toast.MakeText(this, "Tablet OS пока не выбран как главный экран", ToastLength.Short)?.Show();
        if (requestCode == WallpaperRequestCode && resultCode == Result.Ok && data?.Data is Android.Net.Uri selectedUri)
            ApplyWallpaper(selectedUri);
        if (requestCode == AppBackgroundRequestCode && resultCode == Result.Ok && data?.Data is Android.Net.Uri appUri && !string.IsNullOrWhiteSpace(_pendingBackgroundPackage))
            ApplyAppBackground(_pendingBackgroundPackage, appUri);
        if (requestCode == LockWallpaperRequestCode && resultCode == Result.Ok && data?.Data is Android.Net.Uri lockUri)
            ApplyLockWallpaper(lockUri);
        if (requestCode == LockForegroundRequestCode && resultCode == Result.Ok && data?.Data is Android.Net.Uri foregroundUri)
            ApplyLockForeground(foregroundUri);
    }

    private TextView MakeText(string text, float size, string color, bool bold)
    {
        var view = new TextView(this)
        {
            Text = text,
            TextSize = size,
            Gravity = GravityFlags.CenterVertical
        };
        view.SetTextColor(Color.ParseColor(color));
        if (bold)
            view.SetTypeface(null, TypefaceStyle.Bold);
        return view;
    }

    private GradientDrawable Rounded(string color, float radius)
    {
        var shape = new GradientDrawable();
        shape.SetColor(Color.ParseColor(color));
        shape.SetCornerRadius(Dp(radius));
        return shape;
    }

    private GradientDrawable RoundedGradient(string first, string second, float radius)
    {
        var shape = new GradientDrawable(
            GradientDrawable.Orientation.TlBr,
            new[] { Color.ParseColor(first), Color.ParseColor(second) });
        shape.SetCornerRadius(Dp(radius));
        return shape;
    }

    private int Dp(float value) => (int)(value * Resources!.DisplayMetrics!.Density + 0.5f);
}
