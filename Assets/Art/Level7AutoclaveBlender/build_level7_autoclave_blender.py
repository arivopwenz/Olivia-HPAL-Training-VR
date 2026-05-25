import math
import os
import random

import bpy
from mathutils import Vector

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
SOURCE_DIR = os.path.abspath(os.path.join(SCRIPT_DIR, "..", "..", "..", "BlenderSources", "Level7AutoclaveBlender"))
FBX_PATH = os.path.join(SCRIPT_DIR, "level7_autoclave_industrial_uv.fbx")
GLB_PATH = os.path.join(SCRIPT_DIR, "level7_autoclave_industrial_uv.glb")
BLEND_PATH = os.path.join(SOURCE_DIR, "level7_autoclave_industrial_uv.blend")
ATLAS_PATH = os.path.join(SCRIPT_DIR, "level7_autoclave_uv_atlas.png")

PANELS = {
    "shell": (0, 0),
    "dark": (1, 0),
    "yellow": (2, 0),
    "orange": (3, 0),
    "concrete": (0, 1),
    "pipe": (1, 1),
    "steam": (2, 1),
    "acid": (3, 1),
    "slurry": (0, 2),
    "red": (1, 2),
    "green": (2, 2),
    "white": (3, 2),
    "brass": (0, 3),
    "glass": (1, 3),
    "black": (2, 3),
    "label": (3, 3),
}

ROOT_COLLECTION = None
ROOT_EMPTY = None
ATLAS_MATERIAL = None

SHELL_Z = 3.38
SHELL_R = 2.18
LINER_R = 1.98
LIQUID_Z = 2.60
LIQUID_R = 1.42
TOP_LIFT = 0.58
INTERNAL_DROP = 1.10
BAFFLE_R = 1.48


def clamp01(v):
    return max(0.0, min(1.0, v))


def panel_rect(name, pad=0.018):
    col, row = PANELS[name]
    cell = 0.25
    return (
        col * cell + pad,
        row * cell + pad,
        (col + 1) * cell - pad,
        (row + 1) * cell - pad,
    )


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()
    for block in list(bpy.data.meshes):
        if block.users == 0:
            bpy.data.meshes.remove(block)
    for block in list(bpy.data.materials):
        if block.users == 0:
            bpy.data.materials.remove(block)


