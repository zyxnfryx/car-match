# Lot Loop v2 car frames (reuses render_cars3d.py geometry + windshield eyes).
# Writes: cars/ (8 headings + idle, via render_cars3d.py), cars_v2/drive/car_<c>_h00..h15.png (16 headings, 22.5deg, h00=S, clockwise like dirs),
#         cars_v2/eyes/car_<c>_eye<f>_h<NN>.png  f=0 closed(arc) 1 lid .7 2 lid .35 3 wide overshoot 4 open
import numpy as np, os
from PIL import Image
src=open(os.path.join(os.path.dirname(os.path.abspath(__file__)),'render_cars3d.py')).read().split("cols={")[0]
exec(src)
cols={'red':(222,52,52),'blue':(40,110,225),'yellow':(245,196,30),'green':(70,180,60),'teal':(25,170,165),'purple':(140,75,205)}
OUTD='/workspace/carmatch/art/cars_v2/'
os.makedirs(OUTD+'drive',exist_ok=True); os.makedirs(OUTD+'eyes',exist_ok=True)
import sys
only=sys.argv[1:] or list(cols)
for h in range(16):
    G=geom(np.radians(h*22.5))
    for n in only:
        b=np.array(cols[n])/255
        image(G,shade(G,b)).resize((192,192),Image.LANCZOS).save(OUTD+f'drive/car_{n}_h{h:02d}.png')
        for f,kw in enumerate([dict(closed=True),dict(lid=0.7),dict(lid=0.35),dict(wide=1.12),dict()]):
            image(G,shade(G,b,**kw)).resize((192,192),Image.LANCZOS).save(OUTD+f'eyes/car_{n}_eye{f}_h{h:02d}.png')
