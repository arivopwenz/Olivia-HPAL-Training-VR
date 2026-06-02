import bpy, bmesh, math, os
from mathutils import Vector

A = r"C:\Users\mp2dz\Olivia\Assets"
TEX = os.path.join(A, "Art", "FlashCCDIndustrialUVRedesign", "Textures")
OUT = os.path.join(A, "Art", "Level3SlurryTankBlender", "Level3_SlurryTank_IndustrialUV_v2.fbx")
os.makedirs(os.path.dirname(OUT), exist_ok=True)

def u2b(ux, uy, uz): return Vector((-ux, -uz, uy))
def dsz(sx, sy, sz): return (sx, sz, sy)

bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete()
for blk in (bpy.data.meshes, bpy.data.materials, bpy.data.images):
    for b in list(blk):
        try: blk.remove(b)
        except Exception: pass

_mats = {}
def mat(name, tex, rough=0.6, metal=0.7, emis=None, ecol=(1,1,1)):
    if name in _mats: return _mats[name]
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial'); bsdf = nt.nodes.new('ShaderNodeBsdfPrincipled')
    bsdf.inputs['Roughness'].default_value = rough; bsdf.inputs['Metallic'].default_value = metal
    path = os.path.join(TEX, tex)
    if os.path.exists(path):
        img = bpy.data.images.load(path, check_existing=True)
        tn = nt.nodes.new('ShaderNodeTexImage'); tn.image = img
        bump = nt.nodes.new('ShaderNodeBump'); bump.inputs['Strength'].default_value = 0.16
        nt.links.new(tn.outputs['Color'], bsdf.inputs['Base Color'])
        nt.links.new(tn.outputs['Color'], bump.inputs['Height'])
        nt.links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])
    if emis is not None:
        try:
            bsdf.inputs['Emission Color'].default_value=(ecol[0],ecol[1],ecol[2],1); bsdf.inputs['Emission Strength'].default_value=emis
        except Exception: pass
    nt.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    _mats[name]=m; return m

STEEL316 = lambda: mat("Nickel_Slurry_Tank_Industrial_Green", "UV_AcidTank_316L_OffWhite.png", 0.42, 0.72)
BRUSHED  = lambda: mat("M_L3_BrushedSteel", "UV_BrushedSteel_Grey.png", 0.5, 0.85)
DARK     = lambda: mat("Nickel_Crusher_DarkSteel", "UV_DarkRubber_Gasket.png", 0.55, 0.5)
HAZ      = lambda: mat("M_L3_Hazard", "UV_Hazard_BlackYellow.png", 0.55, 0.2)
YEL      = lambda: mat("Nickel_Process_SafetyYellow", "UV_SafetyYellow_Rails.png", 0.5, 0.2)
ORANGE   = lambda: mat("M_L3_OrangeBand", "UV_EmergencyRed.png", 0.5, 0.3, emis=0.5, ecol=(1.0,0.45,0.05))
CONC     = lambda: mat("M_L3_Concrete", "UV_AcidResistantConcrete.png", 0.93, 0.0)
BLUE     = lambda: mat("M_L3_ProcessBlue", "UV_ChemicalPump_Blue.png", 0.45, 0.45)
WATERMAT = lambda: mat("Nickel_WaterTank_PaintedSteel", "UV_BrushedSteel_Grey.png", 0.4, 0.78)
AGI      = lambda: mat("Agitator_Metal", "UV_BrushedSteel_Grey.png", 0.4, 0.9)

