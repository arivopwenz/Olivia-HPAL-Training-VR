import math
import os
import random

import bpy
from mathutils import Matrix, Vector


SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
FBX_PATH = os.path.join(SCRIPT_DIR, "level3_slurry_water_tanks_industrial_uv.fbx")
GLB_PATH = os.path.join(SCRIPT_DIR, "level3_slurry_water_tanks_industrial_uv.glb")
BLEND_PATH = os.path.join(SCRIPT_DIR, "level3_slurry_water_tanks_industrial_uv.blend")
ATLAS_PATH = os.path.join(SCRIPT_DIR, "level3_slurry_water_tanks_uv_atlas.png")
PREVIEW_PATH = os.path.join(SCRIPT_DIR, "level3_slurry_water_tanks_preview.png")

PANELS = {
    "steel": (0, 0),
    "dark": (1, 0),
    "yellow": (2, 0),
    "grating": (3, 0),
    "concrete": (0, 1),
    "slurry": (1, 1),
    "water": (2, 1),
    "pipe": (3, 1),
    "rubber": (0, 2),
    "red": (1, 2),
    "green": (2, 2),
    "blue": (3, 2),
    "black": (0, 3),
    "white": (1, 3),
    "rust": (2, 3),
    "glass": (3, 3),
}

ROOT_COLLECTION = None
ROOT_EMPTY = None


def panel_rect(name, pad=0.018):
    col, row = PANELS[name]
    cell = 0.25
    u0 = col * cell + pad
    v0 = row * cell + pad
    return (u0, v0, u0 + cell - pad * 2, v0 + cell - pad * 2)


def clamp01(value):
    return max(0.0, min(1.0, value))


