#!/usr/bin/env python3
"""Agni Royale — hero texture fixes (LOOK POLISH v2, 2026-11-01).

Why: the hair-v3 heroes' clothes atlas (<Name>_clothes.png) was painted with shapes that are offset/shrunk from the mesh's
UV islands, so every garment edge sampled the background colour around its island -> red/black streaks along jackets and tops
(visible in Unity and, softer, in the Blender renders). Mip-mapping made it worse (no gutter padding).

What it does (on the Unity COPIES in Assets/Agni/Import/Heroes/<Name>/, never on characters_hair_v3/):
 1. clothes: per UV island (LOD0 clothes faces, islands = faces sharing UV coords) take the island's dominant colour; any
    off-colour region that touches the island border AND matches the colour lying just outside the island is a leak ->
    filled from the nearest kept pixel. Thin (<=4 px) jagged lines inside a panel are removed too. Then every pixel outside
    the islands is padded with the nearest island colour (mip-safe, also covers LOD1/2 UV drift).
 2. Meera: her black bodice island is recoloured teal (46,112,120) (teen-safe: the near-black top read as a dark blob).
 3. skin: midriff band (z 52-75 % of height, |x| < 0.2 m) painted with the top colour for Meera/Zoya/Rani (tops reach the skirt);
    feet (z < 9 % of height) painted dark boot colour for the 8 barefoot heroes (no shoe mesh exists in the source).

Usage: python3 fix_hero_textures.py <dump dir from blender_dump.py> <Assets/Agni/Import/Heroes>
  blender -b characters_hair_v3/<N>/<N>_HairV3.blend --python blender_dump.py -- <N> <dump dir>
Needs numpy, scipy, opencv-python, pillow.
"""
import json, sys, os
import numpy as np, cv2
from PIL import Image
from scipy import ndimage as ndi

def islands_from_polys(polys):
    parent = {}
    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]; x = parent[x]
        return x
    keys = []
    for p in polys:
        ks = [(round(u, 5), round(v, 5)) for u, v in p]
        for k in ks: parent.setdefault(k, k)
        for k in ks[1:]:
            a, b = find(ks[0]), find(k)
            if a != b: parent[a] = b
        keys.append(ks[0])
    roots = [find(k) for k in keys]; ids = {r: i for i, r in enumerate(dict.fromkeys(roots))}
    return [ids[r] for r in roots]

def raster_islands(polys, w, h):
    isl = islands_from_polys(polys); n = max(isl) + 1
    masks = [np.zeros((h, w), np.uint8) for _ in range(n)]
    for p, k in zip(polys, isl):
        pts = np.round(np.array([[u * w - 0.5, (1 - v) * h - 0.5] for u, v in p], np.float32) * 16).astype(np.int32)
        cv2.fillPoly(masks[k], [pts], 1, lineType=cv2.LINE_8, shift=4); cv2.polylines(masks[k], [pts], True, 1, 1, lineType=cv2.LINE_8, shift=4)
    return [m.astype(bool) for m in masks]