def create_atlas():
    random.seed(707)
    size = 1024
    img = bpy.data.images.new("level7_autoclave_uv_atlas", size, size, alpha=True)
    pixels = [0.0] * (size * size * 4)
    base = {
        "shell": (0.46, 0.51, 0.49, 1.0),
        "dark": (0.055, 0.060, 0.058, 1.0),
        "yellow": (0.98, 0.68, 0.04, 1.0),
        "orange": (0.78, 0.30, 0.08, 1.0),
        "concrete": (0.42, 0.40, 0.36, 1.0),
        "pipe": (0.35, 0.42, 0.43, 1.0),
        "steam": (0.72, 0.75, 0.72, 0.94),
        "acid": (0.88, 0.74, 0.10, 1.0),
        "slurry": (0.36, 0.16, 0.46, 0.86),
        "red": (0.75, 0.04, 0.035, 1.0),
        "green": (0.05, 0.55, 0.22, 1.0),
        "white": (0.88, 0.88, 0.82, 1.0),
        "brass": (0.82, 0.55, 0.16, 1.0),
        "glass": (0.18, 0.57, 0.78, 0.78),
        "black": (0.012, 0.012, 0.012, 1.0),
        "label": (0.92, 0.86, 0.66, 1.0),
    }

    for name, (col, row) in PANELS.items():
        x0 = int(col * size / 4)
        y0 = int(row * size / 4)
        x1 = int((col + 1) * size / 4)
        y1 = int((row + 1) * size / 4)
        r, g, b, a = base[name]
        for y in range(y0, y1):
            for x in range(x0, x1):
                nx = (x - x0) / max(1, x1 - x0)
                ny = (y - y0) / max(1, y1 - y0)
                noise = (random.random() - 0.5) * 0.045
                rr, gg, bb = r + noise, g + noise, b + noise
                if name in {"shell", "pipe", "steam"}:
                    grain = math.sin(nx * math.tau * 10.0) * 0.014
                    rr += grain
                    gg += grain
                    bb += grain
                    if random.random() < 0.013:
                        rr *= 0.66
                        gg *= 0.66
                        bb *= 0.66
                elif name == "orange":
                    seam = 0.055 if (x - x0) % 84 < 4 or (y - y0) % 84 < 4 else 0.0
                    rr += seam
                    gg += seam * 0.4
                elif name == "yellow":
                    if ((x - x0 + y - y0) // 36) % 2 == 0:
                        rr, gg, bb = 0.08, 0.075, 0.055
                elif name == "acid":
                    pulse = math.sin((nx * 4.0 + ny * 1.8) * math.tau) * 0.04
                    rr += pulse
                    gg += pulse
                elif name == "slurry":
                    swirl = math.sin((nx * 4.8 + ny * 3.0) * math.tau) * 0.05
                    rr += swirl
                    bb += swirl * 1.25
                elif name == "concrete":
                    if random.random() < 0.085:
                        rr, gg, bb = 0.28, 0.27, 0.25
                elif name == "glass":
                    glint = 0.10 if (x - x0 + y - y0) % 64 < 7 else 0.0
                    rr += glint
                    gg += glint
                    bb += glint
                elif name == "label":
                    if (x - x0) % 58 < 3 or (y - y0) % 58 < 3:
                        rr, gg, bb = 0.18, 0.14, 0.08
                i = (y * size + x) * 4
                pixels[i:i + 4] = [clamp01(rr), clamp01(gg), clamp01(bb), a]

    img.pixels.foreach_set(pixels)
    img.filepath_raw = ATLAS_PATH
    img.file_format = "PNG"
    img.save()
    return img


def create_material(image):
    mat = bpy.data.materials.new("M_Level7_Autoclave_UVAtlas")
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
    tex.image = image
    if bsdf:
        mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        if "Alpha" in bsdf.inputs:
            mat.node_tree.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
        if "Metallic" in bsdf.inputs:
            bsdf.inputs["Metallic"].default_value = 0.24
        if "Roughness" in bsdf.inputs:
            bsdf.inputs["Roughness"].default_value = 0.52
    mat.diffuse_color = (0.55, 0.60, 0.60, 1.0)
    return mat


def create_root():
    global ROOT_COLLECTION, ROOT_EMPTY
    ROOT_COLLECTION = bpy.data.collections.new("Level7_Autoclave_Industrial_UV")
    bpy.context.scene.collection.children.link(ROOT_COLLECTION)
    ROOT_EMPTY = bpy.data.objects.new("L7_Autoclave_Industrial_Root", None)
    ROOT_EMPTY.empty_display_type = "PLAIN_AXES"
    ROOT_COLLECTION.objects.link(ROOT_EMPTY)


def put(obj):
    if ROOT_COLLECTION not in obj.users_collection:
        ROOT_COLLECTION.objects.link(obj)
    for col in list(obj.users_collection):
        if col != ROOT_COLLECTION:
            col.objects.unlink(obj)
    if ROOT_EMPTY is not None and obj != ROOT_EMPTY and obj.type not in {"CAMERA", "LIGHT"}:
        obj.parent = ROOT_EMPTY
    return obj

def empty(name, loc, rot=(0, 0, 0)):
    obj = bpy.data.objects.new(name, None)
    obj.empty_display_type = "PLAIN_AXES"
    obj.location = loc
    obj.rotation_euler = rot
    ROOT_COLLECTION.objects.link(obj)
    if ROOT_EMPTY is not None:
        obj.parent = ROOT_EMPTY
    return obj

def parent_keep_world(obj, parent):
    if obj is None or parent is None:
        return obj
    world = obj.matrix_world.copy()
    obj.parent = parent
    obj.matrix_parent_inverse.identity()
    obj.matrix_world = world
    return obj


def normalize_uv_to_rect(obj, rect_name):
    if obj.type != "MESH":
        return
    if not obj.data.uv_layers:
        obj.data.uv_layers.new(name="UVMap")
    uv_layer = obj.data.uv_layers.active
    u0, v0, u1, v1 = panel_rect(rect_name)
    coords = [loop.uv.copy() for loop in uv_layer.data]
    if not coords:
        return
    min_u = min(c.x for c in coords)
    max_u = max(c.x for c in coords)
    min_v = min(c.y for c in coords)
    max_v = max(c.y for c in coords)
    span_u = max(max_u - min_u, 0.0001)
    span_v = max(max_v - min_v, 0.0001)
    for loop in uv_layer.data:
        loop.uv.x = u0 + ((loop.uv.x - min_u) / span_u) * (u1 - u0)
        loop.uv.y = v0 + ((loop.uv.y - min_v) / span_v) * (v1 - v0)


def uv_project(obj, panel):
    if obj.type != "MESH":
        return
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    try:
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.018)
        bpy.ops.object.mode_set(mode="OBJECT")
    except Exception:
        try:
            bpy.ops.object.mode_set(mode="OBJECT")
        except Exception:
            pass
        if not obj.data.uv_layers:
            obj.data.uv_layers.new(name="UVMap")
        for loop in obj.data.uv_layers.active.data:
            loop.uv = (0.5, 0.5)
    normalize_uv_to_rect(obj, panel)


def finish_mesh(obj, panel, bevel=0.0, shade=True):
    put(obj)
    if ATLAS_MATERIAL and obj.type == "MESH":
        obj.data.materials.clear()
        obj.data.materials.append(ATLAS_MATERIAL)
    if obj.type == "MESH":
        if shade:
            for poly in obj.data.polygons:
                poly.use_smooth = True
        if bevel > 0:
            mod = obj.modifiers.new("small_industrial_bevel", "BEVEL")
            mod.width = bevel
            mod.segments = 2
            mod.harden_normals = True
        try:
            obj.modifiers.new("weighted_normals", "WEIGHTED_NORMAL")
        except Exception:
            pass
        uv_project(obj, panel)
    return obj


def box(name, loc, scale, panel, bevel=0.0, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = scale
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return finish_mesh(obj, panel, bevel=bevel)


def cylinder(name, loc, radius, depth, panel, vertices=32, rot=(0, 0, 0), bevel=0.0):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=loc, rotation=rot)
    obj = bpy.context.object
    obj.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return finish_mesh(obj, panel, bevel=bevel)

def cutaway_cylinder_shell(name, loc, radius, length, panel, vertices=88, x_steps=28, cut_min=math.radians(124), cut_max=math.radians(236), bevel=0.0):
    """Horizontal vessel shell with a removed front-side sector so internals stay visible."""
    verts = []
    faces = []
    visible_span = math.tau - (cut_max - cut_min)
    for ix in range(x_steps + 1):
        x = loc[0] - length * 0.5 + length * ix / x_steps
        for ia in range(vertices + 1):
            a = cut_max + visible_span * ia / vertices
            y = loc[1] + math.cos(a) * radius
            z = loc[2] + math.sin(a) * radius
            verts.append((x, y, z))
    row = vertices + 1
    for ix in range(x_steps):
        for ia in range(vertices):
            a = ix * row + ia
            b = ix * row + ia + 1
            c = (ix + 1) * row + ia + 1
            d = (ix + 1) * row + ia
            faces.append((a, b, c, d))
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    return finish_mesh(obj, panel, bevel=bevel)


