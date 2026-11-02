
import bpy, bmesh, sys, json, math, os, re, mathutils, time
import numpy as np
argv = sys.argv[sys.argv.index("--")+1:]
S = json.load(open(argv[0]))
NAME = S["name"]; OUT = S["out"]; os.makedirs(OUT, exist_ok=True)
REP = {"name": NAME, "src": S["src"]}
T0 = time.time()
def log(*a): print("[prep %s %.0fs]" % (NAME, time.time()-T0), *a, flush=True)
def tris(o): return sum(len(p.vertices)-2 for p in o.data.polygons)

# ---------------- load
if S["src"].lower().endswith(".fbx"):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=S["src"], use_custom_normals=True, ignore_leaf_bones=False)
else:
    bpy.ops.wm.open_mainfile(filepath=S["src"])
sc = bpy.context.scene
if bpy.context.object and bpy.context.object.mode != "OBJECT": bpy.ops.object.mode_set(mode="OBJECT")
REP["tris_source"] = sum(tris(o) for o in sc.objects if o.type == "MESH")

# ---------------- relink textures
index = {}
for d in S.get("texdirs", []):
    for root, _, files in os.walk(d):
        for f in files: index.setdefault(f.lower(), os.path.join(root, f)); index.setdefault(os.path.splitext(f.lower())[0], os.path.join(root, f))
for img in bpy.data.images:
    if img.source != "FILE" or img.packed_file: continue
    p = bpy.path.abspath(img.filepath)
    if os.path.exists(p): continue
    b = os.path.basename(p.replace("\\", "/")).lower() or img.name.lower()
    hit = index.get(b) or index.get(os.path.splitext(b)[0]) or index.get(img.name.lower()) or index.get(os.path.splitext(img.name.lower())[0])
    if hit: img.filepath = hit; img.reload()
    else: log("missing texture", img.name, p)

# ---------------- delete junk objects
arms = [o for o in sc.objects if o.type == "ARMATURE"]
arm = [o for o in arms if o.name == S.get("armature")] [0] if S.get("armature") else max(arms, key=lambda a: len(a.data.bones))
dele = [re.compile(x) for x in S.get("delete", [])]
for o in list(sc.objects):
    if o == arm: continue
    kill = o.type not in ("MESH",) or any(r.fullmatch(o.name) for r in dele) or o.hide_render
    if o.type == "MESH" and not kill and len(o.data.polygons) == 0: kill = True
    if kill: bpy.data.objects.remove(o, do_unlink=True)
for a in arms:
    if a != arm and a.name in bpy.data.objects: bpy.data.objects.remove(a, do_unlink=True)

# ---------------- rest pose, no animation, no shape keys, rigid bindings
arm.animation_data_clear(); arm.data.pose_position = "POSE"
for pb in arm.pose.bones: pb.matrix_basis = mathutils.Matrix()
for name, bone in S.get("rigid", {}).items():
    o = bpy.data.objects.get(name)
    if not o: continue
    for g in list(o.vertex_groups): o.vertex_groups.remove(g)
    g = o.vertex_groups.new(name=bone); g.add([v.index for v in o.data.vertices], 1.0, "REPLACE")
meshes = [o for o in sc.objects if o.type == "MESH"]
for o in meshes:
    if o.data.shape_keys:
        kb = o.data.shape_keys.key_blocks; active = [(k.name, round(k.value, 2)) for k in kb[1:] if abs(k.value) > 0.01]
        if active and S.get("bake_shape_mix", True):
            mix = o.shape_key_add(name="__mix", from_mix=True)
            co = [k.co.copy() for k in mix.data]
            o.shape_key_clear()
            for v, c in zip(o.data.vertices, co): v.co = c
            log("shape mix baked", o.name, active[:6])
        else: o.shape_key_clear()
    o.animation_data_clear()
    if not any(m.type == "ARMATURE" for m in o.modifiers):
        if len(o.vertex_groups) == 0: log("WARN unskinned mesh deleted", o.name); bpy.data.objects.remove(o); continue
        m = o.modifiers.new("Armature", "ARMATURE"); m.object = arm
    for m in o.modifiers:
        if m.type == "ARMATURE": m.object = arm
    for m in list(o.modifiers):
        if m.type not in ("ARMATURE",): o.modifiers.remove(m)
