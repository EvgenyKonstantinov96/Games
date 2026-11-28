"""Original reference-matched field kit: red case, olive frag and white quadcopter."""
from math import sin,cos,tau
from uuid import NAMESPACE_URL,uuid5
import numpy as np
from PIL import Image,ImageDraw
from build_track_props import Prop as TrackProp,MESHES,ART

RED,WHITE,RUBBER,STEEL,OLIVE,DARK,LENS,AMBER,SHELL,FABRIC=range(10)


class Prop(TrackProp):
    def octagon(self,rings,tile,front=None):
        # A faceplate may be only a few millimetres thick. Its bevel cannot
        # exceed half that thickness or adjacent corners collapse/cross.
        fitted=[(y,sx,sz,min(bevel,sx*.45,sz*.45),cx,cz) for y,sx,sz,bevel,cx,cz in rings]
        super().octagon(fitted,tile,front)


def save(prop,name):
    prop.save(name)
    dest=MESHES/(name+'.obj.meta')
    if not dest.exists():
        template=(MESHES/'track_gate_pylon.obj.meta').read_text().splitlines()
        template[1]='guid: '+uuid5(NAMESPACE_URL,'ZSR/combat/'+name).hex
        dest.write_text('\n'.join(line.rstrip() for line in template)+'\n')


def lathe(prop,profile,tile,cx=0,cz=0,sides=24):
    points=[(cx+r*cos(tau*i/sides),y,cz+r*sin(tau*i/sides)) for y,r in profile for i in range(sides)]
    faces=[list(reversed(range(sides)))]
    for j in range(len(profile)-1):
        for i in range(sides):
            a=j*sides+i;b=j*sides+(i+1)%sides
            faces.append([a,b,b+sides,a+sides])
    faces.append(list(range((len(profile)-1)*sides,len(profile)*sides)))
    prop.add(points,[face[::-1] for face in faces],tile)


def torus(prop,center,radius,thickness,tile):
    points=[]
    for i in range(20):
        angle=tau*i/20
        for j in range(6):
            turn=tau*j/6;r=radius+thickness*cos(turn)
            points.append((center[0]+r*cos(angle),center[1]+r*sin(angle),center[2]+thickness*sin(turn)))
    faces=[]
    for i in range(20):
        for j in range(6):
            a=i*6+j;b=i*6+(j+1)%6;c=((i+1)%20)*6+(j+1)%6;d=((i+1)%20)*6+j
            faces.append([a,d,c,b])
    prop.add(points,faces,tile)


