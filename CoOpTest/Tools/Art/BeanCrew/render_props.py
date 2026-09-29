import bpy, math, sys
from mathutils import Vector
bpy.ops.wm.open_mainfile(filepath='out/BeanProps.blend')
s=bpy.context.scene
layout={'Workbench':(-3.2,1.5,.5),'SlotTray':(-3.2,1.2,1.08),'BeanCrate':(-1.2,-1,.275),'PowerCell':(-.5,-1.2,.3),'Wrench':(0,-1.3,.09),
 'Generator':(1.6,1.2,.6),'BatteryDock':(1.3,-.2,.85),'WorkshopDoor':(4.4,2.2,1.25),'BigButton':(3.2,-.6,1.0),'PendantLamp':(-1.2,.6,2.4),
 'TrafficCone':(-2.2,-1.4,0),'OilDrum':(4.5,-.4,0),'PalletStack':(-4.6,-1,0),'Pegboard':(0,2.6,1.5),'PendantLampGlow':(99,99,99)}
for n,p in layout.items():
    o=bpy.data.objects['Prop_'+n]; o.location=p; o.hide_render=False
s.render.engine='CYCLES'; s.cycles.samples=32; s.cycles.use_denoising=True
s.render.resolution_x=1400; s.render.resolution_y=800
w=bpy.data.worlds.new('w'); s.world=w; w.use_nodes=True; w.node_tree.nodes['Background'].inputs[0].default_value=(.55,.62,.7,1)
cam=bpy.data.objects.new('cam',bpy.data.cameras.new('cam')); s.collection.objects.link(cam); s.camera=cam
cam.location=(0,-10.5,5.2); cam.data.lens=35
cam.rotation_euler=(Vector((0,.6,.7))-cam.location).to_track_quat('-Z','Y').to_euler()
sun=bpy.data.objects.new('sun',bpy.data.lights.new('sun','SUN')); s.collection.objects.link(sun); sun.data.energy=3.5; sun.rotation_euler=(math.radians(50),0,math.radians(30))
import bmesh
me=bpy.data.meshes.new('f'); bm=bmesh.new(); bmesh.ops.create_grid(bm,x_segments=1,y_segments=1,size=12); bm.to_mesh(me)
f=bpy.data.objects.new('f',me); s.collection.objects.link(f)
m=bpy.data.materials.new('fm'); m.use_nodes=True; m.node_tree.nodes['Principled BSDF'].inputs[0].default_value=(.3,.32,.34,1); me.materials.append(m)
s.render.filepath='/home/claude/art/props_sheet.png'; bpy.ops.render.render(write_still=True)