meshes = [o for o in sc.objects if o.type == "MESH"]
# strip faces using unwanted materials (teeth/tongue/cornea inside a closed mouth/eye)
dm = [re.compile(x, re.I) for x in S.get("delete_materials", [])]
for o in meshes:
    bad = [i for i, ms in enumerate(o.material_slots) if ms.material and any(r.search(ms.material.name) for r in dm)]
    if not bad: continue
    bm = bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.material_index in bad], context="FACES")
    bm.to_mesh(o.data); bm.free()
    if len(o.data.polygons) == 0: bpy.data.objects.remove(o)
meshes = [o for o in sc.objects if o.type == "MESH"]
REP["parts_kept"] = {o.name: tris(o) for o in meshes}

# ---------------- apply transforms (armature + meshes), unparented
bpy.ops.object.select_all(action="DESELECT")
for o in meshes:
    mw = o.matrix_world.copy(); o.parent = None; o.matrix_world = mw
for o in meshes + [arm]: o.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.make_single_user(object=True, obdata=True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
dg = bpy.context.evaluated_depsgraph_get()
def bounds():
    dg.update(); pts = []
    for o in [x for x in sc.objects if x.type == "MESH"]:
        e = o.evaluated_get(dg); me = e.to_mesh(); M = e.matrix_world
        a = np.empty(len(me.vertices)*3, dtype=np.float32); me.vertices.foreach_get("co", a); a = a.reshape(-1, 3)
        a = a @ np.array(M.to_3x3()).T + np.array(M.translation)
        pts.append([a.min(0), a.max(0)]); e.to_mesh_clear()
    mn = np.min([p[0] for p in pts], 0); mx = np.max([p[1] for p in pts], 0); return mn, mx
mn, mx = bounds(); REP["height_source_m"] = float(mx[2]-mn[2])
s = S["height"] / float(mx[2]-mn[2])
hips = arm.data.bones[S["hips"]] if S.get("hips") in arm.data.bones else arm.data.bones[0]
cx, cy = float(hips.head_local.x), float(hips.head_local.y)
M = mathutils.Matrix.Diagonal((s, s, s, 1)) @ mathutils.Matrix.Translation((-cx, -cy, -float(mn[2])))
for o in meshes + [arm]: o.matrix_world = M @ o.matrix_world
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
mn, mx = bounds(); REP["height_m"] = float(mx[2]-mn[2]); log("height", REP["height_m"], "scale", s)
H = REP["height_m"]


if S.get("add_root"):
    nm, kids = S["add_root"]
    bpy.ops.object.select_all(action="DESELECT"); arm.select_set(True); bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT"); eb = arm.data.edit_bones
    ks = [eb[k] for k in kids if k in eb]
    c = sum((k.head for k in ks), mathutils.Vector()) / len(ks)
    b = eb.new(nm); b.head = c; b.tail = c + mathutils.Vector((0, 0, 0.08*H))
    for k in ks: k.use_connect = False; k.parent = b
    bpy.ops.object.mode_set(mode="OBJECT"); log("added root bone", nm, tuple(c))

# ---------------- protect group (face + hands keep detail when decimating)
def bone_head(n):
    b = arm.data.bones.get(n); return b.head_local.copy() if b else None
neck = bone_head(S["neck"]); hands = [bone_head(n) for n in S.get("hands", []) if bone_head(n)]
headb = bone_head(S.get("head", "")) if S.get("head") else None
def make_protect(o, strength):
    # weight 1 = decimate freely; the face front and the hands get 1-strength (0 would freeze them completely)
    g = o.vertex_groups.get("_protect")
    if g: o.vertex_groups.remove(g)
    g = o.vertex_groups.new(name="_protect")
    hy = headb.y if headb is not None else 0.0
    face = []; hand = []; rest = []; eyes = []
    egi = o.vertex_groups["_eyes"].index if "_eyes" in o.vertex_groups else -1
    for v in o.data.vertices:
        p = v.co
        if egi >= 0 and any(g.group == egi and g.weight > 0.5 for g in v.groups): eyes.append(v.index)
        elif neck is not None and p.z > neck.z + 0.01*H and p.y < hy: face.append(v.index)
        elif any((p - h).length < 0.10*H/1.7 for h in hands): hand.append(v.index)
        else: rest.append(v.index)
    if rest: g.add(rest, 1.0, "REPLACE")
    if face: g.add(face, max(0.02, 1.0 - strength), "REPLACE")
    if hand: g.add(hand, max(0.05, 1.0 - 0.8*strength), "REPLACE")
    if eyes: g.add(eyes, 0.0 if strength >= 0.5 else 0.3, "REPLACE")
    return g
def decimate(o, target=None, ratio=None, protect=True, factor=0.8):
    n = tris(o)
    if ratio is None: ratio = min(1.0, target/float(max(1, n)))
    if ratio >= 0.999: return
    bpy.ops.object.select_all(action="DESELECT"); o.select_set(True); bpy.context.view_layer.objects.active = o
    m = o.modifiers.new("Dec", "DECIMATE"); m.decimate_type = "COLLAPSE"; m.ratio = ratio; m.use_collapse_triangulate = True
    if protect: g = make_protect(o, factor); m.vertex_group = g.name; m.invert_vertex_group = False; m.vertex_group_factor = 1.0
    while o.modifiers.find("Dec") > 0: bpy.ops.object.modifier_move_up(modifier="Dec")
    bpy.ops.object.modifier_apply(modifier="Dec")

for o in [x for x in sc.objects if x.type == "MESH"]:
    bm = bmesh.new(); bm.from_mesh(o.data); n0 = len(bm.verts)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.00025*H/1.8)
    seenf = {}; dup = []
    for f in bm.faces:
        key = frozenset(v.index for v in f.verts)
        if key in seenf: dup.append(f)
        else: seenf[key] = f
    if dup: bmesh.ops.delete(bm, geom=dup, context="FACES_ONLY")
    nme = [e for e in bm.edges if not e.is_manifold and not e.is_boundary and not e.is_wire]
    if nme: bmesh.ops.split_edges(bm, edges=nme)
    bm.to_mesh(o.data); bm.free()
    if dup: log("dup faces removed", o.name, len(dup))
    if o.data.has_custom_normals:
        bpy.ops.object.select_all(action="DESELECT"); o.select_set(True); bpy.context.view_layer.objects.active = o
        bpy.ops.mesh.customdata_custom_splitnormals_clear()
    o.data.shade_smooth()
    log("weld", o.name, n0, "->", len(o.data.vertices))
