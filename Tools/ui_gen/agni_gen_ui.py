
import sys, math, os
import skia, numpy as np
from PIL import Image, ImageFilter, ImageDraw, ImageFont, ImageEnhance
OUT=sys.argv[1]; os.makedirs(OUT,exist_ok=True)
def C(h,a=255):
    h=h.lstrip('#'); return skia.ColorSetARGB(int(a),int(h[0:2],16),int(h[2:4],16),int(h[4:6],16))
def surf(w,h):
    s=skia.Surface(w,h); c=s.getCanvas(); c.clear(skia.ColorTRANSPARENT); return s,c
def save(s,name):
    Image.fromarray(s.makeImageSnapshot().toarray(colorType=skia.kRGBA_8888_ColorType),'RGBA').save(os.path.join(OUT,name+'.png'))
P=lambda **k: skia.Paint(AntiAlias=True,**k)
W=P(Color=C('#FFFFFF'))
def stroke(w,col='#FFFFFF'): return P(Color=C(col),Style=skia.Paint.kStroke_Style,StrokeWidth=w,StrokeCap=skia.Paint.kRound_Cap,StrokeJoin=skia.Paint.kRound_Join)
# ---------- 9-slice kit
for r in (16,24,40):
    n=r*2+32; s,c=surf(n,n); c.drawRRect(skia.RRect.MakeRectXY(skia.Rect(0,0,n,n),r,r),W); save(s,f'rr_{r}')
    # bevel button: white body, top light band, bottom shade (grayscale -> tinted by Image.color)
    s,c=surf(n,n)
    rr=skia.RRect.MakeRectXY(skia.Rect(0,0,n,n),r,r)
    c.drawRRect(rr,P(Shader=skia.GradientShader.MakeLinear([skia.Point(0,0),skia.Point(0,n)],[C('#FFFFFF'),C('#F7F7F7'),C('#DEDEDE')],[0,0.55,1])))
    c.save(); c.clipRRect(rr,doAntiAlias=True)
    c.drawRect(skia.Rect(0,n-6,n,n),P(Color=C('#000000',45)))
    c.restore()
    c.drawRRect(skia.RRect.MakeRectXY(skia.Rect(3,3,n-3,n-3),r-3,r-3),stroke(2.5,'#FFFFFF'))
    save(s,f'btn_{r}')
    # outline
    s,c=surf(n,n); c.drawRRect(skia.RRect.MakeRectXY(skia.Rect(2,2,n-2,n-2),r-2,r-2),stroke(4)); save(s,f'ol_{r}')
# soft shadow (border 64)
n=192; s,c=surf(n,n); c.drawRRect(skia.RRect.MakeRectXY(skia.Rect(40,40,n-40,n-40),24,24),P(Color=C('#000000',255),MaskFilter=skia.MaskFilter.MakeBlur(skia.kNormal_BlurStyle,14))); save(s,'shadow_24')
# circle, ring, glow
s,c=surf(256,256); c.drawCircle(128,128,126,W); save(s,'circle')
s,c=surf(256,256); c.drawCircle(128,128,118,stroke(14)); save(s,'ring')
s,c=surf(256,256); c.drawCircle(128,128,128,P(Shader=skia.GradientShader.MakeRadial(skia.Point(128,128),128,[C('#FFFFFF',255),C('#FFFFFF',90),C('#FFFFFF',0)],[0,0.45,1]))); save(s,'glow')
# vertical gradient alpha (top 0 -> bottom 255)
a=np.zeros((256,4,4),np.uint8); a[...,:3]=255; a[...,3]=np.linspace(0,255,256)[:,None].astype(np.uint8); Image.fromarray(a,'RGBA').save(os.path.join(OUT,'vgrad.png'))
# horizontal gradient alpha (left 255 -> right 0)
a=np.zeros((4,256,4),np.uint8); a[...,:3]=255; a[...,3]=np.linspace(255,0,256)[None,:].astype(np.uint8); Image.fromarray(a,'RGBA').save(os.path.join(OUT,'hgrad.png'))
# diagonal stripes tile (subtle pattern, tiled)
s,c=surf(64,64)
for k in range(-2,4): c.drawLine(k*32-10,74,k*32+64,0-10,stroke(9,'#FFFFFF'))
save(s,'stripes')
# ---------- icons 160px white glyphs (own designs)
def icon(name,fn):
    s,c=surf(160,160); fn(c); save(s,'ic_'+name)