def finalize(o, bevel=0.025, seg=2, uv=1.6, smooth=42.0):
    bpy.context.view_layer.objects.active=o; o.select_set(True)
    try:
        dim=min(o.dimensions.x,o.dimensions.y,o.dimensions.z); bw=min(bevel,max(0.004,dim*0.3))
        md=o.modifiers.new('bev','BEVEL'); md.width=bw; md.segments=seg; md.limit_method='ANGLE'; md.angle_limit=math.radians(35); md.harden_normals=True
        bpy.ops.object.modifier_apply(modifier=md.name)
    except Exception: pass
    try:
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.uv.cube_project(cube_size=uv); bpy.ops.object.mode_set(mode='OBJECT')
    except Exception:
        try: bpy.ops.object.mode_set(mode='OBJECT')
        except Exception: pass
    try: bpy.ops.object.shade_auto_smooth(angle=math.radians(smooth))
    except Exception:
        try: bpy.ops.object.shade_smooth()
        except Exception: pass
    o.select_set(False)

def setmat(o,m): o.data.materials.clear(); o.data.materials.append(m)

def box(name, upos, usize, m, bevel=0.025, seg=2, uv=1.6):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=u2b(*upos))
    o=bpy.context.active_object; o.name=name; sx,sy,sz=dsz(*usize); o.scale=(sx,sy,sz)
    bpy.ops.object.transform_apply(scale=True); setmat(o,m); finalize(o,bevel,seg,uv); return o

def cyl(name, upos, radius, length, axis, m, bevel=0.02, uv=1.4, verts=40, cone_r2=None):
    if cone_r2 is not None:
        bpy.ops.mesh.primitive_cone_add(radius1=radius, radius2=cone_r2, depth=length, vertices=verts, location=u2b(*upos))
    else:
        bpy.ops.mesh.primitive_cylinder_add(radius=radius, depth=length, vertices=verts, location=u2b(*upos))
    o=bpy.context.active_object; o.name=name
    if axis=='x': o.rotation_euler=(0, math.radians(90), 0)
    elif axis=='z': o.rotation_euler=(math.radians(90), 0, 0)
    # axis=='y' -> vertical (Blender Z = Unity Y), no rotation
    bpy.ops.object.transform_apply(rotation=True); setmat(o,m); finalize(o,bevel,1,uv,55); return o

def tube(name, upos, radius, length, axis, m, uv=1.2, verts=24):
    return cyl(name, upos, radius, length, axis, m, bevel=0.015, uv=uv, verts=verts)

def aligned_box(name, ua, ub, width, thick, m, bevel=0.02, uv=1.6):
    A_=Vector(ua); B_=Vector(ub); L=(B_-A_); length=max(0.01,L.length); L=L.normalized()
    S=Vector((0,1,0)).cross(L)
    if S.length<1e-4: S=Vector((0,0,1))
    S=S.normalized(); N=L.cross(S).normalized(); C=(A_+B_)*0.5; hl,hw,ht=length*0.5,width*0.5,thick*0.5; verts=[]
    for sl in (-1,1):
        for ss in (-1,1):
            for sn in (-1,1):
                p=C+L*(hl*sl)+S*(hw*ss)+N*(ht*sn); verts.append(list(u2b(p.x,p.y,p.z)))
    faces=[(0,1,3,2),(4,5,7,6),(0,1,5,4),(2,3,7,6),(0,2,6,4),(1,3,7,5)]
    me=bpy.data.meshes.new(name); me.from_pydata(verts,[],faces); me.update()
    o=bpy.data.objects.new(name,me); bpy.context.collection.objects.link(o)
    bpy.context.view_layer.objects.active=o; o.select_set(True)
    try:
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.mesh.normals_make_consistent(inside=False); bpy.ops.object.mode_set(mode='OBJECT')
    except Exception:
        try: bpy.ops.object.mode_set(mode='OBJECT')
        except Exception: pass
    o.select_set(False); setmat(o,m); finalize(o,bevel,2,uv); return o

# ================= SLURRY TANK (agitated mixing tank) center (88,0,55) =================
CX,CZ = 88.0, 55.0
R = 9.0           # shell radius
PLY = 0.45        # plinth top
SHELL_BOT = PLY
SHELL_TOP = 8.9
SH = SHELL_TOP-SHELL_BOT

