"""Author the compact combat meshes against the existing weathered prop atlas."""

from uuid import NAMESPACE_URL, uuid5
from build_track_props import BODY, GLASS, MESHES, PYLON, RIM, RUBBER, SPIKE, TAILLIGHT, Prop


def blade(prop, x, z):
    outline = [(.03,.016),(.12,.033),(.24,.015),(.255,-.009),(.13,-.027),(.034,-.013)]
    points = [(x + u, y, z + v) for y in (-.009,.009) for u,v in outline]
    faces = [list(reversed(range(6))),list(range(6,12))]
    faces.extend([[i,(i+1)%6,6+(i+1)%6,6+i] for i in range(6)])
    prop.add(points,faces,SPIKE)


def save(prop, name):
    prop.save(name)
    dest = MESHES/(name+".obj.meta")
    template = (MESHES/"track_gate_pylon.obj.meta").read_text().splitlines()
    template[1]="guid: "+uuid5(NAMESPACE_URL,"ZSR/combat/"+name).hex
    dest.write_text("\n".join(line.rstrip() for line in template)+"\n")


drone = Prop()
drone.octagon([(-.123,.19,.29,.095,0,-.014),(-.075,.248,.338,.10,0,0),
               (.03,.25,.347,.096,0,0),(.104,.184,.293,.094,0,-.004),(.127,.138,.218,.072,0,-.001)], PYLON)
drone.octagon([(.113,.126,.172,.067,0,-.031),(.148,.115,.153,.064,0,-.039),
               (.151,.094,.125,.056,0,-.037)], BODY)
drone.octagon([(-.137,.115,.075,.038,0,.308),(-.058,.137,.093,.040,0,.324),
               (.028,.105,.066,.038,0,.310)], RUBBER, GLASS)
for x in (-1,1):
    for z in (-1,1):
        drone.rod((.16*x,-.007,.17*z),(.45*x,.05,.38*z),.059,PYLON)
        drone.octagon([(.008,.103,.104,.044,.45*x,.38*z),(.051,.118,.117,.052,.45*x,.38*z),
                       (.099,.099,.098,.049,.45*x,.38*z)], RUBBER, RIM)
save(drone,"combat_drone")

rotor=Prop()
blade(rotor,0,0)
part=Prop()
blade(part,0,0)
rotor.points.extend([(-x,y,-z) for x,y,z in part.points])
n=len(part.points)
rotor.faces.extend([([i+n for i in face],uv) for face,uv in part.faces])
rotor.octagon([(-.014,.038,.038,.012,0,0),(.023,.036,.036,.011,0,0)], RIM)
save(rotor,"combat_rotor")

grenade=Prop()
grenade.octagon([(-.095,.05,.049,.014,0,0),(-.07,.079,.075,.026,0,0),
                (.012,.087,.081,.026,0,0),(.071,.065,.06,.025,0,0),
                (.085,.045,.043,.012,0,0)], BODY)
grenade.octagon([(.071,.028,.026,.005,0,0),(.111,.028,.026,.007,0,0)], RUBBER)
grenade.octagon([(.096,.014,.065,.007,.045,-.013),(.114,.014,.065,.007,.045,-.013)], RIM)
save(grenade,"combat_grenade")

case=Prop()
case.octagon([(-.046,.122,.079,.024,0,0),(-.034,.141,.094,.026,0,0),
              (.034,.141,.094,.026,0,0),(.048,.122,.079,.024,0,0)], TAILLIGHT)
case.octagon([(.047,.066,.024,.009,0,0),(.054,.066,.024,.009,0,0)], RIM)
case.octagon([(.047,.023,.062,.009,0,0),(.054,.023,.062,.009,0,0)], RIM)
case.octagon([(.052,.050,.017,.006,0,-.098),(.078,.050,.017,.006,0,-.098)], RUBBER)
save(case,"combat_medkit")
