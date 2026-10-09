# Turnip Dash

A Hollow Knight mashup with Geometry Dash and Turnip Mountain, remixed from Eveeezy's **Hollow Dash**.

A Geometry Dash portal opens in King's Pass. Step in and the Knight becomes the turnip from
**your own copy of Turnip Mountain**, auto-running the real **Stereo Madness** (level and music read from
**your own copy of Geometry Dash**). Clear it and you're dropped at the foot of a cliff, where you climb
Turnip Mountain-style: each hand grabs on its own button and you swing yourself up to the summit.
Reach the top and you're back in Hallownest with your time.

## What players get
- A Geometry Dash portal in King's Pass (Hollow Knight's opening area)
- Stereo Madness: the real level layout and song from Geometry Dash, with the cube and ship sections
- You play as the turnip, using Turnip Mountain's own art
- A two-hand Turnip Mountain climb to finish the chain
- Single player

## Needs
- Hollow Knight 1.5 (Melty installs the Hollow Knight Modding API by itself)
- Geometry Dash (Steam) and Turnip Mountain (Steam), installed on the same PC. Nothing from either game is
  shipped: the mod reads them from your installs. Non-Steam copies: set `TURNIPDASH_GD_DIR` /
  `TURNIPDASH_TM_DIR` to their folders. If either is missing, the portal says so in the game.

## Controls (your Hollow Knight bindings)
- Portal: **Up** to enter
- Run: **Jump** to jump (hold for repeat jumps, hold to fly the ship), **R** restarts
- Climb: **Attack** = left hand, **Dash** = right hand, arrows/stick steer the free hand and pull you up

## How it is built
The design lives in `design/sheets/*.json` (games, content read from each game, Geometry Dash objects,
stages, run physics, climb rules and route, Hollow Knight hooks, on-screen text). `tools/sheets.py` checks
them (every cell filled, every cross-reference resolving) and generates `src/TurnipDash/Generated/Sheets.g.cs`.

    python3 tools/sheets.py gen
    tools/package.sh          # builds and zips dist/TurnipDash-<version>.zip

`stub/Assembly-CSharp` is a compile-only stand-in for the Modding API's `Mod` class; the real one comes
from the player's game. Game types are reached by reflection (see `design/sheets/hooks.json`).

## Credits
- Original mashup: **Hollow Dash** by Eveeezy (remixed on Melty)
- Hollow Knight by Team Cherry; Geometry Dash by RobTop Games; Turnip Mountain by Luke Sanderson / JanduSoft.
  Their content is never included; each player's own copies are read at runtime.
- Bundled libraries: see THIRD-PARTY-NOTICES.txt
