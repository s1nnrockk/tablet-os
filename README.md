# Tablet OS

An Android tablet home-screen launcher built with C# and Rust.

## Current features

- Android 10+ launcher entry point (HOME / DEFAULT intent filter).
- First-launch request for Android's official Home role through RoleManager.
- HyperOS-inspired, original tablet home screen with a soft blue/lilac/warm-neutral palette.
- Large clock and localized date.
- Live list of launchable installed apps using their real Android icons and labels.
- App search and tap-to-launch behavior.
- Android package visibility query scoped to launcher activities.
- GitHub Actions APK build workflow.

## Technology

- C# / .NET for Android: Android lifecycle, launcher integration, app discovery and UI.
- Rust: planned native core, connected through a small C ABI.

## Build

Install .NET 9 SDK, the Android workload and JDK 17, then run:

    dotnet workload install android
    dotnet publish src/TabletOS.Android/TabletOS.Android.csproj -c Release -f net9.0-android -p:AndroidPackageFormat=apk

Minimum Android version: Android 10 (API 29).

## Current limitations

- This is the first home-screen implementation; widgets, drag-and-drop placement, dock customization, gestures and Rust integration are not implemented yet.
- App icons and labels are provided by Android and vary by device/manufacturer.
