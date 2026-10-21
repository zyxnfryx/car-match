# Lot Loop FX generator: glossy toy-style FX sprite strips (256px cells, RGBA). Run: python3 gen_fx.py
import numpy as np, os
from PIL import Image
OD=os.path.dirname(os.path.abspath(__file__)); C=256; SS=2; N=C*SS
PAL=dict(red=(222,52,52),blue=(40,110,225),yellow=(245,196,30),green=(70,180,60),teal=(25,170,165),purple=(140,75,205),gold=(255,196,40),white=(255,255,255),silver=(205,210,222))
col=lambda k:np.array(PAL[k],float)/255
yy,xx=np.mgrid[0:N,0:N].astype(float); X=(xx+.5)/N*2-1; Y=(yy+.5)/N*2-1   # -1..1
sm=lambda a,b,x:np.clip((x-a)/(b-a),0,1)**2*(3-2*np.clip((x-a)/(b-a),0,1))
ease=lambda t:1-(1-np.clip(t,0,1))**3
class Cell:
    def __init__(s): s.c=np.zeros((N,N,3)); s.a=np.zeros((N,N))  # premultiplied
    def put(s,alpha,rgb,add=False):
        alpha=np.clip(alpha,0,1); rgb=np.broadcast_to(np.asarray(rgb,float),(N,N,3)) if np.ndim(rgb)==1 else rgb
        if add: s.c=np.clip(s.c+rgb*alpha[...,None],0,1); s.a=np.clip(s.a+alpha*.6,0,1); return
        s.c=rgb*alpha[...,None]+s.c*(1-alpha[...,None]); s.a=alpha+s.a*(1-alpha)
    def img(s):
        a=s.a.reshape(C,SS,C,SS).mean((1,3)); c=s.c.reshape(C,SS,C,SS,3).mean((1,3))
        rgb=np.where(a[...,None]>1e-4,c/np.maximum(a,1e-4)[...,None],0)
        return Image.fromarray(np.dstack([np.clip(rgb,0,1)*255,a*255]).astype(np.uint8),'RGBA')
def glow(cl,x,y,r,k,c,i=1.):  cl.put(i*np.exp(-((X-x)**2+(Y-y)**2)/(r*r)),c,add=True)
def disc(cl,x,y,r,c,i=1.,soft=.015): cl.put(i*(1-sm(r-soft,r,np.hypot(X-x,Y-y))),c)
def ring(cl,x,y,r,w,c,i=1.):  d=np.abs(np.hypot(X-x,Y-y)-r); cl.put(i*(1-sm(w*.5,w,d)),c)
def star4(cl,x,y,r,c,i=1.,rot=0.,p=.55):
    """glossy 4-point star: astroid-ish field, white core, saturated rim, soft halo"""
    if r<=0.003 or i<=0: return
    cs,sn=np.cos(rot),np.sin(rot); u=((X-x)*cs+(Y-y)*sn)/r; v=(-(X-x)*sn+(Y-y)*cs)/r
    f=(np.abs(u)**p+np.abs(v)**p)**(1/p); c=np.asarray(c,float)
    glow(cl,x,y,r*.9,0,c,.35*i)
    body=1-sm(.9,1.02,f); cl.put(i*body,c)
    core=1-sm(.0,.55,f); cl.put(i*core*.95,np.array([1,1,1.]))
    hl=np.exp(-((u+.12)**2+(v+.18)**2)/.01); cl.put(i*hl*body*.8,np.array([1,1,1.]))
def stroke(cl,pts,w,c,i=1.,taper=True):
    """soft polyline; width tapers toward start"""
    best=np.full((N,N),9.); wt=np.zeros((N,N))
    for k in range(len(pts)-1):
        a=np.array(pts[k]);b=np.array(pts[k+1]);ab=b-a;L2=max(ab@ab,1e-9)
        t=np.clip(((X-a[0])*ab[0]+(Y-a[1])*ab[1])/L2,0,1); d=np.hypot(X-a[0]-t*ab[0],Y-a[1]-t*ab[1])
        m=d<best; best=np.where(m,d,best); wt=np.where(m,(k+t)/(len(pts)-1),wt)
    ww=w*(wt**.7 if taper else 1)+1e-4
    glow_a=np.exp(-(best/(ww*2.5))**2)*.35; cl.put(i*glow_a,np.asarray(c,float),add=True)
    cl.put(i*(1-sm(ww*.6,ww,best)),np.asarray(c,float)); cl.put(i*(1-sm(0,ww*.45,best))*.9,np.array([1,1,1.]))
