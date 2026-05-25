import math
import os
from mathutils import Vector

import bpy

ROOT = os.path.dirname(os.path.abspath(__file__))
BLEND_PATH = os.path.join(ROOT, "task_hint_arrow_uv.blend")
FBX_PATH = os.path.join(ROOT, "task_hint_arrow_uv.fbx")
GLB_PATH = os.path.join(ROOT, "task_hint_arrow_uv.glb")
ATLAS_PATH = os.path.join(ROOT, "task_hint_arrow_atlas.png")
PREVIEW_PATH = os.path.join(ROOT, "task_hint_arrow_preview.png")

COLORS = {
    "yellow": (1.0, 0.76, 0.04, 1.0),
    "dark": (0.045, 0.047, 0.042, 1.0),
    "cyan": (0.05, 0.78, 1.0, 1.0),
}

ATLAS_RECTS = {
    "yellow": (0.0, 0.5, 0.5, 1.0),
    "dark": (0.5, 0.5, 1.0, 1.0),
    "cyan": (0.0, 0.0, 0.5, 0.5),
}


def clean():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()


def make_atlas():
    size = 512
    pixels = [0.0] * (size * size * 4)
    for key, rect in ATLAS_RECTS.items():
        color = COLORS[key]
        x0, y0, x1, y1 = [int(v * size) for v in rect]
        for y in range(y0, y1):
            for x in range(x0, x1):
                edge = (x - x0) < 3 or (y - y0) < 3 or (x1 - x) < 3 or (y1 - y) < 3
                grain = 0.86 + (((x * 17 + y * 31) % 29) / 29.0) * 0.22
                if edge:
                    grain *= 0.40
                idx = (y * size + x) * 4
                pixels[idx:idx + 4] = (color[0] * grain, color[1] * grain, color[2] * grain, color[3])
    image = bpy.data.images.new("task_hint_arrow_atlas", size, size, alpha=True)
    image.pixels = pixels
    image.filepath_raw = ATLAS_PATH
    image.file_format = "PNG"
    image.save()
    return image


def make_material(image):
    mat = bpy.data.materials.new("M_TaskHint_Arrow_UVAtlas")
    mat.diffuse_color = (1, 1, 1, 1)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    bsdf = nodes.get("Principled BSDF")
    tex = nodes.new(type="ShaderNodeTexImage")
    tex.image = image
    mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    if "Emission Color" in bsdf.inputs:
        bsdf.inputs["Emission Color"].default_value = (1.0, 0.76, 0.04, 1.0)
    if "Emission Strength" in bsdf.inputs:
        bsdf.inputs["Emission Strength"].default_value = 0.25
    if "Metallic" in bsdf.inputs:
        bsdf.inputs["Metallic"].default_value = 0.1
    if "Roughness" in bsdf.inputs:
        bsdf.inputs["Roughness"].default_value = 0.42
    return mat


def assign_uv(obj, key):
    mesh = obj.data
    if not mesh.uv_layers:
        mesh.uv_layers.new(name="UVMap")
    uv = mesh.uv_layers.active.data
    u0, v0, u1, v1 = ATLAS_RECTS[key]
    verts = [v.co.copy() for v in mesh.vertices]
    minv = Vector((min(v.x for v in verts), min(v.y for v in verts), min(v.z for v in verts)))
    maxv = Vector((max(v.x for v in verts), max(v.y for v in verts), max(v.z for v in verts)))
    size = maxv - minv
    axes = sorted([(size.x, 0), (size.y, 1), (size.z, 2)], reverse=True)
    a, b = axes[0][1], axes[1][1]

    def comp(v, idx):
        return (v.x, v.y, v.z)[idx]

    da = max(comp(maxv, a) - comp(minv, a), 0.001)
    db = max(comp(maxv, b) - comp(minv, b), 0.001)
    for poly in mesh.polygons:
        for li in poly.loop_indices:
            co = mesh.vertices[mesh.loops[li].vertex_index].co
            uu = (comp(co, a) - comp(minv, a)) / da
            vv = (comp(co, b) - comp(minv, b)) / db
            uv[li].uv = (u0 + uu * (u1 - u0), v0 + vv * (v1 - v0))


