import bpy
from mathutils import Vector

source_glb = r"C:\Users\Yasha\Documents\ComfyUI-Trellis2\output\3d\ComfyUI_00010.glb"
output_png = r"D:\WorkTogetherCoOp\portal_validation.png"

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=source_glb)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
points = [o.matrix_world @ Vector(corner) for o in meshes for corner in o.bound_box]
low = Vector((min(p[i] for p in points) for i in range(3)))
high = Vector((max(p[i] for p in points) for i in range(3)))
center = (low + high) / 2
extent = high - low

def point_at(obj, point):
    obj.rotation_euler = (point - obj.location).to_track_quat('-Z', 'Y').to_euler()

bpy.ops.object.camera_add()
camera = bpy.context.object
camera.location = center + Vector((extent.x * 1.6, -extent.y * 2.5, extent.z * 0.7))
camera.data.lens = 55
point_at(camera, center)
bpy.context.scene.camera = camera

for offset, energy in ((Vector((1.4,-1.8,2.2)), 850), (Vector((-1.1,-1.2,0.8)), 420)):
    bpy.ops.object.light_add(type='AREA', location=center + Vector((extent.x*offset.x, extent.y*offset.y, extent.z*offset.z)))
    light = bpy.context.object
    light.data.energy = energy
    light.data.shape = 'DISK'
    light.data.size = max(extent) * 1.4
    point_at(light, center)

world = bpy.context.scene.world or bpy.data.worlds.new('World')
bpy.context.scene.world = world
world.use_nodes = True
world.node_tree.nodes['Background'].inputs['Color'].default_value = (0.015, 0.02, 0.03, 1)
world.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.25
scene = bpy.context.scene
scene.render.engine = 'BLENDER_EEVEE'
scene.render.resolution_x = 1024
scene.render.resolution_y = 1024
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.filepath = output_png
scene.view_settings.look = 'AgX - Medium High Contrast'
scene.view_settings.exposure = -0.8
print('MESH_COUNT', len(meshes), 'BOUNDS', tuple(round(x,3) for x in low), tuple(round(x,3) for x in high))
bpy.ops.render.render(write_still=True)
