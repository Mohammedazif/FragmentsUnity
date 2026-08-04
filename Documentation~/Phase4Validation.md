# Phase 4 Validation Record

**Date:** 2026-08-04
**Scope:** editor tooling, render pipelines, progress and packaging
(UNITY_PORT.md §10, phase 4 — items 11, 12 and 13).

## What shipped

| Roadmap item | Shipped | Where |
|---|---|---|
| 11 — inspector metadata viewer | Model inspector (summary counts, Global Id search, the selected element's record) and the same record inline on spawned elements, both with a copy button | `Editor/FragmentModelInspector.cs`, `FragmentElementInspector.cs`, `FragmentMetadataInspectorGUI.cs` |
| 11 — filter `EditorWindow` | Dockable *Window > Fragments > Filter*: levels and categories with counts, per-row toggle and isolate, attribute filter, clear | `Editor/FragmentFilterWindow.cs` over `Runtime/Unity/FragmentFilterState.cs` |
| 11 — asset baking | *Assets > Fragments > Extract Meshes and Materials*, plus the `ExtractMeshes` / `ExtractMaterials` API | `Editor/FragmentAssetExtractor.cs`, `FragmentAssetExtractionPlan.cs` |
| 12 — pipeline variants | Built-in and URP vertex-colour shaders, selected by whether a render pipeline asset is assigned | `Runtime/Shaders/`, `FragmentMaterialFactory.ActiveShaderAssetPath` |
| 12 — translucent/glass | Surface classifier in the parser core; three surface kinds carried through meshes, materials and merge buckets | `Runtime/Core/FragmentSurfaceClassifier.cs`, `FragmentSurfaceKind.cs` |
| 13 — progress and cancellation | Cancellable `EditorUtility.DisplayCancelableProgressBar` over four named build stages; cancelling destroys everything built so far | `Runtime/Unity/FragmentImportProgress.cs`, `FragmentSceneBuilder.DiscardCancelledBuild` |
| 13 — docs and samples | This manual set, `CHANGELOG.md`, and the Basic Import sample | `Documentation~/`, `Samples~/BasicImport/` |

**Item 12 shipped short of the roadmap: there is no HDRP variant.** The roadmap
asked for URP, HDRP and Built-in. An HDRP project has
`GraphicsSettings.currentRenderPipeline` set, so it is handed the *URP* shader
and will show error-shader magenta. This is recorded as a limitation in
`index.md` rather than closed.

## Surface classification

The heuristic is a direct port of `FragmentsActor.cpp:603-617`, moved into
`Runtime/Core` so it is testable without Unity:

- blue-dominant hue (`B > R + 0.15` **and** `B > G + 0.05`) → glass, whatever
  the opacity;
- otherwise opacity below 0.5 → glass;
- otherwise opacity below 0.99 → translucent;
- otherwise opaque.

Glass reached by hue alone arrives with full alpha, so `ResolveOpacity` forces
it to 0.5 — mirroring the `AdjustedOpacity` assignment at
`FragmentsActor.cpp:611-614`. Every threshold is a named constant in
`FragmentImportLimits` (`GlassBlueDominanceOverRed`, `GlassOpacityThreshold`,
`OpaqueOpacityThreshold`, `ForcedGlassOpacity`).

Classified against the three development models:

| File | Opaque | Translucent | Glass |
|---|---|---|---|
| AR520.frag | 1,146 | 0 | 12 |
| Joyson Model.frag | 14,198 | 1 | 58 |
| 20210219Architecture.frag | 29,618 | 477 | 2,460 |

None of the three declares `RenderedFaces.TWO` on any material, so every
instance in every sample is single-sided.

## Materials

`FragmentSceneSpawnContext` caches materials on `(FragmentSurfaceKind,
DoubleSided)`, so a model of any size is drawn with **at most six**: opaque,
translucent and glass, each single- and double-sided. Merged chunks are forced
double-sided (`FragmentsActor.cpp:662`), so the single-sided variants appear
only on unmerged bodies.

| File | per Body | per Element | per Storey | Instanced | Merged |
|---|---|---|---|---|---|
| AR520.frag | 2 | 2 | 2 | 2 | 2 |
| Joyson Model.frag | 3 | 4 | 3 | 3 | 3 |
| 20210219Architecture.frag | 3 | 4 | 3 | 3 | 3 |

Per-Element reaches four on the two larger models because instances the spatial
tree never names are spawned as individual, single-sided bodies alongside the
merged double-sided element chunks — so glass appears in both variants at once.
Nothing observed reaches five or six; that needs a file with a two-sided face
flag.

This supersedes the material column of `Phase3Validation.md`, which measured a
build where every surface was opaque. Object, node, chunk and mesh counts are
unchanged.

## Merge buckets

`FragmentMergeBucket.BuildKey` now appends the surface kind, and quantizes the
opacity *after* the hue-glass forcing rather than as the file gives it. A bucket
therefore holds exactly one surface kind, and every part in it bakes that
bucket's resolved opacity into its vertex alpha. The key mirrors
`FragmentsActor.cpp:628-634`, where the surface kind stands in for UE's
`bIsGlass` term.

Adding a term to a bucket key can only split buckets, never merge them. On the
one sample with a before-and-after number it split nothing: AR520 produced 6
merged chunks in Phase 3 and produces 6 now, because its category+colour buckets
already separated the glass. Joyson produces 71 and Architecture 86; Phase 3
recorded no merged-chunk count for either, so there is nothing to compare them
against.

`FragmentSurfaceMergeTests` pins the cases that matter: two surfaces sharing a
category, colour and quantized opacity but differing in kind still land in
separate chunks; a mixed-kind category yields one chunk per kind; hue-classified
glass carries 0.5 in its vertex alpha while a translucent surface keeps its own.

## Shaders

Both shaders declare `_Cull`, `_SrcBlend`, `_DstBlend` and `_ZWrite` as float
properties, which is how `FragmentMaterialFactory` switches culling and
transparency without a second shader; both carry
`#pragma multi_compile_instancing`, which Instanced mode's `enableInstancing`
flag needs to be anything but inert.

| | Built-in | URP |
|---|---|---|
| Shader name | `FragmentsUnity/VertexColor` | `FragmentsUnity/URP/VertexColor` |
| Implementation | `#pragma surface surf Lambert keepalpha` | Hand-written `ForwardLit`, `ShadowCaster` and `DepthOnly` passes over `UniversalFragmentPBR` |
| Instancing pragma | yes | yes, in all three passes |
| Fallback | `Diffuse` | `Hidden/Universal Render Pipeline/FallbackError` |

**The Built-in shader has no `_Smoothness` and no `_Mode` property**, so two of
the writes `FragmentMaterialFactory` makes are silently skipped there —
`TrySetFloat` guards every write with `HasProperty`. The glass smoothness of
0.9 (mirroring `M_FragBase_Glass` Roughness 0.1 at
`FragmentsUEEditorModule.cpp:264`) therefore only takes effect under URP. The
`_Mode = 3` write lands on neither shader either; it exists for the `Standard`
material `ResolveShader` falls back to when no vertex-colour shader is found.
Transparency itself does work in Built-in, because it comes from
`_SrcBlend` / `_DstBlend` / `_ZWrite` and the render queue, all of which that
shader declares. A Built-in project gets correctly blended glass with default
Lambert shading rather than a glassier highlight.

### Forward+ lighting, and the version gap that remains

The URP pass declares `#pragma multi_compile _ _CLUSTER_LIGHT_LOOP` and sets
`inputData.normalizedScreenSpaceUV`. Both are required together: on the
Forward+ renderer URP forces `_ADDITIONAL_LIGHTS` and
`_ADDITIONAL_LIGHTS_VERTEX` off and enables the cluster keyword instead, so a
shader that does not declare it compiles a variant with **no additional-light
path at all** — every point and spot light silently stops lighting the model,
leaving only the main directional light. Without the screen UV, the cluster
iterator reads tile zero for every pixel. URP's own `Lit.shader` carries the
same pragma.

`_CLUSTER_LIGHT_LOOP` is URP 17's spelling. URP 14–16 (Unity 2022.2–2023) call
the same feature `_FORWARD_PLUS`, and declaring both would emit a deprecation
warning on URP 17. The package floor, Unity 2021.3, ships URP 12, which has no
Forward+ at all, and Unity 6 defaults to it and is now correct — so the gap is
Unity 2022.2–2023 projects that opt into Forward+ explicitly. Those still lose
point and spot lights on imported geometry. Fixing that means adding the second
pragma and accepting the warning; it is deliberately not done here.

## Editor tooling

Every editor surface is split into an untestable GUI shell and a testable logic
class, and the tests are on the logic:

| GUI shell | Logic under test |
|---|---|
| `FragmentFilterWindow.OnGUI` | `FragmentFilterState` — row lists, counts, visibility bookkeeping, isolate/toggle id sets |
| `FragmentModelInspector.OnInspectorGUI` | `FragmentModelInspector.ResolveSelection` — selection to element, walking up from a mesh child |
| `FragmentMetadataInspectorGUI` | `FragmentMetadataFormatter` — the record text, section ordering, row ceilings, the copy payload |
| `FragmentAssetExtractor` menu items | `FragmentAssetExtractionPlan` — folder chain, name sanitizing, collisions, the over-long-path refusal |

The GUI shells themselves are **untested by design**: `EditorGUILayout`,
`GUILayout` and `EditorWindow` cannot be driven outside a real editor, and the
offline stubs only declare enough of them to compile. What the stubs prove is
that the shells call the logic with the right arguments and compile clean under
warnings-as-errors; what happens when Unity paints them is unverified.

The same split limits `FragmentAssetExtractor`: its planning half is covered,
but `AssetDatabase.CreateFolder`, `CreateAsset`, `GenerateUniqueAssetPath` and
`SaveAssets` are absent from the stubs and sit behind
`#if UNITY_2021_3_OR_NEWER`, so the half that writes files has never run at all.

## Progress and cancellation

`FragmentImportProgress` maps each build stage onto a slice of overall progress
and reaches the reporter every 256 items, so a per-instance loop cannot repaint
the progress bar once per body. The build opens on *Preparing materials* at 0,
then runs exactly one of *Building geometry*, *Building hierarchy* or *Merging
chunks* — whichever the mode selects — across 0.05 to 1.0. Reported progress is
monotonic: `Report` keeps the maximum it has seen, so a stage cannot walk the
bar backwards.

The reporter returns false when the user presses Cancel, which propagates back
through `ReportStep` to the build loops. `DiscardCancelledBuild` then destroys
every mesh, every material and the root, clears the counts, logs a warning, and
`FragmentModelImporter` turns that into an import error — nothing half-built is
written. 40 tests cover the progress arithmetic and the discard.

**The progress bar covers the scene build only.** `FragmentParser.LoadFromFile`
runs to completion before the progress bar exists, and `FragmentImportOptions`
carries no progress channel, so parsing a large `.frag` shows no progress and
cannot be cancelled. On the Architecture model that is the part of the import
that reads and inflates the whole file.

## Test coverage

Both suites run under a plain .NET SDK with `FRAGMENTSUNITY_SAMPLE_DIR` set:

| Suite | Project | Tests |
|---|---|---|
| Parser core | `Tests~/DotnetRunner/Tests/FragmentsUnity.Tests.csproj` (compiles `Tests/Editor`) | 89 |
| Unity layer | `Tests~/DotnetRunner/UnityLayerTests/FragmentsUnity.UnityLayerTests.csproj` | 360 |
| **Total** | | **449** |

All pass, none skipped. Of the 360, 137 cover phase 4 work:

| Area | Fixtures | Tests |
|---|---|---|
| Surface classification and merge buckets | `FragmentSurfaceClassifierTests`, `FragmentSurfaceMergeTests` | 21 |
| Editor-tooling logic | `FragmentAssetExtractionPlanTests`, `FragmentFilterStateTests`, `FragmentMetadataFormatterTests`, `FragmentModelInspectorSelectionTests` | 71 |
| Progress and cancellation | `FragmentImportProgressTests`, `FragmentSceneCancellationTests` | 40 |

**Nothing here was run inside Unity.** These numbers describe C# logic executed
against the hand-written API stubs in `Tests~/DotnetRunner/UnityStubs`. The
shaders were not compiled by any shader compiler; the importer, the inspectors,
the filter window and the extraction command have never been drawn or clicked.

The reachability gap `Phase3Validation.md` recorded is unchanged: the Unity-layer
tests live under `Tests~`, which Unity ignores, so a user running the package's
own Test Runner sees the 89 parser tests and none of the other 360.

## Known divergences from FragmentsUE

- **The classifier follows UE's merged-bucket variant, not its per-pair one —
  and the two disagree in the C++ itself.** `FragmentsActor.cpp` classifies
  surfaces in three places. The merged path (`:603-617`) and the per-instance
  hierarchy path (`:1578-1584`) send a surface to glass when it is blue *or*
  under 0.5 opacity, and force hue-only glass down to 0.5. The unique-pair path
  (`:422-428`) sends a surface to glass when it is blue *or* under **0.99**
  opacity — which is every non-opaque surface, leaving its translucent branch
  unreachable — and forces hue-only glass to **0.75**. This port implements the
  first form everywhere. The consequence is visible: a surface at 0.7 opacity is
  translucent here and in UE's merged modes, but glass in UE's unique-pair mode,
  and an opaque blue surface is glass at 0.5 alpha here and 0.75 there. Matching
  UE exactly would mean reproducing a bug and rendering the same surface
  differently in different import modes.
- **UE renders every fragment surface two-sided; this port culls single-sided
  ones.** All three UE base materials set `TwoSided = true` unconditionally
  (`FragmentsUEEditorModule.cpp:53`, `:136`, `:219`). UE parses the
  `RenderedFaces` flag into `bDoubleSided` and threads it all the way into
  `GetOrCreateMaterial`, where it is neither hashed into the material cache key
  nor applied to the material — the flag is dead. This port honours it: a
  single-sided instance gets `_Cull` back-face culling. No sample declares
  `RenderedFaces.TWO`, so on the models to hand this means UE draws back faces
  everywhere and this port draws none, which is the intended reading of the
  file. Expect visible differences on any open shell viewed from behind.
- **Materials are import sub-assets, not project assets.** UE's
  `FragAssetFactory` writes a `UMaterialInstanceConstant` per colour into the
  content browser during import. Here meshes and materials stay sub-assets of
  the `.frag` — read-only, replaced on re-import — and promoting them is an
  explicit *Extract Meshes and Materials* command. A package should not scatter
  assets through a project without being asked.
- **No runtime material-graph generation.** UE's editor module builds its base
  materials as `UMaterial` expression graphs at startup. Unity has no
  equivalent, so authored shaders ship instead — sanctioned by UNITY_PORT.md
  §11.
- **No HDRP variant**, where the roadmap asked for one. See above.

## Deferred to a Unity editor

The checklists in `Phase1Validation.md`, `Phase2Validation.md` and
`Phase3Validation.md` all still apply. Phase 4 adds:

1. **Confirm both shaders actually compile.** Neither has been through a shader
   compiler. Open each in a project of the matching pipeline and check the
   inspector reports no errors, on the target graphics APIs.
2. **Confirm the URP shader does not break a Built-in-only project.**
   `FragmentVertexColorURP.shader` includes
   `Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl`, and
   `package.json` does not depend on `com.unity.render-pipelines.universal`. In
   a project without URP installed that include path does not resolve, and Unity
   compiles every shader in an imported package whether or not anything uses it.
   Install the package into a clean Built-in project and see whether it throws
   shader compile errors on import. **If it does, move the URP shader into
   `Samples~/` so it is only compiled once a URP user imports it**, and document
   the extra step.
3. **Confirm the GPU instancing variants really exist.** Both shaders declare
   `#pragma multi_compile_instancing`; check the compiled variant list actually
   contains `INSTANCING_ON`, or Instanced mode's `enableInstancing` is inert.
   (Carried forward from `Phase3Validation.md` item 3, now applying to both
   shaders.)
4. **Acceptance criterion: the package survives a clean import into an empty URP
   project.** Fresh project on the URP template, add the package from disk,
   import a `.frag`, and confirm no compile errors, no shader errors, materials
   on `FragmentsUnity/URP/VertexColor` rather than the Standard fallback, and
   glass and translucent surfaces visibly blended.
5. **Exercise asset extraction against real sub-assets.** Only the path planning
   is tested. Run the menu command on an imported model and confirm the meshes
   and materials land under `Assets/Fragments/<ModelName>/`, that the copies are
   independent of the `.frag`, that re-running it does not overwrite, and that
   the over-long-path refusal fires rather than throwing.
6. **Exercise the progress bar and cancel.** Confirm the bar appears on a large
   model, that its stages advance monotonically, that Cancel stops the build
   promptly, and that a cancelled import leaves an import error and no partial
   asset — including that the destroyed meshes and materials do not leak into
   the asset file.
7. **Switching render pipeline after import does not re-import.** The importer
   declares a dependency on the shader *file*
   (`ctx.DependsOnSourceAsset(ActiveShaderAssetPath)`) but nothing declares a
   dependency on which pipeline is active. Assign or clear a render pipeline
   asset and existing models keep the materials they were built with, silently,
   until something else triggers a re-import. Confirm this, and decide whether
   to document it or to add a dependency on the graphics settings.
8. **Confirm the inspectors and the filter window draw and respond.** The model
   inspector's summary and Global Id search, the element record and its
   foldouts, the row ceilings and the "… n more not shown" overflow, the copy
   button writing to the system copy buffer, and the filter window's toggle,
   isolate, attribute filter and refresh — all untried.
