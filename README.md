# SteamWorkshopAgent

SteamWorkshopAgent is a local stdio MCP server for publishing RimWorld mod releases to Steam Workshop from an already-prepared GitHub release.

The server is aimed at the local mod-development workflow: build the mod in Release mode, stage the generated mod folder, create a SteamCMD VDF, and upload the Workshop update without opening RimWorld's mod UI.

## Documentation

- [README.md](README.md): user-facing overview and quick start
- [AGENTS.md](AGENTS.md): local MCP redeploy convention for this workspace
- [Architecture Enhancements](docs/ARCHITECTURE_ENHANCEMENTS.md): hybrid SteamCMD, Steamworks, and Workshop read-back architecture

## Highlights

- Inspect RimWorld mod metadata from a source repository or an already-deployed mod folder using `About/About.xml`, optional `About/PublishedFileId.txt`, `LoadFolders.xml`, and optional `Directory.Build.props`.
- Use the fixed RimWorld Steam app id `294100` and the mod Workshop id from `About/PublishedFileId.txt` when present.
- Create the initial private Steam Workshop item for a new local mod from either a source repository or an already-built mod folder, and write the returned id to `About/PublishedFileId.txt`.
- Read GitHub release notes through `gh release view`.
- Generate a dry-run Workshop release plan before uploading.
- Publish through SteamCMD `+workshop_build_item`.
- Submit RimWorld Workshop category tags such as `Mod` and `1.6` through the same local Steamworks UGC path RimWorld uses.
- Preserve the existing Workshop page description unless `updateDescription` is explicitly enabled.
- Read the current main Workshop page title and description through Steam's public item details API.
- Update the main Workshop page description directly with a metadata-only SteamCMD VDF, without rebuilding mod content or changing tags.
- Override the Steam Workshop release changenote at publish time when you want a concise hand-written note instead of the GitHub release body.
- Submit a changenote update for an already-published Workshop item through local Steamworks.

## Requirements

- .NET 10 SDK to build from source
- SteamCMD for Workshop uploads
- GitHub CLI `gh` for reading release notes
- A logged-on Steam desktop client session for local Steamworks tag updates
- A RimWorld mod source repository with a Release build target that supports `RIMWORLD_MOD_DIR` for release publishing
- For initial `new-mod` uploads, either a source repository or an already-built RimWorld mod folder with `About/About.xml`, `About/Preview.png`, and versioned `Assemblies` content

Install SteamCMD on this Mac with:

```sh
brew install --cask steamcmd
```

SteamWorkshopAgent does not store Steam passwords or Steam Guard codes. It passes only the Steam username to SteamCMD. Pass that username to the publish tool or set:

```sh
export STEAMCMD_USER=your_steam_username
```

When running as an MCP server, the client process may not inherit your terminal environment. If `steamUser` is omitted and `STEAMCMD_USER` is missing from the MCP process environment, the agent also probes your configured shell startup files and reads only that username value.

Authenticate SteamCMD once in a real terminal:

```sh
steamcmd +login your_steam_username +quit
```

Enter the password and Steam Guard token there. SteamCMD stores a reusable login token in Steam's config directory, not in this MCP server. On this Mac the relevant file is:

```text
~/Library/Application Support/Steam/config/config.vdf
```

Future automated runs should use only the username. If the token is deleted, invalidated, or Steam requires fresh verification, refresh the login manually in a terminal and then rerun the MCP publish.

Workshop category tags are not submitted through SteamCMD. The agent calls RimWorld's local Steamworks UGC path for tag updates, using RimWorld app id `294100` and the native Steam API library bundled with the installed RimWorld app. This requires the desktop Steam client session to be logged on from Steamworks' point of view. If Steamworks reports `NotLoggedOn`, the upload can still succeed through SteamCMD, but the returned tag update result will show the tag submission failure.

## Build And Test

```sh
dotnet build SteamWorkshopAgent.slnx -c Release
dotnet test SteamWorkshopAgent.slnx
```

## Local Install

This workspace follows the same local MCP install pattern as the active `gabs` and `decompiler` servers: publish a stable native binary under `~/.codex/mcp-servers/<server-dir>/` and point Codex at that installed binary.

```sh
make install
```

The install target writes:

```text
~/.codex/mcp-servers/steam-workshop-agent/SteamWorkshopAgent
```

