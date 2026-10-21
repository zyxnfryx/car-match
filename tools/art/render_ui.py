# Lot Loop UI art (original). Same lighting as art/render_cars3d.py: key light from the upper left,
# soft bevel, specular hotspot, baked soft shadow. Writes Assets/_Project/Resources/UI/*.png
# Run: python3 tools/art/render_ui.py   (numpy, scipy, Pillow)
import numpy as np, os, math, random
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageChops
from scipy import ndimage
ROOT=os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT=os.path.join(ROOT,'Assets/_Project/Resources/UI/'); os.makedirs(OUT,exist_ok=True)
FONT='/usr/share/fonts/truetype/sand-box/google/Lilita One/LilitaOne-Regular.ttf'
if not os.path.exists(FONT): FONT=os.path.join(ROOT,'tools/art/LilitaOne-Regular.ttf')
L=np.array([-0.35,-0.55,0.76]); L/=np.linalg.norm(L); H=L+np.array([0,0,1.]); H/=np.linalg.norm(H)
def save(im,name): im.save(OUT+name+'.png'); print(name,im.size)
def lerp(a,b,t): return a+(b-a)*t
def bevel(mask,base,depth=18,spec=1.0,pw=40,amb=0.55,dif=0.6,grad=None):
    """mask: float HxW 0..1 (antialiased). base: rgb 0..1 or HxWx3. returns RGBA float."""
    d=ndimage.distance_transform_edt(mask>0.5)
    h=np.clip(d/depth,0,1); h=np.sin(h*np.pi/2)*depth*0.5
    h=ndimage.gaussian_filter(h,1.2)
    gy,gx=np.gradient(h); n=np.stack([-gx,-gy,np.ones_like(h)],-1); n/=np.linalg.norm(n,axis=-1,keepdims=True)
    df=np.clip(n@L,0,1); sp=np.clip(n@H,0,1)
    b=np.broadcast_to(np.array(base,np.float32),mask.shape+(3,)) if grad is None else grad
    col=b*(amb+dif*df)[...,None]+(spec*0.6*sp**pw+0.08*sp**6)[...,None]
    return np.dstack([np.clip(col,0,1),mask])
def toimg(a): return Image.fromarray((np.clip(a,0,1)*255).astype(np.uint8),'RGBA')
def rrect(w,h,r,ss=4,pad=0):
    im=Image.new('L',(w*ss,h*ss),0); ImageDraw.Draw(im).rounded_rectangle([pad*ss,pad*ss,(w-pad)*ss-1,(h-pad)*ss-1],r*ss,fill=255)
    return np.asarray(im.resize((w,h),Image.LANCZOS),np.float32)/255
def shadow(img,off=(0,10),blur=10,alpha=0.45,grow=24):
    w,h=img.size; c=Image.new('RGBA',(w+grow*2,h+grow*2),(0,0,0,0))
    a=img.split()[3].point(lambda v:int(v*alpha)); sh=Image.new('RGBA',img.size,(10,14,30,255)); sh.putalpha(a)
    c.alpha_composite(sh.filter(ImageFilter.GaussianBlur(blur)) if False else sh,(grow+off[0],grow+off[1]))
    c=c.filter(ImageFilter.GaussianBlur(blur)); c.alpha_composite(img,(grow,grow)); return c
def vgrad(h,w,top,bot,p=1.0):
    t=(np.linspace(0,1,h)**p)[:,None,None]; return np.broadcast_to(np.array(top)*(1-t)+np.array(bot)*t,(h,w,3)).astype(np.float32)

