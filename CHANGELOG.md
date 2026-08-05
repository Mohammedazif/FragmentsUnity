# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this package adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-08-04

Initial pre-release: a pure-C# importer for ThatOpen Fragments 2.0 (`.frag`) BIM
files.

**Verification.** This release has been run in Unity 6.2 (6000.2.10f1) and 6.5
(6000.5.6f1) on Windows, DX11. Confirmed there: `.frag` import through the
ScriptedImporter, with the editor console reporting geometry, instance, vertex
and triangle counts matching the offline parser exactly; shader compilation and
rendering in original IFC colours; several models and several instances of one
model in a single scene; `FragmentModel`, `FragmentFilter` and
`FragmentVisibilityIndex` populated on the model root; the model root inspector
including Global Id search; the element inspector drawing a complete IFC record
with its copy button; and importing the Basic Import sample from the Package
Manager.

Not yet confirmed: the declared Unity 2021.3 floor, which render pipeline was
active in those sessions, the merged import modes and the merged-chunk
inspector, the filter window, the asset-extraction command, and play-mode
picking. The parser core is covered by 89 automated tests run under a plain .NET
SDK against real models, all passing. `Documentation~/index.md` carries the full
verification status and limitations list.

### Added

- **`.frag` import** as a `ScriptedImporter`: drag a file into the Project
  window and it imports like any native asset, with meshes, materials and the
  metadata payload as sub-assets, a cancellable progress bar over the scene
  build, and an import dependency on the vertex-colour shader so a clean import
  resolves it. Parsing itself runs before the progress bar and cannot be
  cancelled.
- **Geometry** from shells, profiles and holes — FlatBuffers decode, zlib
  inflate, ear-clipping triangulation with holes, crease-angle vertex merging,
  and conversion from Fragments' right-handed metre space to Unity's, with
  triangle winding reversed to match. Meshes switch to 32-bit indices above
  65,535 vertices.
- **IFC metadata**: GlobalId, category, name, express id, direct attributes,
  property and quantity sets (including type-inherited ones, flagged),
  materials with layer names and thicknesses, classifications, relations, and
  the model header. Stored gzip-compressed on a `FragmentModelAsset` sub-asset
  and deserialized on demand.
- **Spatial hierarchy** — Project → Site → Building → Storey → Element — with
  storey resolution through containing ancestors, anonymous-group flattening,
  and orphan instances kept under the model root rather than dropped.
- **Five import modes**: hierarchy per body (default), per element, per storey,
  instanced, and merged whole model, with merge buckets by category and colour
  and chunk splitting at the vertex and index ceilings.
- **Element picking**: `FragmentPicker` resolves a `RaycastHit` back to the
  element that was struck, including inside merged meshes, by binary-searching
  a per-chunk triangle-start table. A **Collider Layer** setting puts every
  collider on a layer the project can exclude from physics.
- **Filtering**: `FragmentFilter` isolates or toggles elements by category,
  storey, attribute value, or explicit local ids, hiding renderers and colliders
  together and restoring them exactly.
- **Query API** on `FragmentModel`: `FindByGlobalId`, `FindByLocalId`,
  `FindByCategory`, `FindByStorey`, `FindByAttribute`, `GetCategoryCounts`,
  `GetStoreyCounts` and `GetFlattenedValues`.
- **Editor tooling**: an inspector for the model root (summary counts, GlobalId
  search, and the selected element's full IFC record with a copy button), the
  same record inline on spawned elements, a dockable **Fragments Filter** window
  listing levels and categories with counts, and an **Extract Meshes and
  Materials** command that promotes the importer's sub-assets into standalone,
  editable assets.
- **Vertex-colour shaders** for the Built-in Render Pipeline and URP, selected
  by the active pipeline, with switchable culling and alpha blending. Surfaces
  are sorted into opaque, translucent and glass by a hue-and-opacity heuristic,
  and single- or double-sided by the file's `RenderedFaces` flag — at most six
  materials for a model of any size.
- **Hardening** against corrupt or hostile files: every table, string and
  allocation is budgeted through `FragmentImportLimits`, and a failed parse
  reports an import error instead of taking the editor down.

### Known limitations

- No `.meta` files are committed, so asset GUIDs differ between machines until
  the package is opened in Unity once and they are committed.
- `CircleExtrusion` geometry (rebar and similar) is skipped; the count is
  logged.
- Merged import modes filter at chunk granularity, so hiding one element hides
  the chunk it shares.
- Imported models are solid to physics until a collider layer is configured.
- Metadata is absent when the `.frag` was exported without properties.
- A `.frag` with no spatial tree is built flat whichever hierarchy mode is
  selected, and no log line reports the fallback.
- No HDRP shader variant. An HDRP project is handed the URP shader.
- The URP shader is compiled even in a Built-in-only project, where its URP
  include path may not resolve.
- On URP 14–16 the Forward+ renderer loses point and spot lights on imported
  geometry; Unity 6's URP 17 is unaffected.
- Switching render pipeline after import does not trigger a re-import, so models
  silently keep the materials they were built with.
- The redistribution terms of ThatOpen's `index.fbs` schema are unresolved; see
  `Third Party Notices.md`.

See `Documentation~/index.md` for the detail behind each limitation.