def coin(cl,x,y,r,spin,i=1.):
    sx=max(abs(np.cos(spin)),.15)*r; e=np.hypot((X-x)/sx,(Y-y)/r)
    g=col('gold'); dk=g*.62
    cl.put(i*(1-sm(.94,1.02,e)),dk); cl.put(i*(1-sm(.74,.8,e)),g)
    cl.put(i*(1-sm(.35,.42,np.hypot((X-x)/sx,(Y-y)/r*1.0)))*.0,g)
    hl=np.exp(-(((X-x)/sx+.35)**2+((Y-y)/r+.4)**2)/.06); cl.put(i*hl*(e<1)*.85,np.array([1,1,.92]))
def strip(frames,name):
    im=Image.new('RGBA',(C*len(frames),C))
    for k,f in enumerate(frames): im.paste(f,(k*C,0))
    im.save(os.path.join(OD,name)); return im
def gif(frames,name,fps):
    out=[]
    for f in frames:
        bg=Image.new('RGBA',(C,C),(110,112,118,255)); bg.alpha_composite(f); out.append(bg.convert('P',palette=Image.ADAPTIVE,colors=255))
    out[0].save(os.path.join(OD,name),save_all=True,append_images=out[1:],duration=int(1000/fps),loop=0,disposal=2)
rng=np.random.default_rng(7); HUES=['red','blue','yellow','green','teal','purple','gold']
# ---------- 1) sparkle burst ----------
def sparkle(n=10):
    fr=[]; P=[(a,d,HUES[k%7],s) for k,(a,d,s) in enumerate(zip(np.linspace(0,2*np.pi,9,endpoint=False)+rng.uniform(-.2,.2,9),rng.uniform(.55,.8,9),rng.uniform(.14,.22,9)))]
    tw=[(rng.uniform(-.85,.85),rng.uniform(-.85,.85),rng.uniform(.25,.85)) for _ in range(10)]
    for f in range(n):
        t=f/(n-1); cl=Cell()
        if f<2: s=.18+.1*f; star4(cl,0,0,s*(1-.25*(f==1)),col('white')*.5+col('yellow')*.5,1,np.pi/4*f*.3,p=.6); glow(cl,0,0,.18,0,col('yellow'),.4)
        else:
            u=(f-2)/(n-3); fade=1-sm(.55,1,u)
            ring(cl,0,0,.15+.75*ease(u),.05*(1-u)+.01,col('white')*.6+col('gold')*.4,(1-u)**1.5*.9)
            glow(cl,0,0,.45*(1-u)+.05,0,col('yellow'),(1-u)**2*.9)
            star4(cl,0,0,.32*(1-ease(u*1.3))+.0,col('white'),max(0,1-u*1.4),u*.6)
            for a,d,c,s in P:
                r=d*ease(u*1.4); sz=s*(1.2 if u<.3 else 1)*(1-sm(.6,1,u)*.8)
                star4(cl,r*np.cos(a),r*np.sin(a),sz,col(c)*.8+.2,fade,u*1.5+a)
            for x,y,ph in tw:
                tt=np.clip((u-ph*.6)/.35,0,1); k=np.sin(np.pi*tt)
                if k>0: star4(cl,x*.95,y*.95,.09*k,col('white'),k,p=.45)
        fr.append(cl.img())
    return fr
