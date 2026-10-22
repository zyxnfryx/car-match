# Underground garages (ladder #3b, intro level 21). Adds a garage to levels 21-100 in place
# (21 is replaced by a gentle intro). The hatch covers two neighbouring parked spots in one lane;
# its queue is whole sets (3 or 6 cars) of colors already in the level, so color counts stay
# multiples of 3. Every result must stay Winnable (SolveCheck --each) and unique; mechanic intro
# levels 26, 31 and 36 are left alone so each mechanic still debuts on its own.
# Run: python3 tools/levels/add_garages.py [path-to-dotnet]
import json, random, subprocess, sys, os
ROOT=os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT=os.path.join(ROOT,'Assets/_Project/Resources/levels.json')
DOTNET=sys.argv[1] if len(sys.argv)>1 else 'dotnet'
SKIP={26,31,36}
def colors_of(lv):
    cs=set()
    for lane in lv['lanes']:
        for t in lane:
            t=t.replace('box:','').replace('?','')
            if t.startswith('tunnel:'): cs.update(t[7:].split(','))
            elif t and t!='-': cs.add(t)
    return sorted(cs)
def add(lv,n,seed):
    r=random.Random(n*101+seed); lv=json.loads(json.dumps(lv))
    lanes=lv['lanes']
    cand=[(l,c) for l in range(len(lanes)) for c in range(len(lanes[l])-1)
          if not any(lanes[l][c+k].startswith('tunnel') for k in (0,1))]
    if not cand: return None
    l,c=r.choice(cand)
    sets=1 if n<51 else 2
    pal=colors_of(lv) or ['coral']
    q=[]
    for s in range(sets): q+= [r.choice(pal)]*3
    r.shuffle(q)
    lv['garages']=[{'lane':l,'cells':[c,c+1],'cars':q}]
    return lv
def intro21():
    return {'id':'lot-021','title':'Underground','blurb':'Clear the spots on a hatch and more cars drive up from below.',
            'lanes':[['coral','teal','teal'],['teal','coral','amber'],['amber','amber','coral']],
            'garages':[{'lane':1,'cells':[0,1],'cars':['lime','lime','lime']}]}
def run(levels):
    tmp='/tmp/garage_check.json'; json.dump({'levels':levels},open(tmp,'w'))
    out=subprocess.run([DOTNET,'run','--project',os.path.join(ROOT,'tools/SolveCheck'),'-c','Release','--no-build','--','--each',tmp],capture_output=True,text=True).stdout
    return [l.split() for l in out.strip().split('\n') if l.strip()]
subprocess.run([DOTNET,'build',os.path.join(ROOT,'tools/SolveCheck'),'-c','Release'],capture_output=True)
levels=json.load(open(OUT))['levels']
levels[20]=intro21()
assert run([levels[20]])[0][1]=='Winnable', 'intro 21 must be winnable'
for n in range(22,101):
    if n in SKIP: continue
    cands=[x for x in (add(levels[n-1],n,s) for s in range(16)) if x]
    res=run(cands)
    ok=[cands[i] for i in range(len(cands)) if res[i][1]=='Winnable']
    if ok: levels[n-1]=ok[0]
    print(n,'garage' if ok else 'kept (no winnable placement)',flush=True)
json.dump({'levels':levels},open(OUT,'w'),indent=1)
print('wrote',OUT)