Local redeploys should overwrite that binary in place. Do not point MCP config at repo-local build output.

## MCP Client Launch

Codex CLI:

```toml
[mcp_servers.steam-workshop-agent]
command = "/Users/you/.codex/mcp-servers/steam-workshop-agent/SteamWorkshopAgent"
args = ["server"]
```

Generic MCP client:

```json
{
  "command": "/Users/you/.codex/mcp-servers/steam-workshop-agent/SteamWorkshopAgent",
  "args": ["server"]
}
```

Restart the MCP client after changing the installed binary or MCP configuration. Codex loads MCP servers at session startup.

## Tools

`SteamStatus`

Checks whether SteamCMD is installed, whether RimWorld is installed through Steam, whether RimWorld's native Steam API library is available for tag updates, and where Steam Workshop logs are expected.

`RimWorldModInspect`

Reads a RimWorld mod source repository or already-deployed mod folder and returns the metadata needed for Workshop publishing.

`WorkshopReleasePlan`

Creates a dry-run plan for a GitHub release tag. It reads the GitHub release body, resolves mod metadata, prepares SteamCMD VDF content, and reports validation issues before upload.

Steam changenotes are formatted from the mod metadata and GitHub release body by default:

```text
v1.2.3

GitHub release body

GitHub release: https://github.com/owner/repo/releases/tag/v1.2.3.0
```

Pass `changeNote` to use custom Steam changenote text for the plan or publish. The agent still prefixes the version heading unless your text already starts with it.

Four-part mod versions with a trailing `.0` are shortened for the Steam heading only.

`WorkshopPublishRelease`

Publishes a confirmed release to Steam Workshop. After SteamCMD uploads the content, the tool submits the intended `Mod` plus supported RimWorld version tags through the local Steamworks tag updater using the same release changenote, so the separate tag submit does not replace the visible update note with generic tag text.

Source release builds always pass `BuildBridgeTools=false` so GitHub/Steam release artifacts contain only the main mod payload. Local validation builds can keep building companion BridgeTools by default.

`WorkshopPublishDeployedRelease`

Publishes an already-built deployed mod folder to Steam Workshop using the same GitHub-release changenote formatting as `WorkshopPublishRelease`. This path does not rebuild the mod and does not refresh Workshop tags. Use it when a separate Release build has already produced the exact deployed folder that should be uploaded.

`WorkshopCreateNewMod`

Creates a new Steam Workshop item for a local RimWorld mod through SteamCMD. The tool defaults to a dry run. With `confirm=true`, it builds source repositories into a temporary staging directory or uploads an already-built mod folder directly, creates a new private Workshop item by using `publishedfileid` `0`, reads the id SteamCMD writes back into the VDF, submits the `Mod` tag plus supported RimWorld version tags such as `1.6` through Steamworks, and saves the id to `About/PublishedFileId.txt`.

Source `new-mod` builds use the same release-build convention and pass `BuildBridgeTools=false`.

`WorkshopSetTags`

Sets category tags on an existing RimWorld Workshop item. Pass either a numeric Workshop id or a source/deployed mod path with `About/PublishedFileId.txt`. If `tags` is omitted for a mod path, the tool uses `Mod` plus the supported RimWorld versions from `About/About.xml`.

`WorkshopGetDescription`

Reads the current main Steam Workshop item title and description. Pass either a numeric Workshop id or a local mod path with `About/PublishedFileId.txt`. The result includes the raw editable description text, character count, title, update timestamp, visibility, tags returned by Steam, and the public Workshop URL.

`WorkshopUpdateDescription`

Updates the main Steam Workshop item description field. Pass either a numeric Workshop id or a local mod path with `About/PublishedFileId.txt`, plus the complete new description text. This path writes a small VDF containing `appid`, `publishedfileid`, and `description`; it intentionally omits `contentfolder`, `previewfile`, and tags so the upload is metadata-only. Title and changenote are optional and omitted by default.

`WorkshopSetChangeNote`

Submits a changenote update for an existing RimWorld Workshop item. Pass either a numeric Workshop id or a source/deployed mod path with `About/PublishedFileId.txt`, plus the changenote text and `confirm=true`.

This submits a new Steamworks item update with the supplied changenote. It does not edit old historical changenote entries that Steam has already recorded.

