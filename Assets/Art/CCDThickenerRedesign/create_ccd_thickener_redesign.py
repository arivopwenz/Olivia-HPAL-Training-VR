import math
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(r"C:\Users\mp2dz\Olivia\Assets\Art\CCDThickenerRedesign")
BLEND_PATH = ROOT / "CCDThickener_Redesign.blend"
FBX_PATH = ROOT / "CCDThickener_Redesign.fbx"
PREVIEW_PATH = ROOT / "CCDThickener_Redesign_Preview.png"


def make_mat(name, color, metallic=0.0, roughness=0.65, alpha=1.0, emission=None):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = color
        bsdf.inputs["Alpha"].default_value = alpha
        bsdf.inputs["Metallic"].default_value = metallic
        bsdf.inputs["Roughness"].default_value = roughness
        if emission and "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = emission[0]
            bsdf.inputs["Emission Strength"].default_value = emission[1]
    mat.blend_method = "BLEND" if alpha < 1.0 else "OPAQUE"
    mat.use_screen_refraction = alpha < 1.0
    mat.show_transparent_back = True
    return mat


MAT_STEEL = make_mat("CCD_DarkGalvanizedSteel", (0.16, 0.18, 0.19, 1), metallic=0.45, roughness=0.48)
MAT_TANK = make_mat("CCD_TankGrey_Industrial", (0.46, 0.50, 0.49, 1), metallic=0.25, roughness=0.55)
MAT_EDGE = make_mat("CCD_DarkRubber_Gasket", (0.035, 0.04, 0.042, 1), roughness=0.78)
MAT_CLEAR_PLS = make_mat("CCD_ClearPLS_Overflow_GreenBlue", (0.20, 0.62, 0.58, 0.46), roughness=0.35, alpha=0.46, emission=((0.05, 0.25, 0.22, 1), 0.12))
MAT_SETTLING = make_mat("CCD_SettlingZone_TranslucentPurple", (0.42, 0.18, 0.55, 0.34), roughness=0.5, alpha=0.34, emission=((0.22, 0.06, 0.32, 1), 0.08))
MAT_UNDERFLOW = make_mat("CCD_ThickUnderflow_BrownPurple", (0.31, 0.17, 0.11, 0.88), roughness=0.85, alpha=0.88)
MAT_FLOC = make_mat("CCD_Flocculant_WhiteBlue", (0.78, 0.92, 1.0, 0.75), roughness=0.4, alpha=0.75, emission=((0.32, 0.55, 0.85, 1), 0.12))
MAT_ARROW_OVERFLOW = make_mat("CCD_Arrow_PLS_Overflow", (0.05, 0.86, 0.72, 1), emission=((0.0, 0.45, 0.34, 1), 0.35))
MAT_ARROW_UNDERFLOW = make_mat("CCD_Arrow_Underflow_Tailing", (0.55, 0.24, 0.12, 1), emission=((0.45, 0.12, 0.02, 1), 0.25))
MAT_WASH = make_mat("CCD_Arrow_WashWater", (0.22, 0.56, 1.0, 1), emission=((0.06, 0.22, 0.85, 1), 0.3))
MAT_PANEL = make_mat("CCD_HMI_BlackPanel", (0.015, 0.018, 0.02, 1), metallic=0.15, roughness=0.55)
MAT_TEXT = make_mat("CCD_Label_Text_White", (0.9, 0.96, 1.0, 1), emission=((0.55, 0.8, 1.0, 1), 0.12))
MAT_GREEN = make_mat("CCD_StatusGreen", (0.03, 0.75, 0.28, 1), emission=((0.0, 0.35, 0.12, 1), 0.3))
MAT_RED = make_mat("CCD_HazardRed", (0.85, 0.05, 0.035, 1), emission=((0.7, 0.02, 0.0, 1), 0.25))
MAT_CONCRETE = make_mat("CCD_ConcretePad", (0.50, 0.51, 0.49, 1), roughness=0.82)


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()


clear_scene()

