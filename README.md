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
  <a href="https://blobek.net/aurbe">
    <img src="https://img.shields.io/badge/Blobek%20Edition-blobek.net%2Faurbe-2878c8?style=for-the-badge">
  </a>
  <a href="https://github.com/blobek/AmongUsRevampedBlobekEdition">
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
- <b>Blobek Edition page:</b> <a href="https://blobek.net/aurbe">blobek.net/aurbe</a>
- <b>If you have BepInEx, obtain the Blobek Edition DLL from its page and move it into Among Us → BepinEx → Plugins</b>
- <b> If you don't have BepInEx:</b>
- Download the Blobek Edition package from the page above
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
- Optional returning-player alerts for players who previously completed a round with your host
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
- Faster startup time
- Zoom out in lobby or when dead
- Higher and more accurate Option ranges
- Always visible lobby timer
- Access experimental animation test scene
- Faster chat cooldown (2.2s)
- 120 character chat limit, copy pasting
- The local lobby host is excluded from this mod's banlist, denied-name/word, and start-word penalties, and mod-issued kick/ban requests targeting the host are blocked. This does not disable or bypass official or third-party lobby enforcement, or prevent server/network disconnects.
- Player target inputs accept ASCII letters/digits, @all, @me, !color:<color>, and // name wildcards
- /help and long command output are paged; use /page <number> <command> [arguments]
- F10 opens the host-only in-game command console
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
| /em<br>/endmeeting                          | Summarize votes in chat, then end the meeting      |
| /sm<br>/skip<br>/skipmeeting                | End the meeting without a vote summary             |
| /aur<br>/socials                            | Send the Blobek site, GitHub, and Discord first, then original-author/source attribution |
| /col<br>/color                              | Set color. Can be used to bypass color limit      | 
| /copyfit {source} {target1,target2,...}     | Copy a player's color, hat, skin, visor, and pet to other players |
| /kill {player}                              | Kill a player (host permission required)          |
| /setimp {0-15}                              | Set next round's total impostors; keeps pre-set roles and randomly rolls remaining eligible players |
| /revive [player\|@all]                      | Revive dead player(s) and restore their pre-death role/abilities during a game, outside meetings (host permission required) |
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

In **Moderation Settings → Denied Words**, `Auto-rainbow on Turkish text (cosmetic)` is disabled by default. When enabled by the host, a player's Turkish-character username or a chat message containing `Ç/Ğ/Ö/Ü` (lowercase or uppercase) or a curated Among Us-related Turkish term can start the same rainbow cycle as `/ucolor gay` for that player. It triggers at most once per player per lobby and is cosmetic only; it does not kick or ban anyone. The term list includes words such as `mürettebat`, `görev`, `şüpheli`, `havalandırma`, `toplantı`, and `güvenlik kamerası`.

`/revive [player|@all]` is a host command for dead players during a game, and cannot be used during a meeting or exile. It revives the target, restores their role from before death (including their normal role abilities), and snaps them to the meeting spawn. This should also prevent the revived player being left with a ghost role/chat state. If chat still is not visible to everyone, check whether another lobby plugin is filtering chat or inspect its logs; AUR cannot override another plugin's server-side policy.

Use `/page <number> <command> [arguments]` to read additional pages of long command output. Public responses to commands issued by other players are suppressed to avoid lobby spam; their results remain available in the host's local console and logs.

`/settings <setting> <value>` can change and sync supported normal-mode options while a match is running. Hide and Seek is not supported by this command. Commands that inherently require a specific game state (for example, `/voteout` during a meeting, `/revive` during a game, or `/start` in a lobby) still enforce that state rather than attempting an invalid action everywhere.

`/setimp <0-15>` (also `/impcount`) sets the desired impostor-team size for the next round. Roles previously queued with `/set` or `/add` are checked first; pre-set impostors stay assigned, while the remaining eligible players are selected randomly at game start. If pre-set impostors exceed the requested count, those assignments are preserved and the total rises to include them.

Under **System Settings → Automation**, `Notify When Players From notifypresence.txt Join` controls alerts for entries in `AUR-DATA/notifypresence.txt`, while `Notify When Previous Players Return` controls alerts for players who previously completed a round with the host. Both use a wrapped, dismissible popup. Returning-player history is stored in `AUR-DATA/LobbyPlayerHistory.txt` with a UTC date and room code per lobby section; sections older than 14 days are pruned.

Round participants are captured at game start but added to `AUR-DATA/LobbyPlayerHistory.txt` only when the end-game screen is reached. Returning-player notifications are generated as those players join; AUR no longer emits a bulk “everyone from the previous lobby” alert at the beginning of a later round.

Use `/bind '<command template>' '<trigger template>' [;:prefix;]` to create a persistent text bind, `/bind list` to view binds, or `/bind remove '<trigger template>'` to delete one. Example: `/bind 'ucolor gay ;' 'make ; gay' ;:!color:;` makes the host's local `make green gay` input silently execute `/ucolor gay !color:green`. The `;` in the trigger captures one ASCII-alphanumeric word and substitutes it into the command; the optional `;:prefix;` transform adds a prefix to that value. Binds are host-local, do not require `/` or `!` when triggered, and are saved in `AUR-DATA/CommandBindings.txt`. Only commands supported by the private command dispatcher can be bound.

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

For this modified edition, visit <https://blobek.net/aurbe> or <https://github.com/blobek/AmongUsRevampedBlobekEdition>. Discord username: `blobekmini`. The `/aur` and `/socials` commands send these first, then a second message beginning with the original-author attribution.

## AI assistance notice

AI-assisted tools were used to help draft and modify some Blobek Edition code and documentation. AI-generated changes can contain mistakes; the Blobek maintainer remains responsible for review, testing, licensing compliance, and distribution.
---

<b>This mod is not affiliated with Among Us or Innersloth LLC, and the content contained therein is not endorsed or otherwise sponsored by Innersloth LLC. Portions of the materials contained herein are property of Innersloth LLC. © Innersloth LLC.</b>
