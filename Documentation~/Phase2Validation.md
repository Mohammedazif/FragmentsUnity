# Phase 2 Validation Record

**Date:** 2026-08-04
**Scope:** metadata and hierarchy (UNITY_PORT.md §10, phase 2).

## Known divergences from FragmentsUE

- **Orphan instances are kept.** FragmentsUE's hierarchy walk silently drops
  instances whose local id the spatial tree never names. This port spawns them
  under the model root and logs the count, because losing geometry that the
  flat path renders is worse than the divergence.
- `UNITY_PORT.md` §10 names the query `FindByGuid`; the shipped name is
  `FindByGlobalId`, matching the DTO field.

## Method

No Unreal Engine installation exists on the build machine, so FragmentsUE
could not be run side by side. Instead each element below was verified against
**ground truth in the `.frag` file itself**: a probe walks the raw FlatBuffers
tables (`guids` → `guids_items` → `local_ids` → `attributes` / `relations`)
with an **independently written tuple parser**, so the check shares no code
with the ported tokenizer it validates.

## Aggregate metadata output

| File | Items | With attributes | With relations | With psets | Total psets | Total properties | With materials | With storey | With type |
|---|---|---|---|---|---|---|---|---|---|
| AR520.frag | 12,952 | 12,950 | 8,226 | 1,299 | 15,657 | 34,110 | 1,276 | 1,117 | 1,111 |
| Joyson Model.frag | 10,282 | 10,265 | 8,107 | 2,124 | 8,458 | 15,555 | 2,111 | 2,014 | 1,983 |
| 20210219Architecture.frag | 180,399 | 178,520 | 108,092 | 17,745 | 188,544 | 569,899 | 15,264 | 15,423 | 13,465 |

No budget-exhaustion diagnostics fire on any sample.

## Element-level verification (acceptance criterion)

**1. Door — AR520.frag, GlobalId `1MXYWRlOjEm8W0fkpMpNTl`**
Raw: express id 3971, dense index 0, category `IFCDOOR`, attribute tuples
Name/ObjectType/Tag/OverallHeight/OverallWidth, relations
`Decomposes`/`IsDefinedBy`/`HasAssociations`.
Parsed: identical identity and name; `Pset_DoorCommon` with `IsExternal=true`
and `Reference=Curtain-Wall-Double-Storefront`; `Pset_QuantityTakeOff`;
`Other` with Host Id/Category/Family/Family and Type — each value equal to the
raw `NominalValue`. Storey `GF-GROUND FLOOR - FFL` resolved through the
containing curtain wall (`IFCCURTAINWALL`), exercising ancestor resolution.

**2. Slab — AR520.frag, GlobalId `3X5KAguVb6zxUkPAK059OM`**
Raw: express id 34999, dense index 776, category `IFCSLAB`, four
`IsDefinedBy` sets of interest plus `ContainedInStructure` → 38.
Parsed: 16 property sets. All three direct sets match the raw graph exactly —
`Pset_QuantityTakeOff`, `Pset_ReinforcementBarPitchOfSlab`, and
`Pset_SlabCommon` (`IsExternal=false`, `Reference=Floor Finish - 400mm`,
`LoadBearing=false`, `PitchAngle=0`). Type-derived sets are marked
`FromType`, and the type's own attributes appear as the `IFCSLABTYPE` set.
One material, `Tiles`, with layer-set name `Floor:Floor Finish - 400mm` and
thickness 400. Storey `GF-GROUND FLOOR - FFL`.

**3. Wall — Joyson Model.frag, GlobalId `1zzhLY2$PAnwlHvBOzeJsG`**
Raw: express id 191180, dense index 1838, category `IFCWALLSTANDARDCASE`;
the raw Name tuple contains an escaped quote (`5/8\"`).
Parsed: name unescaped correctly to `Basic Wall:N-EXT-LB-43-SO-3 5/8" EXT
Finishes:3856213`, exercising the tokenizer's escape handling. All raw sets
match (`Pset_QuantityTakeOff`, `Pset_ReinforcementBarPitchOfWall`,
`Pset_WallCommon` with all four properties), plus the type-derived
`Pset_WallCommon` and the `IFCWALLTYPE` attribute set. Seven material layers
with per-layer thicknesses. Storey `Level 1`.

## Model header

Headers parse correctly on all three files: IFC schema, file name, export
timestamp (preserved verbatim — Json.NET date conversion is disabled so ISO
strings are not reformatted), preprocessor, authoring tool, descriptions,
project/site names, model origin, and the Units set (AR520 reports
`LENGTHUNIT = MILLIMETRE`).

