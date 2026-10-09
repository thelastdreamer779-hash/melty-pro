# Turnip Dash

**Geometry Dash and Turnip Mountain, played inside Celeste.**

A small hub with three rooms:
- **Rhythm climb**: Madeline climbs Geometry Dash blocks and spikes that switch on and off to the beat of
  Geometry Dash's *Stereo Madness*, taken from your own copy. The tempo is measured from the song itself.
  Turnip Mountain's turnip waits at the top.
- **Turnip climb**: step through the portal and you become Turnip Mountain's turnip. Climb a chimney one hand at a
  time to Turnip Mountain's own music: each hand grips any wall it touches, and momentum carries you when you let go.
- **Auto-run**: Geometry Dash rules to *Back On Track*. You run on your own as the Geometry Dash cube, then portals
  turn you into the ship and then the turnip, which swings along the ceiling over a spike pit.

Solo, single player.

## What you need
- Celeste (Steam, Epic or itch), with Everest. Melty installs Everest for you.
- Geometry Dash (Steam) and Turnip Mountain (Steam) installed on the same PC. The mod finds them in your Steam
  libraries and reads their art and music from there. Nothing from either game is shipped with the mod. If one isn't
  found, the hub says so and Celeste's own art fills in.
- Your folders are somewhere else? Set them in Mod Options → Turnip Dash.

## Controls
| Mode | Gamepad | Keyboard / mouse |
|---|---|---|
| Cube | hold Jump to jump | hold Jump (C) or left click |
| Ship | hold Jump to fly up | hold Jump (C) or left click |
| Turnip | left stick / right stick move each hand, LT / RT (or Grab / Jump) grip | arrows aim, Grab (Z/V) grips left hand; mouse aims and left click grips right hand |

## For developers
Everything is generated from the design sheets in `sheets/` (`python3 tools/preflight.py` checks them).
`./build.sh` runs the preflight, generates `src/TurnipDash/Sheets.g.cs` and the map, builds against Everest's
reference build of Celeste, and writes `dist/TurnipDash-<version>.zip`.
