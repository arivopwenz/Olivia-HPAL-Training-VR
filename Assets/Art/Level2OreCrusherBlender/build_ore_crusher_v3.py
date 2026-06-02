import bpy, bmesh, math, random, os
from mathutils import Vector

random.seed(2026)
A = r"C:\Users\mp2dz\Olivia\Assets"
TEX = os.path.join(A, "Art", "FlashCCDIndustrialUVRedesign", "Textures")
OUT = os.path.join(A, "Art", "Level2OreCrusherBlender", "Level2_OreCrusher_IndustrialUV_v3.fbx")

# ---- Unity-world -> Blender (Part 22 convention) ----
def u2b(ux, uy, uz):
    return Vector((-ux, -uz, uy))
def dsz(sx, sy, sz):
    return (sx, sz, sy)

# ---- clean scene ----
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete()
for blk in (bpy.data.meshes, bpy.data.materials, bpy.data.images):
    for b in list(blk):
        try: blk.remove(b)
        except Exception: pass

# ---- materials (image-textured) ----
_mats = {}
def mat(name, tex, rough=0.72, metal=0.0, emis=None, ecol=(1,1,1)):
    if name in _mats: return _mats[name]
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    bsdf = nt.nodes.new('ShaderNodeBsdfPrincipled')
    bsdf.inputs['Roughness'].default_value = rough
    bsdf.inputs['Metallic'].default_value = metal
    path = os.path.join(TEX, tex)
    if os.path.exists(path):
        img = bpy.data.images.load(path, check_existing=True)
        tn = nt.nodes.new('ShaderNodeTexImage'); tn.image = img
        nt.links.new(tn.outputs['Color'], bsdf.inputs['Base Color'])
    if emis is not None:
        try:
            bsdf.inputs['Emission Color'].default_value = (ecol[0],ecol[1],ecol[2],1)
            bsdf.inputs['Emission Strength'].default_value = emis
        except Exception: pass
    nt.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    _mats[name] = m
    return m

STEEL = lambda: mat("M_L2_BrushedSteel", "UV_BrushedSteel_Grey.png", 0.55, 0.85)
HULL  = lambda: mat("M_L2_CrusherHull", "UV_AcidTank_316L_OffWhite.png", 0.5, 0.7)
RUBBER= lambda: mat("M_L2_BeltRubber", "UV_DarkRubber_Gasket.png", 0.82, 0.0)
HAZ   = lambda: mat("M_L2_Hazard", "UV_Hazard_BlackYellow.png", 0.6, 0.2)
YEL   = lambda: mat("M_L2_SafetyYellow", "UV_SafetyYellow_Rails.png", 0.6, 0.2)
CONC  = lambda: mat("M_L2_Concrete", "UV_AcidResistantConcrete.png", 0.92, 0.0)
ORE   = lambda: mat("M_L2_OreRock", "UV_ThickUnderflow_BrownPurple.png", 0.95, 0.0)
BLUE  = lambda: mat("M_L2_ProcessBlue", "UV_ChemicalPump_Blue.png", 0.5, 0.4)
RED   = lambda: mat("M_L2_EmergencyRed", "UV_EmergencyRed.png", 0.5, 0.2, emis=2.0, ecol=(0.9,0.05,0.05))
BLACK = lambda: mat("M_L2_BlackSkid", "UV_DarkRubber_Gasket.png", 0.7, 0.3)

# ---- finalize: bevel + cube UV + auto smooth ----
def finalize(o, bevel=0.025, seg=2, uv=2.0, smooth=40.0):
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    try:
        dim = min(o.dimensions.x, o.dimensions.y, o.dimensions.z)
        bw = min(bevel, max(0.004, dim * 0.32))
        md = o.modifiers.new('bev', 'BEVEL'); md.width = bw; md.segments = seg
        md.limit_method = 'ANGLE'; md.angle_limit = math.radians(35); md.harden_normals = True
        bpy.ops.object.modifier_apply(modifier=md.name)
    except Exception: pass
    try:
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.cube_project(cube_size=uv)
        bpy.ops.object.mode_set(mode='OBJECT')
    except Exception:
        try: bpy.ops.object.mode_set(mode='OBJECT')
        except Exception: pass
    try:
        bpy.ops.object.shade_auto_smooth(angle=math.radians(smooth))
    except Exception:
        try: bpy.ops.object.shade_smooth()
        except Exception: pass
    o.select_set(False)

