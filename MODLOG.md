# MODLOG: Turnip Dash (Celeste x Geometry Dash x Turnip Mountain)

- Melty draft: modId `0e8a0471-c7cf-425e-aeca-3322c0c039eb`, slug `turnip-dash`, linked to this repo.
- Host: Celeste + Everest stable-1.6580.0 (Melty installs it). Built against Everest's `lib-stripped.zip` of that release.
- Geometry Dash (Steam 322170) and Turnip Mountain (Steam 2138410) aren't in Melty's catalog, so the recipe lists them as
  `secondary` and the mod finds them itself (`GameFinder.cs`: settings → Steam libraryfolders.vdf → appmanifest).
- Turnip Mountain's engine is not documented anywhere reachable; `TMContent.cs` detects Unity / Godot / GameMaker / loose files.

## Verified (in this cloud workspace)
- `tools/preflight.py`: sheets clean; 17 cells pending a real-game check (GD file and frame names, TM asset names).
- `tools/gen_map.py`: map encodes and decodes byte for byte (Lönn's BinaryPacker format).
- `tools/sim_autorun.py`: auto-run room is beatable with the sheet's cube/ship numbers (simplified physics).
- C# builds with 0 warnings against Everest's reference Celeste; classdata.tpk loads for Unity 2019.4 to 6000.0.
- Melty: inspect_package / validate_recipe / one_click_check all pass (one click: yes, 16 files placed, 0 left out).

## Not yet verified (needs a PC with the three games)
- The mod loading in Everest, every room playing, GD frames rendering the right way round (plist rotation),
  beat detection on the real songs, and which Turnip Mountain assets get picked.
  Turn on Mod Options → Turnip Dash → "Log what was read from each game", then read Celeste's log.txt.

## Next
- The package upload is blocked here: storage host `ekvzctwehmwidsengayc.storage.supabase.co` isn't on the network allowlist.
- After an in-game test, take a real screenshot for the listing.
