import math
import os
import random

import bpy
from mathutils import Vector


SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
SOURCE_DIR = os.path.abspath(os.path.join(SCRIPT_DIR, "..", "..", "..", "BlenderSources", "Level5PreHeaterBlender"))
FBX_PATH = os.path.join(SCRIPT_DIR, "level5_preheater_industrial_uv.fbx")
GLB_PATH = os.path.join(SCRIPT_DIR, "level5_preheater_industrial_uv.glb")
BLEND_PATH = os.path.join(SOURCE_DIR, "level5_preheater_industrial_uv.blend")
ATLAS_PATH = os.path.join(SCRIPT_DIR, "level5_preheater_uv_atlas.png")
PREVIEW_PATH = os.path.join(SCRIPT_DIR, "level5_preheater_preview.png")

PANELS = {
    "steel": (0, 0),
    "dark": (1, 0),
    "yellow": (2, 0),
    "blue": (3, 0),
    "concrete": (0, 1),
    "pipe": (1, 1),
    "heated": (2, 1),
    "steam": (3, 1),
    "red": (0, 2),
    "green": (1, 2),
    "white": (2, 2),
    "black": (3, 2),
    "brass": (0, 3),
    "glass": (1, 3),
    "rust": (2, 3),
    "label": (3, 3),
}

ROOT_COLLECTION = None
ROOT_EMPTY = None
ATLAS_MATERIAL = None


def clamp01(value):
    return max(0.0, min(1.0, value))


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
    for mesh in list(bpy.data.meshes):
        if mesh.users == 0:
            bpy.data.meshes.remove(mesh)
    for material in list(bpy.data.materials):
        if material.users == 0:
            bpy.data.materials.remove(material)


