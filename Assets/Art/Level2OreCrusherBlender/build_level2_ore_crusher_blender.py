import math
import os
import random

import bpy
from mathutils import Vector


SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
FBX_PATH = os.path.join(SCRIPT_DIR, "level2_ore_crusher_industrial_uv.fbx")
BLEND_PATH = os.path.join(SCRIPT_DIR, "level2_ore_crusher_industrial_uv.blend")
ATLAS_PATH = os.path.join(SCRIPT_DIR, "level2_ore_crusher_uv_atlas.png")


PANELS = {
    "steel": (0, 0),
    "dark": (1, 0),
    "hazard": (2, 0),
    "rubber": (3, 0),
    "concrete": (0, 1),
    "ore": (1, 1),
    "red": (2, 1),
    "blue": (3, 1),
    "yellow": (0, 2),
    "grating": (1, 2),
    "motor": (2, 2),
    "black": (3, 2),
}


def panel_rect(name, pad=0.018):
    col, row = PANELS[name]
    cell = 0.25
    u0 = col * cell + pad
    v0 = row * cell + pad
    return (u0, v0, u0 + cell - pad * 2, v0 + cell - pad * 2)


def create_atlas():
    size = 1024
    image = bpy.data.images.new("level2_ore_crusher_uv_atlas", size, size, alpha=True)
    pixels = [0.0] * (size * size * 4)

    base = {
        "steel": (0.62, 0.69, 0.67, 1.0),
        "dark": (0.08, 0.095, 0.09, 1.0),
        "hazard": (1.00, 0.72, 0.03, 1.0),
        "rubber": (0.015, 0.014, 0.012, 1.0),
        "concrete": (0.46, 0.44, 0.39, 1.0),
        "ore": (0.34, 0.22, 0.12, 1.0),
        "red": (0.72, 0.04, 0.025, 1.0),
        "blue": (0.05, 0.22, 0.42, 1.0),
        "yellow": (1.00, 0.67, 0.02, 1.0),
        "grating": (0.20, 0.22, 0.22, 1.0),
        "motor": (0.04, 0.18, 0.28, 1.0),
        "black": (0.02, 0.02, 0.018, 1.0),
    }

    for name, (col, row) in PANELS.items():
        x0 = int(col * size / 4)
        y0 = int(row * size / 4)
        x1 = int((col + 1) * size / 4)
        y1 = int((row + 1) * size / 4)
        r, g, b, a = base[name]
        for y in range(y0, y1):
            for x in range(x0, x1):
                noise = (random.random() - 0.5) * 0.045
                rr, gg, bb = r + noise, g + noise, b + noise
                if name == "hazard" and ((x - x0 + y - y0) // 28) % 2 == 0:
                    rr, gg, bb = 0.04, 0.035, 0.025
                if name == "grating" and ((x - x0) % 36 < 5 or (y - y0) % 36 < 5):
                    rr, gg, bb = 0.55, 0.57, 0.55
                if name == "ore" and random.random() < 0.12:
                    rr, gg, bb = 0.50, 0.34, 0.18
                if name == "concrete" and random.random() < 0.09:
                    rr, gg, bb = 0.30, 0.29, 0.27
                i = (y * size + x) * 4
                pixels[i:i + 4] = [max(0, min(1, rr)), max(0, min(1, gg)), max(0, min(1, bb)), a]

    image.pixels.foreach_set(pixels)
    image.filepath_raw = ATLAS_PATH
    image.file_format = "PNG"
    image.save()
    return image


def create_material(image):
    mat = bpy.data.materials.new("M_Level2_OreCrusher_UVAtlas")
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
    tex.image = image
    mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.72
    bsdf.inputs["Metallic"].default_value = 0.18
    return mat


def assign_uv(obj, rect_name, mat):
    if obj.type != "MESH":
        return obj
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    uv_layer = obj.data.uv_layers.new(name="UVAtlas") if not obj.data.uv_layers else obj.data.uv_layers[0]
    u0, v0, u1, v1 = panel_rect(rect_name)
    corners = [(u0, v0), (u1, v0), (u1, v1), (u0, v1)]
    for poly in obj.data.polygons:
        for idx, li in enumerate(poly.loop_indices):
            uv_layer.data[li].uv = corners[idx % 4]
    return obj


def add_bevel(obj, amount=0.025, segments=4):
    bevel = obj.modifiers.new("soft_industrial_bevel", "BEVEL")
    bevel.width = amount
    bevel.segments = segments
    bevel.affect = "EDGES"
    obj.modifiers.new("weighted_normals", "WEIGHTED_NORMAL")
    for poly in obj.data.polygons:
        poly.use_smooth = True
    return obj


def box(name, loc, scale, rect, mat, rot=(0, 0, 0), bevel=0.015):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    assign_uv(obj, rect, mat)
    if bevel:
        add_bevel(obj, bevel)
    return obj


def cylinder(name, loc, radius, depth, rect, mat, vertices=32, rot=(0, 0, 0), bevel=False):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=loc, rotation=rot)
    obj = bpy.context.object
    obj.name = name
    assign_uv(obj, rect, mat)
    for poly in obj.data.polygons:
        poly.use_smooth = True
    if bevel:
        add_bevel(obj, 0.025, 3)
    return obj


