from pathlib import Path
import math

import bpy


ROOT = Path(r"C:\Users\mp2dz\Olivia\Assets\Art\FlashVesselTrainRedesign")
PREVIEW_PATH = ROOT / "FlashVesselTrain_Redesign_Preview.png"

cam = bpy.data.objects.get("Camera")
if cam:
    cam.location = (9.0, -9.8, 6.1)
    cam.rotation_euler = (math.radians(61), 0, math.radians(42))
    bpy.context.scene.camera = cam

if bpy.data.objects.get("Key_Light"):
    light = bpy.data.objects["Key_Light"]
    light.location = (1.0, -4.5, 9.5)
    light.data.energy = 760
    light.data.size = 5.0

bpy.context.scene.render.engine = "CYCLES"
bpy.context.scene.cycles.device = "CPU"
bpy.context.scene.cycles.samples = 24
bpy.context.scene.cycles.use_denoising = True
bpy.context.scene.render.resolution_x = 1280
bpy.context.scene.render.resolution_y = 800
bpy.context.scene.view_settings.view_transform = "Filmic"
bpy.context.scene.view_settings.look = "Medium High Contrast"

bpy.ops.render.render(write_still=True)
bpy.data.images["Render Result"].save_render(filepath=str(PREVIEW_PATH))
print("CPU_PREVIEW_DONE", PREVIEW_PATH)