def atlas():
    colors=[(174,38,28),(220,224,213),(24,30,34),(132,145,150),(72,86,44),
            (40,49,53),(17,43,58),(242,152,36),(185,196,195),(85,93,73)]
    image=Image.new('RGB',(512,512),(30,38,43))
    normal=Image.new('RGB',image.size,(128,128,255))
    surface=Image.new('RGBA',image.size,(0,0,0,80))
    rng=np.random.default_rng(281127)
    yy,xx=np.mgrid[:128,:128]
    for slot,color in enumerate(colors):
        grain=rng.normal(0,1.4,(128,128))
        wear=np.maximum(0,np.sin(xx*.16+yy*.08)*np.cos(yy*.17-xx*.05)-.50)
        values=np.array(color)+grain[:,:,None]+wear[:,:,None]*8
        image.paste(Image.fromarray(np.uint8(np.clip(values,0,255))),(slot%4*128,slot//4*128))
        metal=180 if slot==STEEL else 35 if slot in (OLIVE,SHELL) else 0
        gloss=180 if slot==LENS else 135 if slot==STEEL else 78 if slot==RUBBER else 110
        ImageDraw.Draw(surface).rectangle((slot%4*128,slot//4*128,slot%4*128+127,slot//4*128+127),fill=(metal,0,0,gloss))
    for suffix,im in (('',image),('_normal',normal),('_surface',surface)):
        dest=ART/('CombatAtlas'+suffix+'.png');im.save(dest,optimize=True)
        meta=dest.with_suffix('.png.meta')
        if not meta.exists():
            template=(ART/('CharacterAtlas_surface.png.meta' if suffix=='_surface' else 'CharacterAtlas_normal.png.meta' if suffix=='_normal' else 'CharacterAtlas.png.meta')).read_text()
            import re
            template=re.sub(r'guid: [0-9a-f]{32}','guid: '+uuid5(NAMESPACE_URL,'ZSR/CombatAtlas/'+suffix).hex,template)
            meta.write_text(template.replace('maxTextureSize: 2048','maxTextureSize: 512'))


def build():
    drone=Prop()
    drone.octagon([(-.060,.139,.218,.055,0,0),(-.035,.184,.258,.065,0,0),
                   (.040,.179,.250,.064,0,0),(.075,.133,.201,.052,0,-.008)],SHELL)
    drone.octagon([(.070,.090,.160,.036,0,-.020),(.083,.075,.148,.031,0,-.027)],WHITE)
    for x in (-1,1):
        for z in (-1,1):
            drone.rod((.13*x,-.008,.13*z),(.45*x,.035,.38*z),.031,SHELL)
            lathe(drone,[(.007,.049),(.026,.061),(.077,.057),(.098,.044)],RUBBER,.45*x,.38*z,16)
            lathe(drone,[(.093,.047),(.108,.047)],STEEL,.45*x,.38*z,16)
        drone.rod((x*.126,-.041,-.14),(x*.20,-.169,-.22),.016,STEEL)
        drone.rod((x*.126,-.041,.14),(x*.20,-.169,.22),.016,STEEL)
        drone.rod((x*.20,-.169,-.25),(x*.20,-.169,.25),.018,RUBBER)
    for i in range(4):
        z=-.13+i*.032
        drone.octagon([(.074,.055,.006,.003,0,z),(.079,.055,.006,.003,0,z)],DARK)
    drone.octagon([(-.095,.061,.052,.020,0,.242),(-.025,.066,.058,.022,0,.242)],DARK,LENS)
    save(drone,'combat_drone')
    rotor=Prop()
    for sign in (-1,1):
        outline=[(.023,-.010),(.12,-.025),(.245,-.016),(.255,.003),(.14,.025),(.03,.012)]
        points=[(sign*x,y,sign*z) for y in (-.006,.006) for x,z in outline]
        faces=[list(range(6)),list(reversed(range(6,12)))]
        faces += [[i,6+i,6+(i+1)%6,(i+1)%6] for i in range(6)]
        rotor.add(points,faces,RUBBER)
    lathe(rotor,[(-.010,.031),(.016,.031)],STEEL,sides=16)
    save(rotor,'combat_rotor')
    grenade=Prop()
    profile=[(-.095,.026),(-.086,.050),(-.066,.067),(-.044,.078),(-.020,.083),
             (.006,.082),(.031,.074),(.052,.061),(.069,.040),(.075,.029)]
    lathe(grenade,profile,OLIVE)
    for y,r in ((-.066,.067),(-.036,.080),(-.006,.083),(.024,.077)):
        lathe(grenade,[(y-.002,r+.002),(y+.002,r+.002)],DARK)
    lathe(grenade,[(.073,.027),(.112,.027),(.120,.022)],STEEL,sides=16)
    grenade.octagon([(.024,.009,.021,.005,.072,0),(.116,.009,.021,.005,.050,0),(.128,.036,.021,.006,.022,0)],DARK)
    torus(grenade,(-.040,.110,-.010),.025,.004,STEEL)
    save(grenade,'combat_grenade')
    case=Prop()
    case.octagon([(-.105,.142,.048,.018,0,0),(-.089,.168,.066,.025,0,0),
                  (.087,.168,.066,.025,0,0),(.105,.143,.049,.018,0,0)],RED)
    case.octagon([(-.077,.144,.005,.017,0,-.066),(.077,.144,.005,.017,0,-.066)],RED)
    for x in (-.12,.12):
        case.octagon([(.061,.017,.012,.004,x,-.067),(.097,.017,.012,.004,x,-.067)],RUBBER)
        case.octagon([(.068,.012,.003,.003,x,-.081),(.084,.012,.003,.003,x,-.081)],STEEL)
    for x in (-.060,.060):
        case.octagon([(.100,.012,.022,.006,x,0),(.149,.012,.022,.006,x,0)],RUBBER)
    case.octagon([(.138,.061,.022,.007,0,0),(.157,.061,.022,.007,0,0)],RUBBER)
    case.octagon([(-.017,.066,.004,.005,0,-.075),(.017,.066,.004,.005,0,-.075)],WHITE)
    case.octagon([(-.061,.017,.004,.005,0,-.076),(.061,.017,.004,.005,0,-.076)],WHITE)
    save(case,'combat_medkit')
    atlas()


if __name__=='__main__':build()
