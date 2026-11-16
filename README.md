# Tablet OS

An Android tablet home-screen launcher built with C# and Rust.

## Current foundation

- Android 10+ launcher entry point (HOME / DEFAULT intent filter).
- The first app launch requests Android's official Home role through RoleManager.
- The user confirms the system-owned dialog; the app cannot silently replace the current launcher.
- A fallback button lets the user request the Home role again after declining.
- GitHub Actions builds an installable APK artifact.

## Technology

- C# / .NET for Android: Android lifecycle, launcher integration and UI.
- Rust: planned native core, connected through a small C ABI.

## Build

Install .NET 9 SDK, the Android workload and JDK 17, then run:

    dotnet workload install android
    dotnet publish src/TabletOS.Android/TabletOS.Android.csproj -c Release -f net9.0-android -p:AndroidPackageFormat=apk

Minimum Android version: Android 10 (API 29).

## Current status

This is the launcher-registration foundation. The full home screen, app grid, widgets and Rust engine are not implemented yet.
