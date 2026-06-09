import bpy, bmesh, math, os
from mathutils import Vector

# ============================================================
# OLIVIA HPAL - Autoclave Liquid Volume (Level 7)
# Silinder cairan horizontal subdivided (untuk swirl/displacement shader).
# Dibangun di ORIGIN, axis silinder sepanjang X (horizontal), heavily subdivided
# pada permukaan supaya shader bisa bikin gelombang/vortex vertex-level.
# Diorientasi & ditempatkan presisi di Unity (center autoclave -13,8,83.7).
# ============================================================

def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete()
    for blk in (bpy.data.meshes, bpy.data.materials):
        for b in list(blk):
            blk.remove(b)

clear_scene()

# Dimensi (radius/length dalam meter, axis X). Sedikit lebih kecil dari interior shell.
RADIUS = 4.15
LENGTH = 30.0
RADIAL_SEG = 64        # halus melingkar
LENGTH_SEG = 96        # banyak loop sepanjang sumbu -> displacement gelombang mulus

# Buat cylinder default (axis Z), lalu putar 90 di Y supaya axis jadi X.
bpy.ops.mesh.primitive_cylinder_add(
    vertices=RADIAL_SEG, radius=RADIUS, depth=LENGTH,
    end_fill_type='TRIFAN', location=(0, 0, 0))
liq = bpy.context.active_object
liq.name = "L7_AutoclaveLiquid_Volume"

# Rotasi axis Z -> X
liq.rotation_euler = (0.0, math.radians(90.0), 0.0)
bpy.ops.object.transform_apply(rotation=True)

# Subdivide sepanjang sumbu: pakai loopcuts via bmesh untuk densitas gelombang.
me = liq.data
bm = bmesh.new()
bm.from_mesh(me)
# subdiv merata supaya cukup vertex untuk displacement halus
bmesh.ops.subdivide_edges(bm, edges=bm.edges, cuts=2, use_grid_fill=True)
bm.to_mesh(me)
bm.free()

# Shade smooth
bpy.ops.object.shade_smooth()

# UV: cylinder project (untuk scroll tekstur foam/ripple di shader)
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.cylinder_project(direction='ALIGN_TO_OBJECT')
bpy.ops.object.mode_set(mode='OBJECT')

# Material placeholder (shader Unity yang dipakai; ini hanya supaya FBX punya slot)
mat = bpy.data.materials.new("M_AutoclaveLiquid")
mat.use_nodes = True
liq.data.materials.append(mat)

# Origin ke center
bpy.context.view_layer.objects.active = liq
bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY', center='BOUNDS')
liq.location = (0, 0, 0)

# Export FBX
out_dir = os.path.dirname(bpy.data.filepath) if bpy.data.filepath else os.path.dirname(os.path.abspath(__file__))
out_path = os.path.join(out_dir, "AutoclaveLiquid.fbx")
bpy.ops.object.select_all(action='DESELECT')
liq.select_set(True)
bpy.context.view_layer.objects.active = liq
bpy.ops.export_scene.fbx(
    filepath=out_path,
    use_selection=True,
    apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_ALL',
    bake_space_transform=True,
    object_types={'MESH'},
    axis_forward='-Z',
    axis_up='Y',
    mesh_smooth_type='FACE')

print("OLIVIA_AUTOCLAVE_LIQUID_OK verts=%d tris=%d -> %s" % (
    len(liq.data.vertices), len(liq.data.polygons), out_path))
