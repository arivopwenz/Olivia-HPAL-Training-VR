import bpy, bmesh, math, random, os
from mathutils import Vector

random.seed(303)
A = r"C:\Users\mp2dz\Olivia\Assets"
TEX = os.path.join(A, "Art", "FlashCCDIndustrialUVRedesign", "Textures")
OUT = os.path.join(A, "Art", "Level3SlurryWaterTankBlender", "Level3_SlurryWaterTank_IndustrialUV_v2.fbx")

# ---- Unity-world -> Blender coord (proven: instance IDENTITY lands at world coords) ----
def u2b(ux, uy, uz): return Vector((-ux, -uz, uy))
def dsz(sx, sy, sz): return (sx, sz, sy)

bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete()
for blk in (bpy.data.meshes, bpy.data.materials, bpy.data.images):
    for b in list(blk):
        try: blk.remove(b)
        except Exception: pass

# ============================= MATERIALS (rich, non-monotone palette) =============================
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
        bump = nt.nodes.new('ShaderNodeBump'); bump.inputs['Strength'].default_value = 0.16
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

S316  = lambda: mat("M_L3_SlurryShell_316L", "UV_AcidTank_316L_OffWhite.png", 0.42, 0.72)
STEEL = lambda: mat("M_L3_BrushedSteel", "UV_BrushedSteel_Grey.png", 0.5, 0.85)
DARKSTL = lambda: mat("M_L3_DarkSteel", "UV_DarkRubber_Gasket.png", 0.55, 0.55)
RUBBER= lambda: mat("M_L3_Rubber_Gasket", "UV_DarkRubber_Gasket.png", 0.85, 0.0)
HAZ   = lambda: mat("M_L3_Hazard", "UV_Hazard_BlackYellow.png", 0.55, 0.2)
YEL   = lambda: mat("M_L3_SafetyYellow", "UV_SafetyYellow_Rails.png", 0.55, 0.2)
GREEN = lambda: mat("M_L3_SafetyGreen", "UV_SafetyGreen.png", 0.5, 0.2)
CONC  = lambda: mat("M_L3_Concrete", "UV_AcidResistantConcrete.png", 0.93, 0.0)
SLURRY= lambda: mat("M_L3_Slurry_Brown", "UV_ThickUnderflow_BrownPurple.png", 0.6, 0.0, emis=0.18, ecol=(0.28,0.2,0.12))
BLUE  = lambda: mat("M_L3_Process_Blue", "UV_ChemicalPump_Blue.png", 0.42, 0.45)
ORANGE= lambda: mat("M_L3_OrangeBand", "UV_OrangePressureBand.png", 0.5, 0.3)
RED   = lambda: mat("M_L3_Emergency", "UV_EmergencyRed.png", 0.5, 0.2, emis=1.8, ecol=(0.9,0.05,0.05))
GLASS = lambda: mat("M_L3_SightGlass", "UV_TransparentGlass_AcidRated.png", 0.15, 0.1, emis=0.25, ecol=(0.3,0.55,0.85))
WATER = lambda: mat("M_L3_WaterBlue", "UV_ChemicalPump_Blue.png", 0.2, 0.1, emis=0.2, ecol=(0.2,0.45,0.7))
GAUGE = lambda: mat("M_L3_GaugeFace", "UV_RecoveredSteam_White.png", 0.35, 0.1)

# ============================= GEOMETRY HELPERS =============================
def finalize(o, bevel=0.022, seg=2, uv=1.4, smooth=42.0):
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

def box(name, upos, usize, m, bevel=0.022, seg=2, uv=1.4):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=u2b(*upos))
    o = bpy.context.active_object; o.name = name
    sx, sy, sz = dsz(*usize); o.scale = (sx, sy, sz)
    bpy.ops.object.transform_apply(scale=True); setmat(o, m); finalize(o, bevel, seg, uv); return o

def cyl(name, upos, radius, length, axis, m, bevel=0.018, uv=1.2, verts=28):
    # axis 'y' -> vertical (Unity up); 'x' -> Unity X; 'z' -> Unity Z
    bpy.ops.mesh.primitive_cylinder_add(radius=radius, depth=length, vertices=verts, location=u2b(*upos))
    o = bpy.context.active_object; o.name = name
    if axis=='x': o.rotation_euler=(0, math.radians(90), 0)
    elif axis=='z': o.rotation_euler=(math.radians(90), 0, 0)
    bpy.ops.object.transform_apply(rotation=True); setmat(o, m); finalize(o, bevel, 1, uv, 52); return o

