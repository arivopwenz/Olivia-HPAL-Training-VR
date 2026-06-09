# -*- coding: utf-8 -*-
# OLIVIA HPAL VR - Tangki Netralisasi Tailing (Level 11) high-fidelity industrial
# Blender HEADLESS:  blender.exe --background --python build_tailing_neut_tank.py
#
# Konvensi: authored LOCAL ORIGIN, Z-up (Blender). Center axis X=0,Y=0.
#   Blender Z = Unity world Y (saat instance ditaruh y=0).
#   Export FBX_SCALE_ALL + bake_space_transform, axis_up=Y, axis_forward=-Z
#   -> instance Unity scale 1, berdiri tegak. Ditaruh di world (39.08, 0, 142.83).
#
# Tank realistis HPAL: agitated neutralization tank (CaCO3/Ca(OH)2) berpengaduk,
# shell semi-transparan (di-override di Unity) biar liquid kelihatan, drive bridge
# + motor + gearbox + impeller (agitator root pivot di center axis utk diputar
# controller), reagent/lime lance, pH probe, inlet/outlet nozzle berflange,
# overflow launder, tangga + cage, handrail platform, hazard band, label plate,
# concrete plinth + anchor bolt.

import bpy, math
from mathutils import Vector

# ----------------------------------------------------------------- bersihkan scene
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for blk in (bpy.data.meshes, bpy.data.materials, bpy.data.objects):
    for d in list(blk):
        try: blk.remove(d)
        except Exception: pass

TAU = math.pi * 2.0

# ----------------------------------------------------------------- material helper
_mats = {}
def mat(name, rgba, metal=0.85, rough=0.4, emis=None):
    if name in _mats: return _mats[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = rgba
        try: bsdf.inputs["Metallic"].default_value = metal
        except Exception: pass
        try: bsdf.inputs["Roughness"].default_value = rough
        except Exception: pass
        if emis is not None:
            try:
                bsdf.inputs["Emission Color"].default_value = emis
                bsdf.inputs["Emission Strength"].default_value = 1.0
            except Exception: pass
    _mats[name] = m
    return m

M_STEEL   = ("Steel",      (0.55,0.57,0.60,1), 0.90, 0.34)
M_SHELL   = ("Shell_Glass",(0.62,0.72,0.82,1), 0.30, 0.12)   # dioverride transparan di Unity
M_DARK    = ("DarkSteel",  (0.20,0.22,0.25,1), 0.85, 0.45)
M_YELLOW  = ("SafetyYellow",(0.86,0.62,0.06,1),0.30, 0.55)
M_HAZARD  = ("Hazard",     (0.90,0.40,0.04,1), 0.30, 0.55)
M_CONC    = ("Concrete",   (0.52,0.51,0.48,1), 0.00, 0.92)
M_BLUE    = ("MotorBlue",  (0.09,0.27,0.55,1), 0.60, 0.45)
M_PIPE    = ("PipeSteel",  (0.50,0.53,0.57,1), 0.85, 0.38)
M_GRATE   = ("Grating",    (0.29,0.31,0.33,1), 0.70, 0.55)
M_LABEL   = ("LabelPlate", (0.92,0.90,0.85,1), 0.10, 0.70)
M_GREEN   = ("StatusGreen",(0.10,0.70,0.22,1), 0.20, 0.45, (0.05,0.55,0.12,1))

_objs = []
def reg(o): _objs.append(o); return o

def _setmat(o, mdef):
    m = mat(*mdef)
    o.data.materials.clear(); o.data.materials.append(m)

def cyl(name, mdef, loc, radius, depth, verts=48, caps=True, rot=(0,0,0)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth,
        location=loc, end_fill_type='NGON' if caps else 'NOTHING')
    o = bpy.context.active_object; o.name = name
    o.rotation_euler = rot
    _setmat(o, mdef); return reg(o)

def box(name, mdef, loc, size, rot=(0,0,0)):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc)
    o = bpy.context.active_object; o.name = name
    o.scale = (size[0]*0.5, size[1]*0.5, size[2]*0.5)
    o.rotation_euler = rot
    _setmat(o, mdef); return reg(o)

def cone(name, mdef, loc, r1, r2, depth, verts=48, rot=(0,0,0)):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r2,
        depth=depth, location=loc)
    o = bpy.context.active_object; o.name = name
    o.rotation_euler = rot
    _setmat(o, mdef); return reg(o)

def torus(name, mdef, loc, major, minor, rot=(0,0,0)):
    bpy.ops.mesh.primitive_torus_add(location=loc, major_radius=major, minor_radius=minor,
        major_segments=48, minor_segments=12)
    o = bpy.context.active_object; o.name = name
    o.rotation_euler = rot
    _setmat(o, mdef); return reg(o)

# ================================================================= DIMENSI (Z=up)
Ri   = 2.85      # radius dalam
Rsh  = 2.90      # radius shell
PAD_T= 0.15
FLOOR_Z = 1.30   # lantai dalam (liquid mulai naik dari sini)
WALL_TOP= 6.60   # bibir atas shell
RUN_TOP = 7.60   # top handrail

