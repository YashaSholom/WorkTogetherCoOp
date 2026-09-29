"""Editable low-poly workshop art. Run with Blender --background --python this_file.
Source is saved outside Assets so Unity does not need Blender to import the FBX files.
"""
import bpy, math, os
from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '../..'))
OUT = os.path.join(ROOT, 'Assets/CoopPrototype/Art/Models')
os.makedirs(OUT, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

def mat(name, rgb, metal=0):
    m=bpy.data.materials.new(name); m.diffuse_color=(*rgb,1); m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=(*rgb,1)
    p.inputs['Roughness'].default_value=.72; p.inputs['Metallic'].default_value=metal
    return m
skin=mat('Warm skin',(.72,.40,.23)); nose=mat('Rosy nose',(.78,.28,.17))
teal=mat('Petrol workwear',(.055,.26,.29)); navy=mat('Deep blue trousers',(.065,.095,.14))
ochre=mat('Mustard canvas',(.85,.49,.10)); boot=mat('Rubber charcoal',(.045,.055,.065))
cream=mat('Ivory',(.92,.85,.67)); black=mat('Ink',(.015,.023,.025))
wood=mat('Honey timber',(.53,.27,.10)); wood2=mat('Timber edge',(.29,.12,.043))
steel=mat('Worn steel',(.31,.38,.40),.5); red=mat('Signal red',(.72,.12,.07))
green=mat('Battery enamel',(.16,.36,.23)); orange=mat('Generator enamel',(.82,.30,.075))
parts=[]
def finish(o,name,material,bone=None):
    o.name=name; o.data.materials.append(material)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bone:
        g=o.vertex_groups.new(name=bone); g.add(list(range(len(o.data.vertices))),1,'REPLACE')
    parts.append(o); return o
def box(name,pos,size,material,bone=None,bevel=.035):
    bpy.ops.mesh.primitive_cube_add(size=1,location=pos); o=bpy.context.object; o.scale=size
    finish(o,name,material,bone)
    if bevel:
        mod=o.modifiers.new('Soft manufactured edges','BEVEL'); mod.width=bevel; mod.segments=1
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return o
def ell(name,pos,size,material,bone=None):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=8, radius=1, location=pos)
    o=bpy.context.object; o.scale=size; return finish(o,name,material,bone)
def cyl(name,pos,radius,depth,material,bone=None,axis=None):
    bpy.ops.mesh.primitive_cylinder_add(vertices=12,radius=radius,depth=depth,location=pos)
    o=bpy.context.object
    if axis: o.rotation_euler=Vector(axis).to_track_quat('Z','Y').to_euler()
    return finish(o,name,material,bone)
def bar(name,a,b,r,material,bone=None):
    a,b=Vector(a),Vector(b); return cyl(name,(a+b)/2,r,(b-a).length,material,bone,b-a)
def export(name,objects):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects: o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,name+'.fbx'),use_selection=True,
        object_types={'MESH','ARMATURE','EMPTY'},add_leaf_bones=False,bake_anim=False,
        axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS')

# Blender +Z is up; -Y is the character's forward direction.
parts=[]
ell('Jacket belly',(0,0,1.00),(.34,.22,.37),teal,'Spine')
box('Bib pocket',(0,-.213,1.06),(.28,.055,.24),ochre,'Spine',.025)
box('Pocket seam',(0,-.25,1.13),(.245,.012,.018),wood2,'Spine',.004)
for x in [-.21,.21]:
    box('Overall strap',(x,-.17,1.25),(.065,.07,.30),ochre,'Spine',.012)
    ell('Brass button',(x,-.215,1.16),(.03,.016,.03),cream,'Spine')
ell('Seat',(0,.015,.72),(.28,.20,.18),navy,'Hips')
ell('Neck',(0,0,1.39),(.105,.095,.14),skin,'Head')
ell('Big head',(0,-.015,1.64),(.285,.23,.285),skin,'Head')
ell('Nose',(0,-.26,1.61),(.115,.135,.09),nose,'Head')
for x in [-.29,.29]: ell('Ear',(x,0,1.63),(.065,.05,.10),skin,'Head')
for x in [-.11,.11]:
    ell('Eye white',(x,-.214,1.71),(.075,.033,.060),cream,'Head')
    ell('Dot pupil',(x-.013,-.244,1.707),(.023,.016,.031),black,'Head')
    brow=box('Sleepy brow',(x,-.236,1.786),(.13,.025,.032),wood2,'Head',.01)
    brow.rotation_euler.y=math.radians(10 if x<0 else -17)
