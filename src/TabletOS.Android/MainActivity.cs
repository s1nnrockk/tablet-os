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
    private const int AppColumns = 5;

    private RoleManager? _roleManager;
    private TextView _clock = null!;
    private TextView _date = null!;
    private TextView _emptyState = null!;
    private LinearLayout _appGrid = null!;
    private LinearLayout _dock = null!;
    private EditText _search = null!;
    private Button _setLauncherButton = null!;
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
        scroll.SetBackground(new GradientDrawable(
            GradientDrawable.Orientation.TlBr,
            new[] { Color.ParseColor("#EAF1F8"), Color.ParseColor("#F7EEE8"), Color.ParseColor("#F4F3EE") }));

        var page = new LinearLayout(this) { Orientation = Orientation.Vertical };
        page.SetPadding(Dp(26), Dp(22), Dp(26), Dp(124));

        var topLine = new LinearLayout(this)
        {
            Orientation = Orientation.Horizontal,
            Gravity = GravityFlags.CenterVertical
        };

        var brand = MakeText("TABLET OS", 13, "#526579", true);
        var topSpacer = new Space(this);
        var dateTop = MakeText("", 13, "#657487", false);
        topLine.AddView(brand, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(28)));
        topLine.AddView(topSpacer, new LinearLayout.LayoutParams(0, 1, 1));
        topLine.AddView(dateTop, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(28)));
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
        var root = new FrameLayout(this);
        root.AddView(scroll, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
        _dock = BuildDock();
        var dockParams = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(82), GravityFlags.Bottom | GravityFlags.CenterHorizontal);
        dockParams.SetMargins(Dp(22), 0, Dp(22), Dp(16));
        root.AddView(_dock, dockParams);
        SetContentView(root);
        UpdateClock();

        dateTop.Text = DateTime.Now.ToString("dddd, d MMMM", CultureInfo.GetCultureInfo("ru-RU"));
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
        var tile = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical,
            Gravity = GravityFlags.Center,
            Clickable = true,
            Focusable = true
        };
        tile.SetPadding(Dp(3), Dp(7), Dp(3), Dp(5));
        tile.Background = Rounded("#66FFFFFF", 19);

        var iconFrame = new FrameLayout(this)
        {
            ForegroundGravity = GravityFlags.Center
        };
        var icon = new ImageView(this);
        icon.SetImageDrawable(app.Icon);
        icon.SetScaleType(ImageView.ScaleType.FitCenter);
        var iconParams = new FrameLayout.LayoutParams(Dp(52), Dp(52), GravityFlags.Center);
        iconFrame.AddView(icon, iconParams);

        var label = MakeText(app.Label, 11, "#35485B", false);
        label.Gravity = GravityFlags.Center;
        label.MaxLines = 2;
        label.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
        label.SetPadding(Dp(2), Dp(4), Dp(2), 0);

        tile.AddView(iconFrame, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(59)));
        tile.AddView(label, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(34)));
        tile.Click += (_, _) =>
        {
            try
            {
                var launch = PackageManager?.GetLaunchIntentForPackage(app.PackageName);
                if (launch is not null)
                    StartActivity(launch);
            }
            catch (ActivityNotFoundException)
            {
                Toast.MakeText(this, "Не удалось открыть приложение", ToastLength.Short)?.Show();
            }
        };
        return tile;
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
            item.Click += (_, _) => LaunchApp(app);
            var itemParams = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.MatchParent, 1);
            itemParams.SetMargins(Dp(3), 0, Dp(3), 0);
            _dock.AddView(item, itemParams);
        }
    }

    private void LaunchApp(AppEntry app)
    {
        try
        {
            var launch = PackageManager?.GetLaunchIntentForPackage(app.PackageName);
            if (launch is not null) StartActivity(launch);
        }
        catch (ActivityNotFoundException)
        {
            Toast.MakeText(this, "Не удалось открыть приложение", ToastLength.Short)?.Show();
        }
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