def open_tube(name, upos, radius, length, m, thick=0.16, uv=2.4, verts=48):
    # OPEN-TOP (and open-bottom) thick-walled vertical shell -> interior visible from above
    bpy.ops.mesh.primitive_cylinder_add(radius=radius, depth=length, vertices=verts,
        location=u2b(*upos), end_fill_type='NOTHING')
    o = bpy.context.active_object; o.name = name; setmat(o, m)
    bpy.context.view_layer.objects.active = o
    try:
        md = o.modifiers.new('sol','SOLIDIFY'); md.thickness=thick; md.offset=0.0; md.use_rim=True
        bpy.ops.object.modifier_apply(modifier=md.name)
    except Exception: pass
    try:
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.cube_project(cube_size=uv); bpy.ops.object.mode_set(mode='OBJECT')
    except Exception:
        try: bpy.ops.object.mode_set(mode='OBJECT')
        except Exception: pass
    try: bpy.ops.object.shade_auto_smooth(angle=math.radians(52))
    except Exception:
        try: bpy.ops.object.shade_smooth()
        except Exception: pass
    o.select_set(False); return o

def cone(name, upos, r1, r2, length, axis, m, uv=1.1, verts=30):
    bpy.ops.mesh.primitive_cone_add(radius1=r1, radius2=r2, depth=length, vertices=verts, location=u2b(*upos))
    o = bpy.context.active_object; o.name = name
    if axis=='x': o.rotation_euler=(0, math.radians(90), 0)
    elif axis=='z': o.rotation_euler=(math.radians(90), 0, 0)
    bpy.ops.object.transform_apply(rotation=True); setmat(o, m); finalize(o, 0.01, 1, uv, 55); return o

def torus(name, upos, R, r, m, uv=0.9):
    bpy.ops.mesh.primitive_torus_add(location=u2b(*upos), major_radius=R, minor_radius=r,
                                     major_segments=40, minor_segments=12)
    o = bpy.context.active_object; o.name = name
    setmat(o, m)
    try: bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.uv.cube_project(cube_size=uv); bpy.ops.object.mode_set(mode='OBJECT')
    except Exception:
        try: bpy.ops.object.mode_set(mode='OBJECT')
        except Exception: pass
    try: bpy.ops.object.shade_smooth()
    except Exception: pass
    return o

def aligned_box(name, ua, ub, width, thick, m, bevel=0.02, uv=1.6):
    A_=Vector(ua); B_=Vector(ub); L=(B_-A_); length=max(0.01,L.length); L=L.normalized()
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

def bolt_ring(prefix, cx, cz, y, radius, count, m, axis='y', face_x=None):
    # ring of small bolts around a circular feature on a vertical wall (axis='wall' uses a plane facing -X/+X)
    for i in range(count):
        a = 2*math.pi*i/count
        if axis=='wall':  # bolts on a vertical disk facing +/-X, ring in (y,z)
            yy = y + radius*math.sin(a); zz = cz + radius*math.cos(a)
            cyl("%s_Bolt_%02d"%(prefix,i), (face_x, yy, zz), 0.05, 0.12, 'x', STEEL(), uv=0.6, verts=8)
        else:  # bolts on a horizontal ring (top), ring in (x,z)
            xx = cx + radius*math.cos(a); zz = cz + radius*math.sin(a)
            cyl("%s_Bolt_%02d"%(prefix,i), (xx, y, zz), 0.05, 0.12, 'y', STEEL(), uv=0.6, verts=8)

def rail_ring(prefix, cx, cz, radius, y_base, count, segs_drop=True):
    # yellow safety rail ring (posts + top + mid)
    pts=[]
    for i in range(count):
        a=2*math.pi*i/count
        pts.append((cx+radius*math.cos(a), cz+radius*math.sin(a)))
        box("%s_Post_%02d"%(prefix,i),(pts[-1][0],y_base+0.55,pts[-1][1]),(0.07,1.1,0.07),YEL(),bevel=0.012,uv=1.0)
    for lvl,h in (("Top",1.05),("Mid",0.55)):
        for i in range(count):
            a=pts[i]; b=pts[(i+1)%count]
            aligned_box("%s_%sRail_%02d"%(prefix,lvl,i),(a[0],y_base+h,a[1]),(b[0],y_base+h,b[1]),0.05,0.05,YEL(),bevel=0.01,uv=1.8)