for name, tgt in S.get("part_tris", {}).items():
    o = bpy.data.objects.get(name)
    if o: before = tris(o); decimate(o, target=tgt, protect=S.get("part_protect", True)); log("part", name, before, "->", tris(o))

# ---------------- join -> LOD0, one UV layer
meshes = [o for o in sc.objects if o.type == "MESH"]
for o in meshes:
    uvs = o.data.uv_layers
    keep = next((u for u in uvs if u.active_render), uvs[0] if len(uvs) else None)
    if keep is None: keep = uvs.new(name="UVMap")
    for u in list(uvs):
        if u.name != keep.name: uvs.remove(u)
    keep.name = "UVMap"
main = max(meshes, key=tris)
bpy.ops.object.select_all(action="DESELECT")
for o in meshes: o.select_set(True)
bpy.context.view_layer.objects.active = main
if len(meshes) > 1: bpy.ops.object.join()
lod0 = bpy.context.object; lod0.name = NAME + "_LOD0"; lod0.data.name = NAME + "_LOD0"
if S.get("debug_join"): bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "debug_join.blend")); log("debug saved")
REP["tris_joined"] = tris(lod0); log("joined", REP["tris_joined"], "materials", len(lod0.material_slots))

# faces hidden inside shoes etc. that poke through (spec kill_faces: material regex + below a height fraction)
for kf in S.get("kill_faces", []):
    bm = bmesh.new(); bm.from_mesh(lod0.data); rx = re.compile(kf["mat"], re.I); kill = []
    for f in bm.faces:
        m = lod0.material_slots[f.material_index].material if lod0.material_slots else None
        if m and rx.search(m.name) and max(v.co.z for v in f.verts) < kf["zmax"]*H: kill.append(f)
    bmesh.ops.delete(bm, geom=kill, context="FACES"); bm.to_mesh(lod0.data); bm.free(); log("kill_faces", kf, len(kill))
# eye parts never collapse (small closed shapes vanish first under quadric decimation)
EYEK = re.compile(S.get("eye_names", r"(eye|iris|pupil|sclera|cornea)"), re.I); LASHK = re.compile(r"(lash|brow|line)", re.I)
eg = lod0.vertex_groups.new(name="_eyes"); ev = set()
for p in lod0.data.polygons:
    m = lod0.material_slots[p.material_index].material if lod0.material_slots else None
    if m and EYEK.search(m.name) and not LASHK.search(m.name): ev.update(p.vertices)
