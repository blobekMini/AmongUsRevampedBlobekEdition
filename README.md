<h1 align="center">Among Us Revamped Blobek Edition</h1>
<p align="center">Unofficial modified edition based on <a href="https://github.com/ApeMV/AmongUsRevamped">ApeMV's AmongUsRevamped</a>.</p>
<br>

<p align="center">
  <a href="https://discord.gg/83Zhzhyhya" title="Upstream community Discord" target="_blank">
    <img src="https://img.shields.io/badge/Discord%20-%231DA1F2.svg?&style=for-the-badge&logo=discord&logoColor=white&color=5662f6"/>
  </a>
  <a href="https://ko-fi.com/apemv" title="Support the upstream ApeMV project">
    <img src="https://img.shields.io/badge/Support%20upstream%20on-Ko--fi-ff5f5f?style=for-the-badge&logo=ko-fi&logoColor=white">
  </a>
  <a href="https://github.com/blobekMini/AmongUsRevampedBlobekEdition/releases/latest">
    <img src="https://img.shields.io/badge/Download-Latest%20Release-2878c8?style=for-the-badge">
  </a>
  <a href="https://github.com/blobekMini/AmongUsRevampedBlobekEdition">
    <img src="https://img.shields.io/badge/GitHub-Blobek%20Edition-181717?style=for-the-badge&logo=github">
  </a>
</p>
<p align="center">Discord: <b>blobekmini</b></p>

<p align="center">
  <strong>Upstream ApeMV release-download statistics:</strong><br>
  <a href="https://github.com/ApeMV/AmongUsRevamped/releases">
    <img src="https://img.shields.io/github/downloads/ApeMV/AmongUsRevamped/total?style=for-the-badge&color=red"/>
  </a>
  <a href="https://github.com/ApeMV/AmongUsRevamped/releases/latest">
    <img src="https://img.shields.io/github/downloads/ApeMV/AmongUsRevamped/latest/total?style=for-the-badge"/>
  </a>
</p>