def create_atlas():
    random.seed(37)
    size = 1024
    image = bpy.data.images.new("level3_slurry_water_tanks_uv_atlas", size, size, alpha=True)
    pixels = [0.0] * (size * size * 4)
    base = {
        "steel": (0.55, 0.63, 0.61, 1.0),
        "dark": (0.10, 0.12, 0.12, 1.0),
        "yellow": (0.98, 0.67, 0.05, 1.0),
        "grating": (0.18, 0.20, 0.20, 1.0),
        "concrete": (0.44, 0.43, 0.39, 1.0),
        "slurry": (0.38, 0.23, 0.42, 1.0),
        "water": (0.10, 0.43, 0.72, 1.0),
        "pipe": (0.49, 0.56, 0.58, 1.0),
        "rubber": (0.02, 0.022, 0.020, 1.0),
        "red": (0.78, 0.06, 0.035, 1.0),
        "green": (0.08, 0.55, 0.25, 1.0),
        "blue": (0.04, 0.20, 0.38, 1.0),
        "black": (0.015, 0.015, 0.014, 1.0),
        "white": (0.86, 0.88, 0.84, 1.0),
        "rust": (0.54, 0.25, 0.10, 1.0),
        "glass": (0.18, 0.58, 0.78, 0.82),
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
                noise = (random.random() - 0.5) * 0.05
                rr, gg, bb = r + noise, g + noise, b + noise
                if name in {"steel", "pipe"}:
                    rr += math.sin(nx * math.tau * 8.0) * 0.015
                    gg += math.sin(nx * math.tau * 8.0) * 0.015
                    bb += math.sin(nx * math.tau * 8.0) * 0.015
                    if random.random() < 0.018:
                        rr, gg, bb = rr * 0.65, gg * 0.65, bb * 0.65
                if name == "yellow" and ((x - x0 + y - y0) // 34) % 2 == 0:
                    rr, gg, bb = 0.08, 0.07, 0.055
                if name == "grating" and ((x - x0) % 34 < 5 or (y - y0) % 34 < 5):
                    rr, gg, bb = 0.58, 0.60, 0.58
                if name == "concrete" and random.random() < 0.08:
                    rr, gg, bb = 0.30, 0.29, 0.27
                if name == "slurry":
                    swirl = math.sin((nx * 4.5 + ny * 2.0) * math.tau) * 0.04
                    rr += swirl
                    bb += swirl * 1.5
                    if random.random() < 0.03:
                        rr, gg, bb = 0.58, 0.42, 0.54
                if name == "water":
                    wave = math.sin(nx * math.tau * 7.0 + ny * 1.3) * 0.04
                    rr += wave * 0.3
                    gg += wave
                    bb += wave * 1.4
                if name == "rust" and random.random() < 0.20:
                    rr, gg, bb = 0.66, 0.32, 0.13
                if name == "glass":
                    stripe = 0.08 if (x - x0) % 44 < 6 else 0.0
                    rr += stripe
                    gg += stripe
                    bb += stripe
                i = (y * size + x) * 4
                pixels[i:i + 4] = [clamp01(rr), clamp01(gg), clamp01(bb), a]

    image.pixels.foreach_set(pixels)
    image.filepath_raw = ATLAS_PATH
    image.file_format = "PNG"
    image.save()
    return image


def create_material(image):
    mat = bpy.data.materials.new("M_Level3_SlurryWaterTanks_UVAtlas")
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
    tex.image = image
    mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    if "Metallic" in bsdf.inputs:
        bsdf.inputs["Metallic"].default_value = 0.22
    if "Roughness" in bsdf.inputs:
        bsdf.inputs["Roughness"].default_value = 0.66
    return mat


def create_root():
    global ROOT_COLLECTION, ROOT_EMPTY
    ROOT_COLLECTION = bpy.data.collections.new("Level3_SlurryWaterTanks_Industrial_UV")
    bpy.context.scene.collection.children.link(ROOT_COLLECTION)
    ROOT_EMPTY = bpy.data.objects.new("L3_SlurryWaterTanks_Root", None)
    ROOT_EMPTY.empty_display_type = "PLAIN_AXES"
    ROOT_COLLECTION.objects.link(ROOT_EMPTY)


def put(obj):
    if ROOT_COLLECTION is not None and obj.name not in ROOT_COLLECTION.objects:
        ROOT_COLLECTION.objects.link(obj)
    for col in list(obj.users_collection):
        if col != ROOT_COLLECTION:
            col.objects.unlink(obj)
    if ROOT_EMPTY is not None and obj != ROOT_EMPTY and obj.type not in {"CAMERA", "LIGHT"}:
        obj.parent = ROOT_EMPTY
    return obj


def normalize_uv_to_rect(obj, rect_name):
    if obj.type != "MESH" or not obj.data.uv_layers:
        return
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


def fallback_uv(obj, rect_name):
    uv_layer = obj.data.uv_layers.active or obj.data.uv_layers.new(name="UVAtlas")
    u0, v0, u1, v1 = panel_rect(rect_name)
    corners = [(u0, v0), (u1, v0), (u1, v1), (u0, v1)]
    for poly in obj.data.polygons:
        for idx, li in enumerate(poly.loop_indices):
            uv_layer.data[li].uv = corners[idx % 4]


def assign_uv(obj, rect_name, mat):
    if obj.type != "MESH":
        return obj
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    if not obj.data.uv_layers:
        obj.data.uv_layers.new(name="UVAtlas")
    obj.data.uv_layers.active.name = "UVAtlas"
    fallback_uv(obj, rect_name)

    previous_active = bpy.context.view_layer.objects.active
    previous_selection = list(bpy.context.selected_objects)
    try:
        bpy.ops.object.mode_set(mode="OBJECT")
    except Exception:
        pass
    for selected in previous_selection:
        selected.select_set(False)
    try:
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.smart_project(angle_limit=1.15192, island_margin=0.018)
        bpy.ops.object.mode_set(mode="OBJECT")
        if obj.data.uv_layers.active:
            obj.data.uv_layers.active.name = "UVAtlas"
        normalize_uv_to_rect(obj, rect_name)
    except Exception:
        try:
            bpy.ops.object.mode_set(mode="OBJECT")
        except Exception:
            pass
        fallback_uv(obj, rect_name)
    finally:
        obj.select_set(False)
        for selected in previous_selection:
            if selected.name in bpy.data.objects:
                selected.select_set(True)
        if previous_active is not None and previous_active.name in bpy.data.objects:
            bpy.context.view_layer.objects.active = previous_active
    return obj


def add_bevel(obj, amount=0.02, segments=2):
    if obj.type != "MESH":
        return obj
    bevel = obj.modifiers.new("soft_midpoly_bevel", "BEVEL")
    bevel.width = amount
    bevel.segments = segments
    try:
        bevel.affect = "EDGES"
    except Exception:
        pass
    obj.modifiers.new("weighted_normals", "WEIGHTED_NORMAL")
    for poly in obj.data.polygons:
        poly.use_smooth = True
    return obj


def orientation_from_normal(normal, up_hint=(0, 0, 1)):
    normal = Vector(normal).normalized()
    up = Vector(up_hint).normalized()
    if abs(normal.dot(up)) > 0.95:
        up = Vector((1, 0, 0))
    right = up.cross(normal).normalized()
    true_up = normal.cross(right).normalized()
    matrix = Matrix((
        (right.x, true_up.x, normal.x),
        (right.y, true_up.y, normal.y),
        (right.z, true_up.z, normal.z),
    ))
    return matrix.to_euler()


def box(name, loc, scale, rect, mat, rot=(0, 0, 0), bevel=0.015):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    obj = put(bpy.context.object)
    obj.name = name
    obj.dimensions = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    assign_uv(obj, rect, mat)
    if bevel:
        add_bevel(obj, bevel, 2)
    return obj


def panel_box(name, loc, normal, width, height, thickness, rect, mat, bevel=0.006):
    return box(
        name,
        loc,
        (width, height, thickness),
        rect,
        mat,
        rot=orientation_from_normal(normal),
        bevel=bevel,
    )


def cylinder(name, loc, radius, depth, rect, mat, vertices=32, rot=(0, 0, 0), fill="NGON", bevel=0.0):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=depth,
        end_fill_type=fill,
        location=loc,
        rotation=rot,
    )
    obj = put(bpy.context.object)
    obj.name = name
    assign_uv(obj, rect, mat)
    for poly in obj.data.polygons:
        poly.use_smooth = True
    if bevel:
        add_bevel(obj, bevel, 3)
    return obj


def cone(name, loc, radius1, radius2, depth, rect, mat, vertices=32, rot=(0, 0, 0), bevel=0.0):
    bpy.ops.mesh.primitive_cone_add(
        vertices=vertices,
        radius1=radius1,
        radius2=radius2,
        depth=depth,
        end_fill_type="NGON",
        location=loc,
        rotation=rot,
    )
    obj = put(bpy.context.object)
    obj.name = name
    assign_uv(obj, rect, mat)
    for poly in obj.data.polygons:
        poly.use_smooth = True
    if bevel:
        add_bevel(obj, bevel, 2)
    return obj


def sphere(name, loc, radius, rect, mat, segments=24, rings=12, scale=(1, 1, 1), bevel=0.0):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=radius, location=loc)
    obj = put(bpy.context.object)
    obj.name = name
    obj.scale.x *= scale[0]
    obj.scale.y *= scale[1]
    obj.scale.z *= scale[2]
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    assign_uv(obj, rect, mat)
    for poly in obj.data.polygons:
        poly.use_smooth = True
    if bevel:
        add_bevel(obj, bevel, 2)
    return obj