if ev: eg.add(list(ev), 1.0, "REPLACE")
log("eye verts", len(ev))
# ---------------- material sources + classification
def trace_img(sock, depth=0):
    if not sock.is_linked or depth > 8: return None
    n = sock.links[0].from_node; fs = sock.links[0].from_socket
    if n.type == "TEX_IMAGE" and n.image: return (n.image, fs.name == "Alpha")
    for i in n.inputs:
        r = trace_img(i, depth+1)
        if r: return r
    return None
BAD = re.compile(r"(nml|normal|_nrm|spe|spec|rim|matcap|shade|_out|bump|rough|metal|gross|gloss|_ao|occl|alpha|emis|height|disp)", re.I)
def mat_src(mat):
    col = (0.8, 0.8, 0.8, 1); cimg = None; aimg = None
    if mat and mat.use_nodes:
        nodes = mat.node_tree.nodes
        b = next((n for n in nodes if n.type == "BSDF_PRINCIPLED"), None)
        if b:
            col = tuple(b.inputs["Base Color"].default_value)
            r = trace_img(b.inputs["Base Color"]); cimg = r[0] if r else None
            ra = trace_img(b.inputs["Alpha"]); aimg = ra
        if cimg is None:
            cands = [n.image for n in nodes if n.type == "TEX_IMAGE" and n.image and not BAD.search(n.image.name)]
            cimg = cands[0] if cands else None
        if aimg is None:
            al = [n.image for n in nodes if n.type == "TEX_IMAGE" and n.image and re.search("alpha", n.image.name, re.I)]
            if al: aimg = (al[0], False)
    elif mat: col = tuple(mat.diffuse_color)
    if cimg is not None and (cimg.size[0] == 0): cimg = None
    return cimg, col, aimg
def alpha_cov(img, use_alpha):
    if img is None or img.size[0] == 0: return 0.0
    c = img.copy(); c.scale(128, 128); a = np.empty(128*128*4, dtype=np.float32); c.pixels.foreach_get(a); bpy.data.images.remove(c)
    a = a.reshape(-1, 4); v = a[:, 3] if use_alpha else a[:, 0]
    return float((v < 0.5).mean())
CUT = re.compile(S.get("cutout_names", r"(hair|lash|brow|eyeline|highlight)"), re.I)
FORCE_OPAQUE = re.compile(S.get("opaque_names", r"^$"), re.I)
def small(img, n=1024):
    c = img.copy(); w, h = img.size; f = min(1.0, n/float(max(w, h))); c.scale(max(1, int(w*f)), max(1, int(h*f)))
    a = np.empty(c.size[0]*c.size[1]*4, dtype=np.float32); c.pixels.foreach_get(a); a = a.reshape(c.size[1], c.size[0], 4); bpy.data.images.remove(c); return a
info = []; amaps = {}
for i, ms in enumerate(lod0.material_slots):
    m = ms.material; cimg, col, ai = mat_src(m)
    if ai is None and cimg is not None and cimg.depth in (32, 64, 128): ai = (cimg, True)
    cov = 0.0
    if ai and not (m and FORCE_OPAQUE.search(m.name)):
        arr = small(ai[0]); v = arr[..., 3] if ai[1] else arr[..., 0]; cov = float((v < 0.5).mean())
        if cov > 0.005: amaps[i] = v
    tint = S.get("tints", {}).get(m.name if m else "", None)
    info.append({"slot": i, "mat": m.name if m else None, "img": cimg.name if cimg else None, "col": [round(x, 3) for x in col[:3]], "alpha": (ai[0].name if ai and i in amaps else None), "alpha_ch": bool(ai and ai[1]), "cov": round(cov, 3), "cut": i in amaps, "tint": tint})
# per-face: a face is cutout when its texels are not fully opaque
face_cut = [False]*len(lod0.data.polygons)
uvl = lod0.data.uv_layers["UVMap"].data
for p in lod0.data.polygons:
    v = amaps.get(p.material_index)
    if v is None: continue
    hh, ww = v.shape; uvs = [uvl[li].uv for li in p.loop_indices]; c = sum((mathutils.Vector(u) for u in uvs), mathutils.Vector((0, 0)))/len(uvs)
    mnv = 1.0
    for u in [c] + [c + (mathutils.Vector(u) - c)*0.75 for u in uvs]:
        x = int((u.x % 1.0)*ww) % ww; y = int((u.y % 1.0)*hh) % hh; mnv = min(mnv, float(v[y, x]))
    if mnv < S.get("cut_thresh", 0.9): face_cut[p.index] = True
