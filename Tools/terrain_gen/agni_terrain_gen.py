"""Agni Royale - India map terrain generator (step 1 blockout).

Writes TerrainSource/ (16 heightmap tiles 513x513 uint16 gz, 16 splat tiles 512x512 per-layer uint8 gz, map.json)
which Assets/Editor/AgniWorldBuilder.cs turns into the Unity scene (menu: Agni Royale -> Rebuild map scene).

Coastline: Natural Earth 1:10m Admin-0 countries, India point of view (ne_10m_admin_0_countries_ind) - public domain.
  https://naciscdn.org/naturalearth/10m/cultural/ne_10m_admin_0_countries_ind.zip
Rivers:    Natural Earth 1:10m rivers + lake centerlines (public domain); Hooghly added by hand.
  https://naciscdn.org/naturalearth/10m/physical/ne_10m_rivers_lake_centerlines.zip
Needs: numpy scipy pillow pyshp shapely.   Run: python agni_terrain_gen.py <natural_earth_dir> <project_dir>
This file was assembled from the working session that produced the shipped data; the functions below are the
exact ones used. World: x east / z north, metres, 0..4096; sea level 0; terrain base -12, height range 200.
"""
import sys, os, json, math, gzip
import numpy as np
from scipy import ndimage
from scipy.sparse import coo_matrix
from scipy.sparse.csgraph import dijkstra
from PIL import Image, ImageDraw
import shapefile
from shapely.geometry import shape, Point

N=2049; CELL=2.0; SIZE=4096.0; BASE=-12.0; RANGE=200.0
# projection (computed from the India mainland bounds)
lonc=82.75282779600005; latc=22.56523936250006; COS=0.9271838545667874; K=131.82192090006833
def proj(lon,lat): return (2048+(lon-lonc)*COS*K, 2048+(lat-latc)*K)
def unproj(x,z): return (lonc+(x-2048)/(COS*K), latc+(z-2048)/K)
ZONES=[(1, 'Himgiri Ridge', 'Ladakh / Himalaya', 77.6, 34.15, 'snow', 70), (2, 'Sarson Khet', 'Punjab mustard farms', 75.6, 30.9, 'farm', 70), (3, 'Vijay Dwar Chowk', 'Delhi', 77.2, 28.6, 'city', 80), (4, 'Gulabi Bazaar', 'Jaipur', 75.8, 26.9, 'bazaar', 70), (5, 'Sangmarmar Bagh', 'Agra', 78.05, 27.15, 'garden', 70), (6, 'Ghat Nagari', 'Varanasi', 83.0, 25.25, 'ghat', 70), (7, 'Ghanta Ghar Square', 'Kolkata', 88.3, 22.6, 'city', 80), (8, 'Chai Bagan', 'Assam tea gardens', 93.6, 26.55, 'tea', 70), (9, 'Rann Safed', 'Kutch white desert', 70.2, 23.9, 'desert', 70), (10, 'Vindhya Shikhar', 'Central India hill', 79.25, 23.95, 'peak', 38), (11, 'Pathar Gaon', 'Stone village', 76.85, 23.75, 'village', 60), (12, 'Bambai Docks & Mill', 'Mumbai', 72.95, 19.1, 'docks', 80), (13, 'Char Minar Chowk', 'Hyderabad', 78.47, 17.36, 'city', 75), (14, 'Sunehri Beach', 'Goa', 73.95, 15.4, 'beach', 65), (15, 'Rajmahal', 'Mysore palace', 76.65, 12.3, 'palace', 75), (16, 'Kayal Village', 'Kerala backwaters', 76.45, 9.55, 'water', 65)]
SPOTS=[('Anaj Godown', 76.5, 30.25, 'shed'), ('Radio Mast', 72.75, 24.65, 'mast'), ('Bijli Ghar', 85.4, 22.9, 'power'), ('Purana Hawai Adda', 80.6, 19.2, 'airstrip'), ('Khandahar', 76.46, 15.33, 'ruins')]
COASTAL={16, 12, 14}
LAYERS=['sand', 'grass', 'dry', 'rock', 'snow', 'mustard']
FRONT=[(72.0, 33.5), (73.2, 33.0), (74.2, 32.75), (75.3, 32.3), (76.3, 31.6), (77.5, 30.6), (78.6, 29.9), (80.0, 29.0), (81.5, 28.5), (83.5, 27.8), (85.5, 27.35), (88.0, 27.0), (89.5, 26.85), (92.0, 26.95), (94.0, 27.45), (95.5, 27.75), (96.8, 27.95), (98.5, 28.1)]   # Himalaya foothill front line (lon,lat)
HOOGHLY=[(87.95, 24.85), (88.15, 24.2), (88.3, 23.6), (88.4, 23.0), (88.33, 22.6), (88.15, 22.1), (88.1, 21.75)]
RIVER_SEL={'Ganges': ('Ganga', 18), 'Yamuna': ('Yamuna', 11), 'Narmada': ('Narmada', 12), 'Godävari': ('Godavari', 12), 'Krishna': ('Krishna', 11), 'Brahmaputra': ('Brahmaputra', 16), 'Dihang': ('Brahmaputra', 12)}

