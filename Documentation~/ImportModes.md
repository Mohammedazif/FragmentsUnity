# Import Modes

A `.frag` file is imported by `FragmentModelImporter`, a `ScriptedImporter`.
Select the file in the Project window and the Inspector shows **Import Mode**,
**Enable Element Picking** and **Collider Layer**; press Apply and the model is
rebuilt.

The mode decides how the parsed model becomes GameObjects. It is the single
biggest lever you have over three things that pull against each other: how many
objects the scene carries, how many draw calls it costs, and how finely you can
select and hide what you imported.

## The modes

**Hierarchy — per Body.** The full spatial tree (project → site → building →
storey → element), with one GameObject for every body. An element modelled as
three separate solids becomes three objects named `Wall_1234_body1`,
`_body2`, `_body3`. Nothing is welded, so geometry shared between repeated
bodies is still shared: this is the smallest model on disk and the largest
object count. Pick it for small models, or when you need to address individual
solids.

**Hierarchy — per Element (default).** The same tree, but at each element node
the element's bodies are welded into one mesh per colour. One IFC element is
one place in the hierarchy with one or two meshes under it. This is the best
granularity/performance balance in Unity and the reason it is the default.

**Hierarchy — per Storey.** The tree is walked down to `IfcBuildingStorey` and
everything below each storey is welded into a handful of meshes. You get
per-level visibility for a fraction of the objects. Elements remain
individually pickable (see below) but no longer individually hideable.

**Instanced.** No hierarchy at all: one GameObject per body directly under the
model root, over meshes cached per geometry+material pair and a material with
`enableInstancing` turned on. The object count matches per-Body, but Unity
batches the repeated geometry — the mode to use when a model is mostly repeated
parts (curtain-wall panels, fixtures, rebar).

**Merged Whole Model.** The entire model is welded into one mesh per bucket of
category, colour, opacity and surface kind, ignoring the spatial tree. Elements
of the same category and colour are still split apart if one is glass and
another is opaque. Fewest objects, fewest draw
calls, and no per-element visibility. Pick it for background context geometry,
or a whole neighbouring building you only need to see.

FragmentsUE's sixth mode, *Procedural Mesh*, has no Unity counterpart: Unity
builds every mesh through the same `Mesh` API, so the mode would be identical
to the hierarchy path (UNITY_PORT.md section 6).

## At a glance

| Mode | Objects created | Draw calls | Raycast hits | Hide / isolate granularity |
|---|---|---|---|---|
| Hierarchy — per Body | tree nodes + one per body | one per body | the body | exact, per element |
| Hierarchy — per Element | tree nodes + 1–2 per element | one per element | a per-element chunk | near-exact: per element, except an element that aggregates sub-parts, which hides with them |
| Hierarchy — per Storey | tree nodes down to storey + a few per storey | a few per storey | a per-storey chunk | chunk granular: the storey chunk, not the element |
| Instanced | one per body, no tree | batched by GPU instancing | the body | exact, per element |
| Merged Whole Model | a handful for the model | a handful | a model-wide chunk | chunk granular: a category/colour/surface bucket spanning the model |

## Object count and draw calls

Every drawable object carries one `MeshRenderer`, so the object count is
effectively the draw-call count — except in Instanced mode, where objects share
meshes and an instancing-enabled material and collapse into far fewer batches.

Materials never multiply with model size, because colour travels in vertex
colours rather than in the material. What the material *does* carry is the
surface it renders and which faces it draws, so the whole model is drawn with
**at most six materials**: three surface kinds — opaque, translucent, glass —
each in a single-sided and a double-sided variant. That is the full key of the
material cache in `FragmentSceneSpawnContext`, and it does not grow with the
model.

Two things decide which of the six an object gets:

- **Surface kind** comes from `FragmentSurfaceClassifier`, which reads the IFC
  colour and opacity: a blue-dominant hue or an opacity under 0.5 is glass,
  anything else below full opacity is translucent, the rest is opaque. Glass
  classified purely by hue arrives fully opaque, so it is forced down to 0.5
  alpha to be visibly glass.
- **Sidedness** comes from the instance's `RenderedFaces` flag for objects
  spawned one body at a time, and is forced on for every merged chunk — a
  welded chunk mixes bodies from many instances, so it is always double-sided.

Six is the ceiling, not the norm. Measured on the three development models:

| Model | per Body | per Element | per Storey | Instanced | Merged |
|---|---|---|---|---|---|
| AR520 | 2 | 2 | 2 | 2 | 2 |
| Joyson Model | 3 | 4 | 3 | 3 | 3 |
| 20210219Architecture | 3 | 4 | 3 | 3 | 3 |

