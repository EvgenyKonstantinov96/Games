"""Original contoured field equipment fitted to the exported CC0 clothing, not proxy people."""
import math
from mathutils import Vector
from makehuman_body import smooth


def append_field_gear(actor):
    h,w=actor.h,actor.shoulder
    body=[Vector(p) for p in actor.vertices]
    actor.knee_mounts={}

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
        nu,nv=6,6
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
        """A sewn, filled pocket with a soft front and tapered gussets.

        Share the silhouette rings instead of stacking several beveled solids.
        The bowed front and folded flap are visible from the running camera,
        while the flatter rear sits against the carrier or backpack.
        """
        x,y,z=center
        direction=1 if y>0 else -1
        width,depth,height=[value*.5 for value in size]
        corners=3 if size[0]>.20 else 2
        radius=min(width,height)*.35
        outline=[]
        for sx,sz,angle in ((1,-1,-90),(1,1,0),(-1,1,90),(-1,-1,180)):
            for i in range(corners):
                turn=math.radians(angle+90*i/(corners-1))
                a=sx*(width-radius)+radius*math.cos(turn)
                b=sz*(height-radius)+radius*math.sin(turn)
                # Full lower corners and a narrower top give fabric pockets
                # a tailored silhouette without a spherical/boxy appearance.
                outline.append((a*(1-.09*max(0,b/height)),b))
        first=len(actor.vertices);count=len(outline)
        for offset,scale in ((-depth,.94),(depth*.72,1),(depth,.86)):
            for a,b in outline:
                actor.vertices.append((x+a*scale,y+direction*offset,z+b*scale))
                actor.weights.append({bone:1.})
        front=len(actor.vertices)
        actor.vertices.append((x,y+direction*(depth+.004),z-.008))
        actor.weights.append({bone:1.})
        uv=lambda a,b:(.5+a/(width*2)*.94,.5+b/(height*2)*.94)
        def face(ids,coords,slot):
            actor.faces.append(tuple(ids) if direction<0 else tuple(reversed(ids)))
            actor.tiles.append((slot,tuple(coords) if direction<0 else tuple(reversed(coords))))
        face([first+i for i in reversed(range(count))],
             [uv(*outline[i]) for i in reversed(range(count))],tile)
        for ring in range(2):
            for i,(a,b) in enumerate(outline):
                n=(i+1)%count;c,d=outline[n]
                ids=(first+ring*count+i,first+ring*count+n,
                     first+(ring+1)*count+n,first+(ring+1)*count+i)
                face(ids,((i/count,ring*.5),(n/count,ring*.5),
                          (n/count,(ring+1)*.5),(i/count,(ring+1)*.5)),tile)
        for i,(a,b) in enumerate(outline):
            n=(i+1)%count;c,d=outline[n]
            face((first+2*count+i,first+2*count+n,front),
                 (uv(a,b),uv(c,d),(.5,.46)),tile)
        # The flap is a stitched fold in the same fabric, with a softened point.
        flap=len(actor.vertices)
        points=((-width*.86,height*.77),(width*.86,height*.77),
                (width*.80,height*.34),(width*.25,height*.22),
                (-width*.25,height*.22),(-width*.80,height*.34))
        for a,b in points:
            actor.vertices.append((x+a,y+direction*(depth+.008+.003*(1-abs(a)/width)),z+b))
            actor.weights.append({bone:1.})
        face([flap+i for i in reversed(range(6))],
             [uv(*points[i]) for i in reversed(range(6))],tile)
        actor.panel((x,y+direction*(depth+.013),z+height*.27),
                    (min(.024,size[0]*.23),.006,min(.035,size[2]*.20)),4,bone)

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
        actor.loft([(p,.021 if i in (0,len(points)-1) else .022,.008,{'Chest':1.})
                    for i,p in enumerate(points)],[3]*(len(points)-1),8,0,
                   Vector((1,0,0)))
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
        pouch((x,y,z),(.103,.036,.137),0,'Thigh'+side)
        if actor.name=='Eva' and side=='L':
            pouch((x+sign*.032,y+.042,z+.013),(.086,.042,.090),1,'Thigh'+side)
            actor.panel((x+sign*.032,y+.068,z+.013),(.048,.004,.010),16,'Thigh'+side)
            actor.panel((x+sign*.032,y+.069,z+.013),(.010,.004,.048),16,'Thigh'+side)
        first=len(actor.vertices)
        near=sorted(range(len(body)),key=lambda i:(body[i].x-knee.x)**2+(body[i].z-knee.z)**2)[:16]
        mount=sorted(near,key=lambda i:body[i].y)[-4:]
        y=sum(body[i].y for i in mount)/4+.006
        actor.knee_mounts[side]=(mount,Vector((knee.x,y+.004,knee.z-.006)))
        actor.panel((knee.x,y,knee.z-.006),(.115,.012,.132),3,'Shin'+side)
        # A curved rigid shell over a soft backing, rather than stacked boxes.
        start=len(actor.vertices);nu,nv=6,6
        for j in range(nv+1):
            vz=j/nv*2-1
            for i in range(nu+1):
                u=i/nu*2-1
                x=knee.x+u*.052*math.sqrt(1-.20*vz*vz)
                z=knee.z-.006+vz*.058
                dome=.010*math.sqrt(max(0,1-u*u))+.004*(1-vz*vz)
                actor.vertices.append((x,y+.009+dome,z))
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
            if index<shell_end:
                actor.weights[index]={'KneeGuard'+side:1.}
                continue
            t=smooth(knee.z-.065,knee.z+.070,actor.vertices[index][2])*.65
            actor.weights[index]={'Shin'+side:1-t,'Thigh'+side:t}
