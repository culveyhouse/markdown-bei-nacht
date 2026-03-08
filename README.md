# Markdown bei Nacht

Markdown bei Nacht is a lightweight Windows desktop app for previewing local Markdown files without opening a full editor or copying content into a browser.

Quick start: if you want the current installer build, use `artifacts/installer/MarkdownBeiNacht-Setup.exe`.
If you want to run the published app directly, use `artifacts/publish/win-x64/MarkdownBeiNacht.exe`.

It is a native `WPF` application built on `.NET 8` with an embedded `WebView2` renderer. The current `v0.9.0` polish build is almost ready to ship and focuses on a tight Windows desktop experience: install cleanly, open one Markdown file per window, render it clearly, refresh the preview when the file changes on disk, and now present a fully branded Windows app surface.

## Status

Current version: `v0.9.0`

Release state: almost ready to ship, with installer packaging, optional desktop shortcut creation, Explorer `Open With` integration, and an installed end-user guide already in place.

This repository is intentionally stopping just short of a `1.0.0` stamp so the final release pass can stay small and focused on the last polish items.

## Features

- Open a Markdown file from Explorer `Open with`, in-app `File > Open`, or drag-and-drop.
- Keep one Markdown file per window.
- Open additional windows with a small cascade offset so they do not stack exactly on top of each other, whether they come from in-app opening, drag-and-drop, Explorer, or launching the app again while it is already open.
- Auto-refresh the preview when the source file changes on disk.
- Render GitHub-style Markdown structure with a midnight-dark visual theme.
- Open local Markdown links in a new app window.
- Open `http/https` links in the default browser.
- Open non-Markdown local links with the Windows shell.
- Resolve relative local assets against the current Markdown file.
- Block remote images intentionally.
- Persist a configurable base dark color in local app settings.
- Install a user-facing `README.md` guide with the app and open it directly from `Help > User Guide` or `F1`.

## Architecture

The app is split into a few clear layers:

- `src/MarkdownBeiNacht`: the Windows desktop shell built with `WPF`.
- `src/MarkdownBeiNacht.Core`: Markdown rendering, path resolution, settings, and file-loading logic.
- `tests/MarkdownBeiNacht.Tests`: unit tests for the core behavior.
- `installer`: Inno Setup installer script and runtime bootstrapper assets.
- `scripts`: helper scripts for build, publish, and installer packaging.

At runtime, the native shell reads a Markdown file from disk, converts it to sanitized HTML with `Markdig` plus `HtmlSanitizer`, and injects the result into a local `WebView2` HTML shell for rendering.

## Stack

- `C#`
- `.NET 8`
- `WPF`
- `WebView2`
- `Markdig`
- `HtmlSanitizer`
- `highlight.js`
- `xUnit`
- `Inno Setup`

## Repository Layout

```text
src/
  MarkdownBeiNacht/
  MarkdownBeiNacht.Core/
tests/
  MarkdownBeiNacht.Tests/
installer/
scripts/
kuiper-belt-top-5.md
MarkdownBeiNacht.sln
CHANGELOG.md
```

## Development Requirements

To build from source, you need:

- Windows 11 x64
- `.NET 8 SDK`
- WebView2 runtime for local app execution
- Inno Setup only if you want to compile the installer `.exe`

The published app is self-contained for end users, so a separate .NET runtime install is not required for running published builds.

## Build And Test

From the repository root:

```powershell
PowerShell -ExecutionPolicy Bypass -File .\scripts\build.ps1
.\.dotnet8\dotnet.exe test .\tests\MarkdownBeiNacht.Tests\MarkdownBeiNacht.Tests.csproj
PowerShell -ExecutionPolicy Bypass -File .\scripts\publish.ps1 -Configuration Release -Runtime win-x64
```

Notes:

- In this workspace, `scripts/build.ps1` is the reliable build path.
- The repo-local `.dotnet8\dotnet.exe` test command is the reliable way to run tests here.
- Plain `dotnet build MarkdownBeiNacht.sln` can be flaky under default parallelism in this environment.
- If needed, the direct equivalent is `dotnet build MarkdownBeiNacht.sln -m:1`.

## Running The App Locally

The current published executable is expected at:

```text
artifacts/publish/win-x64/MarkdownBeiNacht.exe
```

Example:

```powershell
.\artifacts\publish\win-x64\MarkdownBeiNacht.exe ".\kuiper-belt-top-5.md"
```

Run the executable from its published folder rather than moving the `.exe` by itself.

## Packaging

Installer packaging is defined in `installer/MarkdownBeiNacht.iss`.

Current packaging goals:

- per-user install under `%LocalAppData%\Programs`
- visible in Explorer `Open with` for `.md`
- no forced takeover of the default `.md` association
- optional desktop shortcut during install
- installed user guide in the app folder, Start Menu, and in-app `Help` menu
- WebView2 bootstrap when the runtime is missing

Build the installer from the repository root with:

```powershell
PowerShell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1 -Configuration Release -Runtime win-x64
```

The resulting setup executable is written to `artifacts/installer/MarkdownBeiNacht-Setup.exe`.

## Known Limitations In v0.9.0

- Remote images are blocked by design.
- The installer is not code signed yet.
- The installer may need internet access if WebView2 is missing on the target machine.
- ARM64 is not targeted yet.

## Changelog

Project history is tracked in [CHANGELOG.md](CHANGELOG.md).

The changelog follows a Keep a Changelog style format and uses semantic versioning.

## License

This project is licensed under the [MIT License](LICENSE).