def cage_ladder(prefix, x, z, y0, y1, face='-x'):
    # vertical access ladder w/ safety cage hoops
    n=int((y1-y0)/0.55)
    for i in range(n):
        yy=y0+0.55*i
        box("%s_Rung_%02d"%(prefix,i),(x,yy,z),(0.5,0.05,0.05),STEEL(),bevel=0.01,uv=0.8)
    box("%s_RailL"%prefix,(x,(y0+y1)/2,z+0.26),(0.05,y1-y0,0.05),STEEL(),bevel=0.01,uv=1.4)
    box("%s_RailR"%prefix,(x,(y0+y1)/2,z-0.26),(0.05,y1-y0,0.05),STEEL(),bevel=0.01,uv=1.4)
    dx = -0.55 if face=='-x' else 0.55
    nh=int((y1-y0)/0.85)
    for i in range(nh):
        yy=y0+0.9+0.85*i
        if yy>y1: break
        torus("%s_CageHoop_%02d"%(prefix,i),(x+dx*0.55,yy,z),0.42,0.035,STEEL())

OBJ=[]  # track for count

# ===================================================================================
# =============================== SLURRY TANK =======================================
# ===================================================================================
SCX, SCZ = 91.41, 55.14
SR = 6.5            # shell radius
SR_RIB = 6.62
S_BASE = 0.30
S_RIM  = 7.85
S_H = S_RIM - S_BASE

# concrete plinth ring + foundation pad
cyl("L3_SlurryTank_ConcretePlinth",(SCX,-0.35,SCZ),SR+0.9,0.7,'y',CONC(),uv=2.6,verts=44)
# support legs + footpads (radius 6.7, 8 around) + bottom reinforcement ring
for i in range(8):
    a=2*math.pi*i/8; lx=SCX+6.55*math.cos(a); lz=SCZ+6.55*math.sin(a)
    box("L3_SlurryTank_ShortLeg_%02d"%i,(lx,0.17,lz),(0.6,1.0,0.6),STEEL(),bevel=0.03,uv=1.0)
    box("L3_SlurryTank_FootPad_%02d"%i,(lx,-0.5,lz),(1.1,0.22,1.1),CONC(),bevel=0.03,uv=1.2)
torus("L3_SlurryTank_BottomReinforcementRing",(SCX,S_BASE+0.05,SCZ),SR+0.05,0.18,STEEL())

# dished/sloped conical bottom (solids suspension) -> narrow to center outlet
cone("L3_SlurryTank_SlopedBottomDisk",(SCX,S_BASE-0.55,SCZ),0.55,SR,1.1,'y',SLURRY(),uv=2.2,verts=44)
# main cylindrical shell (316L) - TRULY open top (thick-walled tube, interior visible)
open_tube("L3_SlurryTank_OpenShell_SmoothSteel",(SCX,S_BASE+S_H/2,SCZ),SR,S_H,S316(),thick=0.16,uv=2.4,verts=48)
# heavy top rim pipe
torus("L3_SlurryTank_HeavyTopRimPipe",(SCX,S_RIM,SCZ),SR+0.02,0.22,STEEL())
# painted bands: orange pressure band (low) + hazard band (high) + 2 weld bands
cyl("L3_SlurryTank_OrangeBand",(SCX,1.55,SCZ),SR+0.06,0.7,'y',ORANGE(),uv=3.0,verts=48)
cyl("L3_SlurryTank_HazardBand",(SCX,6.95,SCZ),SR+0.06,0.55,'y',HAZ(),uv=3.4,verts=48)
for wy in (3.4,5.3):
    torus("L3_SlurryTank_WeldBand_%d"%int(wy*10),(SCX,wy,SCZ),SR+0.03,0.05,STEEL())
# vertical reinforcement ribs (18)
NRIB=18
for i in range(NRIB):
    a=2*math.pi*i/NRIB; rx=SCX+SR_RIB*math.cos(a); rz=SCZ+SR_RIB*math.sin(a)
    box("L3_SlurryTank_VerticalRib_%02d"%i,(rx,S_BASE+S_H/2,rz),(0.16,S_H-0.4,0.16),STEEL(),bevel=0.02,uv=1.6)

