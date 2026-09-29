"""Shared mesh helpers for the Bean Crew art pipeline (run with Blender's bpy)."""
import bpy, bmesh, math, os
from mathutils import Vector, Matrix, Quaternion, Euler

MATS = {}
PALETTE = {}

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    s = bpy.context.scene
    s.unit_settings.system = 'METRIC'
    s.unit_settings.scale_length = 1.0
    s.render.fps = 30

def mat(name, rgb, rough=.75, metal=0.0, emit=None):
    if name in MATS:
        return MATS[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes.get('Principled BSDF')
    b.inputs['Base Color'].default_value = (*srgb(rgb), 1)
    b.inputs['Roughness'].default_value = rough
    b.inputs['Metallic'].default_value = metal
    if emit:
        b.inputs['Emission Color'].default_value = (*srgb(emit), 1)
        b.inputs['Emission Strength'].default_value = 2.0
    m.diffuse_color = (*srgb(rgb), 1)
    MATS[name] = m
    PALETTE[name] = {'name': name, 'rgb': list(rgb), 'roughness': rough, 'metallic': metal, 'emission': list(emit) if emit else []}
    return m

def srgb(rgb):
    """Accept 0-255 sRGB, return linear."""
    def f(c):
        c = c / 255.0
        return c / 12.92 if c <= .04045 else ((c + .055) / 1.055) ** 2.4
    return tuple(f(c) for c in rgb)

def link(obj):
    bpy.context.scene.collection.objects.link(obj)
    return obj

def from_data(name, verts, faces, material, smooth=True):
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v) for v in verts], [], faces)
    me.validate()
    me.update()
    o = link(bpy.data.objects.new(name, me))
    o.data.materials.append(material)
    for p in me.polygons:
        p.use_smooth = smooth
    return o

def _frame(d):
    d = d.normalized()
    up = Vector((0, 0, 1)) if abs(d.z) < .95 else Vector((0, 1, 0))
    a = d.cross(up).normalized()
    b = d.cross(a).normalized()
    return a, b

def loft(name, rings, material, seg=20, cap0=True, cap1=True, smooth=True):
    """rings: list of (center, rx, ry). Cross sections perpendicular to the path."""
    verts, faces = [], []
    pts = [Vector(r[0]) for r in rings]
    n = len(rings)
    for i, (c, rx, ry) in enumerate(rings):
        c = Vector(c)
        if i == 0: d = pts[1] - pts[0]
        elif i == n - 1: d = pts[-1] - pts[-2]
        else: d = pts[i + 1] - pts[i - 1]
        a, b = _frame(d)
        for k in range(seg):
            t = 2 * math.pi * k / seg
            verts.append(c + a * (math.cos(t) * rx) + b * (math.sin(t) * ry))
    for i in range(n - 1):
        for k in range(seg):
            k2 = (k + 1) % seg
            faces.append((i * seg + k, i * seg + k2, (i + 1) * seg + k2, (i + 1) * seg + k))
    if cap0:
        verts.append(pts[0]); ci = len(verts) - 1
        for k in range(seg):
            faces.append((ci, (k + 1) % seg, k))
    if cap1:
        verts.append(pts[-1]); ci = len(verts) - 1
        base = (n - 1) * seg
        for k in range(seg):
            faces.append((ci, base + k, base + (k + 1) % seg))
    return from_data(name, verts, faces, material, smooth)

def tube(name, points, radii, material, seg=14, steps=5, round_ends=True):
    """Rounded tube through points with per-point radii (linear interpolation)."""
    pts = [Vector(p) for p in points]
    rings = []
    r0 = radii[0]
    if round_ends:
        d = (pts[0] - pts[1]).normalized()
        for j in range(4, 0, -1):
            ang = j / 4 * math.pi / 2
            rings.append((pts[0] + d * r0 * math.sin(ang), r0 * math.cos(ang) + .001, r0 * math.cos(ang) + .001))
    for i in range(len(pts) - 1):
        for s in range(steps):
            t = s / steps
            rings.append((pts[i].lerp(pts[i + 1], t), radii[i] + (radii[i + 1] - radii[i]) * t, radii[i] + (radii[i + 1] - radii[i]) * t))
    rings.append((pts[-1], radii[-1], radii[-1]))
    if round_ends:
        d = (pts[-1] - pts[-2]).normalized(); r1 = radii[-1]
        for j in range(1, 5):
            ang = j / 4 * math.pi / 2
            rings.append((pts[-1] + d * r1 * math.sin(ang), r1 * math.cos(ang) + .001, r1 * math.cos(ang) + .001))
    return loft(name, rings, material, seg)

def sphere(name, pos, size, material, seg=24, rings=14, flat_bottom=None, smooth=True):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=1)
    for v in bm.verts:
        v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
        if flat_bottom is not None and v.co.z < flat_bottom:
            v.co.z = flat_bottom
        v.co += Vector(pos)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = link(bpy.data.objects.new(name, me)); o.data.materials.append(material)
    for p in me.polygons: p.use_smooth = smooth
    return o

