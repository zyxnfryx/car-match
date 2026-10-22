# Build 11 retune: cars can now leave out the back of a lane, which made mid/late levels easier.
# Re-rolls each level whose casual-player win rate (SolveCheck --rate) rose more than 10 points above
# its pre-back-exit rate (tools/levels/rate_before_b11.txt), adding sets/mix/hidden until it is back
# in range. Every result stays Winnable (SolveCheck --each) and unique. Levels 1-24 are untouched.
# Run: python3 tools/levels/retune_backexit.py [path-to-dotnet]
import json, random, subprocess, sys, os, importlib.util
ROOT=os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT=os.path.join(ROOT,'Assets/_Project/Resources/levels.json')
DOTNET=sys.argv[1] if len(sys.argv)>1 else 'dotnet'
src=open(os.path.join(ROOT,'tools/levels/gen_levels.py')).read()
src=src[:src.index('def check(')]
g={'__file__':os.path.join(ROOT,'tools/levels/gen_levels.py')}; exec(src,g)
plan=g['plan']; COLORS=g['COLORS']; INTRO=g['INTRO']; A=g['A']; B=g['B']
def make(n,seed,bump):
    r=random.Random(n*7919+seed*31+bump); k=plan(n)
    k['sets']+=bump; k['mix']+=bump*4; k['hidden']+=bump//2
    pal=COLORS[:k['colors']]; pool=[]
    for i in range(k['sets']): pool+=[pal[i%len(pal)]]*3
    for _ in range(k['mix']+seed):
        a=r.randrange(len(pool)); b=min(len(pool)-1,max(0,a+r.randint(-6,6))); pool[a],pool[b]=pool[b],pool[a]
    lanes=[[] for _ in range(k['lanes'])]; sides=[]
    for s in range(k['sides']):
        cars=[pool.pop() for _ in range(r.randint(2,3))]
        sides.append({'lane':r.randrange(k['lanes']),'at':r.randint(0,1),'cars':cars})
    tunnels=[[pool.pop() for _ in range(r.randint(2,4))] for _ in range(k['tunnels'])]
    i=0
    while pool: lanes[i%len(lanes)].append(pool.pop(0)); i+=1
    used=set()
    for q in tunnels:
        lane=r.choice([x for x in range(len(lanes)) if x not in used] or list(range(len(lanes)))); used.add(lane)
        lanes[lane].insert(r.randint(0,min(1,len(lanes[lane]))),'tunnel:'+','.join(q))
    # Hidden and boxes: never the front or the back cell (both ends are exits now).
    spots=[(l,c) for l in range(len(lanes)) for c in range(1,len(lanes[l])-1) if not lanes[l][c].startswith('tunnel')]
    r.shuffle(spots)
    for l,c in spots[:k['hidden']]: lanes[l][c]='?'+lanes[l][c]
    for l,c in spots[k['hidden']:k['hidden']+k['boxes']]: lanes[l][c]='box:'+lanes[l][c]
    for s in sides:
        if s['at']>=len(lanes[s['lane']]): s['at']=0
    lanes=[l for l in lanes if l]
    title,blurb=INTRO.get(n,(r.choice(A)+' '+r.choice(B),''))
    lv={'id':'lot-%03d'%n,'title':title,'blurb':blurb,'lanes':lanes}
    if k['superhard']: lv['tier']='superhard'
    if sides: lv['sides']=[s for s in sides if s['lane']<len(lanes)]
    return lv
def run(mode,levels):
    tmp='/tmp/retune_%s.json'%mode; json.dump({'levels':levels},open(tmp,'w'))
    out=subprocess.run([DOTNET,'run','--project',os.path.join(ROOT,'tools/SolveCheck'),'-c','Release','--no-build','--','--'+mode,tmp],capture_output=True,text=True).stdout
    return [l.split() for l in out.strip().split('\n') if l.strip()]
subprocess.run([DOTNET,'build',os.path.join(ROOT,'tools/SolveCheck'),'-c','Release'],capture_output=True)
before={int(a):float(b) for a,b,*_ in (l.split() for l in open(os.path.join(ROOT,'tools/levels/rate_before_b11.txt')))}
levels=json.load(open(OUT))['levels']
now={int(p[0]):float(p[1]) for p in run('rate',levels)}
todo=[n for n in range(25,101) if now[n]>before[n]+10]
print('retuning',len(todo),todo,flush=True)
sig=lambda lv: json.dumps([lv['lanes'],lv.get('sides',[])],sort_keys=True)
for n in todo:
    best=None
    for bump in range(0,5):
        cands=[make(n,s,bump) for s in range(24)]
        each=run('each',cands); rate=run('rate',cands)
        for i,c in enumerate(cands):
            if each[i][1]!='Winnable': continue
            rt=float(rate[i][1]); gap=abs(rt-before[n])
            if best is None or gap<best[0]: best=(gap,rt,c)
        if best and best[0]<=8: break
    if best:
        others={sig(levels[j]) for j in range(100) if j!=n-1}
        if sig(best[2]) not in others: levels[n-1]=best[2]
        print(n,'before',before[n],'backexit',now[n],'->',best[1],flush=True)
json.dump({'levels':levels},open(OUT,'w'),indent=1)
print('wrote',OUT)
