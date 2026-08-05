# Fragments for Unity

A Unity package that imports ThatOpen Fragments 2.0 (`.frag`) BIM files as
native Unity assets, with geometry, materials and complete IFC metadata. Pure
C# — no native plugin, no external process.

<!-- SCREENSHOT PLACEHOLDER: an imported model in the Scene view with an element
     selected, showing its IFC record in the Inspector. Replace this comment
     with: ![An imported model with an element selected](Documentation~/images/overview.png) -->

## Install

*Window > Package Manager > + > Add package from git URL*:

```
https://github.com/Mohammedazif/FragmentsUnity.git
```

Or *Add package from disk* and select this package's `package.json`.

Install it as a package, under `Packages/`. Copying the folder into `Assets/` is
not supported — the importer resolves its shader by package path and will fall
back to an all-white model.

## Requirements

| | |
|---|---|
| Unity | Run on 6.2 (6000.2) and 6.5 (6000.5). The manifest declares a floor of 2021.3, which is untested — see [Verification status](#verification-status). |
| Render pipeline | Built-in RP or URP. No HDRP shader ships. |
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
  IFC record. Import it from *Window > Package Manager > Fragments for Unity >
  Samples*.

## Verification status

Version 0.1.0 — see [CHANGELOG.md](CHANGELOG.md).

**Confirmed** in Unity 6.2 (6000.2.10f1) and 6.5 (6000.5.6f1) on Windows, DX11:
`.frag` import through the ScriptedImporter, with the editor console reporting
counts that match the offline parser exactly; shader compilation and rendering in
original IFC colours; several models and several instances of one model in one
scene; the `FragmentModel`, `FragmentFilter` and `FragmentVisibilityIndex`
components populated on the model root; the model and element inspectors,
including Global Id search and the copy button; and importing the Basic Import
sample from the Package Manager.

**Not yet confirmed:** the declared Unity 2021.3 floor; which render pipeline was
active in those sessions, so neither shader is individually confirmed; the merged
import modes and the merged-chunk inspector; the filter window; the
asset-extraction command; and play-mode picking.

The parser core is covered by 89 automated tests that run under a plain .NET SDK
against real models, all passing. See
[Documentation~/index.md](Documentation~/index.md#tests) for how to run the
suite, and its [Limitations](Documentation~/index.md#limitations) section for
what the package does not do.

## Licence

MIT — see [LICENSE.md](LICENSE.md). Third-party notices (Earcut ISC, FlatBuffers
Apache 2.0) are in [Third Party Notices.md](Third%20Party%20Notices.md).