# ---- pipeline (order used for the shipped data) ----
# 1. load NE India-POV polygon (largest part = mainland, + parts with area>0.004 deg2 north of 9.5N = Andaman etc.)
# 2. raster land mask on the 2049 grid, distance fields dc (inside) / ds (outside)
# 3. noise fields: n_low=fbm(8,4,11) n_mid=fbm(24,3,21) n_hi=fbm(64,2,31) ridged=1-|fbm(14,4,41)|
# 4. dmtn = distance into the mountain polygon (FRONT + north edge); rivers from NE (RIVER_SEL) clipped at dmtn>25 + HOOGHLY,
#    rasterised -> rv (distance) and rw (width)
# 5. zone positions: ZP[id]=settle2(proj(lon,lat), min coast distance, river clearance); spots likewise
# 6. h = build_height4() - 10*exp(-(dwg/60)^2)   (Western Ghats lowered);  H = finish6(h)
# 7. W = splat4(H); write tiles; roads = Dijkstra routes on an 8 m cost grid between zone nodes (route()), smoothed + densified;
#    bridges where a road runs over water (wet_runs < 0.8 m)


def vnoise(cells, seed):
    r=np.random.default_rng(seed); g=r.random((cells+3,cells+3)).astype(np.float32)
    z=ndimage.zoom(g, (N+ (N//cells)*3)/ (cells+3), order=3)
    off=N//cells
    return z[off:off+N, off:off+N]


def fbm(base_cells, octaves, seed, gain=0.5):
    s=np.zeros((N,N),np.float32); a=1; tot=0
    for o in range(octaves):
        s+=a*(vnoise(base_cells*2**o, seed+o)-0.5); tot+=a; a*=gain
    return s/tot*2  # ~[-1,1]


def sstep(e0,e1,x):
    t=np.clip((x-e0)/(e1-e0),0,1); return t*t*(3-2*t)


def poly_mask(lonlat_pts):
    img=Image.new('L',(N,N),0); ImageDraw.Draw(img).polygon([(proj(*p)[0]/CELL,proj(*p)[1]/CELL) for p in lonlat_pts],fill=255)
    return np.array(img)>127


def line_dist(lonlat_pts):
    img=Image.new('L',(N,N),255); ImageDraw.Draw(img).line([(proj(*p)[0]/CELL,proj(*p)[1]/CELL) for p in lonlat_pts],fill=0,width=1)
    return ndimage.distance_transform_edt(np.array(img)>0)*CELL


def raster_polys(geoms, scale=1):
    img=Image.new('L',(N*scale,N*scale),0); dr=ImageDraw.Draw(img)
    for g in geoms:
        for poly in (g.geoms if hasattr(g,'geoms') else [g]):
            pts=[(proj(*c)[0]/CELL*scale, proj(*c)[1]/CELL*scale) for c in poly.exterior.coords]
            dr.polygon(pts,fill=255)
            for h in poly.interiors:
                dr.polygon([(proj(*c)[0]/CELL*scale, proj(*c)[1]/CELL*scale) for c in h.coords],fill=0)
    return np.array(img)>127   # row index = z


def settle(lon,lat,mind):
    x,z=proj(lon,lat); i,j=int(round(z/2)),int(round(x/2))
    if dc[i,j]>=mind: return x,z
    # nearest pixel with dc>=mind
    r=120
    sub=dc[i-r:i+r,j-r:j+r]; ii,jj=np.where(sub>=mind)
    k=np.argmin((ii-r)**2+(jj-r)**2); return (j-r+jj[k])*2.0,(i-r+ii[k])*2.0


def settle2(x,z,mind,rclear=35):
    i,j=int(round(z/2)),int(round(x/2))
    ok=(dc>=mind)&(rv>=rw/2+rclear)
    if ok[i,j]: return x,z
    r=150; sub=ok[i-r:i+r,j-r:j+r]; ii,jj=np.where(sub)
    k=np.argmin((ii-r)**2+(jj-r)**2); return (j-r+jj[k])*2.0,(i-r+ii[k])*2.0


def blob(lon,lat,rad_m):
    x,z=proj(lon,lat); return np.exp(-(((X-x)**2+(Z-z)**2)/(rad_m**2)))


def build_height3():
    h=np.full((N,N),5.0,np.float32)+2.0*n_low+0.6*n_mid
    thar=sstep(0,1, np.clip((73.8-LON)/1.5,0,1))*sstep(0,1,np.clip((LAT-24.2)/0.6,0,1))*sstep(0,1,np.clip((30.2-LAT)/0.6,0,1))
    dunes=1.2*np.sin((X*0.06+Z*0.025)+3*n_mid)*(0.5+0.5*n_hi)
    h=h*(1-thar)+ (4.5+dunes+0.8*n_low)*thar
    dar=line_dist([(72.3,24.2),(73.4,25.4),(74.6,26.4),(75.8,27.5),(76.6,28.3)]); h+= (16+6*n_mid)*np.exp(-(dar/55)**2)
    cen=sstep(20.5,22.0,LAT)*(1-sstep(24.8,25.6,LAT))*sstep(73.8,75.0,LON)*(1-sstep(83.5,85.5,LON)); h+= 8*cen
    dv=line_dist([(74.6,23.0),(76.5,23.6),(78.2,24.0),(80.5,24.4),(82.5,24.4)]); h+= (14+5*n_mid)*np.exp(-(dv/45)**2)
    dsat=line_dist([(74.2,21.6),(76.0,21.5),(78.0,21.9),(80.2,22.4),(81.5,22.7)]); h+= (16+5*n_mid)*np.exp(-(dsat/45)**2)
    dec=(1-sstep(21.3,22.3,LAT))*sstep(40,220,dc); h+= dec*(14+6*n_low)
    dwg=line_dist([(73.55,21.2),(73.5,19.2),(73.75,17.2),(74.05,15.6),(74.7,13.6),(75.7,11.6),(76.95,10.1),(77.35,8.55)]); h+= (34+10*ridged-5)*np.exp(-(dwg/60)**2)
    deg=line_dist([(86.0,20.8),(84.4,19.2),(83.0,18.1),(81.2,16.6),(79.7,14.8),(79.2,13.2),(78.5,12.2)]); h+= (18+6*n_mid)*np.exp(-(deg/55)**2)
    ne=sstep(89.8,90.6,LON)*(1-sstep(25.7,26.3,LAT)); h+= ne*(22+14*ridged+5*n_mid)
    m=sstep(0,320,dmtn)
    hm= 20*sstep(0,60,dmtn) + m*(150+40*(ridged-0.5)*1.2+15*n_low)
    h=np.maximum(h, h*(1-sstep(0,40,dmtn)) + hm)
    px,pz=ZP[10]; r=np.hypot(X-px,Z-pz); peak=1-sstep(30,290,r)
    h=np.maximum(h, 8+ 88*peak**1.5 + 3*n_hi*(r>40))
    return h


def build_height4():
    # same as 3 but without peak (added after rivers)
    global ZP
    saved=ZP[10]; ZP[10]=(-9999,-9999); h=_b3(); ZP[10]=saved; return h


def apply_rivers(h):
    half=rw/2
    v=sstep(half+6, half+170, rv)
    bank=3.0
    h=bank+(h-bank)*v
    ch=sstep(half*0.75, half+4, rv)
    bed=-1.6
    h=np.minimum(h, bed+(bank+0.2-bed)*ch + (h-bank-0.2)*(ch>=1))
    return h


def finish5(h):
    h=apply_rivers(h)
    px,pz=ZP[10]; r=np.hypot(X-px,Z-pz); peak=1-sstep(30,280,r)
    keep_ch=sstep(rw/2+6, rw/2+70, rv)
    h=np.maximum(h, (8+88*peak**1.5+3*n_hi*(r>40))*keep_ch)
    rann=poly_mask([(68.9,23.55),(69.6,24.45),(70.8,24.55),(71.4,24.2),(71.2,23.65),(70.2,23.45)])
    rs=ndimage.gaussian_filter(rann.astype(np.float32),8); h=h*(1-rs)+1.3*rs
    L=40+1.35*np.maximum(h-3,0)
    h=np.where(land, np.where(h<0.5, h, 0.5+(h-0.5)*sstep(0,1,dc/L)), h)
    def pad(h,px,pz,R,mode='median',soft=90,fixed=None):
        r=np.hypot(X-px,Z-pz); sel=r<25
        tgt=fixed if fixed is not None else (float(h[sel].max()) if mode=='max' else float(np.median(h[sel])))
        tgt=max(tgt,2.4)
        w=1-sstep(R,R+soft,r); w=w*sstep(0,8,rv-rw/2); return h*(1-w)+tgt*w
    for zz in ZONES:
        R=zz[6]; soft=90; fixed=None
        if zz[0]==10: soft=30
        if zz[0]==1: R,soft=40,70
        if zz[0] in COASTAL: fixed=2.8; soft=120
        h=pad(h,*ZP[zz[0]],R,mode=('max' if zz[0]==10 else 'median'),soft=soft,fixed=fixed)
    for s in SPOTS: h=pad(h,*SP[s[0]],60 if 'Hawai' in s[0] else 32,soft=60)
    sea=-0.6-7.5*sstep(0,90,ds)+0.6*n_mid
    h=np.where(land,h,sea)
    return ndimage.gaussian_filter(h,0.8)


def finish6(h):
    H=finish5(h)
    sea=-1.3-7.0*sstep(0,90,ds)+0.3*n_mid
    return np.where(land,H,np.minimum(H,sea))


def splat4(H):
    gz,gx=np.gradient(H,CELL); sl=np.degrees(np.arctan(np.hypot(gx,gz)))
    W=np.zeros((6,N,N),np.float32)
    dry=np.clip(((1-sstep(21.5,23.0,LAT))*0.75 + sstep(0,1,(75.5-LON)/2)*sstep(23,25,LAT)*0.7 + 0.35*n_mid),0,1)
    dry*= (1-sstep(83.5,85.5,LON)); dry*= 1-0.8*(1-sstep(11.5,13.0,LAT))*(1-sstep(76.6,77.8,LON))
    thar=sstep(0,1, np.clip((73.8-LON)/1.2,0,1))*sstep(24.0,24.8,LAT)*(1-sstep(29.6,30.4,LAT))
    beach=(1-sstep(8,22,dc))*(1-sstep(2.5,4.5,H))
    river_bank=(1-sstep(rw/2+1,rw/2+5,rv))
    sand=np.maximum(thar*0.9, np.maximum(beach, river_bank*0.9)); sand=np.where(~land,1,sand)
    mus=sstep(73.9,74.6,LON)*(1-sstep(77.2,77.9,LON))*sstep(29.0,29.6,LAT)*(1-sstep(31.6,32.3,LAT))*(1-sstep(15,22,H))
    mus=mus*sstep(-0.15,0.15,n_hi+0.25*n_mid)
    snow=sstep(70,90,H+8*n_mid)*(dmtn>0)
    rann=poly_mask([(68.9,23.55),(69.6,24.45),(70.8,24.55),(71.4,24.2),(71.2,23.65),(70.2,23.45)])
    snow=np.maximum(snow, ndimage.gaussian_filter(rann.astype(np.float32),5)*0.95)
    rock=sstep(24,36,sl)
    px,pz=ZP[10]; rpk=np.hypot(X-px,Z-pz)
    rock=np.maximum(rock,(1-sstep(60,220,rpk))*0.75*(rpk>34))
    rock=np.maximum(rock, sstep(30,60,H)*(dmtn>0)*(1-snow)*0.8)
    W[4]=snow; rem=1-W[4]
    W[3]=rock*rem; rem-=W[3]; W[0]=sand*rem; rem-=W[0]; W[5]=mus*rem; rem-=W[5]; W[2]=dry*0.85*rem; rem-=W[2]; W[1]=rem
    W=np.clip(W,0,1); W/=W.sum(0,keepdims=True); return W


def densify(pts,step=4.0):
    pts=np.array(pts,float)
    # chaikin smoothing x2 keeping endpoints
    for _ in range(3):
        q=[pts[0]]
        for a,b in zip(pts[:-1],pts[1:]): q+= [0.75*a+0.25*b, 0.25*a+0.75*b]
        q.append(pts[-1]); pts=np.array(q)
    out=[pts[0]]
    for a,b in zip(pts[:-1],pts[1:]):
        d=np.linalg.norm(b-a); n=max(1,int(d/step))
        for k in range(1,n+1): out.append(a+(b-a)*k/n)
    return np.array(out)


def smooth_path(pts,win=9):
    P=np.array(pts,float)
    if len(P)<win: return P
    k=np.ones(win)/win
    Q=np.vstack([np.convolve(np.pad(P[:,c],(win//2,win//2),mode='edge'),k,mode='valid') for c in (0,1)]).T
    Q[0]=P[0];Q[-1]=P[-1]; return Q


def wet_runs(P,thr=1.0):
    runs=[];cur=[]
    for i,(x,z) in enumerate(P):
        if hat(x,z)<thr: cur.append(i)
        elif cur: runs.append(cur);cur=[]
    if cur: runs.append(cur)
    return runs


def route(a,b):
    ia=idx[int(a[1]/2/S),int(a[0]/2/S)]; ib=idx[int(b[1]/2/S),int(b[0]/2/S)]
    d,pred=dijkstra(G,indices=ia,return_predecessors=True,limit=np.inf)
    p=[ib]
    while p[-1]!=ia and p[-1]>=0: p.append(pred[p[-1]])
    p=p[::-1]
    return [((k%n)*2*S+S, (k//n)*2*S+S) for k in p]


def hat(x,z): 
    return float(ndimage.map_coordinates(H,[[z/2],[x/2]],order=1)[0])