# concrete plinth + base ring
cyl("L3_SlurryTank_Plinth", (CX,PLY/2,CZ), R+0.9, PLY, 'y', CONC(), uv=2.2, verts=48)
# bottom dished/cone head
cyl("L3_SlurryTank_BottomHead", (CX,PLY+0.55,CZ), R*0.45, 1.1, 'y', STEEL316(), cone_r2=R, uv=1.6, verts=48)
# MAIN SHELL (open-top agitated tank)
cyl("Slurry Tank", (CX,(SHELL_BOT+SHELL_TOP)/2,CZ), R, SH, 'y', STEEL316(), bevel=0.05, uv=2.2, verts=56)
# weld bands
for i,y in enumerate([2.4,4.4,6.4]):
    cyl("L3_SlurryTank_WeldBand_%02d"%i,(CX,y,CZ), R+0.05, 0.22,'y', BRUSHED(), bevel=0.03, uv=1.0, verts=56)
# orange base band + hazard top band
cyl("L3_SlurryTank_OrangeBand",(CX,SHELL_BOT+0.45,CZ), R+0.08, 0.7,'y', ORANGE(), bevel=0.03, uv=1.0, verts=56)
cyl("L3_SlurryTank_HazardBand",(CX,SHELL_TOP-0.45,CZ), R+0.08, 0.8,'y', HAZ(), bevel=0.03, uv=2.6, verts=56)

# top annular platform grating + handrail
NSEG=28
for i in range(NSEG):
    a=2*math.pi*i/NSEG; px=CX+math.cos(a)*(R+1.1); pz=CZ+math.sin(a)*(R+1.1)
    box("L3_SlurryTank_Platform_%02d"%i,(px,SHELL_TOP+0.05,pz),(2.6,0.12,1.9),BRUSHED(),bevel=0.02,uv=1.6)
    rx=CX+math.cos(a)*(R+2.0); rz=CZ+math.sin(a)*(R+2.0)
    box("L3_SlurryTank_RailPost_%02d"%i,(rx,SHELL_TOP+0.55,rz),(0.08,1.05,0.08),YEL(),bevel=0.012,uv=1.0)
for i in range(NSEG):
    a0=2*math.pi*i/NSEG; a1=2*math.pi*(i+1)/NSEG
    pa=(CX+math.cos(a0)*(R+2.0),SHELL_TOP+1.05,CZ+math.sin(a0)*(R+2.0))
    pb=(CX+math.cos(a1)*(R+2.0),SHELL_TOP+1.05,CZ+math.sin(a1)*(R+2.0))
    aligned_box("L3_SlurryTank_TopRail_%02d"%i,pa,pb,0.06,0.06,YEL(),bevel=0.01,uv=2.0)

# AGITATOR DRIVE BRIDGE (visual) across top + motor + gearbox at center
aligned_box("L3_SlurryTank_DriveBridge",(CX-(R+1.0),SHELL_TOP+0.7,CZ),(CX+(R+1.0),SHELL_TOP+0.7,CZ),1.3,0.5,BRUSHED(),bevel=0.04,uv=2.4)
box("L3_SlurryTank_Gearbox",(CX,SHELL_TOP+1.25,CZ),(2.0,1.1,1.8),DARK(),bevel=0.06,uv=1.4)
cyl("L3_SlurryTank_DriveMotor",(CX,SHELL_TOP+2.3,CZ),0.85,1.9,'y',BLUE(),bevel=0.04,uv=1.2,verts=28)
cyl("L3_SlurryTank_MotorFan",(CX,SHELL_TOP+3.35,CZ),0.55,0.5,'y',DARK(),uv=0.8,verts=20)

# CAGE LADDER on west side (-x)
LX=CX-R-0.5
for i in range(11):
    box("L3_SlurryTank_LadderRung_%02d"%i,(LX,SHELL_BOT+0.5+i*0.8,CZ-3.0),(0.05,0.05,0.7),BRUSHED(),bevel=0.01,uv=0.6)
