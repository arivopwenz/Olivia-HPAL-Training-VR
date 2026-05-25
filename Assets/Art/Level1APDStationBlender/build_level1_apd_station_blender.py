import math
import os
import random
from mathutils import Vector

import bpy

ROOT = os.path.dirname(os.path.abspath(__file__))
BLEND_PATH = os.path.join(ROOT, "level1_apd_industrial_workbench_locker_uv.blend")
FBX_PATH = os.path.join(ROOT, "level1_apd_industrial_workbench_locker_uv.fbx")
GLB_PATH = os.path.join(ROOT, "level1_apd_industrial_workbench_locker_uv.glb")
ATLAS_PATH = os.path.join(ROOT, "level1_apd_industrial_workbench_locker_atlas.png")
PREVIEW_PATH = os.path.join(ROOT, "level1_apd_industrial_workbench_locker_preview.png")

COLORS = {
    "green_steel": (0.13, 0.21, 0.18, 1.0),
    "dark_steel": (0.045, 0.055, 0.052, 1.0),
    "blue_steel": (0.055, 0.115, 0.165, 1.0),
    "rubber": (0.028, 0.032, 0.030, 1.0),
    "safety_yellow": (0.95, 0.68, 0.045, 1.0),
    "galvanized": (0.52, 0.56, 0.53, 1.0),
    "shadow": (0.018, 0.023, 0.022, 1.0),
    "concrete": (0.36, 0.37, 0.34, 1.0),
    "label": (0.82, 0.80, 0.70, 1.0),
    "rust": (0.44, 0.18, 0.06, 1.0),
}

ATLAS_RECTS = {
    "green_steel": (0.00, 0.75, 0.25, 1.00),
    "dark_steel": (0.25, 0.75, 0.50, 1.00),
    "blue_steel": (0.50, 0.75, 0.75, 1.00),
    "rubber": (0.75, 0.75, 1.00, 1.00),
    "safety_yellow": (0.00, 0.50, 0.25, 0.75),
    "galvanized": (0.25, 0.50, 0.50, 0.75),
    "shadow": (0.50, 0.50, 0.75, 0.75),
    "concrete": (0.75, 0.50, 1.00, 0.75),
    "label": (0.00, 0.25, 0.25, 0.50),
    "rust": (0.25, 0.25, 0.50, 0.50),
}


def clean_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()


