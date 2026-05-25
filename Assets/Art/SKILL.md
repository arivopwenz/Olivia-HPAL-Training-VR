---
name: design-3d-unity-blender
description: Lightweight realistic 3D asset and scene design for Unity and Blender. Use when Codex needs to create, redesign, optimize, or verify 3D models, industrial equipment, piping, platforms, stairs, walkways, props, prefab-ready assets, Unity scene layouts, Blender mesh passes, FBX/GLB export, LODs, colliders, materials, or performance-safe high-detail visuals.
---

# 3D Design for Unity and Blender

## Operating Model

Build realistic 3D by default as modular mid-poly, not heavy sculpted high-poly. Prioritize correct scale, readable silhouettes, industrial logic, simple colliders, reusable parts, and clear player paths.

Use Unity first for layout, playable spaces, modular equipment, piping, platforms, railing, stairs, collision, materials, screenshots, and scene validation. Use Blender only when Unity primitives or ProBuilder-style shapes cannot create the needed silhouette cleanly.

If Blender MCP is unavailable, continue in Unity and state the limitation briefly.

## Workflow

1. Inspect the current scene, hierarchy, target object names, active materials, player path, and performance-sensitive areas.
2. Establish scale and clearance before detail. Keep equipment, walkways, stairs, service gaps, and pipe routes coherent.
3. Block out the asset with primitives or ProBuilder-style geometry.
4. Add modular realism: flanges, valves, nozzles, bolts, pipe supports, saddles, handrails, ladders, access platforms, nameplates, gauges, guards, cable trays, and service pads.
5. Replace messy routes with clean industrial routing: elevated racks, supported straight runs, intentional elbows, clear tie-ins, and protected low pipes.
6. Optimize while building: reuse materials, avoid unique meshes when unnecessary, use simple colliders, keep particle effects sparse, and avoid excessive transparent surfaces.
7. Verify visually with screenshots from player and overview angles.
8. Validate the Unity scene, check console issues, save only after the result is coherent.

## Unity Practice

Use Unity MCP for scene edits whenever possible.

- Use `manage_scene` to inspect, validate, save, and move between scenes.
- Use `find_gameobjects` and hierarchy reads before modifying existing objects.
- Use `manage_gameobject`, `manage_components`, `manage_material`, `manage_probuilder`, and `execute_code` for creation and edits.
- Use `manage_camera` screenshots for before/after review.
- Keep existing gameplay/controller objects intact unless the user asks to remove them.
- Prefer disabling old visual leftovers over deleting objects that contain process logic, scripts, animation, XR interactables, or references.
- For playable spaces, keep colliders simple and continuous. Avoid pipe clutter where the player walks.

Use modular primitives for:
- pipes: cylinders/capsules with elbows, flanges, supports
- vessels: capsules/cylinders with domes, saddles, bands, nozzles
- platforms: boxes with grating material, rail posts, top rails, toe boards
- stairs: repeated treads, side stringers, handrails, landing pads
- industrial detail: gauges, lights, valves, handwheels, labels, cable trays

## Blender Practice

Use Blender for custom mesh work only when it improves the result:

- complex crusher mouths, curved hoppers, custom nozzle shapes, bevelled panels, duct transitions, handwheel meshes, grating modules, or baked-detail props
- clean topology with low-to-mid poly density
- bevel important edges, use weighted normals, avoid dense subdivisions
- set origin and pivot intentionally
- apply transforms before export
- export FBX or GLB with real-world scale
- create simple Unity colliders separately; do not rely on dense mesh colliders for walkable gameplay

For high-detail assets, fake detail with bevels, normal maps, repeated modules, and smart material contrast instead of raw polygon count.

## Performance Budget

Default target: lightweight VR/game-ready scene.

- Use hero detail only near the player or camera.
- Reuse modular pieces instead of many unique meshes.
- Keep small props under a few thousand triangles.
- Keep major machines mid-poly unless they are a focal hero object.
- Use LODs for Blender-made hero assets.
- Use simple box/capsule colliders for most objects.
- Avoid expensive mesh colliders except for static, necessary walkable surfaces.
- Avoid many real-time lights, excessive particles, and large transparent materials.

## Industrial Checklist

Before calling the design finished, check:

- Does the equipment layout make process sense?
- Are pipes supported and routed with clear intent?
- Are pipe penetrations, nozzles, valves, and flanges aligned?
- Are there service platforms, ladders, stairs, or access clearances where operators would need them?
- Are low pipes guarded or raised away from player routes?
- Are railings, toe boards, and stairs continuous enough for safe traversal?
- Are old/duplicate objects hidden or removed so the scene does not look cluttered?
- Is the silhouette readable from overview and player height?
- Is the scene validated and saved?

## Output Expectations

When finishing, report:

- objects or scene areas changed
- what was created, moved, disabled, or optimized
- screenshot path when captured
- validation result
- any remaining limitation such as unavailable Blender connection or external console noise