ROOT_EMPTY = bpy.data.objects.new("CCDThickener_Redesign_Model", None)
bpy.context.collection.objects.link(ROOT_EMPTY)


def parent(obj):
    obj.parent = ROOT_EMPTY
    return obj


def smooth(obj):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    try:
        bpy.ops.object.shade_smooth()
    except Exception:
        pass
    obj.select_set(False)
    if obj.type == "MESH":
        mod = obj.modifiers.new("WeightedNormals", "WEIGHTED_NORMAL")
        mod.keep_sharp = True
    return obj


def cube(name, loc, scale, mat):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    bevel = obj.modifiers.new("SmallBevel", "BEVEL")
    bevel.width = min(scale) * 0.18
    bevel.segments = 2
    return parent(smooth(obj))


def cyl(name, loc, radius, depth, mat, vertices=64, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=loc, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(mat)
    return parent(smooth(obj))


def cone(name, loc, radius1, radius2, depth, mat, vertices=64, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=radius1, radius2=radius2, depth=depth, location=loc, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(mat)
    return parent(smooth(obj))


def torus(name, loc, major, minor, mat, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, major_segments=128, minor_segments=12, location=loc, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(mat)
    return parent(smooth(obj))


def orient_z(obj, start, end):
    start = Vector(start)
    end = Vector(end)
    vec = end - start
    obj.location = (start + end) / 2
    obj.rotation_euler = vec.to_track_quat("Z", "Y").to_euler()


def pipe(name, start, end, radius, mat, vertices=32):
    obj = cyl(name, (0, 0, 0), radius, (Vector(end) - Vector(start)).length, mat, vertices=vertices)
    orient_z(obj, start, end)
    return obj


def arrow(name, start, end, radius, mat):
    start = Vector(start)
    end = Vector(end)
    direction = (end - start).normalized()
    shaft_end = end - direction * 0.42
    pipe(name + "_Shaft", start, shaft_end, radius, mat, vertices=24)
    head = cone(name + "_Head", (0, 0, 0), radius * 3.4, 0.0, 0.55, mat, vertices=32)
    orient_z(head, shaft_end, end)
    return head


def beam(name, start, end, width, mat):
    start = Vector(start)
    end = Vector(end)
    mid = (start + end) / 2
    length = (end - start).length
    obj = cube(name, mid, (width, width, length), mat)
    obj.rotation_euler = (end - start).to_track_quat("Z", "Y").to_euler()
    return obj


def add_text(name, text, loc, size=0.18, mat=MAT_TEXT, rot=(math.radians(90), 0, 0)):
    bpy.ops.object.text_add(location=loc, rotation=rot)
    obj = bpy.context.object
    obj.name = name
    obj.data.body = text
    obj.data.align_x = "CENTER"
    obj.data.align_y = "CENTER"
    obj.data.size = size
    obj.data.extrude = 0.006
    obj.data.materials.append(mat)
    parent(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.convert(target="MESH")
    obj = bpy.context.object
    obj.name = name
    obj.parent = ROOT_EMPTY
    obj.select_set(False)
    return obj


def ring_wall_mesh(name, loc, radius_outer, radius_inner, height, mat, segments=144):
    verts = []
    faces = []
    for i in range(segments):
        ang = i * math.tau / segments
        co = math.cos(ang)
        si = math.sin(ang)
        verts.extend([
            (radius_outer * co, radius_outer * si, -height / 2),
            (radius_outer * co, radius_outer * si, height / 2),
            (radius_inner * co, radius_inner * si, -height / 2),
            (radius_inner * co, radius_inner * si, height / 2),
        ])
    for i in range(segments):
        n = (i + 1) % segments
        a = i * 4
        b = n * 4
        faces.append((a, b, b + 1, a + 1))
        faces.append((a + 2, a + 3, b + 3, b + 2))
        faces.append((a + 1, b + 1, b + 3, a + 3))
        faces.append((a, a + 2, b + 2, b))
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = loc
    obj.data.materials.append(mat)
    return parent(smooth(obj))


def add_panel(name, loc, lines, status_mat=MAT_GREEN):
    cube(name + "_Backplate", loc, (1.95, 0.08, 1.0), MAT_PANEL)
    cube(name + "_StatusStrip", (loc[0] - 0.84, loc[1] - 0.055, loc[2]), (0.11, 0.05, 0.82), status_mat)
    add_text(name + "_Text", lines, (loc[0] + 0.08, loc[1] - 0.105, loc[2] + 0.02), 0.12)


def add_handrail_arc(prefix, center, radius, z, start_deg, end_deg):
    count = 9
    last = None
    for i in range(count):
        t = i / (count - 1)
        ang = math.radians(start_deg + (end_deg - start_deg) * t)
        pos = (center[0] + math.cos(ang) * radius, center[1] + math.sin(ang) * radius, z)
        pipe(f"{prefix}_RailPost_{i:02d}", (pos[0], pos[1], z - 0.55), (pos[0], pos[1], z + 0.15), 0.025, MAT_STEEL)
        if last is not None:
            pipe(f"{prefix}_TopRail_{i:02d}", last, (pos[0], pos[1], z + 0.15), 0.025, MAT_STEEL)
            pipe(f"{prefix}_MidRail_{i:02d}", (last[0], last[1], z - 0.18), (pos[0], pos[1], z - 0.18), 0.018, MAT_STEEL)
        last = (pos[0], pos[1], z + 0.15)


def add_thickener(prefix, x, y, stage, label, clear_level, sediment_level):
    center = (x, y, 0)
    radius = 2.15
    ring_wall_mesh(prefix + "_OpenCircularTank_Wall", (x, y, 1.0), radius, radius - 0.13, 1.36, MAT_TANK)
    torus(prefix + "_OverflowLaunder_OuterRing", (x, y, 1.76), radius + 0.04, 0.07, MAT_EDGE)
    torus(prefix + "_OverflowWeir_ClearPLS_Ring", (x, y, 1.68), radius - 0.23, 0.045, MAT_CLEAR_PLS)
    cyl(prefix + "_ClearPLS_Surface", (x, y, clear_level), radius - 0.34, 0.035, MAT_CLEAR_PLS, vertices=144)
    cyl(prefix + "_SettlingZone_XRayColumn", (x, y, 1.08), radius - 0.44, 0.72, MAT_SETTLING, vertices=144)
    cone(prefix + "_UnderflowCone_Visible", (x, y, 0.33), radius * 0.72, 0.28, 0.66, MAT_UNDERFLOW, vertices=96)
    cyl(prefix + "_ThickUnderflow_BottomPool", (x, y, sediment_level), radius * 0.58, 0.11, MAT_UNDERFLOW, vertices=96)

    feed_z = 1.92
    ring_wall_mesh(prefix + "_CenterFeedwell", (x, y, feed_z), 0.56, 0.45, 0.64, MAT_STEEL, segments=72)
    cyl(prefix + "_Feedwell_SlurryCore", (x, y, feed_z - 0.05), 0.42, 0.5, MAT_SETTLING, vertices=72)
    cyl(prefix + "_DriveHead_Gearbox", (x, y, 2.43), 0.34, 0.28, MAT_STEEL, vertices=48)
    cyl(prefix + "_RakeShaft", (x, y, 1.08), 0.055, 2.05, MAT_STEEL, vertices=32)

    bridge_z = 2.32
    cube(prefix + "_DriveBridge_MainBeam", (x, y, bridge_z), (radius * 2.45, 0.18, 0.16), MAT_STEEL)
    cube(prefix + "_DriveBridge_WalkwayGrating", (x, y, bridge_z + 0.12), (radius * 2.25, 0.48, 0.055), MAT_EDGE)
    for bx in (-1.35, 1.35):
        beam(prefix + f"_DriveBridge_Truss_{bx:+.1f}_A", (x + bx, y - 0.27, bridge_z + 0.02), (x + bx * 0.35, y + 0.27, bridge_z + 0.52), 0.035, MAT_STEEL)
        beam(prefix + f"_DriveBridge_Truss_{bx:+.1f}_B", (x + bx, y + 0.27, bridge_z + 0.02), (x + bx * 0.35, y - 0.27, bridge_z + 0.52), 0.035, MAT_STEEL)

    # Rake arms and scraper blades, separate objects for possible animation.
    for i in range(6):
        ang = i * math.tau / 6 + stage * 0.14
        end = (x + math.cos(ang) * (radius - 0.42), y + math.sin(ang) * (radius - 0.42), 0.72)
        pipe(prefix + f"_AnimatedRakeArm_{i:02d}", (x, y, 0.84), end, 0.035, MAT_STEEL, vertices=16)
        blade = cube(prefix + f"_RakeBlade_{i:02d}", (end[0], end[1], 0.58), (0.42, 0.055, 0.14), MAT_EDGE)
        blade.rotation_euler[2] = ang + math.radians(25)

    # Visual floc particles settling through the tank.
    for i in range(36):
        ang = i * 2.399963 + stage
        r = 0.25 + (i % 11) / 11 * (radius - 0.75)
        z = 1.48 - (i % 7) * 0.11
        bpy.ops.mesh.primitive_uv_sphere_add(segments=8, ring_count=4, radius=0.035 + (i % 3) * 0.008, location=(x + math.cos(ang) * r, y + math.sin(ang) * r, z))
        p = bpy.context.object
        p.name = prefix + f"_SettlingFlocParticle_{i:02d}"
        p.data.materials.append(MAT_FLOC if i % 3 == 0 else MAT_UNDERFLOW)
        parent(smooth(p))

    add_handrail_arc(prefix + "_ServiceRail", (x, y), radius + 0.46, 2.15, 205, 335)
    add_panel(prefix + "_LocalTorquePanel", (x + radius + 0.95, y - 1.1, 1.62), f"{label}\nRAKE TORQUE OK\nBED LEVEL NORMAL")
    add_text(prefix + "_TankLabel", label, (x, y - radius - 0.36, 1.42), 0.15)


def add_flocculant_skid():
    cube("FlocculantSkid_BaseFrame", (-6.8, -3.25, 0.25), (2.2, 1.25, 0.18), MAT_STEEL)
    cyl("FlocculantSkid_MixingTank", (-7.25, -3.25, 1.08), 0.42, 1.25, MAT_FLOC, vertices=48)
    cyl("FlocculantSkid_AgitatorMotor", (-7.25, -3.25, 1.8), 0.22, 0.25, MAT_STEEL, vertices=32)
    cyl("FlocculantSkid_DosingPump", (-6.35, -3.25, 0.72), 0.23, 0.34, MAT_STEEL, vertices=32, rotation=(math.radians(90), 0, 0))
    add_panel("FlocculantSkid_Panel", (-5.78, -3.9, 1.25), "FLOCCULANT\nDOSING\nAUTO")
    pipe("FlocculantLine_To_CCD1", (-6.18, -3.25, 0.88), (-3.9, -0.7, 2.06), 0.035, MAT_FLOC)
    arrow("FlocculantFlow_ToFeedwell", (-5.62, -2.62, 1.05), (-4.1, -0.92, 1.92), 0.035, MAT_FLOC)
    add_text("FlocculantSkid_Label", "FLOCCULANT DOSING SKID", (-6.85, -4.02, 0.45), 0.12)


def add_pump_station():
    cube("UnderflowPumpStation_Base", (6.9, -3.15, 0.25), (2.4, 1.35, 0.16), MAT_STEEL)
    for i, x in enumerate((6.25, 7.45)):
        cyl(f"UnderflowPump_{i+1}_Casing", (x, -3.16, 0.78), 0.33, 0.38, MAT_TANK, vertices=48, rotation=(math.radians(90), 0, 0))
        cyl(f"UnderflowPump_{i+1}_Motor", (x + 0.58, -3.16, 0.78), 0.25, 0.65, MAT_STEEL, vertices=36, rotation=(0, math.radians(90), 0))
        cyl(f"UnderflowPump_{i+1}_CouplingGuard", (x + 0.27, -3.16, 0.78), 0.16, 0.24, MAT_EDGE, vertices=24, rotation=(0, math.radians(90), 0))
    add_panel("UnderflowPumpStation_HMI", (8.18, -3.9, 1.22), "UNDERFLOW\nPUMPS\nRUNNING")
    add_text("UnderflowPumpStation_Label", "UNDERFLOW TO TAILING", (6.9, -4.02, 0.44), 0.12)


def build_scene():
    cube("CCD_ConcreteSecondaryContainment_Pad", (0, 0, -0.06), (15.8, 8.6, 0.12), MAT_CONCRETE)
    cube("CCD_DarkSkidPerimeter_Frame", (0, 0, 0.08), (15.0, 8.0, 0.18), MAT_EDGE)
    cube("CCD_GratedWalkway_Main", (0, -3.0, 0.22), (14.3, 1.2, 0.08), MAT_EDGE)
    for x in (-6.8, -4.8, -2.8, -0.8, 1.2, 3.2, 5.2, 7.0):
        cube(f"CCD_Walkway_GratingBeam_{x:+.1f}", (x, -3.0, 0.32), (0.05, 1.25, 0.055), MAT_STEEL)

    stages = [(-4.2, 0.45, 1, "CCD-1\nRICH PLS"), (0.0, 0.45, 2, "CCD-2\nWASH"), (4.2, 0.45, 3, "CCD-3\nTAILING")]
    for x, y, stage, label in stages:
        add_thickener(f"CCD{stage}", x, y, stage, label, 1.66 - stage * 0.04, 0.43 + stage * 0.02)

    # Main process piping: slurry/underflow moves left to right; wash water opposite.
    arrow("Feed_FromFlashVessel_To_CCD1_Feedwell", (-7.35, 0.45, 1.88), (-4.82, 0.45, 1.88), 0.06, MAT_SETTLING)
    pipe("FeedPipe_FromFlashVessel", (-7.7, 0.45, 1.88), (-4.78, 0.45, 1.88), 0.10, MAT_STEEL)
    pipe("CCD1_To_CCD2_UnderflowPipe", (-3.9, -1.15, 0.55), (-0.35, -1.15, 0.55), 0.095, MAT_UNDERFLOW)
    pipe("CCD2_To_CCD3_UnderflowPipe", (0.3, -1.15, 0.55), (3.85, -1.15, 0.55), 0.095, MAT_UNDERFLOW)
    arrow("UnderflowDirection_CCD1_To_CCD2", (-3.35, -1.15, 0.73), (-1.0, -1.15, 0.73), 0.045, MAT_ARROW_UNDERFLOW)
    arrow("UnderflowDirection_CCD2_To_CCD3", (0.9, -1.15, 0.73), (3.2, -1.15, 0.73), 0.045, MAT_ARROW_UNDERFLOW)
    pipe("CCD3_Underflow_ToPumpStation", (4.95, -1.15, 0.55), (6.25, -3.15, 0.78), 0.095, MAT_UNDERFLOW)
    arrow("UnderflowDirection_ToTailing", (5.15, -1.45, 0.85), (6.1, -2.85, 0.85), 0.045, MAT_ARROW_UNDERFLOW)

    pipe("CCD1_Overflow_ToPurification_Header", (-4.2, 2.75, 1.78), (-7.35, 2.75, 1.78), 0.08, MAT_CLEAR_PLS)
    arrow("OverflowPLS_ToPurification", (-3.75, 2.95, 2.03), (-6.8, 2.95, 2.03), 0.045, MAT_ARROW_OVERFLOW)
    pipe("CCD3_WashWater_Inlet", (7.35, 2.75, 1.75), (4.2, 2.75, 1.75), 0.075, MAT_WASH)
    pipe("CCD3_To_CCD2_WashOverflow", (3.65, 2.42, 1.72), (0.55, 2.42, 1.72), 0.06, MAT_WASH)
    pipe("CCD2_To_CCD1_WashOverflow", (-0.55, 2.42, 1.72), (-3.65, 2.42, 1.72), 0.06, MAT_WASH)
    arrow("WashWater_CounterCurrent_CCD3_To_CCD2", (3.3, 2.64, 1.96), (0.8, 2.64, 1.96), 0.04, MAT_WASH)
    arrow("WashWater_CounterCurrent_CCD2_To_CCD1", (-0.8, 2.64, 1.96), (-3.25, 2.64, 1.96), 0.04, MAT_WASH)

    add_flocculant_skid()
    add_pump_station()

    add_panel("CCD_MainTrainingPanel", (0, -4.25, 1.45), "LEVEL 10 CCD SEPARATOR\nOVERFLOW: PLS TO PURIFICATION\nUNDERFLOW: RESIDUE TO TAILING\nWASH WATER: COUNTER-CURRENT")
    add_text("CCD_ProcessLegend_Overflow", "PLS OVERFLOW -> PURIFICATION", (-5.35, 3.08, 2.23), 0.14)
    add_text("CCD_ProcessLegend_Wash", "WASH WATER <- COUNTER-CURRENT", (2.2, 3.05, 2.18), 0.14)
    add_text("CCD_ProcessLegend_Underflow", "THICK UNDERFLOW -> TAILING", (2.4, -1.55, 1.05), 0.14)

    # Compact safety markers, no tall yellow pipe clutter.
    cube("CCD_NoYellowDesign_LabelPlate", (5.9, -4.22, 0.75), (2.35, 0.07, 0.45), MAT_PANEL)
    add_text("CCD_NoYellowDesign_LabelText", "MUTED INDUSTRIAL REDESIGN\nNO YELLOW PIPE RACK", (5.9, -4.29, 0.78), 0.105)
    cyl("CCD_EStop_Button", (-7.35, -4.1, 1.12), 0.16, 0.08, MAT_RED, vertices=32, rotation=(math.radians(90), 0, 0))
    cube("CCD_EStop_Box", (-7.35, -4.06, 0.98), (0.48, 0.12, 0.48), MAT_PANEL)
    add_text("CCD_EStop_Label", "LOCAL E-STOP", (-7.35, -4.15, 1.38), 0.09)

    # Camera and light.
    bpy.ops.object.light_add(type="AREA", location=(1.4, -6.4, 9.0))
    light = bpy.context.object
    light.name = "CCD_Key_AreaLight"
    light.data.energy = 850
    light.data.size = 6.0
    parent(light)

    bpy.ops.object.camera_add(location=(10.8, -12.4, 7.2), rotation=(math.radians(60), 0, math.radians(41)))
    cam = bpy.context.object
    cam.name = "CCDThickener_PreviewCamera"
    cam.data.lens = 24
    bpy.context.scene.camera = cam
    parent(cam)


build_scene()

bpy.context.scene.render.engine = "CYCLES"
bpy.context.scene.cycles.device = "CPU"
bpy.context.scene.cycles.samples = 28
bpy.context.scene.cycles.use_denoising = True
bpy.context.scene.render.resolution_x = 1400
bpy.context.scene.render.resolution_y = 900
bpy.context.scene.view_settings.view_transform = "Filmic"
bpy.context.scene.view_settings.look = "Medium High Contrast"

bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
bpy.ops.export_scene.fbx(
    filepath=str(FBX_PATH),
    use_selection=False,
    add_leaf_bones=False,
    apply_unit_scale=True,
    bake_space_transform=False,
    object_types={"EMPTY", "MESH", "LIGHT", "CAMERA"},
)
bpy.ops.render.render(write_still=True)
bpy.data.images["Render Result"].save_render(filepath=str(PREVIEW_PATH))

print("CCD_THICKENER_REDESIGN_DONE")
print("Blend:", BLEND_PATH)
print("FBX:", FBX_PATH)
print("Preview:", PREVIEW_PATH)
print("Objects:", len(bpy.data.objects))