def sphere(name, loc, radius, rect, mat, segments=2):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=segments, radius=radius, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.scale.x *= random.uniform(0.8, 1.25)
    obj.scale.y *= random.uniform(0.75, 1.18)
    obj.scale.z *= random.uniform(0.45, 0.9)
    assign_uv(obj, rect, mat)
    for poly in obj.data.polygons:
        poly.use_smooth = True
    return obj


def cylinder_between(name, a, b, radius, rect, mat, vertices=16):
    a = Vector(a)
    b = Vector(b)
    mid = (a + b) * 0.5
    direction = b - a
    depth = direction.length
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=mid)
    obj = bpy.context.object
    obj.name = name
    quat = Vector((0, 0, 1)).rotation_difference(direction.normalized())
    obj.rotation_euler = quat.to_euler()
    assign_uv(obj, rect, mat)
    for poly in obj.data.polygons:
        poly.use_smooth = True
    return obj


def box_between(name, a, b, width, height, rect, mat):
    a = Vector(a)
    b = Vector(b)
    direction = b - a
    length = direction.length
    mid = (a + b) * 0.5
    bpy.ops.mesh.primitive_cube_add(size=1, location=mid)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = (length, width, height)
    obj.rotation_euler = direction.to_track_quat("X", "Z").to_euler()
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    assign_uv(obj, rect, mat)
    add_bevel(obj, 0.012)
    return obj


