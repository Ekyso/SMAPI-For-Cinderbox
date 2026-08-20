# SMAPI for Cinderbox

Android port of [SMAPI](https://github.com/Pathoschild/SMAPI) 4.5.2 for use with the [Cinderbox](https://github.com/Ekyso/Cinderbox) launcher.

> **Disclaimer**: This is an unofficial community project. Stardew Valley is developed and published by ConcernedApe. A legitimate copy of Stardew Valley is required. No game assets are included in this repository.

## Features

- **PC mod compatibility**  
  Runs upstream PC SMAPI on Android, reporting as Linux so desktop mods use correct code paths.
- **Android SMAPI lifecycle**
  Launches SMAPI inside Cinderbox and coordinates game-thread work with the Android host.
- **Async logging**  
  Background log queue to avoid blocking the game thread.
- **Content redirection**  
  Asset paths redirected to external storage. Raw file cache persists decoded PNG/JSON data across invalidation cycles.
- **Assembly resolution**  
  Rewrites Assembly.Location for APK-bundled assemblies. Cecil resolver stubs for metadata-only resolution.
- **Mobile API rewriting**
  Rewrites desktop mod API calls to explicit mobile facades without applying host-owned Harmony patches.

## Project Structure

```
src/SMAPI/
  Mobile/                          # Android-specific code
    AndroidGameLoopManager.cs      # Game loop callbacks and timing
    AndroidSModHooks.cs            # Main thread task scheduling
  Framework/
    SmapiAndroidLauncher.cs        # Entry point and narrow host contracts used by Cinderbox
    Content/RawFileCache.cs        # Decoded file cache across invalidations
    Logging/AsyncLogQueue.cs       # Background log processing
    ModLoading/Rewriters/          # Mod IL rewriters and mobile API facades
```

## License

[LGPL v3](LICENSE.txt), same as upstream SMAPI.

## Credits

- [Pathoschild](https://github.com/Pathoschild) for [SMAPI](https://github.com/Pathoschild/SMAPI)
- [ConcernedApe](https://www.stardewvalley.net/) for Stardew Valley
