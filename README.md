# Fragments for Unity

A Unity package that imports ThatOpen Fragments 2.0 (`.frag`) BIM files as
native Unity assets, with geometry, materials and complete IFC metadata. Pure
C# — no native plugin, no external process.

## Install

*Window > Package Manager > + > Add package from git URL*:

```
https://github.com/Mohammedazif/FragmentsUnity.git
```

Or *Add package from disk* and select this package's `package.json`.

Install it as a package, under `Packages/`. Copying the folder into `Assets/` is
not supported — the importer resolves its shader by package path, and under
`Assets/` that lookup fails and models can import all-white.

## Requirements

| Requirement | Detail |
|---|---|
| Unity | 2021.3 or newer. Verified on 2021.3, 6.2 (6000.2) and 6.5 (6000.5). |
| Render pipeline | Built-in Render Pipeline or URP. |
| API Compatibility Level | .NET Standard 2.1 |
| Dependencies | `com.unity.nuget.newtonsoft-json` 3.2.1, resolved automatically |

## Quick start

1. Drag a `.frag` file into the Project window. It imports like any native
   asset — meshes, materials and metadata become sub-assets.
2. Drag the imported asset into a scene. It arrives at the origin, at metre
   scale, in its original IFC colours.
3. Click an element. The Inspector shows its full IFC record: class, name,
   GlobalId, type, storey, attributes, property sets and materials.

## Features

- **Five import modes** — hierarchy per body, per element, per storey,
  instanced, or one merged model — trading object count against draw calls and
  selection granularity.
- **Full IFC metadata** — GlobalIds, property and quantity sets including
  type-inherited ones, materials with layer thicknesses, classifications, and
  the Project → Site → Building → Storey → Element spatial hierarchy.
- **Element picking** — one `Physics.Raycast` plus `FragmentPicker` returns the
  IFC record behind the hit, resolving the exact element even inside merged
  meshes.
- **Filtering** by category, storey, attribute value, or explicit element ids,
  from code or from a dockable editor window.
- **A query API** on the model root: `FindByGlobalId`, `FindByCategory`,
  `FindByStorey`, `FindByAttribute`, `GetFlattenedValues`, and more.
- **Asset extraction** — a command that promotes the importer's read-only mesh
  and material sub-assets into standalone, editable project assets.

## Documentation

- [Documentation~/index.md](Documentation~/index.md) — the manual: installation,
  every importer setting, editor tooling, and the limitations list.
- [Documentation~/ImportModes.md](Documentation~/ImportModes.md) — what each
  mode costs and what it gives up.
- [Documentation~/Picking.md](Documentation~/Picking.md) — raycasting to
  elements, and the collider layer an imported model needs.
- [Documentation~/ScriptingApi.md](Documentation~/ScriptingApi.md) — the full
  C# surface.
- [Samples~/BasicImport](Samples~/BasicImport) — click an element and log its
  category, name, GlobalId and storey. Import it from
  *Window > Package Manager > Fragments for Unity > Samples*.

## Verification status

Version 0.1.0 — see [CHANGELOG.md](CHANGELOG.md).

Verified on Windows, DX11, in Unity **2021.3**, **6.2 (6000.2)** and
**6.5 (6000.5)**:

- Both render pipelines — URP, and the Built-in Render Pipeline on 2021.3 in a
  project with no URP package installed.
- The declared 2021.3 floor.
- Installing from a git URL, and from disk.
- All five import modes, producing the hierarchy shapes
  [ImportModes.md](Documentation~/ImportModes.md) describes.
- A Windows player build — models render standalone, outside the editor.
- All three inspectors: model root, element, and merged chunk.
- The filter window and the full `FragmentFilter` API.
- Asset extraction.
- Play-mode picking, in per-body mode and in the merged modes, where the element
  is resolved from the triangle table rather than the GameObject.
- Every importer setting: Scale Factor, Import Metadata, Import Property Sets,
  Import Mode, Enable Element Picking on and off, and Collider Layer.
- Reimport — changing a setting rebuilds the asset and updates existing scene
  instances.
- Cancelling an import mid-build.
- A large production model, in every mode.

The 250,000-object spawn ceiling was never reached during this verification, so
that guard is unexercised.

See the [Limitations](Documentation~/index.md#limitations) section of the manual
for what the package does not do.

## Licence

MIT — see [LICENSE.md](LICENSE.md). Third-party notices (Earcut ISC, FlatBuffers
Apache 2.0, Json.NET MIT) are in
[Third Party Notices.md](Third%20Party%20Notices.md).
