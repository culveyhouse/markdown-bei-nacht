# Markdown bei Nacht

[![Version](https://img.shields.io/badge/version-v1.1.1-2563eb)](https://github.com/culveyhouse/markdown-bei-nacht/releases)
[![Release State](https://img.shields.io/badge/release-stable-16a34a)](https://github.com/culveyhouse/markdown-bei-nacht/releases)
[![Platform](https://img.shields.io/badge/platform-Windows%2011%20x64-0ea5e9)](https://github.com/culveyhouse/markdown-bei-nacht)
[![.NET](https://img.shields.io/badge/.NET-8-512bd4)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
[![License](https://img.shields.io/badge/license-MIT-f59e0b)](LICENSE)

Markdown bei Nacht is a lightweight Windows desktop app for previewing local Markdown and plain text files in a near-GitHub dark style without opening a full editor or copying content into a browser.

`v1.1.1` is the current stable public release. The app stays intentionally lean: one document per window, clean Windows install flow, local-first behavior, and no forced takeover of your default Markdown file association.

## Highlights

- Open local Markdown and `.txt` files from Explorer `Open with`, in-app `File > Open`, or drag-and-drop.
- Reopen the last 8 viewed Markdown or `.txt` files from `File > Recent Files`.
- Keep one document per window.
- Open additional windows with a small cascade offset so they do not stack exactly on top of each other.
- Auto-refresh the preview when the source file changes on disk.
- Render GitHub-style Markdown structure with a midnight-dark visual theme.
- Open local Markdown and `.txt` links in a new app window.
- Open `http/https` links in the default browser.
- Open other local file links with the Windows shell.
- Resolve relative local assets against the current Markdown file.
- Block remote images intentionally.
- Persist a configurable base dark color in local app settings.
- Open the installed user guide from `Help > User Guide` or `F1`.
- Install with an optional desktop shortcut and Explorer `Open with` support for Markdown file types.

## Download And Install

For normal use, download the latest installer from [GitHub Releases](https://github.com/culveyhouse/markdown-bei-nacht/releases/latest).

If you are working from the repository locally, the current build artifacts are:

- Installer: `artifacts/installer/MarkdownBeiNacht-Setup.exe`
- Published app: `artifacts/publish/win-x64/MarkdownBeiNacht.exe`

Install steps on Windows 11:

1. Run `MarkdownBeiNacht-Setup.exe`.
2. If you want a desktop icon, select the desktop shortcut option during setup.
3. Finish the install and launch Markdown bei Nacht from the Start Menu or desktop shortcut.

Install notes:

- The installer is per-user and installs under `%LocalAppData%\Programs\Markdown bei Nacht`.
- Administrator rights are not required.
- If Microsoft WebView2 is missing, setup may download and install it.

## Windows Trust Note

Release artifacts can be code signed with Azure Artifact Signing by running `scripts/sign-release.ps1`.

That signed release flow removes the worst `Unknown publisher` / unsigned posture on Windows, but a brand-new download can still see SmartScreen reputation prompts until the app builds reputation.

## Compatibility

- Windows 11 x64 is the supported target.
- Published builds do not require a separate .NET runtime install.
- WebView2 is required at runtime and is bootstrapped by setup when needed.
- ARM64 is not targeted yet.

## Support And Project Info

- Releases: [GitHub Releases](https://github.com/culveyhouse/markdown-bei-nacht/releases)
- Changelog: [CHANGELOG.md](CHANGELOG.md)
- Issues and feedback: [GitHub Issues](https://github.com/culveyhouse/markdown-bei-nacht/issues)
- License: [MIT License](LICENSE)

## Architecture

The app is split into a few clear layers:

- `src/MarkdownBeiNacht`: the Windows desktop shell built with `WPF`.
- `src/MarkdownBeiNacht.Core`: Markdown rendering, path resolution, settings, and file-loading logic.
- `tests/MarkdownBeiNacht.Tests`: unit tests for core behavior plus a few desktop-side helper checks.
- `installer`: Inno Setup installer script and runtime bootstrapper assets.
- `scripts`: helper scripts for build, publish, and installer packaging.

At runtime, the native shell reads a local document from disk, renders Markdown through `Markdig` plus `HtmlSanitizer` or plain text through a simple paragraph renderer, and injects the result into a local `WebView2` HTML shell for rendering.

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
.\artifacts\publish\win-x64\MarkdownBeiNacht.exe ".\path\to\notes.md"
```

Run the executable from its published folder rather than moving the `.exe` by itself.

## Release Packaging

Installer packaging is defined in `installer/MarkdownBeiNacht.iss`.

Release packaging behavior:

- per-user install under `%LocalAppData%\Programs`
- visible in Explorer `Open with` for `.md`, `.markdown`, and `.mdown`
- no forced takeover of the default `.md` association
- optional desktop shortcut during install
- installed user guide in the app folder, Start Menu, and in-app `Help` menu
- WebView2 bootstrap when the runtime is missing
- signed installer for release distribution when `scripts/sign-release.ps1` is used

Build the installer from the repository root with:

```powershell
PowerShell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1 -Configuration Release -Runtime win-x64
```

The resulting setup executable is written to `artifacts/installer/MarkdownBeiNacht-Setup.exe`.

## Code Signing Releases

The release signing workflow lives in `scripts/sign-release.ps1`.

Use it whenever a shipped binary changes. In practice, rerun signing after any change that rebuilds one of these files:

- `artifacts/publish/win-x64/MarkdownBeiNacht.exe`
- `artifacts/publish/win-x64/MarkdownBeiNacht.dll`
- `artifacts/publish/win-x64/MarkdownBeiNacht.Core.dll`
- `artifacts/installer/MarkdownBeiNacht-Setup.exe`

From the repository root:

```powershell
PowerShell -ExecutionPolicy Bypass -File .\scripts\sign-release.ps1
```

What the script does:

- runs `scripts/publish.ps1`
- signs the published app binaries with Azure Artifact Signing
- rebuilds the Inno Setup installer from those signed binaries
- signs the final `MarkdownBeiNacht-Setup.exe`
- verifies the final installer signature

First-time machine setup:

1. Install the `.NET 8 Runtime`.
2. Install the `Azure CLI`.
3. Install `Microsoft.Azure.ArtifactSigningClientTools`.
4. Sign in once with `az login --use-device-code`.
5. Make sure the Azure signing account, identity validation, and certificate profile already exist.

Current repository defaults for the script:

- Signing account: `culveyhouse-signing`
- Certificate profile: `public-trust`
- Endpoint: `https://eus.codesigning.azure.net`

If those Azure names or the signing region ever change, pass overrides to `scripts/sign-release.ps1` instead of editing the publish or installer scripts.

## Known Limitations In v1.1.1

- Remote images are blocked by design.
- Brand-new signed downloads can still trigger SmartScreen reputation prompts until the app builds reputation.
- The installer may need internet access if WebView2 is missing on the target machine.
- ARM64 is not targeted yet.

