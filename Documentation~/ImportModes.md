# Import Modes

Select a `.frag` file in the Project window and the Inspector shows **Import
Mode**; change it and press **Apply** to rebuild the model.

The mode decides how the parsed model becomes GameObjects. It is the single
biggest lever you have over three things that pull against each other: how many
objects the scene carries, how many draw calls it costs, and how finely you can
select and hide what you imported.

## The modes

**Hierarchy Per Body** *(default)*. The full spatial tree — project, site,
building, storey, element — with one GameObject for every body. An element
modelled as three separate solids becomes three objects named `Wall_1234_body1`,
`_body2`, `_body3`. Nothing is welded, so geometry shared between repeated
bodies is still shared: this is the smallest model on disk and the largest object
count. It is the default because it gives up nothing — every element is
individually selectable, hideable and pickable.

**Hierarchy Per Element.** The same tree, but at each element node the element's
bodies are welded into one mesh per colour. One IFC element is one place in the
hierarchy with one or two meshes under it. Far fewer objects than Per Body while
still hiding and selecting per element, which makes it the usual choice once a
model is too large for Per Body.

**Hierarchy Per Storey.** The tree is walked down to `IfcBuildingStorey` and
everything below each storey is welded into a handful of meshes. You get
per-level visibility for a fraction of the objects. Elements remain individually
pickable, but no longer individually hideable.

**Instanced.** No hierarchy at all: one GameObject per body directly under the
model root, over meshes cached per geometry-and-material pair and a material with
`enableInstancing` turned on. The object count matches Per Body, but Unity
batches the repeated geometry — the mode to use when a model is mostly repeated
parts such as curtain-wall panels or fixtures.

**Merged Whole Model.** The entire model is welded into one mesh per bucket of
category, colour, opacity and surface kind, ignoring the spatial tree. Elements
of the same category and colour are still split apart if one is glass and another
is opaque. Fewest objects, fewest draw calls, and no per-element visibility. Pick
it for background context geometry, or a neighbouring building you only need to
see.

## At a glance

| Mode | Objects created | Draw calls | Raycast hits | Hide / isolate granularity |
|---|---|---|---|---|
| Hierarchy Per Body | tree nodes + one per body | one per body | the body | exact, per element |
| Hierarchy Per Element | tree nodes + 1–2 per element | one per element | a per-element chunk | near-exact: per element, except an element that aggregates sub-parts, which hides with them |
| Hierarchy Per Storey | tree nodes down to storey + a few per storey | a few per storey | a per-storey chunk | chunk granular: the storey chunk, not the element |
| Instanced | one per body, no tree | batched by GPU instancing | the body | exact, per element |
| Merged Whole Model | a handful for the model | a handful | a model-wide chunk | chunk granular: a category/colour/surface bucket spanning the model |

## Measured build shapes

The numbers below were measured offline against three real IFC models used
during development. Those files are not distributed with the package, so they
are identified here by size rather than by name. Treat them as illustrative of
the shape each mode produces, not as a prediction for your model.

The small model — 1,113 geometry-bearing elements — imported with picking
enabled:

| Mode | Objects | Hierarchy nodes | Merged chunks | Meshes | Materials |
|---|---|---|---|---|---|
| Hierarchy Per Body | 2,297 | 1,138 | 0 | 128 | 2 |
| Hierarchy Per Element | 43 | 21 | 21 | 21 | 2 |
| Hierarchy Per Storey | 14 | 7 | 6 | 6 | 2 |
| Instanced | 1,159 | 0 | 0 | 128 | 2 |
| Merged Whole Model | 7 | 0 | 6 | 6 | 2 |

The spread from 2,297 objects to 7 is the whole point of the setting.

## Object count and draw calls

Every drawable object carries one `MeshRenderer`, so the object count is
effectively the draw-call count — except in Instanced mode, where objects share
meshes and an instancing-enabled material and collapse into far fewer batches.

Materials never multiply with model size, because colour travels in vertex
colours rather than in the material. What the material *does* carry is the
surface it renders and which faces it draws, so a model of any size is drawn with
**at most six materials**: three surface kinds — opaque, translucent and glass —
each in a single-sided and a double-sided variant.

Two things decide which of the six an object gets:

- **Surface kind** is read from the IFC colour and opacity. A blue-dominant hue,
  or an opacity below 0.5, is glass; anything else below full opacity is
  translucent; the rest is opaque. Glass classified purely by hue arrives fully
  opaque, so its alpha is forced to 0.5 to make it visibly glass.
- **Sidedness** comes from the instance's rendered-faces flag for objects spawned
  one body at a time, and is forced on for every merged chunk — a welded chunk
  mixes bodies from many instances, so it is always double-sided.

Six is the ceiling, not the norm. Across the three development models:

