"""
OLIVIA VR - build_ccd_connection_pipes.py
Headless Blender script: membangun 2 pipa industrial penghubung dari CCD:
  1. PLS_OverflowPipe  : CCD overflow (cairan PLS) -> area Pemurnian (Level 10 inlet)
  2. Tailing_UnderflowPipe : CCD underflow (padatan/tailing) -> mesin Filter Press

Koordinat dalam WORLD UNITY (meter). Blender Z-up; kita ekspor FBX dgn
-Z forward, Y up supaya orientasi konsisten dgn import Unity default.

Setiap pipa terdiri dari:
  - Outer pipe (steel) sepanjang jalur (poly-line dgn beberapa segmen + elbow)
  - Inner flow mesh (sedikit lebih kecil, material flow) untuk animasi scroll di Unity
  - Flange di tiap ujung
  - Pipe support / saddle berkala

Jalankan:
  blender --background --python build_ccd_connection_pipes.py
Output: Assets/Art/CCDConnectionPipes/CCD_ConnectionPipes.fbx
"""
import bpy
import bmesh
import math
import os
from mathutils import Vector

# ----------------------------------------------------------------------------
# Util
# ----------------------------------------------------------------------------

def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.objects):
        for item in list(block):
            try:
                block.remove(item)
            except Exception:
                pass


def make_material(name, rgba, metallic=0.0, rough=0.6, emis=None):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = rgba
        if "Metallic" in bsdf.inputs:
            bsdf.inputs["Metallic"].default_value = metallic
        if "Roughness" in bsdf.inputs:
            bsdf.inputs["Roughness"].default_value = rough
        if emis is not None:
            # Blender 4.x: Emission Color + Emission Strength
            if "Emission Color" in bsdf.inputs:
                bsdf.inputs["Emission Color"].default_value = emis
                bsdf.inputs["Emission Strength"].default_value = 1.0
            elif "Emission" in bsdf.inputs:
                bsdf.inputs["Emission"].default_value = emis
    return mat


# Unity world (x,y,z) -> Blender (x, -z, y) ... but we will keep authoring in a
# local space and just place objects, exporting with Unity-friendly axes.
# To keep it simple and predictable, we author DIRECTLY in Unity coordinates and
# rely on FBX export axis settings (primary -Z forward, secondary Y up) plus we
# manually convert: Blender_pos = (ux, uz, uy) so that after Unity import
# (which flips), positions match. We instead place each piece in Blender using
# unity coords mapped as (x, z, y) and export with apply_unit_scale.

def u2b(ux, uy, uz):
    """Unity (x,y,z) -> Blender (x, z, y)."""
    return Vector((ux, uz, uy))


def add_cylinder_between(p0, p1, radius, mat, name, segments=20):
    """Buat cylinder dari titik p0 ke p1 (keduanya Blender-space Vector)."""
    mid = (p0 + p1) * 0.5
    direction = (p1 - p0)
    length = direction.length
    if length < 1e-5:
        return None
    bpy.ops.mesh.primitive_cylinder_add(vertices=segments, radius=radius, depth=length, location=mid)
    obj = bpy.context.active_object
    obj.name = name
    # align +Z (default cylinder axis) to direction
    z = Vector((0, 0, 1))
    d = direction.normalized()
    axis = z.cross(d)
    if axis.length < 1e-6:
        if d.z < 0:
            obj.rotation_euler = (math.pi, 0, 0)
    else:
        angle = math.acos(max(-1.0, min(1.0, z.dot(d))))
        axis.normalize()
        obj.rotation_mode = 'AXIS_ANGLE'
        obj.rotation_axis_angle = (angle, axis.x, axis.y, axis.z)
    if mat:
        obj.data.materials.append(mat)
    return obj


def add_torus_flange(center, radius, mat, name, normal):
    bpy.ops.mesh.primitive_torus_add(location=center, major_radius=radius, minor_radius=radius * 0.28)
    obj = bpy.context.active_object
    obj.name = name
    # torus default lies in XY plane (axis +Z). align to normal
    z = Vector((0, 0, 1))
    d = normal.normalized()
    axis = z.cross(d)
    if axis.length > 1e-6:
        angle = math.acos(max(-1.0, min(1.0, z.dot(d))))
        axis.normalize()
        obj.rotation_mode = 'AXIS_ANGLE'
        obj.rotation_axis_angle = (angle, axis.x, axis.y, axis.z)
    if mat:
        obj.data.materials.append(mat)
    return obj


def add_support(base_pos, height, mat, name):
    """Tiang penyangga pipa (vertikal)."""
    loc = Vector((base_pos.x, base_pos.y, base_pos.z - height * 0.5))
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = (0.18, 0.18, height * 0.5)
    if mat:
        obj.data.materials.append(mat)
    return obj


