"""Build the two shell meshes of the fictional salvage carbine in the existing rig's local coordinates."""
from math import cos, sin, tau
from pathlib import Path

DEST = Path(__file__).resolve().parents[2] / "Assets/Resources/Meshes"


class Shell:
    def __init__(self):
        self.vertices, self.faces = [], []

    def join_rings(self, rings):
        base, count = len(self.vertices), len(rings[0])
        for ring in rings:
            self.vertices.extend(ring)
        self.faces.append([base + i for i in reversed(range(count))])
        for n in range(len(rings) - 1):
            left = base + count * n
            for i in range(count):
                j = (i + 1) % count
                self.faces.append([left + i, left + j, left + count + j, left + count + i])
        self.faces.append([base + (len(rings) - 1) * count + i for i in range(count)])

    def housing(self, sections):
        rings = []
        for z, width, floor, roof in sections:
            bevel = min(width * .36, (roof - floor) * .25)
            outline = [(-width + bevel, floor), (width - bevel, floor), (width, floor + bevel),
                       (width, roof - bevel), (width - bevel, roof), (-width + bevel, roof),
                       (-width, roof - bevel), (-width, floor + bevel)]
            rings.append([(x, y, z) for x, y in outline])
        self.join_rings(rings)

    def cylinder(self, sections):
        self.join_rings([[(r * cos(i * tau / 16), .04 + r * sin(i * tau / 16), z)
                          for i in range(16)] for z, r in sections])

    def save(self, name):
        with (DEST / (name + ".obj")).open("w") as stream:
            stream.write(f"# Zombie Survival Run game art\no {name}\n")
            for v in self.vertices:
                stream.write("v %.6f %.6f %.6f\n" % v)
            for face in self.faces:
                stream.write("f " + " ".join(str(i + 1) for i in face) + "\n")


body, trim = Shell(), Shell()
body.housing([(-.705,.056,-.119,.116),(-.673,.069,-.125,.126),(-.593,.067,-.081,.132),
              (-.433,.051,-.008,.140),(-.362,.049,.046,.142),(-.220,.064,.018,.136)])
body.housing([(-.260,.064,-.051,.129),(-.208,.081,-.087,.169),(.092,.087,-.089,.173),
              (.225,.086,-.077,.166),(.307,.078,-.040,.146),(.328,.074,-.030,.134)])
body.housing([(.238,.075,-.040,.127),(.288,.085,-.045,.145),(.686,.073,-.010,.127),(.731,.052,.005,.097)])
# Two overlapping bands give the grip a swept profile while keeping the firing palm on its socket.
body.housing([(-.074,.047,-.299,-.208),(-.036,.055,-.301,-.070),(.020,.058,-.259,-.052),
              (.082,.053,-.155,-.046),(.105,.046,-.103,-.052)])
body.housing([(.153,.047,-.157,-.060),(.181,.057,-.272,-.056),(.223,.063,-.314,-.069),
              (.291,.059,-.301,-.072),(.318,.047,-.285,-.183)])
body.housing([(-.220,.035,.158,.195),(-.168,.041,.163,.230),(-.101,.039,.166,.225),(-.062,.030,.166,.184)])
body.housing([(.665,.025,.109,.151),(.698,.031,.112,.181),(.722,.025,.107,.169)])

trim.cylinder([(.681,.052),(.782,.056),(.811,.043),(1.078,.037),(1.097,.055),
               (1.130,.060),(1.150,.050),(1.240,.050),(1.270,.043),(1.270,.030),(1.242,.030)])
# A recessed, dark muzzle face is visible only on close inspection of the imported art.
body.cylinder([(1.2425,.0295),(1.244,.0295)])
trim.housing([(-.671,.064,.025,.109),(-.622,.072,.015,.114),(-.546,.066,.052,.118)])
trim.housing([(-.201,.040,.160,.192),(-.172,.048,.166,.206),(.208,.043,.154,.199),(.235,.036,.154,.180)])
trim.housing([(.126,.062,-.099,-.056),(.165,.066,-.126,-.064),(.208,.071,-.121,-.068),(.231,.065,-.092,-.063)])
for z in (.328,.409,.490,.571):
    trim.housing([(z,.074,-.035,.122),(z + .012,.085,-.033,.133),
                  (z + .031,.083,-.031,.131),(z + .039,.072,-.028,.123)])
body.save("lux_weapon_rifle")
trim.save("lux_weapon_rifle_steel")
