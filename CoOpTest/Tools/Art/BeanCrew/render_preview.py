"""Render contact sheets of the Bean Crew: python3 render_preview.py BLEND OUTDIR [variant ...]"""
import sys, os, math, bpy
from mathutils import Vector

blend, out = sys.argv[1], sys.argv[2]
shots = sys.argv[3].split(',') if len(sys.argv) > 3 else ['Idle:0']
variants = sys.argv[4].split(',') if len(sys.argv) > 4 else ['Bolt', 'Gus', 'Pip', 'Dot']
view = sys.argv[5] if len(sys.argv) > 5 else 'front'
bpy.ops.wm.open_mainfile(filepath=blend)
os.makedirs(out, exist_ok=True)
s = bpy.context.scene
s.render.engine = 'CYCLES'
s.cycles.device = 'CPU'
s.cycles.samples = 24
s.cycles.use_denoising = True
s.render.resolution_x = 420; s.render.resolution_y = 520
s.render.film_transparent = False
w = bpy.data.worlds.new('w'); s.world = w; w.use_nodes = True
w.node_tree.nodes['Background'].inputs[0].default_value = (.55, .62, .7, 1)
w.node_tree.nodes['Background'].inputs[1].default_value = .9
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); s.collection.objects.link(cam); s.camera = cam
cam.data.lens = float(os.environ.get("LENS", 50))
pos = {'front': (2.0, -4.2, 1.6), 'side': (4.6, -.3, 1.3), 'back': (-2.0, 4.2, 1.8)}[view]
cam.location = pos
d = Vector((0, 0, float(os.environ.get("TZ", .95)))) - cam.location
cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
sun = bpy.data.objects.new('sun', bpy.data.lights.new('sun', 'SUN')); s.collection.objects.link(sun)
sun.data.energy = 3.5; sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(35))
fill = bpy.data.objects.new('fill', bpy.data.lights.new('fill', 'AREA')); s.collection.objects.link(fill)
fill.data.energy = 250; fill.data.size = 4; fill.location = (-3, -3, 3); fill.rotation_euler = (math.radians(50), 0, math.radians(-45))
import bmesh
me = bpy.data.meshes.new('floor'); bm = bmesh.new(); bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=6); bm.to_mesh(me)
fl = bpy.data.objects.new('floor', me); s.collection.objects.link(fl)
fm = bpy.data.materials.new('floor'); fm.use_nodes = True; fm.node_tree.nodes['Principled BSDF'].inputs[0].default_value = (.35, .38, .4, 1); me.materials.append(fm)
rig = bpy.data.objects['BeanRig']
for obj in bpy.data.objects:
    if obj.name.startswith('Crew_') or obj.name.startswith('Prop_'):
        obj.hide_render = True
for v in variants:
    bpy.data.objects['Crew_' + v].hide_render = False
    for shot in shots:
        act, frame = shot.split(':')
        rig.animation_data.action = bpy.data.actions[act]
        s.frame_set(int(frame))
        s.render.filepath = os.path.join(out, f'{v}_{act.replace(" ", "")}_{frame}_{view}.png')
        bpy.ops.render.render(write_still=True)
    bpy.data.objects['Crew_' + v].hide_render = True
print('RENDERED')