# ---------- 2) magnet ----------
def magnet_cell(wob=0.,zap=0.,scale=1.):
    cl=Cell(); cs,sn=np.cos(wob),np.sin(wob); u=(X*cs+Y*sn)/scale; v=(-X*sn+Y*cs)/scale+.05
    R,hw,top=.42,.17,-.62
    arc=np.abs(np.hypot(u,v)-R); leg=np.minimum(np.hypot(u-R,np.clip(v-np.clip(v,top,0),-9,9)),np.hypot(u+R,v-np.clip(v,top,0)))
    leg=np.where((v>=top)&(v<0),np.minimum(np.abs(np.abs(u)-R),9),np.hypot(np.abs(np.abs(u)-R),v-top))
    d=np.where(v>0,arc,leg)-hw; d=np.where(v<top,np.maximum(np.abs(np.abs(u)-R),top-v)-hw+.0,d)
    d=np.where(v<top,np.maximum(np.abs(np.abs(u)-R)-hw,top-.0-v-.0),d)
    m=1-sm(-.012,.008,d); h=np.sqrt(np.clip(1-((d+hw)/hw-1)**2,0,1))   # cross-section bulge
    # light from top-left: lateral coordinate across tube
    lat=np.where(v>0,(np.hypot(u,v)-R)/hw,(np.abs(u)-R)/hw)
    tip=v<top+.26
    base=np.where(tip[...,None],col('silver'),col('red'))
    shade=(.55+.45*h)[...,None]*base+(np.exp(-((lat+.35)**2)/.05)*.75*h)[...,None]
    cl.put(m*0+(1-sm(-.0,.06,d))*.35,np.array([.2,.02,.03]))  # soft dark rim/shadow
    cl.put(m,np.clip(shade,0,1))
    sep=(np.abs(v-(top+.26))<.012)&(d<0); cl.put(sep*.6,np.array([.35,.35,.4]))
    if zap>0:
        for sx in(-R,R):
            for k in range(3):
                a=-np.pi/2+(k-1)*.55+sx*.3; L=.18+.08*zap
                px=sx*scale; py=(top-.05)*scale
                pts=[(px+np.cos(a)*L*j/3+(.03*(-1)**j),py+np.sin(a)*L*j/3) for j in range(4)]
                stroke(cl,[(p[0]*cs-p[1]*sn,p[0]*sn+p[1]*cs) for p in pts],.022,col('teal')*.5+col('yellow')*.5,zap,taper=False)
        glow(cl,0,(top-.08)*scale,.25,0,col('yellow'),.35*zap)
    return cl
def magnet_loop(n=6):
    fr=[]
    for f in range(n):
        t=f/n; z=max(0,np.sin(2*np.pi*t*2))**2 if f%3==1 else (.35 if f%3==2 else 0)
        fr.append(magnet_cell(np.radians(7)*np.sin(2*np.pi*t),z,.92+.03*np.cos(4*np.pi*t)).img())
    return fr
def pull(n=10):
    fr=[]; tgt=(.62,0.); L=[(rng.uniform(-1.1,-.6),rng.uniform(-.8,.8),rng.uniform(0,1),HUES[k%7]) for k,_ in enumerate(range(7))]
    for f in range(n):
        t=f/n; cl=Cell(); glow(cl,*tgt,.22,0,col('yellow'),.5+.2*np.sin(2*np.pi*t))
        for sx,sy,ph,c in L:
            s=(t+ph)%1; a=ease(s); hx=sx+(tgt[0]-sx)*a; hy=sy*(1-a)
            pts=[]
            for j in range(8):
                q=max(0,a-.32*(1-j/7)); x=sx+(tgt[0]-sx)*q; y=sy*(1-q)+.07*np.sin(q*14+ph*6)*(1-q)
                pts.append((x,y))
            i=np.sin(np.pi*s)**.6
            stroke(cl,pts,.035,col(c)*.75+.25,i)
            star4(cl,pts[-1][0],pts[-1][1],.05,col('white'),i*.9,p=.5)
        for k in range(3):  # wavy rings converging
            s=(t+k/3)%1; ring(cl,*tgt,.55*(1-s)+.05,.025,col('teal')*.5+.5,np.sin(np.pi*s)*.5)
        fr.append(cl.img())
    return fr
def impact(n=9):
    fr=[]; ang=np.linspace(0,2*np.pi,12,endpoint=False)
    for f in range(n):
        u=f/(n-1); cl=Cell(); fd=1-sm(.5,1,u)
        glow(cl,0,0,.3*(1-u)+.05,0,col('teal')*.4+col('white')*.6,(1-u)**2.5)
        ring(cl,0,0,.12+.78*ease(u),.07*(1-u)+.012,col('red')*.6+.4,(1-u)*.95)
        ring(cl,0,0,.08+.55*ease(u),.04*(1-u)+.01,col('silver'),(1-u)*.8)
        for k,a in enumerate(ang):
            r0=.15+.55*ease(u); r1=r0+.22*(1-u)+.03
            stroke(cl,[(r0*np.cos(a),r0*np.sin(a)),(r1*np.cos(a),r1*np.sin(a))],.03,col('red' if k%2 else 'yellow')*.7+.3,fd,taper=False)
        for k,a in enumerate(ang[::2]+.26): r=.3+.5*ease(u); star4(cl,r*np.cos(a),r*np.sin(a),.08*(1-u*.7),col(HUES[k%7])*.8+.2,fd,u*2)
        if f<2: star4(cl,0,0,.35-.1*f,col('white'),1)
        fr.append(cl.img())
    return fr
