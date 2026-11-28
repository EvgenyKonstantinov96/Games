"""Original contoured field equipment fitted to the exported CC0 clothing, not proxy people."""
import math
from mathutils import Vector
from makehuman_body import smooth


def append_field_gear(actor):
    h,w=actor.h,actor.shoulder
    body=[Vector(p) for p in actor.vertices]

    def surface(x,z,back=False):
        # Use the already fitted body/garment and exclude limbs beyond the
        # target torso patch. Averaging the nearest front/back samples avoids
        # floating vest plates on a generic fixed-depth chest.
        eligible=[p for p in body if (p.y<.025 if back else p.y>.025)]
        nearby=sorted(eligible,key=lambda p:(p.x-x)**2+(p.z-z)**2)[:8]
        values=sorted(p.y for p in nearby)
        return sum(values[:4] if back else values[-4:])/4

    def patch(x0,x1,z0,z1,tile,back=False,offset=.017):
        first=len(actor.vertices)
        nu,nv=8,7
        for j in range(nv+1):
            t=j/nv
            z=z0+(z1-z0)*t
            for i in range(nu+1):
                u=i/nu
                edge=math.sin(math.pi*t)*.008
                taper=1-.26*smooth(.68,1,t)-.07*(1-smooth(0,.18,t))
                x=(x0+x1)*.5+(x1-x0)*(u-.5)*taper
                height=z-.030*(1-abs(u*2-1))**2*smooth(.82,1,t)
                y=surface(x,height,back)+(-1 if back else 1)*(offset+edge*math.sin(math.pi*u))
                actor.vertices.append((x,y,height))
                chest=smooth(h*.65,h*.77,height)
                actor.weights.append({'Spine':1-chest,'Chest':chest})
        for j in range(nv):
            for i in range(nu):
                a=first+j*(nu+1)+i
                face=(a,a+1,a+nu+2,a+nu+1)
                uv=((i/nu,j/nv),((i+1)/nu,j/nv),((i+1)/nu,(j+1)/nv),(i/nu,(j+1)/nv))
                actor.faces.append(face if back else face[::-1])
                actor.tiles.append((tile,uv if back else uv[::-1]))

    def pouch(center,size,tile,bone):
        actor.panel(center,size,tile,bone)
        x,y,z=center
        direction=1 if y>0 else -1
        # Sewn flap, fastener and raised piping read at gameplay distance.
        actor.panel((x,y+direction*(size[1]*.5+.005),z+size[2]*.29),
                    (size[0]*.86,.012,size[2]*.22),3,bone)
        actor.panel((x,y+direction*(size[1]*.5+.013),z+size[2]*.13),
                    (.024,.009,.038),4,bone)

    if actor.name=='Max':
        patch(-.175,.175,h*.646,h*.827,0)
        patch(-.158,.158,h*.655,h*.821,0,True)
        # Twin MOLLE strips, magazine pockets and an asymmetric utility pouch.
        for row in range(3):
            z=h*(.736+row*.025)
            for side in (-1,1):
                x=side*.083
                actor.panel((x,surface(x,z)+.033,z),(.122,.013,.014),8,'Chest')
        for x in (-.102,-.016,.074):
            z=h*.683
            pouch((x,surface(x,z)+.047,z),(.073,.064,.130),0,'Spine')
        x,z=.145,h*.747
        pouch((x,surface(x,z)+.045,z),(.088,.053,.089),8,'Chest')
    else:
        # Scout's jacket remains visible; no assault plate over its red front.
        for side in (-1,1):
            x,z=side*.115,h*.737
            pouch((x,surface(x,z)+.028,z),(.083,.025,.065),1,'Chest')

    # Sewn sleeve insignia on the garment, fitted from the actual upper arm.
    for side in ('L','R'):
        shoulder=actor.bones['UpperArm'+side][0]
        elbow=actor.bones['Forearm'+side][0]
        p=shoulder.lerp(elbow,.30)
        nearby=sorted(body,key=lambda q:(q.x-p.x)**2+(q.z-p.z)**2)[:12]
        front=max(q.y for q in nearby)+.007
        actor.panel((p.x,front,p.z),(.075,.006,.085),46 if actor.name=='Max' else 47,'UpperArm'+side)

    # Shoulder straps follow a rounded route across the real shoulders.
    for side in (-1,1):
        x=side*w*.245
        front=surface(x,h*.80)+.027
        rear=surface(x,h*.80,True)-.025
        points=[Vector((x,front,h*.705)),Vector((x,front,h*.790)),
                Vector((x,front*.42,h*.844)),Vector((x,rear*.42,h*.844)),
                Vector((x,rear,h*.792)),Vector((x,rear,h*.707))]
        for a,b in zip(points,points[1:]):
            actor.surface(a,b,[(0,.021,.009),(.5,.022,.009),(1,.021,.009)],3,'Chest',8)
        actor.panel((x,front+.012,h*.790),(.043,.016,.052),4,'Chest')

    # Backpack side gussets, lid, zip and three real webbing straps.
    pack_start=len(actor.vertices)
    z=h*.735
    y=surface(0,z,True)-.085
    width=.295 if actor.name=='Max' else .255
    height=.338 if actor.name=='Max' else .303
    pouch((0,y,z),(width,.155,height),0,'Chest')
    for side in (-1,1):
        pouch((side*width*.55,y+.012,z-.039),(.071,.105,.167),0,'Chest')
        actor.panel((side*width*.30,y-.084,z),(.021,.014,height*.87),3,'Chest')
        actor.panel((side*width*.30,y-.096,z-.095),(.030,.008,.035),4,'Chest')
    for row in range(3):
        actor.panel((0,y-.086,z-.055+row*.047),(width*.70,.009,.015),8,'Chest')
    actor.panel((0,y-.090,z+height*.31),(width*.62,.008,.013),3,'Chest')
    if actor.name=='Max':
        actor.surface((-.147,y,z+.10),(-.157,y-.008,z+.282),
                      [(0,.009,.009),(.85,.006,.006),(1,.003,.003)],4,'Chest',8)
    for index in range(pack_start,len(actor.vertices)):
        actor.weights[index]={'Pack':1.}

    # Belt, cargo pockets, leg straps and kneepads are weighted to their actual
    # leg chains; the knee pad blends across the joint rather than floating.
    rings=[]
    for z in (h*.555,h*.568,h*.581):
        rings.append((Vector((0,.025,z)),.171,.116,{'Pelvis':1.}))
    actor.loft(rings,[3,3],24,0)
    actor.panel((0,.152,h*.568),(.052,.023,.036),4,'Pelvis')
    for side,sign in (('L',-1),('R',1)):
        hip=actor.bones['Thigh'+side][0]
        knee=actor.bones['Shin'+side][0]
        x=hip.x+sign*.060
        z=h*.476
        y=surface(x,z)+.030
        pouch((x,y,z),(.113,.050,.148),0,'Thigh'+side)
        if actor.name=='Eva' and side=='L':
            pouch((x+sign*.032,y+.042,z+.013),(.086,.042,.090),1,'Thigh'+side)
            actor.panel((x+sign*.032,y+.068,z+.013),(.048,.004,.010),16,'Thigh'+side)
            actor.panel((x+sign*.032,y+.069,z+.013),(.010,.004,.048),16,'Thigh'+side)
        first=len(actor.vertices)
        near=sorted(body,key=lambda p:(p.x-knee.x)**2+(p.z-knee.z)**2)[:16]
        y=sum(sorted(p.y for p in near)[-4:])/4+.006
        actor.panel((knee.x,y,knee.z-.006),(.129,.015,.149),3,'Shin'+side)
        # A curved rigid shell over a soft backing, rather than stacked boxes.
        start=len(actor.vertices);nu,nv=6,6
        for j in range(nv+1):
            vz=j/nv*2-1
            for i in range(nu+1):
                u=i/nu*2-1
                x=knee.x+u*.059*math.sqrt(1-.20*vz*vz)
                z=knee.z-.006+vz*.065
                dome=.023*math.sqrt(max(0,1-u*u))+.009*(1-vz*vz)
                actor.vertices.append((x,y+.011+dome,z))
                actor.weights.append({'KneeGuard'+side:1.})
        for j in range(nv):
            for i in range(nu):
                a=start+j*(nu+1)+i
                actor.faces.append((a+nu+1,a+nu+2,a+1,a))
                tile=8 if 1<=i<nu-1 and 1<=j<nv-1 else 4
                actor.tiles.append((tile,((i/nu,(j+1)/nv),((i+1)/nu,(j+1)/nv),((i+1)/nu,j/nv),(i/nu,j/nv))))
        # Close the shell. A front-only curved surface reads as a floating fin
        # in the side view, even when its pivot follows the knee correctly.
        border=[start+i for i in range(nu+1)]
        border += [start+j*(nu+1)+nu for j in range(1,nv+1)]
        border += [start+nv*(nu+1)+i for i in range(nu-1,-1,-1)]
        border += [start+j*(nu+1) for j in range(nv-1,0,-1)]
        back=[]
        for front in border:
            x,_,z=actor.vertices[front]
            back.append(len(actor.vertices));actor.vertices.append((x,y+.004,z))
            actor.weights.append({'KneeGuard'+side:1.})
        for index,(a,b) in enumerate(zip(border,border[1:]+border[:1])):
            actor.faces.append((a,b,back[(index+1)%len(back)],back[index]))
            actor.tiles.append((4,((0,0),(1,0),(1,1),(0,1))))
        actor.faces.append(tuple(back))
        actor.tiles.append((3,tuple((.5+(actor.vertices[i][0]-knee.x)/.13,
                                   .5+(actor.vertices[i][2]-knee.z+.006)/.14) for i in back)))
        shell_end=len(actor.vertices)
        for z in (knee.z-.050,knee.z+.040):
            rings=[(Vector((knee.x,knee.y,z+dz)),.078,.077,{'Shin'+side:1.}) for dz in (-.007,.007)]
            actor.loft(rings,[3],12,0)
        for index in range(first,len(actor.vertices)):
            if start<=index<shell_end:continue
            t=smooth(knee.z-.065,knee.z+.070,actor.vertices[index][2])*.65
            actor.weights[index]={'Shin'+side:1-t,'Thigh'+side:t}
