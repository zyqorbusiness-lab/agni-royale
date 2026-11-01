
import sys, math, os
sys.path.insert(0,'/home/user/tab/files/zyqor_game/design/intro_src')
os.chdir('/home/user/tab/files/zyqor_game/design/intro_src')
import skia, numpy as np
import render_intro as RI
from PIL import Image, ImageFilter, ImageDraw
OUT=sys.argv[1]; os.makedirs(OUT,exist_ok=True)
def C(h,a=255):
    h=h.lstrip('#'); return skia.ColorSetARGB(int(a),int(h[0:2],16),int(h[2:4],16),int(h[4:6],16))

# ---- mascot sprites (transparent)
def mascot_png(name, t, over):
    W,H=1000,1100
    surf=skia.Surface(W,H); c=surf.getCanvas(); c.clear(skia.ColorTRANSPARENT)
    st=dict(RI.state(t)); st.update(over)
    # place mascot centred: mascot draws at st['x'] and ground RI.G
    c.translate(W/2,H-40); c.scale(2,2); c.translate(-st['x'],-RI.G)
    RI.mascot(c,t,st)
    a=surf.makeImageSnapshot().toarray(colorType=skia.kRGBA_8888_ColorType)
    im=Image.fromarray(a,'RGBA'); bb=im.getbbox(); im=im.crop(bb)
    im.save(os.path.join(OUT,name+'.png')); return im.size
base=dict(h=0,sx=1,sy=1,tilt=0,swing=0,aswing=0,drop=0,wink=0,blink=0,blush=0.0,pucker=0,arm_mouth=0,arm_throw=0,arm_wave=0,look=(0,0.1),face=1,fsway=0,squint=0)
print(mascot_png('mascot_idle',1.2,dict(base)))
print(mascot_png('mascot_blink',1.2,dict(base,blink=1)))
print(mascot_png('mascot_wave',3.0,dict(base,arm_wave=1,blush=0.6,look=(0.2,0))))
print(mascot_png('mascot_wink',1.8,dict(base,wink=1,blush=0.8,tilt=-6)))
