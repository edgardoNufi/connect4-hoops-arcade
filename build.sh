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

# Stamp the build version BEFORE publish, into the SOURCE index.html. The service worker precaches
# index.html with an integrity hash generated during publish; if we stamped the PUBLISHED copy
# afterwards (as we used to), its bytes would no longer match that hash and the SW install would fail
# (cache.addAll rejects atomically) — no offline, no "ready" toast. Stamping first keeps them in sync.
echo "Stamping build version (pre-publish)…"
# Cloudflare Pages exposes CF_PAGES_COMMIT_SHA; fall back to git, then to "local".
VER_SHA="${CF_PAGES_COMMIT_SHA:-$(git rev-parse --short HEAD 2>/dev/null || echo local)}"
VER="${VER_SHA:0:7} · $(date -u +'%Y-%m-%d %H:%M') UTC"
SRC_INDEX="src/Connect4HoopsArcade.Web/wwwroot/index.html"
sed -i.bak "s|__BUILD_VERSION__|${VER}|" "$SRC_INDEX" && rm -f "${SRC_INDEX}.bak"
echo "Build version: ${VER}"

echo "Publishing Blazor WebAssembly app…"
dotnet publish src/Connect4HoopsArcade.Web/Connect4HoopsArcade.Web.csproj \
  -c Release \
  -o output

echo "Done. Static site is in output/wwwroot"
