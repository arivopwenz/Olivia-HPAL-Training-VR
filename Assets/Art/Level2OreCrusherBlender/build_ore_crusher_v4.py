import bpy, bmesh, math, random, os
from mathutils import Vector

random.seed(2027)
A = r"C:\Users\mp2dz\Olivia\Assets"
TEX = os.path.join(A, "Art", "FlashCCDIndustrialUVRedesign", "Textures")
OUT = os.path.join(A, "Art", "Level2OreCrusherBlender", "Level2_OreCrusher_IndustrialUV_v4.fbx")

def u2b(ux, uy, uz): return Vector((-ux, -uz, uy))
def dsz(sx, sy, sz): return (sx, sz, sy)

bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete()
for blk in (bpy.data.meshes, bpy.data.materials, bpy.data.images):
    for b in list(blk):
        try: blk.remove(b)
        except Exception: pass

_mats = {}
def mat(name, tex, rough=0.7, metal=0.0, emis=None, ecol=(1,1,1)):
    if name in _mats: return _mats[name]
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial'); bsdf = nt.nodes.new('ShaderNodeBsdfPrincipled')
    bsdf.inputs['Roughness'].default_value = rough; bsdf.inputs['Metallic'].default_value = metal
    path = os.path.join(TEX, tex)
    if os.path.exists(path):
        img = bpy.data.images.load(path, check_existing=True)
        tn = nt.nodes.new('ShaderNodeTexImage'); tn.image = img
        # detail boost: bump from texture for micro-HD relief
        bump = nt.nodes.new('ShaderNodeBump'); bump.inputs['Strength'].default_value = 0.18
        nt.links.new(tn.outputs['Color'], bsdf.inputs['Base Color'])
        nt.links.new(tn.outputs['Color'], bump.inputs['Height'])
        nt.links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])
    if emis is not None:
        try:
            bsdf.inputs['Emission Color'].default_value = (ecol[0],ecol[1],ecol[2],1)
            bsdf.inputs['Emission Strength'].default_value = emis
        except Exception: pass
    nt.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    _mats[name] = m; return m

STEEL = lambda: mat("M_L2_BrushedSteel", "UV_BrushedSteel_Grey.png", 0.5, 0.85)
HULL  = lambda: mat("M_L2_CrusherHull", "UV_AcidTank_316L_OffWhite.png", 0.45, 0.7)
RUBBER= lambda: mat("M_L2_BeltRubber", "UV_DarkRubber_Gasket.png", 0.82, 0.0)
HAZ   = lambda: mat("M_L2_Hazard", "UV_Hazard_BlackYellow.png", 0.55, 0.2)
YEL   = lambda: mat("M_L2_SafetyYellow", "UV_SafetyYellow_Rails.png", 0.55, 0.2)
CONC  = lambda: mat("M_L2_Concrete", "UV_AcidResistantConcrete.png", 0.92, 0.0)
ORE   = lambda: mat("M_L2_OreRock", "UV_ThickUnderflow_BrownPurple.png", 0.95, 0.0)
BLUE  = lambda: mat("M_L2_ProcessBlue", "UV_ChemicalPump_Blue.png", 0.45, 0.4)
RED   = lambda: mat("M_L2_EmergencyRed", "UV_EmergencyRed.png", 0.5, 0.2, emis=2.0, ecol=(0.9,0.05,0.05))
BLACK = lambda: mat("M_L2_BlackBox", "UV_DarkRubber_Gasket.png", 0.6, 0.25)

def finalize(o, bevel=0.025, seg=2, uv=1.4, smooth=40.0):
    bpy.context.view_layer.objects.active = o; o.select_set(True)
    try:
        dim = min(o.dimensions.x, o.dimensions.y, o.dimensions.z)
        bw = min(bevel, max(0.004, dim*0.32))
        md = o.modifiers.new('bev','BEVEL'); md.width=bw; md.segments=seg
        md.limit_method='ANGLE'; md.angle_limit=math.radians(35); md.harden_normals=True
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

def box(name, upos, usize, m, bevel=0.025, seg=2, uv=1.4):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=u2b(*upos))
    o = bpy.context.active_object; o.name = name
    sx, sy, sz = dsz(*usize); o.scale = (sx, sy, sz)
    bpy.ops.object.transform_apply(scale=True); setmat(o, m); finalize(o, bevel, seg, uv); return o

