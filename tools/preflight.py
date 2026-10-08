#!/usr/bin/env python3
"""Preflight: overlay every sheet and list unfilled cells, unverified cells and broken cross-sheet refs.
A cell counts as checked only when it has a value AND a _src entry (evidence: design, game file, tool)."""
import json, sys, pathlib

root = pathlib.Path(__file__).resolve().parent.parent / "sheets"
sheets = {p.stem: json.loads(p.read_text()) for p in sorted(root.glob("*.json"))}
ids = {name: {r["id"] for r in s["rows"]} for name, s in sheets.items()}
unfilled, unverified, broken, empty = [], [], [], []

for name, s in sheets.items():
    cols = [c for c in s["columns"] if c != "id"]
    refs = s.get("refs", {})
    if not s["rows"]:
        empty.append(name)
    for r in s["rows"]:
        src = r.get("_src", {})
        for c in cols:
            v = r.get(c, None)
            where = f"{name}.{r['id']}.{c}"
            if c not in r or v is None:
                unfilled.append(where); continue
            if c not in src:
                unverified.append(where)
            if c in refs:
                target = refs[c]
                if target not in sheets:
                    broken.append(f"{where} -> sheet '{target}' missing"); continue
                for ref in (v if isinstance(v, list) else [v]):
                    if ref == "none": continue
                    if ref not in ids[target]:
                        broken.append(f"{where} -> {target}.{ref} not found")

total = sum(len(s["rows"]) * (len(s["columns"]) - 1) for s in sheets.values())
bad = len(set(unfilled) | set(unverified))
print(f"sheets: {len(sheets)}  cells: {total}  checked: {total - bad}  unfilled: {len(unfilled)}  unverified: {len(unverified)}  broken refs: {len(broken)}  empty sheets: {len(empty)}")
for title, items in (("EMPTY SHEETS", empty), ("UNFILLED", unfilled), ("UNVERIFIED", unverified), ("BROKEN REFS", broken)):
    if items:
        print(f"\n{title}:")
        for i in items: print("  ", i)
sys.exit(0 if not (unfilled or unverified or broken or empty) else 1)