REP["faces_cut"] = sum(face_cut)
REP["materials"] = info
for x in info: log("mat", x)

# ---------------- atlas bake (opaque + cutout groups)
sc.render.engine = "CYCLES"; sc.cycles.device = "CPU"; sc.cycles.samples = 1; sc.cycles.use_denoising = False
sc.render.bake.margin = 8; sc.render.bake.use_clear = True
FACEK = re.compile(r"(face|eye|iris|pupil|sclera|brow|mouth|lash|head)", re.I); HAIRK = re.compile("hair", re.I)
def split_group(o, flags):
    bpy.ops.object.select_all(action="DESELECT"); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="DESELECT"); bpy.ops.object.mode_set(mode="OBJECT")
    for p in o.data.polygons: p.select = flags[p.index]
    bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.separate(type="SELECTED"); bpy.ops.object.mode_set(mode="OBJECT")
    return [x for x in bpy.context.selected_objects if x != o][0]
def islands_scale(o, k_face):
    bm = bmesh.new(); bm.from_mesh(o.data); uv = bm.loops.layers.uv.active; bm.faces.ensure_lookup_table()
    par = list(range(len(bm.faces)))
    def find(a):
        while par[a] != a: par[a] = par[par[a]]; a = par[a]
        return a
    for f in bm.faces:
        for l in f.loops:
            e = l.edge
            for l2 in e.link_loops:
                if l2.face == f: continue
                a1 = l[uv].uv; b1 = l.link_loop_next[uv].uv
                a2 = l2.link_loop_next[uv].uv; b2 = l2[uv].uv
                if (a1-a2).length < 1e-5 and (b1-b2).length < 1e-5: par[find(f.index)] = find(l2.face.index)
    groups = {}
    for f in bm.faces: groups.setdefault(find(f.index), []).append(f)
    def uvarea(f):
        ls = [l[uv].uv for l in f.loops]; a = 0.0
        for i in range(1, len(ls)-1): a += abs((ls[i].x-ls[0].x)*(ls[i+1].y-ls[0].y) - (ls[i+1].x-ls[0].x)*(ls[i].y-ls[0].y))*0.5
        return a
    dens = {}
    for k, g in groups.items():
        a3 = sum(f.calc_area() for f in g); au = sum(uvarea(f) for f in g)
        dens[k] = math.sqrt(a3/au) if au > 1e-12 and a3 > 1e-12 else None
    vals = sorted(v for v in dens.values() if v); med = vals[len(vals)//2] if vals else 1.0
    nf = 0
    for k, g in groups.items():
        s = dens[k] if dens[k] else med*0.25
        s = min(max(s, med*0.25), med*4.0)
        hz = sum(1 for f in g if neck is not None and f.calc_center_median().z > neck.z + 0.01*H)
        mats = [o.material_slots[f.material_index].material for f in g]
        named = sum(1 for m in mats if m and FACEK.search(m.name) and not HAIRK.search(m.name))
        hairish = sum(1 for m in mats if m and HAIRK.search(m.name))
        if (hz > 0.5*len(g) and hairish < 0.5*len(g)) or named > 0.5*len(g): nf += 1; s *= k_face
        c = mathutils.Vector((0, 0)); ls = [l for f in g for l in f.loops]
        for l in ls: c += l[uv].uv
        c /= len(ls)
        for l in ls: l[uv].uv = (l[uv].uv - c)*(s/med)
    bm.to_mesh(o.data); bm.free(); return len(groups), nf
def bake_group(o, kind, size):
    o.data.uv_layers["UVMap"].active_render = True
    at = o.data.uv_layers.new(name="atlas", do_init=True); o.data.uv_layers.active = at
    bpy.ops.object.select_all(action="DESELECT"); o.select_set(True); bpy.context.view_layer.objects.active = o
    ni, nfi = islands_scale(o, S.get("face_uv_scale", 1.8))
    bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT"); bpy.ops.uv.select_all(action="SELECT")
    bpy.ops.uv.pack_islands(rotate=True, margin=S.get("uv_margin", 0.004)); bpy.ops.object.mode_set(mode="OBJECT")
    log(kind, "islands", ni, "face islands", nfi)
    img = bpy.data.images.new(NAME + "_" + kind, size, size, alpha=True); img.colorspace_settings.name = "sRGB"
    passes = ["color", "alpha"] if kind == "cut" else ["color"]
    res = {}; orig = [ms.material for ms in o.material_slots]
    for ps in passes:
        for i, ms in enumerate(o.material_slots):
            m = orig[i]
            x = next((y for y in info if y["mat"] == (m.name if m else None)), None)
            nm = bpy.data.materials.new("bake_%s_%d_%s" % (kind, i, ps)); nm.use_nodes = True; nt = nm.node_tree; nt.nodes.clear()
            out = nt.nodes.new("ShaderNodeOutputMaterial"); em = nt.nodes.new("ShaderNodeEmission"); nt.links.new(em.outputs[0], out.inputs[0])
            cimg = bpy.data.images.get(x["img"]) if x and x["img"] else None
            col = x["col"] if x else [0.8, 0.8, 0.8]
            if ps == "color":
                if cimg:
                    uvn = nt.nodes.new("ShaderNodeUVMap"); uvn.uv_map = "UVMap"; tx = nt.nodes.new("ShaderNodeTexImage"); tx.image = cimg; tx.interpolation = "Closest" if S.get("closest") else "Linear"
                    nt.links.new(uvn.outputs[0], tx.inputs[0])
                    if x.get("tint"):
                        mix = nt.nodes.new("ShaderNodeMix"); mix.data_type = "RGBA"; mix.blend_type = "MULTIPLY"; mix.inputs[0].default_value = 1.0
                        nt.links.new(tx.outputs[0], mix.inputs[6]); mix.inputs[7].default_value = list(x["tint"]) + [1]; nt.links.new(mix.outputs[2], em.inputs[0])
                    else: nt.links.new(tx.outputs[0], em.inputs[0])
                else:
                    c = x.get("tint") or col; em.inputs[0].default_value = (c[0], c[1], c[2], 1)
            else:
                ai = bpy.data.images.get(x["alpha"]) if x and x["alpha"] else None
                if ai and x["cut"]:
                    uvn = nt.nodes.new("ShaderNodeUVMap"); uvn.uv_map = "UVMap"; tx = nt.nodes.new("ShaderNodeTexImage"); tx.image = ai; nt.links.new(uvn.outputs[0], tx.inputs[0])
                    if x["alpha_ch"] and ai.alpha_mode == "NONE": ai.alpha_mode = "STRAIGHT"
                    use_a = x["alpha_ch"]
                    nt.links.new(tx.outputs["Alpha" if use_a else "Color"], em.inputs[0])
                else: em.inputs[0].default_value = (1, 1, 1, 1)
            if ps == "color":
                for cv in S.get("cover", []):
                    if not (m and re.search(cv["mat"], m.name, re.I)): continue
                    src = em.inputs[0].links[0].from_socket if em.inputs[0].is_linked else None
                    geo = nt.nodes.new("ShaderNodeNewGeometry"); sep = nt.nodes.new("ShaderNodeSeparateXYZ"); nt.links.new(geo.outputs["Position"], sep.inputs[0])
                    def cmp(sock, op, val):
                        n = nt.nodes.new("ShaderNodeMath"); n.operation = op; nt.links.new(sock, n.inputs[0]); n.inputs[1].default_value = val; return n.outputs[0]
                    def mul(a, b):
                        n = nt.nodes.new("ShaderNodeMath"); n.operation = "MULTIPLY"; nt.links.new(a, n.inputs[0]); nt.links.new(b, n.inputs[1]); return n.outputs[0]
                    ab = nt.nodes.new("ShaderNodeMath"); ab.operation = "ABSOLUTE"; nt.links.new(sep.outputs[0], ab.inputs[0])
                    mk = mul(mul(cmp(ab.outputs[0], "LESS_THAN", cv["x"]*H), cmp(sep.outputs[2], "GREATER_THAN", cv["z0"]*H)), mul(cmp(sep.outputs[2], "LESS_THAN", cv["z1"]*H), cmp(sep.outputs[1], "LESS_THAN", cv.get("y", 0.02)*H)))
                    mix = nt.nodes.new("ShaderNodeMix"); mix.data_type = "RGBA"; nt.links.new(mk, mix.inputs[0])
                    if src: nt.links.new(src, mix.inputs[6])
                    else: mix.inputs[6].default_value = em.inputs[0].default_value
                    mix.inputs[7].default_value = list(cv["color"]) + [1]; nt.links.new(mix.outputs[2], em.inputs[0])
            tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = img; tn.select = True; nt.nodes.active = tn
            ms.material = nm
        bpy.ops.object.bake(type="EMIT", margin=8, use_clear=True)
        a = np.empty(size*size*4, dtype=np.float32); img.pixels.foreach_get(a); res[ps] = a.reshape(size, size, 4).copy()
        for ms in o.material_slots: ms.material = None
    # write file(s)
    from_c = res["color"]
    rgba = from_c.copy()
    if kind == "cut": rgba[..., 3] = res["alpha"][..., 0]
    else: rgba[..., 3] = 1.0
    path = os.path.join(OUT, NAME + "_" + kind + ".png")
    out = bpy.data.images.new("w_" + kind, size, size, alpha=True); out.pixels.foreach_set(rgba.ravel()); out.filepath_raw = path; out.file_format = "PNG"; out.save()
    # one material slot only, uv = atlas
    o.data.uv_layers.remove(o.data.uv_layers["UVMap"]); o.data.uv_layers["atlas"].name = "UVMap"
    mat = bpy.data.materials.new("M_%s_%s" % (NAME, "Opaque" if kind == "opaque" else "Cutout"))
    o.data.materials.clear(); o.data.materials.append(mat)
    for p in o.data.polygons: p.material_index = 0
    return path
ncut = sum(face_cut)
objs = []
if 0 < ncut < len(face_cut):
    cobj = split_group(lod0, face_cut); objs = [(lod0, "opaque"), (cobj, "cut")]
elif ncut: objs = [(lod0, "cut")]
else: objs = [(lod0, "opaque")]
REP["atlas"] = {}
for o, kind in objs:
    REP["atlas"][kind] = bake_group(o, kind, S.get("atlas_size", 2048) if kind == "opaque" else S.get("cut_size", 2048)); REP.setdefault("tris_group", {})[kind] = tris(o)
if len(objs) == 2:
    bpy.ops.object.select_all(action="DESELECT"); objs[0][0].select_set(True); objs[1][0].select_set(True); bpy.context.view_layer.objects.active = objs[0][0]; bpy.ops.object.join()
lod0 = bpy.context.view_layer.objects.active if len(objs) == 2 else objs[0][0]; lod0.name = NAME + "_LOD0"
REP["tris_after_clean"] = tris(lod0)
# ---------------- LOD0 cap, weights, LOD1/LOD2
if tris(lod0) > S.get("lod0_max", 40000): decimate(lod0, target=S.get("lod0_target", 38000))
REP["tris_lod0"] = tris(lod0)
def clean_weights(o):
    bpy.ops.object.select_all(action="DESELECT"); o.select_set(True); bpy.context.view_layer.objects.active = o
    for gn in ("_protect", "_eyes"):
        g = o.vertex_groups.get(gn)
        if g: o.vertex_groups.remove(g)
    bpy.ops.object.vertex_group_clean(group_select_mode="ALL", limit=0.01)
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode="ALL", lock_active=False)
lods = [lod0]
for li, tgt in [(1, S.get("lod1", 15000)), (2, S.get("lod2", 5000))]:
    c = lod0.copy(); c.data = lod0.data.copy(); sc.collection.objects.link(c); c.name = NAME + "_LOD%d" % li; c.data.name = c.name
    decimate(c, target=tgt, factor=0.6 if li == 1 else 0.3); lods.append(c)
    if tris(c) > 1.25*tgt: decimate(c, target=tgt, protect=False)
    if tris(c) > 1.25*tgt:
        bm = bmesh.new(); bm.from_mesh(c.data); bm.verts.ensure_lookup_table(); seen = set(); kill = []
        for v in bm.verts:
            if v.index in seen: continue
            st = [v]; seen.add(v.index); vs = [v]
            while st:
                x = st.pop()
                for e in x.link_edges:
                    w = e.other_vert(x)
                    if w.index not in seen: seen.add(w.index); st.append(w); vs.append(w)
            cs = [x.co for x in vs]; mnv = mathutils.Vector([min(q[i] for q in cs) for i in range(3)]); mxv = mathutils.Vector([max(q[i] for q in cs) for i in range(3)])
            if (mxv - mnv).length < S.get("small_part", 0.05)*H and (neck is None or mxv.z < neck.z): kill += vs
        bmesh.ops.delete(bm, geom=kill, context="VERTS"); bm.to_mesh(c.data); bm.free(); log("LOD%d small parts removed" % li, len(kill), "verts")
        decimate(c, target=tgt, protect=False)
