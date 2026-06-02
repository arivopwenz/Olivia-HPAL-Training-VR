"""
OLIVIA VR - build_slurry_preheater_pipe_v2.py  (HEADLESS, HIGH-FIDELITY)
Pipa slurry feed industrial HPAL nikel: Slurry Tank -> Slurry Pump -> Pre-Heater.
Research: rubber-lined steel slurry main (abrasion resistant), flanged spool pieces,
pump suction/discharge spools + rubber expansion joint, pipe rack + supports,
sight-glass inspection spools (utk visual aliran slurry), orange pressure bands.
Konvensi Part 22: u2b=(-ux,-uz,uy), export FBX_SCALE_ALL+bake, instance IDENTITY di Unity.
UV: cube_project + bevel(harden_normals) + auto_smooth, texture set FlashCCDIndustrialUVRedesign.
Inner flow di-JOIN -> 1 mesh 'SlurryToPreheater_SlurryFlow' utk ProcessPipeFlowAnimator.
Run: blender --background --python build_slurry_preheater_pipe_v2.py
Output: Assets/Art/SlurryToPreheaterPipe/SlurryToPreheater_Pipe_IndustrialUV_v2.fbx
"""
import bpy, math, os
from mathutils import Vector

A = r"C:\Users\mp2dz\Olivia\Assets"
TEX = os.path.join(A, "Art", "FlashCCDIndustrialUVRedesign", "Textures")
OUT = os.path.join(A, "Art", "SlurryToPreheaterPipe", "SlurryToPreheater_Pipe_IndustrialUV_v2.fbx")

def u2b(ux, uy, uz): return Vector((-ux, -uz, uy))

bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete()
for blk in (bpy.data.meshes, bpy.data.materials, bpy.data.images):
    for b in list(blk):
        try: blk.remove(b)
        except Exception: pass

_mats = {}
def mat(name, tex, rough=0.7, metal=0.0, emis=None, ecol=(1, 1, 1)):
    if name in _mats: return _mats[name]
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial'); bsdf = nt.nodes.new('ShaderNodeBsdfPrincipled')
    bsdf.inputs['Roughness'].default_value = rough; bsdf.inputs['Metallic'].default_value = metal
    p = os.path.join(TEX, tex)
    if os.path.exists(p):
        img = bpy.data.images.load(p, check_existing=True)
        tn = nt.nodes.new('ShaderNodeTexImage'); tn.image = img
        bump = nt.nodes.new('ShaderNodeBump'); bump.inputs['Strength'].default_value = 0.16
        nt.links.new(tn.outputs['Color'], bsdf.inputs['Base Color'])
        nt.links.new(tn.outputs['Color'], bump.inputs['Height'])
        nt.links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])
    if emis is not None:
        try:
            bsdf.inputs['Emission Color'].default_value = (ecol[0], ecol[1], ecol[2], 1)
            bsdf.inputs['Emission Strength'].default_value = emis
        except Exception: pass
    nt.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    _mats[name] = m; return m

RUBBER = lambda: mat("M_L3P_Rubber", "UV_DarkRubber_Gasket.png", 0.82, 0.05)
STEEL  = lambda: mat("M_L3P_Steel", "UV_BrushedSteel_Grey.png", 0.42, 0.9)
GLASS  = lambda: mat("M_L3P_Glass", "UV_TransparentGlass_AcidRated.png", 0.12, 0.3)
SLURRY = lambda: mat("M_L3P_Slurry", "UV_ThickUnderflow_BrownPurple.png", 0.5, 0.0, emis=1.1, ecol=(0.42, 0.26, 0.13))
ORANGE = lambda: mat("M_L3P_OrangeBand", "UV_OrangePressureBand.png", 0.5, 0.3)
HAZ    = lambda: mat("M_L3P_Hazard", "UV_Hazard_BlackYellow.png", 0.55, 0.2)
BLUE   = lambda: mat("M_L3P_Blue", "UV_ChemicalPump_Blue.png", 0.45, 0.45)
CONC   = lambda: mat("M_L3P_Concrete", "UV_AcidResistantConcrete.png", 0.92, 0.0)
YEL    = lambda: mat("M_L3P_SafetyYellow", "UV_SafetyYellow_Rails.png", 0.55, 0.2)

def finalize(o, bevel=0.02, seg=1, uv=0.8, smooth=50.0):
    bpy.context.view_layer.objects.active = o; o.select_set(True)
    try:
        dim = min(o.dimensions.x, o.dimensions.y, o.dimensions.z)
        bw = min(bevel, max(0.003, dim * 0.32))
        md = o.modifiers.new('bev', 'BEVEL'); md.width = bw; md.segments = seg
        md.limit_method = 'ANGLE'; md.angle_limit = math.radians(35); md.harden_normals = True
        bpy.ops.object.modifier_apply(modifier=md.name)
    except Exception: pass
    try:
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.cube_project(cube_size=uv); bpy.ops.object.mode_set(mode='OBJECT')
    except Exception:
        try: bpy.ops.object.mode_set(mode='OBJECT')
        except Exception: pass
    try: bpy.ops.object.shade_auto_smooth(angle=math.radians(smooth))
    except Exception:
        try: bpy.ops.object.shade_smooth()
        except Exception: pass
    o.select_set(False)

