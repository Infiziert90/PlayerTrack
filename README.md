# PlayerTrack
Keep track of players you meet. 
A Dalamud plugin for FINAL FANTASY XIV that keeps a personal record of every player you meet.

## Description
PlayerTrack helps you keep a record of who you meet and the content you played together. 
Organize players into categories, keep notes, and track them across name/world changes. 
Customization options include colors, icons, nameplates, and alerts.
Create rules to automatically categorize based on Keywords.
PlayerTrack automatically logs the players you encounter and the content you played together,
so you can remember who that great healer was three weeks later — even after a name or world change.
Every encounter is captured locally on your machine, with no external services required.

### Features
- **Encounter history** — captures when, where, and with whom you played, including duty, territory, and job.
- **Name & world tracking** — follows players across Lodestone name changes and world transfers so their history stays linked.
- **Notes** — attach freeform notes to any player you've met.
- **Categories** — organize players into custom groups (e.g. Friends, Avoid, Guild) with their own colors, icons, and behaviors.
- **Keyword rules** — auto-assign categories when a player's name, note, or tag matches a keyword.
- **Visual customization** — per-player or per-category colors, icons, and nameplate overrides in the overworld.
- **Alerts** — get notified when specific players come online or appear nearby.
- **Lodestone integration** — pulls public character data to keep records up to date.
- **Backups** — automatic local backups of the PlayerTrack database.
- **Localization** — translated into multiple languages via Crowdin.

## How to Use
- Install through XIVLauncher/Dalamud.
- Install through XIVLauncher / Dalamud's plugin installer.
- Use `/ptrack` to open the main window.
- Use `/ptrackconfig` to open settings.
- Right-click a player (in the party list, chat, nameplate, etc.) to add notes, set a category, or open their PlayerTrack entry.

## IPC API
PlayerTrack exposes an IPC interface so other Dalamud plugins can read and update its data.
See [`IPlayerTrackAPI`](PlayerTrack.Plugin/API/IPlayerTrackAPI.cs) for the full surface. Current methods include:

- `GetPlayerCurrentNameWorld(name, worldId)` — resolve a historical name/world to a player's current one.
- `GetPlayerNotes(name, worldId)` — fetch the notes saved for a player.
- `GetAllPlayerNameWorldHistories()` — enumerate every tracked player along with their previous name/world combinations.
- `AssignCategory(name, worldId, categoryId)` — assign a category to a player, updating both the database and the live UI.

PlayerTrack also integrates with the Visibility and XIV Instant Messenger plugins when they are installed.

## Privacy
All data is stored locally in your Dalamud plugin config directory as a SQLite database. PlayerTrack does not upload encounter data anywhere.

## Libraries Used
- [AutoMapper](https://github.com/AutoMapper/AutoMapper)
