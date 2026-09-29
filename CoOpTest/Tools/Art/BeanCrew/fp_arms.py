"""First-person arms for the Bean Crew (one left/right pair per outfit).

Run:  python3 fp_arms.py OUTDIR   ->  OUTDIR/FirstPersonArms.fbx (+ .blend)
Each arm object has its origin at the shoulder and points along Blender +Y, which is
Unity +Z (forward) with the baked-axis export used for props. Knuckles face up (+Z / Unity +Y).
The mitt centre sits at ARM_LENGTH along the arm; FirstPersonArms.cs uses the same value.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from mathutils import Vector
from common import *

OUT = sys.argv[-1] if len(sys.argv) > 1 else '/home/claude/art/out'
os.makedirs(OUT, exist_ok=True)
reset()
ARM_LENGTH = .55

# Same material names as the body so Unity reuses the same URP materials.
SK = {'peach': (241, 190, 150), 'tan': (214, 160, 112), 'brown': (150, 98, 64), 'pink': (240, 172, 158)}
OUTFITS = {
    #        sleeve material                          sleeve end   skin material                       mitt material (None = skin)          cuff
    'Bolt': (('Bolt Hoodie', (240, 178, 48)), .86, ('Bean Skin Peach', SK['peach']), None, ('Bolt Pocket', (220, 155, 36))),
    'Gus': (('Gus Shirt', (110, 140, 170)), .32, ('Bean Skin Pink', SK['pink']), ('Gus Gloves', (200, 170, 90)), None),
    'Pip': (('Pip Lab Coat', (236, 240, 242)), .84, ('Bean Skin Brown', SK['brown']), None, ('Pip Shirt', (120, 170, 230))),
    'Dot': (('Dot Tee', (245, 240, 230)), .26, ('Bean Skin Tan', SK['tan']), None, None),
}

def arm(name, side, sleeve, sleeve_end, skin_m, mitt_m, cuff):
    s = 1 if side == 'R' else -1          # thumb points toward the screen centre
    L = ARM_LENGTH
    wrist = L - .09
    P = []
    # forearm/upper arm in skin, then sleeve over it
    P.append(tube(name + ' arm', [(0, -.1, 0), (0, wrist * .5, -.005), (0, wrist, 0)], [.062, .058, .052], skin_m, seg=16, steps=4))
    end = wrist * sleeve_end
    P.append(tube(name + ' sleeve', [(0, -.12, 0), (0, end * .6, -.004), (0, end, 0)], [.085, .08, .076 if sleeve_end > .6 else .072], sleeve, seg=18, steps=4))
    if cuff:
        P.append(torus(name + ' cuff', (0, end, 0), .07, .018, cuff, rot=(90, 0, 0), seg=18, mseg=8))
    # chunky mitt: palm block + knuckle bump + thumb
    P.append(sphere(name + ' mitt', (0, L, .0), (.078, .085, .068), mitt_m, 20, 12))
    P.append(sphere(name + ' knuckles', (0, L + .045, .012), (.07, .045, .055), mitt_m, 16, 10))
    P.append(sphere(name + ' thumb', (-s * .06, L - .01, .025), (.032, .05, .032), mitt_m, 12, 8))
    o = join(name, P)
    return o

objs = []
for outfit, (sleeve, send, skin_m, mitt, cuff) in OUTFITS.items():
    sm = mat(*sleeve); km = mat(*skin_m); mm = mat(*mitt) if mitt else km; cm = mat(*cuff) if cuff else None
    for side in ('L', 'R'):
        objs.append(arm(f'FP_{outfit}_{side}', side, sm, send, km, mm, cm))

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'FirstPersonArms.blend'))
export_fbx(os.path.join(OUT, 'FirstPersonArms.fbx'), objs)   # static: baked axis conversion
write_palette(os.path.join(OUT, 'BeanPalette.json'))
print('FP_ARMS_EXPORTED', [o.name for o in objs])