def setmat(o, m): o.data.materials.clear(); o.data.materials.append(m)

def align(o, d):
    z = Vector((0, 0, 1)); d = d.normalized(); axis = z.cross(d)
    if axis.length < 1e-6:
        if d.z < 0: o.rotation_euler = (math.pi, 0, 0)
    else:
        ang = math.acos(max(-1.0, min(1.0, z.dot(d)))); axis.normalize()
        o.rotation_mode = 'AXIS_ANGLE'; o.rotation_axis_angle = (ang, axis.x, axis.y, axis.z)

def seg_cyl(uA, uB, r, m, name, uv=0.8, verts=30, fin=True):
    bA = u2b(*uA); bB = u2b(*uB); d = bB - bA; L = d.length
    if L < 1e-4: return None
    bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=L, vertices=verts, location=(bA + bB) * 0.5)
    o = bpy.context.active_object; o.name = name; align(o, d)
    bpy.ops.object.transform_apply(rotation=True); setmat(o, m)
    if fin: finalize(o, 0.02, 1, uv, 55)
    else:
        try: bpy.ops.object.shade_smooth()
        except Exception: pass
    return o

def ring(uC, r, depth, m, u_dir, name, uv=0.5):
    bC = u2b(*uC); bd = Vector(u2b(*u_dir))
    bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=depth, vertices=30, location=bC)
    o = bpy.context.active_object; o.name = name; align(o, bd)
    bpy.ops.object.transform_apply(rotation=True); setmat(o, m); finalize(o, 0.015, 1, uv, 55); return o

def elbow(uC, r, m, name):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=r, location=u2b(*uC), segments=22, ring_count=14)
    o = bpy.context.active_object; o.name = name; setmat(o, m); finalize(o, 0.02, 1, 0.7, 55); return o

def flange(uC, pr, u_dir, name, nbolt=6):
    bC = u2b(*uC); bd = Vector(u2b(*u_dir)).normalized()
    bpy.ops.mesh.primitive_cylinder_add(radius=pr * 1.42, depth=0.18, vertices=30, location=bC)
    o = bpy.context.active_object; o.name = name; align(o, bd)
    bpy.ops.object.transform_apply(rotation=True); setmat(o, STEEL()); finalize(o, 0.02, 1, 0.7, 55)
    # bolt circle
    up = Vector((0, 0, 1)); S = bd.cross(up)
    if S.length < 1e-4: S = bd.cross(Vector((1, 0, 0)))
    S.normalize(); T = bd.cross(S).normalized()
    for k in range(nbolt):
        ang = 2 * math.pi * k / nbolt
        loc = bC + (S * math.cos(ang) + T * math.sin(ang)) * (pr * 1.12)
        bpy.ops.mesh.primitive_cylinder_add(radius=0.05, depth=0.24, vertices=6, location=loc)
        bo = bpy.context.active_object; bo.name = name + "_Bolt%d" % k; align(bo, bd)
        bpy.ops.object.transform_apply(rotation=True); setmat(bo, STEEL())
    return o

flow_objs = []
def lay(name, upts, RO, RI, every=6.0, glass_mod=3):
    # inner flow: 1 cyl per polyline segment (continuous tube, joined later)
    for i in range(len(upts) - 1):
        f = seg_cyl(upts[i], upts[i + 1], RI, SLURRY(), "%s_FlowSeg_%d" % (name, i), uv=0.9, verts=24, fin=False)
        if f: flow_objs.append(f)
    # outer: flanged spool pieces (rubber, occasional sight-glass), flange + band per joint
    si = 0
    for i in range(len(upts) - 1):
        A_ = Vector(upts[i]); B_ = Vector(upts[i + 1]); d = B_ - A_; L = d.length
        n = max(1, int(round(L / every))); dn = d / n
        for s in range(n):
            a = A_ + dn * s; b = A_ + dn * (s + 1)
            glass = (si % glass_mod == glass_mod - 1)
            seg_cyl(tuple(a), tuple(b), RO, GLASS() if glass else RUBBER(),
                    "%s_%sSpool_%d" % (name, "Glass" if glass else "Rubber", si), uv=0.8)
            flange(tuple(a), RO, tuple(d), "%s_Flange_%d" % (name, si))
            if not glass and s % 2 == 0:
                mid = (a + b) * 0.5
                ring(tuple(mid), RO * 1.06, 0.12, ORANGE(), tuple(d), "%s_Band_%d" % (name, si))
            si += 1
        if i < len(upts) - 2:
            elbow(tuple(B_), RO * 1.05, RUBBER(), "%s_Elbow_%d" % (name, i))
    flange(upts[-1], RO, tuple(Vector(upts[-1]) - Vector(upts[-2])), "%s_FlangeEnd" % name)

