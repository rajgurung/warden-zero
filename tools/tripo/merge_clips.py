"""Copy the animation clips from one Tripo GLB into another with the same rig.

Tripo's retarget endpoint takes at most 5 animations per task, so a 7-clip set
comes back as two GLBs that share the same skeleton. This appends the clips of
`extra.glb` to `base.glb`, keeping one mesh.

usage: python3 merge_clips.py base.glb extra.glb out.glb
"""
import json
import struct
import sys


def read(path):
    d = open(path, 'rb').read()
    jl = struct.unpack_from('<I', d, 12)[0]
    g = json.loads(d[20:20 + jl])
    bl = struct.unpack_from('<I', d, 20 + jl)[0]
    return g, bytearray(d[28 + jl:28 + jl + bl])


base, extra, out = sys.argv[1:]
g, bin_ = read(base)
e, ebin = read(extra)
assert [n.get('name') for n in g['nodes']] == [n.get('name') for n in e['nodes']], 'rigs differ'

for anim in e['animations']:
    remap = {}
    for s in anim['samplers']:
        for key in ('input', 'output'):
            ai = s[key]
            if ai not in remap:
                acc = dict(e['accessors'][ai])
                view = dict(e['bufferViews'][acc['bufferView']])
                chunk = ebin[view.get('byteOffset', 0):view.get('byteOffset', 0) + view['byteLength']]
                bin_ += b'\0' * (-len(bin_) % 4)
                view.update(buffer=0, byteOffset=len(bin_))
                bin_ += chunk
                g['bufferViews'].append(view)
                acc['bufferView'] = len(g['bufferViews']) - 1
                g['accessors'].append(acc)
                remap[ai] = len(g['accessors']) - 1
            s[key] = remap[ai]
    g['animations'].append(anim)
    print('added', anim['name'])

bin_ += b'\0' * (-len(bin_) % 4)
g['buffers'][0]['byteLength'] = len(bin_)
js = json.dumps(g, separators=(',', ':')).encode()
js += b' ' * (-len(js) % 4)
total = 12 + 8 + len(js) + 8 + len(bin_)
with open(out, 'wb') as f:
    f.write(struct.pack('<4sII', b'glTF', 2, total))
    f.write(struct.pack('<I4s', len(js), b'JSON') + js)
    f.write(struct.pack('<I4s', len(bin_), b'BIN\0') + bin_)
