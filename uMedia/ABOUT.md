# u-media
a fork of "UWidgets" but its a media player!

# uMedia

A now-playing widget that sits on your Windows desktop.

It shows whatever is currently playing — cover art, title, artist, progress — with
play, pause and skip controls, in a small frameless window with a Mica backdrop. It
reads Windows' own System Media Transport Controls, the same source that drives the
media overlay on your volume keys, so it works with anything that publishes there:
Spotify, browsers, foobar2000, Plex, Even Feishin and so on.

Built with .NET 8 and [Avalonia](https://avaloniaui.net), following the structure and
look of [creewick/uWidgets](https://github.com/creewick/uWidgets).

## What it does

- Three layouts — Compact (2×2), Wide (4×2), Large (4×4) — that scale with the window
- Mica, Mica Alt, Acrylic or solid backdrop, with an adjustable tint over it
- Drag to move, drag the corner to resize, both snapping to a configurable grid
- A whitelist or blacklist of source apps, so browser tabs and call apps can be ignored
- Any installed font, light/dark/auto theme, adjustable corner radius
- Tray icon, optional always-on-top, optional start with Windows
- Position, size and every setting persist across restarts

Portable: two JSON files beside the executable, nothing in AppData, nothing in the
registry except the optional startup entry. Deleting the folder uninstalls it.

## Requirements

- Windows 10 1809 or later for the media controls
- Windows 11 22H2 or later for Mica — earlier versions fall back to acrylic
- .NET 8 runtime, or a self-contained build

---

## ⚠️ Disclaimer: this is *mostly* vibe coded

Most of this was written by Claude! I am one lazy bum and dont understand for SHIT and dont understand how to make widgets on windows (Anthropic)