def create_atlas():
    random.seed(505)
    size = 1024
    image = bpy.data.images.new("level5_preheater_uv_atlas", size, size, alpha=True)
    pixels = [0.0] * (size * size * 4)
    base = {
        "steel": (0.56, 0.62, 0.60, 1.0),
        "dark": (0.08, 0.09, 0.09, 1.0),
        "yellow": (0.96, 0.66, 0.05, 1.0),
        "blue": (0.06, 0.23, 0.42, 1.0),
        "concrete": (0.42, 0.40, 0.36, 1.0),
        "pipe": (0.47, 0.55, 0.58, 1.0),
        "heated": (0.72, 0.25, 0.08, 1.0),
        "steam": (0.76, 0.79, 0.78, 0.92),
        "red": (0.78, 0.04, 0.03, 1.0),
        "green": (0.05, 0.55, 0.22, 1.0),
        "white": (0.86, 0.87, 0.82, 1.0),
        "black": (0.01, 0.01, 0.01, 1.0),
        "brass": (0.84, 0.58, 0.18, 1.0),
        "glass": (0.20, 0.58, 0.74, 0.82),
        "rust": (0.52, 0.23, 0.09, 1.0),
        "label": (0.92, 0.88, 0.68, 1.0),
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
                if name in {"steel", "pipe"}:
                    grain = math.sin(nx * math.tau * 10.0) * 0.014
                    rr += grain
                    gg += grain
                    bb += grain
                    if random.random() < 0.015:
                        rr *= 0.62
                        gg *= 0.62
                        bb *= 0.62
                elif name == "blue":
                    seam = 0.06 if (x - x0) % 96 < 4 or (y - y0) % 96 < 4 else 0.0
                    rr += seam
                    gg += seam
                    bb += seam
                elif name == "yellow":
                    if ((x - x0 + y - y0) // 36) % 2 == 0:
                        rr, gg, bb = 0.08, 0.07, 0.05
                elif name == "heated":
                    pulse = math.sin((nx * 2.5 + ny * 4.0) * math.tau) * 0.05
                    rr += pulse
                    gg += pulse * 0.25
                elif name == "steam":
                    wave = math.sin(nx * math.tau * 6.0 + ny * 2.0) * 0.035
                    rr += wave
                    gg += wave
                    bb += wave
                elif name == "concrete":
                    if random.random() < 0.08:
                        rr, gg, bb = 0.29, 0.28, 0.25
                elif name == "rust":
                    if random.random() < 0.20:
                        rr, gg, bb = 0.64, 0.29, 0.12
                elif name == "glass":
                    glint = 0.10 if (x - x0 + y - y0) % 66 < 8 else 0.0
                    rr += glint
                    gg += glint
                    bb += glint
                elif name == "label":
                    if (x - x0) % 56 < 3 or (y - y0) % 56 < 3:
                        rr, gg, bb = 0.20, 0.15, 0.08
                i = (y * size + x) * 4
                pixels[i:i + 4] = [clamp01(rr), clamp01(gg), clamp01(bb), a]

    image.pixels.foreach_set(pixels)
    image.filepath_raw = ATLAS_PATH
    image.file_format = "PNG"
    image.save()
    return image


def create_material(image):
    mat = bpy.data.materials.new("M_Level5_PreHeater_UVAtlas")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    bsdf = nodes.get("Principled BSDF")
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = image
    if bsdf:
        mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        if "Alpha" in bsdf.inputs:
            mat.node_tree.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
        if "Metallic" in bsdf.inputs:
            bsdf.inputs["Metallic"].default_value = 0.20
        if "Roughness" in bsdf.inputs:
            bsdf.inputs["Roughness"].default_value = 0.56
    return mat


def create_root():
    global ROOT_COLLECTION, ROOT_EMPTY
    ROOT_COLLECTION = bpy.data.collections.new("Level5_PreHeater_Industrial_UV")
    bpy.context.scene.collection.children.link(ROOT_COLLECTION)
    ROOT_EMPTY = bpy.data.objects.new("L5_PreHeater_Industrial_Root", None)
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
            mod = obj.modifiers.new("small_safe_bevel", "BEVEL")
            mod.width = bevel
            mod.segments = 2
            mod.affect = "EDGES"
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


def half_sphere_cap(name, loc, radius, panel, side, segments=40, rings=10):
    # side: -1 left cap, +1 right cap along X axis.
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


def bolt_ring(prefix, x, center_y, center_z, radius, count, axis="x", panel="steel", bolt_radius=0.032):
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
        cylinder(f"{prefix}_Bolt_{i:02d}", loc, bolt_radius, 0.060, panel, 10, rot=rot, bevel=0.003)


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


def elbow_arc(name, center, arc_radius, pipe_radius, panel, start_angle, end_angle, plane="xz", segments=18, ring=12):
    verts = []
    faces = []
    for i in range(segments + 1):
        t = start_angle + (end_angle - start_angle) * i / segments
        c = math.cos(t)
        s = math.sin(t)
        if plane == "xy":
            path = Vector((center[0] + c * arc_radius, center[1] + s * arc_radius, center[2]))
            normal = Vector((c, s, 0.0))
            binormal = Vector((0.0, 0.0, 1.0))
        elif plane == "yz":
            path = Vector((center[0], center[1] + c * arc_radius, center[2] + s * arc_radius))
            normal = Vector((0.0, c, s))
            binormal = Vector((1.0, 0.0, 0.0))
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


def build_grating(name, origin, width, depth, z, panel="dark"):
    box(name + "_Frame", (origin[0], origin[1], z), (width, depth, 0.055), panel, bevel=0.004)
    for i in range(9):
        x = origin[0] - width * 0.44 + i * width * 0.11
        box(name + f"_LongBar_{i:02d}", (x, origin[1], z + 0.045), (0.025, depth * 0.94, 0.030), "steel", bevel=0.001)
    for i in range(5):
        y = origin[1] - depth * 0.40 + i * depth * 0.20
        box(name + f"_CrossBar_{i:02d}", (origin[0], y, z + 0.052), (width * 0.92, 0.020, 0.028), "steel", bevel=0.001)


def build_guardrail(prefix, x0, x1, y, z0, z1):
    for i, x in enumerate([x0, (x0 + x1) * 0.5, x1]):
        cylinder_between(f"{prefix}_Post_{i:02d}", (x, y, z0), (x, y, z1), 0.032, "yellow", 10, bevel=0.001)
    cylinder_between(prefix + "_TopRail", (x0, y, z1), (x1, y, z1), 0.033, "yellow", 10, bevel=0.001)
    cylinder_between(prefix + "_MidRail", (x0, y, z0 + (z1 - z0) * 0.58), (x1, y, z0 + (z1 - z0) * 0.58), 0.026, "yellow", 10, bevel=0.001)
    box(prefix + "_ToeBoard", ((x0 + x1) * 0.5, y, z0 + 0.055), (abs(x1 - x0), 0.035, 0.11), "yellow", bevel=0.001)


def pipe_support(prefix, x, y, z_base, z_top, span=0.72):
    box(prefix + "_Foot", (x, y, z_base + 0.035), (0.58, 0.40, 0.070), "concrete", bevel=0.006)
    cylinder_between(prefix + "_Post_L", (x - span * 0.5, y, z_base + 0.06), (x - span * 0.5, y, z_top), 0.045, "dark", 10, bevel=0.001)
    cylinder_between(prefix + "_Post_R", (x + span * 0.5, y, z_base + 0.06), (x + span * 0.5, y, z_top), 0.045, "dark", 10, bevel=0.001)
    box(prefix + "_CrossHead", (x, y, z_top), (span + 0.20, 0.12, 0.10), "dark", bevel=0.004)
    cylinder(prefix + "_PipeSaddle", (x, y, z_top + 0.08), 0.11, 0.14, "steel", 16, rot=(math.radians(90), 0, 0), bevel=0.002)


def tube_face(prefix, x, side):
    cylinder(prefix + "_MachinedTubeSheet", (x, 0.0, 2.25), 0.86, 0.055, "steel", 48, rot=(0, math.radians(90), 0), bevel=0.004)
    rows = [(-0.48, 0.0), (-0.24, 0.30), (-0.24, -0.30), (0.0, 0.0), (0.24, 0.30), (0.24, -0.30), (0.48, 0.0)]
    for i, (y, zoff) in enumerate(rows):
        cylinder(f"{prefix}_TubeHole_{i:02d}", (x + side * 0.034, y, 2.25 + zoff), 0.070, 0.020, "black", 18, rot=(0, math.radians(90), 0), bevel=0.001)


def build_asset():
    clear_scene()
    os.makedirs(SOURCE_DIR, exist_ok=True)
    create_root()
    global ATLAS_MATERIAL
    ATLAS_MATERIAL = create_material(create_atlas())

    # Base foundation and supports.
    box("L5_PreHeater_Concrete_Foundation", (0.0, 0.0, 0.12), (9.45, 3.35, 0.24), "concrete", bevel=0.025)
    for x in (-3.30, 0.0, 3.30):
        box(f"L5_PreHeater_Skid_CrossFrame_{x:+.1f}", (x, 0.0, 0.43), (0.18, 2.35, 0.20), "dark", bevel=0.012)
    for y in (-1.08, 1.08):
        box(f"L5_PreHeater_Skid_LongBeam_{y:+.1f}", (0.0, y, 0.46), (8.70, 0.16, 0.22), "dark", bevel=0.014)
    for x in (-3.35, 3.35):
        for y in (-0.82, 0.82):
            box(f"L5_Shell_Saddle_Base_{x:+.1f}_{y:+.1f}", (x, y, 0.76), (0.72, 0.28, 0.40), "dark", bevel=0.018)
            cylinder(f"L5_Shell_Saddle_Bolt_{x:+.1f}_{y:+.1f}", (x, y, 1.02), 0.040, 0.10, "steel", 10, bevel=0.004)

    # Main horizontal preheater vessel.
    cylinder("L5_PreHeater_Blue_Insulated_Shell", (0.0, 0.0, 2.25), 1.12, 7.05, "blue", 48, rot=(0, math.radians(90), 0), bevel=0.012)
    half_sphere_cap("L5_PreHeater_Left_Dished_EndCap", (-3.52, 0.0, 2.25), 1.12, "steel", -1, 44, 12)
    half_sphere_cap("L5_PreHeater_Right_Dished_EndCap", (3.52, 0.0, 2.25), 1.12, "steel", 1, 44, 12)
    for i, x in enumerate([-3.48, -2.20, -0.90, 0.42, 1.72, 3.02]):
        cylinder(f"HeatingFin_Band_{i:02d}", (x, 0.0, 2.25), 1.18, 0.10, "heated", 48, rot=(0, math.radians(90), 0), bevel=0.006)
        bolt_ring(f"L5_Band_{i:02d}", x, 0.0, 2.25, 1.23, 14, axis="x", panel="steel", bolt_radius=0.020)

    # Tube bundle end covers and nozzles.
    cylinder("L5_PreHeater_Left_TubeSheet_Flange", (-3.82, 0.0, 2.25), 1.05, 0.18, "steel", 48, rot=(0, math.radians(90), 0), bevel=0.010)
    cylinder("L5_PreHeater_Right_TubeSheet_Flange", (3.82, 0.0, 2.25), 1.05, 0.18, "steel", 48, rot=(0, math.radians(90), 0), bevel=0.010)
    bolt_ring("L5_Left_TubeSheet", -3.92, 0.0, 2.25, 0.91, 18, axis="x", panel="steel", bolt_radius=0.026)
    bolt_ring("L5_Right_TubeSheet", 3.92, 0.0, 2.25, 0.91, 18, axis="x", panel="steel", bolt_radius=0.026)
    for y in (-0.54, -0.18, 0.18, 0.54):
        cylinder_between(f"L5_Visible_TubeBundle_Line_{y:+.2f}", (-3.96, y, 2.25), (-4.38, y, 2.25), 0.040, "pipe", 12, bevel=0.001)
        cylinder_between(f"L5_Visible_TubeBundle_Line_R_{y:+.2f}", (3.96, y, 2.25), (4.38, y, 2.25), 0.040, "pipe", 12, bevel=0.001)

    # Slurry inlet/outlet and steam lines.
    cylinder_between("L5_Slurry_Inlet_Pipe_Stub", (-4.55, -0.95, 2.25), (-3.90, -0.95, 2.25), 0.20, "pipe", 28, bevel=0.004)
    cylinder("L5_Slurry_Inlet_Flange", (-4.60, -0.95, 2.25), 0.34, 0.11, "steel", 32, rot=(0, math.radians(90), 0), bevel=0.006)
    bolt_ring("L5_Slurry_Inlet_Flange", -4.66, -0.95, 2.25, 0.28, 8, axis="x", panel="steel", bolt_radius=0.020)
    cylinder_between("L5_Slurry_Outlet_Riser", (3.28, 0.0, 3.12), (3.28, 0.0, 4.14), 0.19, "pipe", 28, bevel=0.004)
    cylinder_between("L5_Slurry_Outlet_OverheadRun", (3.28, 0.0, 4.14), (4.40, 0.72, 4.14), 0.19, "pipe", 28, bevel=0.004)
    cylinder("L5_Slurry_Outlet_Flange", (4.48, 0.76, 4.14), 0.34, 0.12, "steel", 32, rot=(math.radians(58), 0, math.radians(90)), bevel=0.006)
    cylinder_between("L5_Steam_Manifold_Header", (-2.95, -1.46, 1.45), (3.15, -1.46, 1.45), 0.12, "steam", 22, bevel=0.003)
    for i, x in enumerate([-2.42, -1.20, 0.02, 1.24, 2.46]):
        cylinder_between(f"HeatingFin_Steam_Injection_Lance_{i:02d}", (x, -1.46, 1.45), (x, -0.82, 2.05), 0.055, "heated", 14, bevel=0.002)
        cylinder(f"L5_Steam_Lance_Flange_{i:02d}", (x, -0.82, 2.05), 0.14, 0.045, "steel", 18, rot=(math.radians(45), 0, 0), bevel=0.003)

    # Valve station, gauge, vents.
    cylinder_between("L5_Steam_Valve_Riser", (4.10, -1.46, 1.45), (4.10, -1.46, 2.74), 0.12, "steam", 20, bevel=0.003)
    cylinder("L5_Steam_Valve_Body_Red", (4.10, -1.46, 2.28), 0.22, 0.28, "red", 24, rot=(math.radians(90), 0, 0), bevel=0.008)
    make_handwheel("L5_Decorative_Steam_Valve_Handwheel", (4.10, -1.64, 2.60), 0.30, "yellow", rot=(math.radians(90), 0, 0))
    cylinder_between("L5_Temperature_Tap_Line", (1.36, 0.70, 3.05), (1.70, 1.18, 3.55), 0.030, "brass", 12, bevel=0.001)
    cylinder("L5_TempGauge_Dial", (1.78, 1.28, 3.66), 0.18, 0.045, "white", 32, rot=(math.radians(62), 0, 0), bevel=0.004)
    cylinder_between("L5_TempGauge_Needle", (1.78, 1.31, 3.66), (1.86, 1.34, 3.75), 0.006, "red", 8, bevel=0.001)
    cylinder_between("L5_Top_ReliefValve_Neck", (-0.95, 0.00, 3.32), (-0.95, 0.00, 3.82), 0.08, "pipe", 16, bevel=0.002)
    cylinder("L5_Top_ReliefValve_Cap", (-0.95, 0.00, 3.92), 0.20, 0.20, "brass", 22, bevel=0.006)
    cylinder_between("L5_Condensate_Drain_Line", (-2.80, 0.85, 1.48), (-3.20, 1.42, 0.70), 0.045, "pipe", 12, bevel=0.002)
    make_handwheel("L5_Drain_Valve_Handwheel", (-3.25, 1.48, 0.70), 0.13, "yellow", rot=(math.radians(32), 0, math.radians(35)))

    # Operator access details.
    box("L5_Service_Platform_Grating", (2.42, -1.78, 2.04), (2.20, 0.82, 0.10), "dark", bevel=0.006)
    for x in (1.42, 2.42, 3.42):
        cylinder_between(f"L5_Platform_Post_{x:+.1f}", (x, -2.16, 0.52), (x, -2.16, 2.82), 0.035, "dark", 10, bevel=0.001)
    cylinder_between("L5_Platform_TopRail", (1.28, -2.16, 2.82), (3.58, -2.16, 2.82), 0.035, "yellow", 10, bevel=0.001)
    cylinder_between("L5_Platform_MidRail", (1.28, -2.16, 2.25), (3.58, -2.16, 2.25), 0.028, "yellow", 10, bevel=0.001)
    for i in range(5):
        box(f"L5_Service_Ladder_Rung_{i:02d}", (3.70, -1.77, 0.82 + i * 0.34), (0.58, 0.055, 0.055), "yellow", bevel=0.002)
    cylinder_between("L5_Service_Ladder_Rail_L", (3.44, -1.77, 0.62), (3.44, -1.77, 2.40), 0.030, "yellow", 10, bevel=0.001)
    cylinder_between("L5_Service_Ladder_Rail_R", (3.96, -1.77, 0.62), (3.96, -1.77, 2.40), 0.030, "yellow", 10, bevel=0.001)

    # Control panel and nameplates.
    box("L5_Local_Control_Panel_Post", (-4.05, -1.42, 1.06), (0.06, 0.06, 1.16), "dark", bevel=0.004)
    box("L5_Local_Control_Panel_Box", (-4.05, -1.50, 1.80), (0.52, 0.16, 0.48), "steel", bevel=0.012)
    cylinder("L5_Local_Control_EStop", (-4.22, -1.59, 1.88), 0.055, 0.020, "red", 18, rot=(math.radians(90), 0, 0), bevel=0.002)
    cylinder("L5_Local_Control_RunLamp", (-4.05, -1.59, 1.88), 0.040, 0.018, "green", 16, rot=(math.radians(90), 0, 0), bevel=0.002)
    cylinder("L5_Local_Control_ReadyLamp", (-3.88, -1.59, 1.88), 0.040, 0.018, "glass", 16, rot=(math.radians(90), 0, 0), bevel=0.002)
    box("L5_PreHeater_Nameplate_Label", (0.0, -1.16, 2.18), (1.28, 0.036, 0.30), "label", bevel=0.004)
    label_text("L5_Label_PREHEATER", "PRE-HEATER", (0.0, -1.185, 2.25), 0.115, "black", rot=(math.radians(90), 0, 0))
    label_text("L5_Label_H501", "H-501  STEAM", (0.0, -1.188, 2.07), 0.075, "black", rot=(math.radians(90), 0, 0))

    # Subtle wear, no messy clutter.
    for i, loc in enumerate([(-2.6, -1.07, 1.25), (0.7, 1.06, 1.24), (3.1, -0.9, 1.24)]):
        box(f"L5_Subtle_RustWearPatch_{i:02d}", loc, (0.32, 0.020, 0.10), "rust", bevel=0.002)

    # Upgrade pass: cleaner industrial envelope and readable preheater details.
    box("L5_PreHeater_Extended_Service_Pad", (0.0, 0.0, 0.10), (10.85, 4.10, 0.20), "concrete", bevel=0.018)
    for x in (-4.45, 4.45):
        for y in (-1.62, 1.62):
            box(f"L5_ServicePad_CornerAnchor_{x:+.1f}_{y:+.1f}", (x, y, 0.27), (0.16, 0.16, 0.035), "steel", bevel=0.002)

    for i, x in enumerate([-3.00, -1.74, -0.48, 0.78, 2.04, 3.30]):
        box(f"L5_Insulation_Panel_Seam_{i:02d}", (x, -1.138, 2.25), (0.032, 0.026, 1.34), "dark", bevel=0.001)
        cylinder(f"HeatingFin_JacketClamp_{i:02d}", (x + 0.46, 0.0, 2.25), 1.205, 0.045, "heated", 48, rot=(0, math.radians(90), 0), bevel=0.004)

    tube_face("L5_Left_Exposed", -4.20, -1)
    tube_face("L5_Right_Exposed", 4.20, 1)
    for y in (-0.60, -0.30, 0.0, 0.30, 0.60):
        cylinder_between(f"L5_Left_Exposed_TubeStub_{y:+.2f}", (-4.24, y, 2.25), (-4.76, y, 2.25), 0.043, "pipe", 12, bevel=0.001)
        cylinder_between(f"L5_Right_Exposed_TubeStub_{y:+.2f}", (4.24, y, 2.25), (4.76, y, 2.25), 0.043, "pipe", 12, bevel=0.001)

    cylinder_between("Pipe_Preheater_Inlet_CleanTieIn", (-5.10, -0.95, 2.25), (-4.05, -0.95, 2.25), 0.205, "pipe", 30, bevel=0.004)
    cylinder("L5_CleanInlet_Flange", (-5.12, -0.95, 2.25), 0.35, 0.12, "steel", 34, rot=(0, math.radians(90), 0), bevel=0.006)
    bolt_ring("L5_CleanInlet_Flange", -5.20, -0.95, 2.25, 0.285, 10, axis="x", panel="steel", bolt_radius=0.018)
    cylinder_between("Pipe_Preheater_Outlet_CleanRiser", (3.28, 0.0, 3.10), (3.28, 0.0, 4.08), 0.185, "pipe", 28, bevel=0.004)
    elbow_arc("Pipe_Preheater_Outlet_CleanElbow", (3.28, 0.36, 4.08), 0.36, 0.185, "pipe", math.radians(180), math.radians(90), plane="yz", segments=18, ring=12)
    cylinder_between("Pipe_Preheater_Outlet_CleanHeader", (3.28, 0.36, 4.44), (5.08, 0.36, 4.44), 0.185, "pipe", 28, bevel=0.004)
    cylinder("L5_CleanOutlet_Flange", (5.15, 0.36, 4.44), 0.34, 0.12, "steel", 32, rot=(0, math.radians(90), 0), bevel=0.006)
    for i, x in enumerate([3.66, 4.46]):
        pipe_support(f"L5_OutletPipeRack_Support_{i:02d}", x, 0.36, 0.24, 4.20, span=0.72)

    cylinder("L5_Condensate_Pot_VerticalVessel", (-3.62, 1.44, 1.08), 0.28, 1.14, "steel", 28, bevel=0.006)
    sphere("L5_Condensate_Pot_TopDome", (-3.62, 1.44, 1.66), 0.28, "steel", 24, 8, scale=(1, 1, 0.45), bevel=0.002)
    sphere("L5_Condensate_Pot_BottomDome", (-3.62, 1.44, 0.50), 0.28, "steel", 24, 8, scale=(1, 1, 0.45), bevel=0.002)
    cylinder_between("L5_Condensate_Return_CleanLine", (-2.80, 0.85, 1.48), (-3.42, 1.34, 1.32), 0.050, "pipe", 12, bevel=0.002)
    cylinder_between("L5_Condensate_Drain_ToSkid", (-3.62, 1.44, 0.40), (-3.62, 1.88, 0.40), 0.045, "pipe", 12, bevel=0.002)
    make_handwheel("L5_Condensate_Drain_Handwheel", (-3.62, 1.94, 0.48), 0.13, "yellow", rot=(math.radians(90), 0, 0))

    cylinder_between("L5_PressureTap_CleanLine", (-1.34, 0.76, 3.04), (-1.10, 1.10, 3.42), 0.028, "brass", 12, bevel=0.001)
    cylinder("L5_PressureGauge_Dial", (-1.04, 1.20, 3.50), 0.15, 0.040, "white", 28, rot=(math.radians(62), 0, 0), bevel=0.004)
    cylinder_between("L5_Relief_Discharge_Stack", (-0.95, 0.00, 4.02), (-0.95, 0.72, 4.36), 0.055, "pipe", 14, bevel=0.002)

    build_grating("L5_Service_Platform_ExpandedGrating", (2.40, -1.82), 2.70, 0.92, 2.10)
    build_guardrail("L5_Service_Platform_CleanFrontRail", 1.12, 3.70, -2.28, 2.10, 2.88)
    build_guardrail("L5_Service_Platform_CleanBackRail", 1.12, 3.70, -1.38, 2.10, 2.80)
    box("L5_Service_Stair_LowerLanding", (1.06, -2.02, 0.58), (0.82, 0.74, 0.10), "dark", bevel=0.004)
    for i in range(5):
        box(f"L5_Service_Stair_Tread_{i:02d}", (1.20 + i * 0.22, -2.02, 0.72 + i * 0.28), (0.74, 0.34, 0.075), "dark", bevel=0.004)

    # Preview render for quick QA outside Unity.
    bpy.ops.object.light_add(type="AREA", location=(1.5, -4.6, 6.0))
    lamp = bpy.context.object
    lamp.name = "L5_PreHeater_Preview_AreaLight"
    lamp.data.energy = 360.0
    lamp.data.size = 5.0
    put(lamp)
    bpy.ops.object.camera_add(location=(7.5, -6.4, 4.6), rotation=(math.radians(62), 0, math.radians(47)))
    camera = bpy.context.object
    camera.name = "L5_PreHeater_Preview_Camera"
    bpy.context.scene.camera = camera
    put(camera)
    bpy.context.scene.render.resolution_x = 1280
    bpy.context.scene.render.resolution_y = 720
    bpy.context.scene.render.filepath = PREVIEW_PATH

    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    if os.environ.get("OLIVIA_RENDER_PREVIEW") == "1":
        bpy.ops.render.render(write_still=True)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH,
        use_selection=False,
        object_types={"EMPTY", "MESH"},
        apply_unit_scale=True,
        bake_space_transform=False,
        add_leaf_bones=False,
        use_mesh_modifiers=True,
        path_mode="RELATIVE",
    )
    bpy.ops.export_scene.gltf(filepath=GLB_PATH, export_format="GLB", export_apply=True, export_yup=True)
    print("[OK] Level 5 PreHeater exported")
    print(FBX_PATH)
    print(GLB_PATH)
    print(BLEND_PATH)
    print(ATLAS_PATH)
    print(PREVIEW_PATH)


if __name__ == "__main__":
    build_asset()
