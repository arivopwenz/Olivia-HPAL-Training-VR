import bpy, os, math
from mathutils import Vector

# OLIVIA - PreHeater -> Autoclave pipe (headless).
# Convention (Part 22): author in Unity-world, u2b=(-ux,-uz,uy),
# export FBX_SCALE_ALL + bake_space_transform, instance at IDENTITY in Unity.
def u2b(ux, uy, uz):
    return Vector((-ux, -uz, uy))

HERE = os.path.dirname(os.path.abspath(__file__))
# autoclave UV atlas (match material/texture exactly)
ATLAS = os.path.normpath(os.path.join(HERE, "..", "Level7AutoclaveBlender", "level7_autoclave_uv_atlas.png"))

# --- clean scene ---
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for m in list(bpy.data.materials):
    bpy.data.materials.remove(m)
for img in list(bpy.data.images):
    try:
        bpy.data.images.remove(img)
    except Exception:
        pass

ROOT = bpy.data.objects.new("PreHeaterAutoclave_Pipe", None)
bpy.context.collection.objects.link(ROOT)

# Waypoints = RIGHT-ANGLE polyline mengikuti KOTAK COLLIDER hijau (bukan diagonal).
# Tiap belokan = sudut siku. Rute: outlet preheater -> kiri -> belakang -> kanan
# -> turun -> masuk inlet autoclave.
W = [
    (-4.63, 5.560, 41.89),   # P0 outlet preheater (ujung M2)
    (-8.62, 5.560, 41.89),   # P1 belok (M2 -> M0)
    (-8.62, 5.560, 66.70),   # P2 belok (M0 -> M1)
    ( 1.88, 5.560, 66.70),   # P3 belok turun (M1 -> M4 atas)
    ( 1.88, 2.990, 66.70),   # P4 belok (M4 bawah -> M3)
    ( 1.88, 2.990, 74.80),   # P5 inlet autoclave (ujung M3)
]
RO = 0.50      # outer pipe radius (isi penuh kotak collider ~1.1)
VERTS = 28     # smooth cylinder

pipe_objs = []

def cyl_between(A, B, radius, name):
    bA = u2b(*A); bB = u2b(*B)
    mid = (bA + bB) / 2.0
    d = bB - bA; L = d.length
    if L < 1e-4:
        return None
    bpy.ops.mesh.primitive_cylinder_add(radius=radius, depth=L, location=mid, vertices=VERTS)
    o = bpy.context.active_object; o.name = name
    o.rotation_mode = 'QUATERNION'
    o.rotation_quaternion = d.to_track_quat('Z', 'Y')
    o.parent = ROOT; pipe_objs.append(o)
    return o

def sphere(A, radius, name):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=radius, location=u2b(*A), segments=VERTS, ring_count=int(VERTS/2))
    o = bpy.context.active_object; o.name = name; o.parent = ROOT; pipe_objs.append(o)
    return o

def flange(A, B, radius, name):
    bA = u2b(*A); bB = u2b(*B); d = bB - bA
    bpy.ops.mesh.primitive_cylinder_add(radius=radius, depth=0.16, location=bA, vertices=VERTS)
    o = bpy.context.active_object; o.name = name
    o.rotation_mode = 'QUATERNION'; o.rotation_quaternion = d.to_track_quat('Z', 'Y')
    o.parent = ROOT; pipe_objs.append(o)
    return o

# straight segments
for i in range(len(W) - 1):
    cyl_between(W[i], W[i + 1], RO, "PipeSeg_%d" % i)
# elbow spheres at interior vertices (smooth bends, no breaks)
for i in range(1, len(W) - 1):
    sphere(W[i], RO, "PipeElbow_%d" % i)
# flanges at both ends
flange(W[0], W[1], RO * 1.75, "PipeFlange_Start")
flange(W[-1], W[-2], RO * 1.75, "PipeFlange_End")

# --- join everything into ONE smooth continuous pipe mesh ---
bpy.ops.object.select_all(action='DESELECT')
for o in pipe_objs:
    o.select_set(True)
bpy.context.view_layer.objects.active = pipe_objs[0]
bpy.ops.object.join()
pipe = pipe_objs[0]
pipe.name = "PreHeaterAutoclave_PipeMesh"

# bevel + smooth shading for non-toy look
bpy.context.view_layer.objects.active = pipe
bev = pipe.modifiers.new(name="Bevel", type='BEVEL')
bev.width = 0.015; bev.segments = 2; bev.limit_method = 'ANGLE'; bev.angle_limit = math.radians(35)
bpy.ops.object.modifier_apply(modifier="Bevel")
bpy.ops.object.shade_smooth()

# --- UV unwrap (cube project for consistent texel density) ---
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.cube_project(cube_size=1.6)
bpy.ops.object.mode_set(mode='OBJECT')

# --- material: autoclave UV atlas, metallic steel (match autoclave/preheater) ---
mat = bpy.data.materials.new("M_PreheaterAutoclave_Pipe")
mat.use_nodes = True
nt = mat.node_tree
for n in list(nt.nodes):
    nt.nodes.remove(n)
out = nt.nodes.new("ShaderNodeOutputMaterial")
bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
bsdf.inputs["Metallic"].default_value = 0.9
if "Roughness" in bsdf.inputs:
    bsdf.inputs["Roughness"].default_value = 0.4
nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
if os.path.exists(ATLAS):
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(ATLAS)
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
else:
    bsdf.inputs["Base Color"].default_value = (0.62, 0.64, 0.67, 1.0)
pipe.data.materials.clear()
pipe.data.materials.append(mat)

# --- export FBX (proven: lands exactly at Unity world identity) ---
out_fbx = os.path.join(HERE, "PreheaterAutoclave_Pipe.fbx")
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=out_fbx, use_selection=True, apply_unit_scale=True,
                         apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
                         axis_forward='-Z', axis_up='Y', object_types={'EMPTY', 'MESH'})
print("OLIVIA_PREHEATER_AUTOCLAVE_PIPE_OK ->", out_fbx)
