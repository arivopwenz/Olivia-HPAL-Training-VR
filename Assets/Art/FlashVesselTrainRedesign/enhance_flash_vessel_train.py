import math
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(r"C:\Users\mp2dz\Olivia\Assets\Art\FlashVesselTrainRedesign")
BLEND_PATH = ROOT / "FlashVesselTrain_Redesign.blend"
FBX_PATH = ROOT / "FlashVesselTrain_Redesign.fbx"
PREVIEW_PATH = ROOT / "FlashVesselTrain_Redesign_preview.png"


def mat(name, color, metallic=0.0, roughness=0.65, alpha=1.0, emission=None):
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = color
        bsdf.inputs["Alpha"].default_value = alpha
        bsdf.inputs["Metallic"].default_value = metallic
        bsdf.inputs["Roughness"].default_value = roughness
        if emission:
            bsdf.inputs["Emission Color"].default_value = emission[0]
            bsdf.inputs["Emission Strength"].default_value = emission[1]
    material.blend_method = "BLEND" if alpha < 1 else "OPAQUE"
    material.use_screen_refraction = alpha < 1
    material.show_transparent_back = True
    return material


M_XRAY = mat("OLIVIA_XRay_Ghost_Blue", (0.22, 0.72, 1.0, 0.16), alpha=0.16, emission=((0.08, 0.35, 0.75, 1), 0.45))
M_VAPOR = mat("OLIVIA_RecoveredSteam_White", (0.86, 0.94, 1.0, 0.42), alpha=0.42, emission=((0.35, 0.62, 1.0, 1), 0.25))
M_SLURRY = mat("OLIVIA_PurpleSlurry_FlowOverlay", (0.42, 0.18, 0.55, 0.82), alpha=0.82, emission=((0.38, 0.08, 0.58, 1), 0.18))
M_SCALE = mat("OLIVIA_ScaleDeposit_Rust", (0.62, 0.31, 0.1, 1.0), roughness=0.9)
M_PANEL = mat("OLIVIA_DarkHMI_Panel", (0.02, 0.025, 0.028, 1.0), metallic=0.2, roughness=0.55)
M_TEXT = mat("OLIVIA_Label_Text_White", (0.9, 0.96, 1.0, 1.0), emission=((0.5, 0.9, 1.0, 1), 0.2))
M_GREEN = mat("OLIVIA_StatusGreen", (0.05, 0.85, 0.32, 1.0), emission=((0.0, 0.55, 0.18, 1), 0.4))
M_YELLOW = mat("OLIVIA_SafetyYellow", (1.0, 0.75, 0.05, 1.0), emission=((1.0, 0.5, 0.0, 1), 0.15))
M_RED = mat("OLIVIA_AlarmRed", (0.95, 0.08, 0.05, 1.0), emission=((0.85, 0.02, 0.0, 1), 0.45))
M_BLACK = mat("OLIVIA_Label_Black", (0.01, 0.01, 0.01, 1.0), roughness=0.8)


def ensure_parent(name):
    obj = bpy.data.objects.get(name)
    if obj:
        return obj
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    return obj


ROOT_EMPTY = bpy.data.objects.get("FlashVesselTrain_Redesign_Model") or ensure_parent("FlashVesselTrain_Redesign_Model")
OVERLAY = ensure_parent("FlashVessel_Level9_EducationOverlay")
OVERLAY.parent = ROOT_EMPTY


def set_parent(obj):
    obj.parent = OVERLAY
    return obj


def shade(obj):
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


def cube_obj(name, loc, scale, material):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    bevel = obj.modifiers.new("SmallBevel", "BEVEL")
    bevel.width = 0.025
    bevel.segments = 2
    return set_parent(shade(obj))


