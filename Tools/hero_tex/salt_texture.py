# Agni Royale — Rann salt-crust terrain texture (T_Salt.png, 1024^2, tileable): periodic Voronoi cracks with domain warp,
# warm off-white mottling, alpha 18/255 (URP TerrainLit reads smoothness from albedo alpha). Generated 2026-11-01 (LOOK POLISH v2).
# Used by L_salt.terrainlayer; AgniIndiaTerrain swaps it in for the "snow" splat outside the Himalaya tile row (= the Rann of Kutch).
import sys, numpy as np
from PIL import Image
from scipy.spatial import cKDTree
N = 1024; rng = np.random.default_rng(7); npts = 90
pts = rng.random((npts, 2)) * N
tree = cKDTree(np.concatenate([pts + np.array([dx, dy]) * N for dx in (-1, 0, 1) for dy in (-1, 0, 1)]))
def pnoise(scale, seed):
    g = np.random.default_rng(seed).random((scale, scale)); g2 = np.tile(g, (3, 3))
    b = np.array(Image.fromarray((g2 * 255).astype(np.uint8)).resize((3 * N, 3 * N), Image.BICUBIC)).astype(float) / 255
    return b[N:2 * N, N:2 * N]
yy, xx = np.mgrid[0:N, 0:N]; q = np.stack([xx.ravel() + 0.5, yy.ravel() + 0.5], 1)
w1 = pnoise(8, 1) - 0.5; w2 = pnoise(8, 2) - 0.5
d, i = tree.query(q + np.stack([w1.ravel(), w2.ravel()], 1) * 40, k=2)
edge = (d[:, 1] - d[:, 0]).reshape(N, N); ridge = np.exp(-(edge / 3.0) ** 2); ridge2 = np.exp(-(edge / 9.0) ** 2) * 0.5
cellshade = (np.random.default_rng(3).random(npts) * 0.06 - 0.03)[(i[:, 0] % npts).reshape(N, N)]
mott = (pnoise(16, 5) - 0.5) * 0.10 + (pnoise(48, 6) - 0.5) * 0.06 + (pnoise(128, 9) - 0.5) * 0.04
base = np.array([226, 221, 209], float) / 255
col = base[None, None, :] * (1 + mott + cellshade - 0.10 * ridge2)[..., None]
sh = np.roll(ridge, 3, axis=0); col = col * (1 - 0.18 * np.clip(sh - ridge, 0, 1)[..., None]) + 0.10 * ridge[..., None]
col = np.clip(np.clip(col, 0, 1) * np.array([0.97, 0.96, 0.95])[None, None, :] * (1 + 0.08 * (pnoise(6, 11) - 0.5))[..., None], 0, 1)
Image.fromarray(np.dstack([(col * 255).astype(np.uint8), np.full((N, N), 18, np.uint8)]), 'RGBA').save(sys.argv[1] if len(sys.argv) > 1 else 'T_Salt.png')
