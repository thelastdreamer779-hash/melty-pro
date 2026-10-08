# MODLOG: Deltarune × Black Hole Fishing ("DELTAHOLE", working title)

## Agreed design (interview, 2026-10-08)
- Kris & friends explore a new Dark World; Ted Herring has dropped black holes into its ponds.
  BHF's real fish come out as Deltarune battles; sparing = catching (REEL at full MERCY).
- Opening follows BHF: Kris throws rocks at fish, Ted drops the black hole, first fish battle.
- Hub: Ted's shop with BHF's upgrades, restocking, trait research, breeding and the Smasher.
- Fish traits from BHF decide bullet patterns and which ACTs spare them.
- Co-op, 2 players: host = Kris + overworld; joiner = Susie and Ralsei (switches between them).
- v1: intro, 1 area with 2 void ponds, ~4 fish, hub with breeding + Smasher, short Ted mystery, boss fish.

## Melty facts (game_info / mashup_info)
- deltarune: engine gamemaker, no loader Melty installs. Route = mode "standalone" program that reads the
  player's Deltarune via `--game-dir {game}` (same as live mashup `deltafish`, one click: yes).
- Black Hole Fishing: not in Melty's catalog (slug custom-black-hole-fishing, Steam app 3667390).
  Role "secondary"; the program finds the Steam install itself and says so in-game when missing.
- No relay offered by Melty. Multiplayer connect: host writes "Hosting at <ip:port>" to
  {localappdata}/Deltahole/logs/latest.log; join via `--join {address}`.

## Route
- Own program in C# (.NET 8, self-contained win-x64, cross-built on Linux: verified) with MonoGame
  DesktopGL for drawing/audio, LiteNetLib (MIT) for UDP, UPnP mapping + STUN lookup for reaching
  home routers. Own minimal data.win reader (UndertaleModLib = format reference only; GPLv3, not linked).
- BHF: reportedly Godot (unverified). Read its .pck at runtime once its format is confirmed.

## Sheets
`sheets/*.json` are the source of truth; `python3 tools/preflight.py` lists every unfilled/unverified
cell and broken reference. Build only when it is clean.

## Status / next
- Blocked on game files: this cloud session cannot reach the user's PC.
