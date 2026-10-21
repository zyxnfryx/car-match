# PIL mockups of the home and win screens from the same assets (1080x2340, iPhone-ish).
import os, sys
from PIL import Image, ImageDraw, ImageFont, ImageFilter
R=os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
UI=R+'/Assets/_Project/Resources/UI/'; F=R+'/Assets/_Project/Resources/Fonts/LilitaOne-Regular.ttf'
W,H=1080,2340; CX,CY=W//2,H//2
def ui(n): return Image.open(UI+n+'.png').convert('RGBA')
def put(c,im,x,y,w=None):
    if w: im=im.resize((w,int(im.height*w/im.width)),Image.LANCZOS)
    c.alpha_composite(im,(int(CX+x-im.width/2),int(CY-y-im.height/2)))
def text(c,s,x,y,size,fill=(255,255,255),stroke=(20,40,20),sw=5):
    f=ImageFont.truetype(F,size); d=ImageDraw.Draw(c); d.text((CX+x,CY-y),s,font=f,anchor='mm',fill=fill,stroke_width=sw,stroke_fill=stroke)
def pill(c,x,y,icon,val):
    put(c,ui('pill'),x,y,392); put(c,ui(icon),x-150,y+2,92); text(c,val,x+5,y,40,(250,242,230),(10,12,30),3); put(c,ui('plus'),x+148,y,76)
def home():
    c=ui('home_bg').convert('RGBA'); cl=ui('clouds'); put(c,cl.resize((2160,520)),120,900); put(c,cl,-500,640,1620)
    top=H//2-60-70
    pill(c,-250,top,'icon_coin','400'); pill(c,250,top,'icon_heart','5   Full')
    put(c,ui('logo'),0,560+210,940)
    g=ui('glow').resize((760,260)); g.putalpha(g.split()[3].point(lambda v:int(v*0.55))); put(c,g,0,-40)
    put(c,Image.open(R+'/Assets/_Project/Art/Mascot/mascot_idle.png').convert('RGBA'),0,110,560)
    text(c,'Clear the lane. Match three.',0,-250,46,(250,242,230),(26,30,70),3)
    b=ui('btn_green'); put(c,b,0,-470,744)
    text(c,'Level 1',0,-470+22,92,(255,255,255),(15,80,30),6)
    sh=ui('shine').resize((90,230)); put(c,sh,180,-450)
    return c
def win():
    c=ui('home_bg').convert('RGBA'); ov=Image.new('RGBA',(W,H),(15,20,56,140)); c.alpha_composite(ov)
    top=H//2-60-70; pill(c,-250,top,'icon_coin','420'); pill(c,250,top,'icon_heart','5   Full')
    put(c,ui('rays'),0,260,1500); g=ui('glow'); put(c,g,0,260,700)
    put(c,Image.open(R+'/Assets/_Project/Art/Mascot/mascot_cheer.png').convert('RGBA'),0,250,460)
    put(c,ui('ribbon'),0,590,1040); text(c,'Level 1 · First Lot',0,590+26,54,(255,255,255),(115,10,30),4)
    put(c,ui('title_win'),0,720+40,980)
    import random; r=random.Random(3); cols=[(255,92,107),(255,209,64),(89,209,115),(77,158,255),(191,115,255),(51,217,209)]
    conf=ui('confetti')
    for i in range(60):
        col=cols[i%6]; p=Image.new('RGBA',(22,40),col+(255,)); p.putalpha(conf.resize((22,40)).split()[3]); p=p.rotate(r.randint(0,180),expand=True)
        c.alpha_composite(p,(r.randint(0,W-40),r.randint(150,1300)))
    put(c,ui('piggy'),0,-190,360)
    for k,(x,y,s) in enumerate([(-120,40,90),(-40,110,80),(60,60,70),(110,-60,60)]): put(c,ui('coin_big'),x,y-60,s)
    put(c,ui('pill').resize((440,150)),0,-420); put(c,ui('icon_coin'),-130,-416,110); text(c,'+20',40,-414,96,(255,224,82),(90,46,5),6)
    put(c,ui('btn_green'),0,-640,700); text(c,'Next',0,-640+22,92,(255,255,255),(15,80,30),6)
    put(c,ui('btn_blue'),0,-830,330); text(c,'Home',0,-830+14,56,(255,255,255),(18,46,115),4)
    return c
home().convert('RGB').save(sys.argv[1] if len(sys.argv)>1 else '/workspace/carmatch/preview/home_v2.png')
win().convert('RGB').save(sys.argv[2] if len(sys.argv)>2 else '/workspace/carmatch/preview/win_v2.png')
