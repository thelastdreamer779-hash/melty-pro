#!/usr/bin/env python3
"""Generate the Celeste map (.bin + .meta.yaml) from sheets/rooms.json + legend.json + entities.json."""
import json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mapbin
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
S = {n: json.load(open(os.path.join(ROOT, 'sheets', n + '.json'))) for n in ['rooms', 'legend', 'entities']}
legend = {r['char']: r for r in S['legend']['rows']}
ents = {r['id']: r for r in S['entities']['rows']}
SID_PATH = 'Maps/TurnipDash/TurnipDash'

def room_element(room, next_id):
    L = room['layout']; H, W = len(L), len(L[0])
    solids = '\n'.join(''.join(room['tileset'] if ch == '#' else '0' for ch in row).rstrip('0') for row in L).rstrip('\n')
    used = [[False] * W for _ in range(H)]; entities = []
    for r in range(H):
        for c in range(W):
            ch = L[r][c]
            if used[r][c] or legend[ch]['kind'] != 'entity': continue
            e = ents[legend[ch]['ref']]
            w = h = 1
            if e['merge'] in ('horizontal', 'rect'):
                while c + w < W and L[r][c + w] == ch and not used[r][c + w]: w += 1
            if e['merge'] == 'rect':
                while r + h < H and all(L[r + h][c + k] == ch for k in range(w)): h += 1
            for rr in range(r, r + h):
                for cc in range(c, c + w): used[rr][cc] = True
            el = {'__name': e['everestName'], 'id': next_id()}
            if e['everestName'] == 'player':
                el.update(x=c * 8 + 4, y=(r + 1) * 8)
            elif e['everestName'] == 'spikesUp':
                el.update(x=c * 8, y=(r + 1) * 8, width=w * 8)
            else:
                el.update(x=c * 8, y=r * 8)
                el['width'] = e['width'] * w if e['merge'] != 'none' else e['width']
                el['height'] = e['height'] * h if e['merge'] == 'rect' else e['height']
            for k, v in {**e['params'], **legend[ch]['attrs']}.items(): el[k] = v
            entities.append(el)
    blank = '\n'.join('' for _ in range(H))
    return {'__name': 'level', 'name': room['name'], 'x': room['x'], 'y': room['y'],
            'width': W * 8, 'height': H * 8, 'music': '', 'alt_music': '', 'ambience': '',
            'musicLayer1': True, 'musicLayer2': True, 'musicLayer3': True, 'musicLayer4': True,
            'musicProgress': '', 'ambienceProgress': '', 'dark': False, 'space': False, 'underwater': False,
            'whisper': False, 'disableDownTransition': False, 'delayAltMusicFade': False, 'windPattern': 'None',
            'c': 0, 'cameraOffsetX': 0, 'cameraOffsetY': 0,
            '__children': [
                {'__name': 'solids', 'innerText': solids},
                {'__name': 'bg', 'innerText': blank},
                {'__name': 'objtiles', 'innerText': ''},
                {'__name': 'fgtiles', 'tileset': 'Scenery', 'innerText': ''},
                {'__name': 'bgtiles', 'tileset': 'Scenery', 'innerText': ''},
                {'__name': 'entities', '__children': entities},
                {'__name': 'triggers'},
                {'__name': 'fgdecals', 'tileset': 'scenery'},
                {'__name': 'bgdecals', 'tileset': 'scenery'},
            ]}

def main(out_dir):
    counter = [0]
    def nid(): counter[0] += 1; return counter[0]
    levels = [room_element(r, nid) for r in S['rooms']['rows']]
    root = {'__name': 'Map', '__children': [
        {'__name': 'levels', '__children': levels},
        {'__name': 'Filler'},
        {'__name': 'Style', '__children': [{'__name': 'Foregrounds'}, {'__name': 'Backgrounds'}]}]}
    data = mapbin.encode(root, 'TurnipDash')
    pkg, back = mapbin.decode(data)          # round-trip check
    assert pkg == 'TurnipDash' and back['__children'][0]['__children'][0]['name'] == 'lvl_hub'
    for lv, src in zip(back['__children'][0]['__children'], S['rooms']['rows']):
        rows = lv['__children'][0]['innerText'].split('\n')
        for i, row in enumerate(src['layout']):
            want = ''.join(src['tileset'] if ch == '#' else '0' for ch in row).rstrip('0')
            assert (rows[i] if i < len(rows) else '') == want, (src['id'], i)
    path = os.path.join(out_dir, SID_PATH + '.bin'); os.makedirs(os.path.dirname(path), exist_ok=True)
    open(path, 'wb').write(data)
    open(os.path.join(out_dir, SID_PATH + '.meta.yaml'), 'w').write(
        'Mode:\n  Inventory: Default\n  StartLevel: hub\n  HeartIsEnd: false\n  IgnoreLevelAudioLayerData: true\n')
    n = sum(len(l['__children'][5].get('__children', [])) for l in back['__children'][0]['__children'])
    print(f'map: {path} ({len(data)} bytes, {len(levels)} rooms, {n} entities, round-trip OK)')

if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, 'mod'))
