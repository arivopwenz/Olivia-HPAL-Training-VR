"""
OLIVIA VR - Blender headless pipe builder: CCD -> (MHP / Tailing Filter Press).

Builds two industrial process pipe runs and exports as FBX for Unity:
  1. PLS_Overflow_Pipe    : CCD clarified overflow (liquid PLS) -> Level 10 Pemurnian/MHP inlet.
  2. Underflow_Slurry_Pipe : CCD thickened underflow (solids slurry) -> Tailing Filter Press.

Input waypoints are given in UNITY world coords (x, y=up, z). We convert to Blender Z-up
via U2B(ux,uy,uz) = (ux, uz, uy) and build everything natively in Blender. On FBX export with
the standard Unity preset (axis_up=Y, axis_forward=-Z, bake_space_transform), the geometry maps
back to the original Unity world coords, so the imported FBX sits correctly at identity (0,0,0).

Each run produces: outer steel pipe, flange rings, support legs, and an inner "flow" tube
(distinct material) that the Unity controller scrolls/pulses to fake flowing PLS/slurry.

Run:  blender --background --python _build_ccd_pipes.py
"""

import bpy
import os
from mathutils import Vector

# --------------------------------------------------------------------------------------
# CONFIG — routing waypoints in UNITY world space (meters), captured from the live scene.
# --------------------------------------------------------------------------------------

# PLS overflow: CCD overflow header -> MHP neutralization inlet (elevated pipe rack).
PLS_PATH_UNITY = [
    (19.0, 6.7, 108.0),
    (24.0, 6.9, 108.0),
    (40.0, 6.9, 107.5),
    (60.0, 6.9, 107.0),
    (67.0, 6.9, 106.9),
    (67.5, 4.5, 106.8),
    (67.5, 2.2, 106.7),
]
PLS_RADIUS = 0.42

# Underflow slurry: CCD underflow pump station -> tailing filter press.
UNDER_PATH_UNITY = [
    (-15.0, 1.4, 122.0),
    (-12.0, 2.2, 124.0),
    (-2.0, 2.4, 130.0),
    (8.0, 2.6, 138.0),
    (16.0, 2.6, 144.0),
    (21.0, 2.6, 146.5),
]
UNDER_RADIUS = 0.34

FLANGE_EVERY = 6.0
SUPPORT_EVERY = 7.0
GROUND_Z = 0.0  # Blender Z (=Unity Y) ground level

EXPORT_PATH = bpy.path.abspath("//Art/CCDProcessPipesBlender/CCD_Process_Pipes.fbx")


def U2B(p):
    """Unity (x, y_up, z) -> Blender (x, z, y_up=z_blender)."""
    return Vector((p[0], p[2], p[1]))


# --------------------------------------------------------------------------------------

def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for coll in (bpy.data.meshes, bpy.data.curves, bpy.data.materials):
        for block in list(coll):
            coll.remove(block)


def make_material(name, rgba, metallic=0.0, rough=0.6, emission=None):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = rgba
        if "Metallic" in bsdf.inputs:
            bsdf.inputs["Metallic"].default_value = metallic
        if "Roughness" in bsdf.inputs:
            bsdf.inputs["Roughness"].default_value = rough
        if emission is not None:
            if "Emission Color" in bsdf.inputs:
                bsdf.inputs["Emission Color"].default_value = emission
            if "Emission Strength" in bsdf.inputs:
                bsdf.inputs["Emission Strength"].default_value = 1.2
    return mat


