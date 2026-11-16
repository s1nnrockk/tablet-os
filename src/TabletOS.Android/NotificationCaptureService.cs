using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Service.Notification;

namespace TabletOS.Android;

public sealed record CapturedNotification(string App, string Title, string Text);

[Service(
    Name = "com.sinrok.tabletos.NotificationCaptureService",
    Permission = "android.permission.BIND_NOTIFICATION_LISTENER_SERVICE",
    Exported = true)]
[IntentFilter(new[] { "android.service.notification.NotificationListenerService" })]
public sealed class NotificationCaptureService : NotificationListenerService
{
    private static readonly object Sync = new();
    private static readonly List<CapturedNotification> Recent = new();

    public static IReadOnlyList<CapturedNotification> GetRecent()
    {
        lock (Sync) return Recent.ToArray();
    }

    public override void OnNotificationPosted(StatusBarNotification? sbn)
    {
        base.OnNotificationPosted(sbn);
        if (sbn?.Notification?.Extras is null) return;

        var extras = sbn.Notification.Extras;
        var title = extras.GetCharSequence(Notification.ExtraTitle)?.ToString();
        var text = extras.GetCharSequence(Notification.ExtraText)?.ToString();
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(text)) return;

        var packageName = sbn.PackageName;
        if (string.IsNullOrWhiteSpace(packageName)) return;

        var appName = packageName;
        try
        {
            var packageManager = PackageManager;
            var info = packageManager?.GetApplicationInfo(packageName, PackageInfoFlags.MatchAll);
            if (info is not null && packageManager is not null)
                appName = packageManager.GetApplicationLabel(info)?.ToString() ?? packageName;
        }
        catch
        {
            // Keep the package name as a safe fallback when app metadata is unavailable.
        }

        lock (Sync)
        {
            Recent.Insert(0, new CapturedNotification(appName, title ?? appName, text ?? ""));
            if (Recent.Count > 30) Recent.RemoveAt(Recent.Count - 1);
        }
    }

    public override void OnNotificationRemoved(StatusBarNotification? sbn)
    {
        base.OnNotificationRemoved(sbn);
    }
}
