"""Bean Crew: four goofy co-op workers sharing one skeleton and one animation set.

Run:  python3 bean_crew.py OUTDIR
Outputs OUTDIR/BeanCrew.fbx (armature + 4 skinned variants + all animation takes)
and OUTDIR/BeanCrew.blend (editable source).
Coordinates: metres, character faces -Y, left side is +X.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from mathutils import Vector, Matrix, Quaternion, Euler
from common import *

OUT = sys.argv[-1] if len(sys.argv) > 1 else '/home/claude/art/out'
os.makedirs(OUT, exist_ok=True)
reset()

# --------------------------------------------------------------------------- skeleton
BONES = {
    # name: (head, tail, parent)
    'Root': ((0, 0, 0), (0, 0, .15), None),
    'Hips': ((0, 0, .52), (0, 0, .72), 'Root'),
    'Spine': ((0, 0, .72), (0, 0, .95), 'Hips'),
    'Chest': ((0, 0, .95), (0, 0, 1.15), 'Spine'),
    'Head': ((0, 0, 1.15), (0, 0, 1.62), 'Chest'),
    'Eyes': ((0, -.12, 1.45), (0, -.32, 1.45), 'Head'),
}
for side, s in (('L', 1), ('R', -1)):
    BONES['UpperArm' + side] = ((s * .27, 0, 1.05), (s * .33, 0, .81), 'Chest')
    BONES['Forearm' + side] = ((s * .33, 0, .81), (s * .36, -.01, .62), 'UpperArm' + side)
    BONES['Hand' + side] = ((s * .36, -.01, .62), (s * .37, -.02, .50), 'Forearm' + side)
    BONES['Thigh' + side] = ((s * .13, 0, .50), (s * .13, 0, .29), 'Hips')
    BONES['Shin' + side] = ((s * .13, 0, .29), (s * .13, 0, .10), 'Thigh' + side)
    BONES['Foot' + side] = ((s * .13, 0, .10), (s * .13, -.15, .04), 'Shin' + side)

arm_data = bpy.data.armatures.new('BeanRig')
rig = link(bpy.data.objects.new('BeanRig', arm_data))
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='EDIT')
for name, (h, t, p) in BONES.items():
    b = arm_data.edit_bones.new(name)
    b.head, b.tail = h, t
    # vertical/limb bones keep their X axis on the character's side axis
    b.align_roll(Vector((0, -1, 0)) if abs(Vector(t).z - Vector(h).z) > .05 else Vector((0, 0, 1)))
    if p: b.parent = arm_data.edit_bones[p]
    b.use_connect = False
bpy.ops.object.mode_set(mode='OBJECT')

def seg_dist(p, a, b):
    ab = b - a
    t = max(0, min(1, (p - a).dot(ab) / max(ab.length_squared, 1e-9)))
    return (p - (a + ab * t)).length

def skin(o, bones, power=5):
    """Smooth distance-based weights over a few candidate bones (rigid when one bone)."""
    groups = {b: o.vertex_groups.new(name=b) for b in bones}
    for v in o.data.vertices:
        p = o.matrix_world @ v.co
        if len(bones) == 1:
            groups[bones[0]].add([v.index], 1, 'REPLACE'); continue
        ws = []
        for b in bones:
            h, t, _ = BONES[b]
            d = seg_dist(p, Vector(h), Vector(t))
            ws.append(1 / (d + .02) ** power)
        tot = sum(ws)
        for b, w in zip(bones, ws):
            if w / tot > .01: groups[b].add([v.index], w / tot, 'REPLACE')
    return o

# --------------------------------------------------------------------------- palette
SKIN = {'peach': (241, 190, 150), 'tan': (214, 160, 112), 'brown': (150, 98, 64), 'pink': (240, 172, 158)}
INK = mat('Bean Ink', (30, 26, 30), .5)
WHITE = mat('Bean Eye White', (250, 248, 240), .35)
BLUSH = mat('Bean Blush', (236, 120, 120), .8)

# --------------------------------------------------------------------------- body kit
def body_rings(belly=1.0, width=1.0):
    prof = [  # z, radius
        (.40, .01), (.42, .14), (.46, .22), (.54, .27), (.66, .30), (.78, .30),
        (.90, .285), (1.00, .265), (1.08, .23), (1.14, .17), (1.18, .08), (1.19, .01)]
    rings = []
    for z, r in prof:
        k = belly if .5 < z < .95 else 1 + (belly - 1) * .4
        rings.append(((0, (-.03 * (belly - 1) * 3 if .5 < z < .95 else 0), z), r * width * k, r * k * .88))
    return rings

def body(tag, shirt, pants, belly=1.0, width=1.0, belt=None, belt_z=.56):
    b = loft(tag + ' body', body_rings(belly, width), shirt, seg=28)
    b.data.materials.append(pants)
    for p in b.data.polygons:
        if p.center.z < belt_z: p.material_index = 1
    parts = [skin(b, ['Hips', 'Spine', 'Chest'])]
    if belt:
        r = .28 * width * belly
        parts.append(skin(torus(tag + ' belt', (0, -.03 * (belly - 1) * 3, belt_z), r, .025, belt, scale=(1, .88, 1)), ['Hips', 'Spine']))
    return parts

def limbs(tag, sleeve, skin_m, pants, shoe, glove=None, sleeve_len=.55, sole=None):
    parts = []
    for side, s in (('L', 1), ('R', -1)):
        sh, el, wr = Vector(BONES['UpperArm' + side][0]), Vector(BONES['Forearm' + side][0]), Vector(BONES['Hand' + side][0])
        # sleeve (upper) + skin (lower) as two tubes blended across elbow
        cut = sh.lerp(el, 1.0).lerp(wr, sleeve_len - .5) if sleeve_len > .5 else sh.lerp(el, sleeve_len * 2)
        parts.append(skin(tube(tag + ' sleeve' + side, [sh + Vector((0, 0, .02)), el, cut] if sleeve_len > .5 else [sh + Vector((0, 0, .02)), cut], [.085, .075, .072][:3 if sleeve_len > .5 else 2], sleeve, seg=14), ['UpperArm' + side, 'Forearm' + side]))
        parts.append(skin(tube(tag + ' arm' + side, [sh, el, wr], [.062, .058, .055], skin_m, seg=12), ['UpperArm' + side, 'Forearm' + side, 'Hand' + side]))
        hand_m = glove or skin_m
        hc = wr + Vector((s * .005, -.01, -.06))
        parts.append(skin(sphere(tag + ' mitt' + side, hc, (.075, .065, .085), hand_m, 16, 10), ['Hand' + side]))
        parts.append(skin(sphere(tag + ' thumb' + side, hc + Vector((-s * .045, -.045, .02)), (.03, .03, .04), hand_m, 10, 6), ['Hand' + side]))
        hip, knee, ank = (Vector(BONES[n + side][0]) for n in ('Thigh', 'Shin', 'Foot'))
        parts.append(skin(tube(tag + ' leg' + side, [hip + Vector((0, 0, .06)), knee, ank], [.1, .085, .075], pants, seg=14), ['Thigh' + side, 'Shin' + side]))
        parts.append(skin(sphere(tag + ' shoe' + side, (s * .13, -.06, .07), (.1, .16, .075), shoe, 20, 10, flat_bottom=-.8), ['Foot' + side]))
        if sole:
            parts.append(skin(sphere(tag + ' sole' + side, (s * .13, -.06, .015), (.105, .165, .02), sole, 20, 6), ['Foot' + side]))
    return parts

HEAD_C = Vector((0, 0, 1.42))

def face_y(size, x, z, lift=0.0):
    """Front surface of the head ellipsoid at (x, z), pushed out by lift."""
    k = 1 - (x / size[0]) ** 2 - ((z - HEAD_C.z) / size[2]) ** 2
    return -size[1] * math.sqrt(max(k, .02)) - lift

def head(tag, skin_m, size=(.30, .28, .29), nose=None, nose_size=(.07, .07, .065), eye_gap=.1, eye_size=(.038, .02, .052), brows=True, brow_m=None, brow_tilt=-6, mouth='smile'):
    parts = [skin(sphere(tag + ' head', HEAD_C, size, skin_m, 28, 16), ['Head'])]
    for s in (1, -1):
        ey = face_y(size, eye_gap, 1.45, -.006)
        e = sphere(tag + ' eye', (s * eye_gap, ey, 1.45), eye_size, INK, 14, 8)
        parts.append(skin(e, ['Eyes']))
        parts.append(skin(sphere(tag + ' glint', (s * eye_gap + .013, ey - eye_size[1] + .004, 1.468), (.012, .006, .015), WHITE, 8, 5), ['Eyes']))
        if brows:
            by = face_y(size, eye_gap, 1.53, -.004)
            b = box(tag + ' brow', (s * eye_gap, by, 1.53), (.08, .022, .02), brow_m or INK, bevel=.008, rot=(0, s * brow_tilt, 0))
            parts.append(skin(b, ['Head']))
    if nose:
        parts.append(skin(sphere(tag + ' nose', (0, face_y(size, 0, 1.39, nose_size[1] * .45), 1.39), nose_size, nose, 16, 10), ['Head']))
    if mouth == 'smile':
        pts = []
        for i in range(7):
            a = math.pi * (1.1 + .8 * i / 6)
            x, z = .055 * math.cos(a), 1.345 + .035 * math.sin(a)
            pts.append((x, face_y(size, x, z, -.004), z))
        parts.append(skin(tube(tag + ' mouth', pts, [.011] * 7, INK, seg=8, steps=2), ['Head']))
    elif mouth == 'o':
        parts.append(skin(sphere(tag + ' mouth', (0, face_y(size, 0, 1.31, -.01), 1.31), (.025, .014, .03), INK, 10, 6), ['Head']))
    return parts

def ears(tag, skin_m, size=(.03, .05, .065)):
    return [skin(sphere(tag + ' ear', (s * .295, 0, 1.41), size, skin_m, 12, 8), ['Head']) for s in (1, -1)]

# --------------------------------------------------------------------------- variants
variants = {}

def v_bolt():
    """Bolt: the eager rookie. Mustard hoodie, red beanie with pompom, big round nose."""
    t = 'Bolt'
    sk = mat('Bean Skin Peach', SKIN['peach'])
    shirt = mat('Bolt Hoodie', (240, 178, 48)); pants = mat('Bolt Jeans', (52, 78, 140))
    hat = mat('Bolt Beanie', (214, 58, 52)); shoe = mat('Bolt Sneakers', (240, 240, 235)); sole = mat('Bean Rubber', (60, 55, 60))
    P = body(t, shirt, pants, belly=1.0)
    P += limbs(t, shirt, sk, pants, shoe, sleeve_len=.8, sole=sole)
    P += head(t, sk, nose=mat('Bolt Nose', (236, 150, 120)), nose_size=(.075, .07, .07))
    P += ears(t, sk)
    # beanie: dome + rolled band + pompom
    P.append(skin(sphere(t + ' beanie', (0, .01, 1.56), (.305, .295, .24), hat, 24, 14, flat_bottom=-.02), ['Head']))
    P.append(skin(torus(t + ' band', (0, .01, 1.565), .298, .038, hat, scale=(1, .97, 1)), ['Head']))
    P.append(skin(sphere(t + ' pompom', (0, .02, 1.79), (.07, .07, .07), mat('Bolt Pompom', (250, 245, 230)), 12, 8), ['Head']))
    # hoodie pocket + strings
    P.append(skin(box(t + ' pocket', (0, -.29, .72), (.26, .03, .12), mat('Bolt Pocket', (220, 155, 36)), bevel=.015), ['Spine']))
    for s in (1, -1):
        P.append(skin(tube(t + ' string', [(s * .05, -.26, 1.08), (s * .055, -.285, .96)], [.009, .009], WHITE, seg=6), ['Chest']))
    return P

def v_gus():
    """Gus: the foreman. Round belly, hard hat, hi-vis vest, enormous moustache."""
    t = 'Gus'
    sk = mat('Bean Skin Pink', SKIN['pink'])
    shirt = mat('Gus Hi-Vis', (250, 120, 30)); pants = mat('Gus Trousers', (88, 70, 58)); stripe = mat('Gus Reflective', (230, 235, 200), .3)
    shoe = mat('Gus Boots', (70, 48, 36)); sole = mat('Bean Rubber', (60, 55, 60))
    P = body(t, shirt, pants, belly=1.18, width=1.04, belt=mat('Gus Belt', (40, 32, 30)), belt_z=.555)
    for z in (.72, .86):
        r = .30 * 1.18 * 1.04 + .005
        P.append(skin(torus(t + ' stripe', (0, -.03 * .18 * 3, z), r * (1 if z < .8 else .98), .018, stripe, scale=(1, .88, .4)), ['Spine']))
    P += limbs(t, mat('Gus Shirt', (110, 140, 170)), sk, pants, shoe, glove=mat('Gus Gloves', (200, 170, 90)), sleeve_len=.45, sole=sole)
    P += head(t, sk, size=(.31, .29, .28), nose=mat('Gus Nose', (230, 130, 120)), nose_size=(.085, .08, .07), eye_gap=.105, brow_m=mat('Gus Grey Hair', (130, 120, 115)), brow_tilt=-10, mouth=None)
    P += ears(t, sk, (.035, .055, .07))
    hair = mat('Gus Grey Hair', (130, 120, 115))
    # moustache: two drooping lumps
    for s in (1, -1):
        P.append(skin(sphere(t + ' stache', (s * .075, -.305, 1.335), (.1, .05, .045), hair, 16, 8), ['Head']))
        P.append(skin(sphere(t + ' stache tip', (s * .155, -.285, 1.305), (.05, .035, .035), hair, 10, 6), ['Head']))
    # hard hat: dome + wide brim + ridge
    hh = mat('Gus Hard Hat', (255, 204, 40), .45)
    P.append(skin(sphere(t + ' hardhat', (0, 0, 1.56), (.29, .3, .22), hh, 24, 12, flat_bottom=0), ['Head']))
    P.append(skin(cyl(t + ' brim', (0, -.03, 1.56), .36, .025, hh, seg=32, bevel=.008), ['Head']))
    P.append(skin(box(t + ' ridge', (0, 0, 1.76), (.06, .5, .05), hh, bevel=.02), ['Head']))
    return P

def v_pip():
    """Pip: the nervous engineer. Bowl cut, huge round glasses, lab coat, buck teeth."""
    t = 'Pip'
    sk = mat('Bean Skin Brown', SKIN['brown'])
    coat = mat('Pip Lab Coat', (236, 240, 242)); pants = mat('Pip Trousers', (60, 150, 150))
    shoe = mat('Pip Loafers', (120, 60, 40)); sole = mat('Bean Rubber', (60, 55, 60))
    P = body(t, coat, pants, belly=.92, width=.95, belt_z=.47)
    # coat collar + pocket with pens
    for s in (1, -1):
        P.append(skin(box(t + ' lapel', (s * .09, -.235, 1.02), (.1, .03, .16), coat, bevel=.012, rot=(12, 0, s * 25)), ['Chest']))
    P.append(skin(box(t + ' shirt', (0, -.24, 1.0), (.09, .02, .12), mat('Pip Shirt', (120, 170, 230)), bevel=.01), ['Chest']))
    P.append(skin(box(t + ' pocket', (.13, -.255, .88), (.1, .02, .09), coat, bevel=.008, rot=(8, 0, 0)), ['Chest']))
    for i, c in enumerate([(220, 60, 60), (60, 90, 220)]):
        P.append(skin(cyl(t + ' pen', (.11 + i * .035, -.255, .93), .012, .09, mat('Pip Pen %d' % i, c), bevel=0), ['Chest']))
    P += limbs(t, coat, sk, pants, shoe, sleeve_len=.9, sole=sole)
    P += head(t, sk, size=(.29, .28, .31), nose=mat('Pip Nose', (135, 86, 56)), nose_size=(.045, .05, .06), eye_gap=.1, eye_size=(.03, .018, .04), mouth=None, brow_tilt=-18)
    P += ears(t, sk, (.035, .06, .075))
    hair = mat('Pip Hair', (40, 30, 30))
    P.append(skin(sphere(t + ' bowl', (0, .02, 1.5), (.315, .31, .26), hair, 26, 14, flat_bottom=-.05), ['Head']))
    P.append(skin(sphere(t + ' tuft', (.03, .0, 1.75), (.04, .03, .1), hair, 10, 6), ['Head']))
    frame = mat('Pip Glasses', (30, 30, 36), .4)
    for s in (1, -1):
        P.append(skin(torus(t + ' lens', (s * .1, -.31, 1.45), .075, .012, frame, rot=(90, 0, 0)), ['Eyes']))
        P.append(skin(sphere(t + ' glass', (s * .1, -.305, 1.45), (.07, .008, .07), mat('Pip Lens', (200, 230, 240), .1), 16, 6), ['Eyes']))
        P.append(skin(tube(t + ' arm', [(s * .175, -.31, 1.45), (s * .29, -.05, 1.45)], [.01, .01], frame, seg=6), ['Head']))
    P.append(skin(tube(t + ' bridge', [(-.03, -.315, 1.455), (.03, -.315, 1.455)], [.01, .01], frame, seg=6), ['Eyes']))
    for s in (1, -1):
        P.append(skin(box(t + ' tooth', (s * .014, face_y((.29, .28, .31), 0, 1.29, -.012), 1.285), (.024, .02, .03), WHITE, bevel=.006), ['Head']))
    return P

def v_dot():
    """Dot: the fearless mover. Backwards cap, ginger ponytail, overalls, freckles."""
    t = 'Dot'
    sk = mat('Bean Skin Tan', SKIN['tan'])
    tee = mat('Dot Tee', (245, 240, 230)); overalls = mat('Dot Overalls', (120, 80, 170))
    shoe = mat('Dot Boots', (60, 150, 80)); sole = mat('Bean Rubber', (60, 55, 60))
    P = body(t, tee, overalls, belly=.97, belt_z=.62)
    # bib and straps
    P.append(skin(box(t + ' bib', (0, -.265, .78), (.3, .04, .26), overalls, bevel=.02, rot=(-4, 0, 0)), ['Spine']))
    for s in (1, -1):
        P.append(skin(tube(t + ' strap', [(s * .1, -.27, .9), (s * .15, -.2, 1.1), (s * .15, .1, 1.1), (s * .12, .26, .8)], [.02, .02, .02, .02], overalls, seg=6, steps=3), ['Spine', 'Chest']))
        P.append(skin(sphere(t + ' button', (s * .1, -.29, .9), (.022, .012, .022), mat('Dot Brass', (230, 190, 70), .3, .6), 10, 6), ['Spine']))
    P += limbs(t, tee, sk, overalls, shoe, sleeve_len=.35, sole=sole)
    P += head(t, sk, size=(.29, .28, .29), nose=mat('Dot Nose', (224, 150, 100)), nose_size=(.05, .05, .045), eye_gap=.095)
    P += ears(t, sk)
    for s in (1, -1):
        P.append(skin(sphere(t + ' cheek', (s * .16, -.24, 1.36), (.05, .015, .035), BLUSH, 10, 6), ['Head']))
        for i, (dx, dz) in enumerate([(.13, 1.39), (.17, 1.4), (.15, 1.37)]):
            P.append(skin(sphere(t + ' freckle', (s * dx, -.26 + abs(dx) * .15, dz), (.009, .006, .009), mat('Dot Freckle', (170, 100, 60)), 6, 4), ['Head']))
    hair = mat('Dot Hair', (220, 100, 40))
    cap = mat('Dot Cap', (40, 170, 120))
    P.append(skin(sphere(t + ' fringe', (0, -.02, 1.585), (.305, .29, .13), hair, 20, 10), ['Head']))
    P.append(skin(sphere(t + ' cap', (0, .0, 1.56), (.3, .3, .21), cap, 24, 12, flat_bottom=0), ['Head']))
    P.append(skin(sphere(t + ' visor', (0, .3, 1.565), (.2, .17, .02), cap, 16, 6), ['Head']))
    P.append(skin(sphere(t + ' cap button', (0, 0, 1.775), (.03, .03, .02), cap, 8, 6), ['Head']))
    P.append(skin(sphere(t + ' tail', (0, .33, 1.44), (.08, .12, .1), hair, 14, 8), ['Head']))
    P.append(skin(sphere(t + ' tail tip', (0, .43, 1.36), (.06, .08, .09), hair, 12, 8), ['Head']))
    return P

meshes = []
for fn, name in ((v_bolt, 'Bolt'), (v_gus, 'Gus'), (v_pip, 'Pip'), (v_dot, 'Dot')):
    m = join('Crew_' + name, fn())
    m.parent = rig
    mod = m.modifiers.new('Armature', 'ARMATURE'); mod.object = rig
    meshes.append(m)
    print('mesh', m.name, len(m.data.vertices), 'verts', len(m.data.materials), 'mats')

# --------------------------------------------------------------------------- animation
pb = rig.pose.bones
REST = {b.name: b.bone.matrix_local.to_quaternion() for b in pb}
REST_M = {b.name: b.bone.matrix_local.to_3x3() for b in pb}
for b in pb: b.rotation_mode = 'QUATERNION'

def rot(x=0, y=0, z=0):
    return Euler((math.radians(x), math.radians(y), math.radians(z)), 'XYZ').to_quaternion()

def apply_pose(pose):
    for b in pb:
        b.rotation_quaternion = Quaternion(); b.location = Vector(); b.scale = Vector((1, 1, 1))
    for name, v in pose.items():
        if name == 'hips_loc':
            pb['Hips'].location = REST_M['Hips'].inverted() @ Vector(v); continue
        if name == 'blink':
            # squash the Eyes bone vertically (world Z expressed in its local axes)
            local_z = REST_M['Eyes'].inverted() @ Vector((0, 0, 1))
            sc = [1, 1, 1]
            i = max(range(3), key=lambda k: abs(local_z[k])); sc[i] = v
            pb['Eyes'].scale = Vector(sc); continue
        q = rot(*v)
        pb[name].rotation_quaternion = REST[name].inverted() @ q @ REST[name]

def key_all(frame):
    for b in pb:
        b.keyframe_insert('rotation_quaternion', frame=frame)
        b.keyframe_insert('location', frame=frame)
        b.keyframe_insert('scale', frame=frame)

def make_action(name, keys, loop=True):
    rig.animation_data_create()
    act = bpy.data.actions.new(name); act.use_fake_user = True
    rig.animation_data.action = act
    for f, pose in keys:
        apply_pose(pose); key_all(f)
    act.frame_range = (keys[0][0], keys[-1][0])
    act.use_frame_range = True
    act.use_cyclic = loop
    return act

def mirror(pose):
    out = {}
    for k, v in pose.items():
        if k.endswith('L'): out[k[:-1] + 'R'] = (v[0], -v[1], -v[2])
        elif k.endswith('R'): out[k[:-1] + 'L'] = (v[0], -v[1], -v[2])
        elif k == 'hips_loc': out[k] = (-v[0], v[1], v[2])
        elif k == 'blink': out[k] = v
        else: out[k] = (v[0], -v[1], -v[2])
    return out

def merge(*ps):
    out = {}
    for p in ps: out.update(p)
    return out

ARMS_REST = {'UpperArmL': (0, -6, 0), 'UpperArmR': (0, 6, 0), 'ForearmL': (-10, 0, 0), 'ForearmR': (-10, 0, 0)}

# Idle: breathe, sway, blink ------------------------------------------------
def idle_pose(t, carry=False):
    w = math.sin(t * 2 * math.pi)
    p = {'Spine': (1.5 * w, 0, 0), 'Chest': (1.5 * w, 0, 0), 'Head': (-2 * w, 0, 3 * math.sin(t * 2 * math.pi + 1)),
         'hips_loc': (0, 0, .006 * w)}
    if not carry:
        p.update({'UpperArmL': (2 * w, -7 - 2 * w, 0), 'UpperArmR': (-2 * w, 7 + 2 * w, 0), 'ForearmL': (-12, 0, 0), 'ForearmR': (-12, 0, 0)})
    return p

CARRY_ARMS = {'UpperArmL': (-72, 8, 0), 'UpperArmR': (-72, -8, 0), 'ForearmL': (-18, 10, 0), 'ForearmR': (-18, -10, 0),
              'HandL': (0, 0, -25), 'HandR': (0, 0, 25), 'Spine': (-4, 0, 0)}

def blink_keys(frames, at):
    return {at: .1, at - 2: 1, at + 2: 1}

idle = []
for f in range(0, 61, 5):
    p = idle_pose(f / 60)
    idle.append((f, p))
idle.insert(0, (0, idle_pose(0)))
idle = sorted({f: p for f, p in idle}.items())
# blink around frame 40
idle = [(f, p) for f, p in idle if not 36 <= f <= 44]
for f, b in ((37, 1), (40, .08), (43, 1)):
    idle.append((f, merge(idle_pose(f / 60), {'blink': b})))
idle.sort(key=lambda x: x[0])
make_action('Idle', idle)

carry_idle = []
for f in range(0, 61, 10):
    p = idle_pose(f / 60, carry=True)
    w = math.sin(f / 60 * 2 * math.pi)
    arms = {k: ((v[0] + 2 * w) if k.startswith('UpperArm') else v[0], v[1], v[2]) for k, v in CARRY_ARMS.items()}
    arms['Spine'] = (-4 + 1.5 * w, 0, 0)
    carry_idle.append((f, merge(p, arms)))
make_action('Carry', carry_idle)

# Walk: bouncy waddle -----------------------------------------------------------
def walk_contact(carry=False):
    p = {'ThighL': (-32, 0, 0), 'ShinL': (6, 0, 0), 'FootL': (-10, 0, 0),
         'ThighR': (26, 0, 0), 'ShinR': (30, 0, 0), 'FootR': (22, 0, 0),
         'Hips': (0, 7, 6), 'Spine': (4, -3, -5), 'Chest': (0, -3, -6), 'Head': (-3, 3, 8),
         'hips_loc': (.0, 0, -.015)}
    if carry:
        p.update(CARRY_ARMS); p['Spine'] = (-3, -3, -3); p['Chest'] = (0, 0, -3)
        p['UpperArmL'] = (-70, 8, 0); p['UpperArmR'] = (-74, -8, 0)
    else:
        p.update({'UpperArmL': (30, -8, 0), 'UpperArmR': (-34, 8, 0), 'ForearmL': (-8, 0, 0), 'ForearmR': (-30, 0, 0)})
    return p

def walk_pass(carry=False):
    p = {'ThighL': (-4, 0, 0), 'ShinL': (4, 0, 0), 'FootL': (0, 0, 0),
         'ThighR': (-22, 0, 0), 'ShinR': (62, 0, 0), 'FootR': (-8, 0, 0),
         'Hips': (0, 9, 2), 'Spine': (5, -5, 0), 'Chest': (0, 0, 0), 'Head': (-4, 4, 0),
         'hips_loc': (.012, 0, .05)}
    if carry:
        p.update(CARRY_ARMS); p['Spine'] = (-5, 0, 0)
        p['UpperArmL'] = (-76, 8, 0); p['UpperArmR'] = (-76, -8, 0)
    else:
        p.update({'UpperArmL': (4, -8, 0), 'UpperArmR': (-6, 8, 0), 'ForearmL': (-15, 0, 0), 'ForearmR': (-18, 0, 0)})
    return p

def walk_keys(carry):
    c, pp = walk_contact(carry), walk_pass(carry)
    return [(0, c), (6, pp), (12, mirror(c)), (18, mirror(pp)), (24, c)]

make_action('Walk', walk_keys(False))
make_action('Carry Walk', walk_keys(True))

# Pick up: squat, grab, heave -------------------------------------------------
squat = {'hips_loc': (0, -.04, -.2), 'ThighL': (-70, 0, 0), 'ThighR': (-70, 0, 0), 'ShinL': (100, 0, 0), 'ShinR': (100, 0, 0),
         'FootL': (-30, 0, 0), 'FootR': (-30, 0, 0), 'Spine': (28, 0, 0), 'Chest': (14, 0, 0), 'Head': (-20, 0, 0),
         'UpperArmL': (-55, 12, 0), 'UpperArmR': (-55, -12, 0), 'ForearmL': (-10, 0, 0), 'ForearmR': (-10, 0, 0)}
heave = merge(CARRY_ARMS, {'hips_loc': (0, 0, .03), 'Spine': (-10, 0, 0), 'Chest': (-6, 0, 0), 'Head': (6, 0, 0),
                           'ThighL': (-4, 0, 0), 'ThighR': (-4, 0, 0), 'ShinL': (6, 0, 0), 'ShinR': (6, 0, 0)})
make_action('Pick Up', [(0, idle_pose(0)), (5, squat), (8, squat), (13, heave), (18, merge(idle_pose(0, True), CARRY_ARMS))], loop=False)

# Throw: two-handed overhead heave (release at .25 s = frame 7.5) ----------------
wind = merge({'UpperArmL': (-165, 10, 0), 'UpperArmR': (-165, -10, 0), 'ForearmL': (-55, 0, 0), 'ForearmR': (-55, 0, 0),
              'HandL': (-20, 0, -20), 'HandR': (-20, 0, 20), 'Spine': (-14, 0, 0), 'Chest': (-10, 0, 0), 'Head': (8, 0, 0),
              'ThighL': (-18, 0, 0), 'ShinL': (10, 0, 0), 'ThighR': (12, 0, 0), 'ShinR': (8, 0, 0), 'hips_loc': (0, .04, -.02)})
release = merge({'UpperArmL': (-80, 6, 0), 'UpperArmR': (-80, -6, 0), 'ForearmL': (-5, 0, 0), 'ForearmR': (-5, 0, 0),
                 'HandL': (15, 0, 0), 'HandR': (15, 0, 0), 'Spine': (20, 0, 0), 'Chest': (12, 0, 0), 'Head': (-12, 0, 0),
                 'ThighL': (-30, 0, 0), 'ShinL': (20, 0, 0), 'FootL': (-6, 0, 0), 'ThighR': (18, 0, 0), 'ShinR': (18, 0, 0), 'hips_loc': (0, -.06, -.05)})
follow = merge({'UpperArmL': (-35, -4, 0), 'UpperArmR': (-35, 4, 0), 'ForearmL': (-15, 0, 0), 'ForearmR': (-15, 0, 0),
                'Spine': (16, 0, 0), 'Chest': (8, 0, 0), 'Head': (-6, 0, 0),
                'ThighL': (-20, 0, 0), 'ShinL': (18, 0, 0), 'ThighR': (10, 0, 0), 'ShinR': (12, 0, 0), 'hips_loc': (0, -.04, -.04)})
# keys: wind-up 0-4, hold, whip through the release pose at frame 8 (0.27 s ~ PlayerThrow.releaseDelay .25 s)
make_action('Throw', [(0, merge(idle_pose(0, True), CARRY_ARMS)), (4, wind), (5, wind), (8, release), (12, follow), (21, idle_pose(0))], loop=False)

# Wave: friendly emote (spare clip for greetings / testing) -----------------------
wave_up = merge(idle_pose(0), {'UpperArmR': (-10, 150, 0), 'ForearmR': (0, 0, -30), 'Head': (0, 0, -8), 'Chest': (0, 0, -5)})
wave_l = merge(wave_up, {'ForearmR': (0, 0, 10)})
make_action('Wave', [(0, idle_pose(0)), (6, wave_up), (10, wave_l), (14, wave_up), (18, wave_l), (22, wave_up), (30, idle_pose(0))], loop=False)

rig.animation_data.action = bpy.data.actions['Idle']
apply_pose({})

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'BeanCrew.blend'))
export_fbx(os.path.join(OUT, 'BeanCrew.fbx'), [rig] + meshes, anim=True)
write_palette(os.path.join(OUT, 'BeanPalette.json'))
print('BEAN_CREW_EXPORTED', [a.name for a in bpy.data.actions])