def sphere(name, loc, radius, panel, segments=24, rings=12, scale=(1, 1, 1), rot=(0, 0, 0), bevel=0.0):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=radius, location=loc, rotation=rot)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return finish_mesh(obj, panel, bevel=bevel)


def torus(name, loc, major, minor, panel, major_segments=48, minor_segments=10, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(
        major_radius=major,
        minor_radius=minor,
        major_segments=major_segments,
        minor_segments=minor_segments,
        location=loc,
        rotation=rot,
    )
    obj = bpy.context.object
    obj.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return finish_mesh(obj, panel)


def cylinder_between(name, start, end, radius, panel, vertices=20, bevel=0.0):
    a = Vector(start)
    b = Vector(end)
    direction = b - a
    length = direction.length
    if length <= 0.0001:
        return None
    quat = direction.to_track_quat("Z", "Y")
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=length, location=(a + b) * 0.5, rotation=quat.to_euler())
    obj = bpy.context.object
    obj.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return finish_mesh(obj, panel, bevel=bevel)


def half_sphere_cap(name, loc, radius, panel, side, segments=48, rings=12):
    verts = []
    faces = []
    for ix in range(rings + 1):
        phi = (math.pi * 0.5) * (ix / rings)
        x = side * math.sin(phi) * radius
        ring_r = math.cos(phi) * radius
        for j in range(segments):
            a = math.tau * j / segments
            verts.append((loc[0] + x, loc[1] + math.cos(a) * ring_r, loc[2] + math.sin(a) * ring_r))
    for ix in range(rings):
        for j in range(segments):
            a = ix * segments + j
            b = ix * segments + (j + 1) % segments
            c = (ix + 1) * segments + (j + 1) % segments
            d = (ix + 1) * segments + j
            faces.append((a, b, c, d))
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    return finish_mesh(obj, panel, bevel=0.006)


def elbow_arc(name, center, arc_radius, pipe_radius, panel, start_angle, end_angle, plane="xz", segments=18, ring=12):
    verts = []
    faces = []
    for i in range(segments + 1):
        t = start_angle + (end_angle - start_angle) * i / segments
        c = math.cos(t)
        s = math.sin(t)
        if plane == "yz":
            path = Vector((center[0], center[1] + c * arc_radius, center[2] + s * arc_radius))
            normal = Vector((0.0, c, s))
            binormal = Vector((1.0, 0.0, 0.0))
        elif plane == "xy":
            path = Vector((center[0] + c * arc_radius, center[1] + s * arc_radius, center[2]))
            normal = Vector((c, s, 0.0))
            binormal = Vector((0.0, 0.0, 1.0))
        else:
            path = Vector((center[0] + c * arc_radius, center[1], center[2] + s * arc_radius))
            normal = Vector((c, 0.0, s))
            binormal = Vector((0.0, 1.0, 0.0))
        for j in range(ring):
            p = math.tau * j / ring
            pos = path + normal * (math.cos(p) * pipe_radius) + binormal * (math.sin(p) * pipe_radius)
            verts.append(tuple(pos))
    for i in range(segments):
        for j in range(ring):
            a = i * ring + j
            b = i * ring + (j + 1) % ring
            c = (i + 1) * ring + (j + 1) % ring
            d = (i + 1) * ring + j
            faces.append((a, b, c, d))
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    return finish_mesh(obj, panel, bevel=0.001)


def bolt_ring(prefix, x, center_y, center_z, radius, count, axis="x", panel="dark", bolt_radius=0.028):
    for i in range(count):
        a = math.tau * i / count
        if axis == "x":
            loc = (x, center_y + math.cos(a) * radius, center_z + math.sin(a) * radius)
            rot = (0, math.radians(90), 0)
        elif axis == "y":
            loc = (x + math.cos(a) * radius, center_y, center_z + math.sin(a) * radius)
            rot = (math.radians(90), 0, 0)
        else:
            loc = (x + math.cos(a) * radius, center_y + math.sin(a) * radius, center_z)
            rot = (0, 0, 0)
        cylinder(f"{prefix}_Bolt_{i:02d}", loc, bolt_radius, 0.066, panel, 10, rot=rot, bevel=0.003)


def flange_face_bolts(prefix, x, center_y, center_z, radius, count, side, bolt_radius=0.035):
    torus(prefix + "_OuterRaisedBoltTrack", (x, center_y, center_z), radius, 0.042, "dark", 96, 8, rot=(0, math.radians(90), 0))
    torus(prefix + "_InnerSealTrack", (x + side * 0.010, center_y, center_z), radius - 0.34, 0.026, "dark", 88, 6, rot=(0, math.radians(90), 0))
    for i in range(count):
        a = math.tau * i / count
        y = center_y + math.cos(a) * radius
        z = center_z + math.sin(a) * radius
        cylinder(f"{prefix}_Washer_{i:02d}", (x + side * 0.015, y, z), bolt_radius * 1.85, 0.040, "dark", 18, rot=(0, math.radians(90), 0), bevel=0.003)
        cylinder(f"{prefix}_BoltHead_{i:02d}", (x + side * 0.046, y, z), bolt_radius, 0.070, "brass", 12, rot=(0, math.radians(90), 0), bevel=0.004)

def label_text(name, text, loc, size, panel, rot=(0, 0, 0)):
    curve = bpy.data.curves.new(name, "FONT")
    curve.body = text
    curve.align_x = "CENTER"
    curve.align_y = "CENTER"
    curve.size = size
    curve.extrude = 0.006
    obj = bpy.data.objects.new(name, curve)
    obj.location = loc
    obj.rotation_euler = rot
    bpy.context.collection.objects.link(obj)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.convert(target="MESH")
    obj = bpy.context.object
    return finish_mesh(obj, panel, bevel=0.001, shade=False)


