import math
import os
import random

import bpy
from mathutils import Vector


SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
SOURCE_DIR = os.path.abspath(os.path.join(SCRIPT_DIR, "..", "..", "..", "BlenderSources", "Level4SlurryPumpBlender"))
FBX_PATH = os.path.join(SCRIPT_DIR, "level4_slurry_pump_industrial_uv.fbx")
GLB_PATH = os.path.join(SCRIPT_DIR, "level4_slurry_pump_industrial_uv.glb")
BLEND_PATH = os.path.join(SOURCE_DIR, "level4_slurry_pump_industrial_uv.blend")
ATLAS_PATH = os.path.join(SCRIPT_DIR, "level4_slurry_pump_uv_atlas.png")
PREVIEW_PATH = os.path.join(SCRIPT_DIR, "level4_slurry_pump_preview.png")

PANELS = {
    "steel": (0, 0),
    "dark": (1, 0),
    "yellow": (2, 0),
    "blue": (3, 0),
    "concrete": (0, 1),
    "pipe": (1, 1),
    "rubber": (2, 1),
    "slurry": (3, 1),
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
    random.seed(404)
    size = 1024
    img = bpy.data.images.new("level4_slurry_pump_uv_atlas", size, size, alpha=True)
    pixels = [0.0] * (size * size * 4)
    base = {
        "steel": (0.55, 0.60, 0.58, 1.0),
        "dark": (0.08, 0.10, 0.10, 1.0),
        "yellow": (0.98, 0.66, 0.05, 1.0),
        "blue": (0.05, 0.22, 0.42, 1.0),
        "concrete": (0.42, 0.41, 0.37, 1.0),
        "pipe": (0.48, 0.55, 0.57, 1.0),
        "rubber": (0.015, 0.015, 0.014, 1.0),
        "slurry": (0.45, 0.24, 0.48, 1.0),
        "red": (0.78, 0.04, 0.035, 1.0),
        "green": (0.05, 0.55, 0.22, 1.0),
        "white": (0.86, 0.88, 0.84, 1.0),
        "black": (0.01, 0.01, 0.01, 1.0),
        "brass": (0.86, 0.58, 0.18, 1.0),
        "glass": (0.20, 0.58, 0.74, 0.82),
        "rust": (0.54, 0.24, 0.10, 1.0),
        "label": (0.92, 0.88, 0.70, 1.0),
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
                    stripe = math.sin(nx * math.tau * 9.0) * 0.015
                    rr += stripe
                    gg += stripe
                    bb += stripe
                    if random.random() < 0.018:
                        rr *= 0.62
                        gg *= 0.62
                        bb *= 0.62
                elif name == "yellow":
                    if ((x - x0 + y - y0) // 36) % 2 == 0:
                        rr, gg, bb = 0.08, 0.075, 0.055
                elif name == "blue":
                    panel_line = 0.07 if (x - x0) % 82 < 4 or (y - y0) % 82 < 4 else 0.0
                    rr += panel_line
                    gg += panel_line
                    bb += panel_line
                elif name == "concrete":
                    if random.random() < 0.08:
                        rr, gg, bb = 0.28, 0.27, 0.25
                elif name == "slurry":
                    swirl = math.sin((nx * 5.0 + ny * 2.4) * math.tau) * 0.045
                    rr += swirl
                    bb += swirl * 1.4
                elif name == "glass":
                    glint = 0.09 if (x - x0 + y - y0) % 64 < 7 else 0.0
                    rr += glint
                    gg += glint
                    bb += glint
                elif name == "rust":
                    if random.random() < 0.20:
                        rr, gg, bb = 0.66, 0.31, 0.13
                elif name == "label":
                    if (x - x0) % 62 < 3 or (y - y0) % 62 < 3:
                        rr, gg, bb = 0.18, 0.14, 0.08
                i = (y * size + x) * 4
                pixels[i:i + 4] = [clamp01(rr), clamp01(gg), clamp01(bb), a]

    img.pixels.foreach_set(pixels)
    img.filepath_raw = ATLAS_PATH
    img.file_format = "PNG"
    img.save()
    return img


def create_material(image):
    mat = bpy.data.materials.new("M_Level4_SlurryPump_UVAtlas")
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
            bsdf.inputs["Metallic"].default_value = 0.22
        if "Roughness" in bsdf.inputs:
            bsdf.inputs["Roughness"].default_value = 0.58
    mat.diffuse_color = (0.45, 0.50, 0.50, 1.0)
    return mat


def create_root():
    global ROOT_COLLECTION, ROOT_EMPTY
    ROOT_COLLECTION = bpy.data.collections.new("Level4_SlurryPump_Industrial_UV")
    bpy.context.scene.collection.children.link(ROOT_COLLECTION)
    ROOT_EMPTY = bpy.data.objects.new("L4_SlurryPump_Industrial_Root", None)
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
        bpy.ops.uv.smart_project(angle_limit=math.radians(68), island_margin=0.018)
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


def sphere(name, loc, radius, panel, segments=16, rings=8, scale=(1, 1, 1), bevel=0.0):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=radius, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish_mesh(obj, panel, bevel=bevel)


def torus(name, loc, major, minor, panel, major_segments=48, minor_segments=10, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(
        major_segments=major_segments,
        minor_segments=minor_segments,
        major_radius=major,
        minor_radius=minor,
        location=loc,
        rotation=rot,
    )
    obj = bpy.context.object
    obj.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return finish_mesh(obj, panel, bevel=0.0)


def cylinder_between(name, start, end, radius, panel, vertices=24, bevel=0.0):
    a = Vector(start)
    b = Vector(end)
    mid = (a + b) * 0.5
    direction = b - a
    length = direction.length
    if length <= 0.0001:
        return None
    quat = direction.to_track_quat("Z", "Y")
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=length,
        location=mid,
        rotation=quat.to_euler(),
    )
    obj = bpy.context.object
    obj.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return finish_mesh(obj, panel, bevel=bevel)


def volute_shell():
    center = Vector((-0.56, 0.0, 1.08))
    depth = 0.44
    inner = 0.34
    steps = 72
    theta0 = math.radians(-58)
    theta1 = theta0 + math.tau
    verts = []
    for x in (-depth * 0.5, depth * 0.5):
        for i in range(steps):
            t = i / steps
            theta = theta0 + t * (theta1 - theta0)
            outer = 0.56 + 0.23 * t
            y = math.cos(theta)
            z = math.sin(theta)
            verts.append((center.x + x, center.y + y * outer, center.z + z * outer))
        for i in range(steps):
            theta = theta0 + (i / steps) * (theta1 - theta0)
            y = math.cos(theta)
            z = math.sin(theta)
            verts.append((center.x + x, center.y + y * inner, center.z + z * inner))

    faces = []
    front_outer = 0
    front_inner = steps
    back_outer = steps * 2
    back_inner = steps * 3
    for i in range(steps):
        j = (i + 1) % steps
        faces.append((front_outer + i, front_outer + j, front_inner + j, front_inner + i))
        faces.append((back_outer + j, back_outer + i, back_inner + i, back_inner + j))
        faces.append((front_outer + j, front_outer + i, back_outer + i, back_outer + j))
        faces.append((front_inner + i, front_inner + j, back_inner + j, back_inner + i))

    mesh = bpy.data.meshes.new("L4_VoluteShell_Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new("L4_SlurryPump_Blue_Volute_Casing", mesh)
    return finish_mesh(obj, "blue", bevel=0.018)


def elbow_pipe(name, start, bend_radius, pipe_radius, panel, segments=18, ring=12):
    sx, sy, sz = start
    verts = []
    faces = []
    for i in range(segments + 1):
        a = (math.pi * 0.5) * (i / segments)
        center = Vector((sx, sy + bend_radius * (1.0 - math.cos(a)), sz + bend_radius * math.sin(a)))
        normal = Vector((0.0, -math.cos(a), math.sin(a))).normalized()
        binormal = Vector((1.0, 0.0, 0.0))
        for j in range(ring):
            p = math.tau * (j / ring)
            pos = center + normal * (math.cos(p) * pipe_radius) + binormal * (math.sin(p) * pipe_radius)
            verts.append(tuple(pos))
    for i in range(segments):
        for j in range(ring):
            a = i * ring + j
            b = i * ring + ((j + 1) % ring)
            c = (i + 1) * ring + ((j + 1) % ring)
            d = (i + 1) * ring + j
            faces.append((a, b, c, d))
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    return finish_mesh(obj, panel, bevel=0.0)


def rotor_mesh():
    verts = []
    faces = []
    blade_count = 6
    half_x = 0.035
    inner = 0.10
    outer = 0.28
    width_ang = math.radians(18)
    for i in range(blade_count):
        base = len(verts)
        ang = i * math.tau / blade_count
        pts = []
        for x in (-half_x, half_x):
            for rr, da in ((inner, -width_ang), (outer, -width_ang * 0.25), (outer, width_ang * 0.65), (inner, width_ang)):
                a = ang + da
                pts.append((x, math.cos(a) * rr, math.sin(a) * rr))
        verts.extend(pts)
        faces.extend([
            (base + 0, base + 1, base + 2, base + 3),
            (base + 4, base + 7, base + 6, base + 5),
            (base + 0, base + 4, base + 5, base + 1),
            (base + 1, base + 5, base + 6, base + 2),
            (base + 2, base + 6, base + 7, base + 3),
            (base + 3, base + 7, base + 4, base + 0),
        ])
    mesh = bpy.data.meshes.new("ImpellerPivot_L4_Rotor_Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new("ImpellerPivot_L4_SlurryPump_Rotor", mesh)
    obj.location = (0.10, 0.0, 1.08)
    return finish_mesh(obj, "yellow", bevel=0.006)


def bolt_ring(prefix, x, center_y, center_z, radius, count, panel="steel", bolt_radius=0.035):
    for i in range(count):
        a = math.tau * i / count
        y = center_y + math.cos(a) * radius
        z = center_z + math.sin(a) * radius
        cylinder(f"{prefix}_Bolt_{i:02d}", (x, y, z), bolt_radius, 0.055, panel, 10, rot=(0, math.radians(90), 0), bevel=0.004)


def label_text(name, text, loc, size, panel, rot=(0, 0, 0)):
    font_curve = bpy.data.curves.new(name, "FONT")
    font_curve.body = text
    font_curve.align_x = "CENTER"
    font_curve.align_y = "CENTER"
    font_curve.size = size
    font_curve.extrude = 0.006
    obj = bpy.data.objects.new(name, font_curve)
    obj.location = loc
    obj.rotation_euler = rot
    bpy.context.collection.objects.link(obj)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.convert(target="MESH")
    obj = bpy.context.object
    return finish_mesh(obj, panel, bevel=0.001, shade=False)


def build_asset():
    clear_scene()
    os.makedirs(SOURCE_DIR, exist_ok=True)
    create_root()
    global ATLAS_MATERIAL
    ATLAS_MATERIAL = create_material(create_atlas())

    # Foundation and skid.
    box("L4_Pump_Concrete_Foundation", (0.05, 0.0, 0.09), (4.05, 1.78, 0.18), "concrete", bevel=0.02)
    box("L4_Pump_Grout_Pad_Pump", (-0.50, 0.0, 0.24), (1.55, 1.05, 0.13), "concrete", bevel=0.018)
    box("L4_Pump_Grout_Pad_Motor", (1.03, 0.0, 0.24), (1.55, 1.05, 0.13), "concrete", bevel=0.018)
    for y in (-0.62, 0.62):
        box(f"L4_Skid_Long_IBeam_{y:+.2f}", (0.06, y, 0.38), (3.75, 0.13, 0.20), "dark", bevel=0.014)
    for x in (-1.35, -0.35, 0.75, 1.55):
        box(f"L4_Skid_CrossMember_{x:+.2f}", (x, 0.0, 0.42), (0.12, 1.38, 0.18), "dark", bevel=0.012)
    for x in (-1.72, 1.82):
        for y in (-0.72, 0.72):
            cylinder(f"L4_AnchorBolt_{x:+.1f}_{y:+.1f}", (x, y, 0.24), 0.045, 0.12, "steel", 10, bevel=0.004)
            cylinder(f"L4_AnchorWasher_{x:+.1f}_{y:+.1f}", (x, y, 0.31), 0.075, 0.018, "steel", 14, bevel=0.002)

    # Pump casing and suction side.
    volute_shell()
    cylinder("L4_Pump_Front_Cover_Ring", (-0.81, 0.0, 1.08), 0.55, 0.12, "blue", 48, rot=(0, math.radians(90), 0), bevel=0.014)
    cylinder("L4_Pump_Back_Cover_Ring", (-0.31, 0.0, 1.08), 0.49, 0.10, "blue", 48, rot=(0, math.radians(90), 0), bevel=0.012)
    cylinder("L4_Pump_Center_Hub_Dark", (-0.90, 0.0, 1.08), 0.23, 0.13, "dark", 36, rot=(0, math.radians(90), 0), bevel=0.01)
    cylinder_between("L4_Suction_Nozzle_Pipe", (-1.55, 0.0, 1.08), (-0.92, 0.0, 1.08), 0.21, "pipe", 30, bevel=0.006)
    cylinder("L4_Suction_Flange_Outer", (-1.58, 0.0, 1.08), 0.39, 0.12, "steel", 36, rot=(0, math.radians(90), 0), bevel=0.01)
    cylinder("L4_Suction_Gasket_Rubber", (-1.66, 0.0, 1.08), 0.34, 0.035, "rubber", 32, rot=(0, math.radians(90), 0), bevel=0.003)
    bolt_ring("L4_Suction_Flange", -1.66, 0.0, 1.08, 0.31, 10, "steel", 0.026)

    # Discharge pipe with elbow and flange.
    cylinder_between("L4_Discharge_Riser_Pipe", (-0.55, 0.0, 1.60), (-0.55, 0.0, 2.12), 0.17, "pipe", 28, bevel=0.004)
    elbow_pipe("L4_Discharge_Elbow_90deg", (-0.55, 0.0, 2.12), 0.36, 0.17, "pipe", 18, 12)
    cylinder_between("L4_Discharge_Outlet_Pipe", (-0.55, 0.36, 2.48), (-0.55, 0.86, 2.48), 0.17, "pipe", 28, bevel=0.004)
    cylinder("L4_Discharge_Flange_Top", (-0.55, 0.88, 2.48), 0.31, 0.10, "steel", 32, rot=(math.radians(90), 0, 0), bevel=0.008)
    cylinder("L4_Discharge_Gasket_Rubber", (-0.55, 0.94, 2.48), 0.26, 0.035, "rubber", 32, rot=(math.radians(90), 0, 0), bevel=0.003)
    for i in range(8):
        a = math.tau * i / 8
        cylinder(
            f"L4_Discharge_Flange_Bolt_{i:02d}",
            (-0.55 + math.cos(a) * 0.24, 0.95, 2.48 + math.sin(a) * 0.24),
            0.022,
            0.055,
            "steel",
            10,
            rot=(math.radians(90), 0, 0),
            bevel=0.003,
        )

    # Bearing, shaft, motor, fan, and guard.
    box("L4_Bearing_Pedestal_Block", (-0.03, 0.0, 0.78), (0.45, 0.48, 0.36), "dark", bevel=0.02)
    cylinder("L4_Bearing_Housing_Steel", (-0.02, 0.0, 1.08), 0.23, 0.34, "steel", 28, rot=(0, math.radians(90), 0), bevel=0.012)
    cylinder_between("L4_Drive_Shaft_Visible", (-0.20, 0.0, 1.08), (0.72, 0.0, 1.08), 0.055, "steel", 18, bevel=0.002)
    cylinder("L4_Coupling_Guard_Yellow_Striped", (0.31, 0.0, 1.08), 0.31, 0.70, "yellow", 32, rot=(0, math.radians(90), 0), bevel=0.018)
    rotor_mesh()
    cylinder("L4_DriveMotor_Casing", (1.12, 0.0, 1.08), 0.37, 1.36, "dark", 36, rot=(0, math.radians(90), 0), bevel=0.018)
    cylinder("L4_DriveMotor_Front_Endbell", (0.42, 0.0, 1.08), 0.39, 0.10, "steel", 36, rot=(0, math.radians(90), 0), bevel=0.01)
    cylinder("L4_DriveMotor_Rear_FanCover", (1.83, 0.0, 1.08), 0.40, 0.14, "steel", 36, rot=(0, math.radians(90), 0), bevel=0.014)
    for i in range(8):
        x = 0.66 + i * 0.13
        box(f"L4_Motor_CoolingFin_Top_{i:02d}", (x, 0.0, 1.47), (0.045, 0.55, 0.075), "steel", bevel=0.004)
    for side, y in (("L", -0.39), ("R", 0.39)):
        for i in range(5):
            x = 0.74 + i * 0.18
            box(f"L4_Motor_CoolingFin_{side}_{i:02d}", (x, y, 1.08), (0.045, 0.055, 0.48), "steel", bevel=0.004)
    for zoff in (-0.19, 0.0, 0.19):
        box(f"L4_FanCover_Grille_Bar_{zoff:+.2f}", (1.91, 0.0, 1.08 + zoff), (0.025, 0.72, 0.025), "black", bevel=0.002)
    box("L4_Motor_Terminal_Box", (1.10, -0.36, 1.46), (0.46, 0.18, 0.24), "steel", bevel=0.012)
    cylinder_between("L4_Motor_Cable_Conduit", (1.10, -0.46, 1.42), (1.10, -0.78, 0.65), 0.035, "rubber", 12, bevel=0.002)

    # Gauges, panel, drain and service details.
    cylinder_between("L4_Pressure_Tap_Pipe", (-0.55, 0.18, 1.56), (-0.55, 0.44, 1.86), 0.025, "pipe", 12, bevel=0.002)
    cylinder("L4_Pressure_Gauge_Dial", (-0.55, 0.50, 1.91), 0.13, 0.035, "white", 28, rot=(math.radians(90), 0, 0), bevel=0.004)
    cylinder_between("L4_Pressure_Gauge_Needle", (-0.55, 0.525, 1.91), (-0.50, 0.525, 1.98), 0.006, "red", 8, bevel=0.001)
    box("L4_Local_Control_Panel_Post", (1.72, -0.72, 0.77), (0.07, 0.07, 0.86), "dark", bevel=0.006)
    box("L4_Local_Control_Panel_Box", (1.72, -0.76, 1.30), (0.48, 0.16, 0.42), "steel", bevel=0.014)
    cylinder("L4_Local_Control_EStop", (1.58, -0.855, 1.35), 0.055, 0.020, "red", 18, rot=(math.radians(90), 0, 0), bevel=0.002)
    cylinder("L4_Local_Control_RunLamp", (1.72, -0.855, 1.35), 0.040, 0.018, "green", 16, rot=(math.radians(90), 0, 0), bevel=0.002)
    cylinder("L4_Local_Control_ReadyLamp", (1.86, -0.855, 1.35), 0.040, 0.018, "glass", 16, rot=(math.radians(90), 0, 0), bevel=0.002)
    cylinder_between("L4_Drain_Line_Low", (-0.70, -0.17, 0.76), (-1.10, -0.62, 0.42), 0.035, "pipe", 12, bevel=0.002)
    cylinder("L4_Drain_Valve_Handwheel", (-1.12, -0.64, 0.42), 0.10, 0.025, "yellow", 18, rot=(math.radians(55), 0, math.radians(40)), bevel=0.002)
    box("L4_Skid_Nameplate_Label", (-0.43, -0.67, 0.56), (0.88, 0.035, 0.22), "label", bevel=0.004)
    label_text("L4_Label_SLURRY_PUMP", "SLURRY PUMP", (-0.43, -0.692, 0.59), 0.095, "black", rot=(math.radians(90), 0, 0))
    label_text("L4_Label_P401", "P-401", (-0.43, -0.695, 0.47), 0.075, "black", rot=(math.radians(90), 0, 0))

    # Service guards and tiny rust/wear cues.
    for x in (-0.95, -0.20, 0.52, 1.40):
        box(f"L4_Yellow_Service_Step_{x:+.2f}", (x, 0.73, 0.54), (0.52, 0.08, 0.08), "yellow", bevel=0.006)
    for i, loc in enumerate([(-0.95, -0.50, 0.48), (-0.35, 0.55, 0.48), (1.45, -0.48, 0.48)]):
        box(f"L4_Subtle_RustWearPatch_{i:02d}", loc, (0.22, 0.018, 0.08), "rust", bevel=0.002)

    # Lighting and camera for generated preview.
    bpy.ops.object.light_add(type="AREA", location=(0.5, -3.8, 5.0))
    light = bpy.context.object
    light.name = "Preview_Key_AreaLight"
    light.data.energy = 600
    light.data.size = 4.5
    bpy.ops.object.camera_add(location=(4.2, -5.2, 3.0), rotation=(math.radians(60), 0, math.radians(39)))
    cam = bpy.context.object
    direction = Vector((0.0, 0.0, 1.15)) - cam.location
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = cam

    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    bpy.context.scene.render.resolution_x = 1400
    bpy.context.scene.render.resolution_y = 900
    try:
        bpy.context.scene.render.engine = "BLENDER_EEVEE_NEXT"
    except Exception:
        pass
    try:
        bpy.context.scene.eevee.taa_render_samples = 32
    except Exception:
        pass

    # Save and export.
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
    bpy.ops.export_scene.gltf(
        filepath=GLB_PATH,
        export_format="GLB",
        export_apply=True,
        export_yup=True,
    )
    # Preview render is intentionally skipped in headless mode on this machine:
    # Blender 5.1 can crash inside the GPU driver after export.
    print("[INFO] Preview render skipped; Unity screenshot will be used for verification.")

    print("[OK] Level 4 slurry pump exported")
    print(FBX_PATH)
    print(GLB_PATH)
    print(BLEND_PATH)
    print(ATLAS_PATH)
    print(PREVIEW_PATH)


if __name__ == "__main__":
    build_asset()