The publish path is intentionally conservative:

1. Refuse to publish from a dirty git worktree.
2. For release publishes, create a detached temporary Git worktree at the requested tag and build that source tree in Release mode into a temporary staging directory with `BuildBridgeTools=false`. This keeps the caller's checkout clean even when the mod build rewrites tracked local deploy artifacts.
3. Validate the staged mod content and preview image.
4. Write `workshop.vdf` and `plan.json` under `~/Library/Application Support/SteamWorkshopAgent/runs/...`.
5. Run SteamCMD with `+workshop_build_item`.
6. Submit category tags through the local Steamworks UGC updater with the same release changenote when SteamCMD succeeds.
7. Return SteamCMD output, tag-update result, and recent Steam log tails.

The release tag must exist locally so Git can create the detached worktree. If the agent reports that the tag cannot be resolved, run `git fetch --tags` in the mod repository and retry.

The publish tool defaults to dry-run behavior unless `confirm` is `true`.

## Local Smoke Tests

The installed binary also has a thin CLI over the same services used by the MCP tools:

```sh
~/.codex/mcp-servers/steam-workshop-agent/SteamWorkshopAgent status
~/.codex/mcp-servers/steam-workshop-agent/SteamWorkshopAgent inspect /path/to/mod/repo-or-folder
~/.codex/mcp-servers/steam-workshop-agent/SteamWorkshopAgent plan /path/to/mod/repo v1.2.3
~/.codex/mcp-servers/steam-workshop-agent/SteamWorkshopAgent plan /path/to/mod/repo v1.2.3 --changenote "Simple public release note"
~/.codex/mcp-servers/steam-workshop-agent/SteamWorkshopAgent publish /path/to/mod/repo v1.2.3 --confirm --steam-user your_steam_username
~/.codex/mcp-servers/steam-workshop-agent/SteamWorkshopAgent publish /path/to/mod/repo v1.2.3 --confirm --steam-user your_steam_username --changenote "Simple public release note"
~/.codex/mcp-servers/steam-workshop-agent/SteamWorkshopAgent publish-deployed /path/to/mod/repo v1.2.3 /path/to/deployed/mod --confirm --steam-user your_steam_username
~/.codex/mcp-servers/steam-workshop-agent/SteamWorkshopAgent new-mod /path/to/mod/repo-or-folder --confirm --steam-user your_steam_username
~/.codex/mcp-servers/steam-workshop-agent/SteamWorkshopAgent description-get /path/to/mod/repo-or-folder
~/.codex/mcp-servers/steam-workshop-agent/SteamWorkshopAgent description-get 928376710
~/.codex/mcp-servers/steam-workshop-agent/SteamWorkshopAgent description /path/to/mod/repo-or-folder description.txt --confirm --steam-user your_steam_username
~/.codex/mcp-servers/steam-workshop-agent/SteamWorkshopAgent description 928376710 description.txt --confirm --steam-user your_steam_username
~/.codex/mcp-servers/steam-workshop-agent/SteamWorkshopAgent set-tags /path/to/mod/repo-or-folder --confirm
~/.codex/mcp-servers/steam-workshop-agent/SteamWorkshopAgent set-tags 3727949765 Mod 1.6 --confirm
~/.codex/mcp-servers/steam-workshop-agent/SteamWorkshopAgent set-changenote 3727949765 --changenote "Simple public release note" --confirm
```

## Current Limits

The agent focuses on initial item creation, release publishing, single-item description updates, and category tag updates. Bulk Workshop description edits can be driven by a manifest or shell loop around the metadata-only `description` command.

The new-mod path creates the initial item with private visibility by default. Pass `--visibility public`, `friends`, or `unlisted` only when that is intentional.

SteamCMD is still used for content uploads. Category tags are submitted through the local Steamworks updater because SteamCMD accepts a `tags` block but does not reliably apply RimWorld category tags.

The generated update VDF preserves existing Workshop tags; the confirmed publish path applies the intended tags afterward with `ISteamUGC.SetItemTags`. If the local desktop Steamworks session reports `NotLoggedOn`, the agent returns that failure in `tagUpdate` and does not claim the tags were applied.

The tool only updates the Workshop description when `updateDescription` is explicitly `true`. By default it publishes the release changenote and leaves the long Workshop page description alone.
