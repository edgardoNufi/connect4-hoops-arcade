#!/usr/bin/env bash
# Cloudflare Pages build script for the Connect 4 Hoops Arcade (Blazor WebAssembly).
# Cloudflare's build image has no .NET SDK, so we install it here, then publish the static site.
# Output lands in ./output/wwwroot  →  set Cloudflare "Build output directory" to: output/wwwroot
set -euo pipefail

DOTNET_CHANNEL="10.0"            # matches the repo's target framework (net10.0)
export DOTNET_NOLOGO=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1   # avoid ICU dependency on minimal build images

echo "Installing .NET SDK (channel $DOTNET_CHANNEL)…"
curl -fsSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel "$DOTNET_CHANNEL" --install-dir "$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH"

echo ".NET version: $(dotnet --version)"

echo "Publishing Blazor WebAssembly app…"
dotnet publish src/Connect4HoopsArcade.Web/Connect4HoopsArcade.Web.csproj \
  -c Release \
  -o output

echo "Done. Static site is in output/wwwroot"
