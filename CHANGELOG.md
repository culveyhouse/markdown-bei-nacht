# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.8.0] - 2026-03-08

### Added

- Optional desktop shortcut creation during installer setup.
- A user-facing installed `README.md` plus a Start Menu shortcut that opens the guide inside Markdown bei Nacht.
- Explicit one-file-per-window behavior for `File > Open` and drag-and-drop when a window already has a file open.

### Changed

- Retargeted the project release framing to `0.8.0` as an almost-ready-to-ship polish build awaiting final release cleanup.
- Moved WebView2 runtime data out of the publish/install directory and into local app data.
- Cleaned publish output so release builds do not carry forward stray `*.WebView2` folders or `.pdb` files.
- Kept the installer unsigned and bootstrapper-based for a lean Windows 11 x64 near-release flow.

### Fixed

- Hardened theme settings persistence by saving settings atomically and surfacing save failures to the user.
- Added regression coverage for settings round-trip behavior and the current-window reuse policy.

## [0.2.0] - 2026-03-07

### Added

- Installer packaging flow for the published Windows app, including per-user installation and `Open With` registration for Markdown file types.

### Changed

- Updated app and installer version metadata to `0.2.0`.
- Expanded installer file association support to `.md`, `.markdown`, and `.mdown`.
- Corrected the installer Start Menu shortcut target for Inno Setup packaging.
- Verified clean install, Explorer `Open With` launch, and uninstall cleanup for the packaged app.

## [0.1.1] - Unreleased

### Changed

- Added a quick-start pointer near the top of the README so the published executable path is immediately visible.
- Improved the app menu presentation and submenu styling to better match the midnight theme.
- Reworked the theme settings dialog layout and button styling so the content fits cleanly at normal Windows scaling.

## [0.1.0] - Unreleased

### Added

- Initial Windows desktop Markdown viewer built with `WPF`, `.NET 8`, and `WebView2`.
- Markdown rendering pipeline using `Markdig`, `HtmlSanitizer`, and bundled `highlight.js`.
- Local file open flow through command-line launch, `File > Open`, and drag-and-drop.
- Live preview refresh using file watching with debounce behavior.
- Scroll-position preservation during preview refresh.
- Local Markdown link handling that opens a new application window.
- External link routing to the default browser and local non-Markdown file routing through the Windows shell.
- Local app settings persistence for the midnight base color.
- Unit tests for rendering, path resolution, settings, and debounce-related behavior.
- Build and publish helper scripts.
- Inno Setup installer definition for per-user install and `Open with` registration.

### Known Gaps

- Installer compilation still depends on a local Inno Setup installation.
- No signed release artifacts yet.
