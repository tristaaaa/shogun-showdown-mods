# Shogun Showdown mods

BepInEx 5 plugins for Shogun Showdown (Unity 2021.3, Mono). Built and tested on macOS (soon to be testing on Win10/11 & Debian/Arch/Fedora Linux.)

| Mod | What it does | Settings (`BepInEx/config/`) |
|---|---|---|
| CoinMultiplier | Multiplies coins picked up (x1.0 to x3.0). Adds a slider to the pause menu. | `com.flargington.shogunshowdown.coinmultiplier.cfg` |
| DevMode | Turns on the game's built-in developer mode (hotkeys H, K, N, M, T, R, Q and the C panel). | `com.flargington.shogunshowdown.devmode.cfg` |
| Performance | Filters routine Unity logging, caps the frame rate (with VSync off) and turns off anti-aliasing. | `com.flargington.shogunshowdown.performance.cfg` |
| WeaponCooldowns | Per-weapon starting cooldowns and a lower maximum cooldown. | `com.flargington.shogunshowdown.weaponcooldowns.cfg` |

Each config file is created the first time the game starts with that mod installed. Edit it with the game closed; changes apply on the next launch.

## Requirements

- Shogun Showdown installed through Steam
- [.NET SDK](https://dotnet.microsoft.com/download) (`brew install dotnet` on macOS)
- [BepInEx 5](https://github.com/BepInEx/BepInEx/releases) for your platform, installed in the game folder

## Building

The projects compile against the game's DLLs and BepInEx's core DLLs:

- Game DLLs: read from the default macOS Steam install path (`GameManaged` in each `.csproj`)
- BepInEx DLLs: extract the BepInEx release into `downloads/bepinex/` next to this README (`BepInExCore` in each `.csproj`)

Then run:

```
./build.sh                  # build all mods and copy them into BepInEx/plugins
./build.sh WeaponCooldowns  # build one mod
```

Restart the game to load the new builds.
