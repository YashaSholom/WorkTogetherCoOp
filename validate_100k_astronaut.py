import bpy
from mathutils import Vector

source_glb = r"C:\Users\Yasha\Documents\ComfyUI-Trellis2\output\3d\ComfyUI_00013.glb"
output_png = r"D:\WorkTogetherCoOp\outputs\astronaut_100k_validation.png"

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=source_glb)
meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
triangle_count = sum(sum(len(poly.vertices) - 2 for poly in obj.data.polygons) for obj in meshes)
vertex_count = sum(len(obj.data.vertices) for obj in meshes)
points = [obj.matrix_world @ Vector(corner) for obj in meshes for corner in obj.bound_box]
low = Vector((min(point[i] for point in points) for i in range(3)))
high = Vector((max(point[i] for point in points) for i in range(3)))
center = (low + high) / 2
extent = high - low

def look_at(obj, target):
    obj.rotation_euler = (target - obj.location).to_track_quat('-Z', 'Y').to_euler()

bpy.ops.object.camera_add()
camera = bpy.context.object
camera.location = center + Vector((extent.x * 1.65, -extent.y * 2.6, extent.z * 0.55))
camera.data.lens = 55
look_at(camera, center + Vector((0, 0, extent.z * 0.04)))
bpy.context.scene.camera = camera
for offset, energy in ((Vector((1.5, -1.6, 2.0)), 900), (Vector((-1.1, -1.0, 0.8)), 400)):
    bpy.ops.object.light_add(type='AREA', location=center + Vector((extent.x * offset.x, extent.y * offset.y, extent.z * offset.z)))
    light = bpy.context.object
    light.data.energy = energy
    light.data.size = max(extent) * 1.2
    look_at(light, center)
world = bpy.data.worlds.new('World')
bpy.context.scene.world = world
world.use_nodes = True
world.node_tree.nodes['Background'].inputs['Color'].default_value = (0.015, 0.02, 0.03, 1)
world.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.2
scene = bpy.context.scene
scene.render.engine = 'BLENDER_EEVEE'
scene.render.resolution_x = 1024
scene.render.resolution_y = 1024
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.filepath = output_png
scene.view_settings.look = 'AgX - Medium High Contrast'
scene.view_settings.exposure = -0.9
print('MESHES', len(meshes), 'TRIANGLES', triangle_count, 'VERTICES', vertex_count)
bpy.ops.render.render(write_still=True)