box("L3_SlurryTank_LadderRailA",(LX,SHELL_TOP/2+0.3,CZ-3.35),(0.08,SH,0.08),BRUSHED(),bevel=0.02,uv=1.0)
box("L3_SlurryTank_LadderRailB",(LX,SHELL_TOP/2+0.3,CZ-2.65),(0.08,SH,0.08),BRUSHED(),bevel=0.02,uv=1.0)
for i in range(6):
    a=math.radians(-90+i*36)
    box("L3_SlurryTank_LadderCage_%02d"%i,(LX-0.45,SHELL_BOT+1.5+i*1.1,CZ-3.0),(0.06,0.06,1.5),BRUSHED(),bevel=0.01,uv=1.0)

# === ORE INLET FEED HOOD (east side, catches belt v4 discharge ~x96-99) ===
# controller resolves mid-point by these exact names:
box("Dark_Recessed_Ore_Inlet",(CX+R-0.6,SHELL_TOP-0.4,CZ),(2.4,1.4,3.0),DARK(),bevel=0.05,uv=1.0)
aligned_box("Steel_Discharge_Chute_Into_Inlet",(99.6,8.6,55.0),(CX+R-1.2,7.4,55.0),3.0,0.4,BRUSHED(),bevel=0.05,uv=1.8)
box("L3_SlurryTank_FeedHood",(CX+R+0.4,8.6,CZ),(2.2,1.6,3.6),BRUSHED(),bevel=0.05,uv=1.4)
box("L3_SlurryTank_FeedHood_Lip",(CX+R-1.4,7.2,CZ),(0.6,0.25,3.0),DARK(),bevel=0.03,uv=0.9)

# WATER INLET pipe (from water tank east) into tank top
tube("L3_SlurryTank_WaterInlet_Run",(98.0,8.4,62.0),0.34,9.0,'x',BLUE(),uv=1.6)
tube("L3_SlurryTank_WaterInlet_Drop",(CX+R-1.0,7.6,62.0),0.34,2.4,'y',BLUE(),uv=1.2)
cyl("L3_SlurryTank_WaterInlet_Flange",(CX+R-1.0,6.4,62.0),0.5,0.2,'y',BRUSHED(),uv=0.6,verts=20)

# SLURRY OUTLET pipe (bottom, -z side, to pump/preheater)
tube("L3_SlurryTank_Outlet_Drop",(CX,1.0,CZ-R+0.6),0.5,1.8,'y',BRUSHED(),uv=1.0)
tube("L3_SlurryTank_Outlet_Run",(CX,0.6,CZ-R-3.0),0.5,7.0,'z',BRUSHED(),uv=1.6)
# valve handwheel on outlet
cyl("L3_SlurryTank_Outlet_ValveBody",(CX,0.6,CZ-R-1.2),0.6,0.8,'z',BLUE(),uv=0.8,verts=24)
cyl("L3_SlurryTank_Outlet_Handwheel",(CX+0.9,0.6,CZ-R-1.2),0.55,0.12,'x',YEL(),uv=0.7,verts=24)

# OVERFLOW WEIR launder ring near top (outer trough)
for i in range(0,NSEG,1):
    a=2*math.pi*i/NSEG; px=CX+math.cos(a)*(R+0.55); pz=CZ+math.sin(a)*(R+0.55)
    box("L3_SlurryTank_Weir_%02d"%i,(px,SHELL_TOP-0.2,pz),(1.4,0.5,0.12),DARK(),bevel=0.02,uv=1.0)

