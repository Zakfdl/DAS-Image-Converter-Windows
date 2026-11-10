# Phase 2: Feature Parity Checklist & Windows Application Architecture

**Repository Target:** `C:\Users\zakariya.essa\.gemini\antigravity\scratch\DAS-Image-Converter-Windows`  
**Stack:** C#, .NET 8 (`net8.0-windows10.0.19041.0`), WinUI 3, Windows App SDK, `Magick.NET-Q8-x64`, `CommunityToolkit.Mvvm`, xUnit, MSIX.

---

## 1. Feature Parity Checklist

### 1.1 Core & Website Parity Matrix

| ID | Feature / Behavior | Website Baseline (`img.berrizak.zip`) | Native Windows (WinUI 3) Spec | Verification Method |
|---|---|---|---|---|
| **FP-01** | Supported Formats | Input & output: JPEG, PNG, WebP, AVIF (`app.js:6-7,51-57`) | Exact parity: `.jpg`/`.jpeg`, `.png`, `.webp`, `.avif` decoded & encoded offline via `Magick.NET-Q8-x64` (`libjpeg`, `libpng`, `libwebp`, `libavif`). | Accuracy tests across all 4×4 format pairs |
| **FP-02** | File Selection | Click/Enter/Space on dropzone triggers `<input type="file" multiple>` (`index.html:88-95`, `app.js:425-429`) | Clicking drop zone or pressing `Enter`/`Space` opens WinUI `FileOpenPicker` filtered to JPEG/PNG/WebP/AVIF with multi-select. | Manual UI + FilePicker service unit test |
| **FP-03** | Drag and Drop | Drop files onto `#drop-zone` with `.is-dragging` visual state (`app.js:430-438`) | Native WinUI `AllowDrop="True"` on drop zone (and main workspace) handling `StorageFile` drops with coral drag-over state. | Manual UI drag-and-drop test |
| **FP-04** | Batch Limit (50 files) | `MAX_FILES = 50`; truncates excess and shows notice (`app.js:3,175-194`) | `MaxFiles = 50`; enforces hard cap across multiple adds, truncates partial batches with exact user notice. | Unit tests (`ImageInputValidatorTests`) |
| **FP-05** | File Size & Pixel Guards | `100 MB` max file size, `60 MP` (`60,000,000` px) max, `width > 0 && height > 0` (`app.js:4-5,140-158`) | Identical limits enforced before full processing; oversized/invalid files rejected cleanly with clear messages. | Unit tests with boundary values |
| **FP-06** | Duplicate Filtering | Skips files matching existing `(name, size)` (`app.js:185`) | Skips duplicates matching normalized path or `(FileName, FileSize)` so repeated drops don't duplicate queue items. | Unit test |
| **FP-07** | Queue File List | Thumbnail (`<=160px`), filename, size, `W × H px`, status badge, remove `×`, `"N image(s) ready"`, `"Clear all"` | Native list card with thumbnail, tooltipped filename, `formatBytes` size, `W × H px`, status pill, remove button, live ready count, `Clear all`. | ViewModel unit tests |
| **FP-08** | Output Format Selector | `JPEG` (default), `PNG`, `WebP`, `AVIF` (`index.html:125-130`) | `ComboBox` defaulting to `JPEG` with `JPEG`, `PNG`, `WebP`, `AVIF`. | ViewModel unit test |
| **FP-09** | Quality Control | Slider `1–100`, default `82`, labels `Smaller file` / `Sharper image`; hidden for `PNG` (`app.js:443-448`) | `Slider` (`1–100`, default `82`) + numeric readout + hint labels; automatically collapsed when `PNG` is selected. | ViewModel + Encoder tests |
| **FP-10** | Proportional Downscale Resize | Toggle OFF by default (`"Keep the original proportions"`), `Max width` `1–30000` (default `1920`), **never upscales** (`app.js:222-224`) | Identical toggle + `NumberBox` (`1–30000`, default `1920`); computes `targetWidth = min(origWidth, clamp(maxWidth, 1, 30000))` and proportional height; Lanczos resampling. | Unit + image dimension accuracy tests |
| **FP-11** | JPEG Transparency Background | Banner + color input `#ffffff`, shown only for `JPEG`; fills canvas before draw (`index.html:160-168`, `app.js:231-235`) | Shown only for `JPEG`; interactive color swatch + `ColorPicker` flyout defaulting to `#FFFFFF`; composites alpha onto solid RGB before JPEG write. | Transparent PNG/WebP/AVIF → JPEG pixel color test |
| **FP-12** | EXIF Orientation & Quality Preservation | Browser `createImageBitmap` auto-orients pixels (`TEST-REPORT.md:45`) | Explicit `AutoOrient()` before measuring dimensions/resizing so portrait photos never rotate sideways; sRGB normalization + high-quality Lanczos resize + sharp YUV chroma rules (`>=90` → `4:4:4`). | Oriented JPEG test |
| **FP-13** | Progress & Cancellation | Busy lock, `"Convert images"` ↔ `"Cancel"`, sequential processing, keeps partial results (`app.js:294-333`) | Locks inputs during run, shows overall `ProgressBar` (`X of Y`) + per-row `Working…`/`Done`/`Failed`, supports `Cancel` keeping completed outputs. | Async cancellation integration test |
| **FP-14** | Results & Delta Display | Thumbnail over checkerboard, output filename, `orig → new`, `% smaller`/`% larger`, `Download` (`app.js:111-132`) | Checkerboard thumbnail preview, sanitized filename, size transition, color-coded `% smaller`/`% larger` badge, `Save` button per row (`FileSavePicker`). | ViewModel + UI test |
| **FP-15** | ZIP Export | Uncompressed (`STORE`) UTF-8 ZIP (`app.js:365-423`); note web bug saved `cody-converted-*.zip` | `ZipArchive` (`CompressionLevel.NoCompression` for speed on compressed images), UTF-8 filenames, `DAS-converted-YYYY-MM-DD.zip`, collision disambiguation (`name (2).ext`). | Zip roundtrip & collision unit tests |
| **FP-16** | Light & Dark Themes | Web has Light theme only (Requirement 10 adds Dark theme) | Full **DAS Light** and **DAS Dark** `ResourceDictionary` palettes + `System / Light / Dark` selector persisted locally. | Visual & theme-switch verification |
| **FP-17** | Error Handling (Corrupted/Unsupported) | Catches decode errors; note web aborts whole batch on 1 error | Validates corrupted/fake/truncated images per file with clear error message; isolates per-item conversion failures without aborting valid batch items. | Corrupted/renamed-file unit & integration tests |
| **FP-18** | Offline, Privacy & Ad Removal | AdSense scripts in `index.html`; Hostinger files | **Zero ads, zero web scripts, zero accounts, zero network calls.** `Package.appxmanifest` omits `internetClient`. | Static manifest & source compliance test |

