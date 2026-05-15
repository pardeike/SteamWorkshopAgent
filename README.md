# SteamWorkshopAgent

SteamWorkshopAgent is a local stdio MCP server for publishing RimWorld mod releases to Steam Workshop from an already-prepared GitHub release.

The server is aimed at the local mod-development workflow: build the mod in Release mode, stage the generated mod folder, create a SteamCMD VDF, and upload the Workshop update without opening RimWorld's mod UI.

## Documentation

- [README.md](README.md): user-facing overview and quick start
- [AGENTS.md](AGENTS.md): local MCP redeploy convention for this workspace
- [Architecture Enhancements](docs/ARCHITECTURE_ENHANCEMENTS.md): future SteamCMD, Steamworks helper, and Workshop read-back architecture

## Highlights

- Inspect RimWorld mod metadata from `About/About.xml`, `About/PublishedFileId.txt`, `LoadFolders.xml`, and `Directory.Build.props`.
- Use the fixed RimWorld Steam app id `294100` and the mod Workshop id from `About/PublishedFileId.txt`.
- Read GitHub release notes through `gh release view`.
- Generate a dry-run Workshop release plan before uploading.
- Publish through SteamCMD `+workshop_build_item`.
- Preserve the existing Workshop page description unless `updateDescription` is explicitly enabled.

## Requirements

- .NET 10 SDK to build from source
- SteamCMD for Workshop uploads
- GitHub CLI `gh` for reading release notes
- A RimWorld mod repository with a Release build target that supports `RIMWORLD_MOD_DIR`

Install SteamCMD on this Mac with:

```sh
brew install --cask steamcmd
```

SteamWorkshopAgent does not store Steam passwords or Steam Guard codes. It passes only the Steam username to SteamCMD. Pass that username to the publish tool or set:

```sh
export STEAMCMD_USER=your_steam_username
```

Authenticate SteamCMD once in a real terminal:

```sh
steamcmd +login your_steam_username +quit
```

Enter the password and Steam Guard token there. SteamCMD stores a reusable login token in Steam's config directory, not in this MCP server. On this Mac the relevant file is:

```text
~/Library/Application Support/Steam/config/config.vdf
```

Future automated runs should use only the username. If the token is deleted, invalidated, or Steam requires fresh verification, refresh the login manually in a terminal and then rerun the MCP publish.

## Build And Test

```sh
dotnet build SteamWorkshopAgent.slnx -c Release
dotnet test SteamWorkshopAgent.slnx
```

## Local Install

This workspace follows the same local MCP install pattern as the active `gabs` and `decompiler` servers: publish a stable native binary under `~/.local/lib/<server-dir>/` and point Codex at that installed binary.

```sh
make install
```

The install target writes:

```text
~/.local/lib/steam-workshop-agent/SteamWorkshopAgent
```

Local redeploys should overwrite that binary in place. Do not point MCP config at repo-local build output.

## MCP Client Launch

Codex CLI:

```toml
[mcp_servers.steam-workshop-agent]
command = "/Users/you/.local/lib/steam-workshop-agent/SteamWorkshopAgent"
args = ["server"]
```

Generic MCP client:

```json
{
  "command": "/Users/you/.local/lib/steam-workshop-agent/SteamWorkshopAgent",
  "args": ["server"]
}
```

Restart the MCP client after changing the installed binary or MCP configuration. Codex loads MCP servers at session startup.

## Tools

`SteamStatus`

Checks whether SteamCMD is installed, whether RimWorld is installed through Steam, and where Steam Workshop logs are expected.

`RimWorldModInspect`

Reads a RimWorld mod repository and returns the metadata needed for Workshop publishing.

`WorkshopReleasePlan`

Creates a dry-run plan for a GitHub release tag. It reads the GitHub release body, resolves mod metadata, prepares SteamCMD VDF content, and reports validation issues before upload.

`WorkshopPublishRelease`

Publishes a confirmed release to Steam Workshop.

The publish path is intentionally conservative:

1. Refuse to publish from a dirty git worktree.
2. Build the mod in Release mode into a temporary staging directory.
3. Validate the staged mod content and preview image.
4. Write `workshop.vdf` and `plan.json` under `~/Library/Application Support/SteamWorkshopAgent/runs/...`.
5. Run SteamCMD with `+workshop_build_item`.
6. Return SteamCMD output and recent Steam log tails.

The publish tool defaults to dry-run behavior unless `confirm` is `true`.

## Local Smoke Tests

The installed binary also has a thin CLI over the same services used by the MCP tools:

```sh
~/.local/lib/steam-workshop-agent/SteamWorkshopAgent status
~/.local/lib/steam-workshop-agent/SteamWorkshopAgent inspect /path/to/mod/repo
~/.local/lib/steam-workshop-agent/SteamWorkshopAgent plan /path/to/mod/repo v1.2.3
~/.local/lib/steam-workshop-agent/SteamWorkshopAgent publish /path/to/mod/repo v1.2.3 --confirm --steam-user your_steam_username
```

## Current Limits

V1 focuses on release publishing only. It does not automate bulk Workshop description edits yet.

The generated VDF preserves existing Workshop tags by default. RimWorld's in-game uploader has internal tag handling, but SteamCMD tag syntax is easy to get wrong and a mistaken tag update can silently replace existing Workshop metadata.

The tool only updates the Workshop description when `updateDescription` is explicitly `true`. By default it publishes the release changenote and leaves the long Workshop page description alone.
