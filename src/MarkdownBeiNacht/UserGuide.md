# Markdown bei Nacht

Markdown bei Nacht is a simple Windows app for reading local Markdown and `.txt` files in a clean dark theme.

## What This App Does

- Opens one document file per window.
- Lets you open Markdown files from Explorer `Open with`.
- Lets you open Markdown and `.txt` files from the app or by dragging them in.
- Remembers the last 8 Markdown and `.txt` files you opened in `File > Recent Files`.
- Refreshes the preview when the file changes on disk.
- Reloads the current document from `View > Reload`, `F5`, or `Ctrl+R`.
- Opens web links in your browser.
- Opens Markdown and `.txt` links in a new Markdown bei Nacht window.

## Install On Windows 11

1. Run `MarkdownBeiNacht-Setup.exe`.
2. If Windows asks for permission, allow the installer to continue.
3. Leave the default install folder unless you have a reason to change it.
4. If you want an icon on your desktop, check the desktop shortcut option during setup.
5. Finish the setup.

Release installers are code signed, but brand-new downloads can still show Windows SmartScreen reputation prompts until the app builds reputation.

Some Windows 11 PCs already have Microsoft WebView2 installed. If your PC does not, setup may download and install it during setup.

## Open A Document File

You can open Markdown files from Explorer `Open with`:

- In Explorer, use `Open with` for `.md`, `.markdown`, or `.mdown` files after choosing Markdown bei Nacht.

You can open Markdown or `.txt` files from inside the app:

- Start the app and choose `File > Open`.
- Drag a `.md`, `.markdown`, `.mdown`, or `.txt` file into the app window.

If the current window is empty, the file opens in that window.
If the current window already has a document open, the app opens the new file in a new window.
You can also reopen one of the last 8 Markdown or `.txt` files from File > Recent Files.
New windows open with a small cascade offset, so they do not land in the exact same spot. This also happens if you launch Markdown bei Nacht again while another window is already open.

## Use The Theme Setting

1. Open `View > Settings...`.
2. Choose a base color.
3. Click `Save`.
4. Reopen the app later to confirm the color stayed saved.

## Links, Images, And File Behavior

- Markdown links to other local Markdown or `.txt` files open in a new app window.
- `http` and `https` links open in your default web browser.
- Other local file links open with Windows.
- Local images can load if the image file exists.
- Remote images and safe audio/video media embeds can load in the preview.

## Find Help After Install

You can open the guide from `Help > User Guide` or by pressing `F1` inside Markdown bei Nacht.

The Start Menu includes:

- `Markdown bei Nacht`
- `Markdown bei Nacht User Guide`

The installed app folder also includes this `README.md` file.

## Uninstall

You can uninstall Markdown bei Nacht from `Settings > Apps > Installed apps` in Windows 11.

## Known Limits In v1.1.1

- Brand-new signed downloads can still trigger SmartScreen reputation prompts until the app builds reputation.
- ARM64 is not included yet.

