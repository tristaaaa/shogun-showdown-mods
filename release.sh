#!/usr/bin/env bash
# Builds every mod and publishes the DLLs as a GitHub release (macOS and Linux; needs the GitHub CLI, gh).
#   ./release.sh 1.0.1 notes.md   tags the current commit v1.0.1 and uses notes.md as the release text
# Commit and push first: the release is made from the commit on GitHub, so its source download matches the DLLs.

set -euo pipefail
cd "$(dirname "$0")"

if [ $# -ne 2 ]; then
  echo "Usage: ./release.sh <version> <notes file>   (for example: ./release.sh 1.0.1 notes.md)"
  exit 1
fi
version="v${1#v}"
notes="$2"

if [ ! -f "$notes" ]; then
  echo "Notes file not found: $notes"
  exit 1
fi
if [ -n "$(git status --porcelain --untracked-files=no)" ]; then
  echo "There are uncommitted changes. Commit and push them first, so the release matches the code on GitHub."
  exit 1
fi
git fetch --quiet
if [ "$(git rev-parse HEAD)" != "$(git rev-parse '@{u}')" ]; then
  echo "This branch and GitHub are on different commits. Push (or pull) first, so the release matches the code on GitHub."
  exit 1
fi

./build.sh

# A folder inside the zip, so extracting it never drops files straight into wherever the zip was saved
name="ShogunShowdownMods-$version"
dist="dist/$version"
rm -rf "$dist"
mkdir -p "$dist/$name"
# Only the mod projects, the same ones Directory.Build.targets installs; any other project here is skipped
for project in $(grep -l '<IsShogunShowdownMod>true</IsShogunShowdownMod>' */*.csproj); do
  mod="$(basename "$project" .csproj)"
  cp "$mod"/bin/Release/*/"$mod.dll" "$dist/$name/"
done
(cd "$dist" && zip -qr "$name.zip" "$name")

gh release create "$version" --target "$(git rev-parse HEAD)" --title "$version" --notes-file "$notes" \
  "$dist/$name.zip" "$dist/$name"/*.dll
