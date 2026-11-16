using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace TabletOS.Android;

[Activity(Label = "Tablet OS", MainLauncher = true, Exported = true, Theme = "@android:style/Theme.Material.Light.NoActionBar")]
[IntentFilter(new[] { Intent.ActionMain }, Categories = new[] { Intent.CategoryHome, Intent.CategoryDefault })]
public sealed class MainActivity : Activity
{
    private const int HomeRoleRequestCode = 4101;
    private TextView _status = null!;
    private Button _setLauncherButton = null!;
    private RoleManager? _roleManager;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _roleManager = GetSystemService(Context.RoleService) as RoleManager;
        BuildFirstScreen();
        if (!IsDefaultLauncher()) RequestHomeRole();
        else ShowLauncherActive();
    }

    protected override void OnResume()
    {
        base.OnResume();
        if (_status is not null && IsDefaultLauncher()) ShowLauncherActive();
    }

    private void BuildFirstScreen()
    {
        var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
        root.SetPadding(48, 72, 48, 48);
        root.SetGravity(GravityFlags.CenterHorizontal);

        var title = new TextView(this) { Text = "Tablet OS", TextSize = 32, Gravity = GravityFlags.Center };
        title.SetTypeface(null, Android.Graphics.TypefaceStyle.Bold);

        var subtitle = new TextView(this)
        {
            Text = "Ваш новый главный экран для планшета",
            TextSize = 16,
            Gravity = GravityFlags.Center
        };
        subtitle.SetPadding(0, 16, 0, 40);

        _status = new TextView(this)
        {
            Text = "Проверяем настройки лаунчера…",
            TextSize = 16,
            Gravity = GravityFlags.Center
        };
        _status.SetPadding(0, 0, 0, 28);

        _setLauncherButton = new Button(this) { Text = "Сделать лаунчером по умолчанию" };
        _setLauncherButton.Click += (_, _) => RequestHomeRole();

        root.AddView(title, MatchWrap());
        root.AddView(subtitle, MatchWrap());
        root.AddView(_status, MatchWrap());
        root.AddView(_setLauncherButton, MatchWrap());
        SetContentView(root);
    }

    private static LinearLayout.LayoutParams MatchWrap() =>
        new(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);

    private bool IsDefaultLauncher() =>
        _roleManager?.IsRoleAvailable(RoleManager.RoleHome) == true &&
        _roleManager.IsRoleHeld(RoleManager.RoleHome);

    private void RequestHomeRole()
    {
        if (_roleManager is null || !_roleManager.IsRoleAvailable(RoleManager.RoleHome))
        {
            _status.Text = "Android на этом устройстве не предоставляет выбор приложения главного экрана.";
            _setLauncherButton.Enabled = false;
            return;
        }

        if (_roleManager.IsRoleHeld(RoleManager.RoleHome))
        {
            ShowLauncherActive();
            return;
        }

        _status.Text = "Подтвердите системный запрос Android, чтобы использовать Tablet OS как главный экран.";
        _setLauncherButton.Enabled = false;
        try
        {
            StartActivityForResult(_roleManager.CreateRequestRoleIntent(RoleManager.RoleHome), HomeRoleRequestCode);
        }
        catch (ActivityNotFoundException)
        {
            _status.Text = "Не удалось открыть системное окно выбора лаунчера. Выберите Tablet OS в настройках приложений по умолчанию.";
            _setLauncherButton.Enabled = true;
        }
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode != HomeRoleRequestCode) return;

        _setLauncherButton.Enabled = true;
        if (IsDefaultLauncher()) ShowLauncherActive();
        else _status.Text = "Tablet OS пока не выбран. Можно повторить запрос кнопкой ниже или выбрать приложение в настройках Android.";
    }

    private void ShowLauncherActive()
    {
        _status.Text = "Tablet OS выбран как главный экран.";
        _setLauncherButton.Text = "Лаунчер активен";
        _setLauncherButton.Enabled = false;
    }
}