# ---- low resting slurry pool (center stays OPEN so impeller is visible from the open top) ----
cyl("L3_SlurryTank_RestingSlurryPool",(SCX,1.05,SCZ),SR-0.22,1.5,'y',SLURRY(),uv=2.0,verts=46)

# ---- AGITATOR drive (bridge + motor + gearbox + vertical shaft + hub) ----
aligned_box("L3_SlurryTank_AgitatorBridge_PrimaryBeam",(SCX-SR-0.2,8.85,SCZ),(SCX+SR+0.2,8.85,SCZ),0.55,0.45,STEEL(),uv=2.4)
aligned_box("L3_SlurryTank_AgitatorBridge_CrossBrace",(SCX,8.9,SCZ-SR-0.2),(SCX,8.9,SCZ+SR+0.2),0.3,0.22,STEEL(),uv=2.4)
box("L3_SlurryTank_AgitatorGearbox_Rounded",(SCX,9.95,SCZ),(1.5,1.3,1.5),DARKSTL(),bevel=0.12,seg=3,uv=1.2)
cyl("L3_SlurryTank_AgitatorMotor_Horizontal",(SCX,10.05,SCZ-1.2),0.55,1.6,'z',BLUE(),uv=1.1,verts=24)
cyl("L3_SlurryTank_AgitatorMotor_EndCap",(SCX,10.05,SCZ-2.05),0.5,0.2,'z',STEEL(),uv=0.8,verts=24)
cyl("L3_SlurryTank_MotorFanGuard",(SCX,10.05,SCZ-0.45),0.42,0.18,'z',DARKSTL(),uv=0.8,verts=20)
# vertical drive column from gearbox DOWN to impeller (cosmetic static; C# marker stays authoritative for centering)
cyl("L3_SlurryTank_StirrerColumn_Static",(SCX,5.6,SCZ),0.16,7.2,'y',STEEL(),uv=1.2,verts=18)
cyl("L3_SlurryTank_StirrerHub_Static",(SCX,2.7,SCZ),0.36,0.8,'y',STEEL(),uv=0.9,verts=20)
# VISIBLE impeller: two tiers of radial paddle blades in the open center (the "pengaduk")
for _tier,_by in enumerate((2.6,3.45)):
    for _i in range(4):
        _a=2*math.pi*_i/4 + _tier*math.pi/4
        _ix=SCX+0.42*math.cos(_a); _iz=SCZ+0.42*math.sin(_a)
        _ox=SCX+2.25*math.cos(_a); _oz=SCZ+2.25*math.sin(_a)
        aligned_box("L3_SlurryTank_StirrerBlade_Static_%d_%d"%(_tier,_i),(_ix,_by,_iz),(_ox,_by,_oz),0.62,0.14,STEEL(),uv=1.2)

# ---- FRONT inspection manway + bolt ring + gasket + label plate (-X face, toward spawn) ----
SFX = SCX-SR-0.02   # front face x (~84.9)
cyl("L3_SlurryTank_FrontInspection_RoundManway",(SFX-0.05,4.05,SCZ),1.0,0.35,'x',DARKSTL(),uv=1.0,verts=30)
cyl("L3_SlurryTank_FrontInspection_DarkGasket",(SFX-0.22,4.05,SCZ),1.05,0.08,'x',RUBBER(),uv=0.8,verts=30)
bolt_ring("L3_SlurryTank_FrontInspection_Manway",SCX,SCZ,4.05,1.22,12,STEEL(),axis='wall',face_x=SFX-0.18)
box("L3_SlurryTank_FrontInspection_Label_Plate",(SFX-0.3,2.65,SCZ),(0.08,0.45,1.7),BLUE(),bevel=0.02,uv=1.0)
# big nameplate (SLURRY TANK) higher on shell
box("L3_SlurryTank_MainNameplate_Plate",(SFX-0.32,6.5,SCZ),(0.08,0.95,3.4),BLUE(),bevel=0.02,uv=1.2)
# level marker plates 25/50/75% (front)
for k,my in enumerate((2.2,4.1,5.9)):
    box("L3_SlurryTank_LevelMarker_%d_Plate"%k,(SFX-0.28,my,SCZ+3.05),(0.07,0.5,0.95),DARKSTL(),bevel=0.02,uv=0.9)