def box(name, pos, size, material, bevel=.02, rot=(0, 0, 0), seg=2):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1)
    for v in bm.verts:
        v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=bm.edges[:] + bm.verts[:], offset=bevel, segments=seg, affect='EDGES', profile=.5)
    R = Euler([math.radians(a) for a in rot]).to_matrix()
    for v in bm.verts:
        v.co = R @ v.co + Vector(pos)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = link(bpy.data.objects.new(name, me)); o.data.materials.append(material)
    for p in me.polygons: p.use_smooth = bevel > 0
    if bevel > 0: flat_shade_sharp(o)
    return o

def cyl(name, pos, radius, depth, material, axis='Z', seg=24, bevel=.01, rot=None, r2=None, smooth_sides=True):
    """Cylinder (optionally tapered to r2) centred at pos along an axis."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg, radius1=radius, radius2=radius if r2 is None else r2, depth=depth)
    if bevel > 0:
        edges = [e for e in bm.edges if abs(e.verts[0].co.z - e.verts[1].co.z) < 1e-5 and abs(abs(e.verts[0].co.z) - depth / 2) < 1e-5]
        bmesh.ops.bevel(bm, geom=edges, offset=bevel, segments=2, affect='EDGES', profile=.5)
    M = {'Z': Matrix(), 'X': Matrix.Rotation(math.pi / 2, 3, 'Y'), 'Y': Matrix.Rotation(math.pi / 2, 3, 'X')}[axis]
    if rot: M = Euler([math.radians(a) for a in rot]).to_matrix() @ M
    for v in bm.verts: v.co = M.to_3x3() @ v.co + Vector(pos)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = link(bpy.data.objects.new(name, me)); o.data.materials.append(material)
    for p in me.polygons:
        n = p.normal
        p.use_smooth = smooth_sides
    flat_shade_sharp(o)
    return o

def torus(name, pos, major, minor, material, rot=(0, 0, 0), seg=28, mseg=10, arc=1.0, scale=(1, 1, 1)):
    verts, faces = [], []
    closed = arc >= .999
    U = seg if closed else seg + 1
    for i in range(U):
        u = 2 * math.pi * arc * i / seg
        for j in range(mseg):
            v = 2 * math.pi * j / mseg
            x = (major + minor * math.cos(v)) * math.cos(u)
            y = (major + minor * math.cos(v)) * math.sin(u)
            z = minor * math.sin(v)
            verts.append(Vector((x * scale[0], y * scale[1], z * scale[2])))
    for i in range(seg if closed else seg):
        i2 = (i + 1) % U
        for j in range(mseg):
            j2 = (j + 1) % mseg
            faces.append((i * mseg + j, i2 * mseg + j, i2 * mseg + j2, i * mseg + j2))
    R = Euler([math.radians(a) for a in rot]).to_matrix()
    verts = [R @ v + Vector(pos) for v in verts]
    o = from_data(name, verts, faces, material)
    if not closed:
        # cap open ends with small spheres
        pass
    return o

def add_autosmooth(o, angle=40):
    try:
        mod = o.modifiers.new('Smooth', 'SMOOTH_BY_ANGLE')
    except Exception:
        return
    # Node-group based modifier may not exist in bpy module; ignore silently.

def apply_all(o):
    bpy.context.view_layer.objects.active = o
    for m in list(o.modifiers):
        try:
            bpy.ops.object.modifier_apply(modifier=m.name)
        except Exception:
            o.modifiers.remove(m)

def join(name, parts):
    bpy.ops.object.select_all(action='DESELECT')
    for p in parts:
        apply_all(p)
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    o = bpy.context.object
    o.name = name; o.data.name = name
    return o

def flat_shade_sharp(o, angle=35):
    """Split normals at hard edges without relying on modifiers (works in bpy module)."""
    me = o.data
    bm = bmesh.new(); bm.from_mesh(me)
    thr = math.radians(angle)
    for e in bm.edges:
        if len(e.link_faces) == 2 and e.calc_face_angle(0) > thr:
            e.smooth = False
    bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = True

def export_fbx(path, objects, anim=False, bake_space=None):
    if bake_space is None: bake_space = not anim  # static props: bake Z-up->Y-up into vertices so the root has no rotation
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects: o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'MESH', 'ARMATURE', 'EMPTY'},
        add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
        axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
        bake_space_transform=bake_space, use_armature_deform_only=False, mesh_smooth_type='FACE',
        bake_anim=anim, bake_anim_use_all_actions=anim, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0, bake_anim_step=1.0,
        use_mesh_modifiers=True, path_mode='STRIP')

def write_palette(path):
    import json
    old = {}
    if os.path.exists(path):
        old = {m['name']: m for m in json.load(open(path))['materials']}
    old.update(PALETTE)
    json.dump({'materials': sorted(old.values(), key=lambda m: m['name'])}, open(path, 'w'), indent=1)