def pipe_from_path(name, blender_points, radius, mat, resolution=16):
    curve_data = bpy.data.curves.new(name + "_Curve", type='CURVE')
    curve_data.dimensions = '3D'
    curve_data.resolution_u = 4
    curve_data.bevel_depth = radius
    curve_data.bevel_resolution = max(2, resolution // 2)
    curve_data.use_fill_caps = True

    spline = curve_data.splines.new('POLY')
    spline.points.add(len(blender_points) - 1)
    for i, p in enumerate(blender_points):
        spline.points[i].co = (p.x, p.y, p.z, 1.0)

    obj = bpy.data.objects.new(name, curve_data)
    bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.convert(target='MESH')
    obj = bpy.context.view_layer.objects.active
    obj.data.materials.append(mat)
    for poly in obj.data.polygons:
        poly.use_smooth = True
    obj.select_set(False)
    return obj


def resample(blender_points, step):
    out = []
    pts = blender_points
    acc = 0.0
    next_at = 0.0
    for i in range(len(pts) - 1):
        a, b = pts[i], pts[i + 1]
        seg = b - a
        seg_len = seg.length
        if seg_len < 1e-5:
            continue
        d = seg / seg_len
        while next_at <= acc + seg_len:
            t = next_at - acc
            out.append((a + d * t, d.copy()))
            next_at += step
        acc += seg_len
    out.append((pts[-1], (pts[-1] - pts[-2]).normalized()))
    return out


def add_flange(name, center, direction, radius, mat):
    bpy.ops.mesh.primitive_cylinder_add(radius=radius * 1.35, depth=0.18, location=center)
    obj = bpy.context.view_layer.objects.active
    obj.name = name
    obj.rotation_mode = 'QUATERNION'
    if direction.length > 1e-5:
        obj.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(direction.normalized())
    obj.data.materials.append(mat)
    for poly in obj.data.polygons:
        poly.use_smooth = True
    return obj


def add_support(name, point, radius, mat):
    # point in Blender space, up = Z
    top_z = point.z - radius
    leg_h = max(0.2, top_z - GROUND_Z)
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(point.x, point.y, GROUND_Z + leg_h * 0.5))
    leg = bpy.context.view_layer.objects.active
    leg.name = name
    leg.scale = (0.18, 0.18, leg_h * 0.5)
    leg.data.materials.append(mat)
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(point.x, point.y, top_z))
    sad = bpy.context.view_layer.objects.active
    sad.name = name + "_Saddle"
    sad.scale = (radius * 1.4, radius * 1.4, 0.12)
    sad.data.materials.append(mat)
    return leg


def build_run(prefix, unity_path, radius, steel_mat, flow_mat, parent):
    bpts = [U2B(p) for p in unity_path]
    pipe = pipe_from_path(prefix + "_Pipe", bpts, radius, steel_mat)
    pipe.parent = parent
    flow = pipe_from_path(prefix + "_Flow", bpts, radius * 0.72, flow_mat)
    flow.parent = parent
    for i, (pt, d) in enumerate(resample(bpts, FLANGE_EVERY)):
        add_flange(f"{prefix}_Flange_{i:02d}", pt, d, radius, steel_mat).parent = parent
    for i, (pt, d) in enumerate(resample(bpts, SUPPORT_EVERY)):
        if pt.z - GROUND_Z < 0.6:
            continue
        add_support(f"{prefix}_Support_{i:02d}", pt, radius, steel_mat).parent = parent
    return pipe, flow


def main():
    clear_scene()

    steel = make_material("UV_PipeSteel_Grey", (0.55, 0.57, 0.60, 1.0), metallic=0.85, rough=0.35)
    pls_flow = make_material("UV_PLS_FlowGreen", (0.42, 0.62, 0.30, 1.0), rough=0.25,
                             emission=(0.30, 0.55, 0.22, 1.0))
    slurry_flow = make_material("UV_Underflow_SlurryBrown", (0.34, 0.22, 0.15, 1.0), rough=0.5,
                                emission=(0.18, 0.10, 0.06, 1.0))

    root_pls = bpy.data.objects.new("PLS_Overflow_Pipe", None)
    bpy.context.collection.objects.link(root_pls)
    root_under = bpy.data.objects.new("Underflow_Slurry_Pipe", None)
    bpy.context.collection.objects.link(root_under)

    build_run("PLS", PLS_PATH_UNITY, PLS_RADIUS, steel, pls_flow, root_pls)
    build_run("Underflow", UNDER_PATH_UNITY, UNDER_RADIUS, steel, slurry_flow, root_under)

    os.makedirs(os.path.dirname(EXPORT_PATH), exist_ok=True)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(
        filepath=EXPORT_PATH,
        use_selection=True,
        apply_unit_scale=True,
        global_scale=1.0,
        apply_scale_options='FBX_SCALE_ALL',
        object_types={'MESH', 'EMPTY'},
        use_mesh_modifiers=True,
        mesh_smooth_type='FACE',
        bake_space_transform=True,
        axis_forward='-Z',
        axis_up='Y',
    )
    print("OLIVIA_PIPE_EXPORT_OK:", EXPORT_PATH)


if __name__ == "__main__":
    main()