# pressure gauge (front)
cyl("L3_SlurryTank_PressureGauge_GaugeRim",(SFX-0.35,6.0,SCZ+4.45),0.5,0.18,'x',STEEL(),uv=0.7,verts=24)
cyl("L3_SlurryTank_PressureGauge_GaugeFace",(SFX-0.45,6.0,SCZ+4.45),0.42,0.04,'x',GAUGE(),uv=0.6,verts=24)
box("L3_SlurryTank_PressureGauge_GaugeNeedle",(SFX-0.5,6.05,SCZ+4.45),(0.04,0.34,0.04),RED(),bevel=0.005,uv=0.5)
# pH probe (top edge)
box("L3_SlurryTank_pHProbe_Head",(SCX-3.0,8.4,SCZ+3.0),(0.3,0.5,0.3),DARKSTL(),bevel=0.04,uv=0.8)
cyl("L3_SlurryTank_pHProbe_Shaft",(SCX-3.0,6.6,SCZ+3.0),0.06,3.4,'y',STEEL(),uv=1.0,verts=12)

# ---- ORE feed chute on +X/NE top rim (receives from crusher belt head ~99.8,9.2,55.4) ----
aligned_box("L3_SlurryTank_InclinedOreFeedChute",(99.0,8.6,55.3),(93.6,7.4,55.2),1.7,0.16,DARKSTL(),uv=2.2)
aligned_box("L3_SlurryTank_FeedChute_RubberLiner",(98.6,8.5,55.3),(93.9,7.45,55.2),1.4,0.06,RUBBER(),uv=1.8)
box("L3_SlurryTank_FeedHood",(98.8,9.3,55.3),(1.4,1.0,2.0),STEEL(),bevel=0.05,uv=1.4)
# dark recessed ore inlet recess (renamed: must NOT contain 'Discharge_Chute_Into_Inlet' to avoid baked-(0,0,0) ore-path resolve)
box("L3_SlurryTank_OreInletRecess_Plate",(93.2,7.35,55.2),(1.2,0.9,1.8),RUBBER(),bevel=0.05,uv=1.0)

# ---- WATER inlet downcomer into slurry tank top (from process water pipe) ----
cyl("L3_SlurryTank_WaterInlet_Downcomer",(89.0,6.7,61.4),0.28,2.2,'y',BLUE(),uv=1.2,verts=18)
torus("L3_SlurryTank_WaterInlet_Flange",(89.0,7.7,61.4),0.34,0.07,STEEL())

# ---- BOTTOM outlet -> pump (-Z), bottom nozzle + isolation valve (yellow handwheel) + saddle ----
cyl("L3_SlurryTank_BottomOutlet_Nozzle",(SCX,0.0,SCZ-3.0),0.4,1.4,'z',STEEL(),uv=1.0,verts=22)
# run from under tank south to pump area (z down to ~41)
aligned_box("L3_SlurryTank_OutletPipe_ToPump",(91.41,0.5,52.0),(93.57,1.4,41.0),0.7,0.7,STEEL(),uv=2.4)
torus("L3_SlurryTank_TankOutlet_Flange",(93.0,1.4,46.8),0.42,0.08,STEEL())
# isolation valve body + stem + yellow handwheel
cyl("L3_SlurryTank_OutletIsolationValve_ValveBody",(93.57,1.4,42.3),0.55,0.9,'z',DARKSTL(),uv=1.0,verts=22)
cyl("L3_SlurryTank_OutletIsolationValve_Stem",(93.57,2.4,42.3),0.07,1.6,'y',STEEL(),uv=0.8,verts=10)
torus("L3_SlurryTank_OutletIsolationValve_YellowHandwheel",(93.57,3.55,42.3),0.5,0.07,YEL())
for i in range(4):
    a=2*math.pi*i/4
    aligned_box("L3_SlurryTank_OutletValve_Spoke_%02d"%i,(93.57,3.55,42.3),(93.57+0.48*math.cos(a),3.55,42.3+0.48*math.sin(a)),0.05,0.05,YEL(),bevel=0.01,uv=0.6)
