"""Authored infected headwear, wounds and asymmetrical skin details on the actual rigs."""
import math
from mathutils import Vector


def fitted_bib(actor,width,bottom,top,tile):
    """A garment/vest panel sampled from the actual chest, with a rolled closed edge."""
    first=len(actor.vertices);nu,nv=10,8
    for layer in range(2):
        for j in range(nv+1):
            t=j/nv;z=actor.h*(bottom+(top-bottom)*t)
            for i in range(nu+1):
                u=i/nu*2-1;x=u*width*(1-.15*t)
                y=actor.body.chest_surface(x,z)+(.024 if layer==0 else .007)
                y+=.010*math.sin(math.pi*t)*(1-u*u)
                actor.vertices.append((x,y,z))
                weight=max(0,min(1,(z/actor.h-.65)/.13))
                actor.weights.append({'Spine':1-weight,'Chest':weight})
    count=(nu+1)*(nv+1)
    for layer in range(2):
        for j in range(nv):
            for i in range(nu):
                a=first+layer*count+j*(nu+1)+i
                face=(a,a+1,a+nu+2,a+nu+1)
                actor.faces.append(face[::-1] if layer==0 else face)
                coords=((i/nu,j/nv),((i+1)/nu,j/nv),((i+1)/nu,(j+1)/nv),(i/nu,(j+1)/nv))
                actor.tiles.append((tile,coords[::-1] if layer==0 else coords))
    border=list(range(nu+1))+[j*(nu+1)+nu for j in range(1,nv+1)]
    border += [nv*(nu+1)+i for i in range(nu-1,-1,-1)]+[j*(nu+1) for j in range(nv-1,0,-1)]
    for a,b in zip(border,border[1:]+border[:1]):
        actor.faces.append((first+a,first+b,first+b+count,first+a+count))
        actor.tiles.append((3,((0,0),(1,0),(1,1),(0,1))))

def tank_harness(actor):
    h=actor.h
    fitted_bib(actor,.20,.55,.70,20)
    # One surviving overall strap, its diagonal broken counterpart and seams.
    for sign,upper in ((-1,.83),(1,.74)):
        x=sign*.15
        points=[Vector((x,actor.body.chest_surface(x,h*z)+.027,h*z)) for z in (.57,.66,.74,upper)]
        for a,b in zip(points,points[1:]):
            if (a-b).length<.001:continue
            actor.surface(a,b,[(0,.024,.009),(.5,.025,.010),(1,.023,.008)],20,'Chest',8)
        p=points[2]
        actor.panel(p+Vector((0,.014,0)),(.054,.012,.063),4,'Chest')

def append_infected_details(actor,pack):
    h,w=actor.h,actor.shoulder
    if actor.name in ('Walker','Crawler'):
        from survivor_clothing import append_hair
        append_hair(actor,pack)
    if actor.name=='Runner':
        first=len(actor.vertices)
        # Hood opening stays ahead of the temples and leaves the whole face free.
        rings=((h*.835,.128,.119,.016),(h*.889,.146,.141,-.010),
               (h*.951,.144,.145,-.019),(h*.998,.108,.119,-.021),
               (h*1.017,.034,.050,-.018))
        columns=18
        for j,(z,rx,ry,cy) in enumerate(rings):
            for i in range(columns+1):
                t=i/columns
                a=math.pi*(.77+1.46*t)
                p=(rx*math.cos(a),cy+ry*math.sin(a),z)
                actor.vertices.append(p)
                actor.weights.append({'Chest':.52,'Head':.48} if j==0 else {'Head':1.})
        for j in range(len(rings)-1):
            for i in range(columns):
                a=first+j*(columns+1)+i
                actor.faces.append((a,a+1,a+columns+2,a+columns+1))
                actor.tiles.append((1,((i/columns,j/4),((i+1)/columns,j/4),
                                       ((i+1)/columns,(j+1)/4),(i/columns,(j+1)/4))))
        for edge in (0,columns):
            for j in range(len(rings)-1):
                a=Vector(actor.vertices[first+j*(columns+1)+edge])
                b=Vector(actor.vertices[first+(j+1)*(columns+1)+edge])
                actor.surface(a,b,[(0,.006,.006),(.5,.007,.007),(1,.006,.006)],8,'Head',6)
    if actor.name=='Toxic':
        # A ruptured shoulder and drooling jaw explain the ranged attack.
        mouth=actor.body.joint('mouth')
        for i in range(3):
            p=mouth+Vector((-.017+i*.014,.019,-.012))
            actor.surface(p,p+Vector((.008,.008,-.059-.012*i)),
                          [(0,.004,.004),(.62,.005,.004),(1,.001,.001)],21,'Head',7)
    # Closed, fitted tooth geometry follows the deformed jaw and head.
    from survivor_clothing import append_fitted_head_part
    append_fitted_head_part(actor,pack,'teeth','teeth_base',16,.25)


def append_fitted_helmet(actor):
    from makehuman_body import append_reduced
    body=actor.body
    eye=body.joint('l-eye')
    cutoff=eye.z+.020
    scalp=[i for i,p in body.geometry.items() if p.z>cutoff]
    center=sum((body.geometry[i] for i in scalp),Vector((0,0,0)))/len(scalp)
    points={i:body.geometry[i]+(body.geometry[i]-center)*.085+Vector((0,0,.012)) for i in scalp}
    faces=[]
    used=set()
    for face in body.faces:
        ids=[i for i,_ in face]
        if all(i in points for i in ids):
            faces.append(ids);used.update(ids)
    ordered=sorted(used);remap={index:i for i,index in enumerate(ordered)}
    geometry=[(tuple(remap[i] for i in face),tuple((.5+points[i].x*3,.5+(points[i].z-cutoff)*4) for i in face)) for face in faces]
    append_reduced(actor,'Fitted security helmet',[points[i] for i in ordered],geometry,
                   [4]*len(geometry),[{'Head':1.} for _ in ordered],.65)
    # Forehead rim positioned from the eye landmark instead of guessed height.
    front=eye.y+.025
    actor.panel((0,front,cutoff+.001),(.188,.025,.025),3,'Head')
    # Weathered security insignia distinguishes the sole armored class.
    actor.panel((0,.195,actor.h*.770),(.079,.008,.062),48,'Chest')