---

## 2. Windows Application Architecture

### 2.1 Solution Structure

```text
DAS-Image-Converter-Windows/
├── DASImageConverter.sln
├── THIRD-PARTY-NOTICES.txt                  # Squoosh/DAS + Magick.NET (Apache 2.0) + ImageMagick + libheif/libaom notices
├── src/
│   ├── DasImageConverter.Core/              # Pure .NET 8 library (no WinUI/HWND dependencies)
│   │   ├── Models/
│   │   │   ├── ImageFormatKind.cs
│   │   │   ├── ConversionSettings.cs
│   │   │   ├── QueueImageItem.cs
│   │   │   ├── ConvertedImageResult.cs
│   │   │   └── BatchProgress.cs
│   │   ├── Validation/
│   │   │   └── ImageInputValidator.cs
│   │   └── Services/
│   │       ├── IImageProcessingEngine.cs
│   │       ├── MagickImageProcessingEngine.cs
│   │       ├── BatchConversionService.cs
│   │       ├── FileNameSanitizer.cs
│   │       └── ZipExportService.cs
│   │
│   └── DasImageConverter.App/               # WinUI 3 Single-Project MSIX (net8.0-windows10.0.19041.0)
│       ├── Package.appxmanifest
│       ├── Assets/                          # App & Store icons derived from DAS brand mark
│       ├── Themes/
│       │   ├── DasTokens.xaml               # Light & Dark ThemeDictionaries
│       │   └── DasStyles.xaml
│       ├── Converters/
│       │   └── ValueConverters.cs
│       ├── ViewModels/
│       │   ├── MainViewModel.cs
│       │   ├── QueueItemViewModel.cs
│       │   └── ResultItemViewModel.cs
│       ├── Services/
│       │   ├── IFileDialogService.cs
│       │   ├── WinUIFileDialogService.cs
│       │   └── ThemeService.cs
│       ├── App.xaml / App.xaml.cs
│       └── MainWindow.xaml / MainWindow.xaml.cs
│
└── tests/
    └── DasImageConverter.Tests/             # xUnit test suite targeting DasImageConverter.Core
```

