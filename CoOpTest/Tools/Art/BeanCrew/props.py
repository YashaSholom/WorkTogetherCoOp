"""Chunky cartoon workshop props for the Bean Crew style.

Run:  python3 props.py OUTDIR
Each prop is modelled in metres around the centre of the Unity collider it dresses
(Blender Z = Unity Y). Exports OUTDIR/Props/<Name>.fbx and OUTDIR/BeanProps.blend.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from mathutils import Vector
from common import *

OUT = sys.argv[-1] if len(sys.argv) > 1 else '/home/claude/art/out'
PROPS = os.path.join(OUT, 'Props'); os.makedirs(PROPS, exist_ok=True)
reset()

WOOD = mat('Prop Pine', (222, 164, 96), .8)
WOOD_D = mat('Prop Pine Dark', (160, 104, 58), .85)
IRON = mat('Prop Iron', (70, 76, 86), .5, .4)
STEEL = mat('Prop Steel', (170, 178, 188), .35, .7)
RED = mat('Prop Red', (225, 60, 52), .55)
YELLOW = mat('Prop Yellow', (255, 200, 40), .5)
BLACK = mat('Prop Black', (36, 34, 40), .6)
GREEN = mat('Prop Cell Green', (70, 200, 110), .45)
COPPER = mat('Prop Copper', (220, 140, 80), .35, .7)
CREAM = mat('Prop Cream', (245, 236, 210), .6)
TEAL = mat('Prop Teal', (50, 150, 160), .5)
ORANGE = mat('Prop Orange', (250, 130, 40), .55)
BLUE = mat('Prop Blue', (70, 120, 210), .5)
GLASS = mat('Prop Glass', (180, 225, 240), .1)
GLOW = mat('Prop Glow', (255, 230, 150), .3, emit=(255, 220, 130))
RUBBER = mat('Prop Rubber', (45, 42, 48), .9)

exported = {}

def ship(name, parts):
    o = join('Prop_' + name, parts)
    # origin already at world origin (the collider centre)
    exported[name] = o
    export_fbx(os.path.join(PROPS, name + '.fbx'), [o])
    o.hide_render = True
    return o

# ---------------------------------------------------------------- crate 0.55 cube
def crate():
    h = .275; P = []
    P.append(box('core', (0, 0, 0), (.5, .5, .5), WOOD_D, bevel=.01))
    for face in range(4):
        ang = face * 90
        for i, z in enumerate((-.15, 0, .15)):
            v = Vector((0, -.255, z))
            rot = (0, 0, ang)
            import mathutils
            R = mathutils.Matrix.Rotation(math.radians(ang), 3, 'Z')
            P.append(box('plank', R @ v, (.46 if face % 2 == 0 else .03, .03 if face % 2 == 0 else .46, .13), WOOD, bevel=.012))
    for z in (-.26, .26):
        P.append(box('lid', (0, 0, z), (.5, .5, .03), WOOD, bevel=.012))
    # dark corner posts + diagonal brace
    for sx in (-1, 1):
        for sy in (-1, 1):
            P.append(box('post', (sx * .255, sy * .255, 0), (.07, .07, .56), WOOD_D, bevel=.015))
    for sy in (-1, 1):
        P.append(box('brace', (0, sy * .272, 0), (.62, .035, .07), WOOD_D, bevel=.012, rot=(0, 45, 0)))
    # stencil badge
    P.append(cyl('badge', (0, -.29, 0), .07, .012, RED, axis='Y', bevel=.003))
    return ship('BeanCrate', P)

# ---------------------------------------------------------------- power cell 0.35 x 0.6(h) x 0.35
def cell():
    P = [cyl('body', (0, 0, -.02), .16, .46, GREEN, seg=28, bevel=.03)]
    P.append(cyl('band', (0, 0, -.02), .166, .16, CREAM, seg=28, bevel=.006))
    P.append(cyl('base', (0, 0, -.27), .17, .06, BLACK, seg=28, bevel=.015))
    P.append(cyl('cap', (0, 0, .23), .17, .05, BLACK, seg=28, bevel=.015))
    P.append(cyl('terminal', (0, 0, .28), .06, .06, COPPER, seg=16, bevel=.012))
    P.append(cyl('nub', (0, 0, .305), .03, .03, COPPER, seg=12, bevel=.008))
    # lightning bolt on the label (two chunky slanted bars)
    P.append(box('bolt a', (.02, -.168, .01), (.035, .012, .08), YELLOW, bevel=.004, rot=(0, -25, 0)))
    P.append(box('bolt b', (-.015, -.168, -.05), (.035, .012, .08), YELLOW, bevel=.004, rot=(0, -25, 0)))
    P.append(box('bolt c', (0, -.168, -.02), (.07, .012, .025), YELLOW, bevel=.004))
    # carry handle
    P.append(torus('handle', (0, 0, .25), .08, .016, BLACK, rot=(90, 0, 0), arc=.5, seg=12))
    return ship('PowerCell', P)

# ---------------------------------------------------------------- wrench 0.6 x 0.18(h) x 0.25
def wrench():
    P = [box('shaft', (0, 0, 0), (.36, .07, .04), STEEL, bevel=.015)]
    P.append(box('grip', (.1, 0, 0), (.18, .085, .06), RED, bevel=.02))
    # open jaw at -X: ring with a gap
    P.append(torus('jaw', (-.22, 0, 0), .075, .03, STEEL, rot=(0, 0, 40), arc=.78, seg=18, mseg=10, scale=(1, 1, .7)))
    # ring end at +X
    P.append(torus('ring', (.24, 0, 0), .055, .025, STEEL, seg=18, mseg=8, scale=(1, 1, .8)))
    return ship('Wrench', P)

# ---------------------------------------------------------------- generator 2 x 1.2(h) x 1.2, origin centre
def generator():
    P = []
    # skids
    for sy in (-1, 1):
        P.append(box('skid', (0, sy * .42, -.56), (2.0, .14, .08), IRON, bevel=.025))
    P.append(box('body', (-.15, 0, -.1), (1.55, 1.0, .82), ORANGE, bevel=.09, seg=3))
    # fuel tank
    P.append(box('tank', (-.25, 0, .42), (1.1, .8, .24), RED, bevel=.08, seg=3))
    P.append(cyl('fuel cap', (-.6, 0, .57), .08, .06, BLACK, bevel=.015))
    # control panel on front face (-Y)
    P.append(box('panel', (-.2, -.51, -.05), (.9, .04, .45), CREAM, bevel=.02))
    for i, x in enumerate((-.5, -.2)):
        P.append(cyl('dial', (x, -.54, 0), .09, .03, BLACK, axis='Y', bevel=.008))
        P.append(cyl('dial face', (x, -.556, 0), .07, .01, mat('Prop Dial', (240, 240, 230)), axis='Y', bevel=0))
        P.append(box('needle', (x + .02, -.565, .02), (.06, .006, .012), RED, bevel=0, rot=(0, -40 + i * 70, 0)))
    P.append(cyl('switch', (.12, -.545, .0), .05, .05, RED, axis='Y', bevel=.012))
    # battery slot light
    P.append(box('slot', (.12, -.535, -.18), (.16, .02, .06), BLACK, bevel=.01))
    # engine block + exhaust at +X
    P.append(box('engine', (.8, 0, -.18), (.42, .8, .66), IRON, bevel=.06, seg=3))
    for z in (-.35, -.2, -.05, .1):
        P.append(box('fin', (1.02, 0, z), (.04, .7, .05), STEEL, bevel=.012))
    P.append(tube('exhaust', [(.85, .25, .15), (.85, .25, .5), (.85, .25, .62)], [.06, .06, .075], STEEL, seg=14, round_ends=False))
    P.append(cyl('exhaust tip', (.85, .25, .62), .08, .06, BLACK, bevel=.01))
    # roll cage
    for sx in (-.95, .45):
        for sy in (-1, 1):
            P.append(tube('cage', [(sx, sy * .52, -.52), (sx, sy * .52, .45), (sx, 0, .6), (sx, -sy * .52, .45)], [.03] * 4, IRON, seg=8, steps=2))
    P.append(tube('rail', [(-.95, .0, .6), (.45, 0, .6)], [.03, .03], IRON, seg=8))
    return ship('Generator', P)

# ---------------------------------------------------------------- workbench 4 x 1(h) x 1.5, origin centre
def bench():
    P = [box('top', (0, 0, .45), (4.0, 1.5, .1), WOOD, bevel=.03)]
    for x in (-1.2, -.4, .4, 1.2):
        P.append(box('top line', (x, 0, .5), (.02, 1.46, .01), WOOD_D, bevel=0))
    P.append(box('shelf', (0, 0, -.3), (3.7, 1.3, .06), WOOD_D, bevel=.02))
    for sx in (-1, 1):
        for sy in (-1, 1):
            P.append(box('leg', (sx * 1.88, sy * .62, -.05), (.12, .12, .9), IRON, bevel=.02))
    for sy in (-1, 1):
        P.append(box('apron', (0, sy * .7, .33), (3.8, .05, .14), IRON, bevel=.015))
    # vice on the right end
    P.append(box('vice base', (1.65, -.45, .56), (.3, .25, .12), BLUE, bevel=.03))
    P.append(box('vice jaw', (1.65, -.62, .62), (.26, .08, .14), BLUE, bevel=.02))
    P.append(tube('vice screw', [(1.65, -.66, .6), (1.65, -.82, .6)], [.015, .015], STEEL, seg=8))
    P.append(tube('vice bar', [(1.55, -.82, .6), (1.75, -.82, .6)], [.012, .012], STEEL, seg=8))
    # clutter on the lower shelf: paint tins
    for i, (x, c) in enumerate(((-1.4, RED), (-1.15, BLUE), (1.2, YELLOW))):
        P.append(cyl('tin', (x, .2, -.18), .1, .18, c, bevel=.015))
        P.append(cyl('tin lid', (x, .2, -.085), .1, .015, STEEL, bevel=.004))
    return ship('Workbench', P)

# ---------------------------------------------------------------- slot tray 0.8 x 0.15(h) x 0.8
def tray(name='SlotTray', dock=False):
    P = [box('base', (0, 0, -.045), (.78, .78, .06), BLACK, bevel=.02)]
    # hazard rim: alternating blocks
    n = 6
    for side in range(4):
        for i in range(n):
            t = (i + .5) / n - .5
            c = YELLOW if (i + side) % 2 == 0 else BLACK
            if side % 2 == 0:
                P.append(box('rim', (t * .78, (1 if side == 0 else -1) * .36, .01), (.78 / n, .06, .06), c, bevel=.008))
            else:
                P.append(box('rim', ((1 if side == 1 else -1) * .36, t * .78, .01), (.06, .78 / n, .06), c, bevel=.008))
    P.append(box('pad', (0, 0, -.01), (.62, .62, .02), mat('Prop Pad', (90, 96, 104), .8), bevel=.01))
    if dock:
        # support pillar down to the floor (socket centre sits .85 m above the floor)
        P.append(box('pillar', (0, 0, -.45), (.22, .22, .78), IRON, bevel=.03))
        P.append(box('foot', (0, 0, -.83), (.5, .5, .05), IRON, bevel=.02))
        # cable back to the generator (+Y in Blender = towards the generator in Unity)
        P.append(tube('cable', [(.2, .1, -.06), (.3, .3, -.3), (.3, .55, -.6), (.3, .6, -.8)], [.03] * 4, BLACK, seg=8, steps=4))
        P.append(cyl('plug', (0, 0, .0), .09, .03, COPPER, bevel=.008))
    return ship(name, P)

# ---------------------------------------------------------------- door 2 x 2.5(h) x 0.25
def door():
    P = [box('slab', (0, 0, 0), (1.94, .18, 2.44), mat('Prop Door Blue', (60, 110, 150), .6), bevel=.04)]
    for x in (-.65, 0, .65):
        P.append(box('plank line', (x, -.095, .15), (.02, .01, 2.0), mat('Prop Door Line', (45, 85, 120)), bevel=0))
    for z in (-.8, .9):
        P.append(box('strap', (0, -.1, z), (1.96, .04, .16), IRON, bevel=.015))
        for x in (-.85, -.3, .3, .85):
            P.append(sphere('rivet', (x, -.125, z), (.025, .012, .025), STEEL, 8, 6))
    # hazard kick plate
    for i in range(8):
        P.append(box('hazard', (-.86 + i * .245, -.1, -1.1), (.245, .03, .2), YELLOW if i % 2 == 0 else BLACK, bevel=.006, rot=(0, 0, 0)))
    # porthole window
    P.append(torus('porthole', (0, -.1, .45), .22, .04, STEEL, rot=(90, 0, 0), seg=28))
    P.append(cyl('window', (0, -.07, .45), .22, .04, GLASS, axis='Y', bevel=0))
    # handle
    P.append(tube('handle', [(.7, -.1, .05), (.7, -.2, .05), (.7, -.2, -.2), (.7, -.1, -.2)], [.025] * 4, STEEL, seg=8, steps=2))
    return ship('WorkshopDoor', P)

# ---------------------------------------------------------------- button 0.4 cube at 1 m height
def button():
    P = [box('housing', (0, 0, -.02), (.34, .34, .22), YELLOW, bevel=.04)]
    P.append(cyl('collar', (0, 0, .11), .14, .05, BLACK, bevel=.012))
    P.append(sphere('mushroom', (0, 0, .14), (.13, .13, .08), RED, 20, 10, flat_bottom=0))
    # pedestal to the floor: collider centre is 1 m up
    P.append(cyl('post', (0, 0, -.58), .06, .9, IRON, bevel=.01))
    P.append(cyl('foot', (0, 0, -.98), .2, .04, IRON, bevel=.01))
    P.append(box('label', (0, -.172, -.02), (.2, .01, .07), CREAM, bevel=.004))
    return ship('BigButton', P)

# ---------------------------------------------------------------- pendant lamp (hangs at the light's position)
def lamp():
    P = [tube('cord', [(0, 0, .15), (0, 0, 1.5)], [.012, .012], BLACK, seg=6, round_ends=False)]
    shade = loft('shade', [((0, 0, .15), .05, .05), ((0, 0, .1), .08, .08), ((0, 0, .02), .2, .2), ((0, 0, -.02), .24, .24), ((0, 0, -.025), .235, .235), ((0, 0, .0), .19, .19), ((0, 0, .08), .07, .07)], TEAL, seg=24, cap0=True, cap1=True)
    P.append(shade)
    P.append(sphere('bulb off', (0, 0, -.04), (.07, .07, .08), mat('Prop Bulb Off', (200, 196, 180), .3), 14, 8))
    o = ship('PendantLamp', P)
    g = ship('PendantLampGlow', [sphere('bulb on', (0, 0, -.04), (.075, .075, .085), GLOW, 14, 8)])
    return o

# ---------------------------------------------------------------- decor (static)
def cone():
    P = [box('base', (0, 0, .02), (.42, .42, .04), ORANGE, bevel=.015)]
    P.append(cyl('cone', (0, 0, .33), .16, .6, ORANGE, r2=.03, bevel=.01))
    P.append(cyl('stripe', (0, 0, .36), .105, .09, CREAM, r2=.09, bevel=0))
    return ship('TrafficCone', P)

def barrel():
    P = [cyl('drum', (0, 0, .45), .3, .9, BLUE, seg=28, bevel=.03)]
    for z in (.2, .7):
        P.append(torus('hoop', (0, 0, z), .302, .02, IRON, seg=28))
    P.append(cyl('bung', (.15, 0, .905), .04, .02, STEEL, bevel=.005))
    return ship('OilDrum', P)

def pallet():
    P = []
    for x in (-.5, 0, .5):
        P.append(box('runner', (x, 0, .05), (.12, 1.1, .1), WOOD_D, bevel=.012))
    for y in (-.45, -.22, 0, .22, .45):
        P.append(box('board', (0, y, .12), (1.2, .16, .04), WOOD, bevel=.01))
    P.append(box('box a', (-.28, -.1, .38), (.5, .6, .48), mat('Prop Cardboard', (200, 150, 95)), bevel=.02))
    P.append(box('tape a', (-.28, -.1, .62), (.08, .61, .01), mat('Prop Tape', (230, 200, 140)), bevel=0))
    P.append(box('box b', (.3, .15, .32), (.44, .44, .36), mat('Prop Cardboard', (200, 150, 95)), bevel=.02))
    P.append(box('box c', (.25, .1, .64), (.3, .3, .28), mat('Prop Cardboard', (200, 150, 95)), bevel=.02, rot=(0, 0, 18)))
    return ship('PalletStack', P)

def toolboard():
    P = [box('board', (0, 0, 0), (2.0, .05, 1.0), mat('Prop Pegboard', (205, 175, 130)), bevel=.015)]
    for z in (-.3, 0, .3):
        for x in (-.9, -.6, -.3, 0, .3, .6, .9):
            P.append(cyl('hole', (x, -.027, z + .15), .012, .006, WOOD_D, axis='Y', bevel=0))
    # hanging tools silhouettes
    P.append(box('saw', (-.6, -.05, -.05), (.5, .015, .16), STEEL, bevel=.005))
    P.append(box('saw grip', (-.3, -.05, -.05), (.12, .03, .14), RED, bevel=.02))
    P.append(box('hammer head', (.1, -.05, .25), (.2, .05, .07), IRON, bevel=.015))
    P.append(box('hammer handle', (.1, -.05, .03), (.04, .04, .4), WOOD_D, bevel=.01))
    P.append(tube('coil', [(.6, -.05, .3), (.75, -.05, .1), (.55, -.05, -.1), (.8, -.05, -.25)], [.025] * 4, YELLOW, seg=8, steps=4))
    return ship('Pegboard', P)

for f in (crate, cell, wrench, generator, bench, lambda: tray('SlotTray'), lambda: tray('BatteryDock', True), door, button, lamp, cone, barrel, pallet, toolboard):
    f()

for o in bpy.data.objects: o.hide_render = False
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'BeanProps.blend'))
write_palette(os.path.join(OUT, 'BeanPalette.json'))
c = bpy.data.objects['Prop_BeanCrate']; zs = [(c.matrix_world @ v.co).z for v in c.data.vertices]
print('CRATE_HEIGHT', max(zs) - min(zs))
print('PROPS_EXPORTED', sorted(exported))
