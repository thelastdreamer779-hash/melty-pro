"""Celeste BinaryPacker map encoder/decoder (same format Lönn writes: header, package, string lookup, element tree)."""
import struct

def _w7(n):
    out = bytearray()
    while True:
        b = n & 0x7F; n >>= 7
        out.append(b | (0x80 if n else 0))
        if not n: return bytes(out)

def _wstr(s):
    b = s.encode('utf-8'); return _w7(len(b)) + b

def _rle(s):
    b = s.encode('ascii'); out = bytearray(); i = 0
    while i < len(b):
        j = i
        while j < len(b) and b[j] == b[i] and j - i < 255: j += 1
        out += bytes([j - i, b[i]]); i = j
    return bytes(out)

def _strings(el, seen):
    seen.add(el['__name'])
    for k, v in el.items():
        if k in ('__name', '__children'): continue
        seen.add(k)
        if isinstance(v, str) and k != 'innerText': seen.add(v)
    for c in el.get('__children', []): _strings(c, seen)

def encode(root, package):
    seen = set(); _strings(root, seen)
    lookup = sorted(seen); idx = {s: i for i, s in enumerate(lookup)}
    out = bytearray(_wstr('CELESTE MAP') + _wstr(package) + struct.pack('<h', len(lookup)))
    for s in lookup: out += _wstr(s)
    def val(v):
        if isinstance(v, bool): return bytes([0, int(v)])
        if isinstance(v, int):
            if 0 <= v <= 255: return bytes([1, v])
            if -32768 <= v <= 32767: return bytes([2]) + struct.pack('<h', v)
            return bytes([3]) + struct.pack('<i', v)
        if isinstance(v, float): return bytes([4]) + struct.pack('<f', v)
        if isinstance(v, str):
            if v in idx: return bytes([5]) + struct.pack('<h', idx[v])
            r = _rle(v) if v.isascii() else None
            if r is not None and len(r) < len(v) and len(r) < 2 ** 15: return bytes([7]) + struct.pack('<h', len(r)) + r
            return bytes([6]) + _wstr(v)
        raise TypeError(type(v))
    def el(e):
        attrs = [(k, v) for k, v in e.items() if k not in ('__name', '__children')]
        assert len(attrs) < 256
        b = bytearray(struct.pack('<h', idx[e['__name']]) + bytes([len(attrs)]))
        for k, v in attrs: b += struct.pack('<h', idx[k]) + val(v)
        ch = e.get('__children', [])
        b += struct.pack('<h', len(ch))
        for c in ch: b += el(c)
        return b
    out += el(root)
    return bytes(out)

def decode(data):
    p = [0]
    def r7():
        n = s = 0
        while True:
            b = data[p[0]]; p[0] += 1; n |= (b & 0x7F) << s; s += 7
            if not b & 0x80: return n
    def rstr():
        n = r7(); v = data[p[0]:p[0] + n].decode('utf-8'); p[0] += n; return v
    def rd(fmt):
        v = struct.unpack_from(fmt, data, p[0]); p[0] += struct.calcsize(fmt); return v[0]
    assert rstr() == 'CELESTE MAP'
    pkg = rstr(); lookup = [rstr() for _ in range(rd('<h'))]
    def el():
        e = {'__name': lookup[rd('<h')]}
        for _ in range(rd('<B')):
            k = lookup[rd('<h')]; t = rd('<B')
            if t == 0: v = bool(rd('<B'))
            elif t == 1: v = rd('<B')
            elif t == 2: v = rd('<h')
            elif t == 3: v = rd('<i')
            elif t == 4: v = rd('<f')
            elif t == 5: v = lookup[rd('<h')]
            elif t == 6: v = rstr()
            elif t == 7:
                n = rd('<h'); raw = data[p[0]:p[0] + n]; p[0] += n
                v = ''.join(chr(raw[i + 1]) * raw[i] for i in range(0, n, 2))
            else: raise ValueError(t)
            e[k] = v
        n = rd('<h')
        if n: e['__children'] = [el() for _ in range(n)]
        return e
    root = el(); assert p[0] == len(data), 'trailing bytes'
    return pkg, root