def setmat(o, m):
    o.data.materials.clear(); o.data.materials.append(m)

# ---- primitive builders (author in Unity coords) ----
def box(name, upos, usize, m, bevel=0.025, seg=2, uv=2.0):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=u2b(*upos))
    o = bpy.context.active_object; o.name = name
    sx, sy, sz = dsz(*usize)
    o.scale = (sx, sy, sz)
    bpy.ops.object.transform_apply(scale=True)
    setmat(o, m); finalize(o, bevel, seg, uv)
    return o

def cyl(name, upos, radius, length, axis, m, bevel=0.02, uv=1.5, verts=24):
    # axis: 'x','y','z' in Unity space
    bpy.ops.mesh.primitive_cylinder_add(radius=radius, depth=length, vertices=verts, location=u2b(*upos))
    o = bpy.context.active_object; o.name = name
    # cylinder default along Blender Z (=Unity Y). rotate to desired Unity axis.
    if axis == 'x':   o.rotation_euler = (0, math.radians(90), 0)   # blender: along X
    elif axis == 'z': o.rotation_euler = (math.radians(90), 0, 0)   # unity Z -> blender Y
    # axis 'y' -> default (blender Z = unity Y)
    bpy.ops.object.transform_apply(rotation=True)
    setmat(o, m); finalize(o, bevel, 1, uv, 50)
    return o

def aligned_box(name, ua, ub, width, thick, m, bevel=0.02, uv=2.0):
    # tilted slab spanning Unity ua->ub; width across, thick = surface thickness.
    # built from explicit corners (Unity) -> u2b, so placement is exact (no rotate bug).
    A = Vector((ua[0], ua[1], ua[2])); B = Vector((ub[0], ub[1], ub[2]))
    L = (B - A); length = max(0.01, L.length); L = L.normalized()
    S = Vector((0, 1, 0)).cross(L)
    if S.length < 1e-4: S = Vector((0, 0, 1))
    S = S.normalized()
    N = L.cross(S).normalized()
    C = (A + B) * 0.5
    hl, hw, ht = length * 0.5, width * 0.5, thick * 0.5
    verts = []
    for sl in (-1, 1):
        for ss in (-1, 1):
            for sn in (-1, 1):
                p = C + L * (hl * sl) + S * (hw * ss) + N * (ht * sn)
                verts.append(list(u2b(p.x, p.y, p.z)))
    faces = [(0,1,3,2),(4,5,7,6),(0,1,5,4),(2,3,7,6),(0,2,6,4),(1,3,7,5)]
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces); me.update()
    o = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(o)
    bpy.context.view_layer.objects.active = o; o.select_set(True)
    try:
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.mesh.normals_make_consistent(inside=False)
        bpy.ops.object.mode_set(mode='OBJECT')
    except Exception:
        try: bpy.ops.object.mode_set(mode='OBJECT')
        except Exception: pass
    o.select_set(False)
    setmat(o, m); finalize(o, bevel, 2, uv)
    return o