def build_pipe_run(name, unity_points, outer_r, steel_mat, flow_mat, flow_rgba,
                   support_every=6.0, support_ground_y=0.1):
    """Bangun satu jalur pipa dari list titik (Unity coords)."""
    pts = [u2b(*p) for p in unity_points]
    created = []
    flow_objs = []
    for i in range(len(pts) - 1):
        seg = add_cylinder_between(pts[i], pts[i + 1], outer_r, steel_mat,
                                   f"{name}_Steel_{i}")
        if seg:
            created.append(seg)
        # inner flow cylinder (sedikit lebih kecil)
        flow = add_cylinder_between(pts[i], pts[i + 1], outer_r * 0.78, flow_mat,
                                    f"{name}_Flow_{i}")
        if flow:
            flow_objs.append(flow)
        # elbow sphere di sambungan
        if 0 < i < len(pts) - 1:
            bpy.ops.mesh.primitive_uv_sphere_add(radius=outer_r * 1.05, location=pts[i])
            elb = bpy.context.active_object
            elb.name = f"{name}_Elbow_{i}"
            elb.data.materials.append(steel_mat)
            created.append(elb)
    # flanges di ujung
    for idx, end in enumerate([0, len(pts) - 1]):
        nb = (pts[1] - pts[0]) if end == 0 else (pts[-1] - pts[-2])
        fl = add_torus_flange(pts[end], outer_r * 1.35, steel_mat,
                              f"{name}_Flange_{idx}", nb)
        created.append(fl)
    # supports sepanjang jalur
    total = 0.0
    si = 0
    for i in range(len(pts) - 1):
        a, b = pts[i], pts[i + 1]
        seglen = (b - a).length
        steps = max(1, int(seglen / support_every))
        for s in range(steps):
            t = (s + 0.5) / steps
            p = a.lerp(b, t)
            h = max(0.3, p.z - support_ground_y)
            sup = add_support(p, h, steel_mat, f"{name}_Support_{si}")
            created.append(sup)
            si += 1
    return created, flow_objs


# ----------------------------------------------------------------------------
# Main
# ----------------------------------------------------------------------------

def main():
    clear_scene()

    steel = make_material("CCDPipe_Steel", (0.62, 0.64, 0.68, 1.0), metallic=0.9, rough=0.35)
    pls_flow = make_material("CCDPipe_PLS_Flow", (0.30, 0.62, 0.55, 1.0), rough=0.25,
                             emis=(0.10, 0.35, 0.30, 1.0))   # PLS hijau-kebiruan
    tailing_flow = make_material("CCDPipe_Tailing_Flow", (0.40, 0.30, 0.22, 1.0), rough=0.5,
                                 emis=(0.10, 0.07, 0.04, 1.0))  # lumpur coklat

    # --- PLS overflow: CCD overflow header -> Level 10 (Pemurnian) inlet ---
    # Naik dari launder CCD (y~7) menyeberang ke timur (x naik) lalu turun ke
    # inlet flange Neutralization (67.5, 1.75, 106.7).
    pls_points = [
        (21.6, 7.0, 107.2),   # CCD1 overflow header keluar
        (30.0, 7.2, 107.0),   # rise + run timur
        (45.0, 6.8, 107.0),
        (60.0, 5.0, 106.8),
        (66.0, 2.6, 106.7),   # turun ke inlet
        (67.5, 1.9, 106.7),   # inlet flange L10
    ]
    build_pipe_run("PLS_OverflowPipe", pls_points, outer_r=0.32,
                   steel_mat=steel, flow_mat=pls_flow, flow_rgba=(0.3, 0.62, 0.55, 1),
                   support_every=6.0)

    # --- Tailing underflow: CCD underflow pump station -> Filter Press ---
    # Dari pump station (-16.8,1.1,122) menuju filter press (-15.2,1.5,145.5).
    tailing_points = [
        (-16.0, 1.2, 122.5),  # keluar pump station
        (-16.0, 1.4, 130.0),
        (-15.6, 1.5, 138.0),
        (-15.2, 1.6, 144.0),  # masuk filter press feed
    ]
    build_pipe_run("Tailing_UnderflowPipe", tailing_points, outer_r=0.28,
                   steel_mat=steel, flow_mat=tailing_flow, flow_rgba=(0.4, 0.3, 0.22, 1),
                   support_every=5.0)

    # Select all and export
    bpy.ops.object.select_all(action='SELECT')

    out_dir = os.path.join(os.path.dirname(bpy.data.filepath) if bpy.data.filepath else
                           os.getcwd())
    # We pass explicit absolute path via argv instead.
    export_path = OUTPUT_FBX
    os.makedirs(os.path.dirname(export_path), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=export_path,
        use_selection=True,
        apply_unit_scale=True,
        global_scale=1.0,
        axis_forward='-Z',
        axis_up='Y',
        object_types={'MESH'},
        mesh_smooth_type='FACE',
    )
    print("[BLENDER] Exported:", export_path)


OUTPUT_FBX = os.environ.get(
    "OLIVIA_PIPE_FBX",
    r"C:/Users/mp2dz/Olivia/Assets/Art/CCDConnectionPipes/CCD_ConnectionPipes.fbx")

if __name__ == "__main__":
    main()
