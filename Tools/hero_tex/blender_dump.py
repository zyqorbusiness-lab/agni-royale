# blender -b <N>_HairV3.blend --python blender_dump.py -- <Name> <out dir>   (dump for fix_hero_textures.py; read-only on the .blend)
import bpy, json, sys
from mathutils import Vector
from mathutils.bvhtree import BVHTree
name, outd = sys.argv[sys.argv.index('--') + 1:][:2]
ob = bpy.data.objects[f'{name}_LOD0']; me = ob.data; mw = ob.matrix_world; uv = me.uv_layers['UVMap'].data
mats = [m.name if m else '' for m in me.materials]
si = [i for i, m in enumerate(mats) if 'Skin' in m][0]; ci = [i for i, m in enumerate(mats) if 'Clothes' in m][0]
V = [mw @ v.co for v in me.vertices]; H = max(v.z for v in V)
res = {'H': H, 'feet': [], 'mid': [], 'shin_uv': [], 'clothes_uv_lod0': []}
for p in me.polygons:
    c = sum((V[i] for i in p.vertices), Vector()) / len(p.vertices); u = [list(uv[li].uv) for li in p.loop_indices]
    if p.material_index == ci:
        res['clothes_uv_lod0'].append(u)
        if 0.10 < c.z / H < 0.16: res['shin_uv'].append([sum(x[0] for x in u) / len(u), sum(x[1] for x in u) / len(u)])
    elif p.material_index == si:
        if c.z / H <= 0.09: res['feet'].append({'uv': u})
        elif 0.52 < c.z / H < 0.75 and abs(c.x) < 0.2: res['mid'].append({'uv': u})
json.dump(res, open(f'{outd}/{name}.json', 'w')); print('dumped', name)