def rock(name, upos, size, m, crushed=True):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=(1 if crushed else 2), radius=size, location=u2b(*upos))
    o = bpy.context.active_object; o.name = name
    me = o.data; bm = bmesh.new(); bm.from_mesh(me)
    amp = 0.5 if crushed else 0.28
    for v in bm.verts:
        j = Vector((random.uniform(-amp, amp), random.uniform(-amp, amp), random.uniform(-amp, amp))) * size
        v.co += j
    bm.to_mesh(me); bm.free()
    o.scale = (random.uniform(0.85,1.2), random.uniform(0.6,0.95), random.uniform(0.85,1.2))
    o.rotation_euler = (random.uniform(0,6.28), random.uniform(0,6.28), random.uniform(0,6.28))
    bpy.ops.object.transform_apply(scale=True, rotation=True)
    setmat(o, m)
    if not crushed:
        try:
            sub = o.modifiers.new('s','SUBSURF'); sub.levels=1; sub.render_levels=1
            bpy.ops.object.modifier_apply(modifier=sub.name)
        except Exception: pass
    try: bpy.ops.object.shade_smooth()
    except Exception: pass
    return o

# ================= BELT (inclined) =================
TAIL = (134.9, 5.10, 56.9)   # crusher/feed end (low)
HEAD = (102.9, 8.20, 56.9)   # discharge end (high)
def belt_pt(t):
    return (TAIL[0]+(HEAD[0]-TAIL[0])*t, TAIL[1]+(HEAD[1]-TAIL[1])*t, TAIL[2]+(HEAD[2]-TAIL[2])*t)
WIDTH = 4.4

# belt surface (controller scrolls this; keep exact name)
aligned_box("L2_V2_Wide_Inclined_Rubber_Ore_Belt", TAIL, HEAD, WIDTH, 0.16, RUBBER(), bevel=0.04, uv=3.0)

# trough rollers (15) across width
for i in range(15):
    t = i/14.0
    p = belt_pt(t); p = (p[0], p[1]-0.13, p[2])
    cyl("L2_V2_Smooth_Trough_Roller_%02d" % i, p, 0.16, WIDTH+0.2, 'z', STEEL(), uv=1.2, verts=20)
# head + tail pulley drums (bigger)
cyl("L2_V2_Smooth_Head_Pulley", (HEAD[0], HEAD[1]-0.05, HEAD[2]), 0.34, WIDTH+0.4, 'z', STEEL(), uv=1.2, verts=28)
cyl("L2_V2_Smooth_Tail_Pulley", (TAIL[0], TAIL[1]-0.05, TAIL[2]), 0.34, WIDTH+0.4, 'z', STEEL(), uv=1.2, verts=28)

# hazard side skirts (tall) + deep trusses + supports/feet
for side, z, zt in (("Left", 54.5, 54.0), ("Right", 59.3, 59.8)):
    a = (TAIL[0], TAIL[1]+0.45, z); b = (HEAD[0], HEAD[1]+0.45, z)
    aligned_box("L2_V2_%s_Tall_Yellow_Side_Skirt" % side, a, b, 0.12, 1.0, HAZ(), uv=2.2)
    at = (TAIL[0], TAIL[1]-0.55, zt); bt = (HEAD[0], HEAD[1]-0.55, zt)
    aligned_box("L2_V2_%s_Deep_Main_Truss" % side, at, bt, 0.18, 0.85, STEEL(), uv=2.5)
    # diagonals
    for i in range(13):
        t = (i+0.5)/14.0; p = belt_pt(t)
        box("L2_V2_%s_Truss_Diagonal_%02d" % (side, i), (p[0], p[1]-0.55, zt), (1.9, 0.12, 0.12), STEEL(), bevel=0.02, uv=1.5)
    # support columns + feet (avoid 'conveyor' in name)
    for i in range(0, 15, 2):
        t = i/14.0; p = belt_pt(t)
        gy = -0.6
        box("L2_V2_%s_Jumbo_Belt_Support_%02d" % (side, i), (p[0], (p[1]-0.85+gy)/2.0+0.0, zt), (0.22, max(0.4,(p[1]-0.85)-gy), 0.22), STEEL(), bevel=0.02, uv=1.5)
        box("L2_V2_%s_Jumbo_Foot_%02d" % (side, i), (p[0], gy, zt), (0.7, 0.18, 0.7), STEEL(), bevel=0.03, uv=1.5)