# 💡 How To Install:
- <b>Download the latest Blobek Edition release:</b> <a href="https://github.com/blobekMini/AmongUsRevampedBlobekEdition/releases/latest">GitHub Releases</a>
- <b>If you have BepInEx, download the DLL from the latest release and move it into Among Us → BepinEx → Plugins</b>
- <b> If you don't have BepInEx:</b>
- Download either full package ZIP from the latest release above
- Extract the contents of the zip into your Among Us folder
- Paste the copied files directly into your Among Us game folder
- Shown below are the folder locations:
   - **Steam:** Right-click Among Us in your `Library` → `Manage` → `Browse local files`
   - **Epic Launcher:** Right-click Among Us in your Library → `Manage` → Click the folder icon in the Installation box
   - **Itch.io:** Open the Itch.io app → Right-click Among Us in your Library → `Manage` → `Open folder in Explorer`
   - **Xbox App:** Open the Xbox app → Right-click Among Us in your library → `Manage` → `Files` → `Browse...`
   - **Microsoft Store:** Check [this support article](https://answers.microsoft.com/en-us/xbox/forum/all/where-can-i-find-the-gamefiles-of-a-game/5cb9a0c3-7948-4316-abc5-f27d1767b932) on how to find and access your Among Us folder.
 - Your game folder should look similar to this after installation:
<img src="https://github.com/astra1dev/AUnlocker/assets/90265231/14226f03-a003-4efc-b27b-6df53fb394d6" width=410 height=240>****
‎ 

# 🎮 Features (v2.3.0):

## <b>♠️ Client Side settings:</b>
- Toggle Game Master (Spectator)
- Toggle Show FPS
- Toggle Dark Theme
- Toggle Lobby Music
- Toggle Info When Dead
- Toggle Command Helper
- Toggle Chat Modifications
- Toggle Lobby Info
- Toggle See Ghosts (including ghost chat messages)
## <b>⚙️ Technical settings:</b>
- Advanced AFK Detection system
- Kick/Ban players under/above a certain level
- Kick/Ban invalid (Empty) Friendcodes
- Kick/Ban players who say "start"
- Optional cosmetic rainbow cycle when usernames or messages contain Turkish characters or Among Us-related Turkish terms (Ç/Ğ/Ö/Ü, either case)
- “merhaba” triggers on usernames and chat; “apollo” is checked in usernames only. The host and VIP/moderator/admin ranks are immune to the cosmetic effect.
- Banlist system
- Denyword and Denyname system
- Advanced Moderator system
- To add Admins/Moderators/VIP's, type the user's Friendcode in the designated files located in AUR-DATA
- Enable No Game End
- Custom starting countdown
## <b>♻️ Automation settings:</b>
- Full Autohost and Rehost support
- Automatically rejoin lobby
- Automatically send winner info
- Automatically Send Map Votes
- Enable (configurable) Welcome Messages from Templates.txt
- Optional tracked-player join alerts from AUR-DATA/notifypresence.txt
- Optional returning-player alerts for players who have joined one of your lobbies before
- Optional automatic Cosmicube switching, controlled by the local toggle in Pause menu → AUR Options
- Player-history sections include UTC date and room code; entries are retained for 14 days
- Join alerts appear in a wrapped popup with a close button
- Returning-player history is committed only after a round ends; previous-player alerts are shown when matching players join
- Host-local text binds can silently run supported private commands with `/bind`
## <b>🔧 Gameplay Settings:</b>
- Change sabotage cooldown and durations
- Dead impostors can sabotage
- Disable critical sabotages (individual)
- Disable door sabotages
- Disable meeting calls first 30s
- Disable devices (per map, individual)
- Override decontamination time (per map)
- Disable spore triggers on The Fungle
- % Tasks completed for Crewmates to win
- Enable/Disable any task
- All players have same tasks
- Shield player who died first
- Hide and Seek: Custom Seeker count
## <b>🏆 Custom Gamemodes:</b>
- 0Kcd -> Set Kill Cooldown to 0.0
- Shift and Seek
- Poof and Seek
- Speedrun
## <b>👾 Custom Anticheat:</b>
- Detect invalid sabotages
- Detect malformed RPC's
- Detect invalid venting
- Detect invalid animations
- Detect invalid task completions
- Detect invalid kills
- Detect immortality cheats
- Block attempted votekicks
## <b>❓ Other Improvements:</b>
- Language support: English, Italian, Japanese, Russian, Simplified Chinese, Spanish
- Select a language in Language/YourLanguage.txt
- Preset system for mod settings
- Creatable and customizable Templates
- Eht Dleks (Reverse Skeld) Map
- Ability to cancel starting countdown
- Improved main menu
- Main menu links to the Blobek Edition website, repository, and latest release; the mod name and version sit below these buttons
- Faster startup time
- Checks the latest GitHub release at startup and offers a 30-day reminder snooze
- Friend organizer in the Among Us friends menu: local nicknames and folders
- Zoom out in lobby or when dead
- Higher and more accurate Option ranges
- Always visible lobby timer
- Access experimental animation test scene
- Faster chat cooldown (2.2s)
- 120 character chat limit, copy pasting
- The local lobby host is excluded from this mod's banlist, denied-name/word, and start-word penalties, and mod-issued kick/ban requests targeting the host are blocked. This does not disable or bypass official or third-party lobby enforcement, or prevent server/network disconnects.
- Player target inputs accept ASCII letters/digits, @all, @me, !color:<color>, and // name wildcards
- /help and long command output are paged; host command output sends up to 3 messages per batch (up to 108 body characters each) 0.1 seconds apart, followed by a 5-second cooldown. Use /page <number> <command> [arguments] for additional pages.
- F10 opens the host-only in-game command console; it currently does not work as intended, so use chat commands when possible.
- Remote command replies are suppressed from lobby chat to prevent spam
- <b>🎉 And this all works in public lobbies!</b>

### Hotkeys
| HotKey              | Function                                    | Usable Scene                       |
| ------------------- | ------------------------------------------- | ---------------------------------- |
| `Shift`+`L`+`Enter` | Force End Game                              | In Game                            |
| `Shift`+`M`+`Enter` | Skip meeting when active                    | In Game                            |
| `C`                 | Cancel game start                           | In Starting Countdown              |
| `Shift`             | Start the game immediately                  | In Starting Countdown              |
| `Shift`             | 5x Option increment                         | In Options Menu                    |
| `Ctrl`              | Noclip in lobby                             | In Lobby                           |
| `Ctrl`              | 10x Option increment                        | In Options Menu                    |

### Chat Commands
| Command                                     | Function                                          |
| ------------------------------------------- | ------------------------------------------------- |
| /help                                       | Show command help                                 |
| /page {number} {command} [arguments]        | Show a saved page of command output               |
| /l<br>/lastgame                             | Send last game info                               |
| /r                                          | Send current gamemode/roles description           |
| /0kc<br>/sns</br>/sp                        | Send respective gamemode description              |
| /ban {selector}                             | Ban all players matching a name/selector           |
| /kick {selector}                            | Kick all players matching a name/selector          |
| /cban {color}                               | Ban every player using that color                 |
| /ckick {color}                              | Kick every player using that color                |
| /dump                                       | Copy your LogOutput file                          |
| /eg<br>/endgame                             | Instantly end an ongoing game                     |
| /em<br>/endmeeting                          | Post a brief status message, then end the meeting; no individual vote details are posted |
| /sm<br>/skip<br>/skipmeeting                | End the meeting without a vote summary             |
| /aur<br>/socials                            | Send the Blobek site, GitHub, and Discord first, then original-author/source attribution |
| /col<br>/color                              | Set color. Can be used to bypass color limit      | 
| /copyfit {source} {target1,target2,...}     | Currently broken; may kick the targeted player(s) from the lobby. Do not use. |
| /give {role} {player}                      | Currently broken; may kick the targeted player(s) from the lobby. Do not use. |
| /kill {player}                              | Kill a player (host permission required)          |
| /setimp {0-15}                              | Set next round's total impostors; keeps pre-set roles and randomly rolls remaining eligible players |
| /setvotes {player/!color:black/@all/@me} {number} | Set how many votes selected living player(s) get for the current meeting (0–100; host only) |
| /tp {player} !room:{security/cafeteria/o2/navigation}<br>/teleport | **Broken/untested:** may not work correctly. Do not rely on it. |
| /impregnate {player1} {player2} {player3} | **Broken/untested:** may not work correctly. Do not rely on it. |
| /say {message}                              | Send a message through the normal game chat pipeline and its filters |
| /revive [player\|@all]                      | Unreliable and does not consistently work as intended; use with caution. |
| /settings help                              | Show supported normal-mode settings and value ranges; changes sync in lobby or during a game |
| /reload                                     | Reset the *Language*.JSON file to default         |
| /s<br>/start                                | Start game countdown                              |
| /vip<br>/moderator<br>/admin                | Add a user to respective list by name             |
| /t {template}                               | Send a Template from Templates.txt                |
| /seeker {name}                              | Manually adds someone as Seeker in HnS            |
| /seekers                                    | Shows manual Seeker list in HnS                   |
| /clearseekers                               | Clears manual Seeker list in HnS                  |
| /mapvote                                    | Starts a vote for the map. Default maps only      |
| /allmapvote                                 | Starts a vote for the map. Includes Eht Dleks     |
| /gmvote                                     | Starts a vote for the gamemode                    |
| /private                                    | Sets lobby to private                             |
| /public                                     | Sets lobby to public                              |
‎

The browser-based local command panel has been removed. The separate F10 in-game console remains available for host-only command entry.

Player selectors accept `@all`, `@me`, `!color:blue`, or an exact ASCII-alphanumeric name. Use `//` as a wildcard: `/ban A//` matches names starting with A, while `/ban //67//` matches names containing 67. Group selectors and wildcards may affect multiple players; commands that require one target, such as `/voteout`, reject multiple matches.

`!color:gay` and `!color:rainbow` select players whose rainbow color cycle is currently active. Ordinary color-based `/kick`, `/ban`, `/ckick`, and `/cban` selectors ignore active rainbow-cycle players so the current cycle color does not cause an accidental match. Role assignment accepts common short names such as `sci`, `eng`, `ss`, `det`, `imp`, and `crew`.

`/tp PlayerName !room:security` (also `/teleport`) moves one player to the selected room's center during a game. `security`, `cafeteria`, `o2`, and `navigation` are available only on maps that contain those rooms. The command uses the `/kill` access-level setting.
`/tp` and `/teleport` are currently marked broken/untested; do not rely on them.

`/impregnate PlayerOne PlayerTwo PlayerThree` is a host-only 10-second movement sequence. It sends player2 to player1's starting position, freezes those two, repeatedly snaps player1 between their original positions, then puts player3 at the midpoint and restores their previous movement states. Quote names containing spaces. It shares the `/kill` access-level setting and is experimental.
`/impregnate` is currently marked broken/untested; do not rely on it.

In **Moderation Settings → Denied Words**, `Auto-rainbow on Turkish text (cosmetic)` is disabled by default. When enabled by the host, a player's Turkish-character username or a chat message containing `Ç/Ğ/Ö/Ü` (lowercase or uppercase) or a curated Among Us-related Turkish term can start the same rainbow cycle as `/ucolor gay` for that player. `merhaba` is checked in both usernames and chat messages; `apollo` is checked only in usernames. The host and VIP/moderator/admin ranks are exempt. For other players, it triggers at most once per player per lobby and is cosmetic only; it does not kick or ban anyone. The term list includes words such as `mürettebat`, `görev`, `şüpheli`, `havalandırma`, `toplantı`, and `güvenlik kamerası`.

During a meeting, the host can use `/setvotes <player|!color:black|@all|@me> <0-100>` to set the number of votes a living player receives when they cast their vote. Settings remain in effect until that meeting ends; the default is one vote.

`/em` and `/endmeeting` end the meeting after posting only a short status message; they no longer post individual vote details. `/say <message>` sends through the game's normal chat pipeline, so its usual filters still apply. In the Pause menu → **AUR Options**, `Automatically switch Cosmicubes when fully funded` switches away from the active cube once its remaining unlock costs can be covered by its pod balance. It chooses the unfinished cube with the lowest completion ratio, calculated from the pod cost of owned unlocks divided by total unlock cost. If all unlocked Cosmicubes are complete, it deactivates the current one. This is a local client preference and is not shared with the lobby.

`/copyfit` and `/give` are currently broken and may kick the targeted player(s) from the lobby; do not use them until fixed. `/revive` also does not consistently work as intended, so treat it as unreliable. Its outcome may vary by game state and role; do not rely on it to fully restore a player.

Use `/page <number> <command> [arguments]` to read additional pages of long command output. Public responses to commands issued by other players are suppressed to avoid lobby spam; their results remain available in the host's local console and logs.

`/settings <setting> <value>` can change and sync supported normal-mode options while a match is running. Hide and Seek is not supported by this command. Commands that inherently require a specific game state (for example, `/voteout` during a meeting, `/revive` during a game, or `/start` in a lobby) still enforce that state rather than attempting an invalid action everywhere.

`/setimp <0-15>` (also `/impcount`) sets the desired impostor-team size for the next round. Roles previously queued with `/set` or `/add` are checked first; pre-set impostors stay assigned, while the remaining eligible players are selected randomly at game start. If pre-set impostors exceed the requested count, those assignments are preserved and the total rises to include them.

Under **System Settings → Automation**, `Notify When Players From notifypresence.txt Join` controls alerts for entries in `AUR-DATA/notifypresence.txt`, while `Notify When Previous Players Return` controls alerts for players who have joined any of your lobbies before. Both use a wrapped, dismissible popup. Lobby membership is recorded when a player joins (not just when they complete a round) in `AUR-DATA/LobbyPlayerHistory.txt`, with a UTC date and room code per lobby section; sections older than 14 days are pruned.

Returning-player notifications are generated as players join a lobby. Anyone who was present in a previous lobby can be recognized on a later visit even if they did not play or complete a round; AUR does not emit a bulk “everyone from the previous lobby” alert.

### Experimental / Not Fully Tested
These recently added features have not been thoroughly tested in live games. They may be buggy, behave unexpectedly, or fail to work in some versions or game states:
- `/tp` and `/teleport` room targeting and player movement.
- `/impregnate` player freezing and timed movement sequence.
- Automatic Cosmicube switching and deactivation when all unlocked cubes are complete.
- Local friend nicknames and folders in the Among Us social menu.
- Startup GitHub release checks and the update reminder popup.
- Returning-player notifications based on `AUR-DATA/LobbyPlayerHistory.txt` and tracked-player notifications from `AUR-DATA/notifypresence.txt`.

Use `/bind '<command template>' '<trigger template>' [;:prefix;]` to create a persistent text bind, `/bind list` to view binds, or `/bind remove '<trigger template>'` to delete one. Example: `/bind 'ucolor gay ;' 'make ; gay' ;:!color:;` makes the host's local `make green gay` input appear in chat while executing `/ucolor gay !color:green`. Start a trigger with `#` (for example, `#make ; gay`) to execute it without showing the trigger text in chat. The `;` in the trigger captures one ASCII-alphanumeric word and substitutes it into the command; the optional `;:prefix;` transform adds a prefix to that value. Binds are host-local, do not require `/` or `!` when triggered, and are saved in `AUR-DATA/CommandBindings.txt`. Only commands supported by the private command dispatcher can be bound.

**Credit to these mods and their developers for their code:**
### :star: [AUnlocker](https://github.com/astra1dev/AUnlocker)
> - Original NumberOptionsPatch
> - Always Show Lobby Timer
> - Copy Pasting & Character Increase
### :star: [EHR (Formerly TOHE+)](https://github.com/Gurge44/EndlessHostRoles)
> - Task Patching
### :star: [TOH](https://github.com/tukasa0001/TownOfHost) :
> - OptionHolder & ClientOptionItem
### :star: [Super New Roles](https://github.com/ykundesu/SuperNewRoles) (SNR):
> - Credentials menu
### :star: [TOHY](https://github.com/Yumenopai/TownOfHost_Y) :
> - Zoom
### :star: [MoreGamemodes](https://github.com/Rabek009/MoreGamemodes)
> - Some Chat Patches
> - OptionItem System
---
## Blobek Edition and legal information

This is an unofficial modified edition of [ApeMV/AmongUsRevamped](https://github.com/ApeMV/AmongUsRevamped), branded as **Among Us Revamped Blobek Edition**. The upstream project and its notices are credited below. This edition does not claim to be made by, affiliated with, or endorsed by ApeMV or Innersloth LLC.

The included [LICENSE](LICENSE) is the GNU General Public License, version 3. Keep that license text and upstream copyright/attribution notices intact when redistributing. GPLv3 does not require a non-profit release and permits charging for copies, but distributing a modified version carries conditions, including providing recipients the corresponding source under GPLv3 and clearly marking the modified version. See [NOTICE.md](NOTICE.md) for a summary; the actual license controls.

GPLv3 does not grant rights to Among Us, Innersloth trademarks, game assets, or other third-party materials. Do not include proprietary game files or imply official endorsement. This information is not legal advice and cannot guarantee that a release will avoid a copyright or trademark complaint; get qualified legal advice or permission for any third-party materials or branding you plan to distribute.

For this modified edition, download the latest release at <https://github.com/blobekMini/AmongUsRevampedBlobekEdition/releases/latest> or visit <https://blobek.net/aurbe>. Discord username: `blobekmini`. The `/aur` and `/socials` commands send these first, then a second message beginning with the original-author attribution.

## AI assistance notice

AI-assisted tools were used to help draft and modify some Blobek Edition code and documentation. AI-generated changes can contain mistakes; the Blobek maintainer remains responsible for review, testing, licensing compliance, and distribution.
---

<b>This mod is not affiliated with Among Us or Innersloth LLC, and the content contained therein is not endorsed or otherwise sponsored by Innersloth LLC. Portions of the materials contained herein are property of Innersloth LLC. © Innersloth LLC.</b>
