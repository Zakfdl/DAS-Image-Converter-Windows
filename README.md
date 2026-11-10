# DAS Image Converter — Native Windows App

Native WinUI 3 / Windows App SDK desktop application for **DAS Image Converter** (`img.berrizak.com`), engineered for **100% offline local image conversion** and **Microsoft Store (MSIX)** distribution.

## Features
- **Formats:** JPEG, PNG, WebP, and AVIF (decode and encode completely locally via `Magick.NET`).
- **Quality & Compression:** Live quality slider (`1–100`, default `82`) for JPEG, WebP, and AVIF; lossless PNG output.
- **Proportional Resize:** Downscale-only width cap (`1–30,000 px`, default `1920`) preserving aspect ratio and auto-orienting EXIF metadata.
- **JPEG Transparency Fill:** Configurable background color swatch (`#FFFFFF` default) when converting transparent PNG/WebP/AVIF to JPEG.
- **Batch Processing:** Up to 50 images (60 MP / 100 MB per-file limits), drag-and-drop, native file picker, progress indicator, cancellation, and per-item error isolation.
- **Exports:** Individual native save picker and local collision-safe ZIP archive export (`DAS-converted-YYYY-MM-DD.zip`).
- **Themes:** Native DAS Light, DAS Dark, and System themes.
- **Privacy & Offline:** Zero network capabilities (`internetClient` omitted), zero ads, zero accounts, zero remote uploads.

## Solution Structure
- `src/DasImageConverter.Core/` — Platform-agnostic image engine, validation, sanitization, ZIP export, and MVVM ViewModels.
- `src/DasImageConverter.App/` — WinUI 3 single-project MSIX application (`net8.0-windows10.0.26100.0`).
- `tests/DasImageConverter.Tests/` — xUnit test suite covering format accuracy, EXIF orientation, transparency compositing, ZIP export, offline compliance, ViewModels, and 50-image batch performance.

## Build, Test, and MSIX Packaging
```powershell
# Run automated unit & integration tests
dotnet test tests/DasImageConverter.Tests/DasImageConverter.Tests.csproj

# Build Store-ready MSIX bundle (unsigned for Partner Center upload)
winapp package src/DasImageConverter.App/DasImageConverter.App.csproj -c Release --arch x64 --arch arm64 --no-sign --output artifacts/msix/DASImageConverter.msixbundle
```

> **Microsoft Store Submission Note:** Before uploading to Microsoft Partner Center, update `<Identity Name="..." Publisher="..." />` and `<PublisherDisplayName>` in `src/DasImageConverter.App/Package.appxmanifest` to match your reserved Store app identity, and run the Windows App Certification Kit (WACK).
