"""Three additional beveled weapons using the existing hand socket and barrel axis."""
from math import sin, cos, tau
from build_rifle import Shell


def pipe(shell, sections, x=0, y=.04):
    piece = Shell()
    piece.cylinder(sections)
    first=len(shell.vertices)
    shell.vertices.extend((px+x,py+y-.04,pz) for px,py,pz in piece.vertices)
    shell.faces.extend([[first+i for i in face] for face in piece.faces])


def grip(shell):
    shell.housing([(-.08,.044,-.26,-.06),(-.035,.057,-.29,-.025),
                   (.034,.052,-.25,-.018),(.083,.035,-.13,-.025)])


pistol,steel=Shell(),Shell()
pistol.housing([(-.23,.047,-.045,.121),(-.19,.066,-.055,.139),
                (.42,.065,-.047,.133),(.49,.052,-.025,.106)])
grip(pistol)
steel.housing([(-.19,.064,.084,.128),(-.15,.068,.082,.145),(.40,.068,.079,.137),(.47,.053,.075,.116)])
pipe(steel,[(.43,.036),(.54,.038),(.56,.031),(.56,.022),(.53,.022)])
for z in (-.16,-.12,-.08):
    steel.housing([(z,.068,.025,.105),(z+.012,.072,.028,.102)])
pistol.housing([(-.21,.025,.131,.164),(-.17,.028,.13,.163)])
pistol.housing([(.37,.015,.133,.159),(.40,.015,.13,.156)])
pistol.save("lux_weapon_pistol");steel.save("lux_weapon_pistol_steel")

shotgun,steel=Shell(),Shell()
shotgun.housing([(-.72,.068,-.12,.12),(-.66,.083,-.14,.13),(-.49,.066,-.059,.131),
                  (-.28,.044,-.015,.126),(-.20,.052,-.02,.127)])
shotgun.housing([(-.22,.080,-.05,.151),(.24,.081,-.06,.154),(.29,.067,-.041,.132)])
grip(shotgun)
shotgun.housing([(.32,.083,-.049,.107),(.40,.096,-.064,.12),(.72,.088,-.057,.114),(.77,.066,-.033,.094)])
pipe(steel,[(.22,.045),(.95,.042),(1.48,.039),(1.52,.046),(1.55,.040),(1.55,.028),(1.51,.028)])
pipe(steel,[(.27,.036),(1.13,.034),(1.17,.029)],y=-.063)
for z in (.41,.47,.53,.59,.65):
    steel.housing([(z,.092,-.058,.113),(z+.016,.096,-.057,.115)])
shotgun.save("lux_weapon_shotgun");steel.save("lux_weapon_shotgun_steel")

minigun,steel=Shell(),Shell()
minigun.housing([(-.48,.107,-.12,.17),(-.41,.150,-.15,.21),(.15,.15,-.14,.21),(.28,.119,-.10,.17)])
grip(minigun)
minigun.housing([(-.27,.044,.19,.28),(-.22,.051,.19,.32),(.16,.051,.19,.32),(.21,.044,.19,.28)])
pipe(steel,[(.19,.17),(.25,.18),(.34,.17),(.36,.145)])
for i in range(6):
    angle=i*tau/6
    x,y=.116*cos(angle),.04+.116*sin(angle)
    pipe(steel,[(.31,.035),(1.12,.035),(1.20,.041),(1.27,.041),(1.29,.033),(1.29,.022),(1.26,.022)],x,y)
pipe(minigun,[(.91,.172),(.97,.178),(1.02,.168)])
pipe(steel,[(1.16,.171),(1.20,.173),(1.23,.158)])
minigun.housing([(-.11,.19,-.145,-.075),(.18,.19,-.145,-.075)])
minigun.save("lux_weapon_minigun");steel.save("lux_weapon_minigun_steel")
