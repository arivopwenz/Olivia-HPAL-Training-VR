import bpy, os
from mathutils import Vector

# Convention Part 22: author Unity-world, u2b=(-ux,-uz,uy), export FBX_SCALE_ALL+bake, instance IDENTITY.
def u2b(ux, uy, uz):
    return Vector((-ux, -uz, uy))

# clean
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for m in list(bpy.data.materials):
    bpy.data.materials.remove(m)

ROOT = bpy.data.objects.new("SlurryToPreheater_Pipe", None)
bpy.context.collection.objects.link(ROOT)

glass_objs = []; flow_objs = []; steel_objs = []

def cyl_between(A, B, radius, name, bucket):
    bA = u2b(*A); bB = u2b(*B)
    mid = (bA + bB) / 2.0
    d = bB - bA; L = d.length
    if L < 1e-4:
        return None
    bpy.ops.mesh.primitive_cylinder_add(radius=radius, depth=L, location=mid, vertices=22)
    o = bpy.context.active_object; o.name = name
    o.rotation_mode = 'QUATERNION'
    o.rotation_quaternion = d.to_track_quat('Z', 'Y')
    o.parent = ROOT; bucket.append(o)
    return o

def sphere(A, radius, name, bucket):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=radius, location=u2b(*A), segments=18, ring_count=10)
    o = bpy.context.active_object; o.name = name; o.parent = ROOT; bucket.append(o)
    return o

# Route at z=44 (straight, sejalur slurry pump + preheater z44, hindari slurry tank z51-59)
W = [(93.6, 1.6, 44.0), (93.6, 5.6, 44.0), (19.2, 5.6, 44.4), (18.0, 4.6, 44.4)]
RO = 0.34; RI = 0.24
for i in range(len(W) - 1):
    cyl_between(W[i], W[i + 1], RO, "SlurryToPreheater_XRayGlass_Seg_%d" % i, glass_objs)
    cyl_between(W[i], W[i + 1], RI, "SlurryToPreheater_Flow_Seg_%d" % i, flow_objs)
for i in range(1, len(W) - 1):
    sphere(W[i], RO, "SlurryToPreheater_XRayGlass_Elbow_%d" % i, glass_objs)
    sphere(W[i], RI, "SlurryToPreheater_Flow_Elbow_%d" % i, flow_objs)

# flanges (steel) at pump end and preheater end
for k, (A, B) in enumerate([(W[0], W[1]), (W[-1], W[-2])]):
    bA = u2b(*A); bB = u2b(*B); d = bB - bA
    bpy.ops.mesh.primitive_cylinder_add(radius=RO * 1.7, depth=0.22, location=bA, vertices=22)
    o = bpy.context.active_object; o.name = "SlurryToPreheater_Steel_Flange_%d" % k
    o.rotation_mode = 'QUATERNION'; o.rotation_quaternion = d.to_track_quat('Z', 'Y')
    o.parent = ROOT; steel_objs.append(o)

# support columns down to ground along the long run
for k, x in enumerate([82, 68, 54, 40, 27]):
    cyl_between((x, 5.6, 44.4), (x, 0.0, 44.4), 0.13, "SlurryToPreheater_Steel_Support_%d" % k, steel_objs)
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=u2b(x, -0.1, 44.4))
    o = bpy.context.active_object; o.name = "SlurryToPreheater_Steel_Foot_%d" % k
    o.scale = (0.5, 0.1, 0.5); o.parent = ROOT; steel_objs.append(o)

# join inner flow into single mesh for flow animator
def join(objs, name):
    if not objs:
        return None
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    objs[0].name = name
    return objs[0]

flow_joined = join(flow_objs, "SlurryToPreheater_SlurryFlow")

def mat(name, rgba):
    m = bpy.data.materials.new(name); m.use_nodes = False; m.diffuse_color = rgba
    return m
mg = mat("SlurryToPreheater_XRayGlass", (0.6, 0.7, 0.82, 0.35))
mf = mat("SlurryToPreheater_SlurryFlow", (0.34, 0.27, 0.19, 1.0))
ms = mat("SlurryToPreheater_Steel", (0.6, 0.62, 0.65, 1.0))
for o in glass_objs:
    o.data.materials.clear(); o.data.materials.append(mg)
if flow_joined:
    flow_joined.data.materials.clear(); flow_joined.data.materials.append(mf)
for o in steel_objs:
    o.data.materials.clear(); o.data.materials.append(ms)
for o in glass_objs + steel_objs + ([flow_joined] if flow_joined else []):
    bpy.context.view_layer.objects.active = o; bpy.ops.object.shade_smooth()

out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "SlurryToPreheater_Pipe.fbx")
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, apply_unit_scale=True,
                         apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
                         axis_forward='-Z', axis_up='Y', object_types={'EMPTY', 'MESH'})
print("OLIVIA_SLURRYPIPE_EXPORT_OK")