def hopper_mesh(name, center, top_size, bottom_size, z_bottom, z_top, rect, mat):
    cx, cy, _ = center
    tx, ty = top_size[0] * 0.5, top_size[1] * 0.5
    bx, by = bottom_size[0] * 0.5, bottom_size[1] * 0.5
    verts = [
        (cx - bx, cy - by, z_bottom), (cx + bx, cy - by, z_bottom),
        (cx + bx, cy + by, z_bottom), (cx - bx, cy + by, z_bottom),
        (cx - tx, cy - ty, z_top), (cx + tx, cy - ty, z_top),
        (cx + tx, cy + ty, z_top), (cx - tx, cy + ty, z_top),
    ]
    faces = [(0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7), (0, 3, 2, 1)]
    mesh = bpy.data.meshes.new(name + "_mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    assign_uv(obj, rect, mat)
    add_bevel(obj, 0.035)
    return obj


def add_railing(prefix, points, height, rect, mat):
    for idx, p in enumerate(points):
        cylinder_between(f"{prefix}_post_{idx:02d}", p, (p[0], p[1], p[2] + height), 0.045, rect, mat, 12)
    for idx in range(len(points) - 1):
        a = (points[idx][0], points[idx][1], points[idx][2] + height)
        b = (points[idx + 1][0], points[idx + 1][1], points[idx + 1][2] + height)
        cylinder_between(f"{prefix}_toprail_{idx:02d}", a, b, 0.045, rect, mat, 12)
        a_mid = (points[idx][0], points[idx][1], points[idx][2] + height * 0.55)
        b_mid = (points[idx + 1][0], points[idx + 1][1], points[idx + 1][2] + height * 0.55)
        cylinder_between(f"{prefix}_midrail_{idx:02d}", a_mid, b_mid, 0.032, rect, mat, 10)


def build(mat):
    # Crusher station.
    box("L2_UV_Concrete_Crusher_Pad", (0, 0, 0.08), (10.8, 7.8, 0.24), "concrete", mat, bevel=0.025)
    box("L2_UV_Skid_Frame", (0, 0, 0.45), (8.6, 5.4, 0.34), "dark", mat, bevel=0.035)
    box("L2_UV_JawCrusher_Housing", (0.0, 0.0, 2.0), (4.9, 3.25, 3.05), "steel", mat, bevel=0.05)
    hopper_mesh("L2_UV_Receiving_Hopper_Tapered", (0, 0, 0), (5.7, 4.0), (2.6, 1.65), 3.35, 5.25, "dark", mat)
    box("L2_UV_Left_Jaw_Plate", (0.55, -0.55, 2.35), (0.38, 2.4, 2.55), "black", mat, rot=(0, math.radians(0), math.radians(-12)), bevel=0.02)
    box("L2_UV_Right_Jaw_Plate", (-0.55, 0.55, 2.35), (0.38, 2.4, 2.55), "black", mat, rot=(0, math.radians(0), math.radians(12)), bevel=0.02)
    box("L2_UV_Discharge_Chute_To_Conveyor", (-3.15, 0.78, 3.15), (3.4, 1.25, 0.28), "steel", mat, rot=(0, math.radians(-8), math.radians(0)), bevel=0.025)
    box("L2_UV_EStop_Red_Box", (3.35, -2.75, 1.75), (0.45, 0.16, 0.62), "red", mat, bevel=0.018)

    # Drive side.
    cylinder("L2_UV_Drive_Motor", (4.9, -2.35, 1.6), 0.62, 1.3, "motor", mat, 32, rot=(math.radians(90), 0, 0), bevel=True)
    cylinder("L2_UV_Flywheel_Left", (3.25, -1.86, 1.85), 0.92, 0.22, "dark", mat, 40, rot=(math.radians(90), 0, 0), bevel=True)
    cylinder("L2_UV_Flywheel_Right", (4.65, -1.86, 1.85), 0.92, 0.22, "dark", mat, 40, rot=(math.radians(90), 0, 0), bevel=True)
    box("L2_UV_Hazard_Belt_Guard", (3.95, -1.72, 1.85), (2.45, 0.22, 1.9), "hazard", mat, bevel=0.02)

    # Platform and stairs.
    box("L2_UV_Crusher_Service_Platform_Grating", (-3.05, -2.25, 3.12), (4.6, 1.45, 0.16), "grating", mat, bevel=0.015)
    stair_base = [(-5.25, -2.25, 0.35), (-5.02, -2.25, 0.72), (-4.79, -2.25, 1.09), (-4.56, -2.25, 1.46), (-4.33, -2.25, 1.83), (-4.10, -2.25, 2.20), (-3.87, -2.25, 2.57)]
    for i, p in enumerate(stair_base):
        box(f"L2_UV_Service_Stair_Tread_{i:02d}", p, (0.75, 1.32, 0.12), "grating", mat, bevel=0.01)
    box_between("L2_UV_Service_Stair_Left_Stringer", (-5.55, -2.93, 0.28), (-3.7, -2.93, 2.95), 0.08, 0.12, "yellow", mat)
    box_between("L2_UV_Service_Stair_Right_Stringer", (-5.55, -1.57, 0.28), (-3.7, -1.57, 2.95), 0.08, 0.12, "yellow", mat)
    add_railing("L2_UV_Platform_Rail_Back", [(-5.1, -3.0, 3.15), (-3.2, -3.0, 3.15), (-1.0, -3.0, 3.15)], 1.05, "yellow", mat)
    add_railing("L2_UV_Platform_Rail_End", [(-5.1, -3.0, 3.15), (-5.1, -1.55, 3.15)], 1.05, "yellow", mat)

    # Inclined conveyor / "escalator" ore belt.
    start = Vector((-3.8, 1.25, 3.45))
    end = Vector((-28.0, 4.55, 6.3))
    direction = (end - start).normalized()
    lateral = Vector((-direction.y, direction.x, 0)).normalized()
    belt_width = 2.65
    box_between("L2_UV_Inclined_Rubber_Ore_Belt", start, end, belt_width, 0.16, "rubber", mat)
    box_between("L2_UV_Left_Conveyor_Side_Skirt", start + lateral * 1.42 + Vector((0, 0, 0.32)), end + lateral * 1.42 + Vector((0, 0, 0.32)), 0.12, 0.64, "hazard", mat)
    box_between("L2_UV_Right_Conveyor_Side_Skirt", start - lateral * 1.42 + Vector((0, 0, 0.32)), end - lateral * 1.42 + Vector((0, 0, 0.32)), 0.12, 0.64, "hazard", mat)
    box_between("L2_UV_Left_Main_Truss", start + lateral * 1.65 + Vector((0, 0, -0.62)), end + lateral * 1.65 + Vector((0, 0, -0.62)), 0.16, 0.18, "steel", mat)
    box_between("L2_UV_Right_Main_Truss", start - lateral * 1.65 + Vector((0, 0, -0.62)), end - lateral * 1.65 + Vector((0, 0, -0.62)), 0.16, 0.18, "steel", mat)
    cylinder_between("L2_UV_Tail_Pulley", start - lateral * 1.35, start + lateral * 1.35, 0.34, "steel", mat, 28)
    cylinder_between("L2_UV_Head_Pulley", end - lateral * 1.35, end + lateral * 1.35, 0.38, "steel", mat, 28)

    for i in range(13):
        t = i / 12.0
        p = start.lerp(end, t)
        cylinder_between(f"L2_UV_Conveyor_Trough_Roller_{i:02d}", p - lateral * 1.15 + Vector((0, 0, -0.11)), p + lateral * 1.15 + Vector((0, 0, -0.11)), 0.12, "steel", mat, 18)
        if i % 2 == 0:
            lp = p + lateral * 1.62 + Vector((0, 0, -0.68))
            rp = p - lateral * 1.62 + Vector((0, 0, -0.68))
            cylinder_between(f"L2_UV_Conveyor_Left_Support_{i:02d}", (lp.x, lp.y, 0.25), lp, 0.055, "steel", mat, 12)
            cylinder_between(f"L2_UV_Conveyor_Right_Support_{i:02d}", (rp.x, rp.y, 0.25), rp, 0.055, "steel", mat, 12)
            box(f"L2_UV_Conveyor_Left_Foot_{i:02d}", (lp.x, lp.y, 0.08), (0.55, 0.55, 0.12), "concrete", mat, bevel=0.01)
            box(f"L2_UV_Conveyor_Right_Foot_{i:02d}", (rp.x, rp.y, 0.08), (0.55, 0.55, 0.12), "concrete", mat, bevel=0.01)
        if 0 < i < 12:
            a = p + lateral * 1.65 + Vector((0, 0, -0.62))
            b = start.lerp(end, min(1, t + 0.08)) + lateral * 1.65 + Vector((0, 0, -1.25))
            cylinder_between(f"L2_UV_Left_Truss_Diagonal_{i:02d}", a, b, 0.035, "steel", mat, 8)
            a2 = p - lateral * 1.65 + Vector((0, 0, -0.62))
            b2 = start.lerp(end, min(1, t + 0.08)) - lateral * 1.65 + Vector((0, 0, -1.25))
            cylinder_between(f"L2_UV_Right_Truss_Diagonal_{i:02d}", a2, b2, 0.035, "steel", mat, 8)

    # Conveyor maintenance catwalk.
    cw_start = start + lateral * 2.35 + Vector((0, 0, -0.1))
    cw_end = end + lateral * 2.35 + Vector((0, 0, -0.1))
    box_between("L2_UV_Conveyor_Maintenance_Catwalk", cw_start, cw_end, 0.72, 0.12, "grating", mat)
    rail_points = []
    for i in range(7):
        p = cw_start.lerp(cw_end, i / 6.0) + lateral * 0.42 + Vector((0, 0, 0.1))
        rail_points.append((p.x, p.y, p.z))
    add_railing("L2_UV_Conveyor_Catwalk_Rail", rail_points, 0.95, "yellow", mat)

    # Ore load.
    random.seed(13)
    for i in range(42):
        t = random.uniform(0.02, 0.92)
        side = random.uniform(-0.78, 0.78)
        p = start.lerp(end, t) + lateral * side + Vector((0, 0, 0.19 + random.uniform(0, 0.14)))
        sphere(f"L2_UV_Ore_Rock_On_Belt_{i:02d}", p, random.uniform(0.10, 0.24), "ore", mat, 1)
    for i in range(24):
        p = Vector((random.uniform(-1.4, 1.4), random.uniform(-1.0, 1.0), random.uniform(4.0, 5.15)))
        sphere(f"L2_UV_Ore_Rock_In_Hopper_{i:02d}", p, random.uniform(0.12, 0.28), "ore", mat, 1)

    # Label plates and lamps.
    box("L2_UV_Crusher_Nameplate", (0.0, -1.67, 2.2), (1.7, 0.045, 0.38), "blue", mat, bevel=0.006)
    cylinder("L2_UV_Status_Green_Lamp", (2.15, -1.72, 3.2), 0.12, 0.10, "blue", mat, 16, rot=(math.radians(90), 0, 0), bevel=True)


def build_v2(mat):
    # Jumbo stylized-realistic primary crusher station.
    box("L2_V2_Jumbo_Concrete_Service_Pad", (0, 0, 0.08), (15.6, 10.8, 0.28), "concrete", mat, bevel=0.04)
    box("L2_V2_Jumbo_Black_Skid_Base", (0, 0, 0.46), (12.8, 7.6, 0.42), "dark", mat, bevel=0.08)
    box("L2_V2_Rounded_Primary_Crusher_Body", (0.15, 0, 2.85), (8.4, 5.9, 5.05), "steel", mat, bevel=0.22)
    box("L2_V2_Dark_Jaw_Mouth_Recess", (-3.25, 0.0, 2.95), (1.15, 4.85, 3.85), "black", mat, bevel=0.14)
    box("L2_V2_Left_Smooth_Jaw_Liner", (-3.12, -0.92, 3.0), (0.52, 3.35, 3.35), "dark", mat, rot=(0, 0, math.radians(-10)), bevel=0.08)
    box("L2_V2_Right_Smooth_Jaw_Liner", (-3.12, 0.92, 3.0), (0.52, 3.35, 3.35), "dark", mat, rot=(0, 0, math.radians(10)), bevel=0.08)
    hopper_mesh("L2_V2_SuperJumbo_Flared_Ore_Hopper", (0, 0, 0), (10.8, 7.7), (4.45, 3.15), 5.1, 8.8, "dark", mat)
    hopper_mesh("L2_V2_Inner_Black_Hopper_Throat", (0, 0, 0), (7.9, 5.4), (3.25, 2.05), 5.28, 8.35, "black", mat)

    # Cartoon-realistic panels and bolts: broad readable shapes, smooth bevels.
    box("L2_V2_Front_Blue_Process_Badge", (0.0, -3.02, 3.0), (2.4, 0.065, 0.58), "blue", mat, bevel=0.02)
    box("L2_V2_Front_Black_Service_Spine", (2.8, -3.06, 3.15), (0.42, 0.08, 3.7), "dark", mat, bevel=0.03)
    box("L2_V2_Red_EStop_Box", (4.25, -3.05, 1.85), (0.72, 0.20, 0.86), "red", mat, bevel=0.035)
    for i, x in enumerate([-3.4, -2.3, -1.2, 1.2, 2.3, 3.4]):
        cylinder(f"L2_V2_Front_Round_Bolt_{i:02d}", (x, -3.105, 4.92), 0.105, 0.08, "dark", mat, 24, rot=(math.radians(90), 0, 0), bevel=True)
        cylinder(f"L2_V2_Lower_Round_Bolt_{i:02d}", (x, -3.105, 0.95), 0.09, 0.075, "dark", mat, 24, rot=(math.radians(90), 0, 0), bevel=True)

    # Massive drive package.
    cylinder("L2_V2_Jumbo_Drive_Motor_Smooth", (5.95, -3.95, 2.25), 1.08, 2.1, "motor", mat, 64, rot=(math.radians(90), 0, 0), bevel=True)
    cylinder("L2_V2_Jumbo_Flywheel_A_Smooth", (3.65, -3.35, 2.5), 1.72, 0.38, "dark", mat, 72, rot=(math.radians(90), 0, 0), bevel=True)
    cylinder("L2_V2_Jumbo_Flywheel_B_Smooth", (5.35, -3.35, 2.5), 1.72, 0.38, "dark", mat, 72, rot=(math.radians(90), 0, 0), bevel=True)
    cylinder("L2_V2_Flywheel_Hub_A", (3.65, -3.62, 2.5), 0.55, 0.20, "yellow", mat, 48, rot=(math.radians(90), 0, 0), bevel=True)
    cylinder("L2_V2_Flywheel_Hub_B", (5.35, -3.62, 2.5), 0.55, 0.20, "yellow", mat, 48, rot=(math.radians(90), 0, 0), bevel=True)
    box("L2_V2_Curved_Style_Hazard_Belt_Guard", (4.5, -3.47, 2.5), (3.75, 0.28, 2.7), "hazard", mat, bevel=0.09)

    # Larger discharge and protected handoff to conveyor.
    box("L2_V2_Heavy_Discharge_Chute_To_Belt", (-5.1, 1.22, 4.05), (4.9, 1.85, 0.42), "steel", mat, rot=(0, math.radians(-7), 0), bevel=0.07)
    box("L2_V2_Rubber_Lip_At_Discharge", (-7.1, 1.22, 3.74), (1.2, 1.78, 0.25), "rubber", mat, bevel=0.04)

    # Bigger service deck integrated to the giant machine.
    box("L2_V2_Jumbo_Service_Platform_Grating", (-4.4, -3.45, 4.35), (6.6, 1.85, 0.18), "grating", mat, bevel=0.025)
    stair_base = [
        (-7.7, -3.45, 0.42), (-7.38, -3.45, 0.88), (-7.06, -3.45, 1.34),
        (-6.74, -3.45, 1.80), (-6.42, -3.45, 2.26), (-6.10, -3.45, 2.72),
        (-5.78, -3.45, 3.18), (-5.46, -3.45, 3.64), (-5.14, -3.45, 4.10),
    ]
    for i, p in enumerate(stair_base):
        box(f"L2_V2_Jumbo_Service_Stair_Tread_{i:02d}", p, (0.94, 1.55, 0.14), "grating", mat, bevel=0.018)
    box_between("L2_V2_Service_Stair_Left_Stringer", (-8.08, -4.28, 0.32), (-4.96, -4.28, 4.28), 0.10, 0.16, "yellow", mat)
    box_between("L2_V2_Service_Stair_Right_Stringer", (-8.08, -2.62, 0.32), (-4.96, -2.62, 4.28), 0.10, 0.16, "yellow", mat)
    add_railing("L2_V2_Platform_Back_Rail", [(-7.5, -4.45, 4.42), (-5.6, -4.45, 4.42), (-3.5, -4.45, 4.42), (-1.4, -4.45, 4.42)], 1.15, "yellow", mat)
    add_railing("L2_V2_Platform_End_Rail", [(-7.5, -4.45, 4.42), (-7.5, -2.52, 4.42)], 1.15, "yellow", mat)

    # Inclined ore escalator/conveyor, widened to match jumbo mouth.
    start = Vector((-6.85, 1.35, 4.12))
    end = Vector((-29.5, 4.42, 6.35))
    direction = (end - start).normalized()
    lateral = Vector((-direction.y, direction.x, 0)).normalized()
    belt_width = 3.35
    box_between("L2_V2_Wide_Inclined_Rubber_Ore_Belt", start, end, belt_width, 0.20, "rubber", mat)
    box_between("L2_V2_Left_Tall_Yellow_Side_Skirt", start + lateral * 1.78 + Vector((0, 0, 0.45)), end + lateral * 1.78 + Vector((0, 0, 0.45)), 0.14, 0.82, "hazard", mat)
    box_between("L2_V2_Right_Tall_Yellow_Side_Skirt", start - lateral * 1.78 + Vector((0, 0, 0.45)), end - lateral * 1.78 + Vector((0, 0, 0.45)), 0.14, 0.82, "hazard", mat)
    box_between("L2_V2_Left_Deep_Main_Truss", start + lateral * 2.05 + Vector((0, 0, -0.75)), end + lateral * 2.05 + Vector((0, 0, -0.75)), 0.20, 0.22, "steel", mat)
    box_between("L2_V2_Right_Deep_Main_Truss", start - lateral * 2.05 + Vector((0, 0, -0.75)), end - lateral * 2.05 + Vector((0, 0, -0.75)), 0.20, 0.22, "steel", mat)
    cylinder_between("L2_V2_Smooth_Tail_Pulley", start - lateral * 1.72, start + lateral * 1.72, 0.44, "steel", mat, 48)
    cylinder_between("L2_V2_Smooth_Head_Pulley", end - lateral * 1.72, end + lateral * 1.72, 0.48, "steel", mat, 48)

    for i in range(15):
        t = i / 14.0
        p = start.lerp(end, t)
        cylinder_between(f"L2_V2_Smooth_Trough_Roller_{i:02d}", p - lateral * 1.42 + Vector((0, 0, -0.13)), p + lateral * 1.42 + Vector((0, 0, -0.13)), 0.14, "steel", mat, 28)
        if i % 2 == 0:
            lp = p + lateral * 2.05 + Vector((0, 0, -0.78))
            rp = p - lateral * 2.05 + Vector((0, 0, -0.78))
            cylinder_between(f"L2_V2_Left_Jumbo_Conveyor_Support_{i:02d}", (lp.x, lp.y, 0.28), lp, 0.07, "steel", mat, 16)
            cylinder_between(f"L2_V2_Right_Jumbo_Conveyor_Support_{i:02d}", (rp.x, rp.y, 0.28), rp, 0.07, "steel", mat, 16)
            box(f"L2_V2_Left_Jumbo_Foot_{i:02d}", (lp.x, lp.y, 0.09), (0.72, 0.72, 0.14), "concrete", mat, bevel=0.018)
            box(f"L2_V2_Right_Jumbo_Foot_{i:02d}", (rp.x, rp.y, 0.09), (0.72, 0.72, 0.14), "concrete", mat, bevel=0.018)
        if 0 < i < 14:
            n = min(1, t + 0.07)
            cylinder_between(f"L2_V2_Left_Truss_Diagonal_{i:02d}", p + lateral * 2.05 + Vector((0, 0, -0.75)), start.lerp(end, n) + lateral * 2.05 + Vector((0, 0, -1.55)), 0.045, "steel", mat, 10)
            cylinder_between(f"L2_V2_Right_Truss_Diagonal_{i:02d}", p - lateral * 2.05 + Vector((0, 0, -0.75)), start.lerp(end, n) - lateral * 2.05 + Vector((0, 0, -1.55)), 0.045, "steel", mat, 10)

    cw_start = start + lateral * 2.85 + Vector((0, 0, -0.08))
    cw_end = end + lateral * 2.85 + Vector((0, 0, -0.08))
    box_between("L2_V2_Wide_Conveyor_Maintenance_Catwalk", cw_start, cw_end, 0.86, 0.14, "grating", mat)
    rail_points = []
    for i in range(8):
        p = cw_start.lerp(cw_end, i / 7.0) + lateral * 0.50 + Vector((0, 0, 0.12))
        rail_points.append((p.x, p.y, p.z))
    add_railing("L2_V2_Conveyor_Catwalk_Smooth_Rail", rail_points, 1.05, "yellow", mat)

    random.seed(24)
    for i in range(72):
        t = random.uniform(0.02, 0.94)
        side = random.uniform(-1.03, 1.03)
        p = start.lerp(end, t) + lateral * side + Vector((0, 0, 0.24 + random.uniform(0, 0.22)))
        sphere(f"L2_V2_Rounded_Ore_Rock_On_Belt_{i:02d}", p, random.uniform(0.11, 0.30), "ore", mat, 2)
    for i in range(46):
        p = Vector((random.uniform(-2.4, 2.4), random.uniform(-1.55, 1.55), random.uniform(6.0, 8.32)))
        sphere(f"L2_V2_Rounded_Ore_Rock_In_Hopper_{i:02d}", p, random.uniform(0.16, 0.40), "ore", mat, 2)

    cylinder("L2_V2_Status_Blue_Lamp", (4.15, -3.08, 4.95), 0.15, 0.12, "blue", mat, 24, rot=(math.radians(90), 0, 0), bevel=True)


def main():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    image = create_atlas()
    mat = create_material(image)
    build_v2(mat)

    for obj in bpy.context.scene.objects:
        obj.select_set(True)
        if obj.type == "MESH":
            obj.data.update()

    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH,
        use_selection=False,
        apply_unit_scale=True,
        global_scale=1.0,
        bake_space_transform=False,
        axis_forward="-Z",
        axis_up="Y",
        path_mode="COPY",
        embed_textures=False,
        use_mesh_modifiers=True,
    )
    print("EXPORTED", FBX_PATH)
    print("ATLAS", ATLAS_PATH)


if __name__ == "__main__":
    main()
