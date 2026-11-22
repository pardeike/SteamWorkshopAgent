"""Quiet install of the MCP binary and the RimBridge companion, locally or on a remote Mac.

Usage: install.py [REMOTE_HOST]

Files are copied under a temporary name and renamed over the installed path.
Overwriting a running binary in place makes macOS kill every new launch (exit 137).
"""

from pathlib import Path
import json
import subprocess
import sys


root = Path(__file__).resolve().parent.parent
remote = sys.argv[1] if len(sys.argv) > 1 else None
log = root / "artifacts" / "logs" / ("install-" + (remote or "local") + ".log")
log.parent.mkdir(parents=True, exist_ok=True)
publish_dir = root / "artifacts" / "publish"
companion = root / "artifacts" / "BridgeTools" / "SteamWorkshopAgent" / "SteamWorkshopAgent.BridgeTools.dll"
install_dir = ".codex/mcp-servers/steam-workshop-agent"
bridge_dir = "Library/Application Support/Steam/steamapps/common/RimWorld/BridgeTools/SteamWorkshopAgent"
output = log.open("w")


def run(step, command, capture=False):
    output.write("$ " + " ".join(command) + "\n")
    output.flush()
    try:
        result = subprocess.run(command, cwd=root, stdout=subprocess.PIPE if capture else output,
                                stderr=output, text=True)
    except OSError as error:
        output.write(str(error) + "\n")
        result = None
    if result is None or result.returncode != 0:
        output.close()
        print(f"failed: {step}", file=sys.stderr)
        print("\n".join(log.read_text().splitlines()[-20:]), file=sys.stderr)
        print(f"Full log: {log}", file=sys.stderr)
        sys.exit(result.returncode if result and result.returncode > 0 else 1)
    if capture:
        output.write(result.stdout)
    return result.stdout if capture else None


def shell(step, script, source=None):
    command = ["ssh", remote, script] if remote else ["/bin/sh", "-c", script]
    if source is None:
        return run(step, command, capture=True)
    with open(source, "rb") as data:
        output.write("$ " + " ".join(command) + f" < {source}\n")
        output.flush()
        result = subprocess.run(command, cwd=root, stdin=data, stdout=output, stderr=output)
    if result.returncode != 0:
        output.close()
        print(f"failed: {step}", file=sys.stderr)
        print(f"Full log: {log}", file=sys.stderr)
        sys.exit(result.returncode)


def place(step, source, directory, name, mode):
    target = f"$HOME/{directory}"
    temp = f"{target}/.{name}.new"
    # The same script runs locally and over ssh. The file arrives on stdin.
    shell(step, f'mkdir -p "{target}" && cat > "{temp}" && chmod {mode} "{temp}" && mv -f "{temp}" "{target}/{name}"',
          source)


run("publish", ["dotnet", "publish", "src/SteamWorkshopAgent/SteamWorkshopAgent.csproj", "-c", "Release",
                "-r", "osx-arm64", "--self-contained", "true", "-p:PublishSingleFile=true",
                "-p:PublishTrimmed=false", "-o", str(publish_dir)])
run("companion build", ["dotnet", "build", "src/SteamWorkshopAgent.BridgeTools/SteamWorkshopAgent.BridgeTools.csproj",
                        "-c", "Release", "--nologo", "-v:minimal"])
if remote:
    shell("remote git pull", "cd ~/Projects/SteamWorkshopAgent && git pull --ff-only --quiet")
place("binary", publish_dir / "SteamWorkshopAgent", install_dir, "SteamWorkshopAgent", "755")
place("symbols", publish_dir / "SteamWorkshopAgent.pdb", install_dir, "SteamWorkshopAgent.pdb", "644")
place("companion", companion, bridge_dir, "SteamWorkshopAgent.BridgeTools.dll", "644")
status = json.loads(shell("verify", '"$HOME/.codex/mcp-servers/steam-workshop-agent/SteamWorkshopAgent" status'))
if status.get("status") != "ok" or not status["data"].get("steamworksNativeLibraryFound"):
    output.close()
    print("failed: verify (status not ok or Steamworks library missing)", file=sys.stderr)
    print(f"Full log: {log}", file=sys.stderr)
    sys.exit(1)
output.close()
print("ok")