def make_atlas():
    random.seed(11)
    size = 1024
    pixels = [0.0] * (size * size * 4)

    def paint(x, y, color):
        idx = (y * size + x) * 4
        pixels[idx:idx + 4] = color

    def fill_rect(key, stripe=False, scratches=True):
        rect = ATLAS_RECTS[key]
        color = COLORS[key]
        x0, y0, x1, y1 = rect
        ix0, iy0, ix1, iy1 = int(x0 * size), int(y0 * size), int(x1 * size), int(y1 * size)
        for y in range(iy0, iy1):
            for x in range(ix0, ix1):
                c = [color[0], color[1], color[2], color[3]]
                noise = ((x * 13 + y * 7 + (x ^ y)) % 41) / 40.0
                grain = 0.90 + noise * 0.12
                if key in ("galvanized", "green_steel", "blue_steel", "dark_steel"):
                    grain += 0.035 * math.sin((x + y) * 0.08)
                if stripe and ((x + y) // 28) % 2 == 0:
                    grain *= 0.45
                if scratches and (x * 17 + y * 29) % 193 == 0:
                    grain = min(1.22, grain + 0.30)
                if (x - ix0) < 5 or (y - iy0) < 5 or (ix1 - x) < 5 or (iy1 - y) < 5:
                    grain *= 0.42
                paint(x, y, (c[0] * grain, c[1] * grain, c[2] * grain, c[3]))

    for key in ATLAS_RECTS:
        fill_rect(key, stripe=(key == "safety_yellow"), scratches=(key != "label"))

    image = bpy.data.images.new("level1_apd_station_v2_uv_atlas", size, size, alpha=True)
    image.pixels = pixels
    image.filepath_raw = ATLAS_PATH
    image.file_format = "PNG"
    image.save()
    return image


def make_material(image):
    mat = bpy.data.materials.new("M_Level1_APDStation_UVAtlas")
    mat.diffuse_color = (1, 1, 1, 1)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    bsdf = nodes.get("Principled BSDF")
    tex = nodes.new(type="ShaderNodeTexImage")
    tex.image = image
    tex.extension = "CLIP"
    mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    if "Metallic" in bsdf.inputs:
        bsdf.inputs["Metallic"].default_value = 0.28
    if "Roughness" in bsdf.inputs:
        bsdf.inputs["Roughness"].default_value = 0.62
    return mat


def u_pos(x, y, z):
    return (x, z, y)


def u_size(x, y, z):
    return (x, z, y)


def assign_uv(obj, key):
    if obj.type != "MESH":
        return
    mesh = obj.data
    if not mesh.uv_layers:
        mesh.uv_layers.new(name="UVMap")
    uv_layer = mesh.uv_layers.active.data
    u0, v0, u1, v1 = ATLAS_RECTS[key]
    verts = [v.co.copy() for v in mesh.vertices]
    if not verts:
        return
    minv = Vector((min(v.x for v in verts), min(v.y for v in verts), min(v.z for v in verts)))
    maxv = Vector((max(v.x for v in verts), max(v.y for v in verts), max(v.z for v in verts)))
    size = maxv - minv
    axes = sorted([(size.x, 0), (size.y, 1), (size.z, 2)], reverse=True)
    a, b = axes[0][1], axes[1][1]

    def comp(v, idx):
        return (v.x, v.y, v.z)[idx]

    mina, maxa = comp(minv, a), comp(maxv, a)
    minb, maxb = comp(minv, b), comp(maxv, b)
    da = max(maxa - mina, 0.0001)
    db = max(maxb - minb, 0.0001)

    for poly in mesh.polygons:
        for li in poly.loop_indices:
            co = mesh.vertices[mesh.loops[li].vertex_index].co
            uu = (comp(co, a) - mina) / da
            vv = (comp(co, b) - minb) / db
            uv_layer[li].uv = (u0 + uu * (u1 - u0), v0 + vv * (v1 - v0))


def finish(obj, mat, uv_key, bevel=0.0, smooth=True):
    obj.data.materials.append(mat)
    assign_uv(obj, uv_key)
    if smooth:
        for poly in obj.data.polygons:
            poly.use_smooth = True
    if bevel > 0:
        mod = obj.modifiers.new("industrial_edge_radius", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        mod.affect = "EDGES"
    obj.modifiers.new("weighted_normals", "WEIGHTED_NORMAL")
    return obj


def cube(name, loc, scale, mat, uv_key, bevel=0.018, rot_y=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=u_pos(*loc))
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = u_size(*scale)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if abs(rot_y) > 0.001:
        obj.rotation_euler[2] = math.radians(rot_y)
    return finish(obj, mat, uv_key, bevel)


def cyl(name, loc, radius, height, mat, uv_key, vertices=20, bevel=0.0):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=height, location=u_pos(*loc))
    obj = bpy.context.object
    obj.name = name
    return finish(obj, mat, uv_key, bevel)


def cyl_between(name, a, b, radius, mat, uv_key, vertices=16):
    aa = Vector(u_pos(*a))
    bb = Vector(u_pos(*b))
    mid = (aa + bb) * 0.5
    delta = bb - aa
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=delta.length, location=mid)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_euler = delta.to_track_quat("Z", "Y").to_euler()
    return finish(obj, mat, uv_key, 0.0)


