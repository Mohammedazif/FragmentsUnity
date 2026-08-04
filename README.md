# Fragments for Unity

A pure-C# Unity package that natively imports ThatOpen Fragments 2.0 (`.frag`)
BIM files with complete IFC metadata: GlobalIds, property sets, quantity sets,
materials with layer thicknesses, classifications, and the
Project → Site → Building → Storey → Element spatial hierarchy.

Drag a `.frag` file into the Project window and it imports like any native
asset. Models arrive with original IFC colors, correct meter-scale orientation,
and per-element metadata queryable from C#.

## What you get

- **Five import modes** — hierarchy per body, per element, per storey,
  instanced, or one merged model — trading object count against draw calls and
  selection granularity.
- **Element picking**: one `Physics.Raycast` plus `FragmentPicker` returns the
  IFC record behind the hit, resolving the exact element even inside merged
  meshes.
- **Filtering** by category, storey, attribute value, or explicit element ids.
- **A query API** on the model root: `FindByGlobalId`, `FindByCategory`,
  `FindByStorey`, `FindByAttribute`, `GetFlattenedValues`.
- **Editor tooling**: metadata inspectors for the model and its elements, a
  dockable filter window, and a command that extracts meshes and materials into
  standalone assets.

## Getting started

Install it as a UPM package — *Window > Package Manager > + > Add package from
disk* — so it mounts under `Packages/`, then drag a `.frag` file into the
Project window.

**[Documentation~/index.md](Documentation~/index.md) is the manual**:
installation, importer settings, the query API, editor tooling, and an honest
list of what this package does not do. [ImportModes.md](Documentation~/ImportModes.md)
and [Picking.md](Documentation~/Picking.md) go deeper on the two decisions that
matter most.

Requires Unity 2021.3 or newer with the Built-in Render Pipeline or URP.

## Status

Version 0.1.0 — see [CHANGELOG.md](CHANGELOG.md). This is a pre-release, and the
honest summary is short:

- **The parser and the scene-building logic are validated.** 444 automated tests
  run under a plain .NET SDK, including regression pins over all three
  development models. Where FragmentsUE left a usable reference — its own run
  logs — the parsed counts match it exactly; `Phase1Validation.md` explains
  which of those logs is stale and why one of them cannot be used.
- **Nothing in this package has ever been run inside a Unity editor.** Not once.
  The two shaders have never been compiled; the importer has never imported
  anything; the inspectors, the filter window and the asset-extraction command
  have never been drawn or clicked. The Unity layer is compiled against
  hand-written stubs of the Unity API, which prove it builds and that its own
  contracts hold — not that the engine behaves the way the stubs assume.

The `Phase1Validation.md` … `Phase4Validation.md` records in `Documentation~/`
list, phase by phase, exactly what was measured and what is still waiting on a
real editor. Read them before you trust a number.

## License

MIT — see [LICENSE](LICENSE). Third-party notices (Earcut ISC, FlatBuffers
Apache 2.0) are in [ThirdParty/LICENSES.md](ThirdParty/LICENSES.md).
