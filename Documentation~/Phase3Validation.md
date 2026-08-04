# Phase 3 Validation Record

**Date:** 2026-08-04
**Scope:** import modes, element picking, filtering (UNITY_PORT.md §10, phase 3).

## Per-mode build results (AR520.frag, picking enabled)

| Mode | Objects | Hierarchy nodes | Merged chunks | Meshes | Materials |
|---|---|---|---|---|---|
| HierarchyPerBody | 2,297 | 1,138 | 0 | 128 | 1 |
| HierarchyPerElement | 43 | 21 | 21 | 21 | 1 |
| HierarchyPerStorey | 14 | 7 | 6 | 6 | 1 |
| Instanced | 1,159 | 0 | 0 | 128 | 1 |
| MergedWholeModel | 7 | 0 | 6 | 6 | 1 |

Joyson Model in HierarchyPerElement produces 4,174 objects, 2,110 chunks and
2 materials — the only sample/mode pair needing more than one material, so it
covers the double-sided material path.

> **Superseded by Phase 4 — material counts only.** Phase 3 rendered every
> surface opaque, so the only thing that could split a material was sidedness.
> Phase 4 gave glass and translucent surfaces their own materials, and the
> counts above moved: AR520 now builds 2 materials in every mode (opaque and
> glass) and Joyson HierarchyPerElement builds 4. Objects, hierarchy nodes,
> merged chunks and mesh counts are unchanged, and every measurement in the rest
> of this record still holds. See `Phase4Validation.md`.

Every mode is asserted to satisfy set-equality between the meshes actually
rendered in the hierarchy and `FragmentSceneBuildResult.Meshes`, and likewise
for materials. That single assertion catches a mesh missing from the
sub-assets, a duplicate registration, and an orphaned sub-asset, all of which
would corrupt the imported asset.

## Picking (acceptance criterion)

For each mode, a `RaycastHit` is synthesised against a real spawned collider
and resolved through `FragmentPicker`:

- Merged modes walk a chunk's real triangle range via `FragmentElementTable`
  and assert the first, middle and last triangle of a part all resolve to that
  part's element, while the first triangle of the *next* part resolves to the
  next element — the boundary case the binary search exists for.
- Per-element and per-body modes resolve through `FragmentElementReference`.
- The resolved metadata's GlobalId and category are checked against what the
  parser produced for that local id.

Triangle-start tables are kept in **all** merged modes, so picking resolves an
exact element even in HierarchyPerStorey and MergedWholeModel. The UE tooltips
say those modes have no element selection; UNITY_PORT.md §6 sanctions keeping
the tables, so this is a deliberate improvement.

Unity returns `RaycastHit.triangleIndex` natively, so UE's UV0 part-index
encoding, its `DecodePartIndexFromUV` fallback and the "Support UV From Hit
Results" project setting are deliberately not ported.

## Filtering (acceptance criterion)

Isolating a storey taken from real metadata leaves that storey's objects
visible and hides the rest (Joyson: 9,945 renderers visible, 2,478 hidden;
AR520: 48 visible, 770 hidden). `ClearFilter` restores them.

**"Hides exactly that storey" holds only where the build is element-granular.**
Measured on Joyson isolating `Level 1` (1,986 geometry-bearing elements, 887 in
the storey, 1,099 outside it):

| Mode | Out-of-storey elements still drawn |
|---|---|
| HierarchyPerBody | 0 |
| Instanced | 0 |
| HierarchyPerElement | 0 |
| HierarchyPerStorey | 1 |
| MergedWholeModel | **385 of 1,099** |

A merged chunk is one GameObject holding many elements, so it can only hide as
a unit. FragmentsUE has the same limit and refuses to filter merged imports
finer than the merged unit, warning at `FragmentsActor.cpp:1187-1193`. This
port now mirrors that intent: `FragmentVisibilityIndex.SupportsElementFiltering`
reports whether the built scene is element-granular, and `FragmentFilter` logs
one warning per call when it is not. The leak is pinned by a test so it stays
known behavior rather than a surprise. AR520 cannot show this — all of its
elements sit on one storey.

## Colliders and physics

An imported model carries a `MeshCollider` per drawable so picking works, which
makes it solid to everything by default. FragmentsUE deliberately avoids that:
it sets query-only collision, ignores every movement channel, and disables
navigation (`FragmentsActor.cpp:91-122`). Unity 2021.3 has no per-collider
channel filtering and a non-convex `MeshCollider` cannot be a trigger, so the
equivalent is a dedicated layer: `FragmentSceneBuildOptions.ColliderLayer` (an
importer setting) puts every collider on a layer the project can clear from its
collision matrix. The default is Unity's Default layer, so the opt-in is
documented in `Picking.md` rather than assumed — a package cannot safely claim a
layer index.