for o in lods: clean_weights(o)
REP["tris_lods"] = [tris(o) for o in lods]
# ---------------- names, export
arm.name = NAME + "_Skel"; arm.data.name = NAME + "_Skel"
pre = S.get("bone_prefix_swap")
if pre:
    for b in arm.data.bones:
        if b.name.startswith(pre[0]): b.name = pre[1] + b.name[len(pre[0]):]
REP["bones"] = len(arm.data.bones)
for o in lods:
    for m in o.modifiers:
        if m.type == "ARMATURE": m.object = arm
    o.parent = arm
def export(path, objs_):
    for x in [arm] + list(objs_):
        x.hide_viewport = False; x.hide_set(False)
        if not x.visible_get() and x.name not in bpy.context.scene.collection.objects: bpy.context.scene.collection.objects.link(x)
        for col in x.users_collection: col.hide_viewport = False
    log("export visible", [(x.name, x.visible_get()) for x in [arm] + list(objs_)])
    bpy.ops.object.select_all(action="DESELECT"); arm.select_set(True)
    for o in objs_: o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"ARMATURE", "MESH"}, add_leaf_bones=False, bake_anim=False,
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", use_armature_deform_only=False,
        primary_bone_axis="Y", secondary_bone_axis="X", path_mode="STRIP", embed_textures=False, use_mesh_modifiers=False)
    return os.path.getsize(path)