def store(c):
    # shop awning + counter
    p=skia.Path(); p.moveTo(22,58); p.lineTo(34,24); p.lineTo(126,24); p.lineTo(138,58); p.close(); c.drawPath(p,W)
    for i in range(5):
        x=22+i*23.2; c.drawCircle(x+11.6,58,11.6,W)
    c.drawRRect(skia.RRect.MakeRectXY(skia.Rect(32,74,128,138),8,8),W)
    c.drawRRect(skia.RRect.MakeRectXY(skia.Rect(68,96,92,138),4,4),P(Color=C('#000000',0),BlendMode=skia.BlendMode.kClear))
def bag(c):
    c.drawRRect(skia.RRect.MakeRectXY(skia.Rect(34,44,126,144),26,26),W)
    c.drawRRect(skia.RRect.MakeRectXY(skia.Rect(58,18,102,54),16,16),stroke(10))
    clr=P(BlendMode=skia.BlendMode.kClear)
    c.drawRRect(skia.RRect.MakeRectXY(skia.Rect(52,92,108,124),8,8),clr)
    c.drawRRect(skia.RRect.MakeRectXY(skia.Rect(58,98,102,118),5,5),W)
    c.drawRect(skia.Rect(52,78,108,84),clr)
def person(c):
    c.drawCircle(80,52,28,W)
    p=skia.Path(); p.addRRect(skia.RRect.MakeRectXY(skia.Rect(28,90,132,170),44,44)); c.save(); c.clipRect(skia.Rect(0,0,160,142)); c.drawPath(p,W); c.restore()
def paw(c):
    c.drawOval(skia.Rect(46,74,114,132),W)
    for (x,y,r) in [(40,62,15),(64,40,15),(96,40,15),(120,62,15)]: c.drawOval(skia.Rect(x-r,y-r*1.2,x+r,y+r*1.2),W)
def events(c):
    # calendar with star
    c.drawRRect(skia.RRect.MakeRectXY(skia.Rect(24,34,136,140),16,16),W)
    clr=P(BlendMode=skia.BlendMode.kClear)
    c.drawRect(skia.Rect(24,58,136,64),clr)
    for x in (52,108): c.drawRRect(skia.RRect.MakeRectXY(skia.Rect(x-7,20,x+7,46),6,6),W)
    star=skia.Path()
    for i in range(10):
        ang=-math.pi/2+i*math.pi/5; r=30 if i%2==0 else 13
        x,y=80+r*math.cos(ang),102+r*math.sin(ang)
        (star.moveTo if i==0 else star.lineTo)(x,y)
    star.close(); c.drawPath(star,clr)
def gear(c):
    p=skia.Path()
    for i in range(16):
        ang=i*math.pi/8; r=66 if i%2==0 else 50
        for d in (-0.16,0.16):
            a=ang+d; (p.moveTo if (i==0 and d<0) else p.lineTo)(80+r*math.cos(a),80+r*math.sin(a))
    p.close(); c.drawPath(p,W); c.drawCircle(80,80,22,P(BlendMode=skia.BlendMode.kClear))
def plus(c):
    c.drawLine(80,36,80,124,stroke(18)); c.drawLine(36,80,124,80,stroke(18))
def close(c):
    c.drawLine(44,44,116,116,stroke(18)); c.drawLine(116,44,44,116,stroke(18))
def friends(c):
    c.drawCircle(58,56,22,W); c.drawCircle(106,62,18,W)
    c.save(); c.clipRect(skia.Rect(0,0,160,138))
    c.drawRRect(skia.RRect.MakeRectXY(skia.Rect(18,86,98,160),36,36),W)
    c.drawRRect(skia.RRect.MakeRectXY(skia.Rect(84,90,146,160),30,30),W); c.restore()
def chevron(c):
    p=skia.Path(); p.moveTo(56,30); p.lineTo(108,80); p.lineTo(56,130); c.drawPath(p,stroke(18))
def lock(c):
    c.drawRRect(skia.RRect.MakeRectXY(skia.Rect(34,70,126,140),14,14),W); c.drawRRect(skia.RRect.MakeRectXY(skia.Rect(52,24,108,96),28,28),stroke(12))
def music(c):
    c.drawOval(skia.Rect(30,104,74,136),W); c.drawOval(skia.Rect(90,94,134,126),W)
    c.drawLine(68,120,68,36,stroke(10)); c.drawLine(128,110,128,26,stroke(10))
    p=skia.Path(); p.moveTo(64,30); p.lineTo(132,18); p.lineTo(132,40); p.lineTo(64,52); p.close(); c.drawPath(p,W)
