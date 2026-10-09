#!/usr/bin/env python3
"""Checks the auto-run room is beatable with the sheet's cube/ship numbers: searches hold/release inputs
(10 Hz decisions) for a run from the cube portal to the turnip portal without touching a spike or a wall."""
import json, os
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
rooms = {r['id']: r for r in json.load(open(os.path.join(ROOT, 'sheets/rooms.json')))['rows']}
modes = {r['id']: r for r in json.load(open(os.path.join(ROOT, 'sheets/modes.json')))['rows']}
L = rooms['autorun']['layout']
def cell(x, y):
    c, r = int(x // 8), int(y // 8)
    return L[r][c] if 0 <= r < len(L) and 0 <= c < len(L[0]) else '#'
solid = lambda x, y: cell(x, y) in '#G'
def box(x, y, ch):  # player hitbox 8x11, x = centre, y = feet
    return any(cell(px, py) in ch for px in (x - 3.9, x, x + 3.9) for py in (y - 0.1, y - 5.5, y - 10.9))
def spike(x, y):    # GD spikes kill on their inner 4x4 only
    for px in (x - 3.9, x + 3.9):
        for py in (y - 0.1, y - 10.9):
            c, r = int(px // 8), int(py // 8)
            if cell(px, py) == 'A' and 2 <= px - c * 8 <= 6 and py - r * 8 >= 4: return True
    return box(x, y, '^')
portal_x = {ch: L[[i for i, row in enumerate(L) if ch in row][0]].index(ch) * 8 for ch in 'CHU'}
dt = 1 / 60
def run(plan):
    x, y, vy, mode, t = portal_x['C'] + 8, 168.0, 0.0, 'cube', 0
    for step in range(int(12 / dt)):
        hold = plan[min(int(t * 10), len(plan) - 1)]
        m = modes[mode]
        if x >= portal_x['H'] and mode == 'cube': mode = 'ship'
        if x >= portal_x['U']: return True, x, t
        x2 = x + m['runSpeed'] * dt
        if box(x2, y, '#G'): return False, x, t                        # dies on wall hit
        x = x2
        ground = solid(x - 3.9, y + 1) or solid(x + 3.9, y + 1)
        if mode == 'cube':
            if ground and vy >= 0: vy = -m['jumpSpeed'] if hold else 0
            else: vy = min(m['maxFall'], vy + m['gravity'] * dt)
        else:
            vy = max(-m['maxRise'], vy - m['liftAccel'] * dt) if hold else min(m['maxFall'], vy + m['gravity'] * dt)
        y2 = y + vy * dt
        if box(x, y2, '#G'):
            vy = 0
        else: y = y2
        if spike(x, y): return False, x, t
        t += dt
    return False, x, t
# greedy search with backtracking over 0.1 s decisions
best = (0, None)
def search(plan, depth=0):
    global best
    ok, x, t = run(plan + [0] * 200)
    if ok: return plan
    if x > best[0]: best = (x, plan)
    if len(plan) > int(t * 10) + 1 or len(plan) > 140: return None
    for h in (0, 1):
        r = search(plan + [h], depth + 1)
        if r: return r
    return None
import sys; sys.setrecursionlimit(10000)
plan = search([])
if plan: print(f'auto-run: beatable (found a hold pattern, {len(plan)} decisions)'); sys.exit(0)
print(f'auto-run: NOT beatable with sheet numbers; furthest x={best[0]:.0f}px (tile {best[0]//8:.0f})'); sys.exit(1)
