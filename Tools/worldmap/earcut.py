"""Python port of the core of mapbox/earcut for single outer rings (no holes).

Derived from https://github.com/mapbox/earcut — ISC License, Copyright (c) 2016, Mapbox.
Permission to use, copy, modify, and/or distribute this software for any purpose with or without fee is hereby
granted, provided that the above copyright notice and this permission notice appear in all copies.
THE SOFTWARE IS PROVIDED "AS IS" AND ISC DISCLAIMS ALL WARRANTIES WITH REGARD TO THIS SOFTWARE.
"""
import sys
sys.setrecursionlimit(100000)
class N:
    __slots__=("i","x","y","prev","next","steiner")
    def __init__(s,i,x,y): s.i=i; s.x=x; s.y=y; s.prev=s.next=None; s.steiner=False
def ins(i,x,y,last):
    p=N(i,x,y)
    if last is None: p.prev=p.next=p
    else: p.next=last.next; p.prev=last; last.next.prev=p; last.next=p
    return p
def rem(p): p.next.prev=p.prev; p.prev.next=p.next
def eq(a,b): return a.x==b.x and a.y==b.y
def area(p,q,r): return (q.y-p.y)*(r.x-q.x)-(q.x-p.x)*(r.y-q.y)
def sgn(v): return (v>0)-(v<0)
def pit(ax,ay,bx,by,cx,cy,px,py):
    return (cx-px)*(ay-py)>=(ax-px)*(cy-py) and (ax-px)*(by-py)>=(bx-px)*(ay-py) and (bx-px)*(cy-py)>=(cx-px)*(by-py)
def signed_area(pts):
    s=0; j=len(pts)-1
    for i in range(len(pts)): s+=(pts[j][0]-pts[i][0])*(pts[i][1]+pts[j][1]); j=i
    return s
def linked(pts):
    last=None
    if signed_area(pts)>0:
        for i,(x,y) in enumerate(pts): last=ins(i,x,y,last)
    else:
        for i in range(len(pts)-1,-1,-1): last=ins(i,pts[i][0],pts[i][1],last)
    if last and eq(last,last.next): rem(last); last=last.next
    return last
def filt(start,end=None):
    if start is None: return start
    if end is None: end=start
    p=start
    while True:
        again=False
        if not p.steiner and (eq(p,p.next) or area(p.prev,p,p.next)==0):
            rem(p); p=end=p.prev
            if p is p.next: break
            again=True
        else: p=p.next
        if not (again or p is not end): break
    return end
def is_ear(e):
    a,b,c=e.prev,e,e.next
    if area(a,b,c)>=0: return False
    p=c.next
    while p is not a:
        if not (p.x==a.x and p.y==a.y) and pit(a.x,a.y,b.x,b.y,c.x,c.y,p.x,p.y) and area(p.prev,p,p.next)>=0: return False
        p=p.next
    return True
def on_seg(p,q,r): return min(p.x,r.x)<=q.x<=max(p.x,r.x) and min(p.y,r.y)<=q.y<=max(p.y,r.y)
def intersects(p1,q1,p2,q2):
    o1,o2,o3,o4=sgn(area(p1,q1,p2)),sgn(area(p1,q1,q2)),sgn(area(p2,q2,p1)),sgn(area(p2,q2,q1))
    if o1!=o2 and o3!=o4: return True
    if o1==0 and on_seg(p1,p2,q1): return True
    if o2==0 and on_seg(p1,q2,q1): return True
    if o3==0 and on_seg(p2,p1,q2): return True
    if o4==0 and on_seg(p2,q1,q2): return True
    return False
def loc_in(a,b):
    if area(a.prev,a,a.next)<0: return area(a,b,a.next)>=0 and area(a,a.prev,b)>=0
    return area(a,b,a.prev)<0 or area(a,a.next,b)<0
def mid_in(a,b):
    p=a; inside=False; px=(a.x+b.x)/2; py=(a.y+b.y)/2
    while True:
        if ((p.y>py)!=(p.next.y>py)) and p.next.y!=p.y and px<(p.next.x-p.x)*(py-p.y)/(p.next.y-p.y)+p.x: inside=not inside
        p=p.next
        if p is a: break
    return inside
def int_poly(a,b):
    p=a
    while True:
        if p.i!=a.i and p.next.i!=a.i and p.i!=b.i and p.next.i!=b.i and intersects(p,p.next,a,b): return True
        p=p.next
        if p is a: return False
def valid_diag(a,b):
    return a.next.i!=b.i and a.prev.i!=b.i and not int_poly(a,b) and (
        (loc_in(a,b) and loc_in(b,a) and mid_in(a,b) and (area(a.prev,a,b.prev)!=0 or area(a,b.prev,b)!=0)) or
        (eq(a,b) and area(a.prev,a,a.next)>0 and area(b.prev,b,b.next)>0))
def split(a,b):
    a2=N(a.i,a.x,a.y); b2=N(b.i,b.x,b.y); an=a.next; bp=b.prev
    a.next=b; b.prev=a; a2.next=an; an.prev=a2; b2.next=a2; a2.prev=b2; bp.next=b2; b2.prev=bp
    return b2
def cure(start,tris):
    p=start
    while True:
        a=p.prev; b=p.next.next
        if not eq(a,b) and intersects(a,p,p.next,b) and loc_in(a,b) and loc_in(b,a):
            tris+= [a.i,p.i,b.i]; rem(p); rem(p.next); p=start=b
        p=p.next
        if p is start: break
    return filt(p)
def split_ear(start,tris):
    a=start
    while True:
        b=a.next.next
        while b is not a.prev:
            if a.i!=b.i and valid_diag(a,b):
                c=split(a,b); a=filt(a,a.next); c=filt(c,c.next)
                run(a,tris,0); run(c,tris,0); return
            b=b.next
        a=a.next
        if a is start: return
def run(ear,tris,pas):
    if ear is None: return
    stop=ear
    while ear.prev is not ear.next:
        prev,nxt=ear.prev,ear.next
        if is_ear(ear):
            tris+= [prev.i,ear.i,nxt.i]; rem(ear); ear=nxt.next; stop=nxt.next; continue
        ear=nxt
        if ear is stop:
            if pas==0: run(filt(ear),tris,1)
            elif pas==1: run(cure(filt(ear),tris),tris,2)
            else: split_ear(ear,tris)
            break
def earcut(pts):
    tris=[]; n=linked(pts)
    if n is None or n.next is n.prev: return tris
    run(n,tris,0); return tris