None of the three files declares a two-sided face flag, so their single-sided
variants only ever appear on unmerged bodies. Per-Element reaches four on the
two larger models because elements missing from the spatial tree are spawned as
individual bodies — single-sided — beside merged, double-sided element chunks.

Merging is not free. Welding bakes each body's vertices into world space, so
geometry that dozens of instances used to share is duplicated per instance: the
merged modes produce fewer, fatter meshes and a larger asset on disk. That is
the trade the UE plugin describes as "smallest on disk, most actors" versus
"fewer actors, larger on disk", and it applies here unchanged.

Two ceilings are worth knowing. A build stops after 250,000 objects and logs an
error, which is what a large model in per-Body or Instanced mode will hit — move
it to per-Storey or Merged. Merged meshes are split into new chunks at 500,000
vertices or 4,000,000 indices, so "one merged mesh" is in practice a small
series of them.

The hierarchy modes need a spatial tree. A `.frag` file that carries none is
built flat, exactly like Instanced — one GameObject per body directly under the
model root — whichever hierarchy mode you asked for.

**Nothing in the import log announces that fallback.** No line reports it, and
the `Scene built (…)` summary still names the mode you selected, not the flat
build you got. Two things do give it away, and they are all you have:

- That same summary line reports `0 hierarchy nodes`. Any real hierarchy build
  reports more.
- `FragmentVisibilityIndex.SupportsElementFiltering` returns true even in
  per-Storey or Merged Whole Model, because a flat build gives every body its
  own object and so filters exactly.

If a model imports with the mode you asked for but no spatial tree in the
Hierarchy window, this is why: the file carried no tree to walk.

## Picking granularity

Every mode resolves a raycast back to the element that was hit, including the
merged ones. Unity's `MeshCollider` reports `triangleIndex` on a hit, so a
merged chunk carries a triangle-start table and a binary search recovers the
element. This is a deliberate improvement over FragmentsUE, whose merged modes
are documented as losing element identity because Unreal needs a UV0 encoding
and a project-wide physics setting to get there.

What differs between the modes is the *GameObject* you hit.
`hit.collider.gameObject` is the element itself in per-Body and Instanced mode,
and a shared merged chunk in the three merged modes — so never treat the hit
object as the element. Ask `FragmentPicker` instead; see `Picking.md`.

Colliders only exist when **Enable Element Picking** was on at import. Turn it
off for context geometry you never click: it removes one `MeshCollider` per
object and shrinks the imported asset noticeably.

## Filtering granularity

`FragmentFilter` is attached to the model root at import, next to
`FragmentModel`, so category / storey / attribute filtering works without any
setup. What it can achieve depends on the mode, because **a merged chunk hides
as a unit**.

The visibility index registers every element inside a chunk against that one
chunk object. Hiding any of them hides the chunk, and every other element welded
into it goes with it. In per-Storey mode the practical unit is the storey; in
Merged Whole Model mode it is a category/colour/surface bucket spanning the
model. Per
Element mode is safe in normal use — a chunk holds one element — with the
exception of an element that aggregates sub-parts in the spatial tree, whose
parts are welded in with it.

An isolate in a chunk-granular mode therefore *under*-hides as well as
over-hides: a chunk is kept whenever any one of its elements is wanted, so every
other element welded into that chunk stays on screen. Measured on the Joyson
sample imported as Merged Whole Model, `IsolateByStorey("Level 1")` records all
1,099 out-of-storey elements as hidden, yet 385 of them are still drawn, because
they share a kept chunk with a Level 1 element. Nothing leaks in per-Body mode,
where each object draws exactly one element.
`FragmentFilterIntegrationTests` pins those numbers.

`FragmentVisibilityIndex.SupportsElementFiltering` reports whether the scene was
built one-object-per-element (per-Body, per-Element, Instanced) or in chunks
(per-Storey, Merged Whole Model). It reads the build, not the setting: a merged
mode that fell back to a flat build for want of a spatial tree gave every body
its own object, and reports element granularity. When it is false, every
`FragmentFilter` call logs one warning saying filtering can only isolate whole
chunks — mirroring the refusal FragmentsUE makes at
`FragmentsActor.cpp:1187-1193`. It is a warning, not an error: the filter still
runs.

FragmentsUE refuses filtering outright in Instanced and Merged Whole Model
(`SupportsFiltering`, `FragmentsActor.cpp:1071-1074`) because those modes spawn
no child actors there. This port diverges deliberately: Instanced spawns one
GameObject per body, so it filters exactly, and the merged modes filter at chunk
granularity with the warning above rather than not at all.

If you need to isolate arbitrary sets of elements, import in per-Body,
per-Element, or Instanced mode. `FragmentFilter.SetVisibleLocalIds(ids, visible)`
is the toggle primitive behind `SetCategoryVisible` and `SetStoreyVisible`, and
`IsolateLocalIds` replaces the whole visible set.
