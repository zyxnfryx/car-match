import numpy as np
from PIL import Image, ImageFilter, ImageDraw
R=512; OUT=256; HALF=1.1
el=np.radians(45)
fwd=np.array([0,-np.sin(el),-np.cos(el)]); up=np.array([0,np.cos(el),-np.sin(el)]); rt=np.array([1.,0,0])
j,i=np.mgrid[0:R,0:R].astype(np.float32)
sx=(i/R-0.5)*2*HALF; sy=-(j/R-0.5)*2*HALF
O=(rt[:,None,None]*sx+up[:,None,None]*(sy)) .transpose(1,2,0)+np.array([0,0.45,0])-fwd*4
O=O.reshape(-1,3); D=np.broadcast_to(fwd,O.shape)
def rbox(p,c,b,r):
    q=np.abs(p-c)-(np.array(b)-r); return np.linalg.norm(np.maximum(q,0),axis=1)+np.minimum(q.max(1),0)-r
def cyl(p,c,rad,hw):
    q=p-c; d=np.stack([np.sqrt(q[:,1]**2+q[:,2]**2)-rad,np.abs(q[:,0])-hw],1)
    return np.minimum(d.max(1),0)+np.linalg.norm(np.maximum(d,0),axis=1)-0.03
def smin(a,b,k): h=np.clip(0.5+0.5*(b-a)/k,0,1); return b*(1-h)+a*h-k*h*(1-h)
def sdf(p):
    body=rbox(p,[0,0.4,0],[0.56,0.27,0.86],0.24)
    cab=rbox(p,[0,0.72,-0.12],[0.44,0.22,0.42],0.2)
    car=smin(body,cab,0.08)
    w=np.full(len(p),9.)
    for x in(-0.5,0.5):
        for z in(-0.52,0.52): w=np.minimum(w,cyl(p,np.array([x,0.21,z]),0.21,0.1))
    return car,w,cab
def rot(a): c,s=np.cos(a),np.sin(a); return np.array([[c,0,s],[0,1,0],[-s,0,c]])
def geom(a):
    M=rot(a)  # local->world ; world->local = M.T
    o=O@M; d=D@M  # rows: v@M == M.T v
    t=np.zeros(len(o)); hit=np.zeros(len(o),bool); act=np.ones(len(o),bool)
    for _ in range(90):
        idx=np.where(act)[0]
        if len(idx)==0: break
        p=o[idx]+d[idx]*t[idx,None]; c,w,_=sdf(p); s=np.minimum(c,w)
        t[idx]+=s; h=s<1e-3; hit[idx[h]]=True; act[idx[h|(t[idx]>8)]]=False
    p=o+d*t[:,None]; e=1e-3; n=np.zeros_like(p)
    for k in range(3):
        dv=np.zeros(3); dv[k]=e; a1=np.minimum(*sdf(p+dv)[:2]); a2=np.minimum(*sdf(p-dv)[:2]); n[:,k]=a1-a2
    n/=np.linalg.norm(n,axis=1,keepdims=True)+1e-9
    c,w,cab=sdf(p); iswheel=w<c
    # ground for shadow: y=0 plane
    tg=-o[:,1]/d[:,1]; g=o+d*tg[:,None]
    gs=rbox(np.stack([g[:,0],np.zeros(len(g)),g[:,2]],1),[0,0,0],[0.6,0.01,0.9],0.3)
    return dict(p=p,n=n,hit=hit,wheel=iswheel,cab=cab<0.02,gs=gs,M=M)