Two properties are enforced deliberately:

- **Hidden means unpickable.** Hiding disables colliders as well as rendering,
  so a hidden element cannot be picked through.
- **Restore is exact and stateless.** Filtering hides via
  `Renderer.forceRenderingOff`, a separate channel from the `Renderer.enabled`
  flag the build uses to hide `IfcSpace`-class volumes, so clearing a filter can
  never reveal geometry that was meant to stay hidden. `Clear()` sweeps the
  serialized target list rather than a runtime record, so it still works after a
  domain reload — without a reload, a script recompile mid-filter would strand
  the model hidden.

## Known divergences from FragmentsUE

- **Instanced mode** uses shared-mesh GameObjects with `enableInstancing`
  rather than `Graphics.RenderMeshInstanced`. UNITY_PORT.md §6 suggests the
  latter but notes it needs "a parallel collider strategy (or GPU picking)";
  picking in every mode is a phase acceptance criterion, so the collider-bearing
  approach wins. Unity still batches these draws.
- **Merged modes filter at chunk granularity** — a chunk is one GameObject
  holding many elements, so hiding one hides its chunk. UE has the same
  limitation and warns about it.
- **Merged modes bake instance transforms into vertices**, so element
  GameObjects sit at the identity transform.
- **Only nodes that own merged meshes are hide targets.** Registering every
  spatial node would make hiding one element also hide elements nested beneath
  it, because Unity's visibility walk reaches children while Unreal's
  per-actor hiding does not.
- **Glass and translucent materials are Phase 4**, so merge buckets ignore
  them and merged chunks render double-sided.
  *Superseded.* Phase 4 ported the surface classifier and added the surface kind
  to the merge-bucket key (`FragmentMergeBucket.BuildKey`), so a bucket now holds
  one surface kind and a chunk renders as glass, translucent or opaque. Merged
  chunks are still forced double-sided, mirroring `FragmentsActor.cpp:662`.

## Test reachability gap

All Unity-layer tests live in `Tests~/DotnetRunner/UnityLayerTests`. Unity
ignores folders ending in `~`, so a Unity user running the package's own Test
Runner gets the 89 parser tests from `Tests/Editor` and **no** import-mode,
picking or filtering coverage. They run and pass here, against the stubs; they
are simply not reachable from inside Unity.

Relocating them into `Tests/Editor` so both runners compile the same files is
the right fix, but one obstacle has to be cleared first: the stub exposes a
`Debug.capturedWarnings` hook for asserting warnings, which does not exist in
real Unity (the editor equivalent is `LogAssert.Expect` from
`UnityEngine.TestTools`). Those assertions need an abstraction that satisfies
both before the files can move, and the move cannot be validated without an
editor — so it is listed below rather than done blind.

## Deferred to a Unity editor

The checklists in `Phase1Validation.md` and `Phase2Validation.md` still apply.
Phase 3 adds:

1. Confirm PhysX reports `RaycastHit.triangleIndex` in the same triangle order
   written by `Mesh.SetTriangles` — the tests synthesise the hit, so the
   table-to-element mapping is proven but the engine's index order is not.
2. Confirm Unity can cook a `MeshCollider` for the largest merged chunk
   (Joyson's largest is 318,137 triangles) and measure the cook time.
3. Confirm the vertex-colour shader declares GPU instancing support
   (`#pragma multi_compile_instancing`), or Instanced mode's
   `enableInstancing` flag is inert.
4. Verify `FragmentElementTable`, `FragmentVisibilityIndex` and
   `FragmentElementReference` serialized state survives the ScriptedImporter
   round-trip into the imported asset.
5. Verify hiding actually stops drawing and raycasting in the engine, not just
   in component flags.
6. Measure filter latency on the Architecture model; `Isolate` currently walks
   each target's children on every toggle. Joyson per-body already registers
   14,257 targets, each costing two `GetComponentsInChildren` allocations per
   toggle.
7. Move the Unity-layer tests into `Tests/Editor` behind a log-assertion
   abstraction that works against both `LogAssert` and the stub, then confirm
   they compile and pass in the Unity Test Runner.
8. Confirm `FragmentMaterialFactory.PackageShaderAssetPath` resolves. It
   hard-codes the `Packages/com.fragmentsunity.importer/` mount, so a package
   embedded under `Assets/` instead of consumed via UPM silently falls back to
   the `Standard` shader — an all-white model with only a console warning.
9. Exercise the spawn ceiling. No sample approaches 250,000 objects (the
   largest build is 16,324), so none of its five call sites has ever run.
10. Exercise the merged-index ceiling. The largest real chunk reaches 181,397
    vertices and 958,299 indices — 36% and 24% of the limits — so the
    `MaxMergedIndices` split branch has no coverage from real data.
