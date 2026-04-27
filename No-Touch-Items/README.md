# No Touch Items

Vintage Story **1.22+** mod — *Creative-first tooling* for controlled placement and “no break / no pick” protection on specific blocks and entities.

| | |
|-|-|
| **Mod id** | `notouchitems` |
| **Current version** | 0.1.31 (sync with `NoTouchConstants.ModVersion` in source) |
| **Game** | Vintage Story 1.22.0+ |
| **.NET** | 10+ (`net10.0` SDK) |

> **In-game name:** “No Touch Items” — this Git folder is named `No-Touch-Items` so paths and repo URLs are easy to type.

## What it does

- **Creative-oriented** item flows: totem-style use, right-clicks, and placement.
- **Protection:** blocks breaking and *BuildOrBreak* pickup for protected targets when the rules are active.
- **Commands** under `/nt` (e.g. `protect` / `noprotect` for future spawn rules); `/nt*` and related diagnostics.
- **Alt+P** to show id paths; join chat and `server-main.log` log the mod version (see `NoTouchConstants`).

`modinfo.json` has the one-line blurb; code lives under `src/`, assets under `assets/notouchitems/`.

## Requirements to build

1. **.NET SDK** that can build `net10.0` (e.g. current .NET 10).
2. A local **Vintage Story** install. Point MSBuild at the **game** folder (the one that contains `VintagestoryAPI.dll`), *not* only `VintagestoryData`.

Set the path in **`Directory.Build.props`**, or for one-off builds:

```text
dotnet build "NoTouchItems.csproj" -c Release -p:VintageStoryPath="C:\Path\To\Vintagestory"
```

On Windows you can also set environment variable **`VINTAGE_STORY_PATH`** to that folder before `dotnet build`.

## Build and deploy (Windows)

- Run **`Build No Touch Items.cmd`** in this folder, **or** `dotnet build "NoTouchItems.csproj" -c Release` from a shell.

After a successful build, the project copies **`notouchitems.dll`**, **`modinfo.json`**, and **`assets/`** to:

- This **project directory** (for a quick zip or manual copy), and  
- **`%APPDATA%\Roaming\VintagestoryData\Mods\notouchitems\`** (standard per-user data mods folder)

Do not rely on `Vintagestory\Mods\` for development installs; use the **VintagestoryData** `Mods` folder (or your portable data directory).

## Repository layout

```text
src/            C# (client/server systems, item classes, entity behavior, constants)
assets/         JSON, lang, itemtypes (domain: notouchitems)
modinfo.json
NoTouchItems.csproj
Directory.Build.props   ← set VintageStoryPath for your machine after clone
LICENSE
```

## License

[MIT](LICENSE) — you keep copyright; others can reuse with attribution in the license text.

## Author

adams (see `modinfo.json`).

## Contributing / issues

Issues and small PRs are welcome. Please match existing style; bump **`NoTouchConstants.ModVersion`** (and `modinfo.json` **version**) when you ship a build you expect others to run.
