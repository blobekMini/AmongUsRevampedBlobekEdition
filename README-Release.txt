Among Us Revamped Blobek Edition - Release v@VERSION@

INSTALL FROM EITHER ZIP (FULL BepInEx PACKAGE)
1. Close Among Us before installing.
2. Locate the Among Us game folder:
   - Steam: Library > right-click Among Us > Manage > Browse local files.
   - Itch.io: open the game's menu in the app and choose the option to open its install folder.
3. Extract the entire ZIP into the Among Us game folder. Do not extract it into a new subfolder.
4. If Windows asks, merge folders and replace the included files.
   The package contains BepInEx, winhttp.dll, doorstop_config.ini, and
   BepInEx/plugins/AmongUsRevamped.dll.
5. Launch Among Us normally. The mod version is shown in-game with the
   Among Us Revamped Blobek Edition label.

INSTALL THE DLL ONLY (BepInEx ALREADY INSTALLED)
Copy AmongUsRevamped.dll into the game's BepInEx/plugins folder, replacing any
older AmongUsRevamped.dll. Do not copy the DLL into the game root.

VERSION NUMBERING
The version format is x.x.x.y.z:
   x.x.x = the original mod version (@BASE_VERSION@)
   y     = the Blobek Edition fork version (read from BEversion.txt; this release: @BE_VERSION@)
   z     = the Blobek Edition build number (read from build-number.txt; this release: @BUILD_NUMBER@)

This release is therefore v@VERSION@. Normal runs of build.ps1 increment the
build number and write it back to build-number.txt. BEversion.txt tracks the
fork version separately. The complete five-part version is embedded in the
assembly's informational version and displayed in-game.

PACKAGES
- The main ZIP and Itch.io ZIP both include the full BepInEx runtime files and
  this installation guide.
- AmongUsRevamped.dll is also provided separately for existing BepInEx setups.