"""Author foot, pelvis and timing targets for the baked FBX motion takes."""

import math
from mathutils import Vector

# Forward reach, rear reach, stance share, swing clearance, right foot phase.
PROFILES = {
    "Max": (.44, .49, .44, .265, .50),
    "Eva": (.42, .43, .43, .245, .50),
    "Walker": (.29, .27, .65, .095, .53),
    "Runner": (.48, .43, .43, .23, .48),
    "Armored": (.29, .28, .64, .095, .52),
    "Brute": (.32, .32, .67, .11, .54),
    "Toxic": (.29, .29, .62, .10, .56),
    "Crawler": (.19, .24, .70, .07, .53),
    "Boss": (.33, .34, .66, .12, .51),
}


def eased_keys(keys, phase):
    for (a, v), (b, w) in zip(keys, keys[1:]):
        if phase <= b:
            t = (phase-a)/(b-a)
            return v+(w-v)*t*t*(3-2*t)
    return keys[-1][1]


def foot_target(name, side, phase, ankle):
    """Roll over the heel and forefoot before an eased airborne recovery."""
    front, back, stance, lift, right_phase = PROFILES[name]
    sign = -1 if side == "L" else 1
    phase = (phase+(right_phase if sign == 1 else 0)) % 1
    human = name in ("Max", "Eva")
    if name in ("Walker", "Toxic") and side == "R":
        front *= .71
        back *= .86
        lift *= .40

    slope = -(front+back)/stance
    if phase <= stance:
        contact = phase/stance
        travel = front+slope*phase
        pitch = eased_keys(((0, 11), (.24, 0), (.68, -5), (1, -31 if human else -22)), contact)
        clearance = 0
    else:
        recover = (phase-stance)/(1-stance)
        t2, t3 = recover**2, recover**3
        tangent = slope*(1-stance)
        travel = (-back*(2*t3-3*t2+1) + front*(-2*t3+3*t2)
                  + tangent*(2*t3-3*t2+recover))
        pitch = eased_keys(((0, -31 if human else -22), (.23, -42 if human else -25),
                            (.57, -3), (.82, 15), (1, 11)), recover)
        clearance = lift*eased_keys(((0, 0), (.17, .41), (.40, 1), (.70, .68), (1, 0)), recover)

    if name == "Crawler":
        travel -= .31
        pitch *= .72

    # The ball of the foot stays grounded at push-off. Use the modeled sole
    # offsets so that a rotating ankle doesn't slide the boot through the floor.
    angle = math.radians(pitch)
    pivot_y = -.073 if pitch >= 0 else .163
    sole = -.085
    rotated_y = math.cos(angle)*pivot_y - math.sin(angle)*sole
    rotated_z = math.sin(angle)*pivot_y + math.cos(angle)*sole
    lateral = sign*(.018 if human else .033)
    foot = Vector((ankle.x+lateral, ankle.y+travel+pivot_y-rotated_y, .006-rotated_z+clearance))
    toe = Vector((0, .16*math.cos(angle)+.06*math.sin(angle),
                  .16*math.sin(angle)-.06*math.cos(angle)))
    bend = eased_keys(((0, .95), (stance*.60, .85), (stance, .10),
                       (stance+(1-stance)*.30, .90), (1, .95)), phase)
    return foot, toe, phase, pitch*bend
