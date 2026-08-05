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
- **Original IFC colours**, baked into vertex colours, at metre scale, in
  Unity's left-handed space with correct winding.

## Contents

- [Requirements](#requirements)
- [Installation](#installation)
- [Quick start](#quick-start)
- [Importer settings](#importer-settings)
- [Import modes](ImportModes.md) — what each mode costs and what it gives up
- [Element picking](Picking.md) — raycasting to elements, and the collider layer
- [Scripting API](ScriptingApi.md) — queries, picking and filtering from C#
- [Editor tooling](#editor-tooling)
- [Tests](#tests)
- [Verification status](#verification-status)
- [Limitations](#limitations)

## Requirements

- Unity **6.2 (6000.2)** or newer is what this package has been run on. The
  manifest declares a lower floor of 2021.3 — see
  [Verification status](#verification-status) before relying on it.
- API Compatibility Level **.NET Standard 2.1**.
- The **Built-in Render Pipeline** or **URP**. There is no HDRP shader; see
  [Limitations](#limitations).
- `com.unity.nuget.newtonsoft-json` 3.2.1, resolved automatically as a package
  dependency.

## Installation

Install it as a UPM package, so it mounts under `Packages/`.

**From a git URL** — *Window > Package Manager > + > Add package from git URL*,
then paste:

```
https://github.com/Mohammedazif/FragmentsUnity.git
```

**From disk** — *Window > Package Manager > + > Add package from disk*, then
select this package's `package.json`.

Either way the package mounts at `Packages/com.fragmentsunity.importer/`, which
is where the importer looks for its shader.

> **Do not copy the package into `Assets/`.** The importer resolves the
> vertex-colour shader by its package asset path
> (`Packages/com.fragmentsunity.importer/Runtime/Shaders/…`) so it can declare an
> import dependency on it. Under `Assets/` that path does not exist, the lookup
> returns null, and the import falls back to `Shader.Find` — which is unreliable
> during a clean import, because the shader may not be compiled yet. The symptom
> is an all-white model with a single console warning saying the shader was not
> found. If you must embed the package, expect to re-import your models once the
> shader has compiled.

## Quick start

1. Drag a `.frag` file into the Project window.
2. Unity imports it and logs the counts it built:
   `Imported '<model>': <n> geometries, <n> instances, <n> vertices, <n> triangles`.
3. Expand the imported asset. The main object is a GameObject hierarchy; under
   it sit the meshes, the materials, and a `ModelData` object holding the
   metadata.
4. Drag the asset into a scene. It is a normal GameObject tree — move it, light
   it, and it renders with its IFC colours.
5. Select any element under the model root. The Inspector shows its full IFC
   record.

The model root carries three components: `FragmentModel`, which answers metadata
queries, `FragmentFilter`, which shows and hides parts of the model, and
`FragmentVisibilityIndex`, the map from element to the objects that draw it.

## Importer settings

Select the `.frag` asset and the Inspector shows the importer's settings. Change
any of them and press **Apply** to rebuild the model.

| Setting | Default | What it does | What it costs |
|---|---|---|---|
| **Scale Factor** | `1` | Multiplies every coordinate. Fragments are metres and so is Unity, so leave it at 1 unless your project works at another scale. | Nothing. |
| **Import Metadata** | on | Parses attributes, property sets, materials, classifications and the spatial tree into the model's `ModelData` sub-asset. | Most of the import time and nearly all of the asset size. Off means geometry only: no queries, no inspector record, no filtering by category or storey. |
| **Import Property Sets** | on | Walks `IsDefinedBy` into property and quantity sets, including type-inherited ones. | The bulk of the metadata payload. Off keeps identity and the hierarchy but drops the property tables. |
| **Import Mode** | Hierarchy Per Body | How the parsed model becomes GameObjects. The biggest lever over object count, draw calls, and how finely you can select and hide. | See [ImportModes.md](ImportModes.md). |
| **Enable Element Picking** | on | Adds a `MeshCollider` to every drawable so raycasts can resolve elements. | One collider per drawable, plus collider cook time at import and asset size. Off means the model is not pickable at all. See [Picking.md](Picking.md). |
| **Collider Layer** | `0` (Default) | The layer index every collider-bearing object is moved onto. | Leaving it at Default makes the building solid to your whole project. Read [Picking.md](Picking.md) before shipping a scene. |

**Import Mode** is drawn as a plain enum popup, so the options read
*Hierarchy Per Body*, *Hierarchy Per Element*, *Hierarchy Per Storey*,
*Instanced* and *Merged Whole Model*. **Collider Layer** is drawn as a plain
integer field, not a layer dropdown — enter the layer's index, which
*Project Settings > Tags and Layers* lists beside its name.

Import shows a cancellable progress bar once it starts building the scene.
Cancelling discards the whole build and reports an import error; nothing
half-built is written. Parsing the file happens before that, with no progress
and no way to cancel — on a very large `.frag` the editor is unresponsive until
the parse finishes.

## Editor tooling

**Model inspector.** Select the model root and the Inspector shows the model
name and guid, item / category / storey counts, a **Find Global Id** search box,
and the full IFC record of whichever element is selected in the scene —
attributes, property sets, materials, classifications — each section foldable,
with a button that copies the record to the clipboard.

**Element inspector.** Select any spawned element and the same record is shown
inline on its `FragmentElementReference`: IFC class, name, GlobalId, type,
container, storey, local id, attributes, property sets and materials.

**Merged-chunk inspector.** In the merged modes a drawable is a chunk holding
many elements rather than one element, and it carries a `FragmentElementTable`
instead of a `FragmentElementReference`. Its inspector reports how many elements
are welded into the chunk and gives you a popup to pick one of them; the same
full IFC record is then drawn for whichever you select. Very large chunks list
only the first n elements and say how many are not shown.

**Filter window.** *Window > Fragments > Filter* opens a dockable window listing
the selected model's levels and categories with element counts. Toggle a row to
show or hide it, press **Isolate** to show only it, filter by attribute name and
value, and **Clear Filter** to restore the model.

**Asset extraction.** Right-click an imported model, or use the **Assets** menu,
and choose *Fragments > Extract Meshes and Materials*. The importer owns meshes
and materials as sub-assets of the `.frag`, which means they are read-only and
are replaced on re-import. Extraction copies them into standalone assets you can
edit and reference from prefabs, materials and other scenes:

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

## Tests

The package ships an automated test suite. It does not compile into a consuming
project: `Tests/` is gated behind a `UNITY_INCLUDE_TESTS` define constraint with
`autoReferenced: false`, and `Tests~/` is tilde-hidden so Unity ignores it
entirely.

**In the Unity Test Runner.** Add the package to the consuming project's
`Packages/manifest.json`:

```json
"testables": [ "com.fragmentsunity.importer" ]
```

Then open *Window > General > Test Runner* and run the **EditMode** tests. Tests
that read sample models look for `FRAGMENTSUNITY_SAMPLE_DIR` in the environment
and skip when it is unset, so set it before launching the editor if you want
them.

**Offline, without Unity.** `Tests~/DotnetRunner` is a .NET harness that
compiles the parser core and runs the same suite under a plain .NET SDK:

```
dotnet test Tests~/DotnetRunner/Tests/FragmentsUnity.Tests.csproj
```

Tests that read sample models are skipped unless `FRAGMENTSUNITY_SAMPLE_DIR`
points at a directory holding them. A second project under the same folder,
`UnityLayerTests`, exercises `Runtime/Unity` against hand-written stubs of the
Unity API; see `Tests~/DotnetRunner/README.md`.

**Verified count:** the parser-core suite is **89 tests**, all passing, run
offline against real `.frag` models. The Unity-layer suite is larger, but it
does not currently compile against the checked-in stubs, so no total is quoted
here.

## Verification status

This package has been run in the Unity editor. Being precise about what that
covered matters more than a broad claim, so:

**Confirmed working** in Unity **6.2 (6000.2.10f1)** and **6.5 (6000.5.6f1)**,
on Windows, DX11:

- `.frag` import through the `ScriptedImporter`. On one development model the
  editor console reported 128 geometries, 1,158 instances, 10,080 vertices and
  7,536 triangles — matching the offline .NET harness figures for that file
  exactly.
- Shaders compile, and geometry renders in its original IFC colours.
- Several imported models, and several instances of one model, in a single
  scene.
- The model root carries `FragmentModel`, `FragmentFilter` and
  `FragmentVisibilityIndex`, populated. The same model reported 1,113 items,
  5 categories and 1 storey.
- The model root inspector, including the **Find Global Id** box.
- The element inspector, drawing a complete record: IFC class, name, GlobalId,
  type, container, storey, local id, attributes, property sets, materials, and
  the **Copy metadata** button.
- Importing the Basic Import sample from the Package Manager.

**Not yet confirmed:**

- **The declared Unity 2021.3 floor.** `package.json` declares it; only 6.2 and
  6.5 have been run. Everything between is untested, and URP in particular
  differs across those versions — see [Limitations](#limitations).
- **Which render pipeline was active** in those sessions is not recorded here.
  Geometry rendered correctly, so the shader for the pipeline in use compiled
  and ran; but the Built-in and URP shaders are not individually confirmed.
- **The merged import modes** — *Hierarchy Per Storey* and *Merged Whole Model*
  — and the merged-chunk inspector.
- **The filter window.**
- **The asset-extraction command.** Its path planning is covered by tests; the
  half that calls `AssetDatabase.CreateFolder`, `CreateAsset` and `SaveAssets`
  has not been run.
- **Play-mode picking.** `FragmentPicker` is covered by offline tests over
  synthesised hits, so the table-to-element mapping is proven, but PhysX
  reporting `RaycastHit.triangleIndex` in the same order the mesh was written
  has not been observed in a running scene.

No performance figures, benchmarks or platform claims appear anywhere in this
documentation that were not measured.

## Limitations

**Rebar and other circular extrusions are not imported.** Geometry stored as
`CircleExtrusion` — typically reinforcement bars, some pipework and cable trays
— is skipped, and the import log reports how many were skipped. Everything
shell-based imports normally.

**Merged import modes filter at chunk granularity.** In *Hierarchy Per Storey*
and *Merged Whole Model*, many elements share one welded mesh, so hiding one
element hides the chunk it belongs to, and every other element welded into a
kept chunk stays visible. Picking still resolves the exact element.
`FragmentVisibilityIndex.SupportsElementFiltering` reports which kind of build
you got, and `FragmentFilter` logs a warning when a filter cannot be exact. If
you need to hide arbitrary sets of elements, import Per Body, Per Element or
Instanced. [ImportModes.md](ImportModes.md) has the measured leak.

**An imported model is solid until you give it a layer.** Colliders are ordinary
non-convex `MeshCollider`s, which cannot be triggers, so out of the box the
building blocks character controllers, answers every unrelated raycast, and bakes
into the NavMesh. Set **Collider Layer** to a layer you exclude in the physics
collision matrix. [Picking.md](Picking.md) walks through it.

**Metadata is only as good as the export.** If the `.frag` was written without
properties, elements arrive with identity and geometry but no property sets, and
the inspector says so. Nothing in the importer can recover data the file does not
contain.

**A file with no spatial tree is built flat, silently.** Whichever hierarchy mode
you pick, a `.frag` carrying no spatial structure is built like Instanced — one
GameObject per body under the model root. No log line reports the fallback, and
the `Scene built (…)` summary still names the mode you asked for. The one tell is
that the same line reports `0 hierarchy nodes`.

**No HDRP shader.** Two vertex-colour shaders ship, one for the Built-in Render
Pipeline and one for URP, chosen by whether a render pipeline asset is assigned.
An HDRP project has one assigned, so it is handed the **URP** shader and will
show error-shader magenta until you author an HDRP equivalent.

**The URP shader is compiled even in a Built-in-only project.** It includes
`Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl`, and
`package.json` does not depend on the URP package. Unity compiles every shader in
an imported package whether or not anything uses it, so in a project without URP
installed that include path may not resolve. If you hit shader compile errors on
import in a Built-in-only project, this is the cause.

**Forward+ on URP 14–16 loses point and spot lights.** The URP shader declares
`_CLUSTER_LIGHT_LOOP`, which is URP 17's spelling of the Forward+ keyword. URP
14–16 (Unity 2022.2–2023) call the same feature `_FORWARD_PLUS`. A project on
those versions that opts into the Forward+ renderer compiles a variant with no
additional-light path, so only the main directional light reaches imported
geometry. Unity 6 defaults to URP 17 and is unaffected.

**Glass smoothness only takes effect under URP.** The Built-in shader declares no
`_Smoothness` property, so the glass smoothness the material factory writes is
silently skipped there. Transparency itself works in both, because it comes from
the blend and depth-write properties both shaders declare. A Built-in project
gets correctly blended glass with default Lambert shading rather than a glassier
highlight.

**Switching render pipeline after import does not re-import.** The importer
declares a dependency on the shader *file*, but nothing declares a dependency on
which pipeline is active. Assign or clear a render pipeline asset and existing
models silently keep the materials they were built with until something else
triggers a re-import. Re-import them by hand after switching.

**Colour space.** Vertex colours are baked with a `pow(2.2)` conversion that
targets **Linear** colour space. Behaviour in a Gamma project is unverified.

**Practical ceilings.** A build stops after 250,000 GameObjects, and merged
meshes split into new chunks at 500,000 vertices or 4,000,000 indices. Large
models in Per Body or Instanced mode are the ones that hit the first ceiling;
move them to Per Storey or Merged.

**No `.meta` files are committed.** Unity generates a `.meta` file, and with it a
GUID, for every asset the first time it sees one. Because none are committed, two
machines that install this package get different GUIDs for the same shader,
script and sample — so a scene or prefab referencing them in one checkout
resolves to nothing in another. This is why the Basic Import sample ships a
script rather than a scene.

**The `.frag` schema's redistribution terms are unresolved.**
`ThirdParty/index.fbs`, and the `index_codegen.fbs` variant beside it, are
ThatOpen Components' format description, vendored here so the FlatBuffers
bindings can be regenerated. The code in `Runtime/Generated/` is output of
`flatc`, not ThatOpen source, and carries no FlatBuffers obligation of its own;
the schema file itself is the open question. Check ThatOpen's current licence
before redistributing this package with the schema in it. See
[Third Party Notices.md](../Third%20Party%20Notices.md).

## Further reading

- [ImportModes.md](ImportModes.md) — what each mode costs and what it gives up.
- [Picking.md](Picking.md) — raycasting to elements, and the collider layer.
- [ScriptingApi.md](ScriptingApi.md) — the full C# surface.
- [CHANGELOG.md](../CHANGELOG.md) at the package root.