# ----------------------------------------------------------------- pad / plinth beton
cyl("TNT_Concrete_Plinth", M_CONC, (0,0,PAD_T*0.5), 3.55, PAD_T, verts=56)
cyl("TNT_Pedestal_Ring",   M_DARK, (0,0,(FLOOR_Z)*0.5+PAD_T*0.5), 2.72, FLOOR_Z-PAD_T, verts=56, caps=False)
# anchor bolt di sekeliling base
for i in range(8):
    a = i/8.0*TAU
    box("TNT_Anchor_%02d"%i, M_DARK, (math.cos(a)*3.25, math.sin(a)*3.25, PAD_T+0.06),
        (0.22,0.22,0.16))

# ----------------------------------------------------------------- dished bottom + lantai
cone("TNT_DishedBottom", M_STEEL, (0,0,FLOOR_Z-0.18), Ri+0.05, 0.55, 0.55, verts=56)
cyl("TNT_Floor", M_STEEL, (0,0,FLOOR_Z-0.02), Ri, 0.06, verts=56)

# ----------------------------------------------------------------- SHELL semi-transparan
shell = cyl("TNT_Shell_Glass", M_SHELL, (0,0,(FLOOR_Z+WALL_TOP)*0.5),
            Rsh, WALL_TOP-FLOOR_Z, verts=64, caps=False)
# weld stiffener ring
for z in (2.20, 3.40, 4.60, 5.80):
    torus("TNT_WeldBand_%d"%int(z*10), M_STEEL, (0,0,z), Rsh+0.01, 0.05)
# hazard band bawah + atas
torus("TNT_HazardBand_Low",  M_HAZARD, (0,0,1.55), Rsh+0.02, 0.10)
torus("TNT_HazardBand_High", M_HAZARD, (0,0,6.35), Rsh+0.02, 0.10)
# top curb ring
torus("TNT_TopCurb", M_STEEL, (0,0,WALL_TOP), Rsh+0.02, 0.09)

# ----------------------------------------------------------------- overflow launder (ring trough) + pipe
torus("TNT_Launder_Outer", M_STEEL, (0,0,6.05), Rsh+0.28, 0.14)
cyl("TNT_OverflowPipe", M_PIPE, (Rsh+0.28, 0, 4.4), 0.16, 3.4, verts=20)
cyl("TNT_OverflowElbow", M_PIPE, (Rsh+0.28, 0, 6.0), 0.18, 0.5, verts=20, rot=(0,math.radians(90),0))

# ----------------------------------------------------------------- inlet nozzle (tailing feed dari CCD underflow)
ang_in = math.radians(210)
ix, iy = math.cos(ang_in)*Rsh, math.sin(ang_in)*Rsh
cyl("TNT_Inlet_Pipe", M_PIPE, (ix*1.45, iy*1.45, 1.95), 0.22, 1.6, verts=24,
    rot=(0, math.radians(90), ang_in))
cyl("TNT_Inlet_Flange", M_DARK, (ix*1.05, iy*1.05, 1.95), 0.34, 0.12, verts=24,
    rot=(0, math.radians(90), ang_in))

# ----------------------------------------------------------------- sludge / underflow outlet (bawah)
cyl("TNT_Sludge_Outlet", M_PIPE, (0, 1.2, 0.55), 0.20, 1.4, verts=22, rot=(math.radians(90),0,0))
cyl("TNT_Sludge_Flange", M_DARK, (0, 1.95, 0.55), 0.30, 0.12, verts=22, rot=(math.radians(90),0,0))

# ----------------------------------------------------------------- DRIVE BRIDGE + motor + gearbox
box("TNT_DriveBridge_A", M_STEEL, (0, 0.42, WALL_TOP+0.18), (Rsh*2+0.6, 0.26, 0.30))
box("TNT_DriveBridge_B", M_STEEL, (0,-0.42, WALL_TOP+0.18), (Rsh*2+0.6, 0.26, 0.30))
box("TNT_DriveBridge_Deck", M_GRATE, (0,0,WALL_TOP+0.34), (1.5,1.3,0.06))
cyl("TNT_Gearbox", M_DARK, (0,0,WALL_TOP+0.70), 0.45, 0.6, verts=24)
box("TNT_Motor", M_BLUE, (0.0,0.0,WALL_TOP+1.35), (0.7,0.7,1.1))
cyl("TNT_Motor_Fan", M_DARK, (0,0,WALL_TOP+1.95), 0.30, 0.18, verts=20)

