"""Joined roadside meshes with real bevels and the existing track material."""
from uuid import uuid5,NAMESPACE_URL
from build_track_props import Prop,MESHES,PYLON,RIM,REBAR,BED,CHEVRON,CONCRETE


def save(mesh,name):
    mesh.save(name)
    meta=MESHES/(name+".obj.meta")
    if not meta.exists():
        text=(MESHES/"track_gate_pylon.obj.meta").read_text().splitlines()
        text[1]="guid: "+uuid5(NAMESPACE_URL,"ZSR/street/"+name).hex
        meta.write_text("\n".join(line.rstrip() for line in text)+"\n")


cases=Prop()
for x,z,raise_y,span in ((-.36,0,0,.41),(.45,.19,0,.34),(-.29,.04,.71,.32)):
    cases.octagon([(raise_y+.02,span,.34,.045,x,z),(raise_y+.08,span+.015,.35,.05,x,z),
                   (raise_y+.63,span,.34,.05,x,z),(raise_y+.71,span-.018,.32,.045,x,z)],PYLON)
    cases.octagon([(raise_y+.60,span+.026,.353,.04,x,z),
                   (raise_y+.66,span+.026,.353,.04,x,z)],RIM)
    for side in (-1,1):
        cases.octagon([(raise_y+.49,.043,.020,.009,x+side*span*.62,z-.347),
                       (raise_y+.62,.043,.020,.009,x+side*span*.62,z-.347)],REBAR)
for i in range(3):
    x=-.84+i*.63
    cases.octagon([(.01,.28,.24,.055,x,-.83-i*.12),
                   (.11+i*.03,.23,.19,.04,x+.07,-.79-i*.12)],CONCRETE)
save(cases,"track_maintenance")

lamp=Prop()
lamp.octagon([(0,.15,.15,.026,0,0),(.28,.15,.15,.026,0,0),
              (.33,.048,.048,.014,0,0),(5.99,.048,.048,.014,0,0)],REBAR)
lamp.octagon([(5.99,.58,.052,.02,-.53,0),(6.08,.58,.052,.02,-.53,0)],RIM)
lamp.octagon([(5.93,.21,.155,.028,-1.09,0),(6.04,.24,.17,.033,-1.09,0),
              (6.12,.18,.14,.022,-1.09,0)],PYLON)
save(lamp,"track_streetlamp")
