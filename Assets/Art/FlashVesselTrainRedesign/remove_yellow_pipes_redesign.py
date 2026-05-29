import math
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(r"C:\Users\mp2dz\Olivia\Assets\Art\FlashVesselTrainRedesign")
BLEND_PATH = ROOT / "FlashVesselTrain_Redesign.blend"
FBX_PATH = ROOT / "FlashVesselTrain_Redesign.fbx"
PREVIEW_PATH = ROOT / "FlashVesselTrain_Redesign_Preview.png"


def material(name, color, metallic=0.0, roughness=0.65, alpha=1.0):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = color
        bsdf.inputs["Metallic"].default_value = metallic
        bsdf.inputs["Roughness"].default_value = roughness
        bsdf.inputs["Alpha"].default_value = alpha
    mat.blend_method = "BLEND" if alpha < 1 else "OPAQUE"
    return mat


M_DARK_STEEL = material("OLIVIA_Redesign_DarkGalvanizedSteel", (0.12, 0.14, 0.15, 1), metallic=0.35, roughness=0.48)
M_PIPE_SUPPORT = material("OLIVIA_Redesign_MutedPipeSupport", (0.26, 0.29, 0.30, 1), metallic=0.45, roughness=0.42)
M_WALKWAY_EDGE = material("OLIVIA_Redesign_WalkwayEdgeGrey", (0.08, 0.09, 0.095, 1), metallic=0.25, roughness=0.55)
M_TEXT = bpy.data.materials.get("OLIVIA_Label_Text_White") or material("OLIVIA_Label_Text_White", (0.9, 0.96, 1.0, 1))

for yellow_name in ("UV_SafetyYellow_Rails_Valves", "OLIVIA_SafetyYellow"):
    mat_yellow = bpy.data.materials.get(yellow_name)
    if mat_yellow and mat_yellow.use_nodes:
        bsdf = mat_yellow.node_tree.nodes.get("Principled BSDF")
        if bsdf:
            bsdf.inputs["Base Color"].default_value = (0.18, 0.21, 0.22, 1.0)
            bsdf.inputs["Metallic"].default_value = 0.3
            bsdf.inputs["Roughness"].default_value = 0.5
            if "Emission Color" in bsdf.inputs:
                bsdf.inputs["Emission Color"].default_value = (0.0, 0.25, 0.12, 1.0)
            if "Emission Strength" in bsdf.inputs:
                bsdf.inputs["Emission Strength"].default_value = 0.08


ROOT_EMPTY = bpy.data.objects.get("FlashVesselTrain_Redesign_Model")
REDESIGN = bpy.data.objects.get("FlashVessel_NoYellowPipe_Redesign")
if REDESIGN:
    for child in list(REDESIGN.children):
        bpy.data.objects.remove(child, do_unlink=True)
else:
    REDESIGN = bpy.data.objects.new("FlashVessel_NoYellowPipe_Redesign", None)
    bpy.context.collection.objects.link(REDESIGN)
if ROOT_EMPTY:
    REDESIGN.parent = ROOT_EMPTY


REMOVE_PREFIXES = (
    "CagedLadder_",
    "PipeRack_",
    "UpperPlatform_",
    "SteamHeader_ShoePost_",
)


removed = []
for obj in list(bpy.data.objects):
    if obj.name.startswith(REMOVE_PREFIXES):
        removed.append(obj.name)
        bpy.data.objects.remove(obj, do_unlink=True)


def set_parent(obj):
    obj.parent = REDESIGN
    return obj


def shade(obj):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    try:
        bpy.ops.object.shade_smooth()
    except Exception:
        pass
    obj.select_set(False)
    if obj.type == "MESH" and not obj.modifiers.get("WeightedNormals"):
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
    bevel = obj.modifiers.new("SoftBevel", "BEVEL")
    bevel.width = 0.02
    bevel.segments = 2
    return set_parent(shade(obj))


