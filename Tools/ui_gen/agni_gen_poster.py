
import sys, math, os, random
import skia, numpy as np
from PIL import Image
OUT=sys.argv[1]
FD='/home/user/tab/files/zyqor_game/design/intro_src/fonts/'
def C(h,a=255):
    h=h.lstrip('#'); return skia.ColorSetARGB(int(a),int(h[0:2],16),int(h[2:4],16),int(h[4:6],16))
P=lambda **k: skia.Paint(AntiAlias=True,**k)
W,H=2400,1080
s=skia.Surface(W,H); c=s.getCanvas()
# base: deep teal -> dusk
c.drawRect(skia.Rect(0,0,W,H),P(Shader=skia.GradientShader.MakeLinear([skia.Point(0,0),skia.Point(0,H)],[C('#0E3B44'),C('#0A2A33')],[0,1])))
# map art on the right
m=Image.open('/home/user/tab/files/zyqor_game/design/agni_map_v2_phone.jpg').convert('RGB')
from PIL import ImageFilter
m=m.crop((600,0,1760,1330)).filter(ImageFilter.GaussianBlur(2.2))
sc=1240/1330; m=m.resize((int(m.width*sc*1.0),1240),Image.LANCZOS)
mi=skia.Image.fromarray(np.dstack([np.array(m),np.full(m.size[::-1],255,np.uint8)]),colorType=skia.kRGBA_8888_ColorType)
c.save(); c.translate(1060,-60); c.rotate(-4)
c.drawImage(mi,0,0,skia.SamplingOptions(skia.FilterMode.kLinear),P(Alphaf=0.92))
c.restore()
# teal grade + vignette over map
c.drawRect(skia.Rect(0,0,W,H),P(Shader=skia.GradientShader.MakeLinear([skia.Point(0,0),skia.Point(W,0)],[C('#0B2E36',255),C('#0B2E36',245),C('#0B2E36',90),C('#0B2E36',30)],[0,0.38,0.62,1])))
c.drawRect(skia.Rect(0,0,W,H),P(Shader=skia.GradientShader.MakeRadial(skia.Point(W*0.62,H*0.5),W*0.75,[C('#000000',0),C('#000000',0),C('#06161B',200)],[0,0.55,1])))
c.drawRect(skia.Rect(1500,0,W,H),P(Shader=skia.GradientShader.MakeLinear([skia.Point(1500,0),skia.Point(W,0)],[C('#06161B',0),C('#06161B',170)],[0,1])))
# saffron sun glow behind mascot
cx,cy=640,560
c.drawCircle(cx,cy,700,P(Shader=skia.GradientShader.MakeRadial(skia.Point(cx,cy),700,[C('#FFB347',235),C('#FF7A1A',150),C('#E8431C',40),C('#E8431C',0)],[0,0.35,0.7,1])))
# sun rays
c.save(); c.translate(cx,cy)
for i in range(18):
    c.rotate(20)
    p=skia.Path(); p.moveTo(0,0); p.lineTo(900,-60); p.lineTo(900,60); p.close()
    c.drawPath(p,P(Color=C('#FFE2A8',22)))
c.restore()
# mandala-ish ring (own geometric motif)
for r,a in [(330,60),(360,35)]:
    c.drawCircle(cx,cy,r,P(Color=C('#FFF3DC',a),Style=skia.Paint.kStroke_Style,StrokeWidth=4))
for i in range(36):
    ang=i*math.pi/18; x,y=cx+345*math.cos(ang),cy+345*math.sin(ang)
    c.drawCircle(x,y,7,P(Color=C('#FFF3DC',70)))
# embers / bokeh
random.seed(7)
for i in range(90):
    x=random.uniform(0,W); y=random.uniform(0,H); r=random.uniform(2,9) if i<70 else random.uniform(20,60)
    a=random.uniform(60,190) if i<70 else random.uniform(15,40)
    c.drawCircle(x,y,r,P(Color=C(random.choice(['#FFC14D','#FF8A3D','#FFE7B0']),a),MaskFilter=skia.MaskFilter.MakeBlur(skia.kNormal_BlurStyle,r*0.6 if i<70 else r*0.5)))
# mascot
mas=Image.open('/tmp/agni_ui/mascot_wave.png')
mm=skia.Image.fromarray(np.array(mas),colorType=skia.kRGBA_8888_ColorType)
mh=700; mw=mas.width*mh/mas.height
# ground shadow
c.drawOval(skia.Rect(cx-200,cy+330,cx+200,cy+380),P(Color=C('#3A1A0A',120),MaskFilter=skia.MaskFilter.MakeBlur(skia.kNormal_BlurStyle,14)))
c.drawImageRect(mm,skia.Rect(cx-mw/2,cy+360-mh,cx+mw/2,cy+360),skia.SamplingOptions(skia.FilterMode.kLinear,skia.MipmapMode.kLinear))
# season title on the right, over the map
tf=skia.Typeface.MakeFromFile(FD+'LilitaOne-Regular.ttf'); bf=skia.Typeface.MakeFromFile(FD+'Baloo2.ttf')
def text(t,x,y,size,font,fill,outline=None,ow=0,shadow=True,align='l'):
    f=skia.Font(font,size); w=f.measureText(t)
    if align=='r': x-=w
    if align=='c': x-=w/2
    blob=skia.TextBlob(t,f)
    if shadow: c.drawTextBlob(blob,x+6,y+10,P(Color=C('#000000',120),MaskFilter=skia.MaskFilter.MakeBlur(skia.kNormal_BlurStyle,8)))
    if outline: c.drawTextBlob(blob,x,y,P(Color=C(outline),Style=skia.Paint.kStroke_Style,StrokeWidth=ow,StrokeJoin=skia.Paint.kRound_Join))
    c.drawTextBlob(blob,x,y,fill)
RX=2290
text('SEASON 1',RX,300,70,tf,P(Color=C('#FFE9C7')),outline='#3B1406',ow=12,align='r')
g=P(Shader=skia.GradientShader.MakeLinear([skia.Point(0,330),skia.Point(0,470)],[C('#FFE27A'),C('#FF9A2E'),C('#F0531C')],[0,0.5,1]))
text('BHARAT',RX,470,170,tf,g,outline='#3B1406',ow=22,align='r')
text('YATRA',RX,620,170,tf,g,outline='#3B1406',ow=22,align='r')
text('Kashmir se Kanyakumari tak — ek hi jung',RX,700,46,bf,P(Color=C('#FFF3DC')),outline='#0B2E36',ow=8,align='r')
img=s.makeImageSnapshot().toarray(colorType=skia.kRGBA_8888_ColorType)
Image.fromarray(img[...,:3]).save(os.path.join(OUT,'poster_s1.jpg'),quality=88)
print('ok')