# pH/level sensor + cable tray + panel + label
box("L3_SlurryTank_pHProbe_Head",(CX-2.0,SHELL_TOP+0.6,CZ+R-1.0),(0.4,0.7,0.4),BLUE(),bevel=0.04,uv=0.6)
tube("L3_SlurryTank_pHProbe_Shaft",(CX-2.0,SHELL_TOP-1.5,CZ+R-1.0),0.08,4.0,'y',BRUSHED(),uv=0.8,verts=14)
aligned_box("L3_SlurryTank_CableTray",(CX-R-0.4,1.2,CZ+2.0),(CX-R-0.4,SHELL_TOP,CZ+2.0),0.35,0.12,DARK(),bevel=0.02,uv=2.0)
box("L3_SlurryTank_ControlPanel",(CX-R-1.2,2.2,CZ+4.5),(1.6,2.2,0.5),BLUE(),bevel=0.05,uv=1.2)
box("L3_SlurryTank_NamePlate",(CX,4.6,CZ+R+0.06),(3.4,1.1,0.12),DARK(),bevel=0.02,uv=0.9)

# ================= WATER TANK (vertical) center (104,0,69) =================
WX,WZ = 104.0, 69.0; WR=3.4; WB=0.4; WT=14.5; WH=WT-WB
cyl("L3_WaterTank_Plinth",(WX,WB/2,WZ), WR+0.7, WB,'y', CONC(), uv=2.0, verts=40)
cyl("Water_tank",(WX,(WB+WT)/2,WZ), WR, WH,'y', WATERMAT(), bevel=0.05, uv=2.2, verts=44)
cyl("L3_WaterTank_DomeTop",(WX,WT+0.5,WZ), WR, 1.1,'y', WATERMAT(), cone_r2=0.2, uv=1.6, verts=44)
for i,y in enumerate([4.0,8.0,12.0]):
    cyl("L3_WaterTank_Band_%02d"%i,(WX,y,WZ), WR+0.05, 0.2,'y', BRUSHED(), bevel=0.02, uv=1.0, verts=44)
cyl("L3_WaterTank_BlueBand",(WX,1.2,WZ), WR+0.07, 0.6,'y', BLUE(), bevel=0.02, uv=1.0, verts=44)
# ladder
for i in range(15):
    box("L3_WaterTank_Rung_%02d"%i,(WX-WR-0.45,0.8+i*0.85,WZ),(0.05,0.05,0.55),BRUSHED(),bevel=0.01,uv=0.5)
box("L3_WaterTank_RailA",(WX-WR-0.45,WT/2,WZ-0.28),(0.07,WT,0.07),BRUSHED(),bevel=0.02,uv=1.0)
box("L3_WaterTank_RailB",(WX-WR-0.45,WT/2,WZ+0.28),(0.07,WT,0.07),BRUSHED(),bevel=0.02,uv=1.0)
# top platform + handrail
for i in range(12):
    a=2*math.pi*i/12; px=WX+math.cos(a)*(WR+0.7); pz=WZ+math.sin(a)*(WR+0.7)
    box("L3_WaterTank_Plat_%02d"%i,(px,WT+0.05,pz),(1.6,0.1,1.2),BRUSHED(),bevel=0.02,uv=1.4)
    box("L3_WaterTank_RailPost_%02d"%i,(WX+math.cos(a)*(WR+1.3),WT+0.55,WZ+math.sin(a)*(WR+1.3)),(0.07,1.0,0.07),YEL(),bevel=0.012,uv=0.8)
# water outlet pipe toward slurry tank (west)
tube("L3_WaterTank_Outlet_Run",(WX-WR-3.5,8.4,WZ),0.34,7.0,'x',BLUE(),uv=1.6)
tube("L3_WaterTank_Outlet_Drop",(WX,8.4,WZ),0.34,0.6,'x',BLUE(),uv=0.8)
box("L3_WaterTank_NamePlate",(WX,7.0,WZ-WR-0.05),(2.2,1.0,0.1),BLUE(),bevel=0.02,uv=0.9)

bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_ALL', global_scale=1.0, bake_space_transform=True,
    axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE', use_mesh_modifiers=True,
    path_mode='COPY', embed_textures=False)
print("OLIVIA_SLURRYTANK_V2_EXPORT_OK objects=%d" % len(bpy.data.objects))