for n,f in [('store',store),('bag',bag),('character',person),('pet',paw),('events',events),('gear',gear),('plus',plus),('close',close),('friends',friends),('chevron',chevron),('lock',lock),('music',music)]: icon(n,f)
# ---- coloured currency icons
s,c=surf(160,160)
c.drawCircle(80,84,66,P(Color=C('#B9780B')))
c.drawCircle(80,78,66,P(Shader=skia.GradientShader.MakeLinear([skia.Point(30,20),skia.Point(130,140)],[C('#FFF1A8'),C('#FFC83D'),C('#E59A12')],[0,0.45,1])))
c.drawCircle(80,78,50,stroke(6,'#FFE58A'))
fl=skia.Path(); fl.moveTo(80,40); fl.cubicTo(104,64,108,82,98,100); fl.cubicTo(92,112,68,112,62,100); fl.cubicTo(54,84,64,72,72,60); fl.cubicTo(74,72,80,74,84,70); fl.cubicTo(88,60,86,50,80,40); fl.close()
c.drawPath(fl,P(Color=C('#C9730A')))
save(s,'cur_coin')
s,c=surf(160,160)
top=[(40,52),(62,30),(98,30),(120,52)]; bot=(80,138)
g=skia.Path(); g.moveTo(*top[0]); [g.lineTo(*p) for p in top[1:]]; g.lineTo(*bot); g.close()
c.drawPath(g,P(Shader=skia.GradientShader.MakeLinear([skia.Point(40,30),skia.Point(120,138)],[C('#FF8A5B'),C('#F0263F'),C('#8E0D2E')],[0,0.5,1])))
f1=skia.Path(); f1.moveTo(40,52); f1.lineTo(120,52); f1.lineTo(80,138); f1.close(); c.drawPath(f1,P(Color=C('#FFFFFF',40)))
f2=skia.Path(); f2.moveTo(62,30); f2.lineTo(80,52); f2.lineTo(98,30); f2.close(); c.drawPath(f2,P(Color=C('#FFFFFF',110)))
f3=skia.Path(); f3.moveTo(62,52); f3.lineTo(80,138); f3.lineTo(98,52); f3.close(); c.drawPath(f3,P(Color=C('#FFFFFF',35)))
c.drawPath(g,stroke(4,'#FFD2C0'))
c.drawCircle(56,44,5,P(Color=C('#FFFFFF',230)))
save(s,'cur_gem')
# ---- rank badge Kaansa (bronze shield with flame)
s,c=surf(200,220)
sh=skia.Path(); sh.moveTo(100,8); sh.lineTo(184,36); sh.cubicTo(184,120,150,180,100,212); sh.cubicTo(50,180,16,120,16,36); sh.close()
c.drawPath(sh,P(Color=C('#5A2E14')))
c.save(); c.translate(100,112); c.scale(0.86,0.86); c.translate(-100,-112)
c.drawPath(sh,P(Shader=skia.GradientShader.MakeLinear([skia.Point(30,10),skia.Point(170,210)],[C('#F6C08A'),C('#CD7F32'),C('#7A4019')],[0,0.5,1])))
c.restore()
c.save(); c.translate(100,118); c.scale(0.62,0.62); c.translate(-100,-118); c.drawPath(sh,P(Color=C('#8A4A1E'))); c.restore()
fl2=skia.Path(); fl2.moveTo(100,62); fl2.cubicTo(136,96,140,126,124,150); fl2.cubicTo(114,166,86,166,76,150); fl2.cubicTo(62,128,76,108,88,92); fl2.cubicTo(90,108,98,110,104,104); fl2.cubicTo(110,92,108,76,100,62); fl2.close()
c.drawPath(fl2,P(Shader=skia.GradientShader.MakeLinear([skia.Point(100,62),skia.Point(100,166)],[C('#FFE3B0'),C('#F2A65A')],[0,1])))
save(s,'rank_kaansa')

# pre-coloured saffron buttons (orange gradient, warm rim, top highlight)
for r in (16,24,40):
    n=r*2+32; s,c=surf(n,n)
    rr=skia.RRect.MakeRectXY(skia.Rect(0,0,n,n),r,r)
    c.drawRRect(rr,P(Color=C('#B8400C')))
    inner=skia.RRect.MakeRectXY(skia.Rect(0,0,n,n-5),r,r)
    c.drawRRect(inner,P(Shader=skia.GradientShader.MakeLinear([skia.Point(0,0),skia.Point(0,n-5)],[C('#FFD457'),C('#FFA126'),C('#FF7A14')],[0,0.5,1])))
    c.save(); c.clipRRect(inner,doAntiAlias=True)
    c.drawRect(skia.Rect(0,0,n,(n-5)*0.46),P(Color=C('#FFFFFF',40)))
    c.restore()
    c.drawRRect(skia.RRect.MakeRectXY(skia.Rect(3,3,n-3,n-8),r-3,r-3),P(Color=C('#FFF1B8',150),Style=skia.Paint.kStroke_Style,StrokeWidth=2.5))
    save(s,f'btno_{r}')
print('btno ok')

print('ok')
