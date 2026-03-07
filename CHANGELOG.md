# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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



