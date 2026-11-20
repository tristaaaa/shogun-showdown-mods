#!/usr/bin/env bash
# Builds the Shogun Showdown mods and installs them into the game's BepInEx/plugins folder (macOS and Linux).
# On Windows, run `dotnet build -c Release` in this folder instead; it does the same thing.
#   ./build.sh                  builds every mod
#   ./build.sh CoinMultiplier   builds just that one
# If the game is not in the default Steam library, set SHOGUN_SHOWDOWN_DIR to its folder first.

cd "$(dirname "$0")" || exit 1

# Homebrew's dotnet needs DOTNET_ROOT to find its runtime (Apple Silicon, then Intel prefix)
if [ -z "$DOTNET_ROOT" ]; then
  for dir in /opt/homebrew/opt/dotnet/libexec /usr/local/opt/dotnet/libexec; do
    if [ -d "$dir" ]; then
      export DOTNET_ROOT="$dir"
      break
    fi
  done
fi

if [ $# -eq 0 ]; then
  set -- ShogunShowdownMods.sln
fi

for target in "$@"; do
  if ! dotnet build "$target" -c Release --nologo -v minimal; then
    echo "Build failed. Mods that built were installed; any that failed keep their previous version in the game."
    exit 1
  fi
done

echo "Done. Restart the game to load the changes."
