# Picking Elements at Runtime

Picking an imported element is an ordinary `Physics.Raycast` followed by one
call to `FragmentPicker`. There is no fragment-specific camera, input handler,
or physics setting to configure.

```csharp
using FragmentsUnity;
using UnityEngine;

public sealed class FragmentClickInspector : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [SerializeField] private float _maxDistance = 500f;

    private void Update()
    {
        if (!Input.GetMouseButtonDown(0))
        {
            return;
        }

        Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, _maxDistance))
        {
            return;
        }

        if (!FragmentPicker.TryGetMetadata(hit, out FragmentItemMetadata item))
        {
            return;
        }

        Debug.Log($"{item.Category} '{item.Name}' — GlobalId {item.GlobalId}, local id {item.LocalId}, storey {item.StoreyName}");

        FragmentModel model = hit.collider.GetComponentInParent<FragmentModel>(true);
        foreach (var value in model.GetFlattenedValues(item.LocalId))
        {
            Debug.Log($"    {value.Key} = {value.Value}");
        }
    }
}
```

Drop that on any GameObject, assign a camera, and clicking the model logs the
element's identity and every attribute, property-set entry, classification and
material it carries. The example uses the legacy `Input` class; with the Input
System package, replace the two input lines and keep the rest.

## What the API gives you

`FragmentPicker.TryGetMetadata(hit, out FragmentItemMetadata)` returns the full
IFC record behind a hit, or `false` with a null item when the ray hit something
that is not fragment geometry. `FragmentPicker.TryGetLocalId(hit, out int)` is
the cheaper half: it returns only the element's local id, which is all you need
to drive `FragmentFilter` or to key your own selection set.

Metadata comes from the `FragmentModelAsset` wired into the `FragmentModel`
component on the model root, so it is available only if **Import Metadata** was
on at import. `TryGetLocalId` works regardless.

## Colliders are an import-time decision

The raycast can only hit what has a collider, and colliders are created only
when **Enable Element Picking** was checked at import. A model imported with it
off is not pickable at all — no amount of runtime code recovers that; re-import
with the option on.

## An imported model is solid — put it on its own layer

Those colliders are ordinary `MeshCollider`s, and a non-convex `MeshCollider`
cannot be a trigger. So out of the box the building is a solid physics body: it
stops character controllers, it is hit by every unrelated raycast and overlap
query in your project, and it bakes into the NavMesh. That is almost never what
a BIM viewer wants.

Unity has no per-collider channel filtering, so the way to keep an imported
model pickable without making it an obstacle is a **layer** the project excludes
from movement and from unrelated queries:

1. *Project Settings > Tags and Layers* — add a layer, for example `Fragments`,
   and note the index it was given. Never use layer 2: it is Unity's built-in
   `Ignore Raycast`, and `Physics.Raycast` skips those colliders, so picking
   silently stops working with no error and no warning.
2. Select the `.frag` asset and enter that index in **Collider Layer**, then
   Apply. The field is a plain integer, not a layer dropdown. Every object that
   receives a collider is moved onto that layer; objects without a collider —
   the model root and the hierarchy nodes — are left alone.
3. *Project Settings > Physics* — clear the whole row and column for that layer
   in the collision matrix, so nothing collides with the building.
4. Pass the layer explicitly when you want to hit it:

```csharp
private LayerMask _fragmentsLayer;

if (Physics.Raycast(ray, out RaycastHit hit, _maxDistance, _fragmentsLayer))
```

With the row cleared the model still answers picking raycasts — a `Raycast` with
an explicit `layerMask` ignores the collision matrix — but it no longer blocks
movement, no longer answers `Physics.OverlapSphere` on other layers, and can be
excluded from NavMesh baking by unchecking the layer in the bake settings.

Leaving **Collider Layer** at 0 keeps everything on Unity's `Default` layer,
which is the solid behaviour described above. That is the default only because
Unity has no unused layer a package can safely claim.

## Merged meshes resolve natively

In the merged import modes — Hierarchy Per Element, Hierarchy Per Storey and
Merged Whole Model — many elements share one welded mesh. Unity's `MeshCollider`
reports `hit.triangleIndex`, so the chunk's triangle-start table is
binary-searched and the exact element falls out. No project-wide physics setting
and no UV encoding is involved.

The practical consequence is that `hit.collider.gameObject` is **not** the
element in merged modes — it is a chunk shared by many elements. Always go
through `FragmentPicker` rather than reading components off the hit object, and
never parent a highlight or gizmo to the hit transform in a merged import.

## Two things that surprise people

Filtering disables colliders. When `FragmentFilter` or `FragmentVisibilityIndex`
hides geometry, it turns the colliders off with the renderers, so hidden
elements are not pickable — a ray passes through them to whatever is behind.
That is usually what you want from an isolate; it does mean a pick that
"stopped working" may simply be filtered out. `FragmentFilter.ClearFilter()`
restores both.

Spatial volumes are invisible but solid. `IfcSpace`, `IfcSite`, `IfcBuilding`,
`IfcOpeningElement` and `IfcAnnotation` import with their renderer disabled and
their collider intact. A ray can therefore stop on a room volume you cannot see.
If that is unwanted, check `item.Category` after the pick and re-cast, or use
`Physics.RaycastAll` and take the first hit whose category you accept.

## From a pick to a filter

The local id from a pick is the same id `FragmentFilter` takes, so
click-to-isolate is one call:

```csharp
if (FragmentPicker.TryGetLocalId(hit, out int localId))
{
    hit.collider.GetComponentInParent<FragmentFilter>(true)
        .IsolateLocalIds(new[] { localId });
}
```

In the merged modes this isolates the element's whole chunk rather than the
element alone, and logs a warning saying so — see [ImportModes.md](ImportModes.md)
for which modes hide precisely. `SetVisibleLocalIds(ids, visible)` is the toggle
form of the same primitive, for building up a selection instead of replacing it.
[ScriptingApi.md](ScriptingApi.md) lists the rest of the surface.
