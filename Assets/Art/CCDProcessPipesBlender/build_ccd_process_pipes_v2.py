"""
OLIVIA VR - build_ccd_process_pipes_v2.py  (HEADLESS)
Rebuild 2 pipa proses industrial keluaran CCD, koordinat WORLD UNITY yang benar:
  1. PLS_Overflow_Pipe : CCD overflow header (21.6,6.95,107.2) -> MHP inlet flange (73.42,8.21,111.88)
                          (cairan jernih PLS kaya Ni/Co -> Pemurnian/MHP, Level 10)
  2. Underflow_Slurry_Pipe : CCD underflow pump (-16,1.4,122.3) -> Filter Press feed (21.5,2.6,146)
                          (padatan/lumpur tailing -> Filter Press, level terakhir)

Konvensi terbukti: author di Unity-coords via u2b=(ux,uz,uy), export normal,
instance di Unity dgn rotasi Y=180 -> landing tepat di koordinat Unity.

Flow tube inner di-JOIN jadi 1 mesh bernama 'PLS_Flow' & 'Underflow_Flow'
supaya Level10CCDController.AutoFindReferences menemukannya (animasi aliran).

Run: blender --background --python build_ccd_process_pipes_v2.py
Output: Assets/Art/CCDProcessPipesBlender/CCD_Process_Pipes_v2.fbx
"""
import bpy, math, os
from mathutils import Vector

OUT = r"C:/Users/mp2dz/Olivia/Assets/Art/CCDProcessPipesBlender/CCD_Process_Pipes_v2.fbx"


def clear():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.objects):
        for it in list(coll):
            try: coll.remove(it)
            except Exception: pass


def mat(name, rgba, metallic=0.0, rough=0.6, emis=None):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF")
    if b:
        b.inputs["Base Color"].default_value = rgba
        if "Metallic" in b.inputs: b.inputs["Metallic"].default_value = metallic
        if "Roughness" in b.inputs: b.inputs["Roughness"].default_value = rough
        if emis is not None:
            if "Emission Color" in b.inputs:
                b.inputs["Emission Color"].default_value = emis
                b.inputs["Emission Strength"].default_value = 1.2
            elif "Emission" in b.inputs:
                b.inputs["Emission"].default_value = emis
    return m


def u2b(ux, uy, uz):
    return Vector((ux, uz, uy))


def smooth(o):
    for p in o.data.polygons:
        p.use_smooth = True


def align(obj, direction):
    z = Vector((0, 0, 1)); d = direction.normalized()
    axis = z.cross(d)
    if axis.length < 1e-6:
        if d.z < 0: obj.rotation_euler = (math.pi, 0, 0)
    else:
        ang = math.acos(max(-1.0, min(1.0, z.dot(d)))); axis.normalize()
        obj.rotation_mode = 'AXIS_ANGLE'
        obj.rotation_axis_angle = (ang, axis.x, axis.y, axis.z)


def cyl(p0, p1, r, m, name, seg=24):
    mid = (p0 + p1) * 0.5; d = (p1 - p0); L = d.length
    if L < 1e-5: return None
    bpy.ops.mesh.primitive_cylinder_add(vertices=seg, radius=r, depth=L, location=mid)
    o = bpy.context.active_object; o.name = name
    align(o, d); o.data.materials.append(m); smooth(o)
    return o


def elbow(c, r, m, name):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=r, location=c, segments=20, ring_count=12)
    o = bpy.context.active_object; o.name = name
    o.data.materials.append(m); smooth(o)
    return o


def flange(c, r, m, name, normal):
    bpy.ops.mesh.primitive_torus_add(location=c, major_radius=r, minor_radius=r * 0.30)
    o = bpy.context.active_object; o.name = name
    align(o, normal); o.data.materials.append(m); smooth(o)
    return o


def support(p, ground_y, m, name):
    h = max(0.4, p.z - ground_y)   # p.z is Blender-Z = Unity-Y (vertical)
    loc = Vector((p.x, p.y, p.z - h * 0.5))
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc)
    o = bpy.context.active_object; o.name = name
    o.scale = (0.12, 0.12, h * 0.5); o.data.materials.append(m)
    # foot pad
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(p.x, p.y, p.z - h))
    f = bpy.context.active_object; f.name = name + "_Foot"
    f.scale = (0.30, 0.30, 0.06); f.data.materials.append(m)
    return o


