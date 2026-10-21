# Generates Assets/_Project/Resources/levels.json: 100 levels, numbered in order, on the ladder in
# docs/mechanics-ladder.md. Every level is checked with the game's own solver (tools/SolveCheck --each)
# and re-rolled until it is Winnable. Run: python3 tools/levels/gen_levels.py [path-to-dotnet]
import json, random, subprocess, sys, os
ROOT=os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT=os.path.join(ROOT,'Assets/_Project/Resources/levels.json')
DOTNET=sys.argv[1] if len(sys.argv)>1 else 'dotnet'
COLORS=['coral','teal','amber','lime','violet','sky','rose','gold']
A=['Sunny','Corner','Market','Harbor','Maple','Cedar','Station','River','Garden','Hilltop','Plaza','Lantern','Bakery','Library','Meadow','Canal','Orchard','Seaside','Festival','Rooftop','Midtown','Park','Pier','Willow','Arcade']
B=['Lot','Lane','Row','Yard','Square','Loop','Court','Deck','Bay','Stop','Drive','Garage','Strip','Block','Circle']
INTRO={1:("First Lot","Tap a car with a clear lane."),3:("Three Streets","Three colors, three lanes."),
       16:("Curb Cut","Side lots open when the lane mouth is clear."),
       26:("Mystery Row","A '?' car shows its color once the car in front leaves."),
       36:("Tunnel Mouth","Empty the tunnel before the cars behind it can leave."),
       51:("Full Lot","Everything together.")}
def plan(n):
    """Smooth curve. Returns dict of knobs for level n (1..100)."""
    t=(n-1)/99
    breather = n%10==0 and n not in INTRO
    lanes=min(6,2+int(round(t*4.4)))
    colors=min(8,2+int(round(t*6.2)))
    sets=colors+int(round(t*colors*1.2))
    mix=int(round(1+t*30))
    sides = 1 if n>=16 else 0
    if n>=40: sides=1+(n%3==0)
    hidden = 0 if n<26 else min(6,1+(n-26)//10)
    tunnels = 0 if n<36 else min(3,1+(n-36)//22)
    if n in (16,26,36):  # intro levels stay gentle
        lanes=max(2,lanes-1); colors=max(3,colors-2); sets=colors; mix=2
    if breather:
        sets=max(colors,sets-colors//2); mix=max(2,mix//2)
    return dict(lanes=lanes,colors=colors,sets=sets,mix=mix,sides=sides,hidden=hidden,tunnels=tunnels)
def make(n,seed):
    r=random.Random(n*1000+seed); k=plan(n)
    pal=COLORS[:k['colors']]
    pool=[]
    for i in range(k['sets']): pool+= [pal[i%len(pal)]]*3
    # Start grouped, then a growing number of swaps mixes colors apart.
    for _ in range(k['mix']):
        a=r.randrange(len(pool)); b=min(len(pool)-1,max(0,a+r.randint(-6,6))); pool[a],pool[b]=pool[b],pool[a]
    lanes=[[] for _ in range(k['lanes'])]
    sides=[]
    for s in range(k['sides']):
        cnt=r.randint(2,3); cars=[pool.pop() for _ in range(cnt)]
        sides.append({'lane':r.randrange(k['lanes']),'at':r.randint(0,1),'cars':cars})
    tunnels=[]
    for t in range(k['tunnels']):
        cnt=r.randint(2,4); tunnels.append([pool.pop() for _ in range(cnt)])
    i=0
    while pool:
        lanes[i%len(lanes)].append(pool.pop(0)); i+=1
    used=set()
    for q in tunnels:
        lane=r.choice([x for x in range(len(lanes)) if x not in used] or list(range(len(lanes)))); used.add(lane)
        at=r.randint(0,min(1,len(lanes[lane])))
        lanes[lane].insert(at,'tunnel:'+','.join(q))
    # Hidden: never the front cell of a lane.
    spots=[(l,c) for l in range(len(lanes)) for c in range(1,len(lanes[l])) if not lanes[l][c].startswith('tunnel')]
    r.shuffle(spots)
    for l,c in spots[:k['hidden']]: lanes[l][c]='?'+lanes[l][c]
    for s in sides:
        if s['at']>=len(lanes[s['lane']]): s['at']=0
    lanes=[l for l in lanes if l] or [['coral','coral','coral']]
    title,blurb=INTRO.get(n,(r.choice(A)+' '+r.choice(B),''))
    lv={'id':'lot-%03d'%n,'title':title,'blurb':blurb,'lanes':lanes}
    if sides: lv['sides']=[s for s in sides if s['lane']<len(lanes)]
    return lv
def check(levels):
    tmp='/tmp/levels_check.json'; json.dump({'levels':levels},open(tmp,'w'))
    out=subprocess.run([DOTNET,'run','--project',os.path.join(ROOT,'tools/SolveCheck'),'-c','Release','--no-build','--','--each',tmp],capture_output=True,text=True).stdout
    res={}
    for line in out.split('\n'):
        p=line.split()
        if len(p)>=2 and p[0].isdigit(): res[int(p[0])]=p[1]
    return res
subprocess.run([DOTNET,'build',os.path.join(ROOT,'tools/SolveCheck'),'-c','Release'],capture_output=True)
seeds={n:0 for n in range(1,101)}
levels=[make(n,0) for n in range(1,101)]
for rnd in range(60):
    res=check(levels)
    bad=[n for n in range(1,101) if res.get(n)!='Winnable']
    print('round',rnd,'unsolved',len(bad),bad[:12],flush=True)
    if not bad: break
    for n in bad:
        seeds[n]+=1; levels[n-1]=make(n,seeds[n])
json.dump({'levels':levels},open(OUT,'w'),indent=1)
print('wrote',OUT)