# ----------------------------------------------------------------- AGITATOR (flat -> rig; pivot dibikin di Unity)
# CATATAN: JANGAN parent agitator ke empty bersarang di Blender -> bake_space_transform
# tidak konversi axis utk subtree empty (shaft jadi rebah di Z). Jadikan child rig (flat),
# lalu di Unity di-group ke pivot "TNT_Agitator_Root" @ center axis utk diputar controller.
cyl("TNT_Agitator_Shaft", M_STEEL, (0,0,3.6), 0.13, 6.0, verts=20)   # dari gearbox turun ke impeller
# tier impeller (pitched blade turbine) - 2 tingkat
for tier, zc, br in ((0, 2.05, 1.05), (1, 3.15, 1.05)):
    cyl("TNT_Impeller_Hub_%d"%tier, M_DARK, (0,0,zc), 0.22, 0.30, verts=18)
    for b in range(4):
        a = b/4.0*TAU
        box("TNT_Impeller_Blade_%d_%d"%(tier,b), M_STEEL,
            (math.cos(a)*br*0.6, math.sin(a)*br*0.6, zc),
            (br, 0.06, 0.34), rot=(0, math.radians(22), a))

# ----------------------------------------------------------------- REAGENT / LIME lance (susu kapur dari atas)
lx = 1.45
cyl("TNT_Lime_Lance", M_PIPE, (lx,0,5.3), 0.12, 2.8, verts=18)
cyl("TNT_Lime_Header", M_PIPE, (lx-0.5,0,WALL_TOP+0.05), 0.13, 1.1, verts=18, rot=(0,math.radians(90),0))
cyl("TNT_Lime_Lance_Head", M_DARK, (lx,0,3.95), 0.18, 0.28, verts=18)

# ----------------------------------------------------------------- pH probe + gauge box (sisi)
px, py = -1.55, 0.85
cyl("TNT_pH_Probe", M_DARK, (px,py,4.0), 0.07, 5.4, verts=14)
box("TNT_pH_GaugeBox", M_DARK, (px-0.1, py, 5.6), (0.5,0.4,0.5))
cyl("TNT_pH_GaugeFace", M_LABEL, (px-0.36, py, 5.6), 0.20, 0.05, verts=24, rot=(0,math.radians(90),0))
box("TNT_pH_StatusLamp", M_GREEN, (px-0.1, py+0.30, 5.95), (0.14,0.14,0.10))

# ----------------------------------------------------------------- LADDER + safety cage (sisi +X)
ladx = Rsh + 0.18
box("TNT_Ladder_RailL", M_STEEL, (ladx, 0.26, (0.4+WALL_TOP)*0.5), (0.06,0.06,WALL_TOP-0.3))
box("TNT_Ladder_RailR", M_STEEL, (ladx,-0.26, (0.4+WALL_TOP)*0.5), (0.06,0.06,WALL_TOP-0.3))
z = 0.6
ri = 0
while z < WALL_TOP-0.1:
    box("TNT_Ladder_Rung_%02d"%ri, M_STEEL, (ladx, 0.0, z), (0.06,0.62,0.06)); z += 0.42; ri += 1
# cage hoops
for hz in (2.0, 3.2, 4.4, 5.6):
    torus("TNT_Ladder_Cage_%d"%int(hz*10), M_STEEL, (ladx+0.05,0,hz), 0.42, 0.03,
          rot=(0, math.radians(90), 0))

# ----------------------------------------------------------------- HANDRAIL platform around top
torus("TNT_Platform_Grate", M_GRATE, (0,0,WALL_TOP+0.02), Rsh+0.45, 0.22)
np = 10
for i in range(np):
    a = i/float(np)*TAU
    box("TNT_Rail_Post_%02d"%i, M_STEEL, (math.cos(a)*(Rsh+0.55), math.sin(a)*(Rsh+0.55),
        (WALL_TOP+RUN_TOP)*0.5), (0.06,0.06,RUN_TOP-WALL_TOP))
torus("TNT_TopRail", M_YELLOW, (0,0,RUN_TOP), Rsh+0.55, 0.05)
torus("TNT_MidRail", M_YELLOW, (0,0,(WALL_TOP+RUN_TOP)*0.5+0.1), Rsh+0.55, 0.04)

# ----------------------------------------------------------------- LABEL plate (menghadap +X+Y -> arah player)
ang_lbl = math.radians(35)
lxp, lyp = math.cos(ang_lbl)*Rsh, math.sin(ang_lbl)*Rsh
box("TNT_LabelPlate", M_LABEL, (lxp*1.02, lyp*1.02, 4.6), (0.06,2.4,0.9), rot=(0,0,ang_lbl))

# ================================================================= PARENT ke empty rig
bpy.ops.object.empty_add(type='PLAIN_AXES', location=(0,0,0))
rig = bpy.context.active_object; rig.name = "TailingNeutTank_IndustrialRig"
for o in _objs:
    if o.parent is None:
        o.parent = rig

# ================================================================= EXPORT FBX
import os
out_dir = os.path.dirname(bpy.data.filepath) if bpy.data.filepath else os.getcwd()
# argv path absolut diberikan dari pemanggil
out_fbx = r"C:\Users\mp2dz\Olivia\Assets\Art\TailingNeutTankBlender\TailingNeutTank_IndustrialUV.fbx"

bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(
    filepath=out_fbx,
    use_selection=True,
    apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_ALL',
    bake_space_transform=True,
    object_types={'EMPTY','MESH'},
    mesh_smooth_type='FACE',
    axis_forward='-Z',
    axis_up='Y',
)
print("OLIVIA_TAILING_TANK_OK ->", out_fbx)