box("L3_SlurryTank_OutletPipeSupport_Base",(93.57,-0.45,39.7),(1.0,0.4,1.0),CONC(),bevel=0.04,uv=1.2)
box("L3_SlurryTank_OutletPipeSupport_Saddle",(93.57,0.35,39.7),(0.9,0.5,0.5),STEEL(),bevel=0.04,uv=1.0)

# ---- top platform grating + safety rails + service ladder ----
rail_ring("L3_SlurryTank_YellowSafetyRail",SCX,SCZ,SR+0.25,S_RIM+0.15,28)
cage_ladder("L3_SlurryTank_ServiceLadder",SFX-0.1,SCZ-4.6,0.3,8.0,face='-x')

# ===================================================================================
# =============================== WATER TANK ========================================
# ===================================================================================
WCX, WCZ = 91.70, 73.39
WR = 3.0
W_BASE = 0.30
W_TOP  = 8.5
W_H = W_TOP - W_BASE

cyl("L3_WaterTank_ConcretePlinth",(WCX,-0.35,WCZ),WR+0.7,0.7,'y',CONC(),uv=2.4,verts=40)
for i in range(6):
    a=2*math.pi*i/6; lx=WCX+(WR+0.05)*math.cos(a); lz=WCZ+(WR+0.05)*math.sin(a)
    box("L3_WaterTank_ShortLeg_%02d"%i,(lx,0.17,lz),(0.5,1.0,0.5),STEEL(),bevel=0.03,uv=1.0)
    box("L3_WaterTank_FootPad_%02d"%i,(lx,-0.5,lz),(0.9,0.22,0.9),CONC(),bevel=0.03,uv=1.2)
torus("L3_WaterTank_BottomRing",(WCX,W_BASE+0.05,WCZ),WR+0.05,0.15,STEEL())
# shell (painted steel) - closed top
cyl("L3_WaterTank_VerticalShell_SmoothSteel",(WCX,W_BASE+W_H/2,WCZ),WR,W_H,'y',STEEL(),bevel=0.05,uv=2.4,verts=40)
# shallow conical roof + roof rim + vent cap
cone("L3_WaterTank_ShallowConicalRoof",(WCX,W_TOP+0.45,WCZ),WR+0.05,0.3,0.9,'y',S316(),uv=2.0,verts=40)
torus("L3_WaterTank_RoofRim",(WCX,W_TOP,WCZ),WR+0.03,0.12,STEEL())
cyl("L3_WaterTank_TopVentCap",(WCX,W_TOP+1.05,WCZ),0.35,0.3,'y',DARKSTL(),uv=0.8,verts=18)
# BLUE identity band + weld band
cyl("L3_WaterTank_BlueBand",(WCX,3.0,WCZ),WR+0.06,0.9,'y',BLUE(),uv=2.6,verts=40)
torus("L3_WaterTank_WeldBand_57",(WCX,5.7,WCZ),WR+0.03,0.05,STEEL())
# vertical ribs (12)
for i in range(12):
    a=2*math.pi*i/12; rx=WCX+(WR+0.08)*math.cos(a); rz=WCZ+(WR+0.08)*math.sin(a)
    box("L3_WaterTank_VerticalRib_%02d"%i,(rx,W_BASE+W_H/2,rz),(0.13,W_H-0.4,0.13),STEEL(),bevel=0.02,uv=1.6)

# front (-X face) manway + bolt ring + gasket + label + MANHOLE plate
WFX = WCX-WR-0.02   # ~88.68
cyl("L3_WaterTank_FrontManway_RoundManway",(WFX-0.05,4.0,WCZ),0.85,0.32,'x',DARKSTL(),uv=1.0,verts=28)
cyl("L3_WaterTank_FrontManway_DarkGasket",(WFX-0.2,4.0,WCZ),0.9,0.07,'x',RUBBER(),uv=0.8,verts=28)
bolt_ring("L3_WaterTank_FrontManway",WCX,WCZ,4.0,1.05,10,STEEL(),axis='wall',face_x=WFX-0.16)
box("L3_WaterTank_FrontManway_Label_Plate",(WFX-0.27,2.7,WCZ),(0.07,0.4,1.2),BLUE(),bevel=0.02,uv=0.9)
box("L3_WaterTank_MainNameplate_Plate",(WFX-0.29,6.4,WCZ),(0.07,0.8,2.4),BLUE(),bevel=0.02,uv=1.1)
# blue sight glass tube + level ticks
cyl("L3_WaterTank_BlueSightGlass_Tube",(WFX-0.25,4.5,WCZ+1.5),0.12,5.0,'y',GLASS(),uv=1.0,verts=14)
for k in range(5):
    box("L3_WaterTank_SightGlass_LevelTick_%02d"%k,(WFX-0.35,1.8+1.35*k,WCZ+1.5),(0.04,0.05,0.4),STEEL(),bevel=0.005,uv=0.5)