def cyl_obj(name, loc, radius, depth, material, vertices=64, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=loc, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(material)
    return set_parent(shade(obj))


def cone_obj(name, loc, radius1, depth, material, vertices=48, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=radius1, radius2=0.0, depth=depth, location=loc, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(material)
    return set_parent(shade(obj))


def orient_z_to_vector(obj, start, end):
    start_v = Vector(start)
    end_v = Vector(end)
    direction = end_v - start_v
    obj.location = (start_v + end_v) / 2
    obj.rotation_euler = direction.to_track_quat("Z", "Y").to_euler()


def pipe_between(name, start, end, radius, material, vertices=32):
    length = (Vector(end) - Vector(start)).length
    obj = cyl_obj(name, (0, 0, 0), radius, length, material, vertices)
    orient_z_to_vector(obj, start, end)
    return obj


def arrow_between(name, start, end, radius, material):
    start_v = Vector(start)
    end_v = Vector(end)
    direction = (end_v - start_v).normalized()
    shaft_end = end_v - direction * 0.28
    pipe_between(name + "_Shaft", start_v, shaft_end, radius, material, 24)
    cone = cone_obj(name + "_Head", (0, 0, 0), radius * 3.5, 0.42, material, 32)
    orient_z_to_vector(cone, shaft_end, end_v)
    return cone


def add_text(name, text, loc, size=0.22, material=M_TEXT, align="CENTER"):
    bpy.ops.object.text_add(location=loc, rotation=(math.radians(90), 0, 0))
    obj = bpy.context.object
    obj.name = name
    obj.data.body = text
    obj.data.align_x = align
    obj.data.align_y = "CENTER"
    obj.data.size = size
    obj.data.extrude = 0.006
    obj.data.materials.append(material)
    set_parent(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.convert(target="MESH")
    mesh = bpy.context.object
    mesh.name = name
    mesh.data.materials.clear()
    mesh.data.materials.append(material)
    mesh.parent = OVERLAY
    mesh.select_set(False)
    return mesh


def add_panel(name, x, z, lines, status_mat):
    cube_obj(name + "_Backplate", (x, -1.86, z), (1.35, 0.05, 0.82), M_PANEL)
    cube_obj(name + "_StatusStrip", (x - 0.57, -1.89, z), (0.08, 0.04, 0.68), status_mat)
    add_text(name + "_Text", lines, (x + 0.05, -1.92, z + 0.02), 0.16, M_TEXT)


def helix_curve(name, center, radius, height, turns, material, bevel=0.025, phase=0.0):
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 2
    curve.bevel_depth = bevel
    curve.bevel_resolution = 3
    poly = curve.splines.new("POLY")
    count = 96
    poly.points.add(count - 1)
    for i, point in enumerate(poly.points):
        t = i / (count - 1)
        angle = phase + t * turns * math.tau
        point.co = (
            center[0] + math.cos(angle) * radius,
            center[1] + math.sin(angle) * radius,
            center[2] - height * 0.5 + t * height,
            1.0,
        )
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(material)
    return set_parent(obj)


def add_cutaway_for_vessel(prefix, x, body_height, body_center_z, slurry_z, pressure, temp, status_mat):
    cyl_obj(prefix + "_XRay_VaporZone_Ghost", (x, 0, body_center_z + body_height * 0.19), 1.04, body_height * 0.45, M_XRAY, 96)
    cyl_obj(prefix + "_XRay_SlurryPool_Ghost", (x, 0, slurry_z), 1.0, 0.72, M_SLURRY, 96)
    helix_curve(prefix + "_InternalSteamFlash_Swirl", (x, 0, body_center_z + 0.75), 0.62, body_height * 0.55, 3.25, M_VAPOR, 0.026, phase=x)
    helix_curve(prefix + "_InternalSlurryVortex_Purple", (x, 0, slurry_z + 0.2), 0.52, 0.75, 1.4, M_SLURRY, 0.028, phase=x * 0.4)
    add_panel(prefix + "_PressureCascadePanel", x, 6.25, f"{prefix}\n{temp} C\n{pressure} bar", status_mat)


def add_scale_deposit_cluster():
    bpy.ops.mesh.primitive_torus_add(major_radius=0.39, minor_radius=0.04, major_segments=72, minor_segments=10, location=(-5.72, -0.72, 2.66), rotation=(0, math.radians(90), 0))
    ring = bpy.context.object
    ring.name = "LetdownValve_ScaleDeposit_Ring_TrainingHazard"
    ring.data.materials.append(M_SCALE)
    set_parent(shade(ring))
    for i in range(14):
        angle = i * math.tau / 14
        loc = (-5.72, -0.72 + math.cos(angle) * 0.39, 2.66 + math.sin(angle) * 0.39)
        bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=6, radius=0.055 + 0.02 * (i % 3), location=loc)
        chunk = bpy.context.object
        chunk.name = f"LetdownValve_ScaleDeposit_Chunk_{i:02d}"
        chunk.scale.x *= 1.5
        chunk.data.materials.append(M_SCALE)
        set_parent(shade(chunk))
    cube_obj("LetdownValve_ScaleRisk_LabelPlate", (-5.72, -1.55, 3.48), (1.35, 0.05, 0.38), M_RED)
    add_text("LetdownValve_ScaleRisk_LabelText", "SCALE RISK\nLETDOWN VALVE", (-5.72, -1.59, 3.5), 0.115, M_TEXT)


def add_flow_overlays():
    slurry_points = [
        ((-7.15, -1.05, 2.66), (-5.95, -0.86, 2.66), "Autoclave_To_Letdown"),
        ((-5.55, -0.82, 2.66), (-4.55, -1.35, 4.45), "Letdown_To_FV1"),
        ((-2.82, -0.72, 2.66), (-1.72, -0.72, 2.66), "FV1_To_FV2"),
        ((1.05, -0.72, 2.46), (2.35, -0.72, 2.28), "FV2_To_FV3"),
        ((4.55, -0.55, 1.22), (6.48, -0.55, 1.22), "FV3_To_CCD"),
    ]
    for start, end, suffix in slurry_points:
        arrow_between("PurpleSlurryFlow_" + suffix, start, end, 0.055, M_SLURRY)

    steam_points = [
        ((-3.95, 0.72, 8.18), (-3.95, 2.18, 8.18), "FV1_To_Header"),
        ((0.0, 0.72, 7.95), (0.0, 2.18, 7.95), "FV2_To_Header"),
        ((3.95, 0.72, 7.72), (3.95, 2.18, 7.72), "FV3_To_Header"),
        ((-4.8, 2.58, 8.18), (5.65, 2.58, 8.18), "Header_To_Preheater"),
    ]
    for start, end, suffix in steam_points:
        arrow_between("RecoveredSteamFlow_" + suffix, start, end, 0.045, M_VAPOR)

    cube_obj("FlowLegend_Backplate", (0.0, -3.16, 2.05), (4.4, 0.05, 0.74), M_PANEL)
    cube_obj("FlowLegend_PurpleSwatch", (-1.8, -3.2, 2.18), (0.42, 0.05, 0.12), M_SLURRY)
    cube_obj("FlowLegend_SteamSwatch", (-1.8, -3.2, 1.92), (0.42, 0.05, 0.12), M_VAPOR)
    add_text("FlowLegend_Text", "PURPLE = HOT PLS SLURRY TO CCD\nWHITE = RECOVERED FLASH STEAM TO PRE-HEATER", (0.22, -3.24, 2.05), 0.12, M_TEXT)


def add_operator_inspection_targets():
    targets = [
        ("Inspect_01_LetdownValve", (-5.72, -1.28, 2.66), M_RED, "1\nLETDOWN"),
        ("Inspect_02_FV1Pressure", (-3.2, -1.55, 4.8), M_YELLOW, "2\nFV1 12 BAR"),
        ("Inspect_03_SteamRecovery", (2.45, 2.78, 8.18), M_GREEN, "3\nSTEAM RECOVERY"),
    ]
    for name, loc, material, label in targets:
        cyl_obj(name + "_BeaconRing", loc, 0.28, 0.035, material, 48, rotation=(math.radians(90), 0, 0))
        add_text(name + "_Text", label, (loc[0], loc[1] - 0.04, loc[2] + 0.43), 0.105, M_TEXT)


def cleanup_previous_overlay():
    old = bpy.data.objects.get("FlashVessel_Level9_EducationOverlay")
    if not old:
        return
    children = list(old.children)
    for obj in children:
        bpy.data.objects.remove(obj, do_unlink=True)


cleanup_previous_overlay()
add_cutaway_for_vessel("FV1", -3.95, 4.85, 4.32, 2.18, "12", "190", M_YELLOW)
add_cutaway_for_vessel("FV2", 0.0, 4.5, 4.15, 2.05, "3", "120", M_YELLOW)
add_cutaway_for_vessel("FV3", 3.95, 4.15, 3.98, 1.92, "1", "80", M_GREEN)
add_scale_deposit_cluster()
add_flow_overlays()
add_operator_inspection_targets()

add_panel("Level9TrainingGoalPanel", 6.08, 5.95, "LEVEL 9\nFLASH LETDOWN\nPRESSURE CASCADE", M_GREEN)
add_text("RecoveredSteam_ToPreheater_Label", "RECOVERED FLASH STEAM -> PRE-HEATER", (2.15, 2.92, 8.62), 0.13, M_TEXT)
add_text("FinalSlurry_ToCCD_Label", "COOLED PLS SLURRY -> CCD", (5.45, -0.88, 1.64), 0.13, M_TEXT)

# Camera and light for verification render.
cam = bpy.data.objects.get("Camera")
if cam:
    cam.location = (9.2, -10.2, 6.7)
    cam.rotation_euler = (math.radians(63), 0, math.radians(43))
    bpy.context.scene.camera = cam

sun = bpy.data.objects.get("Key_Light") or bpy.data.objects.new("Key_Light", bpy.data.lights.new("Key_Light", "AREA"))
if not sun.users_collection:
    try:
        bpy.context.collection.objects.link(sun)
    except RuntimeError:
        pass
sun.location = (1.0, -4.5, 9.5)
sun.data.energy = 700
sun.data.size = 5

try:
    bpy.context.scene.render.engine = "BLENDER_EEVEE_NEXT"
except TypeError:
    bpy.context.scene.render.engine = "BLENDER_EEVEE"
if hasattr(bpy.context.scene, "eevee"):
    bpy.context.scene.eevee.taa_render_samples = 64
bpy.context.scene.render.resolution_x = 1600
bpy.context.scene.render.resolution_y = 1000
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

print("ENHANCED_FLASH_VESSEL_DONE")
print(f"Blend: {BLEND_PATH}")
print(f"FBX: {FBX_PATH}")
print(f"Preview: {PREVIEW_PATH}")
print(f"Objects: {len(bpy.data.objects)}")