| Model | Per Body | Per Element | Per Storey | Instanced | Merged |
|---|---|---|---|---|---|
| Small (1.1k elements) | 2 | 2 | 2 | 2 | 2 |
| Medium (2.0k elements) | 3 | 4 | 3 | 3 | 3 |
| Large (15.2k elements) | 3 | 4 | 3 | 3 | 3 |

None of the three declares a two-sided face flag, so their single-sided variants
only ever appear on unmerged bodies. Per Element reaches four on the two larger
models because elements missing from the spatial tree are spawned as individual,
single-sided bodies beside merged, double-sided element chunks.

Merging is not free. Welding bakes each body's vertices into world space, so
geometry that dozens of instances used to share is duplicated per instance: the
merged modes produce fewer, fatter meshes and a larger asset on disk. Smallest on
disk with the most objects, or fewest objects with the largest asset — that is
the trade.

Two ceilings are worth knowing. A build stops after 250,000 objects and logs an
error, which is what a large model in Per Body or Instanced mode will hit — move
it to Per Storey or Merged. Merged meshes are split into new chunks at 500,000
vertices or 4,000,000 indices, so "one merged mesh" is in practice a small series
of them.

## When there is no spatial tree

The hierarchy modes need a spatial tree. A `.frag` file that carries none is
built flat, exactly like Instanced — one GameObject per body directly under the
model root — whichever hierarchy mode you asked for.

**Nothing in the import log announces that fallback.** No line reports it, and
the `Scene built (…)` summary still names the mode you selected, not the flat
build you got. Two things give it away, and they are all you have:

- That same summary line reports `0 hierarchy nodes`. Any real hierarchy build
  reports more.
- `FragmentVisibilityIndex.SupportsElementFiltering` returns true even in Per
  Storey or Merged Whole Model, because a flat build gives every body its own
  object and so filters exactly.

If a model imports with the mode you asked for but no spatial tree in the
Hierarchy window, this is why: the file carried no tree to walk.

## Picking granularity

Every mode resolves a raycast back to the element that was hit, including the
merged ones. Unity's `MeshCollider` reports `triangleIndex` on a hit, so a merged
chunk carries a triangle-start table and a binary search recovers the element.

What differs between the modes is the *GameObject* you hit.
`hit.collider.gameObject` is the element itself in Per Body and Instanced mode,
and a shared merged chunk in the three merged modes — so never treat the hit
object as the element. Ask `FragmentPicker` instead; see [Picking.md](Picking.md).

Colliders only exist when **Enable Element Picking** was on at import. Turn it
off for context geometry you never click: it removes one `MeshCollider` per
object and shrinks the imported asset noticeably.

## Filtering granularity

`FragmentFilter` is attached to the model root at import, next to
`FragmentModel`, so category, storey and attribute filtering works without any
setup. What it can achieve depends on the mode, because **a merged chunk hides as
a unit**.

The visibility index registers every element inside a chunk against that one
chunk object. Hiding any of them hides the chunk, and every other element welded
into it goes with it. In Per Storey mode the practical unit is the storey; in
Merged Whole Model mode it is a category/colour/surface bucket spanning the
model. Per Element mode is safe in normal use — a chunk holds one element — with
the exception of an element that aggregates sub-parts in the spatial tree, whose
parts are welded in with it.

An isolate in a chunk-granular mode therefore *under*-hides as well as
over-hides: a chunk is kept whenever any one of its elements is wanted, so every
other element welded into that chunk stays on screen.

Measured on the medium model — 1,986 geometry-bearing elements — isolating a
storey that holds 887 of them and leaves 1,099 outside it:

| Mode | Out-of-storey elements still drawn |
|---|---|
| Hierarchy Per Body | 0 |
| Instanced | 0 |
| Hierarchy Per Element | 0 |
| Hierarchy Per Storey | 1 |
| Merged Whole Model | **385 of 1,099** |

Nothing leaks in Per Body mode, where each object draws exactly one element.

`FragmentVisibilityIndex.SupportsElementFiltering` reports whether the scene was
built one-object-per-element (Per Body, Per Element, Instanced) or in chunks
(Per Storey, Merged Whole Model). It reads the build, not the setting: a merged
mode that fell back to a flat build for want of a spatial tree gave every body
its own object, and reports element granularity. When it is false, every
`FragmentFilter` call logs one warning saying filtering can only isolate whole
chunks. It is a warning, not an error: the filter still runs.

If you need to isolate arbitrary sets of elements, import in Per Body, Per
Element or Instanced mode. `FragmentFilter.SetVisibleLocalIds(ids, visible)` is
the toggle primitive behind `SetCategoryVisible` and `SetStoreyVisible`, and
`IsolateLocalIds` replaces the whole visible set. See
[ScriptingApi.md](ScriptingApi.md).