# maintenance catwalk (one side) + rails (avoid 'conveyor')
ca = (TAIL[0], TAIL[1]-0.1, 52.9); cb = (HEAD[0], HEAD[1]-0.1, 52.9)
aligned_box("L2_V2_Wide_Maint_Catwalk", ca, cb, 1.4, 0.08, STEEL(), uv=3.0)
for i in range(7):
    t = i/6.0; p = belt_pt(t)
    box("L2_V2_Maint_Catwalk_Rail_post_%02d" % i, (p[0], p[1]+0.45, 52.1), (0.06, 1.0, 0.06), YEL(), bevel=0.015, uv=1.2)
ra = (TAIL[0], TAIL[1]+0.95, 52.1); rb = (HEAD[0], HEAD[1]+0.95, 52.1)
aligned_box("L2_V2_Maint_Catwalk_Rail_toprail", ra, rb, 0.06, 0.06, YEL(), bevel=0.015, uv=2.0)

# ================= CRUSHER + HOPPER =================
box("L2_V2_Jumbo_Concrete_Service_Pad", (146.0, -0.55, 55.0), (12.5, 0.5, 11.0), CONC(), bevel=0.05, uv=4.0)
box("L2_V2_Jumbo_Black_Skid_Base", (145.0, 0.1, 56.0), (8.0, 0.6, 7.0), BLACK(), bevel=0.04, uv=2.5)
box("L2_V2_Rounded_Primary_Crusher_Body", (145.0, 3.2, 56.2), (6.4, 5.2, 6.0), HULL(), bevel=0.18, seg=3, uv=2.5)
box("L2_V2_Left_Smooth_Jaw_Liner", (141.6, 3.3, 54.7), (0.6, 3.4, 2.6), STEEL(), bevel=0.05, uv=1.5)
box("L2_V2_Right_Smooth_Jaw_Liner", (141.6, 3.3, 57.7), (0.6, 3.4, 2.6), STEEL(), bevel=0.05, uv=1.5)
box("L2_V2_Dark_Jaw_Mouth_Recess", (141.0, 3.5, 56.2), (0.5, 2.4, 2.4), BLACK(), bevel=0.04, uv=1.2)
# flared hopper above crusher (inverted pyramid-ish via scaled box) + throat
box("L2_V2_SuperJumbo_Flared_Ore_Hopper", (145.0, 8.2, 56.2), (7.6, 3.4, 7.2), STEEL(), bevel=0.06, uv=3.0)
box("L2_V2_Inner_Black_Hopper_Throat", (145.0, 6.2, 56.2), (3.0, 1.6, 3.0), BLACK(), bevel=0.04, uv=1.5)
# flywheels + hubs + motor + guard
cyl("L2_V2_Jumbo_Flywheel_A_Smooth", (150.4, 2.8, 53.0), 1.5, 0.5, 'z', STEEL(), uv=1.0, verts=32)
cyl("L2_V2_Jumbo_Flywheel_B_Smooth", (150.4, 2.8, 59.4), 1.5, 0.5, 'z', STEEL(), uv=1.0, verts=32)
cyl("L2_V2_Flywheel_Hub_A", (150.4, 2.8, 53.0), 0.4, 0.7, 'z', BLACK(), uv=1.0, verts=20)
cyl("L2_V2_Flywheel_Hub_B", (150.4, 2.8, 59.4), 0.4, 0.7, 'z', BLACK(), uv=1.0, verts=20)
cyl("L2_V2_Jumbo_Drive_Motor_Smooth", (153.6, 2.3, 56.2), 1.0, 2.4, 'x', BLUE(), uv=1.5, verts=26)
box("L2_V2_Curved_Style_Hazard_Belt_Guard", (151.0, 2.8, 56.2), (2.6, 3.4, 0.6), HAZ(), bevel=0.04, uv=1.8)
# discharge chute crusher->belt tail + rubber lip
aligned_box("L2_V2_Heavy_Discharge_Chute_To_Belt", (140.5, 4.6, 56.2), (135.8, 5.3, 56.9), 2.6, 0.5, STEEL(), bevel=0.05, uv=2.0)
box("L2_V2_Rubber_Lip_At_Discharge", (135.4, 5.25, 56.9), (0.5, 0.25, 3.0), RUBBER(), bevel=0.03, uv=1.2)

