# Fragments for Unity

Fragments for Unity imports ThatOpen **Fragments 2.0** (`.frag`) BIM files into
Unity as ordinary Unity content. It is pure C# — no native plugin, no external
process, no runtime service. A `.frag` file dropped into the Project window
becomes a GameObject hierarchy with meshes, materials and the model's full IFC
metadata, exactly like importing an FBX.

What arrives with the geometry:

- **Identity** — GlobalId, IFC category, name, express id, and the type object.
- **Property sets and quantity sets**, including the ones inherited from the
  element's type, flagged so you can tell them apart.
- **Materials** with layer names and layer thicknesses, and classifications.
- **The spatial hierarchy** — Project → Site → Building → Storey → Element.
- **Original IFC colours**, baked into vertex colours, at meter scale, in
  Unity's left-handed space with correct winding.

## Requirements

- Unity **2021.3** or newer, API Compatibility Level **.NET Standard 2.1**.
- `com.unity.nuget.newtonsoft-json` 3.2.1, pulled in automatically as a package
  dependency.
- Built-in Render Pipeline or URP. See [Limitations](#limitations) for HDRP.

## Installation

Install it as a UPM package, so it lands under `Packages/`:

- **From disk** — *Window > Package Manager > + > Add package from disk*, then
  select this package's `package.json`.
- **From git** — *Window > Package Manager > + > Add package from git URL*, then
  paste the repository URL.

Either way the package mounts at
`Packages/com.fragmentsunity.importer/`, which is where the importer looks for
its shader.

> **Do not copy the package into `Assets/`.** The importer resolves the
> vertex-colour shader by its package asset path
> (`Packages/com.fragmentsunity.importer/Runtime/Shaders/…`) so it can declare an
> import dependency on it. Under `Assets/` that path does not exist, the lookup
> returns null, and the import falls back to `Shader.Find` — which is unreliable
> during a clean import (the shader may not be compiled yet). The symptom is an
> all-white model with a single console warning saying the shader was not found.
> If you must embed the package, expect to re-import your models once the shader
> has compiled.

## Quick start

1. Drag a `.frag` file into the Project window.
2. Unity imports it and logs the counts:
   `Imported 'AR520': 128 geometries, 1158 instances, 10080 vertices, 7536 triangles`.
3. Expand the imported asset. The main object is a GameObject hierarchy; under
   it sit the meshes, the materials, and a `ModelData` object holding the
   metadata.
4. Drag the asset into a scene. It is a normal GameObject tree — move it, light
   it, and it renders with its IFC colours.

The model root carries `FragmentModel`, which answers metadata queries,
`FragmentFilter`, which shows and hides parts of the model, and
`FragmentVisibilityIndex`, the map from element to the objects that draw it.

## Importer settings

Select the `.frag` asset and the Inspector shows the importer's settings. Change
any of them and press **Apply** to rebuild the model.

| Setting | Default | What it does |
|---|---|---|
| **Scale Factor** | `1` | Multiplies every coordinate. Fragments are metres and so is Unity, so leave it at 1 unless your project works at another scale. |
| **Import Metadata** | on | Parses attributes, property sets, materials, classifications and the spatial tree, and stores them in the model's `ModelData` sub-asset. Off means geometry only — no queries, no inspector data, no filtering by category or storey. |
| **Import Property Sets** | on | Walks `IsDefinedBy` into property and quantity sets, including type-inherited ones. Turning it off keeps identity and the hierarchy but drops the property tables, which is the bulk of the metadata payload. |
| **Import Mode** | Hierarchy — per Element | How the parsed model becomes GameObjects: per body, per element, per storey, instanced, or one merged model. This is the biggest lever over object count, draw calls, and how finely you can select and hide. See [ImportModes.md](ImportModes.md). |
| **Enable Element Picking** | on | Adds a `MeshCollider` to every drawable so raycasts can resolve elements. Off removes the colliders and shrinks the imported asset; the model is then not pickable at all. See [Picking.md](Picking.md). |
| **Collider Layer** | `0` (Default) | The layer every collider-bearing object is moved onto. Leaving it at Default makes the building solid to your whole project — read [Picking.md](Picking.md) before shipping a scene. |

Import shows a cancellable progress bar once it starts building the scene.
Cancelling discards the whole build and reports an import error; nothing
half-built is written. Parsing the file happens before that, with no progress
and no way to cancel — on a very large `.frag` the editor is unresponsive until
the parse finishes.

## Querying metadata from C#

`FragmentModel` on the model root is the query surface. It deserializes its
metadata on first access and caches it.

```csharp
using System.Collections.Generic;
using FragmentsUnity;
using UnityEngine;

public sealed class ModelReport : MonoBehaviour
{
    [SerializeField] private FragmentModel _model;

    private void Start()
    {
        FragmentItemMetadata door = _model.FindByGlobalId("1MXYWRlOjEm8W0fkpMpNTl");
        Debug.Log($"{door.Category} '{door.Name}' on {door.StoreyName}");

        List<int> walls = _model.FindByCategory("IFCWALL");
        List<int> groundFloor = _model.FindByStorey("GF-GROUND FLOOR - FFL");
        Debug.Log($"{walls.Count} walls, {groundFloor.Count} elements on the ground floor");

        foreach (KeyValuePair<string, string> value in _model.GetFlattenedValues(door.LocalId))
        {
            Debug.Log($"{value.Key} = {value.Value}");
        }
    }
}
```

- `FindByGlobalId(string)` returns one `FragmentItemMetadata`, matched
  case-insensitively, or null.
- `FindByLocalId(int)` is the same lookup by the dense id that picking returns.
- `FindByCategory(string)` and `FindByStorey(string)` return local ids.
  `FindByStorey` includes the storey element itself as well as everything
  contained in it.
- `FindByAttribute(name, value, exactMatch)` searches direct attributes first,
  then property-set members. An empty value matches every element that carries
  the attribute at all.
- `GetCategoryCounts()` and `GetStoreyCounts()` return name → count maps, which
  is what the filter window lists.
- `GetFlattenedValues(localId)` flattens one element's attributes, property sets
  (`SetName.PropertyName`), classifications, materials and placement into a
  single name → value dictionary — the fastest way to dump everything known
  about an element.

Queries run over the elements that **carry geometry**, deduplicated in
first-instance order. That mirrors FragmentsUE and is why `GetCategoryCounts`
returns building elements rather than every IFC record in the file.

## Filtering

`FragmentFilter`, on the model root, drives visibility:

```csharp
var filter = model.GetComponent<FragmentFilter>();

filter.IsolateByStorey("Level 1");                  // show one storey
filter.SetCategoryVisible("IFCWALL", false);        // hide all walls
filter.IsolateByAttribute("FireRating", "REI 60", exactMatch: true);
filter.IsolateLocalIds(new[] { 1234 });             // show one element
filter.ClearFilter();                               // everything back
```

Hiding turns off renderers *and* colliders, so hidden elements are not pickable
either. How precisely a filter can hide depends on the import mode: merged modes
hide a whole chunk at a time. [ImportModes.md](ImportModes.md) has the numbers.

## Editor tooling

**Model inspector.** Select the model root and the Inspector shows the model
name and guid, item/category/storey counts, a **Find Global Id** search box, and
the full IFC record of whichever element is selected in the scene — attributes,
property sets, materials, classifications — each section foldable, with a button
that copies the record to the clipboard.

**Element inspector.** Select any spawned element and the same record is shown
inline on its `FragmentElementReference`.

**Filter window.** *Window > Fragments > Filter* opens a dockable window listing
the selected model's levels and categories with element counts. Toggle a row to
show or hide it, press **Isolate** to show only it, filter by attribute name and
value, and **Clear Filter** to restore the model.

**Asset extraction.** Right-click an imported model (or use the **Assets** menu)
and choose *Fragments > Extract Meshes and Materials*. The importer owns meshes
and materials as sub-assets of the `.frag`, which means they are read-only and
vanish on re-import. Extraction copies them into standalone assets you can edit
and reference from prefabs, materials, and other scenes:

```
Assets/Fragments/<ModelName>/Meshes/<name>.asset
Assets/Fragments/<ModelName>/Materials/<name>.mat
```

Names are sanitized to letters, digits and underscores, clamped to 64
characters, and made unique within the folder; nothing existing is overwritten.
The same operation is available from code as
`FragmentAssetExtractor.ExtractMeshes(model, folder)` and
`ExtractMaterials(model, folder)`, both returning how many assets were written.
Extracted copies are independent: re-importing the `.frag` does not update them.

## Limitations

**No part of this package has ever been executed in a Unity editor.** The
parser and the scene-building logic are covered by 444 automated tests that run
under a plain .NET SDK against hand-written stubs of the Unity API, and the
parser's output is cross-checked against FragmentsUE's own run logs where a
reference exists. That is the whole of the evidence. The two shaders have never
been compiled by Unity's shader compiler; the `ScriptedImporter` has never run;
the inspectors, the filter window and the asset-extraction command have never
been drawn or clicked. The `Phase*Validation.md` records list, item by item,
what each phase measured and what is still waiting on a real editor. Treat
everything under *Editor tooling* above as untried until you have tried it.

**Rebar and other circular extrusions are not imported.** Geometry stored as
`CircleExtrusion` (typically reinforcement bars, some pipework and cable trays)
is skipped, and the import log reports how many were skipped. This matches
FragmentsUE, which skips them too. Everything shell-based imports normally.

**Merged import modes filter at chunk granularity.** In *Hierarchy — per Storey*
and *Merged Whole Model*, many elements share one welded mesh, so hiding one
element hides the chunk it belongs to, and every other element in that chunk
stays visible when the chunk is kept. Picking still resolves the exact element.
If you need to hide arbitrary sets of elements, import per Body, per Element, or
Instanced. `FragmentVisibilityIndex.SupportsElementFiltering` reports which you
got, and `FragmentFilter` logs a warning when a filter cannot be exact.

**An imported model is solid until you give it a layer.** Colliders are ordinary
non-convex `MeshCollider`s, which cannot be triggers, so out of the box the
building blocks character controllers, answers every unrelated raycast, and bakes
into the NavMesh. Set **Collider Layer** to a layer you exclude in the physics
collision matrix. [Picking.md](Picking.md) walks through it.

**Metadata is only as good as the export.** If the `.frag` was written without
properties, the elements arrive with identity and geometry but no property sets,
and the inspector says so. Nothing in the importer can recover data the file does
not contain.

**A file with no spatial tree is built flat, silently.** Whichever hierarchy
mode you pick, a `.frag` that carries no spatial structure is built like
Instanced — one GameObject per body under the model root. No log line reports
the fallback, and the `Scene built (…)` summary still names the mode you asked
for. The one tell is that the same line reports `0 hierarchy nodes`.
[ImportModes.md](ImportModes.md) has the detail.

**Built-in and URP only.** Two vertex-colour shaders ship, one for the Built-in
Render Pipeline and one for URP, chosen by whether a render pipeline asset is
assigned. There is **no HDRP variant** — an HDRP project gets the URP shader
selected and will show magenta materials until you author an HDRP equivalent.
The package is developed against Unity 2021.3.

**Practical ceilings.** A build stops after 250,000 GameObjects, and merged
meshes split into new chunks at 500,000 vertices or 4,000,000 indices. Large
models in per-Body or Instanced mode are the ones that hit the first ceiling;
move them to per-Storey or Merged.

**There are no `.meta` files in the package yet.** Unity generates a `.meta`
file, and with it a GUID, for every asset the first time it sees one. Because
none are committed, two machines that import this package get different GUIDs
for the same shader, script and sample — so a scene or prefab referencing them
in one checkout resolves to nothing in another. Open the package in Unity once,
let it generate the `.meta` files, and commit them before anyone builds content
against it. It is also why the Basic Import sample ships a script instead of a
scene.

**The `.frag` schema's redistribution terms are unresolved.**
`ThirdParty/index.fbs`, and the `index_codegen.fbs` variant beside it, are
ThatOpen Components' format description, vendored here so the FlatBuffers
bindings can be regenerated. `ThirdParty/LICENSES.md` flags
that redistributing it may carry terms this package has not established. The
code in `Runtime/Generated/` is output of `flatc`, not ThatOpen source, and
carries no FlatBuffers obligation of its own; the schema file itself is the open
question. Check ThatOpen's current licence before redistributing this package
with the schema in it.

## Further reading

- [ImportModes.md](ImportModes.md) — what each mode costs and what it gives up.
- [Picking.md](Picking.md) — raycasting to elements, and the collider layer.
- `Phase1Validation.md` … `Phase4Validation.md` — how each phase of the port was
  validated against the file format and against FragmentsUE, and what still
  needs checking inside a real editor.
- `CHANGELOG.md` at the package root.
