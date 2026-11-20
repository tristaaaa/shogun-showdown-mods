#!/bin/zsh
# Builds Shogun Showdown mods and copies them into the game's BepInEx plugins folder.
#   ./build.sh                  builds every mod (CoinMultiplier, DevMode, Performance, WeaponCooldowns)
#   ./build.sh CoinMultiplier   builds just that one

export DOTNET_ROOT="/opt/homebrew/opt/dotnet/libexec"
MODS_DIR="${0:A:h}"
PLUGINS_DIR="$HOME/Library/Application Support/Steam/steamapps/common/Shogun Showdown/BepInEx/plugins"

if (( $# > 0 )); then
  mods=("$@")
else
  mods=(CoinMultiplier DevMode Performance WeaponCooldowns)
fi

for mod in $mods; do
  echo "Building $mod..."
  if ! dotnet build "$MODS_DIR/$mod" -c Release --nologo -v quiet; then
    echo "$mod failed to build, the game still has the previous version."
    exit 1
  fi
  cp "$MODS_DIR/$mod/bin/Release/netstandard2.1/$mod.dll" "$PLUGINS_DIR/"
  echo "$mod installed."
done

echo "Done. Restart the game to load the changes."