def torus(name, loc, major_radius, minor_radius, rect, mat, major_segments=64, minor_segments=8, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(
        major_segments=major_segments,
        minor_segments=minor_segments,
        major_radius=major_radius,
        minor_radius=minor_radius,
        location=loc,
        rotation=rot,
    )
    obj = put(bpy.context.object)
    obj.name = name
    assign_uv(obj, rect, mat)
    for poly in obj.data.polygons:
        poly.use_smooth = True
    return obj


def cylinder_axis(name, center, direction, radius, depth, rect, mat, vertices=16, bevel=0.0):
    direction = Vector(direction)
    if direction.length < 0.0001:
        direction = Vector((0, 0, 1))
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=center)
    obj = put(bpy.context.object)
    obj.name = name
    obj.rotation_euler = Vector((0, 0, 1)).rotation_difference(direction.normalized()).to_euler()
    assign_uv(obj, rect, mat)
    for poly in obj.data.polygons:
        poly.use_smooth = True
    if bevel:
        add_bevel(obj, bevel, 2)
    return obj


def cylinder_between(name, a, b, radius, rect, mat, vertices=16, bevel=0.0):
    a = Vector(a)
    b = Vector(b)
    direction = b - a
    return cylinder_axis(name, (a + b) * 0.5, direction, radius, direction.length, rect, mat, vertices, bevel)


def box_between(name, a, b, width, height, rect, mat, bevel=0.012):
    a = Vector(a)
    b = Vector(b)
    direction = b - a
    length = direction.length
    mid = (a + b) * 0.5
    bpy.ops.mesh.primitive_cube_add(size=1, location=mid)
    obj = put(bpy.context.object)
    obj.name = name
    obj.dimensions = (length, width, height)
    obj.rotation_euler = direction.to_track_quat("X", "Z").to_euler()
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    assign_uv(obj, rect, mat)
    if bevel:
        add_bevel(obj, bevel, 2)
    return obj