def cyl(name, loc, radius, depth, mat, vertices=32, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=loc, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(mat)
    return set_parent(shade(obj))


def orient_z(obj, start, end):
    start = Vector(start)
    end = Vector(end)
    direction = end - start
    obj.location = (start + end) / 2
    obj.rotation_euler = direction.to_track_quat("Z", "Y").to_euler()


def pipe(name, start, end, radius, mat):
    obj = cyl(name, (0, 0, 0), radius, (Vector(end) - Vector(start)).length, mat)
    orient_z(obj, start, end)
    return obj


def add_text(name, text, loc, size=0.13):
    bpy.ops.object.text_add(location=loc, rotation=(math.radians(90), 0, 0))
    obj = bpy.context.object
    obj.name = name
    obj.data.body = text
    obj.data.align_x = "CENTER"
    obj.data.align_y = "CENTER"
    obj.data.size = size
    obj.data.extrude = 0.006
    obj.data.materials.append(M_TEXT)
    set_parent(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.convert(target="MESH")
    converted = bpy.context.object
    converted.name = name
    converted.data.materials.clear()
    converted.data.materials.append(M_TEXT)
    converted.parent = REDESIGN
    converted.select_set(False)
    return converted


# Compact grey supports replacing the tall yellow pipe rack.
for x in (-5.4, -2.7, 0.0, 2.7, 5.4):
    cube(f"NoYellow_SteamHeader_ShortPost_{x:+.1f}", (x, 2.52, 7.35), (0.14, 0.14, 1.02), M_PIPE_SUPPORT)
    cube(f"NoYellow_SteamHeader_Saddle_{x:+.1f}", (x, 2.52, 7.90), (0.72, 0.18, 0.10), M_DARK_STEEL)

# Low dark toe-rails around deck: visible safety boundary, not tall yellow clutter.
pipe("NoYellow_DeckFront_ToeRail", (-7.25, -2.82, 0.92), (7.25, -2.82, 0.92), 0.035, M_WALKWAY_EDGE)
pipe("NoYellow_DeckBack_ToeRail", (-7.25, 2.82, 0.92), (7.25, 2.82, 0.92), 0.035, M_WALKWAY_EDGE)
pipe("NoYellow_DeckLeft_ToeRail", (-7.25, -2.82, 0.92), (-7.25, 2.82, 0.92), 0.035, M_WALKWAY_EDGE)
pipe("NoYellow_DeckRight_ToeRail", (7.25, -2.82, 0.92), (7.25, 2.82, 0.92), 0.035, M_WALKWAY_EDGE)

for x in (-7.25, -3.6, 0.0, 3.6, 7.25):
    cube(f"NoYellow_DeckFront_LowPost_{x:+.1f}", (x, -2.82, 0.76), (0.08, 0.08, 0.38), M_WALKWAY_EDGE)
    cube(f"NoYellow_DeckBack_LowPost_{x:+.1f}", (x, 2.82, 0.76), (0.08, 0.08, 0.38), M_WALKWAY_EDGE)

# Industrial underside bracing so the train still feels supported after yellow rack removal.
for x in (-5.8, -2.9, 0.0, 2.9, 5.8):
    pipe(f"NoYellow_Underframe_CrossBrace_A_{x:+.1f}", (x - 0.45, -2.5, 0.36), (x + 0.45, 2.5, 0.66), 0.026, M_DARK_STEEL)
    pipe(f"NoYellow_Underframe_CrossBrace_B_{x:+.1f}", (x + 0.45, -2.5, 0.36), (x - 0.45, 2.5, 0.66), 0.026, M_DARK_STEEL)

cube("NoYellow_Redesign_Nameplate", (0, -3.12, 0.98), (3.5, 0.05, 0.42), M_DARK_STEEL)
add_text("NoYellow_Redesign_Nameplate_Text", "FLASH LETDOWN TRAIN\nNO YELLOW PIPE RACK", (0, -3.16, 1.0), 0.12)


cam = bpy.data.objects.get("Camera")
if cam:
    cam.location = (9.0, -9.8, 6.1)
    cam.rotation_euler = (math.radians(61), 0, math.radians(42))
    bpy.context.scene.camera = cam

key = bpy.data.objects.get("Key_Light")
if key:
    key.location = (1.0, -4.5, 9.5)
    key.data.energy = 760
    key.data.size = 5.0

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
if False:
    bpy.ops.render.render(write_still=True)
    bpy.data.images["Render Result"].save_render(filepath=str(PREVIEW_PATH))

print("NO_YELLOW_PIPE_REDESIGN_DONE")
print("Removed:", len(removed))
print("\n".join(removed))
print("Objects:", len(bpy.data.objects))
print("Blend:", BLEND_PATH)
print("FBX:", FBX_PATH)
print("Preview skipped due local GPU render crash:", PREVIEW_PATH)