box('Crooked moustache',(0,-.234,1.51),(.19,.040,.048),wood2,'Head',.02)
ell('Lower lip',(.02,-.211,1.467),(.061,.03,.023),nose,'Head')
ell('Beanie crown',(0,.015,1.84),(.29,.235,.14),ochre,'Head')
box('Cap brim',(0,-.215,1.829),(.39,.27,.045),ochre,'Head',.025)
box('Cap patch',(.075,-.20,1.879),(.11,.035,.08),cream,'Head',.015)
for s,suffix in [(-1,'L'),(1,'R')]:
    x=s*.16
    ell('Trouser thigh',(x,0,.54),(.135,.14,.24),navy,'Thigh'+suffix)
    ell('Trouser shin',(x,0,.275),(.105,.105,.19),navy,'Shin'+suffix)
    box('Oversized boot',(x,-.085,.105),(.255,.38,.19),boot,'Foot'+suffix,.055)
    box('Boot sole',(x,-.085,.035),(.27,.40,.07),wood2,'Foot'+suffix,.025)
    ell('Rolled sleeve',(s*.365,0,1.17),(.145,.145,.18),teal,'UpperArm'+suffix)
    ell('Forearm',(s*.405,-.012,.95),(.087,.09,.15),skin,'Forearm'+suffix)
    ell('Mitten hand',(s*.415,-.025,.785),(.105,.09,.12),skin,'Hand'+suffix)
    ell('Thumb',(s*.345,-.07,.80),(.055,.058,.072),skin,'Hand'+suffix)

bpy.ops.object.armature_add(); rig=bpy.context.object; rig.name='WorkerRig'
bpy.ops.object.mode_set(mode='EDIT'); rig.data.edit_bones.remove(rig.data.edit_bones[0])
def bone(name,head,tail,parent=None):
    b=rig.data.edit_bones.new(name); b.head=head; b.tail=tail
    if parent: b.parent=rig.data.edit_bones[parent]
bone('Hips',(0,0,.7),(0,0,.85))
bone('Spine',(0,0,.85),(0,0,1.35),'Hips')
bone('Head',(0,0,1.35),(0,0,1.85),'Spine')
for s,suffix in [(-1,'L'),(1,'R')]:
    bone('Thigh'+suffix,(s*.16,0,.71),(s*.16,0,.39),'Hips')
    bone('Shin'+suffix,(s*.16,0,.39),(s*.16,0,.13),'Thigh'+suffix)
    bone('Foot'+suffix,(s*.16,0,.13),(s*.16,-.22,.09),'Shin'+suffix)
    bone('UpperArm'+suffix,(s*.34,0,1.29),(s*.40,0,1.04),'Spine')
    bone('Forearm'+suffix,(s*.40,0,1.04),(s*.415,-.01,.85),'UpperArm'+suffix)
    bone('Hand'+suffix,(s*.415,-.01,.85),(s*.415,-.025,.72),'Forearm'+suffix)
bpy.ops.object.mode_set(mode='OBJECT')
# One skinned mesh with named material slots, simple rigid weights per articulated part.
bpy.ops.object.select_all(action='DESELECT')
for o in parts: o.select_set(True)
bpy.context.view_layer.objects.active=parts[0]; bpy.ops.object.join(); mesh=bpy.context.object; mesh.name='WorkerSkin'
mod=mesh.modifiers.new('Worker skeleton','ARMATURE'); mod.object=rig; mesh.parent=rig
export('WorkshopWorker',[rig,mesh]); character=[rig,mesh]

# Cargo crate: inset planks, raised corner battens, steel straps and nail heads.
parts=[]
for z in [-.21,-.105,0,.105,.21]:
    box('Front plank',(0,-.255,z),(.51,.045,.095),wood,bevel=.007)
    box('Back plank',(0,.255,z),(.51,.045,.095),wood,bevel=.007)
    box('Side plank',(-.255,0,z),(.045,.48,.095),wood,bevel=.007)
    box('Side plank',(.255,0,z),(.045,.48,.095),wood,bevel=.007)
