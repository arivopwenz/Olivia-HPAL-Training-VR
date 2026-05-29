import math
import os

import bpy
import mathutils

ROOT_DIR = os.path.dirname(os.path.abspath(__file__))
BLEND_PATH = os.path.join(ROOT_DIR, "Ore_Tangga_Industrial_Access_UV.blend")
FBX_PATH = os.path.join(ROOT_DIR, "Ore_Tangga_Industrial_Access_UV.fbx")
PREVIEW_PATH = os.path.join(ROOT_DIR, "Ore_Tangga_Industrial_Access_UV_Preview.png")

# Professional fixed industrial stair proportions. OSHA 1910.25 allows 30-50 degrees;
# this build uses a 36-37 degree pitch with uniform risers/treads.
STAIR_RISE_TARGET = 0.185
STAIR_TREAD_RUN = 0.255
STAIR_WIDTH = 1.45
RAIL_TOP_HEIGHT = 1.18
RAIL_MID_HEIGHT = 0.62
TOE_BOARD_HEIGHT = 0.18
SAVE_BLEND = True
USE_DETAIL_MODIFIERS = False


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()


def make_mat(name, color, metallic=0.0, roughness=0.65):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    return mat


MAT_STEEL = make_mat("MAT_OreTangga_HotDip_Galvanized_Steel", (0.48, 0.54, 0.51, 1), 0.55, 0.38)
MAT_DARK = make_mat("MAT_OreTangga_Black_Open_Metal_Grating", (0.025, 0.027, 0.024, 1), 0.35, 0.72)
MAT_YELLOW = make_mat("MAT_OreTangga_Safety_Yellow_Powdercoat", (0.96, 0.70, 0.07, 1), 0.12, 0.52)
MAT_EDGE = make_mat("MAT_OreTangga_Worn_Yellow_Nosing", (1.0, 0.78, 0.10, 1), 0.20, 0.48)
MAT_CONCRETE = make_mat("MAT_OreTangga_Stained_Concrete_Footing", (0.42, 0.44, 0.39, 1), 0.0, 0.88)
MAT_BOLT = make_mat("MAT_OreTangga_Dark_Bolts_Baseplates", (0.055, 0.052, 0.047, 1), 0.38, 0.62)
MAT_SIGN = make_mat("MAT_OreTangga_Blue_Access_Sign", (0.02, 0.13, 0.30, 1), 0.0, 0.50)
MAT_WHITE = make_mat("MAT_OreTangga_White_Label", (0.86, 0.88, 0.84, 1), 0.0, 0.55)


def u(point):
    x, y, z = point
    return mathutils.Vector((x, -z, y))


def yaw_unity_to_blender(angle_rad):
    return -angle_rad


def make_empty(name, parent=None):
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    if parent:
        obj.parent = parent
    return obj