def make_handwheel(name, loc, radius, panel, rot=(0, 0, 0)):
    torus(name + "_OuterRing", loc, radius, 0.018, panel, 32, 8, rot)
    cylinder(name + "_Hub", loc, 0.055, 0.045, panel, 18, rot=rot, bevel=0.003)
    for i in range(4):
        a = i * math.pi * 0.5
        end = (loc[0] + math.cos(a) * radius * 0.86, loc[1] + math.sin(a) * radius * 0.86, loc[2])
        cylinder_between(name + f"_Spoke_{i:02d}", loc, end, 0.012, panel, 8, bevel=0.001)


def grating(name, center, width, depth, z):
    box(name + "_Frame", (center[0], center[1], z), (width, depth, 0.06), "dark", bevel=0.004)
    for i in range(11):
        x = center[0] - width * 0.45 + i * width * 0.09
        box(name + f"_LongBar_{i:02d}", (x, center[1], z + 0.05), (0.024, depth * 0.92, 0.030), "shell", bevel=0.001)
    for i in range(5):
        y = center[1] - depth * 0.40 + i * depth * 0.20
        box(name + f"_CrossBar_{i:02d}", (center[0], y, z + 0.056), (width * 0.92, 0.020, 0.026), "shell", bevel=0.001)


def rail(prefix, x0, x1, y, z0, z1):
    for i, x in enumerate([x0, (x0 + x1) * 0.5, x1]):
        cylinder_between(f"{prefix}_Post_{i:02d}", (x, y, z0), (x, y, z1), 0.030, "yellow", 10, bevel=0.001)
    cylinder_between(prefix + "_TopRail", (x0, y, z1), (x1, y, z1), 0.033, "yellow", 10, bevel=0.001)
    cylinder_between(prefix + "_MidRail", (x0, y, z0 + (z1 - z0) * 0.58), (x1, y, z0 + (z1 - z0) * 0.58), 0.026, "yellow", 10, bevel=0.001)
    box(prefix + "_ToeBoard", ((x0 + x1) * 0.5, y, z0 + 0.055), (abs(x1 - x0), 0.035, 0.11), "yellow", bevel=0.001)


def pipe_support(prefix, x, y, z_base, z_top, span=0.78):
    box(prefix + "_Foot", (x, y, z_base + 0.035), (0.62, 0.42, 0.070), "concrete", bevel=0.006)
    cylinder_between(prefix + "_Post_L", (x - span * 0.5, y, z_base + 0.06), (x - span * 0.5, y, z_top), 0.045, "dark", 10, bevel=0.001)
    cylinder_between(prefix + "_Post_R", (x + span * 0.5, y, z_base + 0.06), (x + span * 0.5, y, z_top), 0.045, "dark", 10, bevel=0.001)
    box(prefix + "_CrossHead", (x, y, z_top), (span + 0.20, 0.12, 0.10), "dark", bevel=0.004)


def gauge(prefix, x, y, z, label):
    cylinder(prefix + "_Housing", (x, y, z), 0.24, 0.12, "dark", 28, rot=(math.radians(90), 0, 0), bevel=0.004)
    cylinder(prefix + "_Face", (x, y - 0.068, z), 0.205, 0.018, "white", 28, rot=(math.radians(90), 0, 0), bevel=0.002)
    needle = cylinder_between(prefix + "_Needle", (x, y - 0.083, z), (x + 0.11, y - 0.083, z + 0.08), 0.006, "red", 8, bevel=0.001)
    label_text(prefix + "_Label", label, (x, y - 0.092, z - 0.23), 0.055, "black", rot=(math.radians(90), 0, 0))
    return needle

def animate_rotor(rotor, phase=0.0):
    if rotor is None:
        return
    bpy.context.scene.frame_start = 1
    bpy.context.scene.frame_end = 120
    prefs = getattr(bpy.context.preferences, "edit", None)
    old_interp = getattr(prefs, "keyframe_new_interpolation_type", None) if prefs else None
    if prefs and old_interp is not None:
        prefs.keyframe_new_interpolation_type = "LINEAR"
    rotor.rotation_euler = (0.0, 0.0, phase)
    rotor.keyframe_insert(data_path="rotation_euler", frame=1)
    rotor.rotation_euler = (0.0, 0.0, phase + math.tau * 4.0)
    rotor.keyframe_insert(data_path="rotation_euler", frame=120)
    if prefs and old_interp is not None:
        prefs.keyframe_new_interpolation_type = old_interp
    if rotor.animation_data and rotor.animation_data.action:
        rotor.animation_data.action.name = rotor.name + "_60RPM_Demo"

def make_drive_unit(prefix, x, y=0.0):
    box(prefix + "_ReinforcedNozzlePad", (x, y, 5.44), (1.04, 0.96, 0.13), "dark", bevel=0.010)
    cylinder_between(prefix + "_StuffingBox_Neck", (x, y, 5.42), (x, y, 5.88), 0.27, "dark", 28, bevel=0.004)
    cylinder(prefix + "_MechanicalSeal_Flange", (x, y, 5.90), 0.49, 0.18, "dark", 34, bevel=0.006)
    cylinder(prefix + "_Gearbox", (x, y, 6.20), 0.44, 0.44, "pipe", 32, bevel=0.008)
    cylinder_between(prefix + "_Motor", (x, y - 0.20, 6.44), (x, y - 1.02, 6.44), 0.27, "dark", 28, bevel=0.006)
    cylinder(prefix + "_Motor_FanCover", (x, y - 1.13, 6.44), 0.30, 0.09, "dark", 28, rot=(math.radians(90), 0, 0), bevel=0.004)
    for j in range(4):
        box(prefix + f"_CoolingFin_{j:02d}", (x, y - 0.61, 6.24 + j * 0.080), (0.60, 0.58, 0.020), "dark", bevel=0.001)