# ---------- buttons (3D glossy, darker lip baked under) ----------
def button(w,h,top,bot,lip,name,r=None):
    r=r or h*0.42; lipH=int(h*0.12)
    full=Image.new('RGBA',(w,h+lipH),(0,0,0,0))
    m=rrect(w,h,r); lipimg=toimg(bevel(m,np.array(lip)/255,depth=10,spec=0.2)); full.alpha_composite(lipimg,(0,lipH))
    face=bevel(m,None,depth=h*0.22,spec=1.1,pw=30,grad=vgrad(h,w,np.array(top)/255,np.array(bot)/255))
    # gloss band on upper half
    g=rrect(w-int(h*0.24),int(h*0.42),int(h*0.2)); gl=np.zeros((h,w),np.float32); y0=int(h*0.08); x0=int(h*0.12)
    gl[y0:y0+g.shape[0],x0:x0+g.shape[1]]=g*np.linspace(0.42,0.06,g.shape[0])[:,None]
    face[...,:3]=face[...,:3]*(1-gl[...,None])+gl[...,None]
    full.alpha_composite(toimg(face),(0,0)); save(shadow(full,(0,8),9,0.5,22),name)
button(700,190,(120,226,90),(38,160,64),(22,100,42),'btn_green')
button(420,140,(110,170,250),(40,100,220),(24,62,150),'btn_blue')
button(420,140,(255,190,80),(240,130,30),(170,80,20),'btn_orange')
button(210,190,(110,170,250),(40,100,220),(24,62,150),'btn_booster',r=48)
button(150,96,(70,84,120),(36,44,72),(18,22,40),'btn_navy',r=40)
# ---------- top bar pill + plus badge ----------
def pill():
    w,h=360,96; m=rrect(w,h,46)
    a=bevel(m,None,depth=14,spec=0.6,pw=50,grad=vgrad(h,w,(0.20,0.24,0.40),(0.10,0.12,0.24)))
    inner=rrect(w-16,h-16,38); ii=np.zeros_like(m); ii[8:8+inner.shape[0],8:8+inner.shape[1]]=inner
    a[...,:3]=a[...,:3]*(1-0.25*(m-ii)[...,None])+0.25*(m-ii)[...,None]*np.array([0.5,0.6,0.9])
    save(shadow(toimg(a),(0,6),7,0.45,16),'pill')