def sm(e0,e1,v): t=np.clip((v-e0)/(e1-e0),0,1); return t*t*(3-2*t)
L=np.array([-0.3,0.9,0.35]); L/=np.linalg.norm(L)
def shade(G,base,closed=False,lid=0.0,wide=1.0):
    p,n,M=G['p'],G['n'],G['M']; nw=n@M.T  # world normals
    V=-fwd; H=(L+V); H/=np.linalg.norm(H)
    dif=np.clip(nw@L,0,1); spec=np.clip(nw@H,0,1); fres=(1-np.clip(nw@V,0,1))**3
    col=np.tile(base,(len(p),1)); ks=np.full(len(p),0.55); pw=np.full(len(p),40.)
    glass=G['cab']&(np.abs(n[:,1])<0.55)&(p[:,1]>0.62)
    col[glass]=[0.17,0.23,0.33]; ks[glass]=0.5; pw[glass]=25
    wh=G['wheel']; col[wh]=[0.12,0.12,0.15]; ks[wh]=0.25
    hub=wh&(np.abs(n[:,0])>0.8)&(np.sqrt((p[:,1]-0.21)**2+np.minimum((p[:,2]-0.52)**2,(p[:,2]+0.52)**2))<0.1)
    col[hub]=[0.75,0.75,0.8]
    top=(~wh)&(n[:,1]>0.2); u,v=p[:,0],p[:,2]
    def paint(m,c):
        nonlocal col; m=(m*top)[:,None]; col=col*(1-m)+np.array(c)*m
    aa=0.012
    # lights
    for x in(-0.36,0.36):
        r=np.sqrt(((u-x)/0.11)**2+((p[:,1]-0.45)/0.08)**2); m=(1-sm(1-aa*8,1,r))*(n[:,2]>0.4)*~wh
        col=col*(1-m[:,None])+np.array([1,.96,.75])*m[:,None]
        m=(1-sm(1-aa*8,1,r))*(n[:,2]<-0.4)*~wh; col=col*(1-m[:,None])+np.array([.85,.15,.15])*m[:,None]
    # eyes in the windshield (front face of the cabin), not on the roof
    ws=G['cab']&(n[:,2]>0.3)&(p[:,1]>0.66)&~wh
    def wpaint(m,c):
        nonlocal col; m=(m*ws)[:,None]; col=col*(1-m)+np.array(c)*m
    yb=p[:,1]; ec=0.80
    for x in(-0.17,0.17):
        if not closed:
            er=0.11*wide; eye=1-sm(er-aa,er,np.hypot(u-x,(yb-ec)*0.9))
            wpaint(eye,[1,1,1])
            wpaint(1-sm(0.062*wide-aa,0.062*wide,np.hypot(u-x*0.9,yb-ec+0.02)),[.06,.06,.1])
            wpaint(1-sm(0.02-aa,0.02,np.hypot(u-x*0.9+0.022,yb-ec+0.0)),[1,1,1])
            if lid>0:
                edge=ec+er/0.9-lid*2*er/0.9
                wpaint(eye*sm(edge-0.01,edge+0.01,yb),list(base*0.85))
                wpaint(eye*(1-sm(0.008,0.016,np.abs(yb-edge))),[.08,.06,.1])
        else:
            d=np.abs(np.hypot(u-x,yb-ec-0.03)-0.08); wpaint((1-sm(0.024-aa,0.02,d))*(yb<ec+0.03),[.95,.95,1])
    # mouth on hood
    mz=0.45; hd=(p[:,1]>0.45)&(v>0.3)
    if not closed:
        r=np.hypot(u,v-mz); m=(1-sm(0.2-aa,0.2,r))*(v>mz)*hd; paint(m,[.3,.04,.08])
        paint((1-sm(0.08-aa,0.08,np.hypot(u,v-mz-0.16)))*m,[1,.45,.5])
    else:
        d=np.abs(np.hypot(u,v-mz-0.14)-0.11); paint((1-sm(0.026-aa,0.022,d))*(v<mz+0.14)*(np.abs(u)<0.08)*hd,[.25,.04,.08])
    amb=0.5+0.25*nw[:,1]
    c=col*(amb+0.55*dif)[:,None]+(ks*spec**pw*0.8+0.08*fres)[:,None]
    return np.clip(c,0,1)
def image(G,c):
    a=G['hit'].astype(np.float32); sh=np.clip(1-sm(-0.15,0.25,G['gs']),0,1)
    shimg=np.asarray(Image.fromarray((sh.reshape(R,R)*255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(12)),np.float32)/255*0.75
    rgb=np.zeros((R*R,3)); rgb[G['hit']]=c[G['hit']]
    A=np.maximum(a.reshape(R,R),shimg); rgbA=rgb.reshape(R,R,3)*(a.reshape(R,R)/np.maximum(A,1e-6))[...,None]
    out=np.dstack([rgbA/np.maximum(A,1e-6)[...,None]*0+rgb.reshape(R,R,3)*(a.reshape(R,R)>0)[...,None],A])
    # premultiplied composite of car over black shadow
    out=np.dstack([rgb.reshape(R,R,3)*a.reshape(R,R)[...,None]/np.maximum(A,1e-6)[...,None],A])
    return Image.fromarray((np.clip(out,0,1)*255).astype(np.uint8),'RGBA').resize((OUT,OUT),Image.LANCZOS)
cols={'red':(222,52,52),'blue':(40,110,225),'yellow':(245,196,30),'green':(70,180,60),'teal':(25,170,165),'purple':(140,75,205)}
dirs={'S':0,'SE':45,'E':90,'NE':135,'N':180,'NW':225,'W':270,'SW':315}
Dd='/workspace/carmatch/art/cars/'
import os
for f in os.listdir(Dd):
    if f.endswith('.png'): os.remove(Dd+f)
Gs={k:geom(np.radians(v)) for k,v in dirs.items()}
for n,c in cols.items():
    b=np.array(c)/255
    for k,G in Gs.items():
        im=image(G,shade(G,b)); im.save(Dd+f'car_{n}_open_{k}.png')
        if k=='S': im.save(Dd+f'car_{n}_open.png')
    image(Gs['S'],shade(Gs['S'],b,True)).save(Dd+f'car_{n}_closed.png')
sheet=Image.new('RGBA',(8*256+90,3*256+40+340),(118,120,124,255)); dr=ImageDraw.Draw(sheet)
for x in range(0,sheet.width,48): dr.line([(x,0),(x,sheet.height)],fill=(110,112,116),width=3)
for ci,n in enumerate(cols):
    sheet.alpha_composite(Image.open(Dd+f'car_{n}_open.png'),(10+ci*266,10))
    sheet.alpha_composite(Image.open(Dd+f'car_{n}_closed.png'),(10+ci*266,276))
for k,dn in enumerate(['N','NE','E','SE','S','SW','W','NW']):
    sheet.alpha_composite(Image.open(Dd+f'car_red_open_{dn}.png'),(10+k*266,542))
M='/workspace/carmatch/art/mascot/'
for i,s in enumerate(('idle','happy','cheer')):
    sheet.alpha_composite(Image.open(M+f'mascot_{s}.png').resize((320,320),Image.LANCZOS),(10+i*340,808))
sheet.convert('RGB').save(Dd+'cars_sheet.png')
