#!/usr/bin/env python3
"""Preflight: overlay every sheet, list unfilled cells and unresolved cross-sheet references.
Exit 1 on any error. 'needs-game' verification cells are listed as pending (they can only be
checked against a real install of that game) but do not block the build."""
import json, os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
SHEETS = os.path.join(HERE, '..', 'sheets')
def load(n): return json.load(open(os.path.join(SHEETS, n + '.json')))
S = {n: load(n) for n in ['games', 'content', 'rooms', 'entities', 'modes', 'legend', 'hooks', 'inputs']}
errors, pending = [], []
ids = {n: {r[S[n]['columns'][0]] for r in S[n]['rows']} for n in S}

# 1. every row x column crossing is filled
for n, sh in S.items():
    for r in sh['rows']:
        key = r[sh['columns'][0]]
        for c in sh['columns']:
            if c not in r: errors.append(f'{n}[{key}].{c}: missing'); continue
            v = r[c]
            empty_ok = (n == 'legend' and r['kind'] == 'air' and c == 'ref')
            if (v == '' or v is None or v == []) and not empty_ok:
                errors.append(f'{n}[{key}].{c}: empty')
        extra = set(r) - set(sh['columns'])
        if extra: errors.append(f'{n}[{key}]: cells outside the sheet columns {sorted(extra)}')
        if 'verified' in r and 'needs-game' in str(r['verified']):
            pending.append(f'{n}[{key}]: {r["verified"]}')

def ref(where, kind, v):
    if kind == 'content' and v.startswith('celeste:'): return
    if v not in ids[kind]: errors.append(f'{where}: "{v}" not found in {kind}')

# 2. references between sheets
for r in S['content']['rows']:
    ref(f'content[{r["id"]}].game', 'games', r['game'])
    for u in r['usedBy']:
        k, _, v = u.partition(':')
        ref(f'content[{r["id"]}].usedBy', {'room': 'rooms', 'entity': 'entities', 'mode': 'modes'}[k], v)
for r in S['rooms']['rows']:
    ref(f'rooms[{r["id"]}].music', 'content', r['music'])
    ref(f'rooms[{r["id"]}].startMode', 'modes', r['startMode'])
for n in ['entities', 'modes']:
    for r in S[n]['rows']:
        for a in r['art'].replace('|', '+').split('+'):
            ref(f'{n}[{r["id"]}].art', 'content', a)
for r in S['legend']['rows']:
    if r['kind'] == 'entity': ref(f'legend[{r["char"]}].ref', 'entities', r['ref'])
    if r['kind'] == 'entity' and r['ref'] == 'ModePortal': ref(f'legend[{r["char"]}].attrs.mode', 'modes', r['attrs']['mode'])
for r in S['inputs']['rows']: ref(f'inputs[{r["id"]}].mode', 'modes', r['mode'])
for r in S['entities']['rows']:
    rt = r['params'].get('returnTo')
    if rt: ref(f'entities[{r["id"]}].params.returnTo', 'rooms', rt)
# every content row is used by something that exists, every mode but normal is reachable
used_modes = {r['attrs']['mode'] for r in S['legend']['rows'] if r['ref'] == 'ModePortal'}
for m in ids['modes'] - used_modes - {'normal'}: errors.append(f'modes[{m}]: no portal reaches it')

# 3. room grids
chars = {r['char'] for r in S['legend']['rows']}
rects = []
for r in S['rooms']['rows']:
    L = r['layout']
    if len(L) != r['heightTiles']: errors.append(f'rooms[{r["id"]}]: {len(L)} rows, sheet says {r["heightTiles"]}')
    for i, row in enumerate(L):
        if len(row) != r['widthTiles']: errors.append(f'rooms[{r["id"]}] row {i}: {len(row)} wide, sheet says {r["widthTiles"]}')
        for ch in set(row) - chars: errors.append(f'rooms[{r["id"]}] row {i}: "{ch}" not in legend')
    if sum(row.count('P') for row in L) < 1: errors.append(f'rooms[{r["id"]}]: no spawn P')
    rects.append((r['id'], r['x'], r['y'], r['widthTiles'] * 8, r['heightTiles'] * 8, L))
for i, a in enumerate(rects):
    for b in rects[i + 1:]:
        if a[1] < b[1] + b[3] and b[1] < a[1] + a[3] and a[2] < b[2] + b[4] and b[2] < a[2] + a[4]:
            errors.append(f'rooms {a[0]} and {b[0]} overlap')
# 4. doorways: an open edge cell must face an open cell of a neighbouring room (or the room's own wall)
def cell(rc, wx, wy):
    _, x, y, w, h, L = rc
    if not (x <= wx < x + w and y <= wy < y + h): return None
    return L[(wy - y) // 8][(wx - x) // 8]
links = set()
for rc in rects:
    rid, x, y, w, h, L = rc
    edges = [(c, 0, 0, -1) for c in range(w // 8)] + [(c, h // 8 - 1, 0, 1) for c in range(w // 8)] + \
            [(0, rr, -1, 0) for rr in range(h // 8)] + [(w // 8 - 1, rr, 1, 0) for rr in range(h // 8)]
    for c, rr, dx, dy in edges:
        if L[rr][c] == '#': continue
        wx, wy = x + c * 8 + 4 + dx * 8, y + rr * 8 + 4 + dy * 8
        for other in rects:
            if other is rc: continue
            v = cell(other, wx, wy)
            if v is not None:
                if v == '#': errors.append(f'rooms[{rid}] edge cell ({c},{rr}) opens into a wall of {other[0]}')
                else: links.add(tuple(sorted((rid, other[0]))))
for need in [('hub', 'rhythm'), ('hub', 'turnip'), ('autorun', 'hub')]:
    if tuple(sorted(need)) not in links: errors.append(f'no doorway between {need[0]} and {need[1]}')

print(f'sheets: {", ".join(f"{n}({len(S[n]["rows"])})" for n in S)}')
print(f'doorways: {sorted(links)}')
for p in pending: print('PENDING (check in the real game):', p)
for e in errors: print('ERROR:', e)
print('preflight', 'CLEAN' if not errors else f'FAILED with {len(errors)} errors', f'({len(pending)} cells pending an in-game check)')
sys.exit(1 if errors else 0)
