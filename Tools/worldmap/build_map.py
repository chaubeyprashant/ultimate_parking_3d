"""Natural Earth 1:110m (public domain) -> triangulated world map JSON for the World Tour screen.
Miller projection, latitudes LAT_MIN..LAT_MAX, coordinates normalised to 0..1 (y up)."""
import json, math, sys
from earcut import earcut
LAT_MIN, LAT_MAX = -57.0, 80.0
def miller(lat): return 1.25 * math.log(math.tan(math.pi / 4 + 0.4 * math.radians(lat)))
Y0, Y1 = miller(LAT_MIN), miller(LAT_MAX)
def proj(lon, lat):
    lat = max(LAT_MIN, min(LAT_MAX, lat))
    return ((lon + 180.0) / 360.0, (miller(lat) - Y0) / (Y1 - Y0))

def area(p): return sum(p[i][0]*p[(i+1)%len(p)][1]-p[(i+1)%len(p)][0]*p[i][1] for i in range(len(p)))/2
def cross(o,a,b): return (a[0]-o[0])*(b[1]-o[1])-(a[1]-o[1])*(b[0]-o[0])
def in_tri(p,a,b,c):
    d1,d2,d3=cross(a,b,p),cross(b,c,p),cross(c,a,p)
    return d1>=0 and d2>=0 and d3>=0
def earclip(pts):
    pts=[p for i,p in enumerate(pts) if i==0 or p!=pts[i-1]]
    if pts[0]==pts[-1]: pts=pts[:-1]
    if area(pts)<0: pts=pts[::-1]
    idx=list(range(len(pts))); tris=[]; guard=0
    while len(idx)>3 and guard<len(idx)*len(idx)+10:
        guard+=1; n=len(idx); found=False
        reflex={idx[i] for i in range(n) if cross(pts[idx[i-1]],pts[idx[i]],pts[idx[(i+1)%n]])<=0}
        for i in range(n):
            a,b,c=idx[i-1],idx[i],idx[(i+1)%n]
            if b in reflex: continue
            if any(in_tri(pts[r],pts[a],pts[b],pts[c]) for r in reflex if r not in (a,b,c)): continue
            tris+= [a,b,c]; idx.pop(i); found=True; break
        if not found:   # degenerate remainder: drop a collinear vertex
            for i in range(n):
                a,b,c=idx[i-1],idx[i],idx[(i+1)%n]
                if abs(cross(pts[a],pts[b],pts[c]))<1e-12: idx.pop(i); found=True; break
            if not found: tris+=[idx[-1],idx[0],idx[1]]; idx.pop(0)
    if len(idx)==3: tris+=idx
    return pts,tris

def rings(geom):
    polys=[geom["coordinates"]] if geom["type"]=="Polygon" else geom["coordinates"]
    for p in polys:
        ring=p[0]
        if max(pt[1] for pt in ring) < LAT_MIN: continue          # Antarctica
        yield [proj(lon,lat) for lon,lat in ring]

def mesh(geoms):
    V=[];T=[]
    for g in geoms:
        for ring in rings(g):
            if len(ring)<4: continue
            pts=[q for k,q in enumerate(ring) if k==0 or q!=ring[k-1]]
            if pts[0]==pts[-1]: pts=pts[:-1]
            tris=earcut(pts); base=len(V)//2
            for x,y in pts: V+= [round(x,5),round(y,5)]
            T+=[base+t for t in tris]
    return {"v":V,"t":T}

src=sys.argv[1]; out=sys.argv[2]
land=json.load(open(f"{src}/ne_110m_land.geojson"))
cty=json.load(open(f"{src}/ne_110m_admin_0_countries.geojson"))
want="IN JP CN KR AE TH SG MY ID SA GB FR DE IT ES RU NL CH GR US CA MX BR AR NG ZA EG AU NZ TR".split()
by={}
for f in cty["features"]:
    p=f["properties"]; iso=p.get("ISO_A2_EH") or p.get("ISO_A2")
    if iso in want: by[iso]=f["geometry"]
data={"projection":"miller","latMin":LAT_MIN,"latMax":LAT_MAX,
      "source":"Natural Earth 1:110m land + admin-0 countries (public domain)",
      "land":mesh([f["geometry"] for f in land["features"]]),
      "countries":[dict(id=k.lower(),**mesh([by[k]])) for k in want if k in by]}
json.dump(data,open(out,"w"),separators=(",",":"))
lv=len(data["land"]["v"])//2; lt=len(data["land"]["t"])//3
print("land verts",lv,"tris",lt,"countries",len(data["countries"]),"cverts",sum(len(c["v"])//2 for c in data["countries"]))
# sanity: triangle area sum vs polygon area