def torus_rot(name, loc, major, minor, mat, uv_key, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(
        major_segments=32,
        minor_segments=8,
        major_radius=major,
        minor_radius=minor,
        location=u_pos(*loc),
        rotation=rot,
    )
    obj = bpy.context.object
    obj.name = name
    return finish(obj, mat, uv_key, 0.0)


def bolt(name, loc, mat, uv_key="galvanized", scale=(0.055, 0.020, 0.055)):
    return cube(name, loc, scale, mat, uv_key, 0.006)


def repeated_bolts(prefix, xs, y, z, mat, uv_key="galvanized"):
    for i, x in enumerate(xs):
        bolt(f"{prefix}_{i:02}", (x, y, z), mat, uv_key)


def build_floor_skid(mat):
    cube("L1APD_V2_Concrete_Service_Pad", (-5.85, 0.045, -7.58), (5.92, 0.09, 1.86), mat, "concrete", 0.020)
    cube("L1APD_V2_Black_AntiSlip_Rubber_Mat", (-4.55, 0.105, -7.22), (3.25, 0.045, 0.82), mat, "rubber", 0.012)

    for z in (-8.32, -6.82):
        cube(f"L1APD_V2_Skid_Long_Rail_{z}", (-5.82, 0.19, z), (5.72, 0.16, 0.12), mat, "dark_steel", 0.015)
    for x in (-8.52, -7.42, -6.32, -5.22, -4.12, -3.02):
        cube(f"L1APD_V2_Skid_Crossmember_{x}", (x, 0.22, -7.57), (0.13, 0.13, 1.52), mat, "dark_steel", 0.012)

    for i in range(11):
        x = -8.45 + i * 0.50
        cube(
            f"L1APD_V2_Front_Hazard_Toe_Stripe_{i:02}",
            (x, 0.24, -6.69),
            (0.31, 0.055, 0.070),
            mat,
            "safety_yellow" if i % 2 == 0 else "shadow",
            0.004,
        )


def build_industrial_locker(mat):
    x0, y0, z0 = -7.55, 0.0, -7.58
    cube("L1APD_V2_Locker_Welded_Back_Box", (x0, y0 + 1.12, z0 - 0.05), (2.26, 2.22, 0.72), mat, "shadow", 0.035)
    cube("L1APD_V2_Locker_Left_Side_Column", (x0 - 1.20, y0 + 1.13, z0 + 0.34), (0.12, 2.26, 0.16), mat, "dark_steel", 0.018)
    cube("L1APD_V2_Locker_Right_Side_Column", (x0 + 1.20, y0 + 1.13, z0 + 0.34), (0.12, 2.26, 0.16), mat, "dark_steel", 0.018)
    cube("L1APD_V2_Locker_Top_Cap_Heavy", (x0, y0 + 2.28, z0 + 0.02), (2.55, 0.20, 0.88), mat, "dark_steel", 0.022)
    cube("L1APD_V2_Locker_Toe_Kick_Recess", (x0, y0 + 0.13, z0 + 0.02), (2.50, 0.22, 0.86), mat, "dark_steel", 0.018)
    cube("L1APD_V2_Locker_Header_Painted", (x0, y0 + 2.12, z0 + 0.41), (2.24, 0.13, 0.075), mat, "blue_steel", 0.012)

    door_xs = (-8.22, -7.55, -6.88)
    for i, x in enumerate(door_xs):
        door_rot = -7.5 if i == 1 else 0.0
        door_z = z0 + (0.43 if i != 1 else 0.48)
        cube(f"L1APD_V2_Locker_Door_Corrugated_{i + 1}", (x, 1.14, door_z), (0.58, 1.70, 0.070), mat, "green_steel", 0.018, rot_y=door_rot)
        cube(f"L1APD_V2_Locker_Door_Inner_Shadow_{i + 1}", (x, 1.14, door_z + 0.045), (0.45, 1.45, 0.026), mat, "shadow", 0.006, rot_y=door_rot)
        cube(f"L1APD_V2_Locker_Door_Center_Rib_{i + 1}", (x, 1.14, door_z + 0.075), (0.10, 1.50, 0.034), mat, "blue_steel", 0.006, rot_y=door_rot)
        for r in (-0.20, 0.20):
            cube(f"L1APD_V2_Locker_Door_Raised_Rib_{i + 1}_{r}", (x + r, 1.14, door_z + 0.082), (0.060, 1.55, 0.036), mat, "green_steel", 0.006, rot_y=door_rot)
        for j in range(6):
            yy = 1.78 - j * 0.075
            cube(f"L1APD_V2_Locker_Top_Louver_{i + 1}_{j}", (x, yy, door_z + 0.105), (0.34, 0.018, 0.035), mat, "galvanized", 0.003, rot_y=door_rot)
            yy2 = 0.58 + j * 0.062
            cube(f"L1APD_V2_Locker_Bottom_Louver_{i + 1}_{j}", (x, yy2, door_z + 0.105), (0.30, 0.016, 0.032), mat, "galvanized", 0.003, rot_y=door_rot)
        cube(f"L1APD_V2_Locker_Nameplate_{i + 1}", (x, 1.95, door_z + 0.110), (0.31, 0.075, 0.036), mat, "label", 0.004, rot_y=door_rot)
        cube(f"L1APD_V2_Locker_Hasp_Plate_{i + 1}", (x + 0.21, 1.16, door_z + 0.130), (0.070, 0.25, 0.045), mat, "galvanized", 0.005, rot_y=door_rot)
        torus_rot(
            f"L1APD_V2_Locker_Padlock_Loop_{i + 1}",
            (x + 0.21, 1.00, door_z + 0.155),
            0.060,
            0.010,
            mat,
            "galvanized",
            rot=(math.radians(90), 0, 0),
        )
        for yy in (0.48, 1.12, 1.76):
            cube(f"L1APD_V2_Locker_Hinge_{i + 1}_{yy}", (x - 0.31, yy, door_z + 0.125), (0.048, 0.18, 0.042), mat, "galvanized", 0.004, rot_y=door_rot)

    for x in (-8.74, -6.36):
        cyl_between(f"L1APD_V2_Locker_Yellow_Bollard_{x}", (x, 0.25, -6.98), (x, 2.22, -6.98), 0.030, mat, "safety_yellow", 16)
        cyl_between(f"L1APD_V2_Locker_Bollard_Foot_{x}", (x - 0.16, 0.25, -6.98), (x + 0.16, 0.25, -6.98), 0.026, mat, "safety_yellow", 16)

    repeated_bolts("L1APD_V2_Locker_TopCap_Bolts", [-8.58, -7.92, -7.18, -6.52], 2.39, -6.98, mat)
    repeated_bolts("L1APD_V2_Locker_ToeKick_Bolts", [-8.58, -7.92, -7.18, -6.52], 0.27, -6.92, mat)


def build_workbench(mat):
    x0, y0, z0 = -4.35, 0.0, -7.55
    cube("L1APD_V2_Workbench_Black_Rubberized_Top", (x0, y0 + 0.94, z0 + 0.15), (3.62, 0.18, 1.08), mat, "rubber", 0.028)
    cube("L1APD_V2_Workbench_Galv_Edge_Band_Front", (x0, y0 + 1.04, z0 + 0.72), (3.74, 0.075, 0.075), mat, "galvanized", 0.010)
    cube("L1APD_V2_Workbench_Galv_Edge_Band_Back", (x0, y0 + 1.04, z0 - 0.44), (3.74, 0.075, 0.075), mat, "galvanized", 0.010)
    cube("L1APD_V2_Workbench_Left_End_Cap", (x0 - 1.90, y0 + 0.93, z0 + 0.14), (0.12, 0.28, 1.18), mat, "dark_steel", 0.012)
    cube("L1APD_V2_Workbench_Right_End_Cap", (x0 + 1.90, y0 + 0.93, z0 + 0.14), (0.12, 0.28, 1.18), mat, "dark_steel", 0.012)

    for x in (x0 - 1.67, x0 + 1.67):
        for z in (z0 - 0.32, z0 + 0.61):
            cyl(f"L1APD_V2_Workbench_Round_Tube_Leg_{x}_{z}", (x, 0.53, z), 0.055, 0.92, mat, "dark_steel", 18)
            cube(f"L1APD_V2_Workbench_Foot_Plate_{x}_{z}", (x, 0.055, z), (0.30, 0.055, 0.24), mat, "galvanized", 0.007)
    for z in (z0 - 0.32, z0 + 0.61):
        cyl_between(f"L1APD_V2_Workbench_FrontBack_Tube_{z}", (x0 - 1.68, 0.70, z), (x0 + 1.68, 0.70, z), 0.035, mat, "dark_steel", 16)
    for x in (x0 - 1.67, x0 + 1.67):
        cyl_between(f"L1APD_V2_Workbench_Side_Tube_{x}", (x, 0.70, z0 - 0.32), (x, 0.70, z0 + 0.61), 0.035, mat, "dark_steel", 16)
    cyl_between("L1APD_V2_Workbench_XBrace_A", (x0 - 1.65, 0.34, z0 + 0.60), (x0 + 1.65, 0.68, z0 - 0.30), 0.022, mat, "galvanized", 12)
    cyl_between("L1APD_V2_Workbench_XBrace_B", (x0 - 1.65, 0.68, z0 - 0.30), (x0 + 1.65, 0.34, z0 + 0.60), 0.022, mat, "galvanized", 12)

    cube("L1APD_V2_Workbench_Dark_Drawer_Case", (x0 - 0.80, 0.63, z0 + 0.70), (1.74, 0.47, 0.18), mat, "dark_steel", 0.016)
    for i in range(3):
        x = x0 - 1.35 + i * 0.55
        cube(f"L1APD_V2_Workbench_Drawer_Face_{i + 1}", (x, 0.65, z0 + 0.81), (0.46, 0.31, 0.055), mat, "green_steel", 0.010)
        cube(f"L1APD_V2_Workbench_Drawer_Pull_{i + 1}", (x, 0.66, z0 + 0.86), (0.24, 0.032, 0.032), mat, "galvanized", 0.004)

    cube("L1APD_V2_Workbench_Open_Shelf_Galv_Pan", (x0 + 0.98, 0.43, z0 + 0.09), (1.38, 0.075, 0.70), mat, "galvanized", 0.010)
    for i in range(7):
        x = x0 + 0.40 + i * 0.18
        cube(f"L1APD_V2_Workbench_Shelf_Grate_Bar_{i}", (x, 0.51, z0 + 0.09), (0.035, 0.035, 0.72), mat, "shadow", 0.003)

    cube("L1APD_V2_Back_Pegboard_Frame", (x0, 1.52, z0 - 0.54), (3.78, 1.18, 0.105), mat, "dark_steel", 0.018)
    cube("L1APD_V2_Back_Pegboard_Painted_Panel", (x0, 1.52, z0 - 0.48), (3.50, 0.94, 0.040), mat, "green_steel", 0.010)
    cube("L1APD_V2_Back_Top_Rail_Blue", (x0, 2.12, z0 - 0.48), (3.78, 0.16, 0.080), mat, "blue_steel", 0.010)
    cube("L1APD_V2_Back_Lower_Kick_Rail", (x0, 0.98, z0 - 0.47), (3.78, 0.11, 0.070), mat, "dark_steel", 0.008)

    for row in range(5):
        for col in range(12):
            x = x0 - 1.48 + col * 0.27
            y = 1.25 + row * 0.15
            cube(f"L1APD_V2_Pegboard_Dark_Perf_{row}_{col}", (x, y, z0 - 0.435), (0.055, 0.024, 0.018), mat, "shadow", 0.002)

    for x in (x0 - 1.28, x0 - 0.60, x0 + 0.24, x0 + 1.10):
        cyl_between(f"L1APD_V2_Pegboard_Hook_{x}", (x, 1.50, z0 - 0.41), (x, 1.50, z0 - 0.08), 0.014, mat, "galvanized", 12)
        cube(f"L1APD_V2_Pegboard_Hook_End_{x}", (x, 1.47, z0 - 0.075), (0.060, 0.040, 0.032), mat, "galvanized", 0.003)

    cube("L1APD_V2_Workbench_BenchVise_Base", (x0 + 1.25, 1.13, z0 + 0.28), (0.42, 0.11, 0.32), mat, "blue_steel", 0.012)
    cube("L1APD_V2_Workbench_BenchVise_Static_Jaw", (x0 + 1.10, 1.23, z0 + 0.18), (0.11, 0.26, 0.12), mat, "galvanized", 0.008)
    cube("L1APD_V2_Workbench_BenchVise_Moving_Jaw", (x0 + 1.40, 1.23, z0 + 0.18), (0.11, 0.26, 0.12), mat, "galvanized", 0.008)
    cyl_between("L1APD_V2_Workbench_BenchVise_Handle", (x0 + 1.08, 1.20, z0 + 0.43), (x0 + 1.48, 1.20, z0 + 0.43), 0.014, mat, "galvanized", 12)

    cube("L1APD_V2_Workbench_Parts_Bin_1", (x0 - 0.18, 1.11, z0 + 0.26), (0.46, 0.16, 0.32), mat, "blue_steel", 0.012)
    cube("L1APD_V2_Workbench_Parts_Bin_2", (x0 + 0.36, 1.11, z0 + 0.26), (0.46, 0.16, 0.32), mat, "green_steel", 0.012)
    cube("L1APD_V2_Workbench_Bin_Lip_1", (x0 - 0.18, 1.20, z0 + 0.44), (0.50, 0.035, 0.035), mat, "safety_yellow", 0.004)
    cube("L1APD_V2_Workbench_Bin_Lip_2", (x0 + 0.36, 1.20, z0 + 0.44), (0.50, 0.035, 0.035), mat, "safety_yellow", 0.004)

    cyl_between("L1APD_V2_Back_Conduit_Pipe", (x0 - 1.82, 2.02, z0 - 0.36), (x0 + 1.82, 2.02, z0 - 0.36), 0.026, mat, "galvanized", 16)
    cube("L1APD_V2_Back_Junction_Box", (x0 - 1.55, 1.85, z0 - 0.34), (0.30, 0.26, 0.095), mat, "galvanized", 0.008)
    cube("L1APD_V2_Back_Junction_Box_Warning_Tab", (x0 - 1.55, 1.85, z0 - 0.27), (0.16, 0.10, 0.025), mat, "safety_yellow", 0.003)

    repeated_bolts("L1APD_V2_Workbench_Top_Bolts_Front", [-5.95, -5.30, -4.65, -4.00, -3.35, -2.75], 1.07, z0 + 0.73, mat)
    repeated_bolts("L1APD_V2_Workbench_BackPanel_Bolts", [-5.95, -5.30, -4.65, -4.00, -3.35, -2.75], 2.09, z0 - 0.40, mat)


def build_side_guard_and_label(mat):
    cyl_between("L1APD_V2_Right_GuardRail_Top", (-2.25, 1.18, -6.93), (-2.25, 1.18, -8.17), 0.035, mat, "safety_yellow", 16)
    cyl_between("L1APD_V2_Right_GuardRail_Post_Front", (-2.25, 0.20, -6.93), (-2.25, 1.18, -6.93), 0.034, mat, "safety_yellow", 16)
    cyl_between("L1APD_V2_Right_GuardRail_Post_Back", (-2.25, 0.20, -8.17), (-2.25, 1.18, -8.17), 0.034, mat, "safety_yellow", 16)
    cube("L1APD_V2_Small_Industrial_Nameplate_NoText", (-5.85, 2.30, -7.07), (1.10, 0.16, 0.050), mat, "label", 0.006)
    cube("L1APD_V2_Nameplate_Black_Bottom_Line", (-5.85, 2.23, -7.035), (1.02, 0.026, 0.035), mat, "shadow", 0.002)
    for i in range(3):
        cube(f"L1APD_V2_Nameplate_Status_Block_{i}", (-6.18 + i * 0.30, 2.31, -7.03), (0.10, 0.055, 0.030), mat, "safety_yellow" if i == 1 else "blue_steel", 0.002)


def setup_camera_and_light():
    bpy.ops.object.light_add(type="AREA", location=(0, -3.8, 5.2))
    light = bpy.context.object
    light.name = "Preview_Large_Industrial_Softbox"
    light.data.energy = 520
    light.data.size = 5.8

    bpy.ops.object.light_add(type="POINT", location=(-4.2, -6.0, 2.2))
    rim = bpy.context.object
    rim.name = "Preview_Cool_Rim_Light"
    rim.data.energy = 80
    rim.data.color = (0.78, 0.88, 1.0)

    bpy.ops.object.camera_add(location=(-5.4, 5.7, 2.35))
    cam = bpy.context.object
    bpy.context.scene.camera = cam
    direction = Vector(u_pos(-5.45, 1.13, -7.55)) - cam.location
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = 27


def parent_all():
    root = bpy.data.objects.new("Level1_APD_Blender_Workbench_Locker_Root", None)
    bpy.context.collection.objects.link(root)
    for obj in bpy.context.scene.objects:
        if obj.name != root.name and obj.type in {"MESH", "FONT", "EMPTY"}:
            obj.parent = root
    return root


def validate():
    mesh_count = 0
    missing_uv = []
    tris = 0
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH":
            continue
        mesh_count += 1
        if not obj.data.uv_layers:
            missing_uv.append(obj.name)
        tris += sum(max(1, len(poly.vertices) - 2) for poly in obj.data.polygons)
    print(f"VALIDATION mesh_count={mesh_count} missing_uv={len(missing_uv)} approx_tris={tris}")
    if missing_uv:
        print("MISSING_UV " + ", ".join(missing_uv[:20]))
        raise RuntimeError("UV validation failed")


def set_origin_and_units():
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    for obj in bpy.context.scene.objects:
        if obj.type == "MESH":
            obj.select_set(True)
        else:
            obj.select_set(False)
    bpy.context.view_layer.objects.active = next((o for o in bpy.context.scene.objects if o.type == "MESH"), None)


def main():
    clean_scene()
    image = make_atlas()
    mat = make_material(image)
    build_floor_skid(mat)
    build_industrial_locker(mat)
    build_workbench(mat)
    build_side_guard_and_label(mat)
    set_origin_and_units()
    root = parent_all()
    setup_camera_and_light()

    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    validate()

    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    bpy.context.view_layer.objects.active = root
    for child in root.children:
        child.select_set(True)

    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH,
        use_selection=True,
        apply_unit_scale=True,
        bake_space_transform=False,
        object_types={"EMPTY", "MESH"},
        axis_forward="-Z",
        axis_up="Y",
        path_mode="COPY",
        embed_textures=False,
        use_mesh_modifiers=True,
    )
    bpy.ops.export_scene.gltf(
        filepath=GLB_PATH,
        export_format="GLB",
        use_selection=True,
        export_apply=True,
    )

    bpy.context.scene.render.resolution_x = 1400
    bpy.context.scene.render.resolution_y = 900
    if hasattr(bpy.context.scene, "eevee"):
        bpy.context.scene.eevee.taa_render_samples = 64
    bpy.context.scene.render.filepath = PREVIEW_PATH
    bpy.ops.render.render(write_still=True)
    print("WROTE", BLEND_PATH, FBX_PATH, GLB_PATH, ATLAS_PATH, PREVIEW_PATH)


if __name__ == "__main__":
    main()
