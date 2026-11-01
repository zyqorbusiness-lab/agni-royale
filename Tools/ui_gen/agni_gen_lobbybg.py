
import sys, math, os, random
import skia, numpy as np
from PIL import Image
OUT=sys.argv[1]
def C(h,a=255):
    h=h.lstrip('#'); return skia.ColorSetARGB(int(a),int(h[0:2],16),int(h[2:4],16),int(h[4:6],16))
P=lambda **k: skia.Paint(AntiAlias=True,**k)
W,H=2400,1080
s=skia.Surface(W,H); c=s.getCanvas()
HZ=700
c.drawRect(skia.Rect(0,0,W,H),P(Shader=skia.GradientShader.MakeLinear([skia.Point(0,0),skia.Point(0,HZ)],[C('#0B2F38'),C('#1C4A4E'),C('#C8642A'),C('#FFB45E')],[0,0.45,0.85,1])))
cx=W/2
c.drawCircle(cx,HZ,900,P(Shader=skia.GradientShader.MakeRadial(skia.Point(cx,HZ),900,[C('#FFD38A',220),C('#FF8A2A',110),C('#FF8A2A',0)],[0,0.4,1])))
c.save(); c.translate(cx,HZ)
for i in range(24):
    c.rotate(15); p=skia.Path(); p.moveTo(0,0); p.lineTo(1500,-70); p.lineTo(1500,70); p.close(); c.drawPath(p,P(Color=C('#FFF0C8',16)))
c.restore()
# sun disc
c.drawCircle(cx,HZ-40,170,P(Color=C('#FFE7B0',120),MaskFilter=skia.MaskFilter.MakeBlur(skia.kNormal_BlurStyle,30)))
# far hills (two layers)
random.seed(3)
def ridge(base,amp,col,seed,step=60):
    random.seed(seed); p=skia.Path(); p.moveTo(0,H); y=base
    pts=[]
    for x in range(0,W+step,step):
        y=base-amp*(0.5+0.5*math.sin(x/260+seed))-random.uniform(0,amp*0.35); pts.append((x,y))
    p.lineTo(0,pts[0][1])
    for i in range(1,len(pts)):
        x0,y0=pts[i-1]; x1,y1=pts[i]; p.quadTo(x0,y0,(x0+x1)/2,(y0+y1)/2)
    p.lineTo(W,H); p.close(); c.drawPath(p,P(Color=C(col)))
ridge(HZ+10,120,'#B45A35',1.3)
ridge(HZ+40,70,'#7A3B2C',2.1)
# skyline silhouette (own shapes): clock tower, mill chimney, water tank, palms, shop houses
sil=P(Color=C('#3B1E1C'))
base=HZ+70
def box(x,w,h): c.drawRect(skia.Rect(x,base-h,x+w,base+400),sil)
# left cluster
box(180,160,120); box(330,90,180); box(420,140,100)
# clock tower
x=560; box(x,70,330); c.drawRect(skia.Rect(x-10,base-345,x+80,base-330),sil)
p=skia.Path(); p.moveTo(x-6,base-345); p.lineTo(x+35,base-420); p.lineTo(x+76,base-345); p.close(); c.drawPath(p,sil)
c.drawCircle(x+35,base-290,22,P(Color=C('#FFC77A',200)))
box(640,180,140); box(820,110,90)
# right cluster: mill + chimney + water tank
box(1500,260,150); 
for k in range(5):
    p=skia.Path(); p.moveTo(1500+k*52,base-150); p.lineTo(1500+k*52+52,base-150); p.lineTo(1500+k*52+52,base-190); p.close(); c.drawPath(p,sil)
c.drawRect(skia.Rect(1790,base-380,1822,base),sil); c.drawRect(skia.Rect(1784,base-392,1828,base-380),sil)
box(1860,120,110)
c.drawRect(skia.Rect(1990,base-260,2090,base-200),sil)
for xx in (1998,2076): c.drawRect(skia.Rect(xx,base-200,xx+8,base),sil)
box(2120,200,130)
# palms
def palm(x,h):
    p=skia.Path(); p.moveTo(x,base); p.quadTo(x+20,base-h*0.5,x+8,base-h); c.drawPath(p,P(Color=C('#3B1E1C'),Style=skia.Paint.kStroke_Style,StrokeWidth=9))
    for a in range(0,360,45):
        r=math.radians(a); q=skia.Path(); q.moveTo(x+8,base-h); q.quadTo(x+8+60*math.cos(r),base-h+60*math.sin(r)-30,x+8+90*math.cos(r),base-h+70*math.sin(r)+10)
        c.drawPath(q,P(Color=C('#3B1E1C'),Style=skia.Paint.kStroke_Style,StrokeWidth=10,StrokeCap=skia.Paint.kRound_Cap))
palm(110,230); palm(1000,190); palm(1400,250); palm(2330,210)
# ground
c.drawRect(skia.Rect(0,base,W,H),P(Shader=skia.GradientShader.MakeLinear([skia.Point(0,base),skia.Point(0,H)],[C('#3B1E1C'),C('#1A1112')],[0,1])))
# haze over horizon
c.drawRect(skia.Rect(0,HZ-200,W,base+40),P(Shader=skia.GradientShader.MakeLinear([skia.Point(0,HZ-200),skia.Point(0,base+40)],[C('#FFB45E',0),C('#FFB45E',70),C('#FF9A40',0)],[0,0.75,1])))
# bunting / diyas lights string across top (festive)
for row,(y0,sag) in enumerate([(70,90),(40,60)]):
    pts=[(x, y0+sag*math.sin(math.pi*((x%800)/800))) for x in range(0,W+1,10)]
    for x,y in pts[::7]:
        col=random.choice(['#FFC14D','#FF7A2F','#FFE7B0'])
        c.drawCircle(x,y,9,P(Color=C(col,220)))
        c.drawCircle(x,y,24,P(Color=C(col,60),MaskFilter=skia.MaskFilter.MakeBlur(skia.kNormal_BlurStyle,12)))
# embers
for i in range(80):
    x=random.uniform(0,W); y=random.uniform(0,H*0.9); r=random.uniform(2,7)
    c.drawCircle(x,y,r,P(Color=C(random.choice(['#FFC14D','#FF8A3D','#FFE7B0']),random.uniform(60,170)),MaskFilter=skia.MaskFilter.MakeBlur(skia.kNormal_BlurStyle,r*0.7)))
# vignette
c.drawRect(skia.Rect(0,0,W,H),P(Shader=skia.GradientShader.MakeRadial(skia.Point(cx,H*0.55),W*0.62,[C('#000000',0),C('#000000',0),C('#050A0C',190)],[0,0.6,1])))
img=s.makeImageSnapshot().toarray(colorType=skia.kRGBA_8888_ColorType)
Image.fromarray(img[...,:3]).save(os.path.join(OUT,'lobby_bg.jpg'),quality=88); print('ok')
