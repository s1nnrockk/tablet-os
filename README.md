# Tablet OS

An Android tablet home-screen launcher built with C# and a Rust native core.

## Current features

- Android 10+ launcher entry point and official Home role request.
- Original HyperOS-inspired home screen with a soft blue / lilac / warm-neutral palette.
- Large clock, localized date, app search and a five-column app grid.
- Fixed Liquid Glass-inspired bottom dock with translucent material, fine highlight and soft elevation.
- Dock prioritizes common system apps and fills remaining slots from installed apps.
- Rust native UTF-8 search matcher, packaged for ARM64, ARMv7 and x86_64.
- C# handles Android lifecycle, views and app launching; Rust handles lightweight native search matching.

## Performance

- No live backdrop blur, continuous animation loop or per-frame native work.
- The dock uses a translucent drawable and one elevation shadow.
- Rust is called only for non-empty search queries; an ordinary home-screen render does not cross the native boundary.
- Managed search fallback is available if the native library cannot be loaded.

## Build

Install .NET 9, JDK 17, Android workload, Rust and cargo-ndk. Build the native library first:

    ANDROID_NDK_HOME="$ANDROID_SDK_ROOT/ndk/27.2.12479018" cargo ndk -t arm64-v8a -t armeabi-v7a -t x86_64 -o src/TabletOS.Android/JniLibs build --manifest-path native/tabletos-core/Cargo.toml --release

Then publish:

    dotnet workload install android
    dotnet publish src/TabletOS.Android/TabletOS.Android.csproj -c Release -f net9.0-android -p:AndroidPackageFormat=apk

Minimum Android version: Android 10 (API 29).