REP["fbx"] = {NAME + ".fbx": export(os.path.join(OUT, NAME + ".fbx"), [lods[0]]), NAME + "_LOD12.fbx": export(os.path.join(OUT, NAME + "_LOD12.fbx"), lods[1:])}
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, NAME + "_prep.blend"), compress=True)
# preview render (Cycles, flat emission of the atlas, alpha clip honoured)
for o in lods[1:]: o.hide_render = True
for o in lods:
    for ms in o.material_slots:
        m = ms.material; m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
        kind = "opaque" if m.name.endswith("Opaque") else "cut"; im = bpy.data.images.load(REP["atlas"][kind], check_existing=True)
        out = nt.nodes.new("ShaderNodeOutputMaterial"); tx = nt.nodes.new("ShaderNodeTexImage"); tx.image = im; tx.interpolation = "Linear"
        em = nt.nodes.new("ShaderNodeEmission"); nt.links.new(tx.outputs[0], em.inputs[0])
        if kind == "cut":
            tr = nt.nodes.new("ShaderNodeBsdfTransparent"); mx = nt.nodes.new("ShaderNodeMixShader"); gt = nt.nodes.new("ShaderNodeMath"); gt.operation = "GREATER_THAN"; gt.inputs[1].default_value = 0.5
            nt.links.new(tx.outputs[1], gt.inputs[0]); nt.links.new(gt.outputs[0], mx.inputs[0]); nt.links.new(tr.outputs[0], mx.inputs[1]); nt.links.new(em.outputs[0], mx.inputs[2]); nt.links.new(mx.outputs[0], out.inputs[0])
        else: nt.links.new(em.outputs[0], out.inputs[0])
