import bpy, math, os
from mathutils import Vector

source='D:/WorkTogetherCoOp/CoOpTest/Assets/CoopPrototype/Art/BeanCrew/Models/BeanCrew.fbx'
output='D:/WorkTogetherCoOp/CoOpTest/Assets/CoopPrototype/Art/Planet/PlanetCrew.fbx'
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=source)
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
for obj in list(bpy.data.objects):
    if obj!=rig: bpy.data.objects.remove(obj,do_unlink=True)
rig.animation_data_clear()
def mat(name,color,rough=.4):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
    b=m.node_tree.nodes.get('Principled BSDF');b.inputs['Base Color'].default_value=(*color,1);b.inputs['Roughness'].default_value=rough
    return m
ivory=mat('Planet Ivory',(.87,.83,.74));rubber=mat('Planet Rubber',(.055,.049,.07));visor=mat('Planet Visor',(.013,.024,.045),.12);orange=mat('Planet Orange',(1,.3,.016));white=mat('Planet Glint',(.9,.94,1),.18)
def ellipsoid(parts,name,loc,scale,material,bone):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24,ring_count=16,location=loc)
    o=bpy.context.object;o.name=name;o.scale=scale;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    o.data.materials.append(material)
    for p in o.data.polygons:p.use_smooth=True
    g=o.vertex_groups.new(name=bone);g.add(list(range(len(o.data.vertices))),1,'REPLACE');parts.append(o);return o
def box(parts,name,loc,size,material,bone,rot=(0,0,0)):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.name=name;o.scale=size;o.rotation_euler=rot
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(material)
    b=o.modifiers.new('Soft molded corners','BEVEL');b.width=min(size)*.15;b.segments=3;bpy.ops.object.modifier_apply(modifier=b.name)
    o.modifiers.new('Weighted normals','WEIGHTED_NORMAL');bpy.ops.object.modifier_apply(modifier=o.modifiers[-1].name)
    g=o.vertex_groups.new(name=bone);g.add(list(range(len(o.data.vertices))),1,'REPLACE');parts.append(o);return o
crew=[]
for idx,(name,color) in enumerate([('Bolt',(.1,.37,.88)),('Gus',(1,.68,.012)),('Pip',(.85,.09,.06)),('Dot',(.35,.73,.06))]):
    suit=mat('Planet Suit '+name,color);parts=[]
    ellipsoid(parts,'Suit belly',(0,0,.78),(.285,.215,.335),suit,'Spine')
    ellipsoid(parts,'Suit chest',(0,0,1.02),(.27,.19,.16),suit,'Chest')
    ellipsoid(parts,'Hips',(0,0,.53),(.265,.195,.15),suit,'Hips')
    ellipsoid(parts,'Collar',(0,0,1.12),(.23,.18,.065),rubber,'Chest')
    ellipsoid(parts,'Porcelain helmet',(0,0,1.4),(.32,.285,.33),ivory,'Head')
    ellipsoid(parts,'Glossy black visor',(0,-.231,1.43),(.25,.097,.225),visor,'Head')
    ellipsoid(parts,'Helmet chin',(0,-.18,1.19),(.25,.16,.053),ivory,'Head')
    ellipsoid(parts,'Visor reflection',(-.12,-.320,1.55),(.027,.009,.045),white,'Head')
    for side,label in [(1,'L'),(-1,'R')]:
        box(parts,'Helmet module',(side*.31,0,1.4),(.065,.12,.13),orange,'Head')
        ellipsoid(parts,'Upper arm',(side*.30,0,.95),(.12,.13,.19),suit,'UpperArm'+label)
        ellipsoid(parts,'Forearm',(side*.345,-.005,.73),(.107,.112,.145),suit,'Forearm'+label)
        ellipsoid(parts,'White wrist cuff',(side*.357,-.008,.655),(.113,.12,.064),ivory,'Forearm'+label)
        ellipsoid(parts,'Glove',(side*.364,-.02,.555),(.105,.106,.13),rubber,'Hand'+label)
        ellipsoid(parts,'Thumb',(side*.285,-.07,.57),(.052,.06,.075),rubber,'Hand'+label)
        ellipsoid(parts,'Thigh',(side*.13,0,.41),(.135,.155,.14),suit,'Thigh'+label)
        ellipsoid(parts,'Shin',(side*.13,0,.205),(.125,.14,.135),suit,'Shin'+label)
        ellipsoid(parts,'White boot cuff',(side*.13,0,.145),(.131,.147,.06),ivory,'Shin'+label)
        ellipsoid(parts,'Black boot',(side*.13,-.055,.069),(.139,.20,.08),rubber,'Foot'+label)
        box(parts,'Boot sole',(side*.13,-.054,.025),(.279,.40,.05),rubber,'Foot'+label)
        box(parts,'Chest strap',(side*.18,-.172,.98),(.067,.047,.22),rubber,'Chest')
        box(parts,'Strap buckle',(side*.18,-.205,1.0),(.083,.04,.065),ivory,'Chest')
    box(parts,'Belt',(0,-.193,.82),(.55,.044,.09),rubber,'Spine')
    box(parts,'Centre buckle',(0,-.23,.83),(.13,.06,.125),ivory,'Spine')
    box(parts,'Life support housing',(0,.232,.94),(.35,.16,.4),rubber,'Chest')
    box(parts,'Backpack shell',(0,.335,.95),(.30,.095,.34),ivory,'Chest')
    box(parts,'Backpack orange panel',(0,.393,1.03),(.12,.027,.064),orange,'Chest')
    ellipsoid(parts,'Backpack valve',(0,.39,.925),(.061,.02,.061),orange,'Chest')
    bpy.ops.object.select_all(action='DESELECT')
    for o in parts:o.select_set(True)
    bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();o=bpy.context.object;o.name='Crew_'+name
    # Bake mesh in the rig's export frame so it shares the existing animation skeleton.
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    o.parent=rig;modifier=o.modifiers.new('Planet crew skeleton','ARMATURE');modifier.object=rig
    crew.append(o)
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
for o in crew:o.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=output,use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True)
print('PLANET_CREW_EXPORTED',output)
