# Markdown bei Nacht

Markdown bei Nacht is a simple Windows app for reading local Markdown files in a clean dark theme.

## What This App Does

- Opens one Markdown file per window.
- Lets you open files from the app, from Explorer, or by dragging them in.
- Refreshes the preview when the file changes on disk.
- Opens web links in your browser.
- Opens Markdown links in a new Markdown bei Nacht window.

## Install On Windows 11

1. Run `MarkdownBeiNacht-Setup.exe`.
2. If Windows asks for permission, allow the installer to continue.
3. Leave the default install folder unless you have a reason to change it.
4. If you want an icon on your desktop, check the desktop shortcut option during setup.
5. Finish the setup.

Some Windows 11 PCs already have Microsoft WebView2 installed. If your PC does not, setup may download and install it during setup.

## Open A Markdown File

You can open Markdown files in any of these ways:

- Double-click a Markdown file from Explorer after choosing Markdown bei Nacht in `Open with`.
- Start the app and choose `File > Open`.
- Drag a `.md`, `.markdown`, or `.mdown` file into the app window.

If the current window is empty, the file opens in that window.
If the current window already has a Markdown file open, the app opens the new file in a new window.
New windows open with a small cascade offset, so they do not land in the exact same spot. This also happens if you launch Markdown bei Nacht again while another window is already open.

## Use The Theme Setting

1. Open `View > Settings...`.
2. Choose a base color.
3. Click `Save`.
4. Reopen the app later to confirm the color stayed saved.

## Links, Images, And File Behavior

- Markdown links to other local Markdown files open in a new app window.
- `http` and `https` links open in your default web browser.
- Other local file links open with Windows.
- Local images can load if the image file exists.
- Remote images are blocked on purpose in this version.

## Find Help After Install

You can open the guide from `Help > User Guide` or by pressing `F1` inside Markdown bei Nacht.

The Start Menu includes:

- `Markdown bei Nacht`
- `Markdown bei Nacht User Guide`

The installed app folder also includes this `README.md` file.

## Uninstall

You can uninstall Markdown bei Nacht from `Settings > Apps > Installed apps` in Windows 11.

## Known Limits In v0.9.0

- The installer is not code signed yet.
- Remote images are blocked on purpose.
- ARM64 is not included yet.