def finish(obj, mat, key, bevel=0.0):
    obj.data.materials.append(mat)
    assign_uv(obj, key)
    for poly in obj.data.polygons:
        poly.use_smooth = True
    if bevel > 0:
        mod = obj.modifiers.new("soft_midpoly_edges", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        mod.affect = "EDGES"
    obj.modifiers.new("weighted_normals", "WEIGHTED_NORMAL")
    return obj


def cyl(name, radius, depth, loc, mat, key, vertices=32):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=loc)
    obj = bpy.context.object
    obj.name = name
    return finish(obj, mat, key, 0.01)


def cone(name, radius1, radius2, depth, loc, mat, key, vertices=36):
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=radius1, radius2=radius2, depth=depth, location=loc)
    obj = bpy.context.object
    obj.name = name
    return finish(obj, mat, key, 0.006)


def cube(name, loc, scale, mat, key):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(obj, mat, key, 0.012)


def torus(name, loc, major, minor, mat, key):
    bpy.ops.mesh.primitive_torus_add(major_segments=40, minor_segments=8, major_radius=major, minor_radius=minor, location=loc)
    obj = bpy.context.object
    obj.name = name
    return finish(obj, mat, key, 0.0)


def parent_all():
    root = bpy.data.objects.new("TaskHint_Arrow3D_Root", None)
    bpy.context.collection.objects.link(root)
    for obj in bpy.context.scene.objects:
        if obj.name != root.name and obj.type in {"MESH", "EMPTY"}:
            obj.parent = root
    return root


def validate():
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    missing = [o.name for o in meshes if not o.data.uv_layers]
    tris = sum(sum(max(1, len(p.vertices) - 2) for p in o.data.polygons) for o in meshes)
    print(f"VALIDATION mesh_count={len(meshes)} missing_uv={len(missing)} approx_tris={tris}")
    if missing:
        raise RuntimeError("Missing UV: " + ", ".join(missing))


def setup_preview():
    bpy.context.scene.render.engine = "BLENDER_WORKBENCH"
    bpy.context.scene.display.shading.light = "STUDIO"
    bpy.context.scene.display.shading.color_type = "MATERIAL"
    bpy.ops.object.light_add(type="AREA", location=(0, -3, 3))
    light = bpy.context.object
    light.data.energy = 380
    light.data.size = 4
    bpy.ops.object.camera_add(location=(1.4, -3.2, 1.2))
    cam = bpy.context.object
    direction = Vector((0, 0, -0.25)) - cam.location
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = 45
    bpy.context.scene.camera = cam


def main():
    clean()
    image = make_atlas()
    mat = make_material(image)

    # Blender Z-up. Export maps Blender Z to Unity Y, so this points down in Unity.
    cyl("Arrow_Yellow_Shaft", 0.055, 0.52, (0, 0, -0.18), mat, "yellow")
    cone("Arrow_Heavy_Down_Head", 0.0, 0.23, 0.36, (0, 0, -0.62), mat, "yellow")
    cyl("Arrow_Dark_Collar", 0.095, 0.045, (0, 0, 0.10), mat, "dark")
    torus("Arrow_Cyan_Pulse_Ring", (0, 0, 0.14), 0.18, 0.014, mat, "cyan")

    for i, angle in enumerate((0, 90, 180, 270)):
        fin = cube(f"Arrow_Dark_Fin_{i}", (0, 0, -0.32), (0.035, 0.16, 0.26), mat, "dark")
        fin.rotation_euler[2] = math.radians(angle)

    root = parent_all()
    setup_preview()

    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    validate()

    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    bpy.context.view_layer.objects.active = root
    for child in root.children:
        child.select_set(True)

    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH,
        use_selection=True,
        apply_unit_scale=True,
        bake_space_transform=False,
        object_types={"EMPTY", "MESH"},
        axis_forward="-Z",
        axis_up="Y",
        path_mode="COPY",
        embed_textures=False,
        use_mesh_modifiers=True,
    )
    bpy.ops.export_scene.gltf(filepath=GLB_PATH, export_format="GLB", use_selection=True, export_apply=True)

    # Headless preview render can be slow on some Unity/Blender MCP sessions.
    # The FBX/GLB/atlas are the required Unity deliverables.
    print("WROTE", BLEND_PATH, FBX_PATH, GLB_PATH, ATLAS_PATH)


if __name__ == "__main__":
    main()