def cyl(name, upos, radius, length, axis, m, bevel=0.02, uv=1.2, verts=24):
    bpy.ops.mesh.primitive_cylinder_add(radius=radius, depth=length, vertices=verts, location=u2b(*upos))
    o = bpy.context.active_object; o.name = name
    if axis=='x': o.rotation_euler=(0, math.radians(90), 0)
    elif axis=='z': o.rotation_euler=(math.radians(90), 0, 0)
    bpy.ops.object.transform_apply(rotation=True); setmat(o, m); finalize(o, bevel, 1, uv, 50); return o

def aligned_box(name, ua, ub, width, thick, m, bevel=0.02, uv=1.4):
    A_=Vector((ua[0],ua[1],ua[2])); B_=Vector((ub[0],ub[1],ub[2]))
    L=(B_-A_); length=max(0.01,L.length); L=L.normalized()
    S=Vector((0,1,0)).cross(L)
    if S.length<1e-4: S=Vector((0,0,1))
    S=S.normalized(); N=L.cross(S).normalized(); C=(A_+B_)*0.5
    hl,hw,ht=length*0.5,width*0.5,thick*0.5; verts=[]
    for sl in (-1,1):
        for ss in (-1,1):
            for sn in (-1,1):
                p=C+L*(hl*sl)+S*(hw*ss)+N*(ht*sn); verts.append(list(u2b(p.x,p.y,p.z)))
    faces=[(0,1,3,2),(4,5,7,6),(0,1,5,4),(2,3,7,6),(0,2,6,4),(1,3,7,5)]
    me=bpy.data.meshes.new(name); me.from_pydata(verts,[],faces); me.update()
    o=bpy.data.objects.new(name,me); bpy.context.collection.objects.link(o)
    bpy.context.view_layer.objects.active=o; o.select_set(True)
    try:
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.mesh.normals_make_consistent(inside=False); bpy.ops.object.mode_set(mode='OBJECT')
    except Exception:
        try: bpy.ops.object.mode_set(mode='OBJECT')
        except Exception: pass
    o.select_set(False); setmat(o,m); finalize(o,bevel,2,uv); return o

def rock(name, upos, size, m, crushed=True):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=(1 if crushed else 2), radius=size, location=u2b(*upos))
    o=bpy.context.active_object; o.name=name; me=o.data; bm=bmesh.new(); bm.from_mesh(me)
    amp=0.5 if crushed else 0.28
    for v in bm.verts:
        v.co += Vector((random.uniform(-amp,amp),random.uniform(-amp,amp),random.uniform(-amp,amp)))*size
    bm.to_mesh(me); bm.free()
    o.scale=(random.uniform(0.85,1.2),random.uniform(0.6,0.95),random.uniform(0.85,1.2))
    o.rotation_euler=(random.uniform(0,6.28),random.uniform(0,6.28),random.uniform(0,6.28))
    bpy.ops.object.transform_apply(scale=True,rotation=True); setmat(o,m)
    try: bpy.ops.object.shade_smooth()
    except Exception: pass
    return o

# ===== BELT: crusher discharge (low) -> over slurry tank inlet (high) =====
TAIL = (140.0, 2.7, 56.5)   # crusher discharge / black box end (low)
HEAD = (99.8, 9.2, 55.4)    # over slurry tank ore inlet (high)
def belt_pt(t): return (TAIL[0]+(HEAD[0]-TAIL[0])*t, TAIL[1]+(HEAD[1]-TAIL[1])*t, TAIL[2]+(HEAD[2]-TAIL[2])*t)
WIDTH = 4.2

# belt surface (controller scrolls _BaseMap; CLEAN - no ore on it)
aligned_box("L2_V2_Wide_Inclined_Rubber_Ore_Belt", TAIL, HEAD, WIDTH, 0.16, RUBBER(), bevel=0.04, uv=2.4)

NROLL = 19
for i in range(NROLL):
    t=i/(NROLL-1.0); p=belt_pt(t); p=(p[0],p[1]-0.13,p[2])
    cyl("L2_V2_Smooth_Trough_Roller_%02d"%i, p, 0.16, WIDTH+0.2, 'z', STEEL(), uv=1.0, verts=20)