def make_wavy_disc(name, center, radius, z, rect, mat, rings=9, segments=64, wave=0.035):
    cx, cy = center
    verts = [(cx, cy, z)]
    for r in range(1, rings + 1):
        rr = radius * (r / rings)
        for s in range(segments):
            a = math.tau * s / segments
            h = math.sin(a * 4.0 + r * 0.62) * wave * (r / rings)
            h += math.cos(a * 2.0 - r * 0.3) * wave * 0.45
            verts.append((cx + math.cos(a) * rr, cy + math.sin(a) * rr, z + h))
    faces = []
    for s in range(segments):
        faces.append((0, 1 + s, 1 + ((s + 1) % segments)))
    for r in range(2, rings + 1):
        prev = 1 + (r - 2) * segments
        cur = 1 + (r - 1) * segments
        for s in range(segments):
            faces.append((prev + s, prev + ((s + 1) % segments), cur + ((s + 1) % segments), cur + s))
    mesh = bpy.data.meshes.new(name + "_mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    ROOT_COLLECTION.objects.link(obj)
    obj.parent = ROOT_EMPTY
    assign_uv(obj, rect, mat)
    for poly in obj.data.polygons:
        poly.use_smooth = True
    return obj


def add_arc_pipe(prefix, center, radius, z, angle0, angle1, pipe_radius, rect, mat, segments=12):
    points = []
    for i in range(segments + 1):
        t = i / segments
        a = math.radians(angle0 + (angle1 - angle0) * t)
        points.append((center[0] + math.cos(a) * radius, center[1] + math.sin(a) * radius, z))
    for i in range(len(points) - 1):
        cylinder_between(f"{prefix}_{i:02d}", points[i], points[i + 1], pipe_radius, rect, mat, 10)
    return points


def add_circular_rail(prefix, center, radius, deck_z, height, mat, posts=28, start_deg=0, end_deg=360):
    cx, cy = center
    angles = [math.radians(start_deg + (end_deg - start_deg) * i / posts) for i in range(posts + 1)]
    points = [(cx + math.cos(a) * radius, cy + math.sin(a) * radius, deck_z) for a in angles]
    for i, p in enumerate(points[:-1]):
        cylinder_between(f"{prefix}_Post_{i:02d}", p, (p[0], p[1], p[2] + height), 0.035, "yellow", mat, 12)
    for i in range(len(points) - 1):
        a = points[i]
        b = points[i + 1]
        cylinder_between(
            f"{prefix}_TopRail_{i:02d}",
            (a[0], a[1], deck_z + height),
            (b[0], b[1], deck_z + height),
            0.038,
            "yellow",
            mat,
            12,
        )
        cylinder_between(
            f"{prefix}_MidRail_{i:02d}",
            (a[0], a[1], deck_z + height * 0.55),
            (b[0], b[1], deck_z + height * 0.55),
            0.027,
            "yellow",
            mat,
            10,
        )


def add_rect_rail(prefix, corners, base_z, height, mat):
    points = [(x, y, base_z) for x, y in corners]
    for i, p in enumerate(points):
        cylinder_between(f"{prefix}_Post_{i:02d}", p, (p[0], p[1], p[2] + height), 0.035, "yellow", mat, 12)
    for i in range(len(points)):
        a = points[i]
        b = points[(i + 1) % len(points)]
        cylinder_between(f"{prefix}_TopRail_{i:02d}", (a[0], a[1], base_z + height), (b[0], b[1], base_z + height), 0.038, "yellow", mat, 12)
        cylinder_between(f"{prefix}_MidRail_{i:02d}", (a[0], a[1], base_z + height * 0.55), (b[0], b[1], base_z + height * 0.55), 0.027, "yellow", mat, 10)


def add_tank_feet(prefix, center, radius, count, mat):
    cx, cy = center
    for i in range(count):
        a = math.tau * i / count
        x = cx + math.cos(a) * radius
        y = cy + math.sin(a) * radius
        box(f"{prefix}_FootPad_{i:02d}", (x, y, 0.105), (0.55, 0.42, 0.16), "concrete", mat, rot=(0, 0, a), bevel=0.012)
        cylinder_between(f"{prefix}_ShortLeg_{i:02d}", (x, y, 0.18), (x, y, 0.52), 0.055, "dark", mat, 12)


def add_ladder(prefix, center, radius, angle_deg, bottom_z, top_z, mat, cage=True):
    cx, cy = center
    a = math.radians(angle_deg)
    radial = Vector((math.cos(a), math.sin(a), 0))
    tangent = Vector((-math.sin(a), math.cos(a), 0))
    ladder_center = Vector((cx, cy, 0)) + radial * (radius + 0.26)
    rail_offset = 0.22
    left = ladder_center - tangent * rail_offset
    right = ladder_center + tangent * rail_offset
    cylinder_between(f"{prefix}_LeftRail", (left.x, left.y, bottom_z), (left.x, left.y, top_z), 0.035, "yellow", mat, 12)
    cylinder_between(f"{prefix}_RightRail", (right.x, right.y, bottom_z), (right.x, right.y, top_z), 0.035, "yellow", mat, 12)
    rung_count = max(4, int((top_z - bottom_z) / 0.32))
    for i in range(rung_count):
        z = bottom_z + 0.22 + i * ((top_z - bottom_z - 0.44) / max(1, rung_count - 1))
        cylinder_between(f"{prefix}_Rung_{i:02d}", (left.x, left.y, z), (right.x, right.y, z), 0.025, "yellow", mat, 10)
    if not cage:
        return
    cage_radius = 0.48
    cage_zs = [bottom_z + 1.05 + i * 0.48 for i in range(max(1, int((top_z - bottom_z - 1.0) / 0.48)))]
    cage_anchor = Vector((ladder_center.x, ladder_center.y, 0))
    for idx, z in enumerate(cage_zs):
        points = []
        for j in range(11):
            phi = math.radians(-112 + 224 * j / 10)
            p = cage_anchor + tangent * (math.sin(phi) * cage_radius) + radial * (math.cos(phi) * cage_radius * 0.72)
            points.append((p.x, p.y, z))
        for j in range(len(points) - 1):
            cylinder_between(f"{prefix}_CageHoop_{idx:02d}_{j:02d}", points[j], points[j + 1], 0.018, "yellow", mat, 8)
    if len(cage_zs) >= 2:
        for side, phi in enumerate([-90, 0, 90]):
            offset = tangent * (math.sin(math.radians(phi)) * cage_radius) + radial * (math.cos(math.radians(phi)) * cage_radius * 0.72)
            p = cage_anchor + offset
            cylinder_between(f"{prefix}_CageVertical_{side:02d}", (p.x, p.y, cage_zs[0]), (p.x, p.y, cage_zs[-1]), 0.016, "yellow", mat, 8)


def add_bolt_ring(prefix, center, normal, up, ring_radius, count, mat, bolt_radius=0.035, bolt_depth=0.045):
    normal = Vector(normal).normalized()
    up = Vector(up).normalized()
    if abs(normal.dot(up)) > 0.95:
        up = Vector((1, 0, 0))
    tangent = up.cross(normal).normalized()
    up2 = normal.cross(tangent).normalized()
    for i in range(count):
        a = math.tau * i / count
        p = Vector(center) + tangent * (math.cos(a) * ring_radius) + up2 * (math.sin(a) * ring_radius)
        cylinder_axis(f"{prefix}_Bolt_{i:02d}", p, normal, bolt_radius, bolt_depth, "dark", mat, 10, bevel=0.004)


def add_flange(prefix, point, direction, pipe_radius, mat):
    point = Vector(point)
    direction = Vector(direction).normalized()
    cylinder_axis(f"{prefix}_FlangeDisc", point, direction, pipe_radius * 1.72, 0.10, "pipe", mat, 28, bevel=0.008)
    add_bolt_ring(f"{prefix}_Flange", point + direction * 0.055, direction, (0, 0, 1), pipe_radius * 1.35, 8, mat, bolt_radius=0.020, bolt_depth=0.030)


def add_valve(prefix, loc, direction, pipe_radius, mat):
    direction = Vector(direction).normalized()
    loc = Vector(loc)
    cylinder_axis(f"{prefix}_ValveBody", loc, direction, pipe_radius * 1.55, 0.46, "dark", mat, 24, bevel=0.015)
    add_flange(f"{prefix}_Left", loc - direction * 0.32, direction, pipe_radius, mat)
    add_flange(f"{prefix}_Right", loc + direction * 0.32, direction, pipe_radius, mat)
    cylinder_between(f"{prefix}_Stem", (loc.x, loc.y, loc.z + pipe_radius * 0.9), (loc.x, loc.y, loc.z + 0.72), 0.030, "dark", mat, 10)
    torus(f"{prefix}_YellowHandwheel", (loc.x, loc.y, loc.z + 0.80), 0.18, 0.018, "yellow", mat, 32, 8)
    cylinder_axis(f"{prefix}_HandwheelHub", (loc.x, loc.y, loc.z + 0.80), (0, 0, 1), 0.055, 0.055, "dark", mat, 16, bevel=0.004)


def add_manway(prefix, center, tank_radius, angle_deg, z, mat, label=None):
    a = math.radians(angle_deg)
    normal = Vector((math.cos(a), math.sin(a), 0))
    loc = Vector((center[0], center[1], z)) + normal * (tank_radius + 0.055)
    cylinder_axis(f"{prefix}_RoundManway", loc, normal, 0.38, 0.10, "steel", mat, 32, bevel=0.012)
    cylinder_axis(f"{prefix}_DarkGasket", loc + normal * 0.06, normal, 0.30, 0.035, "dark", mat, 32)
    add_bolt_ring(f"{prefix}_Manway", loc + normal * 0.085, normal, (0, 0, 1), 0.31, 10, mat, bolt_radius=0.018, bolt_depth=0.025)
    if label:
        make_nameplate(f"{prefix}_Label", label, loc + normal * 0.14 + Vector((0, 0, -0.48)), normal, 0.75, 0.18, mat)


def make_nameplate(prefix, text, loc, normal, width, height, mat, plate_rect="blue", text_rect="white"):
    normal = Vector(normal).normalized()
    panel_box(f"{prefix}_Plate", loc, normal, width, height, 0.035, plate_rect, mat, bevel=0.005)
    bpy.ops.object.text_add(location=Vector(loc) + normal * 0.035, rotation=orientation_from_normal(normal))
    txt = put(bpy.context.object)
    txt.name = f"{prefix}_Text"
    txt.data.body = text
    txt.data.align_x = "CENTER"
    txt.data.align_y = "CENTER"
    txt.data.size = height * 0.52
    txt.data.extrude = 0.004
    bpy.context.view_layer.objects.active = txt
    txt.select_set(True)
    bpy.ops.object.convert(target="MESH")
    mesh_txt = bpy.context.object
    mesh_txt.name = f"{prefix}_TextMesh"
    put(mesh_txt)
    assign_uv(mesh_txt, text_rect, mat)
    add_bevel(mesh_txt, 0.001, 1)
    return mesh_txt


def add_tank_band(prefix, center, radius, z, mat, rect="dark"):
    torus(f"{prefix}_Band_{z:.2f}", (center[0], center[1], z), radius, 0.026, rect, mat, 64, 6)


def add_vertical_ribs(prefix, center, radius, z0, z1, count, mat):
    cx, cy = center
    for i in range(count):
        a = math.tau * i / count
        x = cx + math.cos(a) * (radius + 0.015)
        y = cy + math.sin(a) * (radius + 0.015)
        cylinder_between(f"{prefix}_VerticalRib_{i:02d}", (x, y, z0), (x, y, z1), 0.020, "dark", mat, 8)


def add_gauge(prefix, center, tank_radius, angle_deg, z, mat):
    a = math.radians(angle_deg)
    normal = Vector((math.cos(a), math.sin(a), 0))
    loc = Vector((center[0], center[1], z)) + normal * (tank_radius + 0.09)
    cylinder_axis(f"{prefix}_GaugeFace", loc, normal, 0.18, 0.045, "white", mat, 32, bevel=0.004)
    cylinder_axis(f"{prefix}_GaugeRim", loc + normal * 0.03, normal, 0.195, 0.025, "dark", mat, 32, bevel=0.003)
    tangent = Vector((-normal.y, normal.x, 0))
    box_between(
        f"{prefix}_GaugeNeedle",
        loc + normal * 0.055,
        loc + normal * 0.060 + tangent * 0.11 + Vector((0, 0, 0.04)),
        0.018,
        0.010,
        "red",
        mat,
        bevel=0.002,
    )


def build_slurry_tank(mat):
    center = (2.1, 0.05)
    radius = 2.85
    bottom = 0.32
    top = 3.02
    height = top - bottom

    shell = cylinder("L3_SlurryTank_OpenShell_SmoothSteel", (center[0], center[1], bottom + height * 0.5), radius, height, "steel", mat, 72, fill="NOTHING", bevel=0.012)
    solid = shell.modifiers.new("tank_wall_thickness", "SOLIDIFY")
    solid.thickness = 0.075
    solid.offset = 0
    torus("L3_SlurryTank_HeavyTopRimPipe", (center[0], center[1], top), radius + 0.03, 0.060, "pipe", mat, 96, 10)
    torus("L3_SlurryTank_BottomReinforcementRing", (center[0], center[1], bottom + 0.08), radius + 0.02, 0.045, "dark", mat, 96, 8)
    add_tank_band("L3_SlurryTank", center, radius + 0.02, bottom + 0.78, mat, "dark")
    add_tank_band("L3_SlurryTank", center, radius + 0.02, bottom + 1.72, mat, "dark")
    add_vertical_ribs("L3_SlurryTank", center, radius, bottom + 0.20, top - 0.15, 18, mat)
    add_tank_feet("L3_SlurryTank", center, radius * 0.82, 8, mat)

    cylinder("L3_SlurryTank_SlopedBottomDisk", (center[0], center[1], bottom - 0.02), radius * 0.94, 0.18, "steel", mat, 72, bevel=0.010)
    make_wavy_disc("L3_SlurryTank_PurpleSlurry_WavySurface_50Percent", center, radius * 0.82, 1.72, "slurry", mat)
    torus("L3_SlurryTank_SubtleSlurrySurfaceRing", (center[0], center[1], 1.735), radius * 0.46, 0.018, "white", mat, 64, 6)
    torus("L3_SlurryTank_InnerLevelShadowRing", (center[0], center[1], 1.71), radius * 0.86, 0.020, "dark", mat, 64, 6)

    add_circular_rail("L3_SlurryTank_YellowSafetyRail", center, radius + 0.27, top + 0.06, 0.72, mat, posts=30)
    add_ladder("L3_SlurryTank_ServiceLadder", center, radius, -58, 0.18, top + 0.08, mat)

    # Agitator bridge and drive.
    box("L3_SlurryTank_AgitatorBridge_PrimaryBeam", (center[0], center[1], top + 0.36), (radius * 2.25, 0.22, 0.20), "dark", mat, bevel=0.025)
    box("L3_SlurryTank_AgitatorBridge_CrossBrace", (center[0], center[1], top + 0.38), (0.24, radius * 1.85, 0.16), "dark", mat, bevel=0.020)
    cylinder_between("L3_SlurryTank_AgitatorVerticalShaft", (center[0], center[1], top + 0.72), (center[0], center[1], 0.86), 0.070, "dark", mat, 20)
    box("L3_SlurryTank_AgitatorGearbox_Rounded", (center[0], center[1], top + 0.74), (0.76, 0.66, 0.50), "blue", mat, bevel=0.055)
    cylinder("L3_SlurryTank_AgitatorMotor_Horizontal", (center[0] + 0.82, center[1], top + 0.78), 0.28, 0.78, "dark", mat, 32, rot=(0, math.radians(90), 0), bevel=0.018)
    cylinder("L3_SlurryTank_AgitatorMotor_EndCap", (center[0] + 1.24, center[1], top + 0.78), 0.30, 0.08, "pipe", mat, 32, rot=(0, math.radians(90), 0), bevel=0.010)
    for i, z in enumerate([1.06, 1.38]):
        box(f"L3_SlurryTank_ImpellerBlade_A_{i}", (center[0], center[1], z), (1.12, 0.14, 0.065), "dark", mat, rot=(0, 0, math.radians(22 + i * 38)), bevel=0.014)
        box(f"L3_SlurryTank_ImpellerBlade_B_{i}", (center[0], center[1], z), (0.14, 1.12, 0.065), "dark", mat, rot=(0, 0, math.radians(22 + i * 38)), bevel=0.014)

    # Side equipment and marks.
    add_manway("L3_SlurryTank_FrontInspection", center, radius, -90, 1.70, mat, "INSPECTION")
    add_gauge("L3_SlurryTank_PressureGauge", center, radius, -122, 2.38, mat)
    make_nameplate("L3_SlurryTank_MainNameplate", "SLURRY TANK", (center[0], center[1] - radius - 0.07, 2.55), (0, -1, 0), 1.45, 0.28, mat)
    for idx, (label, z) in enumerate([("25%", 1.05), ("50%", 1.72), ("75%", 2.34)]):
        make_nameplate(f"L3_SlurryTank_LevelMarker_{idx}", label, (center[0] - 1.15 + idx * 0.08, center[1] - radius - 0.08, z), (0, -1, 0), 0.42, 0.16, mat, plate_rect="dark", text_rect="white")

    # Slurry outlet to pump side.
    p0 = (center[0] + radius + 0.02, center[1] + 0.75, 0.78)
    p1 = (center[0] + radius + 1.08, center[1] + 0.75, 0.78)
    p2 = (center[0] + radius + 3.00, center[1] + 0.75, 0.78)
    cylinder_between("L3_SlurryTank_BottomOutlet_Nozzle", p0, p1, 0.17, "pipe", mat, 24, bevel=0.004)
    cylinder_between("L3_SlurryTank_OutletPipe_ToPump", p1, p2, 0.17, "pipe", mat, 24, bevel=0.004)
    add_flange("L3_SlurryTank_TankOutlet", p0, (1, 0, 0), 0.17, mat)
    add_valve("L3_SlurryTank_OutletIsolationValve", (p1[0] + 0.55, p1[1], p1[2]), (1, 0, 0), 0.17, mat)
    box("L3_SlurryTank_OutletPipeSupport_Saddle", (p2[0] - 0.45, p2[1], 0.40), (0.24, 0.42, 0.38), "dark", mat, bevel=0.012)
    box("L3_SlurryTank_OutletPipeSupport_Base", (p2[0] - 0.45, p2[1], 0.12), (0.68, 0.62, 0.14), "concrete", mat, bevel=0.010)

    return center, radius, top


def build_water_tank(mat):
    center = (-4.25, 0.15)
    radius = 1.45
    bottom = 0.35
    shell_top = 3.22

    cylinder("L3_WaterTank_VerticalShell_SmoothSteel", (center[0], center[1], (bottom + shell_top) * 0.5), radius, shell_top - bottom, "steel", mat, 56, bevel=0.012)
    cone("L3_WaterTank_ShallowConicalRoof", (center[0], center[1], shell_top + 0.26), radius + 0.04, 0.34, 0.52, "steel", mat, 56, bevel=0.010)
    cylinder("L3_WaterTank_TopVentCap", (center[0], center[1], shell_top + 0.62), 0.25, 0.18, "dark", mat, 28, bevel=0.010)
    torus("L3_WaterTank_RoofRim", (center[0], center[1], shell_top + 0.02), radius + 0.02, 0.040, "pipe", mat, 64, 8)
    torus("L3_WaterTank_BottomRing", (center[0], center[1], bottom + 0.05), radius + 0.02, 0.040, "dark", mat, 64, 8)
    add_tank_band("L3_WaterTank", center, radius + 0.02, 1.35, mat, "dark")
    add_tank_band("L3_WaterTank", center, radius + 0.02, 2.28, mat, "dark")
    add_vertical_ribs("L3_WaterTank", center, radius, bottom + 0.16, shell_top - 0.10, 12, mat)
    add_tank_feet("L3_WaterTank", center, radius * 0.74, 6, mat)
    add_circular_rail("L3_WaterTank_TopSafetyRail", center, radius + 0.22, shell_top + 0.28, 0.62, mat, posts=20)
    add_ladder("L3_WaterTank_AccessLadder", center, radius, -152, 0.18, shell_top + 0.35, mat, cage=True)

    # Level sight glass.
    normal = Vector((0, -1, 0))
    x = center[0] - 0.52
    y = center[1] - radius - 0.09
    cylinder_between("L3_WaterTank_BlueSightGlass_Tube", (x, y, 0.88), (x, y, 2.82), 0.045, "glass", mat, 16)
    for i, z in enumerate([0.90, 1.38, 1.86, 2.34, 2.82]):
        box(f"L3_WaterTank_SightGlass_LevelTick_{i:02d}", (x + 0.16, y - 0.01, z), (0.20, 0.032, 0.035), "white", mat, bevel=0.002)
    add_manway("L3_WaterTank_FrontManway", center, radius, -90, 1.68, mat, "MAKEUP")
    add_gauge("L3_WaterTank_LevelGauge", center, radius, -52, 2.55, mat)
    make_nameplate("L3_WaterTank_MainNameplate", "WATER TANK", (center[0], center[1] - radius - 0.07, 2.96), normal, 1.20, 0.24, mat)

    # Outlet nozzle.
    p0 = (center[0] + radius + 0.01, center[1] - 0.95, 1.35)
    p1 = (center[0] + radius + 0.62, center[1] - 0.95, 1.35)
    cylinder_between("L3_WaterTank_OutletNozzle", p0, p1, 0.12, "pipe", mat, 20, bevel=0.004)
    add_flange("L3_WaterTank_OutletFlange", p0, (1, 0, 0), 0.12, mat)
    return center, radius, shell_top


def build_pipe_routing(mat, water_center, water_radius, slurry_center, slurry_radius):
    # Make-up water line climbs from small tank, crosses the service gap, then drops into slurry tank.
    points = [
        (water_center[0] + water_radius + 0.58, water_center[1] - 0.95, 1.35),
        (-2.55, water_center[1] - 0.95, 1.35),
        (-2.55, water_center[1] - 0.95, 2.70),
        (slurry_center[0] - slurry_radius - 0.05, water_center[1] - 0.95, 2.70),
        (slurry_center[0] - slurry_radius + 0.55, water_center[1] - 0.95, 2.70),
    ]
    for i in range(len(points) - 1):
        cylinder_between(f"L3_ProcessWaterPipe_Segment_{i:02d}", points[i], points[i + 1], 0.12, "pipe", mat, 22, bevel=0.004)
    for i, p in enumerate(points[1:-1]):
        sphere(f"L3_ProcessWaterPipe_CleanElbow_{i:02d}", p, 0.17, "pipe", mat, 20, 10, scale=(1, 1, 1), bevel=0.004)
    add_valve("L3_ProcessWaterPipe_ControlValve", (-1.55, water_center[1] - 0.95, 2.70), (1, 0, 0), 0.12, mat)
    add_flange("L3_ProcessWaterPipe_SlurryInletFlange", points[-1], (1, 0, 0), 0.12, mat)
    cylinder_between("L3_SlurryTank_WaterInlet_Downcomer", points[-1], (points[-1][0] + 0.22, points[-1][1], 2.25), 0.105, "pipe", mat, 20, bevel=0.004)

    for i, x in enumerate([-2.55, -1.05]):
        box(f"L3_ProcessWaterPipe_SupportPost_{i:02d}", (x, water_center[1] - 0.95, 1.52), (0.12, 0.12, 2.15), "dark", mat, bevel=0.006)
        box(f"L3_ProcessWaterPipe_SupportFoot_{i:02d}", (x, water_center[1] - 0.95, 0.11), (0.48, 0.48, 0.14), "concrete", mat, bevel=0.008)

    # Ore/slurry feed chute into the open tank.
    chute_start = (slurry_center[0] - slurry_radius - 1.15, slurry_center[1] + 1.18, 3.35)
    chute_end = (slurry_center[0] - slurry_radius + 0.58, slurry_center[1] + 0.76, 2.94)
    box_between("L3_SlurryTank_InclinedOreFeedChute", chute_start, chute_end, 0.52, 0.24, "dark", mat, bevel=0.035)
    box_between("L3_SlurryTank_FeedChute_RubberLiner", (chute_start[0], chute_start[1], chute_start[2] + 0.02), (chute_end[0], chute_end[1], chute_end[2] + 0.02), 0.42, 0.07, "rubber", mat, bevel=0.014)


def build_service_platform(mat):
    box("L3_TankArea_ConcretePad", (-0.80, 0.05, 0.04), (11.5, 7.10, 0.16), "concrete", mat, bevel=0.025)
    box("L3_TankArea_DrainChannel_Front", (-0.80, -3.28, 0.14), (10.7, 0.18, 0.08), "dark", mat, bevel=0.006)
    box("L3_TankArea_DrainChannel_Right", (5.22, -0.10, 0.14), (0.18, 5.15, 0.08), "dark", mat, bevel=0.006)

    # Bridge catwalk between both tanks, visually clear but not overbuilt.
    box("L3_TankArea_BetweenTanks_GratingCatwalk", (-2.00, -2.18, 3.30), (2.55, 0.78, 0.12), "grating", mat, bevel=0.012)
    box("L3_TankArea_Catwalk_LeftStringer", (-2.00, -2.61, 3.20), (2.70, 0.08, 0.14), "dark", mat, bevel=0.006)
    box("L3_TankArea_Catwalk_RightStringer", (-2.00, -1.75, 3.20), (2.70, 0.08, 0.14), "dark", mat, bevel=0.006)
    add_rect_rail(
        "L3_TankArea_CatwalkRail",
        [(-3.20, -2.62), (-0.82, -2.62), (-0.82, -1.74), (-3.20, -1.74)],
        3.35,
        0.72,
        mat,
    )
    for i, x in enumerate([-3.02, -1.10]):
        cylinder_between(f"L3_TankArea_CatwalkSupport_{i:02d}", (x, -2.20, 0.18), (x, -2.20, 3.25), 0.045, "dark", mat, 12)
        box(f"L3_TankArea_CatwalkSupportBase_{i:02d}", (x, -2.20, 0.10), (0.46, 0.46, 0.12), "concrete", mat, bevel=0.008)


def build_details(mat):
    # Pump skid placeholder near slurry outlet to make the outlet line process-readable.
    box("L3_SlurryPump_SkidFrame", (7.05, 0.80, 0.26), (1.15, 0.82, 0.20), "dark", mat, bevel=0.016)
    cylinder("L3_SlurryPump_Casing_Rounded", (6.72, 0.80, 0.70), 0.36, 0.34, "blue", mat, 36, rot=(0, math.radians(90), 0), bevel=0.018)
    cylinder("L3_SlurryPump_Motor", (7.42, 0.80, 0.70), 0.28, 0.74, "dark", mat, 32, rot=(0, math.radians(90), 0), bevel=0.015)
    cylinder_between("L3_SlurryPump_DischargeStub", (6.42, 0.80, 0.70), (5.60, 0.80, 0.74), 0.14, "pipe", mat, 22, bevel=0.004)
    torus("L3_SlurryPump_CouplingGuard_Yellow", (7.07, 0.80, 0.70), 0.24, 0.028, "yellow", mat, 32, 8, rot=(0, math.radians(90), 0))

    # Safety beacons and small junction box.
    box("L3_TankArea_FieldJunctionBox", (0.65, -3.38, 1.02), (0.52, 0.18, 0.72), "dark", mat, bevel=0.025)
    cylinder("L3_TankArea_GreenStatusBeacon", (0.65, -3.50, 1.45), 0.10, 0.12, "green", mat, 20, rot=(math.radians(90), 0, 0), bevel=0.008)
    cylinder("L3_TankArea_RedAlarmBeacon", (0.38, -3.50, 1.45), 0.10, 0.12, "red", mat, 20, rot=(math.radians(90), 0, 0), bevel=0.008)


def build_scene(mat):
    build_service_platform(mat)
    slurry_center, slurry_radius, _ = build_slurry_tank(mat)
    water_center, water_radius, _ = build_water_tank(mat)
    build_pipe_routing(mat, water_center, water_radius, slurry_center, slurry_radius)
    build_details(mat)


def look_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def setup_preview():
    bpy.ops.object.light_add(type="AREA", location=(0.0, -5.2, 8.0))
    area = put(bpy.context.object)
    area.name = "L3_Preview_Key_AreaLight"
    area.data.energy = 600
    area.data.size = 6.5
    bpy.ops.object.light_add(type="SUN", location=(-4, -3, 8))
    sun = put(bpy.context.object)
    sun.name = "L3_Preview_SoftSun"
    sun.data.energy = 1.5
    bpy.ops.object.camera_add(location=(6.2, -8.7, 5.4))
    cam = put(bpy.context.object)
    cam.name = "L3_Preview_Camera"
    cam.data.lens = 31
    look_at(cam, (0.5, -0.1, 1.9))
    bpy.context.scene.camera = cam

    scene = bpy.context.scene
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720
    scene.render.filepath = PREVIEW_PATH
    try:
        scene.render.engine = "BLENDER_EEVEE"
    except Exception:
        scene.render.engine = "BLENDER_WORKBENCH"
    scene.view_settings.view_transform = "Filmic"
    scene.view_settings.look = "Medium High Contrast"
    scene.view_settings.exposure = 0.0
    scene.view_settings.gamma = 1.0


def select_export_objects():
    bpy.ops.object.select_all(action="DESELECT")
    for obj in ROOT_COLLECTION.objects:
        if obj.type not in {"CAMERA", "LIGHT"}:
            obj.select_set(True)
    bpy.context.view_layer.objects.active = ROOT_EMPTY


def validate_assets():
    mesh_objs = [obj for obj in ROOT_COLLECTION.objects if obj.type == "MESH"]
    no_uv = [obj.name for obj in mesh_objs if not obj.data.uv_layers]
    tri_estimate = 0
    for obj in mesh_objs:
        tri_estimate += sum(max(1, len(poly.vertices) - 2) for poly in obj.data.polygons)
    print(f"VALIDATION mesh_objects={len(mesh_objs)} missing_uv={len(no_uv)} tri_estimate_pre_modifier={tri_estimate}")
    if no_uv:
        print("MISSING_UV", ", ".join(no_uv[:20]))


def main():
    random.seed(11)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    create_root()

    image = create_atlas()
    mat = create_material(image)
    build_scene(mat)
    setup_preview()
    validate_assets()

    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)

    try:
        bpy.ops.render.render(write_still=True)
        print("PREVIEW", PREVIEW_PATH)
    except Exception as exc:
        print("PREVIEW_RENDER_FAILED", exc)

    select_export_objects()
    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH,
        use_selection=True,
        apply_unit_scale=True,
        global_scale=1.0,
        bake_space_transform=False,
        axis_forward="-Z",
        axis_up="Y",
        path_mode="COPY",
        embed_textures=False,
        use_mesh_modifiers=True,
    )
    print("FBX", FBX_PATH)

    try:
        select_export_objects()
        bpy.ops.export_scene.gltf(
            filepath=GLB_PATH,
            export_format="GLB",
            use_selection=True,
            export_apply=True,
        )
        print("GLB", GLB_PATH)
    except Exception as exc:
        print("GLB_EXPORT_FAILED", exc)

    print("BLEND", BLEND_PATH)
    print("ATLAS", ATLAS_PATH)


if __name__ == "__main__":
    main()