## Unity-layer test coverage

171 tests run without Unity: 89 pure-core tests plus 82 Unity-layer tests that
exercise asset serialization, the queryable-item filter, every query method,
flattened values, and both scene-build paths against the committed API stubs
(`Tests~/DotnetRunner`).

What the stubs cannot show, and therefore still needs the Unity Test Runner:
parent/child links (stub `SetParent` is a no-op), component state (stub
`AddComponent` returns an instance the builder writes to and drops, so
`FragmentElementReference.LocalId`, mesh/material assignment and the
volume-category `renderer.enabled = false` rule are unverified), `_body{n}`
label suffixes on spawned children, mesh contents, real shader resolution and
the double-sided `_Cull` write, and the importer itself.

## Unity-layer compile verification

The Unity layer (11 files under `Runtime/Unity` and `Editor`) cannot be built
without a Unity installation, so it is compiled against hand-written
**reference stubs** that declare the Unity 2021.3 API surface it uses
(`GameObject`, `Transform`, `Mesh`, `Material`, `Shader`, `MonoBehaviour`,
`ScriptableObject`, `ScriptedImporter`, `AssetImportContext`, `AssetDatabase`,
`IndexFormat`, `CullMode`, `Debug`, …). The layer compiles clean with
warnings-as-errors.

This proves syntax, every cross-file contract inside the package, and every
reference into `Runtime/Core`. It does **not** prove the Unity API calls
themselves are correct, because the stub signatures encode this port's belief
about the API — that check still needs a real editor.

## Queryable item set

`FragmentModelAsset` persists **only items that carry geometry**, deduped in
first-instance order, mirroring `AFragmentsActor::BuildMetadataTables`
(`FragmentsActor.cpp:781`). Every UE query runs over that filtered table, so
matching it keeps `FindByCategory`, `GetCategoryCounts` and friends returning
element categories rather than property-set and material records.

| File | Parsed items | Queryable items |
|---|---|---|
| AR520.frag | 12,952 | 1,113 |
| Joyson Model.frag | 10,282 | **1,986** |
| 20210219Architecture.frag | 180,399 | 15,191 |

The Joyson figure matches FragmentsUE exactly: its run log records
`Metadata table: 1986 elements` (`debug_log4.txt:1549`). AR520's 1,113 equally
matches that log family's `MeshesItems: 1113`.

That log also reports `1478 with GUIDs` where this port gives all 1,986 items a
GlobalId, and the 1,986 are distinct — so it is not `TMap` deduplication. It is
the same stale-log effect Phase 1 documented: the logging build predates the
fix recorded at `FragParser.cpp:1358` ("guids_items[k] holds the express id
owning guids[k], not a dense index"), and the same log family reports AR520 as
`8209 unique, 895 mapped to localIds`. Current code maps far more of them.

## Metadata payload size

Serialized metadata is large: before filtering, the Architecture model produced
153 million characters of JSON (~306 MB in memory as UTF-16), which no Unity
`ScriptableObject` string field can carry. `FragmentModelAsset` now stores
**gzip-compressed UTF-8 bytes** of the filtered item set.

| File | Stored payload |
|---|---|
| AR520.frag | 130,734 bytes |
| Joyson Model.frag | 138,528 bytes |
| 20210219Architecture.frag | 2,045,945 bytes |

Round-trip was verified exact on all three — queryable item, property-set,
property, material and relation-target counts, items with a resolved storey,
the raw model header, `FindItem` by local id, and per-element category, name,
storey, material thickness and `FromType` flags. Compression is byte-identical
across runs, so imports stay reproducible. Serializer settings are pinned
explicitly so a host project's `JsonConvert.DefaultSettings` cannot change what
is written or read.

## Deferred to a Unity editor

Everything in `Phase1Validation.md`'s checklist still applies, plus:

1. `FragmentModelAsset`: the Json.NET round trip is already proven outside
   Unity (see above); confirm the compressed `byte[]` sub-asset survives a
   reimport and an editor restart, and measure import time on the Architecture
   model.
2. `FragmentModel` query API against a real imported model: `FindByGlobalId`,
   `FindByCategory`, `FindByStorey`, `FindByAttribute`, `GetFlattenedValues`.
3. Spatial hierarchy shape in the scene view: storey grouping, anonymous-group
   flattening, `_body` suffixes, and that `IfcSpace`-class volumes are present
   but not rendered.
