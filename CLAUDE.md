# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**MyAssetManager** (also called "Booth Asset Linker") is a Unity Editor plugin written in C#. It manages imported Unity packages from two sources: the Booth marketplace and the Unity Asset Store. The tool scans library folders, parses `.unitypackage` files, and lets users register and navigate imported packages from within the Unity Editor.

## Deployment

This project has no build system. All `.cs` files are placed directly into a Unity project at `Assets/Editor/MyAssetManager/` (or any subfolder under `Assets/Editor/`). Unity compiles them automatically on import. Menu items appear under `Tools/MyAssetManager/*` in the Unity Editor.

There are no tests, no linter, and no CI configuration.

## Architecture

The codebase follows a strict layered structure with all classes under the root namespace:

### Layers (bottom to top)

1. **Data Models** — plain serializable structs/classes for JSON persistence:
   - [AssetLinkerData.cs](AssetLinkerData.cs) — Booth: `PackageData`, `ProductData`, `AssetLinkerData`
   - [AssetStoreLinkerData.cs](AssetStoreLinkerData.cs) — Asset Store: `AssetStorePackageData`, `AssetStoreProductData`, `AssetStoreLinkerData`

2. **Settings & Storage** — static classes for persistence:
   - [AssetLinkerSettings.cs](AssetLinkerSettings.cs) — EditorPrefs for library root paths (`MyAssetManager.BoothLibraryRoot`, `MyAssetManager.AssetStoreRoot`)
   - [AssetLinkerStorage.cs](AssetLinkerStorage.cs) — Reads/writes `UserSettings/booth-asset-linker.json`
   - [AssetStoreStorage.cs](AssetStoreStorage.cs) — Reads/writes `UserSettings/assetstore-asset-linker.json`

3. **Parsing & Scanning** — static utility classes:
   - [UnitypackageParser.cs](UnitypackageParser.cs) — Low-level TAR/gzip decompression; extracts asset paths and embedded icons from `.unitypackage` files; handles both standard and GNU tar header formats
   - [BoothScanner.cs](BoothScanner.cs) — Scans Booth library root for `b{ID}/` folders; parses packages; matches files already present in the project; suggests target directory via heuristic (most common ancestor of matched files)
   - [AssetStoreScanner.cs](AssetStoreScanner.cs) — Scans Unity Asset Store cache; derives publisher name from directory depth; computes composite IDs; extracts icons from packages

4. **UI Windows** — `EditorWindow` subclasses:
   - [ProductListWindow.cs](ProductListWindow.cs) — Main dashboard; shows registered products with thumbnails, Ping/navigate buttons, delete/re-target
   - [RegistrationWindow.cs](RegistrationWindow.cs) — Single-product registration: pick `b{ID}` folder → parse → choose target dir → save
   - [ScanAllWindow.cs](ScanAllWindow.cs) — Batch Booth scan; multi-select registration with editable target dirs
   - [AssetStoreScanAllWindow.cs](AssetStoreScanAllWindow.cs) — Batch Asset Store scan; parallel to `ScanAllWindow`
   - [UnitypackageParserTestWindow.cs](UnitypackageParserTestWindow.cs) — Debug/dev tool for testing the parser

### Key Design Decisions

- All scanner, parser, storage, and settings classes are **static** — no instances, no DI.
- Booth product metadata (name, thumbnail URL) is fetched **asynchronously** via `HttpClient` scraping Booth HTML, with results applied via `EditorApplication.delayCall` to avoid blocking the editor.
- The Asset Store path structure is used to infer publisher names (directory depth heuristic).
- Progress feedback uses `EditorUtility.DisplayProgressBar` / `ClearProgressBar`.
- Booth folders are identified by the pattern `b{numericID}/` at the library root.

### Data Persistence Locations

| Store | File |
|---|---|
| Booth products | `{UnityProject}/UserSettings/booth-asset-linker.json` |
| Asset Store products | `{UnityProject}/UserSettings/assetstore-asset-linker.json` |
| Library root paths | Unity `EditorPrefs` |

### External Dependencies (Unity/.NET only)

- `System.IO.Compression` — GZipStream for `.unitypackage` decompression
- `System.Net.Http` — HttpClient for Booth web scraping
- `System.Text.RegularExpressions` — TAR header parsing, Booth item ID extraction, HTML metadata parsing
- Unity APIs: `UnityEditor`, `UnityEngine`, `EditorPrefs`, `AssetDatabase`, `EditorGUILayout`

## UI Language

UI-facing strings (labels, dialog messages, menu items) are in **Japanese**. Code identifiers and comments are in English.
