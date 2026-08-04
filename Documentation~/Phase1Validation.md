# Phase 1 Validation Record

**Date:** 2026-08-04
**Scope:** core parse and first render (UNITY_PORT.md §10, phase 1).

## Sample parse results (dotnet harness, pure core)

| File | Geometries | Instances | Vertices | Triangles | Elements | Categories |
|---|---|---|---|---|---|---|
| AR520.frag | 128 | 1,158 | 10,080 | 7,536 | 8,209 | 12,952 |
| Joyson Model.frag | 1,044 | 14,257 | 112,030 | 123,034 | 7,940 | 10,282 |
| 20210219Architecture.frag | 4,075 | 32,555 | 390,236 | 371,252 | 101,407 | 180,399 |

All three parse with `Success = true` and zero diagnostics.

## Cross-validation against FragmentsUE

**Joyson Model.frag matches the UE reference exactly** — instances, vertices,
and triangles all equal the counts in the UE run logs (`debug_log.txt`,
`debug_log3.txt`, `debug_log4.txt` at the repository root). Vertex counts are
crease-merge-dependent, so this match covers the whole geometry pipeline:
holes, earcut, crease merge, and totals.

**AR520.frag's UE log (`debug_log2.txt`) predates the current UE geometry
code and is not a valid reference.** The evidence:

- The log reports 18,608 vertices and 10,056 triangles. Reading AR520's shell
  tables directly gives 9,304 outer-ring vertices and 5,028 outer-ring-only
  triangles (Σ per face of n−2, holes ignored): the log's numbers are exactly
  2 × 9,304 and 2 × 5,028. That is an implementation which ignored holes,
  performed no crease-angle vertex merge, and duplicated every face for
  backface rendering — none of which the current `FragParser.cpp` does.
- The analytic earcut-with-holes expectation for AR520 is
  Σ(n_outer + n_holes + 2·holeCount − 2) = 5,028 + 1,984 + 2 × 280 = 7,572
  triangles. The port produces 7,536 — the 36-triangle shortfall is
  collinear/duplicate-vertex filtering inside earcut, the same behavior the
  reference earcut has.
- The Joyson runs in the same log family match the current algorithm exactly,
  so the logs were written at different code states.

**Deferred:** when an Unreal Engine installation is available, re-run AR520
and 20210219Architecture through the current FragmentsUE build and diff the
counts against the table above. The AR520 and Architecture test pins in
`Tests/Editor/FragmentParserSampleTests.cs` are port-generated regression
pins until then; the Joyson pins are UE-validated.

## Unity-editor checklist (deferred — no editor on the build machine)

1. Embed the package in a Unity 2021.3 project (API compatibility
   .NET Standard 2.1), let Unity generate `.meta` files, commit them.
2. All three asmdefs compile with zero warnings.
3. `FragmentVertexColor.shader` compiles (Built-in RP); vertex colors render;
   `_Cull` off double-sides.
4. Drag each sample `.frag` into the Project window: imports without errors,
   logged counts match the table above, reimport is idempotent, sub-assets
   persist.
5. Clean import order: delete `Library`, reopen, confirm materials use
   `FragmentsUnity/VertexColor` (not the Standard fallback) on the very first
   import — exercises the `DependsOnSourceAsset` path in
   `FragmentModelImporter`.
6. Prove the `IndexFormat.UInt32` branch with a synthetic mesh over 65,535
   vertices.
7. Visual acceptance vs FragmentsUE side by side: not mirrored, meter scale,
   no inside-out faces under backface culling, colors match. Known phase 1
   limitation: translucent materials (AR520 has 1, Joyson 7, Architecture 6)
   render opaque until the phase 4 translucent/glass materials land.
   *Superseded — they landed.* Phase 4 ships opaque, translucent and glass
   materials, so this check is now a check that all three render correctly, not
   that translucency is missing. Classified against the shipped models,
   `FragmentSurfaceClassifier` sorts AR520 into 1,146 opaque and 12 glass
   instances, Joyson into 14,198 opaque, 1 translucent and 58 glass, and
   Architecture into 29,618 opaque, 477 translucent and 2,460 glass. The
   translucent counts quoted above were the by-opacity counts; the classifier
   sends most low-opacity surfaces to glass on hue. See `Phase4Validation.md`.
8. Color space: the pow(2.2) vertex-color bake targets Linear color space;
   verify and document behavior in Gamma projects.
9. The double-sided material slot has never run (no sample declares
   RenderedFaces.TWO); exercise it with a synthetic file.
   *Superseded in part.* No sample declares `RenderedFaces.TWO`, so the flag
   path is still unexercised by real data — but merged chunks force double-sided
   (`FragmentsActor.cpp:662`), so from Phase 3 onward every merged mode builds
   the double-sided material. What still needs a synthetic file is the flag
   itself reaching an unmerged body.
10. Add the package to `"testables"` and run the full suite in the Unity Test
    Runner; `FRAGMENTSUNITY_SAMPLE_DIR` must be set before launching.