sc.render.engine = "CYCLES"; sc.cycles.samples = 8; sc.cycles.max_bounces = 2; sc.cycles.transparent_max_bounces = 16
w = sc.world or bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True; w.node_tree.nodes["Background"].inputs[0].default_value = (0.25, 0.25, 0.27, 1)
sc.render.image_settings.file_format = "PNG"; sc.render.resolution_x = 700; sc.render.resolution_y = 1000; sc.render.film_transparent = False
sc.view_settings.view_transform = "Standard"
cd = bpy.data.cameras.new("c"); cd.type = "ORTHO"; co = bpy.data.objects.new("c", cd); sc.collection.objects.link(co); sc.camera = co
views = [(0, H*1.08, H/2), (60, H*1.08, H/2), (180, H*1.08, H/2), (0, H*0.16, H*0.925), (35, H*0.16, H*0.925)]
for i, (ang, os_, z) in enumerate(views):
    cd.ortho_scale = os_; a = math.radians(ang); co.location = (math.sin(a)*6, -math.cos(a)*6, z); co.rotation_euler = (math.radians(90), 0, a)
    if i >= 3: sc.render.resolution_x = 600; sc.render.resolution_y = 600
    sc.render.filepath = os.path.join(OUT, "preview_%d.png" % i); bpy.ops.render.render(write_still=True)
REP["seconds"] = round(time.time()-T0)
json.dump(REP, open(os.path.join(OUT, "report.json"), "w"), indent=1)
log("DONE", REP["tris_lods"], REP["fbx"])
