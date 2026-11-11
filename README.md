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
- **Privacy & Offline:** Zero network capabilities (`internetClient` omitted), zero ads, zero accounts, zero remote uploads. See [PRIVACY.md](PRIVACY.md) or the live page at `https://berrizak.com/PRIVACY.html`.

## Solution Structure
- `src/DasImageConverter.Core/` — Platform-agnostic image engine, validation, sanitization, ZIP export, and MVVM ViewModels.
- `src/DasImageConverter.App/` — WinUI 3 single-project MSIX application (`net8.0-windows10.0.26100.0`, self-contained `.NET 8`, framework-dependent `Microsoft.WindowsAppRuntime.2`).
- `tests/DasImageConverter.Tests/` — xUnit test suite covering format accuracy, EXIF orientation, transparency compositing, ZIP export, offline/Store compliance, ViewModels, and 50-image batch performance.

## Build, Test, and MSIX Packaging
```powershell
# Restore all packages and run automated unit & integration tests
dotnet test tests/DasImageConverter.Tests/DasImageConverter.Tests.csproj

# Build Store-ready multi-arch MSIX bundle (unsigned for Partner Center upload)
# Uses winapp CLI (or the winapp.exe bundled via Microsoft.Windows.SDK.BuildTools.WinApp)
winapp package src/DasImageConverter.App/DasImageConverter.App.csproj -c Release --arch x64 --arch arm64 --no-sign --output artifacts/msix/DASImageConverter.msixbundle
```

## Microsoft Store Submission Checklist
1. **Store App Identity Configured:** `src/DasImageConverter.App/Package.appxmanifest` is bound to `Berrizak.DASImageConverter` (`Publisher="CN=B0B0AFCA-DF2F-4FD5-B05F-5BA3B734E00D"`, `PublisherDisplayName="Berrizak"`, PFN `Berrizak.DASImageConverter_wzjktnp1vrtna`, Store listing `https://apps.microsoft.com/detail/9N390W6N5XZC`). Increment `Version` before each subsequent submission.
2. **Build Multi-Arch Store Bundle:** Run `winapp package` above to generate `artifacts/msix/DASImageConverter.msixbundle` (unsigned; Partner Center signs with the Store certificate upon ingestion).
3. **Run Windows App Certification Kit (WACK):** From an elevated Developer PowerShell, test a dev-signed `x64` package using `appcert.exe`:
   ```powershell
   appcert.exe reset
   appcert.exe test -apptype windowsstoreapp -packagefullname "<InstalledPackageFullName>" -reportoutputpath "artifacts\wack-report.xml"
   ```
4. **Partner Center Declarations:**
   - **Privacy Policy URL:** `https://berrizak.com/PRIVACY.html` (markdown source kept in [PRIVACY.md](PRIVACY.md)).
   - **Restricted Capability (`runFullTrust`) Justification:** *"DAS Image Converter is a native WinUI 3 desktop utility that runs offline with standard medium-integrity user privileges to decode, convert, and save user-selected local image files and ZIP archives via Windows file pickers and drag-and-drop."*
   - **Listing Metadata & Store Assets:** Upload desktop screenshots and Store logos from `docs/store/`, set IARC age rating, and target Windows Desktop.
