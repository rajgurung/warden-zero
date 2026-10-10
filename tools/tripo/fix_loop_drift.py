"""Clean up the Hips root track of Tripo's `animate_in_place` clips.

Two problems seen in Tripo output (Oct 2026):
  1. Walk/run climb upward every cycle (the hips end the loop 0.2-0.4 m higher
     than they start), so the loop pops. For the clips named on the command
     line, the linear drift from first to last key is removed.
  2. Every clip starts with the hips shifted sideways or forward from the rest
     pose (run ~1 m forward, idle ~0.25 m sideways), so the mesh sits away from
     the GameObject origin. For all clips, the horizontal offset at the first
     key is moved back to the rest position.

usage: python3 fix_loop_drift.py in.glb out.glb run walk
"""
import json
import struct
import sys

HIPS = 'mixamorig:Hips'

src, dst, *clips = sys.argv[1:]
data = bytearray(open(src, 'rb').read())
json_len = struct.unpack_from('<I', data, 12)[0]
gltf = json.loads(data[20:20 + json_len])
bin_start = 20 + json_len + 8  # skip BIN chunk header

hips = next(i for i, n in enumerate(gltf['nodes']) if n.get('name') == HIPS)
rest = gltf['nodes'][hips].get('translation', [0, 0, 0])
# Hips sit under a Z-up Root, so local Z is world up and X/Y are horizontal.
up_axis = max(range(3), key=lambda k: abs(rest[k]))


def floats(accessor_index):
    acc = gltf['accessors'][accessor_index]
    view = gltf['bufferViews'][acc['bufferView']]
    assert acc['componentType'] == 5126 and 'byteStride' not in view
    width = {'SCALAR': 1, 'VEC3': 3}[acc['type']]
    offset = bin_start + view.get('byteOffset', 0) + acc.get('byteOffset', 0)
    values = list(struct.unpack_from(f'<{acc["count"] * width}f', data, offset))
    return acc, offset, width, values


for anim in gltf['animations']:
    for ch in anim['channels']:
        if ch['target'].get('node') != hips or ch['target']['path'] != 'translation':
            continue
        sampler = anim['samplers'][ch['sampler']]
        _, _, _, times = floats(sampler['input'])
        acc, offset, _, v = floats(sampler['output'])
        keys = [v[i:i + 3] for i in range(0, len(v), 3)]
        t0, t1 = times[0], times[-1]
        drift = [keys[-1][k] - keys[0][k] if anim['name'] in clips else 0 for k in range(3)]
        shift = [keys[0][k] - rest[k] if k != up_axis else 0 for k in range(3)]
        fixed = [[p[k] - drift[k] * (t - t0) / (t1 - t0) - shift[k] for k in range(3)]
                 for t, p in zip(times, keys)]
        struct.pack_into(f'<{len(v)}f', data, offset, *[c for p in fixed for c in p])
        if 'min' in acc:
            acc['min'] = [min(p[k] for p in fixed) for k in range(3)]
            acc['max'] = [max(p[k] for p in fixed) for k in range(3)]
        print(f'{anim["name"]}: removed drift {[round(d, 3) for d in drift]}, '
              f'offset {[round(s, 3) for s in shift]}')

# Re-pack the JSON chunk (min/max may have changed length), padded to 4 bytes.
new_json = json.dumps(gltf, separators=(',', ':')).encode()
new_json += b' ' * (-len(new_json) % 4)
rest_bytes = data[20 + json_len:]
out = bytearray(struct.pack('<4sII', b'glTF', 2, 12 + 8 + len(new_json) + len(rest_bytes)))
out += struct.pack('<I4s', len(new_json), b'JSON') + new_json + rest_bytes
open(dst, 'wb').write(out)