cyl("L2_V2_Smooth_Head_Pulley", (HEAD[0],HEAD[1]-0.05,HEAD[2]), 0.34, WIDTH+0.4, 'z', STEEL(), uv=1.0, verts=28)
cyl("L2_V2_Smooth_Tail_Pulley", (TAIL[0],TAIL[1]-0.05,TAIL[2]), 0.34, WIDTH+0.4, 'z', STEEL(), uv=1.0, verts=28)

for side, z, zt in (("Left", 54.6, 54.2), ("Right", 58.6, 59.0)):
    aligned_box("L2_V2_%s_Tall_Yellow_Side_Skirt"%side, (TAIL[0],TAIL[1]+0.42,z),(HEAD[0],HEAD[1]+0.42,z), 0.1, 0.85, HAZ(), uv=2.0)
    aligned_box("L2_V2_%s_Deep_Main_Truss"%side, (TAIL[0],TAIL[1]-0.55,zt),(HEAD[0],HEAD[1]-0.55,zt), 0.18, 0.8, STEEL(), uv=2.2)
    for i in range(NROLL-2):
        t=(i+0.5)/(NROLL-1.0); p=belt_pt(t)
        box("L2_V2_%s_Truss_Diagonal_%02d"%(side,i),(p[0],p[1]-0.55,zt),(2.0,0.1,0.1),STEEL(),bevel=0.02,uv=1.2)
    for i in range(0, NROLL, 2):
        t=i/(NROLL-1.0); p=belt_pt(t); gy=-0.6
        h=max(0.4,(p[1]-0.85)-gy)
        box("L2_V2_%s_Jumbo_Belt_Support_%02d"%(side,i),(p[0],(p[1]-0.85+gy)/2.0,zt),(0.2,h,0.2),STEEL(),bevel=0.02,uv=1.2)
        box("L2_V2_%s_Jumbo_Foot_%02d"%(side,i),(p[0],gy,zt),(0.7,0.16,0.7),STEEL(),bevel=0.03,uv=1.4)

# maintenance catwalk + rail (avoid 'conveyor')
aligned_box("L2_V2_Wide_Maint_Catwalk",(TAIL[0],TAIL[1]-0.1,53.0),(HEAD[0],HEAD[1]-0.1,53.0),1.3,0.07,STEEL(),uv=2.6)
for i in range(8):
    t=i/7.0; p=belt_pt(t)
    box("L2_V2_Maint_Catwalk_Rail_post_%02d"%i,(p[0],p[1]+0.45,52.3),(0.05,1.0,0.05),YEL(),bevel=0.012,uv=1.0)
aligned_box("L2_V2_Maint_Catwalk_Rail_toprail",(TAIL[0],TAIL[1]+0.95,52.3),(HEAD[0],HEAD[1]+0.95,52.3),0.05,0.05,YEL(),bevel=0.012,uv=2.0)

# ===== CRUSHER + BLACK BOX DISCHARGE (spawn) =====
box("L2_V2_Jumbo_Concrete_Service_Pad",(146.0,-0.55,55.0),(12.5,0.5,11.0),CONC(),bevel=0.05,uv=2.5)
box("L2_V2_Jumbo_Black_Skid_Base",(145.0,0.1,56.0),(8.0,0.6,7.0),BLACK(),bevel=0.04,uv=1.6)
box("L2_V2_Rounded_Primary_Crusher_Body",(145.0,3.6,56.2),(6.4,5.6,6.0),HULL(),bevel=0.18,seg=3,uv=1.6)
box("L2_V2_Left_Smooth_Jaw_Liner",(141.8,3.6,54.8),(0.6,3.6,2.6),STEEL(),bevel=0.05,uv=1.0)
box("L2_V2_Right_Smooth_Jaw_Liner",(141.8,3.6,57.6),(0.6,3.6,2.6),STEEL(),bevel=0.05,uv=1.0)
box("L2_V2_Dark_Jaw_Mouth_Recess",(141.2,3.8,56.2),(0.5,2.6,2.4),BLACK(),bevel=0.04,uv=0.9)
# *** BLACK BOX: crusher discharge chute = ORE SPAWN, connects crusher to belt tail (no gap) ***
box("L2_V2_Crusher_Discharge_BlackBox",(141.0,2.7,56.4),(3.2,2.6,3.6),BLACK(),bevel=0.05,uv=1.2)
box("L2_V2_Crusher_Discharge_BlackBox_Lip",(139.4,2.3,56.5),(1.0,0.3,3.4),RUBBER(),bevel=0.03,uv=1.0)
# flared hopper above crusher + throat
box("L2_V2_SuperJumbo_Flared_Ore_Hopper",(145.0,8.4,56.2),(7.6,3.4,7.2),STEEL(),bevel=0.06,uv=2.0)
box("L2_V2_Inner_Black_Hopper_Throat",(145.0,6.4,56.2),(3.0,1.6,3.0),BLACK(),bevel=0.04,uv=1.0)
cyl("L2_V2_Jumbo_Flywheel_A_Smooth",(150.4,3.0,53.2),1.5,0.5,'z',STEEL(),uv=0.9,verts=32)
cyl("L2_V2_Jumbo_Flywheel_B_Smooth",(150.4,3.0,59.2),1.5,0.5,'z',STEEL(),uv=0.9,verts=32)
cyl("L2_V2_Flywheel_Hub_A",(150.4,3.0,53.2),0.4,0.7,'z',BLACK(),uv=0.9,verts=20)
cyl("L2_V2_Flywheel_Hub_B",(150.4,3.0,59.2),0.4,0.7,'z',BLACK(),uv=0.9,verts=20)
cyl("L2_V2_Jumbo_Drive_Motor_Smooth",(153.6,2.5,56.2),1.0,2.4,'x',BLUE(),uv=1.2,verts=26)
box("L2_V2_Curved_Style_Hazard_Belt_Guard",(151.0,3.0,56.2),(2.6,3.6,0.55),HAZ(),bevel=0.04,uv=1.5)

