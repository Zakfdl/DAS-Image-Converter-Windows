# Phase 1: Source Code Audit & Technical Feasibility Report

**Project:** DAS Image Converter — Native Windows App  
**Audit Scope:** Full source inspection of user-supplied `img.berrizak.zip` (19 files) + local Windows development toolchain assessment.  
**Live URL Note:** Direct HTTP verification of `https://img.berrizak.com` was not executed in the offline sandbox; all web findings below reflect the exact source archive provided (`img.berrizak.zip`, file timestamps `2026-10-12`).

---

## 1. Source Code Audit (`img.berrizak.zip`)

### 1.1 Archive Inventory & Brand Heritage
| File / Path | Size | Role | Audit Notes |
|---|---:|---|---|
| [index.html](file:///C:/Users/zakariya.essa/.gemini/antigravity/brain/1f60c2d3-1264-4300-9092-82688225ef16/scratch/audit_src/index.html) | 9,392 B | App markup & metadata | Branded **DAS Image Converter**; contains Google AdSense `<script>` and placeholder slots (`REPLACE_WITH_*_SLOT_ID`). |
| [app.js](file:///C:/Users/zakariya.essa/.gemini/antigravity/brain/1f60c2d3-1264-4300-9092-82688225ef16/scratch/audit_src/app.js) | 18,683 B | Core browser logic | 487 lines, zero runtime npm dependencies; Canvas encoding + lazy WASM AVIF + custom STORE ZIP writer. |
| [styles.css](file:///C:/Users/zakariya.essa/.gemini/antigravity/brain/1f60c2d3-1264-4300-9092-82688225ef16/scratch/audit_src/styles.css) | 13,716 B | Visual system & layout | Light theme only; no dark mode rules or theme variables override. |
| [avif/avif_enc.js](file:///C:/Users/zakariya.essa/.gemini/antigravity/brain/1f60c2d3-1264-4300-9092-82688225ef16/scratch/audit_src/avif/avif_enc.js) & `avif_enc.wasm` | 43 KB / 2.82 MB | AVIF encoder | Reused Squoosh codec build (`libavif v1.0.1` + AOM AV1). |
| [manifest.webmanifest](file:///C:/Users/zakariya.essa/.gemini/antigravity/brain/1f60c2d3-1264-4300-9092-82688225ef16/scratch/audit_src/manifest.webmanifest) | 461 B | PWA manifest | Name: `"DAS Image Converter"`, Short: `"DAS Images"`, BG/Theme: `#f5f7fb`. |
| `android-chrome-192.png`, `512.png`, `apple-touch-icon.png`, `favicon.ico` | 3.6–32 KB | Brand icons | Coral-orange gradient square containing bold white geometric **DAS** wordmark. |
| `ads.js`, `ads.txt`, `.htaccess`, `HOSTINGER-GUIDE.md` | — | Web hosting & ads | Static Hostinger deployment & AdSense integration; **excluded** from offline desktop scope. |
| `README.md`, `TEST-REPORT.md`, `THIRD-PARTY-NOTICES.txt`, `LICENSE-SQUOOSH-APACHE-2.0` | — | Docs & licenses | Originated as **Cody Image Converter**; explicitly excludes GPL `libimagequant`. |

---

### 1.2 Functional Behavior & Exact Business Rules (`app.js` & `index.html`)

1. **Accepted Inputs & Hard Guards**
   - **Formats:** JPEG (`.jpg`, `.jpeg`, `image/jpeg`), PNG (`.png`, `image/png`), WebP (`.webp`, `image/webp`), AVIF (`.avif`, `image/avif`).
   - **Batch limit (`MAX_FILES`):** `50` files max in queue. Trying to add when full shows `"You can convert up to 50 files at a time."`; partial overflow truncates to remaining slots and notifies `"Only 50 files can be selected at once."`.
   - **File size limit (`MAX_FILE_BYTES`):** `100 MB` (`100 * 1024 * 1024` bytes) per file.
   - **Resolution guard (`MAX_PIXELS`):** `60,000,000` pixels (60 MP) max per image (`width * height > MAX_PIXELS`), plus `width > 0 && height > 0`.
   - **Deduplication:** Incoming files matching both `file.name` and `file.size` of an existing queued item are silently skipped (`app.js:185`).
   - **Input methods:** Click/keyboard (`Enter`/`Space`) on drop zone opens file picker (`multiple`), plus HTML5 drag-and-drop onto `#drop-zone`.

2. **Pre-Conversion Inspection & Queue List**
   - Each accepted file is decoded immediately via `createImageBitmap(file)` to validate integrity, measure pixel dimensions, and generate a thumbnail (`<= 160 px` wide JPEG at `0.72` quality).
   - File row displays: 36×36 thumbnail, filename (truncated with ellipsis), `formatted size · W × H px`, status badge (`Ready` / `Working…` / `Done` / `Unsupported`), and individual remove (`×`) button.
   - Footer/action strip displays `"N image(s) ready"`, `"Clear all"`, and `"Convert images"`.

3. **Conversion Settings**
   - **Convert to:** `jpeg` (default), `png`, `webp`, `avif`.
   - **Quality slider:** Integer `1–100`, default `82`, labeled `"Smaller file"` (min) to `"Sharper image"` (max). Hidden automatically when `png` is selected. Passed as `quality / 100` to Canvas for JPEG/WebP and `1–100` to AVIF.
   - **Resize toggle + Max width:** Switch `"Resize images"` / `"Keep the original proportions"` (default **OFF**). Numeric `Max width` (`1–30000 px`, default `1920`).
     - **Downscale-only math (`app.js:223-224`):** `targetWidth = Math.min(item.width, Math.max(1, maxWidth || item.width))`; `targetHeight = Math.max(1, Math.round(item.height * targetWidth / item.width))`. Smaller images are **never upscaled**.
   - **JPEG Transparency Handling (`app.js:231-235`):** Shown only when `jpeg` is selected. Defaults to `#ffffff` color picker; fills canvas with chosen solid RGB before drawing the source bitmap.

4. **Conversion Execution & Cancellation**
   - Locks drop zone and all settings controls (`setBusy(true)`), swaps `"Convert images"` with `"Cancel"`.
   - Processes valid queue items sequentially.
   - **AVIF encoding parameters (`app.js:258-270`):** `quality`, `qualityAlpha: -1`, `speed: 6`, `subsample: quality >= 90 ? 3 (4:4:4) : 1 (4:2:0)`, `enableSharpYUV: true`, `tune: 0`.
   - **Cancellation:** `AbortController` aborts conversion and retains any already-completed outputs in Results (`"Conversion cancelled. Completed files are still available below."`).

5. **Output Filenames, Results & ZIP Export**
   - **Filename sanitization (`app.js:59-61`):** Strips original extension, removes non-`[\w\s-]`, trims, converts whitespace runs to `-`, falls back to `"image"`, and appends `.jpg` / `.png` / `.webp` / `.avif`.
   - **Results panel:** 62×54 preview thumbnail over transparency checkerboard, output filename, `input size → output size`, percentage delta (`X% smaller` in green or `X% larger` in warm terracotta), and per-file `Download` button.
   - **ZIP export (`app.js:365-423`):** Uncompressed (`STORE`, method `0`) ZIP with UTF-8 flag (`0x800`) and CRC-32.

---

### 1.3 Brand Identity & Visual Design Tokens (`styles.css`)

| Token / Element | Observed Value | Desktop Adaptation Notes |
|---|---|---|
| Canvas background (`--soft`) | `#f5f7fb` + subtle top-right coral radial wash | Map to DAS Light backdrop; craft complementary dark slate (`#11171f` / `#18212c`) for Dark theme. |
| Card surface (`--card`) | `#ffffff`, border `rgba(220,227,235,0.9)`, `20px` radius | Native WinUI `Border` / card container with subtle elevation & hairline border. |
| Primary text (`--ink`) | `#18212c` | Crisp high-contrast ink; `#f1f5f9` in Dark theme. |
| Muted text (`--muted`) | `#687383` | Secondary metadata; `#9aa5b1` in Dark theme. |
| Brand Coral (`--coral` / `--coral-dark`) | `#ef6b4d` / `#d9573a`, wash `#fff0eb` | Signature DAS accent for primary CTA, active toggle, hero emphasis, kickers. |
| Success / Mint (`--mint` / `#47a786`) | `#dff2eb`, status `#399073`, `#377f69` | Local privacy status dot, size-reduction delta, download buttons, results border. |
| Warning / Error | Text `#a64d34`, surface `#fff1ec`; JPEG note `#876e5f` / `#fff7ed` | Inline status banners & error badges. |
| Typography | `Inter, Segoe UI, sans-serif`; uppercase tracked kickers; tight bold headings | Use `Segoe UI Variable` (native WinUI 3) with matching weight/tracking hierarchy. |

---

### 1.4 Gaps, Bugs & Desktop-Specific Improvements Identified in Web Code

1. **Missing Dark Theme:** Web `styles.css` only implements light mode. Native app requires both **Light and Dark themes** (supporting System default + manual Light/Dark/System toggle).
2. **Residual `"Cody"` ZIP Naming Bug:** `app.js:477` exports `cody-converted-YYYY-MM-DD.zip`. Desktop app must use `DAS-converted-YYYY-MM-DD.zip`.
3. **ZIP Filename Collisions:** `baseName()` can produce identical output filenames (e.g., `photo.jpg` and `photo.png` both converted to WebP become `photo.webp`). Native ZIP & batch folder export must disambiguate duplicates (`photo.webp`, `photo (2).webp`).
4. **Batch Abort on Single Item Error:** In `app.js:306-322`, an exception on item *N* aborts items *N+1..50*. Native app should record per-item errors and allow remaining valid files to finish cleanly.
5. **Minimal Progress Feedback:** Web app only toggles `"Working…"` text per row. Native app requires clear batch progress indicators (overall progress bar, `X of Y` counter, current file status).
6. **EXIF Orientation & Metadata Fidelity:** Browser `createImageBitmap` automatically applies EXIF orientation but strips metadata. Native pipeline must explicitly auto-orient pixels via EXIF orientation tags and preserve sRGB/color fidelity (with optional metadata preservation vs clean output clarified in Phase 2).
7. **AdSense & Hostinger Artifacts:** `ads.js`, `ads.txt`, `.htaccess`, and Google AdSense scripts must be **completely omitted** from the offline native app.

---

## 2. Technical Feasibility Report

### 2.1 Mandatory Stack & Target Platform
- **Framework:** C# + .NET (`.NET 8` LTS or `.NET 9`, targeting `net8.0-windows10.0.19041.0` or `net9.0-windows10.0.19041.0`) + **WinUI 3 / Windows App SDK** (single-project MSIX).
- **Target OS:** Windows 10 version 1809+ (build `17763`+) / recommended minimum `10.0.19041.0` (Windows 10 2004+) and Windows 11. Current dev machine is **Windows Pro Build 26200 (25H2), x64, 8 logical cores, 270.65 GB free**.
- **Microsoft Store Compliance:** Offline-first, zero network capability needed (`internetClient` not required), standard `runFullTrust` packaged desktop capability, `FileOpenPicker` / `FileSavePicker` / `FolderPicker` + WinUI drag-and-drop without broad filesystem permissions.

### 2.2 Local Image Processing Engine Evaluation (Offline JPEG, PNG, WebP, AVIF)

> [!IMPORTANT]
> Built-in Windows Imaging Component (`Windows.Graphics.Imaging.BitmapDecoder` / `BitmapEncoder`) **cannot** guarantee offline AVIF encoding/decoding because Windows relies on optional Microsoft Store codec extensions (*AV1 Video Extension*, *WebP Image Extension*) that may not be installed on a user's machine. All four codecs must be self-contained inside the MSIX package.

| Candidate Engine | Formats Covered | License Profile | MSIX / Store Feasibility | Verdict |
|---|---|---|---|---|
| **Option A: `Magick.NET-Q8-x64` (+ `ARM64` for multi-arch bundle)** | JPEG, PNG, WebP, **AVIF** (all-in-one) | Magick.NET is Apache 2.0, ImageMagick is Apache-compatible; **HOWEVER**, bundled `libheif` / `libde265` delegates are **LGPL v3** (and HEVC patent-adjacent). | Single self-contained NuGet; bundles ~25–35 MB native DLL per arch. Easy API (`AutoOrient()`, Lanczos resize, alpha composite, quality/chroma controls). Must review LGPL relinkability notice for closed-source Store MSIX. | **Strongest turnkey engineering choice** if LGPL notice/compliance is acceptable (or build custom delegates without HEVC/LGPL). |
| **Option B: `SkiaSharp` (MIT) + `libavif` (`BSD-2-Clause` + `libaom`/`dav1d`)** | `SkiaSharp` handles JPEG, PNG, WebP + resize/compositing; `libavif` handles AVIF decode/encode | **100% permissive (MIT + BSD-2-Clause + AOM royalty-free patent license)** — matches web `THIRD-PARTY-NOTICES.txt` zero-copyleft stance! | Requires bundling/wrapping `libavif.dll` (via `LibavifSharp` or small custom P/Invoke + CMake/vcpkg build of `libavif` + `aom`/`dav1d` for `x64`/`ARM64`) alongside `SkiaSharp`. Smaller footprint & zero LGPL/HEVC exposure. | **Cleanest Store licensing & closest codec parity** with the web app's Squoosh/`libavif` pipeline. |
| **Option C: `NetVips` + `NetVips.Native`** | JPEG, PNG, WebP, AVIF | `libvips` + `libheif` are **LGPL** | Very fast/low memory, larger multi-DLL native payload inside MSIX. | Less ideal than Option A or B for Store compliance simplicity. |

### 2.3 Local Toolchain & Environment Audit (Critical Blocker)

Inspection of the current machine revealed:
- **Installed:** `.NET Runtime 9.0.22` (`Microsoft.NETCore.App` & `Microsoft.WindowsDesktop.App` only), `winget.exe`, `python.exe`.
- **Missing (Required before Phase 3 compilation & Phase 10 MSIX packaging):**
  1. **.NET SDK** (`.NET 8 SDK` or `.NET 9 SDK` — `dotnet --info` reports *"No SDKs were found"*).
  2. **Windows 10/11 SDK** (`Windows Kits\10` — needed for `makeappx.exe`, `signtool.exe`, `Windows App Certification Kit (WACK)`, and WinMD/PRI tooling).
  3. **MSBuild / Visual Studio Build Tools (or Visual Studio 2022)** with *.NET Desktop* + *Windows App SDK / C#* build components (required for WinUI 3 XAML compilation & single-project MSIX packaging).
  4. **Git on active PATH** (User PATH references `AppData\Local\hermes\git\cmd`, not currently accessible inside sandbox).

### 2.4 Performance & Memory Feasibility (50-Image Batch)
- Worst-case input in existing spec: `50` images up to `60 MP` (`~240 MB` uncompressed RGBA per decoded frame).
- **Risk:** Decoding/holding all 50 full-resolution bitmaps simultaneously would allocate `>12 GB` RAM and trigger `OutOfMemoryException`.
- **Mitigation Design:**
  - Store only lightweight metadata + capped `160 px` preview thumbnails in UI state.
  - Decode, resize, composite, encode, and dispose **one source bitmap at a time per worker** using bounded parallelism (e.g., `1–2` concurrent workers for heavy AVIF encodes, `2–4` for JPEG/PNG/WebP) with `ArrayPool` / immediate disposal and cancellation token support.
  - Write converted outputs to temp storage or stream-backed buffers rather than keeping 50 uncompressed canvases resident.

### 2.5 Technical Feasibility Verdict
**FEASIBLE — with two prerequisites before coding begins:**
1. Confirm the **imaging engine choice** (Option A `Magick.NET` vs Option B `SkiaSharp` + `libavif` permissive stack) and **target repository path**.
2. Install the missing **.NET SDK + Windows SDK / Visual Studio Build Tools** so WinUI 3 compilation, unit/integration tests, and MSIX/WACK verification can run locally.
