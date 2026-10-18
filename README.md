<div align="center">

# Scenery Addons Browser

**A Windows desktop app to find, download and install Microsoft Flight Simulator scenery by airport — the project that grew into [SimFleet](https://github.com/Daniiel18/msfs-addons-browser).**

![.NET](https://img.shields.io/badge/.NET-8-512BD4?logo=dotnet&logoColor=white)
![WPF](https://img.shields.io/badge/WPF-desktop-0078D6?logo=windows&logoColor=white)
![C#](https://img.shields.io/badge/C%23-12-239120?logo=csharp&logoColor=white)
![Tauri](https://img.shields.io/badge/Tauri-2-24C8DB?logo=tauri&logoColor=white)
![Rust](https://img.shields.io/badge/Rust-backend-000000?logo=rust&logoColor=white)
![Releases](https://img.shields.io/github/v/release/Daniiel18/SceneryAddonsBrowser?label=release&color=2563eb)

</div>

> **Development continues in [SimFleet](https://github.com/Daniiel18/msfs-addons-browser)**, the all-in-one companion for MSFS 2020 & 2024 (addon browser, logbook, airline economy and more), with a [live demo](https://daniiel18.github.io/msfs-addons-browser/).

## ✨ What it does

- **Search by ICAO** — type an airport code and get the matching sceneries from SceneryAddons, with developer, version and links.
- **Download inside the app** — a built-in BitTorrent engine (MonoTorrent) with a queue, progress and resume, plus an embedded browser (WebView2) for web file hosts.
- **One-click install** — extracts RAR, 7z and ZIP archives (SharpCompress) straight into the simulator's **Community** folder, which it detects automatically.
- **GSX profiles** — finds the GSX Pro ground-services profile for the same airport on flightsim.to.
- **History and settings** — remembers downloads and installs, and keeps your folders and preferences.
- **Automatic updates** — releases are published on GitHub and installed in place with [Velopack](https://velopack.io).

## 🧭 Two generations in one repository

| Folder | Version | Stack |
| --- | --- | --- |
| `/` (root) | The original desktop app | C# · .NET 8 · WPF · MonoTorrent · SharpCompress · WebView2 · Velopack |
| `app/` | The rewrite that became SimFleet | Tauri 2 · Rust · React 18 · TypeScript · Tailwind CSS · MapLibre |

The Tauri rewrite moved to its own repository once it outgrew the original idea; the root project stays here as the first, WPF-based version.

## 🚀 Building from source

**Requirements:** Windows 10/11 and the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
git clone https://github.com/Daniiel18/SceneryAddonsBrowser.git
cd SceneryAddonsBrowser
dotnet run
```

For the Tauri version in `app/`, install [Rust](https://rustup.rs) and Node.js, then run `npm install` and `npm run tauri dev` inside `app/`.

## ⚖️ Disclaimer

Scenery Addons Browser is an independent tool and is not affiliated with Microsoft, Asobo Studio, SceneryAddons or any addon developer. Respect each addon's license and its author's terms.