def make_internal_baffle(prefix, x):
    zc = SHELL_Z - INTERNAL_DROP
    box(prefix + "_TopWeirPlate", (x, 0.0, zc + 0.92), (0.13, 2.54, 0.46), "brass", bevel=0.006)
    box(prefix + "_BottomDamPlate", (x, 0.0, zc - 1.02), (0.13, 2.54, 0.50), "brass", bevel=0.006)
    box(prefix + "_LeftWeb", (x, -1.22, zc), (0.13, 0.30, 1.78), "brass", bevel=0.006)
    box(prefix + "_RightWeb", (x, 1.22, zc), (0.13, 0.30, 1.78), "brass", bevel=0.006)
    torus(prefix + "_ReinforcementRing", (x, 0.0, zc), BAFFLE_R, 0.026, "dark", 48, 6, rot=(0, math.radians(90), 0))
    for j, (y, z) in enumerate(((-0.58, zc - 0.90), (0.0, zc - 0.90), (0.58, zc - 0.90), (-0.44, zc + 0.92), (0.44, zc + 0.92))):
        cylinder(prefix + f"_DarkTransferPort_{j:02d}", (x - 0.064, y, z), 0.082, 0.018, "dark", 18, rot=(0, math.radians(90), 0), bevel=0.001)
    label_text(prefix + "_Label", "COMPARTMENT BAFFLE", (x - 0.075, -1.42, zc), 0.044, "black", rot=(math.radians(90), 0, math.radians(-90)))

def make_internal_rotor(prefix, x):
    zc = SHELL_Z - INTERNAL_DROP
    rotor = empty(prefix, (x, 0.0, zc))
    parent_keep_world(cylinder_between(prefix + "_VerticalShaft", (x, 0.0, 1.34), (x, 0.0, 5.02), 0.060, "brass", 14, bevel=0.002), rotor)
    for level, z in enumerate((zc - 0.48, zc + 0.34)):
        for blade in range(4):
            a = blade * math.pi * 0.5 + (level * math.pi * 0.25)
            dx = math.cos(a) * 0.36
            dy = math.sin(a) * 0.36
            length = 0.74
            width = 0.13
            rot = (math.radians(12 if level == 0 else -12), 0, a)
            obj = box(prefix + f"_PitchedImpeller_{level}_{blade}", (x + dx, dy, z), (length, width, 0.055), "brass", bevel=0.004, rot=rot)
            parent_keep_world(obj, rotor)
    parent_keep_world(cylinder(prefix + "_LowerHub", (x, 0.0, zc - 0.48), 0.19, 0.18, "brass", 18, bevel=0.004), rotor)
    parent_keep_world(cylinder(prefix + "_UpperHub", (x, 0.0, zc + 0.34), 0.18, 0.16, "brass", 18, bevel=0.004), rotor)
    parent_keep_world(cylinder(prefix + "_BottomSteadyBearing", (x, 0.0, 1.34), 0.25, 0.11, "dark", 22, bevel=0.004), rotor)
    for z, rad in ((zc - 0.48, 0.62), (zc + 0.34, 0.58)):
        parent_keep_world(torus(prefix + f"_MotionRing_{z:.2f}", (x, 0.0, z), rad, 0.010, "glass", 48, 5), rotor)
    animate_rotor(rotor, phase=(x + 6.0) * 0.13)
    return rotor