# level gauge dial
cyl("L3_WaterTank_LevelGauge_GaugeRim",(WFX-0.3,6.5,WCZ-1.3),0.4,0.16,'x',STEEL(),uv=0.6,verts=22)
cyl("L3_WaterTank_LevelGauge_GaugeFace",(WFX-0.4,6.5,WCZ-1.3),0.33,0.04,'x',GAUGE(),uv=0.6,verts=22)
box("L3_WaterTank_LevelGauge_GaugeNeedle",(WFX-0.45,6.55,WCZ-1.3),(0.03,0.26,0.03),RED(),bevel=0.005,uv=0.4)
# outlet nozzle (toward slurry tank, -Z) + flange
cyl("L3_WaterTank_OutletNozzle",(89.0,3.0,WCZ-5.1),0.3,1.2,'z',BLUE(),uv=1.0,verts=20)
torus("L3_WaterTank_OutletFlange",(89.0,3.0,69.1),0.36,0.07,STEEL())
# rails + ladder
rail_ring("L3_WaterTank_TopSafetyRail",WCX,WCZ,WR+0.25,W_TOP+0.15,16)
cage_ladder("L3_WaterTank_AccessLadder",WFX-0.05,WCZ+2.0,0.3,8.5,face='-x')

# ===================================================================================
# ====================== PROCESS WATER PIPE (water tank -> slurry tank) =============
# ===================================================================================
# water tank outlet (89,3,68.3) -> up -> over -> control valve -> slurry inlet (89,6.3,61.4)
cyl("L3_ProcessWaterPipe_Segment_Riser",(89.0,4.8,68.4),0.26,3.8,'y',BLUE(),uv=1.2,verts=18)
torus("L3_ProcessWaterPipe_CleanElbow_Top",(89.0,6.7,68.4),0.26,0.1,STEEL())
aligned_box("L3_ProcessWaterPipe_Segment_Horizontal",(89.0,6.7,68.4),(89.0,6.7,61.4),0.5,0.5,BLUE(),uv=2.2)
torus("L3_ProcessWaterPipe_CleanElbow_Down",(89.0,6.7,61.4),0.26,0.1,STEEL())
# control valve + stem + yellow handwheel at z~65.6
cyl("L3_ProcessWaterPipe_ControlValve_ValveBody",(89.0,6.7,65.6),0.45,0.9,'z',DARKSTL(),uv=1.0,verts=22)
cyl("L3_ProcessWaterPipe_ControlValve_Stem",(89.0,7.8,65.6),0.06,2.0,'y',STEEL(),uv=0.8,verts=10)
torus("L3_ProcessWaterPipe_ControlValve_YellowHandwheel",(89.0,9.0,65.6),0.46,0.06,YEL())
for i in range(4):
    a=2*math.pi*i/4
    aligned_box("L3_ProcessWaterPipe_Valve_Spoke_%02d"%i,(89.0,9.0,65.6),(89.0+0.44*math.cos(a),9.0,65.6+0.44*math.sin(a)),0.045,0.045,YEL(),bevel=0.01,uv=0.6)
# support posts + feet
for z in (68.0,64.2):
    box("L3_ProcessWaterPipe_SupportPost_%d"%int(z),(89.0,3.4,z),(0.2,6.6,0.2),STEEL(),bevel=0.02,uv=1.6)
    box("L3_ProcessWaterPipe_SupportFoot_%d"%int(z),(89.0,-0.45,z),(0.6,0.3,0.6),CONC(),bevel=0.03,uv=1.0)

# ============================= EXPORT =============================
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_ALL', global_scale=1.0, bake_space_transform=True,
    axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE', use_mesh_modifiers=True,
    path_mode='COPY', embed_textures=False)
print("OLIVIA_SLURRYWATERTANK_V2_EXPORT_OK objects=%d" % len(bpy.data.objects))
