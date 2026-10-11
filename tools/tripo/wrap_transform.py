"""Put all scene roots of a GLB under one parent node with a transform.

Used to give a prop a sensible pivot, size and facing without re-generating it.
The transform is applied in this order: translate by -pivot (in the original
model's units), then rotate by yaw degrees about +Y, then scale uniformly.

usage: python3 wrap_transform.py in.glb out.glb name scale yaw_deg px py pz
"""
import json
import math
import struct
import sys

src, dst, name, scale, yaw, px, py, pz = sys.argv[1:]
scale, yaw, pivot = float(scale), math.radians(float(yaw)), [float(px), float(py), float(pz)]

d = open(src, 'rb').read()
jl = struct.unpack_from('<I', d, 12)[0]
g = json.loads(d[20:20 + jl])
rest = d[20 + jl:]

# World = S * R * (p - pivot), so the parent's translation is S * R * (-pivot).
c, s = math.cos(yaw), math.sin(yaw)
x, y, z = (-v for v in pivot)
t = [scale * (c * x + s * z), scale * y, scale * (-s * x + c * z)]
parent = {'name': name, 'children': g['scenes'][g.get('scene', 0)]['nodes'],
          'translation': t, 'rotation': [0, math.sin(yaw / 2), 0, math.cos(yaw / 2)],
          'scale': [scale] * 3}
g['nodes'].append(parent)
g['scenes'][g.get('scene', 0)]['nodes'] = [len(g['nodes']) - 1]

js = json.dumps(g, separators=(',', ':')).encode()
js += b' ' * (-len(js) % 4)
with open(dst, 'wb') as f:
    f.write(struct.pack('<4sII', b'glTF', 2, 12 + 8 + len(js) + len(rest)))
    f.write(struct.pack('<I4s', len(js), b'JSON') + js + rest)