def build_asset():
    clear_scene()
    os.makedirs(SOURCE_DIR, exist_ok=True)
    create_root()
    global ATLAS_MATERIAL
    ATLAS_MATERIAL = create_material(create_atlas())

    # Foundation, skid, saddle supports.
    box("L7_Autoclave_Concrete_ServicePad", (0.0, 0.0, 0.10), (18.8, 6.4, 0.20), "concrete", bevel=0.020)
    for y in (-1.72, 1.72):
        box(f"L7_Skid_Longitudinal_IBeam_{y:+.1f}", (0.0, y, 0.45), (16.9, 0.20, 0.32), "dark", bevel=0.010)
    for x in (-6.6, -3.3, 0.0, 3.3, 6.6):
        box(f"L7_Skid_Cross_IBeam_{x:+.1f}", (x, 0.0, 0.46), (0.22, 3.86, 0.30), "dark", bevel=0.010)
    for x in (-5.8, -2.0, 2.0, 5.8):
        box(f"L7_Saddle_GroutPad_{x:+.1f}", (x, 0.0, 0.69), (1.18, 3.08, 0.12), "concrete", bevel=0.010)
        for y in (-1.12, 1.12):
            box(f"L7_Saddle_Shoe_{x:+.1f}_{y:+.1f}", (x, y, 1.05), (0.86, 0.35, 0.56), "dark", bevel=0.018)
            cylinder(f"L7_Saddle_AnchorBolt_{x:+.1f}_{y:+.1f}", (x, y, 1.38), 0.035, 0.11, "brass", 10, bevel=0.002)

    # HPAL pressure vessel shell, bolted ends, circumferential stiffener bands.
    # Front sector is cut away to show the internal compartment baffles and mixers.
    shell_cut_min = math.radians(134)
    shell_cut_max = math.radians(226)
    cutaway_cylinder_shell("L7_Autoclave_PressureShell", (0.0, 0.0, SHELL_Z), SHELL_R, 13.90, "shell", 96, 32, shell_cut_min, shell_cut_max, bevel=0.010)
    cutaway_cylinder_shell("L7_XRay_Titanium_InternalLiner_Cutaway", (0.0, 0.0, SHELL_Z), LINER_R, 13.26, "steam", 84, 26, shell_cut_min, shell_cut_max, bevel=0.004)
    for i, a in enumerate((shell_cut_min, shell_cut_max)):
        y = math.cos(a)
        z = math.sin(a)
        cylinder_between(f"L7_Cutaway_ThickWall_Lip_{i:02d}", (-6.90, y * SHELL_R, SHELL_Z + z * SHELL_R), (6.90, y * SHELL_R, SHELL_Z + z * SHELL_R), 0.062, "dark", 14, bevel=0.002)
        cylinder_between(f"L7_XRay_TitaniumLiner_Lip_{i:02d}", (-6.60, y * LINER_R, SHELL_Z + z * LINER_R), (6.60, y * LINER_R, SHELL_Z + z * LINER_R), 0.030, "steam", 12, bevel=0.001)
    label_text("L7_Label_CUTAWAY_VIEW", "CUTAWAY: BAFFLES + ROTATING AGITATORS", (0.0, -2.34, 5.05), 0.080, "black", rot=(math.radians(90), 0, 0))
    half_sphere_cap("L7_Autoclave_EndCap_Left", (-6.95, 0.0, SHELL_Z), SHELL_R, "shell", -1, 72, 16)
    half_sphere_cap("L7_Autoclave_EndCap_Right", (6.95, 0.0, SHELL_Z), SHELL_R, "shell", 1, 72, 16)
    cylinder("L7_Autoclave_Left_HeavyFlange", (-7.22, 0.0, SHELL_Z), 2.25, 0.30, "dark", 72, rot=(0, math.radians(90), 0), bevel=0.008)
    cylinder("L7_Autoclave_Right_HeavyFlange", (7.22, 0.0, SHELL_Z), 2.25, 0.30, "dark", 72, rot=(0, math.radians(90), 0), bevel=0.008)
    flange_face_bolts("L7_Left_EndFlange", -7.38, 0.0, SHELL_Z, 2.02, 32, side=-1, bolt_radius=0.034)
    flange_face_bolts("L7_Right_EndFlange", 7.38, 0.0, SHELL_Z, 2.02, 32, side=1, bolt_radius=0.034)
    for i, x in enumerate((-5.45, -3.05, -0.65, 1.75, 4.15, 6.10)):
        torus(f"L7_Autoclave_Dark_StiffenerBand_{i:02d}", (x, 0.0, SHELL_Z), 2.22, 0.038, "dark", 80, 8, rot=(0, math.radians(90), 0))
    box("L7_Autoclave_Top_Longitudinal_Seam", (0.0, 0.0, 5.56), (12.2, 0.12, 0.070), "dark", bevel=0.002)
    box("L7_Autoclave_Service_Nameplate", (-3.95, -2.19, 3.28), (1.88, 0.050, 0.52), "label", bevel=0.004)
    label_text("L7_Label_AUTOCLAVE", "HPAL AUTOCLAVE", (-3.95, -2.232, 3.40), 0.094, "black", rot=(math.radians(90), 0, 0))
    label_text("L7_Label_R701", "R-701  250C  50 bar", (-3.95, -2.235, 3.08), 0.063, "black", rot=(math.radians(90), 0, 0))

    # Five top-mounted agitator drives, matching compartmented HPAL practice.
    rotor_xs = [-5.20, -2.60, 0.0, 2.60, 5.20]
    for i, x in enumerate(rotor_xs):
        make_drive_unit(f"L7_AgitatorDrive_{i:02d}", x, y=0.0)

    # Process connections: preheater slurry in, acid top injection, vapor outlet, bottom letdown.
    cylinder_between("Pipe_Autoclave_SlurryInlet_SideNozzle", (7.62, -1.44, 3.26), (6.58, -1.44, 3.26), 0.28, "pipe", 30, bevel=0.004)
    cylinder("L7_SlurryInlet_Flange", (7.70, -1.44, 3.26), 0.48, 0.18, "dark", 36, rot=(0, math.radians(90), 0), bevel=0.006)
    bolt_ring("L7_SlurryInlet_Flange", 7.80, -1.44, 3.26, 0.390, 12, axis="x", panel="brass", bolt_radius=0.020)

    cylinder_between("Pipe_Autoclave_AcidInject_TopNozzle", (1.30, 0.78, 5.46), (1.30, 0.78, 6.48), 0.16, "acid", 22, bevel=0.003)
    cylinder("L7_AcidInjection_TopFlange", (1.30, 0.78, 6.50), 0.33, 0.13, "dark", 28, bevel=0.005)
    cylinder_between("Pipe_Autoclave_AcidInject_Header", (-1.50, 1.68, 6.48), (3.25, 1.68, 6.48), 0.110, "acid", 20, bevel=0.003)
    cylinder_between("Pipe_Autoclave_AcidInject_Drop", (1.30, 1.68, 6.48), (1.30, 0.78, 6.48), 0.110, "acid", 20, bevel=0.003)
    cylinder("L7_AcidInject_ControlValve_Red", (-1.50, 1.68, 6.48), 0.23, 0.26, "red", 24, rot=(math.radians(90), 0, 0), bevel=0.006)
    make_handwheel("L7_AcidInject_Handwheel", (-1.50, 1.96, 6.72), 0.25, "yellow", rot=(math.radians(90), 0, 0))
    label_text("L7_Label_ACID_IN", "ACID IN", (1.30, 1.90, 6.08), 0.085, "black", rot=(math.radians(90), 0, 0))

    cylinder_between("Pipe_Autoclave_VaporOutlet_TopNozzle", (-5.85, 0.76, 5.46), (-5.85, 0.76, 6.78), 0.22, "steam", 26, bevel=0.003)
    cylinder("L7_VaporOutlet_TopFlange", (-5.85, 0.76, 6.78), 0.42, 0.15, "dark", 34, bevel=0.006)
    cylinder_between("Pipe_Autoclave_VaporOutlet_Header", (-5.85, 0.76, 6.78), (-7.10, 1.82, 7.00), 0.20, "steam", 26, bevel=0.003)
    cylinder("L7_VaporOutlet_EndFlange", (-7.22, 1.88, 7.02), 0.39, 0.13, "dark", 32, rot=(math.radians(62), 0, math.radians(90)), bevel=0.006)
    label_text("L7_Label_VAPOR_OUT", "VAPOR OUT", (-5.85, 1.90, 6.28), 0.080, "black", rot=(math.radians(90), 0, 0))

    cylinder_between("Pipe_Autoclave_BottomOutlet_DrainNozzle", (-6.00, 0.0, 1.24), (-6.00, 0.0, 0.58), 0.28, "pipe", 30, bevel=0.004)
    elbow_arc("Pipe_Autoclave_BottomOutlet_Elbow", (-6.52, 0.0, 0.58), 0.52, 0.28, "pipe", 0.0, math.radians(90), plane="xz", segments=18, ring=14)
    cylinder_between("Pipe_Autoclave_BottomOutlet_ToFlashStub", (-7.04, 0.0, 1.10), (-8.75, 0.0, 1.10), 0.28, "pipe", 30, bevel=0.004)
    cylinder("L7_BottomOutlet_Flange", (-8.88, 0.0, 1.10), 0.47, 0.16, "dark", 34, rot=(0, math.radians(90), 0), bevel=0.006)
    cylinder("L7_BottomOutlet_LetdownValve_Body", (-7.52, 0.0, 1.10), 0.38, 0.40, "red", 28, rot=(0, math.radians(90), 0), bevel=0.008)
    make_handwheel("L7_BottomOutlet_LetdownHandwheel", (-7.52, -0.44, 1.46), 0.30, "yellow", rot=(math.radians(90), 0, 0))
    pipe_support("L7_BottomOutlet_PipeSupport", -8.10, 0.0, 0.22, 0.89, span=0.90)
    label_text("L7_Label_BOTTOM_OUT", "BOTTOM OUT", (-6.92, -0.70, 1.58), 0.075, "black", rot=(math.radians(90), 0, 0))

    # Additional low-point liquid outlet, matching the lower drain spool shown in the reference.
    cylinder_between("Pipe_Autoclave_LiquidUnderflow_BellyNozzle", (5.72, -0.62, 1.54), (5.72, -1.52, 1.18), 0.24, "pipe", 30, bevel=0.004)
    cylinder("L7_LiquidUnderflow_Vessel_Flange_Red", (5.72, -1.62, 1.18), 0.43, 0.20, "red", 36, rot=(math.radians(90), 0, 0), bevel=0.006)
    cylinder("L7_LiquidUnderflow_Gasket_Dark", (5.72, -1.49, 1.18), 0.47, 0.065, "dark", 36, rot=(math.radians(90), 0, 0), bevel=0.004)
    bolt_ring("L7_LiquidUnderflow_Flange", 5.72, -1.64, 1.18, 0.34, 12, axis="y", panel="brass", bolt_radius=0.018)
    cylinder("L7_LiquidUnderflow_ValveBody_Red", (5.72, -2.02, 1.18), 0.34, 0.34, "red", 30, rot=(math.radians(90), 0, 0), bevel=0.008)
    cylinder_between("Pipe_Autoclave_LiquidUnderflow_ToTransfer", (5.72, -1.80, 1.18), (5.72, -3.34, 1.18), 0.26, "pipe", 30, bevel=0.004)
    cylinder("L7_LiquidUnderflow_EndFlange_Dark", (5.72, -3.48, 1.18), 0.44, 0.16, "dark", 34, rot=(math.radians(90), 0, 0), bevel=0.006)
    cylinder("L7_LiquidUnderflow_BlindCap", (5.72, -3.62, 1.18), 0.36, 0.13, "pipe", 32, rot=(math.radians(90), 0, 0), bevel=0.006)
    make_handwheel("L7_LiquidUnderflow_Handwheel", (5.72, -2.02, 1.66), 0.23, "yellow", rot=(0, 0, 0))
    box("L7_LiquidUnderflow_SupportFoot", (5.72, -2.82, 0.24), (0.96, 0.42, 0.080), "concrete", bevel=0.006)
    cylinder_between("L7_LiquidUnderflow_SupportPost_L", (5.38, -2.82, 0.28), (5.38, -2.82, 0.94), 0.035, "dark", 10, bevel=0.001)
    cylinder_between("L7_LiquidUnderflow_SupportPost_R", (6.06, -2.82, 0.28), (6.06, -2.82, 0.94), 0.035, "dark", 10, bevel=0.001)
    box("L7_LiquidUnderflow_SupportSaddle", (5.72, -2.82, 0.98), (0.84, 0.14, 0.10), "dark", bevel=0.004)
    label_text("L7_Label_LIQUID_DRAIN", "LIQUID OUT", (5.72, -3.06, 1.72), 0.070, "black", rot=(math.radians(90), 0, 0))

    # Manways, rupture disk, relief valve, gauges.
    cylinder_between("L7_Manway_Neck_A", (-3.92, -0.98, 5.34), (-3.92, -0.98, 6.08), 0.32, "shell", 28, bevel=0.004)
    cylinder("L7_Manway_BlindFlange_A", (-3.92, -0.98, 6.13), 0.55, 0.13, "dark", 36, bevel=0.006)
    bolt_ring("L7_Manway_BoltRing_A", -3.92, -0.98, 6.13, 0.45, 12, axis="z", panel="brass", bolt_radius=0.020)
    cylinder_between("L7_Manway_Neck_B", (3.92, -0.98, 5.34), (3.92, -0.98, 6.08), 0.32, "shell", 28, bevel=0.004)
    cylinder("L7_Manway_BlindFlange_B", (3.92, -0.98, 6.13), 0.55, 0.13, "dark", 36, bevel=0.006)
    cylinder_between("L7_ReliefValve_Neck", (6.15, 0.72, 5.48), (6.15, 0.72, 6.18), 0.15, "steam", 18, bevel=0.002)
    cylinder("L7_ReliefValve_BrassCap", (6.15, 0.72, 6.33), 0.28, 0.26, "brass", 22, bevel=0.006)
    cylinder_between("L7_ReliefValve_Discharge", (6.15, 0.72, 6.46), (6.15, 1.70, 6.88), 0.085, "steam", 14, bevel=0.002)
    gauge("L7_PressureGauge", -1.00, -2.25, 4.18, "P")
    gauge("L7_TemperatureGauge", 0.0, -2.25, 4.18, "T")
    gauge("L7_RpmGauge", 1.00, -2.25, 4.18, "RPM")

    # Clean service platform for top drives. Kept offset so the vessel silhouette stays readable.
    grating("L7_TopDrive_ServicePlatform_Grating", (0.0, -2.72), 11.4, 0.88, 5.58)
    rail("L7_TopDrive_ServicePlatform_OuterRail", -5.7, 5.7, -3.20, 5.58, 6.40)
    for i, x in enumerate((-4.8, -2.4, 0.0, 2.4, 4.8)):
        cylinder_between(f"L7_TopDrive_Platform_Bracket_{i:02d}", (x, -2.05, 5.38), (x, -2.72, 5.58), 0.038, "dark", 10, bevel=0.001)
    # X-Ray internals: compartment baffles/weirs, slurry volume, titanium liner ribs, independent agitator rotors.
    empty("L7_XRay_AgitatorShaft", (0.0, 0.0, SHELL_Z))
    cylinder("L7_XRay_InnerSlurry_Surface", (0.0, 0.0, LIQUID_Z), LIQUID_R, 12.20, "slurry", 64, rot=(0, math.radians(90), 0), bevel=0.004)
    box("L7_XRay_OpenLiquidCutFace", (0.0, -1.26, LIQUID_Z), (12.12, 0.040, 1.32), "slurry", bevel=0.002)
    for i, x in enumerate((-3.90, -1.30, 1.30, 3.90)):
        make_internal_baffle(f"L7_XRay_CompartmentBaffle_{i:02d}", x)
    for i, x in enumerate(rotor_xs):
        make_internal_rotor(f"L7_XRay_AgitatorRotor_{i:02d}", x)
    for i, x in enumerate((-6.15, -3.90, -1.30, 1.30, 3.90, 6.15)):
        torus(f"L7_XRay_Titanium_Liner_Rib_{i:02d}", (x, 0.0, SHELL_Z - 0.18), 1.70, 0.020, "steam", 60, 6, rot=(0, math.radians(90), 0))
    for i, x in enumerate((-5.7, -4.1, -2.5, -0.8, 0.8, 2.5, 4.1, 5.7)):
        sphere(f"L7_XRay_LateriteParticle_{i:02d}", (x, -0.68 + (i % 3) * 0.38, 2.04 + (i % 2) * 0.14), 0.105, "orange", 12, 6, scale=(1.0, 1.0, 0.62), bevel=0.001)

    # Local field controls.
    box("L7_Local_ControlPanel_Post", (7.30, -1.78, 1.08), (0.065, 0.065, 1.20), "dark", bevel=0.004)
    box("L7_Local_ControlPanel_Box", (7.30, -1.88, 1.88), (0.66, 0.20, 0.58), "shell", bevel=0.012)
    cylinder("L7_Local_Control_EStop", (7.04, -2.00, 2.00), 0.058, 0.022, "red", 18, rot=(math.radians(90), 0, 0), bevel=0.002)
    cylinder("L7_Local_Control_RunLamp", (7.28, -2.00, 2.00), 0.042, 0.020, "green", 16, rot=(math.radians(90), 0, 0), bevel=0.002)
    cylinder("L7_Local_Control_GlassLamp", (7.50, -2.00, 2.00), 0.042, 0.020, "glass", 16, rot=(math.radians(90), 0, 0), bevel=0.002)

    # Blender preview lighting/camera; exporters omit these, but opening the .blend shows the redesign clearly.
    bpy.ops.object.light_add(type="AREA", location=(0.0, -7.0, 8.0))
    key = bpy.context.object
    key.name = "L7_Preview_Area_KeyLight"
    key.data.energy = 650
    key.data.size = 5.5
    bpy.ops.object.camera_add(location=(9.8, -8.8, 6.2), rotation=(math.radians(62), 0, math.radians(47)))
    cam = bpy.context.object
    cam.name = "L7_Preview_Cutaway_Camera"
    bpy.context.scene.camera = cam
    try:
        cam.data.lens = 28
    except Exception:
        pass

    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH,
        use_selection=False,
        object_types={"EMPTY", "MESH"},
        apply_unit_scale=True,
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=False,
        add_leaf_bones=False,
        use_mesh_modifiers=True,
        path_mode="RELATIVE",
    )
    bpy.ops.export_scene.gltf(filepath=GLB_PATH, export_format="GLB", export_apply=True, export_yup=True)
    print("[OK] Level 7 Autoclave exported")
    print(FBX_PATH)
    print(GLB_PATH)
    print(BLEND_PATH)
    print(ATLAS_PATH)


if __name__ == "__main__":
    build_asset()
