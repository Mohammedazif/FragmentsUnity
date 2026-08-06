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
- [Verification status](#verification-status)
- [Limitations](#limitations)

## Requirements

- Unity **2021.3** or newer. Verified on 2021.3, 6.2 (6000.2) and 6.5 (6000.5).
- API Compatibility Level **.NET Standard 2.1**.
- The **Built-in Render Pipeline** or **URP**.
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
| **Collider Layer** | `0` (Default) | The layer index every collider-bearing object is moved onto. Do not use layer 2: it is Unity's built-in `Ignore Raycast`, so `Physics.Raycast` skips the colliders and picking silently does nothing, with no error and no warning. | Leaving it at Default makes the building solid to your whole project. Read [Picking.md](Picking.md) before shipping a scene. |

**Import Mode** is drawn as a plain enum popup, so the options read
*Hierarchy Per Body*, *Hierarchy Per Element*, *Hierarchy Per Storey*,
*Instanced* and *Merged Whole Model*. **Collider Layer** is drawn as a plain
integer field, not a layer dropdown — enter the layer's index, which
*Project Settings > Tags and Layers* lists beside its name.

Import shows a cancellable progress bar once it starts building the scene.
Cancelling discards the whole build; nothing half-built is written. Parsing the
file happens before that, with no progress and no way to cancel — on a very
large `.frag` the editor is unresponsive until the parse finishes.

**A cancelled import leaves an empty asset.** The importer reports an import
error reading `Import cancelled — nothing was written and the model is empty`,
and the `.frag` stays in the project as an asset with no model in it — no
geometry, no metadata, nothing to drag into a scene. Unity does not retry it on
its own. Right-click the `.frag` and choose **Reimport** to build it properly.

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

## Verification status

Verified on Windows, DX11, in Unity **2021.3**, **6.2 (6000.2)** and
**6.5 (6000.5)**:

- **Both render pipelines.** URP, and the Built-in Render Pipeline on 2021.3 in
  a project with no URP package installed.
- **The declared 2021.3 floor.**
- **Installation** from a git URL, and from disk.
- **All five import modes**, producing the hierarchy shapes
  [ImportModes.md](ImportModes.md) describes.
- **A Windows player build.** Models render standalone, outside the editor.
- **All three inspectors** — model root, element, and merged chunk.
- **The filter window**, and the full `FragmentFilter` API.
- **Asset extraction**, through to assets written on disk.
- **Play-mode picking**, in per-body mode and in the merged modes, where the
  element is resolved from the chunk's triangle table rather than from the
  GameObject that was hit.
- **Every importer setting**: Scale Factor, Import Metadata, Import Property
  Sets, Enable Element Picking on and off, and Collider Layer.
- **Reimport.** Changing a setting rebuilds the asset and updates existing scene
  instances.
- **Cancelling an import** mid-build.
- **A large production model**, in every mode.

The 250,000-object spawn ceiling described under [Limitations](#limitations) was
never reached during this verification, so that guard is unexercised.

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
Instanced. [ImportModes.md](ImportModes.md) sets out the trade-off per mode.

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