def add_cube_obj(name, center, size, mat, parent=None, yaw=0.0, bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=u(center), rotation=(0.0, 0.0, yaw_unity_to_blender(yaw)))
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = (size[0], size[2], size[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if mat:
        obj.data.materials.append(mat)
    if parent:
        obj.parent = parent
    if USE_DETAIL_MODIFIERS and bevel > 0 and max(size) > 0.5:
        mod = obj.modifiers.new(name="small_welded_bevel", type="BEVEL")
        mod.width = bevel
        mod.segments = 2
        mod.affect = "EDGES"
        obj.modifiers.new(name="weighted_industrial_normals", type="WEIGHTED_NORMAL")
    return obj


def add_cylinder_between(name, start, end, radius, mat, parent=None, vertices=16):
    a = u(start)
    b = u(end)
    vec = b - a
    length = vec.length
    if length < 0.001:
        return None
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=length, location=(a + b) * 0.5)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_euler = vec.to_track_quat("Z", "Y").to_euler()
    if mat:
        obj.data.materials.append(mat)
    if parent:
        obj.parent = parent
    if USE_DETAIL_MODIFIERS:
        obj.modifiers.new(name="weighted_pipe_normals", type="WEIGHTED_NORMAL")
    return obj


def vec_from_yaw(yaw):
    return math.cos(yaw), math.sin(yaw), -math.sin(yaw), math.cos(yaw)


def point_offset(point, yaw, along=0.0, side=0.0, up=0.0):
    dx, dz, px, pz = vec_from_yaw(yaw)
    return (point[0] + dx * along + px * side, point[1] + up, point[2] + dz * along + pz * side)


def add_post(name, x, z, y0, y1, mat, parent, radius=0.052):
    return add_cylinder_between(name, (x, y0, z), (x, y1, z), radius, mat, parent, vertices=14)


def add_grating(prefix, center, length, width, yaw, parent, dense=False):
    cross_spacing = 1.05 if dense else 1.35
    cross_count = max(4, min(38, int(length / cross_spacing)))
    for i in range(cross_count + 1):
        along = -length * 0.5 + length * i / cross_count
        pos = point_offset(center, yaw, along=along, up=0.105)
        add_cube_obj(f"{prefix}_Cross_Grating_Bar_{i:02d}", pos, (0.042, 0.055, width * 0.86), MAT_STEEL, parent, yaw + math.pi * 0.5, bevel=0.004)

    lanes = max(3, min(5, int(width / 0.42)))
    for j in range(lanes):
        side = -width * 0.38 + width * 0.76 * j / max(1, lanes - 1)
        pos = point_offset(center, yaw, side=side, up=0.125)
        add_cube_obj(f"{prefix}_Longitudinal_Grating_Flatbar_{j:02d}", pos, (length * 0.94, 0.046, 0.034), MAT_STEEL, parent, yaw, bevel=0.003)


def add_guardrail_run(prefix, p0, p1, deck_y0, deck_y1, side_offset, parent, rail_mat=MAT_YELLOW):
    add_cylinder_between(f"{prefix}_TopRail", (p0[0], deck_y0 + RAIL_TOP_HEIGHT, p0[2]), (p1[0], deck_y1 + RAIL_TOP_HEIGHT, p1[2]), 0.043, rail_mat, parent, vertices=16)
    add_cylinder_between(f"{prefix}_MidRail", (p0[0], deck_y0 + RAIL_MID_HEIGHT, p0[2]), (p1[0], deck_y1 + RAIL_MID_HEIGHT, p1[2]), 0.030, MAT_STEEL, parent, vertices=12)
    length = math.dist((p0[0], p0[2]), (p1[0], p1[2]))
    posts = max(2, int(length / 2.35) + 1)
    for i in range(posts):
        t = i / max(1, posts - 1)
        x = p0[0] + (p1[0] - p0[0]) * t
        y = deck_y0 + (deck_y1 - deck_y0) * t
        z = p0[2] + (p1[2] - p0[2]) * t
        add_post(f"{prefix}_Post_{i:02d}", x, z, y + 0.02, y + RAIL_TOP_HEIGHT + 0.04, rail_mat, parent)


def add_deck_guardrails(prefix, center, length, width, yaw, deck_y, parent, open_ends=True, omit_sides=None):
    omit_sides = omit_sides or set()
    for side, tag in ((1, "North"), (-1, "South")):
        if tag in omit_sides:
            continue
        a = point_offset(center, yaw, along=-length * 0.5, side=side * width * 0.5)
        b = point_offset(center, yaw, along=length * 0.5, side=side * width * 0.5)
        add_guardrail_run(f"{prefix}_{tag}Rail", a, b, deck_y, deck_y, side * width * 0.5, parent)
        toe = point_offset(center, yaw, side=side * width * 0.5, up=TOE_BOARD_HEIGHT)
        add_cube_obj(f"{prefix}_{tag}_ToeBoard", toe, (length, 0.18, 0.065), MAT_EDGE, parent, yaw, bevel=0.005)

    if not open_ends:
        for along, tag in ((-length * 0.5, "WestEnd"), (length * 0.5, "EastEnd")):
            a = point_offset(center, yaw, along=along, side=width * 0.5)
            b = point_offset(center, yaw, along=along, side=-width * 0.5)
            add_guardrail_run(f"{prefix}_{tag}", a, b, deck_y, deck_y, 0.0, parent)


def add_supports(prefix, center, length, width, yaw, deck_y, parent, spacing=6.6):
    if deck_y < 1.05:
        return
    count = max(2, min(12, int(length / spacing) + 1))
    for i in range(count):
        along = -length * 0.5 + length * i / max(1, count - 1)
        for side, side_tag in ((1, "L"), (-1, "R")):
            x, _, z = point_offset(center, yaw, along=along, side=side * width * 0.39)
            add_cube_obj(f"{prefix}_Support_{side_tag}_{i:02d}_Concrete_Footing", (x, 0.08, z), (0.55, 0.16, 0.55), MAT_CONCRETE, parent, yaw, bevel=0.018)
            add_cube_obj(f"{prefix}_Support_{side_tag}_{i:02d}_BasePlate", (x, 0.205, z), (0.34, 0.05, 0.34), MAT_BOLT, parent, yaw, bevel=0.008)
            add_cylinder_between(f"{prefix}_Support_{side_tag}_{i:02d}_Column", (x, 0.22, z), (x, deck_y - 0.14, z), 0.058, MAT_STEEL, parent, vertices=14)
            for bx, bz in ((0.14, 0.14), (-0.14, 0.14), (0.14, -0.14), (-0.14, -0.14)):
                add_cube_obj(f"{prefix}_Support_{side_tag}_{i:02d}_AnchorBolt_{bx:+.2f}_{bz:+.2f}", (x + bx, 0.265, z + bz), (0.040, 0.060, 0.040), MAT_BOLT, parent, yaw, bevel=0.004)

        if i < count - 1:
            a = point_offset(center, yaw, along=along, side=width * 0.39)
            b = point_offset(center, yaw, along=-length * 0.5 + length * (i + 1) / max(1, count - 1), side=-width * 0.39)
            add_cylinder_between(f"{prefix}_XBrace_A_{i:02d}", (a[0], max(0.35, deck_y * 0.24), a[2]), (b[0], deck_y * 0.78, b[2]), 0.026, MAT_STEEL, parent, vertices=10)
            add_cylinder_between(f"{prefix}_XBrace_B_{i:02d}", (a[0], deck_y * 0.78, a[2]), (b[0], max(0.35, deck_y * 0.24), b[2]), 0.026, MAT_STEEL, parent, vertices=10)


def add_catwalk(prefix, center, length, width, yaw, parent, support=True, open_ends=True, dense_grating=False):
    module = make_empty(f"{prefix}_Access_Module", parent)
    deck_y = center[1]
    add_cube_obj(f"{prefix}_Heavy_Grated_Deck", center, (length, 0.15, width), MAT_DARK, module, yaw, bevel=0.016)
    add_cube_obj(f"{prefix}_Center_Spine_Channel", (center[0], deck_y - 0.12, center[2]), (length, 0.16, 0.10), MAT_STEEL, module, yaw, bevel=0.006)
    for side, tag in ((1, "NorthEdge"), (-1, "SouthEdge")):
        edge = point_offset(center, yaw, side=side * width * 0.5, up=0.11)
        channel = point_offset(center, yaw, side=side * (width * 0.5 + 0.055), up=-0.12)
        add_cube_obj(f"{prefix}_{tag}_Yellow_Nosing", edge, (length, 0.070, 0.115), MAT_EDGE, module, yaw, bevel=0.006)
        add_cube_obj(f"{prefix}_{tag}_Outer_Channel", channel, (length, 0.18, 0.085), MAT_STEEL, module, yaw, bevel=0.006)
    add_grating(prefix, center, length, width, yaw, module, dense=dense_grating)
    add_deck_guardrails(prefix, center, length, width, yaw, deck_y, module, open_ends=open_ends)
    if support:
        add_supports(prefix, center, length, width, yaw, deck_y, module)
    return module


def add_bridge_between(prefix, p0, p1, width, parent, support=True):
    dx = p1[0] - p0[0]
    dz = p1[2] - p0[2]
    length = math.sqrt(dx * dx + dz * dz)
    yaw = math.atan2(dz, dx)
    center = ((p0[0] + p1[0]) * 0.5, (p0[1] + p1[1]) * 0.5, (p0[2] + p1[2]) * 0.5)
    return add_catwalk(prefix, center, length, width, yaw, parent, support=support, open_ends=True)


def add_professional_stair(prefix, top_point, yaw, total_rise, width, parent):
    steps = max(4, math.ceil(total_rise / STAIR_RISE_TARGET))
    rise = total_rise / steps
    run = STAIR_TREAD_RUN
    horizontal = steps * run
    dx, dz, px, pz = vec_from_yaw(yaw)
    bottom = (top_point[0] - dx * horizontal, top_point[1] - total_rise, top_point[2] - dz * horizontal)
    module = make_empty(f"{prefix}_Industrial_Stair_Flight_OSHA_36deg", parent)

    angle = math.atan2(total_rise, horizontal)
    for i in range(steps):
        center = (
            bottom[0] + dx * (i + 0.5) * run,
            bottom[1] + (i + 0.5) * rise,
            bottom[2] + dz * (i + 0.5) * run,
        )
        add_cube_obj(f"{prefix}_Tread_Uniform_{i:02d}", center, (run * 0.92, 0.070, width), MAT_DARK, module, yaw, bevel=0.008)
        add_cube_obj(f"{prefix}_Front_Yellow_Nosing_{i:02d}", point_offset(center, yaw, along=run * 0.40, up=0.050), (0.060, 0.050, width * 0.96), MAT_EDGE, module, yaw + math.pi * 0.5, bevel=0.004)
        add_cube_obj(f"{prefix}_Back_Riser_Plate_{i:02d}", point_offset(center, yaw, along=-run * 0.45, up=-rise * 0.36), (0.040, rise * 0.72, width * 0.90), MAT_STEEL, module, yaw + math.pi * 0.5, bevel=0.003)
        for lane in (-0.30, 0.0, 0.30):
            add_cube_obj(f"{prefix}_Tread_Serrated_Flatbar_{i:02d}_{lane:+.1f}", point_offset(center, yaw, side=lane * width, up=0.052), (run * 0.74, 0.020, 0.018), MAT_STEEL, module, yaw, bevel=0.002)

    for side, tag in ((1, "L"), (-1, "R")):
        side_offset = side * width * 0.58
        stringer_bottom = point_offset(bottom, yaw, side=side_offset, up=0.04)
        stringer_top = point_offset(top_point, yaw, side=side_offset, up=0.04)
        add_cylinder_between(f"{prefix}_Sloped_Stringer_{tag}", stringer_bottom, stringer_top, 0.048, MAT_STEEL, module, vertices=12)
        add_cube_obj(f"{prefix}_Stringer_Web_{tag}", ((stringer_bottom[0] + stringer_top[0]) * 0.5, (stringer_bottom[1] + stringer_top[1]) * 0.5, (stringer_bottom[2] + stringer_top[2]) * 0.5), (horizontal, 0.090, 0.040), MAT_STEEL, module, yaw, bevel=0.004)

        rail_bottom = point_offset(bottom, yaw, side=side_offset, up=0.10)
        rail_top = point_offset(top_point, yaw, side=side_offset, up=0.10)
        add_guardrail_run(f"{prefix}_SlopedGuard_{tag}", rail_bottom, rail_top, bottom[1], top_point[1], side_offset, module)

        # Return rails into landings so rail looks continuous, not cut off.
        ret_low_a = point_offset(bottom, yaw, along=-0.55, side=side_offset, up=0.0)
        ret_low_b = point_offset(bottom, yaw, side=side_offset, up=0.0)
        ret_high_a = point_offset(top_point, yaw, side=side_offset, up=0.0)
        ret_high_b = point_offset(top_point, yaw, along=0.55, side=side_offset, up=0.0)
        add_cylinder_between(f"{prefix}_Lower_ReturnRail_{tag}", (ret_low_a[0], bottom[1] + RAIL_TOP_HEIGHT, ret_low_a[2]), (ret_low_b[0], bottom[1] + RAIL_TOP_HEIGHT, ret_low_b[2]), 0.043, MAT_YELLOW, module, vertices=16)
        add_cylinder_between(f"{prefix}_Upper_ReturnRail_{tag}", (ret_high_a[0], top_point[1] + RAIL_TOP_HEIGHT, ret_high_a[2]), (ret_high_b[0], top_point[1] + RAIL_TOP_HEIGHT, ret_high_b[2]), 0.043, MAT_YELLOW, module, vertices=16)

    add_cube_obj(f"{prefix}_Bottom_Landing_Threshold", point_offset(bottom, yaw, along=-0.36, up=0.02), (0.72, 0.055, width * 1.22), MAT_EDGE, module, yaw, bevel=0.006)
    add_cube_obj(f"{prefix}_Top_Landing_Threshold", point_offset(top_point, yaw, along=0.36, up=0.02), (0.72, 0.055, width * 1.22), MAT_EDGE, module, yaw, bevel=0.006)
    add_cube_obj(f"{prefix}_Spec_Tag_36deg", point_offset(top_point, yaw, along=-0.4, side=-width * 0.78, up=0.72), (0.54, 0.20, 0.035), MAT_SIGN, module, yaw + math.pi * 0.5, bevel=0.004)
    return bottom, top_point, angle


def add_switchback_stair_tower(prefix, parent):
    module = make_empty(f"{prefix}_Main_Switchback_Stair_Tower", parent)

    landing_specs = [
        ("L00_Lower_Bridge_Interface", (72.35, 3.56, 43.10), 3.65, 3.20, 0.0),
        ("L01_First_Return_Landing", (75.20, 5.39, 43.10), 3.65, 3.20, 0.0),
        ("L02_Second_Return_Landing", (72.35, 7.22, 45.90), 3.65, 3.20, 0.0),
        ("L03_Top_Service_Landing", (75.20, 9.04, 45.90), 3.65, 3.20, 0.0),
    ]

    for tag, center, length, width, yaw in landing_specs:
        landing = add_catwalk(f"{prefix}_{tag}", center, length, width, yaw, module, support=True, open_ends=False, dense_grating=True)
        landing.name = f"{prefix}_{tag}_Professional_Grated_Landing"

    # Four obvious switchback flights. This makes the stair read as real stairs from the main viewport.
    add_professional_stair(f"{prefix}_Flight_01_Lower_To_Return", (75.20, 5.39, 43.10), 0.0, 1.83, 1.65, module)
    add_professional_stair(f"{prefix}_Flight_02_Return_Back", (72.35, 7.22, 45.90), math.pi, 1.83, 1.65, module)
    add_professional_stair(f"{prefix}_Flight_03_To_Top", (75.20, 9.04, 45.90), 0.0, 1.82, 1.65, module)

    add_bridge_between(f"{prefix}_Lower_Bridge_Connector", (69.90, 3.56, 43.10), (72.35, 3.56, 43.10), 2.10, module, support=True)
    add_bridge_between(f"{prefix}_Top_Platform_Connector", (75.20, 9.04, 45.90), (89.15, 9.04, 44.25), 2.10, module, support=True)

    # Rectangular tower frame and diagonal cross bracing, industrial readable from distance.
    x_values = [70.45, 77.10]
    z_values = [41.15, 47.90]
    for ix, x in enumerate(x_values):
        for iz, z in enumerate(z_values):
            add_cube_obj(f"{prefix}_Tower_Column_{ix}_{iz}_BasePlate", (x, 0.20, z), (0.42, 0.060, 0.42), MAT_BOLT, module, 0.0, bevel=0.006)
            add_cube_obj(f"{prefix}_Tower_Column_{ix}_{iz}_ConcretePad", (x, 0.08, z), (0.62, 0.16, 0.62), MAT_CONCRETE, module, 0.0, bevel=0.010)
            add_cylinder_between(f"{prefix}_Tower_Column_{ix}_{iz}", (x, 0.22, z), (x, 10.35, z), 0.070, MAT_STEEL, module, vertices=14)
    for y in (3.56, 5.39, 7.22, 9.04):
        add_cylinder_between(f"{prefix}_Tower_Frame_North_{y:.1f}", (70.45, y, 41.15), (77.10, y, 41.15), 0.043, MAT_STEEL, module, vertices=12)
        add_cylinder_between(f"{prefix}_Tower_Frame_South_{y:.1f}", (70.45, y, 47.90), (77.10, y, 47.90), 0.043, MAT_STEEL, module, vertices=12)
        add_cylinder_between(f"{prefix}_Tower_Frame_West_{y:.1f}", (70.45, y, 41.15), (70.45, y, 47.90), 0.043, MAT_STEEL, module, vertices=12)
        add_cylinder_between(f"{prefix}_Tower_Frame_East_{y:.1f}", (77.10, y, 41.15), (77.10, y, 47.90), 0.043, MAT_STEEL, module, vertices=12)
    for x in x_values:
        add_cylinder_between(f"{prefix}_Tower_XBrace_A_{x:.1f}", (x, 0.70, 41.15), (x, 9.85, 47.90), 0.030, MAT_STEEL, module, vertices=10)
        add_cylinder_between(f"{prefix}_Tower_XBrace_B_{x:.1f}", (x, 9.85, 41.15), (x, 0.70, 47.90), 0.030, MAT_STEEL, module, vertices=10)
    add_sign(f"{prefix}_Tower_Tag", (76.80, 8.80, 47.70), 0.0, module)
    return module


def add_right_emergency_switchback_tower(prefix, parent):
    module = make_empty(f"{prefix}_Right_Emergency_Switchback_Tower", parent)
    landing_specs = [
        ("L00_Ground", (127.20, 0.72, 55.20)),
        ("L01_Return", (123.75, 2.56, 55.20)),
        ("L02_Return", (127.20, 4.40, 58.20)),
        ("L03_Return", (123.75, 6.24, 58.20)),
        ("L04_Return", (127.20, 8.08, 55.20)),
        ("L05_Top", (123.75, 9.92, 55.20)),
    ]

    for tag, center in landing_specs:
        landing = add_catwalk(f"{prefix}_{tag}", center, 3.75, 3.10, 0.0, module, support=True, open_ends=False, dense_grating=True)
        landing.name = f"{prefix}_{tag}_Professional_Return_Landing"

    add_professional_stair(f"{prefix}_Flight_01", (123.75, 2.56, 55.20), math.pi, 1.84, 1.55, module)
    add_professional_stair(f"{prefix}_Flight_02", (127.20, 4.40, 58.20), 0.0, 1.84, 1.55, module)
    add_professional_stair(f"{prefix}_Flight_03", (123.75, 6.24, 58.20), math.pi, 1.84, 1.55, module)
    add_professional_stair(f"{prefix}_Flight_04", (127.20, 8.08, 55.20), 0.0, 1.84, 1.55, module)
    add_professional_stair(f"{prefix}_Flight_05", (123.75, 9.92, 55.20), math.pi, 1.84, 1.55, module)

    add_bridge_between(f"{prefix}_Top_Platform_Connector", (123.75, 9.92, 55.20), (118.95, 9.92, 51.05), 2.15, module, support=True)

    x_values = [121.20, 129.15]
    z_values = [53.25, 60.10]
    for ix, x in enumerate(x_values):
        for iz, z in enumerate(z_values):
            add_cube_obj(f"{prefix}_Tower_Column_{ix}_{iz}_ConcretePad", (x, 0.08, z), (0.66, 0.16, 0.66), MAT_CONCRETE, module, 0.0, bevel=0.010)
            add_cube_obj(f"{prefix}_Tower_Column_{ix}_{iz}_BasePlate", (x, 0.20, z), (0.44, 0.060, 0.44), MAT_BOLT, module, 0.0, bevel=0.006)
            add_cylinder_between(f"{prefix}_Tower_Column_{ix}_{iz}", (x, 0.22, z), (x, 10.95, z), 0.074, MAT_STEEL, module, vertices=14)

    for y in (0.72, 2.56, 4.40, 6.24, 8.08, 9.92):
        add_cylinder_between(f"{prefix}_Tower_Frame_North_{y:.1f}", (121.20, y, 53.25), (129.15, y, 53.25), 0.045, MAT_STEEL, module, vertices=12)
        add_cylinder_between(f"{prefix}_Tower_Frame_South_{y:.1f}", (121.20, y, 60.10), (129.15, y, 60.10), 0.045, MAT_STEEL, module, vertices=12)
        add_cylinder_between(f"{prefix}_Tower_Frame_West_{y:.1f}", (121.20, y, 53.25), (121.20, y, 60.10), 0.045, MAT_STEEL, module, vertices=12)
        add_cylinder_between(f"{prefix}_Tower_Frame_East_{y:.1f}", (129.15, y, 53.25), (129.15, y, 60.10), 0.045, MAT_STEEL, module, vertices=12)

    for x in x_values:
        add_cylinder_between(f"{prefix}_Tower_XBrace_A_{x:.1f}", (x, 0.75, 53.25), (x, 10.45, 60.10), 0.030, MAT_STEEL, module, vertices=10)
        add_cylinder_between(f"{prefix}_Tower_XBrace_B_{x:.1f}", (x, 10.45, 53.25), (x, 0.75, 60.10), 0.030, MAT_STEEL, module, vertices=10)
    add_sign(f"{prefix}_Tower_Tag", (121.55, 9.45, 59.85), 0.0, module)
    return module


def add_sign(prefix, center, yaw, parent):
    add_cube_obj(f"{prefix}_Blue_Access_Sign_Backplate", center, (1.08, 0.42, 0.045), MAT_SIGN, parent, yaw, bevel=0.010)
    add_cube_obj(f"{prefix}_White_Label_Stripe", (center[0], center[1] + 0.012, center[2]), (0.82, 0.060, 0.055), MAT_WHITE, parent, yaw, bevel=0.003)


def add_vertical_access_ladder(prefix, base, height, yaw, parent):
    module = make_empty(f"{prefix}_Caged_Service_Ladder", parent)
    width = 0.55
    left = point_offset(base, yaw, side=width * 0.5)
    right = point_offset(base, yaw, side=-width * 0.5)
    add_cylinder_between(f"{prefix}_Left_Rail", (left[0], base[1], left[2]), (left[0], base[1] + height, left[2]), 0.028, MAT_YELLOW, module, vertices=12)
    add_cylinder_between(f"{prefix}_Right_Rail", (right[0], base[1], right[2]), (right[0], base[1] + height, right[2]), 0.028, MAT_YELLOW, module, vertices=12)
    rung_count = max(6, int(height / 0.31))
    for i in range(rung_count + 1):
        y = base[1] + 0.22 + i * (height - 0.44) / rung_count
        a = point_offset((base[0], y, base[2]), yaw, side=width * 0.48)
        b = point_offset((base[0], y, base[2]), yaw, side=-width * 0.48)
        add_cylinder_between(f"{prefix}_Uniform_Rung_{i:02d}", a, b, 0.023, MAT_STEEL, module, vertices=10)
    for i in range(5):
        y = base[1] + height * (0.24 + i * 0.14)
        cage_center = point_offset((base[0], y, base[2]), yaw, along=-0.38)
        add_cylinder_between(f"{prefix}_Half_Cage_Hoop_{i:02d}_A", point_offset(cage_center, yaw, side=width * 0.70), point_offset(cage_center, yaw, side=-width * 0.70), 0.020, MAT_YELLOW, module, vertices=12)
    return module


def setup_preview(root, render_preview=False):
    bpy.ops.object.light_add(type="SUN", location=(0, 0, 20))
    sun = bpy.context.object
    sun.name = "Preview_Sun_Key_Light"
    sun.data.energy = 2.2
    sun.rotation_euler = (math.radians(45), 0, math.radians(-28))
    sun.parent = root

    bpy.ops.object.camera_add(location=(105, -72, 18), rotation=(math.radians(72), 0, math.radians(49)))
    camera = bpy.context.object
    target = u((77, 5.2, 74))
    direction = target - camera.location
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = camera
    camera.parent = root
    camera.data.lens = 24

    bpy.context.scene.render.resolution_x = 1600
    bpy.context.scene.render.resolution_y = 900
    bpy.context.scene.eevee.taa_render_samples = 64
    if render_preview:
        bpy.context.scene.render.filepath = PREVIEW_PATH
        bpy.ops.render.render(write_still=True)


def build():
    clear_scene()
    root = make_empty("Ore_Tangga_IndustrialAccess_BlenderRig")
    print("BUILD root", flush=True)

    # Elevated crusher access spine: clean decks first, stairs attach to clear landings.
    add_catwalk("Ore_Tangga_02_Lower_Service_Bridge", (50.38, 3.52, 39.93), 40.20, 2.45, 0.0, root, support=True, open_ends=True, dense_grating=True)
    add_catwalk("Ore_Tangga_02_East_Transition_Landing", (71.95, 3.56, 41.15), 3.30, 2.80, math.radians(10), root, support=True, open_ends=True, dense_grating=True)
    add_catwalk("Ore_Tangga_High_Crusher_Top_Landing", (89.30, 9.04, 44.00), 3.60, 2.80, math.radians(8), root, support=True, open_ends=True, dense_grating=True)
    add_switchback_stair_tower("Ore_Tangga_Main", root)
    print("BUILD elevated decks", flush=True)

    stair_yaw = math.radians(10)
    add_professional_stair("Ore_Tangga_Ground_To_Lower", (44.20, 3.52, 39.72), math.radians(11), 3.17, STAIR_WIDTH, root)
    # Main elevated climb is handled by the visible switchback tower above.
    print("BUILD stairs", flush=True)

    add_catwalk("Ore_Tangga_01_Crusher_Service_Platform", (93.90, 9.04, 44.35), 13.20, 3.10, 0.0, root, support=True, open_ends=True, dense_grating=True)
    add_catwalk("Ore_Tangga_00_Discharge_Service_Platform", (118.89, 9.92, 49.49), 13.20, 3.10, 0.0, root, support=True, open_ends=True, dense_grating=True)
    add_bridge_between("Ore_Tangga_High_Crusher_To_Discharge_Bridge", (100.35, 9.46, 44.80), (112.35, 9.82, 48.85), 2.35, root, support=True)
    add_right_emergency_switchback_tower("Ore_Tangga_Right_Emergency", root)
    print("BUILD high bridge", flush=True)

    # Ground-level plant access, still rebuilt as proper industrial catwalk modules.
    add_catwalk("Ore_Tangga_03_Tank_Local_Service_Walkway", (4.95, 0.62, 55.00), 18.60, 2.25, 0.0, root, support=False, open_ends=False)
    add_catwalk("Ore_Tangga_05_Tank_Pump_Service_Walkway", (-22.16, 0.62, 47.13), 15.50, 2.25, 0.0, root, support=False, open_ends=False)
    add_catwalk("Ore_Tangga_04_Ground_Service_Corridor", (7.88, 0.62, 80.80), 92.00, 3.40, 0.0, root, support=False, open_ends=True)
    add_catwalk("Ore_Tangga_07_Return_Service_Corridor", (4.89, 0.62, 126.10), 36.80, 2.25, 0.0, root, support=False, open_ends=True)
    add_catwalk("Ore_Tangga_08_North_Service_Corridor", (23.50, 0.62, 160.40), 30.20, 2.25, 0.0, root, support=False, open_ends=True)
    add_catwalk("Ore_Tangga_09_West_Service_Corridor", (56.89, 0.62, 118.90), 58.00, 2.25, 0.0, root, support=False, open_ends=True)
    print("BUILD ground corridors", flush=True)

    add_vertical_access_ladder("Ore_Tangga_Tank_Service", (12.35, 0.42, 53.92), 4.15, math.radians(90), root)
    add_sign("Ore_Tangga_Inspection", (88.2, 8.75, 42.65), 0.0, root)

    world_anchor = make_empty("Ore_Tangga_World_Anchor_DoNotMove", root)
    world_anchor.empty_display_size = 0.35

    setup_preview(root, render_preview=False)
    print(f"BUILD objects {len(bpy.context.scene.objects)}", flush=True)

    if SAVE_BLEND:
        bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
        print(f"WROTE {BLEND_PATH}", flush=True)

    print("EXPORT fbx start", flush=True)
    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH,
        use_selection=False,
        object_types={"EMPTY", "MESH"},
        apply_unit_scale=True,
        bake_space_transform=False,
        axis_forward="-Z",
        axis_up="Y",
        add_leaf_bones=False,
        use_mesh_modifiers=True,
    )
    print(f"WROTE {FBX_PATH}", flush=True)
    print(f"OBJECTS {len(bpy.context.scene.objects)}", flush=True)


if __name__ == "__main__":
    build()