def valve(c, r, body_m, wheel_m, name, normal):
    # body box
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=c)
    body = bpy.context.active_object; body.name = name + "_Body"
    body.scale = (r * 1.9, r * 1.9, r * 1.6); body.data.materials.append(body_m)
    bpy.ops.object.modifier_add(type='BEVEL'); body.modifiers[-1].width = 0.04
    bpy.ops.object.modifier_apply(modifier=body.modifiers[-1].name)
    smooth(body)
    # bonnet + handwheel on top
    top = c + Vector((0, 0, r * 1.4))
    bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=r * 0.4, depth=r * 1.0, location=c + Vector((0, 0, r * 1.0)))
    bon = bpy.context.active_object; bon.name = name + "_Bonnet"; bon.data.materials.append(body_m); smooth(bon)
    bpy.ops.mesh.primitive_torus_add(location=top, major_radius=r * 0.85, minor_radius=r * 0.14)
    wh = bpy.context.active_object; wh.name = name + "_Handwheel"; wh.data.materials.append(wheel_m); smooth(wh)
    return [body, bon, wh]


def build_run(name, upts, outer_r, steel, flow_m, flow_name, support_every=6.0):
    bpts = [u2b(*p) for p in upts]
    flow_objs = []
    for i in range(len(bpts) - 1):
        cyl(bpts[i], bpts[i + 1], outer_r, steel, f"{name}_Steel_{i}")
        f = cyl(bpts[i], bpts[i + 1], outer_r * 0.72, flow_m, f"{name}_FlowSeg_{i}", seg=20)
        if f: flow_objs.append(f)
        if 0 < i < len(bpts) - 1:
            elbow(bpts[i], outer_r * 1.08, steel, f"{name}_Elbow_{i}")
    # flanges at both ends
    flange(bpts[0], outer_r * 1.4, steel, f"{name}_Flange_Start", bpts[1] - bpts[0])
    flange(bpts[-1], outer_r * 1.4, steel, f"{name}_Flange_End", bpts[-1] - bpts[-2])
    # supports
    si = 0
    for i in range(len(bpts) - 1):
        a, b = bpts[i], bpts[i + 1]; seglen = (b - a).length
        steps = max(1, int(seglen / support_every))
        for s in range(steps):
            t = (s + 0.5) / steps; p = a.lerp(b, t)
            support(p, 0.05, steel, f"{name}_Support_{si:02d}"); si += 1
    # join flow segments -> single mesh named flow_name
    if flow_objs:
        bpy.ops.object.select_all(action='DESELECT')
        for o in flow_objs: o.select_set(True)
        bpy.context.view_layer.objects.active = flow_objs[0]
        bpy.ops.object.join()
        flow_objs[0].name = flow_name
    return flow_objs[0] if flow_objs else None


def main():
    clear()
    steel = mat("CCDPipe_Steel", (0.60, 0.63, 0.67, 1), metallic=0.92, rough=0.32)
    wheel = mat("CCDPipe_ValveWheel", (0.85, 0.42, 0.12, 1), metallic=0.4, rough=0.5)
    pls_flow = mat("CCDPipe_PLS_Flow", (0.34, 0.66, 0.42, 1), rough=0.18, emis=(0.10, 0.42, 0.20, 1))
    tail_flow = mat("CCDPipe_Tailing_Flow", (0.36, 0.24, 0.16, 1), rough=0.55, emis=(0.14, 0.08, 0.04, 1))

    # --- PLS overflow: CCD overflow header -> MHP inlet flange (elevated rack) ---
    pls = [
        (21.6, 6.95, 107.2),
        (30.0, 7.05, 108.2),
        (45.0, 7.45, 109.6),
        (60.0, 7.90, 111.0),
        (70.0, 8.20, 111.7),
        (73.42, 8.21, 111.88),
    ]
    build_run("PLS_Overflow_Pipe", pls, 0.30, steel, pls_flow, "PLS_Flow", support_every=6.0)
    valve(u2b(25.5, 6.98, 107.5), 0.34, steel, wheel, "PLS_LetdownValve", Vector((1, 0, 0)))

    # --- Underflow slurry: CCD underflow pump -> Filter Press feed ---
    tail = [
        (-16.0, 1.45, 122.3),
        (-16.0, 1.95, 131.0),
        (-7.0, 2.30, 138.0),
        (6.0, 2.55, 143.0),
        (21.5, 2.60, 146.0),
    ]
    build_run("Underflow_Slurry_Pipe", tail, 0.27, steel, tail_flow, "Underflow_Flow", support_every=5.5)
    valve(u2b(-13.0, 1.55, 123.5), 0.30, steel, wheel, "Underflow_KnifeValve", Vector((0, 0, 1)))

    bpy.ops.object.select_all(action='SELECT')
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=OUT, use_selection=True, apply_unit_scale=True, global_scale=1.0,
        axis_forward='-Z', axis_up='Y', object_types={'MESH'}, mesh_smooth_type='FACE')
    print("OLIVIA_CCDPIPE_V2_OK ->", OUT)


if __name__ == "__main__":
    main()