pill()
def disc(size,top,bot,name,ring=None):
    m=rrect(size,size,size//2)
    a=bevel(m,None,depth=size*0.25,spec=1.0,pw=30,grad=vgrad(size,size,np.array(top)/255,np.array(bot)/255))
    return a
def plus():
    s=76; a=disc(s,(130,230,95),(40,160,60),'')
    im=toimg(a); d=ImageDraw.Draw(im); c=s//2; t=8; l=20
    d.rounded_rectangle([c-l,c-t,c+l,c+t],6,fill=(255,255,255,255)); d.rounded_rectangle([c-t,c-l,c+t,c+l],6,fill=(255,255,255,255))
    save(shadow(im,(0,4),4,0.5,10),'plus')
plus()
# ---------- coin (3D, embossed star) ----------
def coin(s=160,name='icon_coin'):
    m=rrect(s,s,s//2,pad=2); a=bevel(m,None,depth=s*0.16,spec=1.2,pw=24,grad=vgrad(s,s,(1.0,0.86,0.32),(0.93,0.60,0.10)))
    inner=Image.new('L',(s*4,s*4),0); dd=ImageDraw.Draw(inner); c=s*2; R=s*1.45
    dd.ellipse([c-R,c-R,c+R,c+R],fill=255); im=np.asarray(inner.resize((s,s),Image.LANCZOS),np.float32)/255
    star=Image.new('L',(s*4,s*4),0); pts=[]
    for k in range(10):
        r=(s*0.95 if k%2==0 else s*0.42); ang=-math.pi/2+k*math.pi/5; pts.append((c+r*math.cos(ang),c+r*math.sin(ang)+s*0.05))
    ImageDraw.Draw(star).polygon(pts,fill=255); st=np.asarray(star.resize((s,s),Image.LANCZOS),np.float32)/255
    b2=bevel(st,(1.0,0.93,0.55),depth=s*0.05,spec=1.4,pw=30)
    rim=np.clip(m-im,0,1)[...,None]*0.18; a[...,:3]=a[...,:3]*(1-rim)+rim*np.array([1,1,0.8])
    a[...,:3]=a[...,:3]*(1-st[...,None]*0.9)+b2[...,:3]*st[...,None]*0.9
    save(toimg(a),name); return toimg(a)
coin(); coin(220,'coin_big')
def heart(s=160):
    S=s*4; im=Image.new('L',(S,S),0); d=ImageDraw.Draw(im); r=S*0.25
    d.ellipse([S*0.06,S*0.12,S*0.06+2*r,S*0.12+2*r],fill=255); d.ellipse([S*0.94-2*r,S*0.12,S*0.94,S*0.12+2*r],fill=255)
    d.polygon([(S*0.09,S*0.42),(S*0.91,S*0.42),(S*0.5,S*0.92)],fill=255)
    m=np.asarray(im.filter(ImageFilter.GaussianBlur(6)).resize((s,s),Image.LANCZOS),np.float32)/255; m=np.clip((m-0.5)*4+0.5,0,1)
    a=bevel(m,None,depth=s*0.22,spec=1.3,pw=26,grad=vgrad(s,s,(1.0,0.40,0.48),(0.82,0.10,0.24)))
    save(toimg(a),'icon_heart')
heart()
# ---------- logo ----------
def logo(text='Lot Loop',name='logo',W=980,Hh=380,size=210,top=(1.0,0.93,0.40),bot=(1.0,0.55,0.12),outline=(0.42,0.13,0.10),arc=0.10):
    f=ImageFont.truetype(FONT,size*4)
    S=4; canvas=Image.new('L',(W*S,Hh*S),0); d=ImageDraw.Draw(canvas)
    # per-letter on an arc
    widths=[d.textlength(ch,font=f) for ch in text]; tw=sum(widths); x=(W*S-tw)/2
    for ch,wd in zip(text,widths):
        cx=x+wd/2; u=(cx/(W*S)-0.5)*2; y=Hh*S*0.16+ (u*u)*Hh*S*arc
        L_=Image.new('L',(int(wd)+400,size*S+400),0); ImageDraw.Draw(L_).text((200,100),ch,font=f,fill=255)
        L_=L_.rotate(-u*7,resample=Image.BICUBIC,center=(L_.width/2,L_.height/2))
        canvas.paste(255,(int(x)-200,int(y)-100),L_); x+=wd
    m=np.asarray(canvas.resize((W,Hh),Image.LANCZOS),np.float32)/255
    ol=ndimage.grey_dilation(m,footprint=np.hypot(*np.mgrid[-14:15,-14:15])<=14)
    rim=ndimage.grey_dilation(m,footprint=np.hypot(*np.mgrid[-5:6,-5:6])<=5)
    out=np.zeros((Hh,W,4),np.float32)
    o=bevel(ol,np.array(outline),depth=10,spec=0.4); out=o
    wr=bevel(rim,(1,1,1),depth=4,spec=0.3,amb=0.85,dif=0.2)
    out[...,:3]=out[...,:3]*(1-rim[...,None])+wr[...,:3]*rim[...,None]
    face=bevel(m,None,depth=12,spec=1.2,pw=26,grad=vgrad(Hh,W,top,bot,1.0))
    gl=(np.linspace(1,0,Hh)[:,None]>0.62)*0.28*m
    face[...,:3]=np.clip(face[...,:3]+gl[...,None],0,1)
    out[...,:3]=out[...,:3]*(1-m[...,None])+face[...,:3]*m[...,None]
    im=toimg(out); save(shadow(im,(0,16),10,0.55,30),name)
logo()
logo('Lot Cleared!','title_win',W=1000,Hh=250,size=150,arc=0.05)
# ---------- shine, glow, rays, confetti, dust, sparkle ----------
s=Image.new('L',(160,400),0); d=ImageDraw.Draw(s); d.polygon([(60,0),(110,0),(100,400),(50,400)],fill=255); d.polygon([(10,0),(35,0),(25,400),(0,400)],fill=170)
s=s.filter(ImageFilter.GaussianBlur(6)); w=Image.new('RGBA',s.size,(255,255,255,0)); w.putalpha(s); save(w,'shine')
Y,X=np.mgrid[-128:128,-128:128]/128.; r=np.hypot(X,Y)
g=np.clip(1-r,0,1)**2; save(toimg(np.dstack([np.ones((256,256,3)),g])),'glow')
Y,X=np.mgrid[-512:512,-512:512]/512.; r=np.hypot(X,Y); th=np.arctan2(Y,X)
ray=(0.5+0.5*np.cos(th*12))**6*np.clip(1-r,0,1)**1.2*np.clip(r*4,0,1)
save(toimg(np.dstack([np.ones((1024,1024,3))*np.array([1,0.95,0.7]),ray*0.9])),'rays')
save(toimg(np.dstack([np.ones((48,24,3)),rrect(24,48,6)])),'confetti')
Y,X=np.mgrid[-64:64,-64:64]/64.; dust=np.clip(1-np.hypot(X,Y),0,1)**1.5
save(toimg(np.dstack([np.ones((128,128,3))*np.array([0.92,0.88,0.80]),dust*0.8])),'dust')
st=np.clip(1-(np.abs(X)**0.5+np.abs(Y)**0.5)**2,0,1)**1.2; save(toimg(np.dstack([np.ones((128,128,3)),st])),'sparkle')
# ---------- ribbon banner ----------
def ribbon(W=1000,Hh=230):
    im=Image.new('RGBA',(W,Hh),(0,0,0,0)); S=4
    def poly(pts,top,bot,name_depth=10,dark=1.0):
        mm=Image.new('L',(W*S,Hh*S),0); ImageDraw.Draw(mm).polygon([(x*S,y*S) for x,y in pts],fill=255)
        m=np.asarray(mm.resize((W,Hh),Image.LANCZOS),np.float32)/255
        a=bevel(m,None,depth=name_depth,spec=0.8,pw=30,grad=vgrad(Hh,W,np.array(top)*dark,np.array(bot)*dark)); return toimg(a)
    im.alpha_composite(poly([(0,70),(170,70),(170,200),(0,200),(50,135)],(0.75,0.12,0.2),(0.55,0.06,0.14)))
    im.alpha_composite(poly([(W,70),(W-170,70),(W-170,200),(W,200),(W-50,135)],(0.75,0.12,0.2),(0.55,0.06,0.14)))
    im.alpha_composite(poly([(120,170),(170,200),(170,170)],(0.35,0.03,0.08),(0.35,0.03,0.08)))
    im.alpha_composite(poly([(W-120,170),(W-170,200),(W-170,170)],(0.35,0.03,0.08),(0.35,0.03,0.08)))
    im.alpha_composite(poly([(110,30),(W-110,30),(W-110,175),(110,175)],(1.0,0.36,0.40),(0.86,0.14,0.24),16))
    save(shadow(im,(0,10),8,0.45,20),'ribbon')
ribbon()
# ---------- bar tray + slot ----------
def tray():
    w,h=1020,170; m=rrect(w,h,44)
    a=bevel(m,None,depth=22,spec=0.7,pw=40,grad=vgrad(h,w,(0.36,0.42,0.62),(0.18,0.22,0.38)))
    save(shadow(toimg(a),(0,10),10,0.5,20),'tray')
    s=rrect(122,128,26); b=bevel(1-s+0,None,depth=1,grad=vgrad(128,122,(0.1,0.12,0.2),(0.1,0.12,0.2)))
    # recessed: invert lighting
    d=ndimage.distance_transform_edt(s>0.5); hh=-np.clip(d/12,0,1)*6; gy,gx=np.gradient(ndimage.gaussian_filter(hh,1))
    n=np.stack([-gx,-gy,np.ones_like(hh)],-1); n/=np.linalg.norm(n,axis=-1,keepdims=True); df=np.clip(n@L,0,1)
    col=np.array([0.10,0.13,0.24])*(0.6+0.7*df)[...,None]
    save(toimg(np.dstack([col,s])),'slot')
tray()
# ---------- asphalt tile ----------
rng=np.random.default_rng(3); n=rng.normal(0,1,(256,256)); n=ndimage.gaussian_filter(n,1.0,mode='wrap')*0.6+ndimage.gaussian_filter(rng.normal(0,1,(256,256)),6,mode='wrap')*2.5
v=0.30+0.035*n; spk=(rng.random((256,256))>0.996)*0.12
col=np.dstack([v+spk,v+0.01+spk,v+0.04+spk]); save(toimg(np.dstack([col,np.ones((256,256))])),'asphalt')
# ---------- home backdrop: sky, sun, skyline (2 depths), lot, vignette ----------
def backdrop(W=1080,Hh=2340):
    sky=vgrad(Hh,W,(0.36,0.62,0.96),(1.0,0.80,0.62),1.2).copy()
    Y,X=np.mgrid[0:Hh,0:W].astype(np.float32)
    sun=np.exp(-((X-W*0.72)**2+(Y-Hh*0.30)**2)/(2*260**2)); sky+=sun[...,None]*np.array([0.35,0.28,0.12])
    im=toimg(np.dstack([np.clip(sky,0,1),np.ones((Hh,W))]))
    rnd=random.Random(7)
    def skyline(base_y,hmin,hmax,col,win,seed):
        r=random.Random(seed); lay=Image.new('RGBA',(W,Hh),(0,0,0,0)); d=ImageDraw.Draw(lay); x=-20
        while x<W:
            bw=r.randint(90,170); bh=r.randint(hmin,hmax); top=base_y-bh
            d.rounded_rectangle([x,top,x+bw,base_y+40],10,fill=col)
            if r.random()<0.3: d.rectangle([x+bw*0.4,top-40,x+bw*0.45,top],fill=col)
            for wy in range(int(top)+24,int(base_y)-10,38):
                for wx in range(x+16,x+bw-24,30):
                    if r.random()<0.55: d.rounded_rectangle([wx,wy,wx+14,wy+20],3,fill=win)
            x+=bw+r.randint(4,20)
        return lay
    far=skyline(Hh*0.56,180,420,(150,165,215,255),(190,200,235,255),1).filter(ImageFilter.GaussianBlur(2))
    im.alpha_composite(far)
    haze=Image.new('RGBA',(W,Hh),(255,226,200,0)); ha=np.zeros((Hh,W)); ha[int(Hh*0.4):int(Hh*0.6)]=np.linspace(0,0.35,int(Hh*0.6)-int(Hh*0.4))[:,None]
    haze.putalpha(Image.fromarray((ha*255).astype(np.uint8))); im.alpha_composite(haze)
    near=skyline(Hh*0.60,120,300,(92,104,160,255),(255,214,130,255),2); im.alpha_composite(near)
    # trees row
    d=ImageDraw.Draw(im)
    for x in range(-30,W+60,70):
        r=rnd.randint(38,58); cy=Hh*0.605
        d.ellipse([x-r,cy-r*1.3,x+r,cy+r*0.6],fill=(58,140,92,255)); d.ellipse([x-r*0.6,cy-r*1.25,x+r*0.2,cy-r*0.4],fill=(88,176,110,255))
    # lot (asphalt) with perspective stripes
    lotTop=int(Hh*0.62)
    asp=Image.open(OUT+'asphalt.png').convert('RGBA'); lot=Image.new('RGBA',(W,Hh-lotTop))
    for yy in range(0,lot.height,256):
        for xx in range(0,W,256): lot.paste(asp,(xx,yy))
    im.alpha_composite(lot,(0,lotTop))
    d=ImageDraw.Draw(im); d.rectangle([0,lotTop-14,W,lotTop+6],fill=(200,196,186,255)); d.rectangle([0,lotTop+6,W,lotTop+14],fill=(150,146,140,255))
    vx=W/2; vy=lotTop-500
    for i in range(-6,7):
        x0=W/2+i*170; t0=0.06; 
        xa=vx+(x0-vx)*((lotTop+30-vy)/(Hh-vy)); d.line([(xa,lotTop+30),(x0,Hh)],fill=(235,232,220,200),width=10)
    for c,(xx,yy,sc) in zip(['red','blue','yellow','teal','green','purple'],[(90,1640,0.9),(980,1700,0.95),(250,1980,1.0),(860,2040,1.0),(70,2260,1.0),(1010,2290,1.0)]):
        p=os.path.join(ROOT,f'Assets/_Project/Art/Cars/car_{c}_open.png')
        if os.path.exists(p):
            car=Image.open(p).convert('RGBA'); car=car.resize((int(260*sc),int(260*sc)),Image.LANCZOS)
            dim=Image.new('RGBA',car.size,(30,40,70,0)); car=Image.blend(car,Image.composite(Image.new('RGBA',car.size,(40,50,80,255)),car,car.split()[3]).convert('RGBA'),0.25); car.putalpha(Image.open(p).convert('RGBA').resize(car.size).split()[3])
            im.alpha_composite(car,(int(xx-car.width/2),int(yy-car.height/2)))
    # soft light pool behind mascot + vignette
    a=np.asarray(im,np.float32)/255
    pool=np.exp(-((X-W/2)**2/(2*360**2)+(Y-Hh*0.50)**2/(2*300**2)))*0.25
    a[...,:3]=a[...,:3]+pool[...,None]*np.array([1,0.95,0.8])
    vg=np.clip(np.hypot((X-W/2)/(W*0.75),(Y-Hh*0.48)/(Hh*0.62)),0,1.5); vig=np.clip((vg-0.55)/0.6,0,1)**1.6*0.55
    a[...,:3]=a[...,:3]*(1-vig[...,None])+vig[...,None]*np.array([0.08,0.08,0.20])
    save(toimg(a).convert('RGB'),'home_bg')
backdrop()
def clouds(W=2160,Hh=520):
    im=Image.new('RGBA',(W,Hh),(0,0,0,0)); d=ImageDraw.Draw(im); r=random.Random(5)
    for k in range(7):
        cx=r.randint(100,W-100); cy=r.randint(100,Hh-140); s=r.uniform(0.6,1.2)
        for j in range(6):
            rx=r.randint(60,120)*s; ox=(j-2.5)*70*s; oy=-abs(j-2.5)*-8*s - (40*s if 1<j<4 else 0)
            d.ellipse([cx+ox-rx,cy+oy-rx*0.8,cx+ox+rx,cy+oy+rx*0.8],fill=(255,255,255,235))
    im=im.filter(ImageFilter.GaussianBlur(3)); save(im,'clouds')
clouds()
# ---------- piggy bank (3D heightfield, pink, original) ----------
def piggy(s=420):
    S=s; Y,X=np.mgrid[0:S,0:S].astype(np.float32); X=(X/S-0.5)*2.4; Y=(Y/S-0.5)*2.4
    def se(cx,cy,a,b,n=2): return (np.abs((X-cx)/a)**n+np.abs((Y-cy)/b)**n)**(1/n)
    def dome(r,k=1.0): return np.sqrt(np.clip(1-r**2,0,1))*k
    def sm(e0,e1,v): t=np.clip((v-e0)/(e1-e0),0,1); return t*t*(3-2*t)
    aa=2.4/S*1.5
    img=np.zeros((S,S,3),np.float32); al=np.zeros((S,S),np.float32)
    def over(c,m):
        nonlocal img,al; img=c*m[...,None]+img*(1-m[...,None]); al=m+al*(1-m)
    def sh(h,base,k=1.0,pw=50):
        gy,gx=np.gradient(h); f=S/2.4; n=np.stack([-gx*f,-gy*f,np.ones_like(h)],-1); n/=np.linalg.norm(n,axis=-1,keepdims=True)
        d=np.clip(n@L,0,1); sp=np.clip(n@H,0,1); return np.array(base)*(0.45+0.65*d)[...,None]+(k*sp**pw*1.0+0.12*sp**8)[...,None]
    shd=np.asarray(Image.fromarray(((se(0,0.86,0.8,0.09)<1)*255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(S/40)),np.float32)/255*0.45
    over(np.zeros((S,S,3)),shd)
    pink=(1.0,0.56,0.68); dk=(0.92,0.42,0.56)
    for lx,ly in((-0.45,0.62),(0.42,0.62),(-0.22,0.68),(0.2,0.68)):
        r=se(lx,ly,0.13,0.2,2.5); m=1-sm(1-aa*6,1,r); over(sh(dome(r,0.12),dk),m)
    for ex in(-0.36,0.36):
        r=se(ex,-0.62,0.17,0.2,2); m=(1-sm(1-aa*6,1,r)); over(sh(dome(r,0.1),dk),m)
    rb=se(0,0.05,0.86,0.68,2.2); mb=1-sm(1-aa,1,rb); hb=dome(rb,0.6); body=sh(hb,pink,1.0,45)
    # coin slot
    rs=se(0,-0.55,0.22,0.04,4); ms=(1-sm(1-aa*8,1,rs)); body=body*(1-ms[...,None])+ms[...,None]*np.array([0.45,0.12,0.22])
    # snout
    rn=se(0,0.18,0.28,0.2,2); mn=1-sm(1-aa*6,1,rn); sn=sh(hb+dome(rn,0.14),(1.0,0.66,0.76),1.1,60)
    body=body*(1-mn[...,None])+sn*mn[...,None]
    for nx in(-0.1,0.1):
        r=se(nx,0.18,0.05,0.08); m=1-sm(1-aa*10,1,r); body=body*(1-m[...,None])+m[...,None]*np.array([0.55,0.16,0.26])
    for ex in(-0.32,0.32):
        r=se(ex,-0.15,0.1,0.12); m=1-sm(1-aa*10,1,r); body=body*(1-m[...,None])+m[...,None]*np.array([0.08,0.06,0.1])
        r=se(ex-0.03,-0.19,0.03,0.035); m=1-sm(1-aa*12,1,r); body=body*(1-m[...,None])+m[...,None]
        r=se(ex*1.55,0.12,0.11,0.06); m=(1-sm(0.3,1,r))*0.5; body=body*(1-m[...,None])+m[...,None]*np.array([1,0.36,0.46])
    over(body,mb)
    out=np.dstack([np.clip(img/np.maximum(al,1e-6)[...,None],0,1),al])
    save(toimg(out),'piggy')
piggy()
# ---------- mascot blink frame (from the painted idle; eyes closed into soft arcs) ----------
def blink():
    p=os.path.join(ROOT,'Assets/_Project/Art/Mascot/mascot_idle.png')
    im=Image.open(p).convert('RGBA'); W=im.width; k=W/768
    S=4; big=im.resize((W*S,W*S),Image.LANCZOS); d=ImageDraw.Draw(big)
    for ex in (320,448):
        cx,cy=ex*k*S,304*k*S; rx,ry=50*k*S,60*k*S
        glass=big.getpixel((int(cx),int(cy+ry+14*k*S)))
        d.ellipse([cx-rx,cy-ry,cx+rx,cy+ry],fill=glass)
        d.arc([cx-rx*0.8,cy-ry*0.6,cx+rx*0.8,cy+ry*0.5],20,160,fill=(255,255,255,255),width=int(12*k*S))
    save(big.resize((W,W),Image.LANCZOS),'mascot_blink')
blink()