box('Crate interior',(0,0,0),(.48,.48,.48),wood2,bevel=.01)
for x in [-.19,.19]:
    for y in [-.278,.278]:
        box('Corner batten',(x,y,0),(.065,.035,.55),wood2,bevel=.006)
        for z in [-.22,.22]: ell('Nail',(x,y*1.07,z),(.014,.012,.014),steel)
for x in [-.16,.16]: box('Lid strap',(x,0,.273),(.045,.54,.015),steel,bevel=.003)
export('CargoCrate',parts); crates=parts[:]

parts=[]
box('Battery case',(0,0,-.025),(.34,.33,.48),green,bevel=.03)
box('Battery top',(0,0,.23),(.36,.35,.06),boot,bevel=.012)
for x,m in [(-.105,black),(.105,red)]: cyl('Terminal',(x,0,.287),.04,.06,m)
for x in [-.12,.12]: bar('Handle support',(x,.065,.26),(x,.065,.35),.018,steel)
bar('Carry handle',(-.12,.065,.35),(.12,.065,.35),.025,boot)
box('Label',(0,-.171,0),(.24,.009,.25),cream,bevel=.006)
bolt=box('Lightning upper',(.025,-.18,.06),(.04,.015,.12),orange,bevel=.003); bolt.rotation_euler.y=-.4
bolt=box('Lightning lower',(-.015,-.18,-.04),(.04,.015,.12),orange,bevel=.003); bolt.rotation_euler.y=-.4
export('WorkshopBattery',parts); batteries=parts[:]

parts=[]
box('Wrench handle',(0,0,0),(.42,.065,.07),steel,bevel=.02)
box('Rubber grip',(-.055,0,0),(.22,.08,.085),teal,bevel=.025)
cyl('Wrench head',(.225,0,0),.105,.055,steel,axis=(0,1,0))
box('Open jaw upper',(.29,0,.085),(.12,.06,.06),steel,bevel=.012)
box('Open jaw lower',(.29,0,-.085),(.12,.06,.06),steel,bevel=.012)
export('Spanner',parts); tools=parts[:]

parts=[]
box('Generator tank',(0,0,.67),(1.48,.9,.57),orange,bevel=.13)
box('Engine block',(0,0,.28),(1.18,.72,.34),boot,bevel=.05)
for x in [-.84,.84]:
    for y in [-.52,.52]: bar('Cage upright',(x,y,.08),(x,y,1.12),.045,steel)
    for z in [.08,1.12]: bar('Cage rail',(x,-.52,z),(x,.52,z),.045,steel)
for y in [-.52,.52]:
    for z in [.08,1.12]: bar('Cage crossbar',(-.84,y,z),(.84,y,z),.045,steel)
for x in [-.52,-.39,-.26,-.13,0,.13]: box('Cooling vent',(x,-.462,.67),(.055,.016,.31),boot,bevel=.012)
cyl('Gauge',(.48,-.478,.76),.105,.025,cream,axis=(0,1,0))
bar('Gauge needle',(.48,-.50,.76),(.53,-.50,.80),.009,red)
cyl('Fuel cap',(.40,.12,.98),.075,.035,boot)
bar('Exhaust',(-.55,.29,.47),(-.55,.29,1.18),.06,steel)
export('PortableGenerator',parts); generator=parts[:]

parts=[]
for x in [-1.85,1.85]:
    for y in [-.60,.60]: box('Steel leg',(x,y,.49),(.08,.08,.98),teal,bevel=.008)
for z in [.15,1.0]:
    box('Shelf boards',(0,0,z),(4,1.5,.10),wood,bevel=.025)
    for y in [-.70,.70]: box('Shelf edging',(0,y,z),(4,.065,.14),steel,bevel=.012)
export('WorkshopBench',parts); bench=parts[:]

# Store every original mesh and skeleton in one editable source with named collections.
for name,objects in [('Worker',character),('Crate',crates),('Battery',batteries),('Spanner',tools),('Generator',generator),('Bench',bench)]:
    col=bpy.data.collections.new(name); bpy.context.scene.collection.children.link(col)
    for o in objects:
        for old in list(o.users_collection): old.objects.unlink(o)
        col.objects.link(o)
    if name!='Worker':
        for o in objects: o.hide_render=True; o.hide_set(True)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT,'Tools/Art/WorkshopArt.blend'))
print('WORKSHOP_ART_EXPORTED')