# ---------- 3) super glow ----------
def sweep(n=10):
    fr=[]
    for f in range(n):
        t=f/(n-1); cl=Cell(); pos=-1.9+3.8*ease(t*.95+.02) if 0 else -1.8+3.6*t
        d=(X+Y)/np.sqrt(2)-pos*0.72*np.sqrt(2)/1.0
        fade=np.sin(np.pi*t)**.5; edge=1-sm(.7,1.0,np.maximum(np.abs(X),np.abs(Y)))
        cl.put(np.exp(-(d/.35)**2)*.45*fade*edge,col('gold'),add=True)
        cl.put(np.exp(-(d/.12)**2)*.75*fade*edge,col('gold')*.4+.6)
        cl.put(np.exp(-((d-.22)/.04)**2)*.55*fade*edge,np.array([1,1,.9]))
        hx=pos*.5; star4(cl,hx,hx,.1*fade,col('white'),fade,t*2,p=.5)
        fr.append(cl.img())
    return fr
def goldpop(n=10):
    fr=[]; th=np.arctan2(Y,X); rr=np.hypot(X,Y)
    items=[(a,rng.uniform(.5,.85),k%2) for k,a in enumerate(np.linspace(0,2*np.pi,8,endpoint=False)+.2)]
    for f in range(n):
        u=f/(n-1); cl=Cell(); fd=1-sm(.6,1,u)
        if f==0: glow(cl,0,0,.15,0,col('gold'),.8); star4(cl,0,0,.12,col('gold'),1); fr.append(cl.img()); continue
        rays=(np.cos(th*12+u*1.5)*.5+.5)**6*np.exp(-(rr/(.25+.7*ease(u)))**2)*(1-sm(.0,.12,rr)*0)
        cl.put(rays*(1-u)**1.2*.9,col('gold')*.6+.4,add=True)
        glow(cl,0,0,.4*(1-u)+.1,0,col('gold'),(1-u)*.9)
        ring(cl,0,0,.1+.8*ease(u),.05*(1-u)+.01,col('gold')*.5+.5,(1-u))
        star4(cl,0,0,.3*(1-ease(u*1.2)),col('white')*.4+col('gold')*.6,max(0,1-u*1.3),u)
        for a,d,kind in items:
            r=d*ease(u*1.3); x,y=r*np.cos(a),r*np.sin(a)+.25*u*u
            if kind: coin(cl,x,y,.09,u*7+a,fd)
            else: star4(cl,x,y,.1*(1-u*.5),col('gold')*.85+.15,fd,u*2+a)
        fr.append(cl.img())
    return fr
if __name__=='__main__':
    A=dict(sparkle=sparkle(), icon=magnet_loop(), pull=pull(), impact=impact(), sweep=sweep(), gpop=goldpop())
    magnet_cell(0,0,.95).img().save(os.path.join(OD,'magnet_icon.png'))
    strip(A['sparkle'],'fx_sparkle_burst.png'); gif(A['sparkle'],'fx_sparkle_burst_preview.gif',20)
    # combined sheets (rows) + per-part strips
    def rows(groups,name):
        w=max(len(g) for g in groups); im=Image.new('RGBA',(w*C,C*len(groups)))
        for r,g in enumerate(groups):
            for k,f in enumerate(g): im.paste(f,(k*C,r*C))
        im.save(os.path.join(OD,name))
    strip(A['icon'],'fx_magnet_icon_loop.png'); strip(A['pull'],'fx_magnet_pull.png'); strip(A['impact'],'fx_magnet_impact.png')
    strip(A['icon']+A['pull']+A['impact'],'fx_magnet.png')
    gif(A['icon']*2+A['pull']+A['impact'],'fx_magnet_preview.gif',16)
    strip(A['sweep'],'fx_super_glow_sweep.png'); strip(A['gpop'],'fx_super_glow_pop.png')
    strip(A['sweep']+A['gpop'],'fx_super_glow.png'); gif(A['sweep']+A['gpop'],'fx_super_glow_preview.gif',20)
    # contact sheet: each row on alternating asphalt / terracotta / gray
    G=list(A.values()); w=max(map(len,G)); S=Image.new('RGBA',(w*C,C*len(G)*2))
    for r,g in enumerate(G):
        for b,bgc in enumerate([(52,54,60,255),(196,104,72,255)]):
            row=Image.new('RGBA',(w*C,C),bgc)
            for k,f in enumerate(g): row.alpha_composite(f,(k*C,0))
            S.paste(row,(0,(2*r+b)*C))
    S.resize((S.width//2,S.height//2),Image.LANCZOS).save(os.path.join(OD,'fx_sheet_preview.png'))
    print({k:len(v) for k,v in A.items()})