# ===== DISCHARGE CHUTE INTO SLURRY TANK (head end) =====
aligned_box("L2_V2_Heavy_Discharge_Chute_To_Tank",(99.8,8.7,55.4),(96.5,7.0,55.2),2.4,0.45,STEEL(),bevel=0.05,uv=1.8)
box("L2_V2_Rubber_Lip_At_Discharge",(96.2,6.9,55.2),(0.5,0.22,2.8),RUBBER(),bevel=0.03,uv=1.0)
box("L2_V2_Discharge_Hood_Cover",(98.6,9.6,55.4),(3.0,0.5,3.4),STEEL(),bevel=0.04,uv=1.6)

# service stair + platform near crusher
for i in range(9):
    box("L2_V2_Jumbo_Service_Stair_Tread_%02d"%i,(135.5+i*0.42,0.0+i*0.6,50.5),(0.9,0.1,1.4),STEEL(),bevel=0.02,uv=1.0)
box("L2_V2_Service_Stair_Left_Stringer",(137.5,2.5,49.6),(4.0,0.16,0.16),STEEL(),bevel=0.02,uv=1.2)
box("L2_V2_Service_Stair_Right_Stringer",(137.5,2.5,51.4),(4.0,0.16,0.16),STEEL(),bevel=0.02,uv=1.2)
box("L2_V2_Jumbo_Service_Platform_Grating",(140.0,5.6,50.8),(5.0,0.1,2.2),STEEL(),bevel=0.02,uv=2.5)
# fixtures
box("L2_V2_Red_EStop_Box",(151.2,2.0,53.0),(0.6,0.6,0.4),RED(),bevel=0.04,uv=0.8)
box("L2_V2_Status_Blue_Lamp",(151.1,6.4,53.0),(0.4,0.4,0.4),BLUE(),bevel=0.06,uv=0.8)
box("L2_V2_Front_Blue_Process_Badge",(145.3,3.8,52.6),(1.6,1.0,0.1),BLUE(),bevel=0.02,uv=0.8)
for i in range(6):
    box("L2_V2_Front_Round_Bolt_%02d"%i,(140.8+i*1.8,6.4,52.2),(0.15,0.15,0.1),STEEL(),bevel=0.04,uv=0.5)

# ===== ORE: hopper feed only (NO belt ore -> belt CLEAN) =====
oreM = ORE()
for i in range(22):
    px=random.uniform(142.6,147.6); pz=random.uniform(54.4,58.2); py=random.uniform(8.0,10.6)
    rock("L2_V2_Rounded_Ore_Rock_In_Hopper_%02d"%i,(px,py,pz),random.uniform(0.42,0.82),oreM,crushed=False)

bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_ALL', global_scale=1.0, bake_space_transform=True,
    axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE', use_mesh_modifiers=True,
    path_mode='COPY', embed_textures=False)
print("OLIVIA_ORECRUSHER_V4_EXPORT_OK objects=%d" % len(bpy.data.objects))