def fix_clothes(tex_path, polys, out_path, recolor=None):
    img = np.array(Image.open(tex_path).convert('RGBA')); h, w = img.shape[:2]
    rgb = img[..., :3].astype(np.int32); out = rgb.copy()
    masks = raster_islands(polys, w, h); anyM = np.zeros((h, w), bool)
    for m in masks: anyM |= m
    for k, M in enumerate(masks):
        if M.sum() < 30: continue
        core = ndi.binary_erosion(M, iterations=6)
        if core.sum() < 10: core = M
        q = rgb[core] // 12; vals, cnt = np.unique(q, axis=0, return_counts=True); dq = vals[cnt.argmax()]
        dom = rgb[core][(q == dq).all(1)].mean(0)
        ring = ndi.binary_dilation(M, iterations=5) & ~anyM; outcols = rgb[ring]
        nondom = M & (np.sqrt(((rgb - dom) ** 2).sum(-1)) > 45)
        border = M & ~ndi.binary_erosion(M, iterations=2)
        dl, nd = ndi.label(nondom); fill = np.zeros_like(M)
        for c in range(1, nd + 1):
            comp = dl == c
            if not (comp & border).any():
                if (not ndi.binary_erosion(comp, iterations=2).any() and comp.sum() < 0.03 * M.sum()) or comp.sum() < 0.004 * M.sum(): fill |= comp
                continue
            if len(outcols) == 0: continue
            mc = rgb[comp].mean(0)
            if (np.sqrt(((outcols - mc) ** 2).sum(-1)) < 50).mean() > 0.15: fill |= comp
        if fill.any():
            keep = M & ~fill; idx = ndi.distance_transform_edt(~keep, return_distances=False, return_indices=True)
            out[fill] = out[idx[0][fill], idx[1][fill]]
    for k, (maxlum, col) in (recolor or {}).items():
        sel = masks[k] & (out.max(-1) < maxlum); l = out[sel].mean(-1, keepdims=True); l0 = max(l.mean(), 1)
        out[sel] = np.clip(np.array(col)[None, :] * (0.92 + 0.08 * np.clip(l / l0, 0, 2)), 0, 255).astype(np.int32)
    pad = ~anyM; idx = ndi.distance_transform_edt(pad, return_distances=False, return_indices=True)
    out[pad] = out[idx[0][pad], idx[1][pad]]
    Image.fromarray(np.dstack([out.astype(np.uint8), img[..., 3]])).save(out_path)

def paint_polys(img, polys, color, dil=2):
    h, w = img.shape[:2]; m = np.zeros((h, w), np.uint8)
    for p in polys:
        pts = np.round(np.array([[u * w - 0.5, (1 - v) * h - 0.5] for u, v in p], np.float32) * 16).astype(np.int32)
        cv2.fillPoly(m, [pts], 1, lineType=cv2.LINE_8, shift=4)
    if dil: m = cv2.dilate(m, np.ones((3, 3), np.uint8), iterations=dil)
    M = m.astype(bool); l = img[M, :3].astype(float).mean(1, keepdims=True); l0 = max(l.mean(), 1)
    img[M, :3] = np.clip(np.array(color, float)[None, :] * (0.93 + 0.07 * np.clip(l / l0, 0, 2)), 0, 255).astype(np.uint8)

BAREFOOT = ['Meera', 'Laila', 'Arjun', 'Kabir', 'DJ_Taal', 'Dr_Sameer', 'Chotu', 'Bheem']
MIDRIFF = {'Meera': (46, 112, 120), 'Zoya': (40, 42, 62), 'Rani': (201, 30, 50)}
RECOLOR = {'Meera': {0: (80, (46, 112, 120))}}

if __name__ == '__main__':
    dump, heroes = sys.argv[1], sys.argv[2]
    for n in ['Meera', 'Laila', 'Zoya', 'Rani', 'Arjun', 'Kabir', 'DJ_Taal', 'Dr_Sameer', 'Chotu', 'Bheem']:
        d = json.load(open(f'{dump}/{n}.json')); hd = f'{heroes}/{n}'
        fix_clothes(f'{hd}/{n}_clothes.png', d['clothes_uv_lod0'], f'{hd}/{n}_clothes.png', RECOLOR.get(n))
        cl = np.array(Image.open(f'{hd}/{n}_clothes.png').convert('RGB')).astype(float); ch, cw = cl.shape[:2]
        sk = np.array(Image.open(f'{hd}/{n}_skin.png').convert('RGBA'))
        if n in BAREFOOT:
            shin = [cl[int((1 - v) * ch) % ch, int(u * cw) % cw] for u, v in d['shin_uv']]
            sc = np.median(np.array(shin), 0) if shin else np.array([30, 26, 24])
            paint_polys(sk, [e['uv'] for e in d['feet']], np.clip(0.5 * sc * 0.75 + 0.5 * np.array([42, 33, 29]), 0, 255))
        if n in MIDRIFF: paint_polys(sk, [e['uv'] for e in d['mid']], MIDRIFF[n], dil=1)
        Image.fromarray(sk).save(f'{hd}/{n}_skin.png'); print('fixed', n)