### 2.2 Processing Pipeline & 50-Image Memory Architecture
1. **Inspection Phase (On Add):**
   - `ImageInputValidator` checks extension/size (`<= 100 MB`) and batch capacity (`<= 50`) before touching pixel buffers.
   - `MagickImageProcessingEngine.InspectAsync` reads image dimensions (`Ping` + `AutoOrient` dimension mapping, or full decode verification to catch truncated/corrupted payloads immediately!), validates `0 < W*H <= 60,000,000`, generates a `<= 160 px` wide JPEG thumbnail byte array, and **disposes `MagickImage` immediately**.
   - **Memory guarantee:** Even 50 queued 60 MP images consume only ~1–2 MB total RAM in UI state (50 tiny thumbnails + metadata).
2. **Conversion Phase (`BatchConversionService`):**
   - Creates a dedicated temporary batch workspace under `Path.Combine(Path.GetTempPath(), "DASImageConverter", batchId)` (inside MSIX isolated temp container).
   - Processes items with bounded concurrency (`SemaphoreSlim`, max `2` concurrent for AVIF or `Environment.ProcessorCount / 2` capped at `4` for JPEG/PNG/WebP) and configures `ResourceLimits.Memory` so memory spikes never trigger OOM.
   - Per item:
     1. Load source file → `AutoOrient()`.
     2. If resize enabled and `maxWidth < image.Width`: `Resize(new MagickGeometry(targetWidth, targetHeight) { IgnoreAspectRatio = true })` using Lanczos filter (`FilterType.Lanczos`).
     3. Color space normalization to sRGB if needed (preserving visual color accuracy when converting wide-gamut/CMYK sources).
     4. If target is `JPEG`: composite onto `JpegBackgroundColor` (`AlphaOption.Remove`) and encode at `Quality`.
     5. If target is `PNG`: lossless encode preserving alpha.
     6. If target is `WebP`: encode at `Quality` preserving alpha.
     7. If target is `AVIF`: encode at `Quality` with chroma subsampling parity (`4:4:4` for `quality >= 90`, `4:2:0` otherwise) and speed `6` (`avif:speed=6`).
     8. Write output bytes directly to batch temp file (and build `<= 160 px` PNG preview thumbnail for transparency checkerboard), then **dispose `MagickImage`**.
   - **Cleanup:** Temp files from superseded batches are cleaned when `Clear all`, a new batch starts, or the app closes.
3. **Export Phase:**
   - **Individual Save:** Streams result temp file directly to user-selected `StorageFile` via `FileSavePicker`.
   - **ZIP Export:** `ZipExportService` streams each result temp file sequentially into `ZipArchive` (`CompressionLevel.NoCompression`) with `FileNameSanitizer` duplicate disambiguation—constant minimal memory overhead even for 50 large outputs.

### 2.3 DAS Light & Dark Theme Token System

| Semantic Token | DAS Light | DAS Dark | Usage |
|---|---|---|---|
| `DasPageBackground` | `#F5F7FB` | `#0F141B` | Window / scroll backdrop (plus subtle coral glow at top-right) |
| `DasCardBackground` | `#FFFFFF` | `#171F2A` | Settings & Results cards |
| `DasRowBackground` | `#FBFCFD` | `#1C2633` | Queue & Result rows |
| `DasBorderSubtle` | `#DFE5EC` | `#273242` | Card borders, inputs, dividers |
| `DasInkPrimary` | `#18212C` | `#F1F5F9` | Headlines, filenames, values |
| `DasInkMuted` | `#687383` | `#94A3B8` | Subtitles, metadata, labels |
| `DasCoralAccent` | `#EF6B4D` | `#F27A5E` | Primary CTA, active switch, slider, hero emphasis |
| `DasCoralDark` | `#D9573A` | `#E06346` | Hover state, section kickers |
| `DasCoralWash` | `#FFF0EB` | `#2D1D1B` | Drop zone hover tint, icon tile, error notice fill |
| `DasMintAccent` | `#47A786` | `#52BA96` | `"Processed locally"` status dot |
| `DasMintText` | `#377F69` | `#6EE7B7` | Size reduction delta, Save button foreground |
| `DasMintWash` | `#F4FBF8` | `#142823` | Save button background |
| `DasWarningWash` | `#FFF7ED` | `#2A2118` | JPEG transparency guidance banner |

### 2.4 Single-Project MSIX & Store Architecture
- `Package.appxmanifest` declarations:
  - `<Capabilities><rescap:Capability Name="runFullTrust" /></Capabilities>` **only** — explicitly **no** `internetClient`, **no** `broadFileSystemAccess`.
  - Supported architectures: explicit `win-x64` (and `win-arm64` build support), `WindowsAppSDKSelfContained` / Framework-dependent Store configuration validated via WACK.
  - Visual assets generated cleanly from `android-chrome-512.png` (DAS coral mark) at required MSIX scales.
