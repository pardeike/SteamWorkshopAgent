# Repository Instructions

## Local MCP Deployment

Follow the same local MCP install convention as the other active servers on this machine:

- Keep the installed binary at `~/.local/lib/steam-workshop-agent/SteamWorkshopAgent`.
- Keep the MCP config pointed at that stable installed binary path.
- Overwrite the installed binary in place during local redeploys.
- Do not switch the MCP config to repo-local `bin/` output or alternate temporary binary names.
- Do not create backup binaries such as `SteamWorkshopAgent.bak-*`.

Codex sessions load MCP servers at startup, so restart the session after changing the installed binary or MCP config.