# stairs + platform + rails (back side z49-51)
for i in range(9):
    box("L2_V2_Jumbo_Service_Stair_Tread_%02d" % i, (135.0+i*0.42, -0.1+i*0.64, 50.3), (0.9, 0.1, 1.4), STEEL(), bevel=0.02, uv=1.2)
box("L2_V2_Service_Stair_Left_Stringer", (137.0, 2.5, 49.4), (4.0, 0.18, 0.18), STEEL(), bevel=0.02, uv=1.5)
box("L2_V2_Service_Stair_Right_Stringer", (137.0, 2.5, 51.2), (4.0, 0.18, 0.18), STEEL(), bevel=0.02, uv=1.5)
box("L2_V2_Jumbo_Service_Platform_Grating", (139.5, 5.4, 50.6), (5.0, 0.1, 2.2), STEEL(), bevel=0.02, uv=3.0)
for i in range(4):
    box("L2_V2_Platform_Back_Rail_post_%02d" % i, (135.5+i*2.6, 6.2, 49.0), (0.06, 1.0, 0.06), YEL(), bevel=0.015, uv=1.2)
box("L2_V2_Platform_Back_Rail_toprail", (139.0, 7.1, 49.0), (8.0, 0.06, 0.06), YEL(), bevel=0.015, uv=2.0)

# e-stop + lamp + badge + bolts
box("L2_V2_Red_EStop_Box", (151.2, 1.9, 53.0), (0.6, 0.6, 0.4), RED(), bevel=0.04, uv=1.0)
box("L2_V2_Status_Blue_Lamp", (151.1, 6.2, 53.0), (0.4, 0.4, 0.4), BLUE(), bevel=0.06, uv=1.0)
box("L2_V2_Front_Blue_Process_Badge", (145.3, 3.5, 52.6), (1.6, 1.0, 0.1), BLUE(), bevel=0.02, uv=1.0)
for i in range(6):
    box("L2_V2_Front_Round_Bolt_%02d" % i, (140.6+i*1.9, 6.2, 52.2), (0.16, 0.16, 0.12), STEEL(), bevel=0.04, uv=0.6)

# ================= ORE =================
oreM = ORE()
# feed ore (chunky, uncrushed) inside hopper mouth
for i in range(26):
    px = random.uniform(142.5, 147.8); pz = random.uniform(54.2, 58.4); py = random.uniform(7.8, 10.6)
    rock("L2_V2_Rounded_Ore_Rock_In_Hopper_%02d" % i, (px, py, pz), random.uniform(0.42, 0.85), oreM, crushed=False)
# CRUSHED ore on belt (small angular) — controller animates these
N_BELT = 60
for i in range(N_BELT):
    t = random.uniform(0.05, 0.95)
    p = belt_pt(t)
    lat = random.uniform(-1.9, 1.9)
    px = p[0]; py = p[1] + 0.18 + random.uniform(0.0, 0.12); pz = p[2] + lat
    rock("L2_V2_Rounded_Ore_Rock_On_Belt_%02d" % i, (px, py, pz), random.uniform(0.11, 0.27), oreM, crushed=True)

# ================= EXPORT =================
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(
    filepath=OUT, use_selection=True, apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_ALL', global_scale=1.0, bake_space_transform=True,
    axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE',
    use_mesh_modifiers=True, path_mode='COPY', embed_textures=False)
print("OLIVIA_ORECRUSHER_V3_EXPORT_OK objects=%d" % len(bpy.data.objects))