def support(ux, uy_top, uz, name, ground=0.0):
    seg_cyl((ux, uy_top, uz), (ux, ground + 0.1, uz), 0.16, STEEL(), name + "_Col", uv=0.6, verts=16)
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=u2b(ux, ground + 0.06, uz))
    f = bpy.context.active_object; f.name = name + "_Foot"; f.scale = (0.55, 0.12, 0.55)
    bpy.ops.object.transform_apply(scale=True); setmat(f, CONC()); finalize(f, 0.02, 1, 0.8)
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=u2b(ux, uy_top + 0.02, uz))
    sh = bpy.context.active_object; sh.name = name + "_Shoe"; sh.scale = (0.65, 0.18, 0.55)
    bpy.ops.object.transform_apply(scale=True); setmat(sh, YEL()); finalize(sh, 0.02, 1, 0.7)

def valve(uC, pr, u_dir, name):
    bC = u2b(*uC); bd = Vector(u2b(*u_dir)).normalized()
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=bC)
    body = bpy.context.active_object; body.name = name + "_Body"
    body.scale = (pr * 2.1, pr * 2.1, pr * 1.7); bpy.ops.object.transform_apply(scale=True)
    setmat(body, BLUE()); finalize(body, 0.04, 2, 0.7)
    top = bC + Vector((0, 0, pr * 1.9))
    bpy.ops.mesh.primitive_cylinder_add(radius=pr * 0.42, depth=pr * 1.6, vertices=16, location=bC + Vector((0, 0, pr * 1.1)))
    bon = bpy.context.active_object; bon.name = name + "_Bonnet"; setmat(bon, STEEL()); finalize(bon, 0.02, 1, 0.6)
    bpy.ops.mesh.primitive_torus_add(location=top, major_radius=pr * 0.9, minor_radius=pr * 0.13)
    wh = bpy.context.active_object; wh.name = name + "_Handwheel"; setmat(wh, ORANGE())
    try: bpy.ops.object.shade_smooth()
    except Exception: pass

# ---- ROUTE (Unity world): slurry tank -> SLURRY PUMP (suction->discharge) -> STRAIGHT vertical riser -> STRAIGHT header -> 2 preheater flanges
# intake TERCELUP di DALAM slurry @ StirrerColumn(1) XZ; pool surface y~1.81; rim tangki y~7.86
SX, SZ = 86.68, 55.14               # L3_SlurryTank_StirrerColumn_Static (1) intake XZ (TERCELUP di slurry)
HY   = 10.84                        # header / preheater inlet height (sejajar L5_CleanOutlet_Flange)
TEE  = (22.0, HY, SZ)               # TEE sejajar intake -> main LURUS sempurna
RO, RI = 0.66, 0.48                 # slurry main DIBESARKAN (OD ~1.32 m)

# intake TERCELUP di dalam slurry -> naik TEGAK lewat bibir tangki -> LURUS horizontal ke preheater (TANPA turun)
main = [(SX, 0.8, SZ),              # intake tercelup di dalam slurry
        (SX, HY, SZ),               # naik TEGAK lewat bibir tangki ke ketinggian header
        TEE]                        # LURUS horizontal ke preheater
brA  = [TEE, (22.0, HY, 57.09), (20.7, HY, 57.09)]          # -> L5_CleanOutlet_Flange A
brB  = [TEE, (22.0, HY, 45.26), (20.7, HY, 45.26)]          # -> L5_CleanOutlet_Flange B

lay("SlurryToPreheater_Main", main, RO, RI, every=6.0, glass_mod=3)
lay("SlurryToPreheater_BranchA", brA, RO * 0.8, RI * 0.8, every=4.0, glass_mod=2)
lay("SlurryToPreheater_BranchB", brB, RO * 0.8, RI * 0.8, every=4.0, glass_mod=2)

# pump tie-in detailing: rubber expansion joint (bellows rings) + isolation valve + hazard band
# pump tie-in detail dihapus: rute lurus, tidak ada riser/dip di pompa

# pipe rack supports (overhead header to ground) + low-run + branch
for k, (x, yt, z) in enumerate([(78.0, HY, SZ), (66.0, HY, SZ), (54.0, HY, SZ),
                                (42.0, HY, SZ), (30.0, HY, SZ), (24.0, HY, SZ),
                                (22.0, HY, 45.26)]):
    support(x, yt, z, "SlurryToPreheater_Support_%02d" % k)

# join inner flow -> single mesh for ProcessPipeFlowAnimator
flow_objs = [o for o in flow_objs if o is not None]
if flow_objs:
    bpy.ops.object.select_all(action='DESELECT')
    for o in flow_objs: o.select_set(True)
    bpy.context.view_layer.objects.active = flow_objs[0]
    bpy.ops.object.join(); flow_objs[0].name = "SlurryToPreheater_SlurryFlow"

bpy.ops.object.select_all(action='SELECT')
os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, apply_unit_scale=True,
                         apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
                         axis_forward='-Z', axis_up='Y', object_types={'MESH'}, mesh_smooth_type='FACE')
print("OLIVIA_SLURRYPIPE_V2_OK ->", OUT)
