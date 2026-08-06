# Scripting API

Everything an imported model exposes to C# lives in the `FragmentsUnity`
namespace, on three types: `FragmentModel` for queries, `FragmentPicker` for
resolving a raycast to an element, and `FragmentFilter` for visibility.

## FragmentModel — querying metadata

`FragmentModel` sits on the model root. It deserializes its metadata payload on
first access and caches it, so the first query after load is the expensive one.

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
        List<int> groundFloor = _model.FindByStorey("Level 1");
        Debug.Log($"{walls.Count} walls, {groundFloor.Count} elements on Level 1");

        foreach (KeyValuePair<string, string> value in _model.GetFlattenedValues(door.LocalId))
        {
            Debug.Log($"{value.Key} = {value.Value}");
        }
    }
}
```

### Lookups

| Member | Returns | Notes |
|---|---|---|
| `FindByGlobalId(string)` | `FragmentItemMetadata` or `null` | Matched case-insensitively. |
| `FindByLocalId(int)` | `FragmentItemMetadata` or `null` | The dense id that picking returns. |
| `FindByCategory(string)` | `List<int>` of local ids | Case-insensitive exact match on the IFC class, e.g. `"IFCWALL"`. |
| `FindByStorey(string)` | `List<int>` of local ids | Includes the storey element itself as well as everything contained in it. |
| `FindByAttribute(name, value, exactMatch)` | `List<int>` of local ids | Searches direct attributes first, then property-set members. |
| `GetCategoryCounts()` | `Dictionary<string, int>` | Category name → element count. |
| `GetStoreyCounts()` | `Dictionary<string, int>` | Storey name → element count. |
| `GetFlattenedValues(int localId)` | `Dictionary<string, string>` | One element's whole record as name → value. |
| `Data` | `FragmentModelData` | The deserialized payload, if you want to walk it yourself. |
| `ModelAsset` | `FragmentModelAsset` | The sub-asset holding the compressed payload. |

`FindByAttribute` with an empty `value` matches every element that carries the
attribute at all, whatever its value. With `exactMatch: false` it matches on
substring. Both name and value compare case-insensitively.

`GetFlattenedValues` merges an element's attributes, property sets, materials,
classifications, type and containment into one dictionary — the fastest way to
dump everything known about an element. Keys are namespaced so they cannot
collide:

| Source | Key form |
|---|---|
| Direct attribute | `Name` |
| Property / quantity set member | `SetName.PropertyName` |
| Classification | `Classification.Name` |
| Material | `Material.0`, `Material.1`, … |
| Type object | `TypeName` |
| Containing element | `ContainedIn` |
| Storey | `Storey` |

Keys compare case-insensitively and later duplicates win.

### What "every element" means

Queries run over the elements that **carry geometry**, deduplicated in
first-instance order. A `.frag` holds far more IFC records than that — property
sets, materials, type objects and relationship records are all in the file — and
persisting them all would produce a metadata payload far too large for a Unity
sub-asset. So `GetCategoryCounts` returns building elements rather than every
record, and an element with no geometry is not found by any query.

### FragmentItemMetadata

The record a lookup returns:

| Member | What it holds |
|---|---|
| `GlobalId` | The IFC GlobalId string. |
| `LocalId` | The dense id used by picking and filtering. |
| `ExpressId` | The element's express id in the source IFC. |
| `Category` | IFC class, e.g. `IFCWALLSTANDARDCASE`. |
| `Name` | The element's name. |
| `TypeName` | The type object's name, if any. |
| `ContainerName`, `ContainerCategory` | The element that contains this one, if any. |
| `StoreyName` | The resolved storey, walking up through containing ancestors. |
| `Attributes` | `List<FragmentAttribute>` of direct attributes. |
| `PropertySets` | `List<FragmentPropertySet>`; each carries a `FromType` flag marking sets inherited from the type object. |
| `Materials` | `List<FragmentMaterial>` with layer names and thicknesses. |
| `Classifications` | `List<FragmentAttribute>`. |
| `Relations` | `List<FragmentRelation>` — the raw IFC relationship targets. |
| `TypeLocalId`, `ContainerLocalId`, `StoreyLocalId` | The local ids behind the three names above, or `-1`. |

`FindAttribute(name)`, `FindPropertySet(name)` and
`FindProperty(setName, propertyName)` are case-insensitive lookups on a single
record, for when you want one value rather than the whole flattened dictionary.

## FragmentPicker — raycast to element

```csharp
if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
{
    if (FragmentPicker.TryGetMetadata(hit, out FragmentItemMetadata item))
    {
        Debug.Log($"{item.Category} '{item.Name}' — {item.GlobalId}");
    }
}
```

`FragmentPicker.TryGetMetadata(RaycastHit, out FragmentItemMetadata)` returns
the full IFC record behind a hit, or `false` with a null item when the ray hit
something that is not fragment geometry. It requires **Import Metadata** to have
been on at import.

`FragmentPicker.TryGetLocalId(RaycastHit, out int)` is the cheaper half: it
returns only the element's local id, which is all you need to drive
`FragmentFilter` or key your own selection set. It works regardless of whether
metadata was imported.

Both resolve the exact element in every import mode, including the merged ones
where many elements share one mesh. `hit.collider.gameObject` is **not** the
element in those modes — always go through `FragmentPicker`.
[Picking.md](Picking.md) covers colliders, layers and the surprises.

## FragmentFilter — showing and hiding

`FragmentFilter` sits on the model root beside `FragmentModel`, so filtering
works with no setup.

```csharp
var filter = model.GetComponent<FragmentFilter>();

filter.IsolateByStorey("Level 1");
filter.IsolateByCategory("IFCWALL");
filter.IsolateByAttribute("FireRating", "REI 60", exactMatch: true);
filter.IsolateLocalIds(new[] { 1234 });

filter.SetCategoryVisible("IFCWALL", false);
filter.SetStoreyVisible("Level 2", false);
filter.SetVisibleLocalIds(new[] { 1234, 5678 }, true);

filter.ClearFilter();
```

| Member | Effect |
|---|---|
| `IsolateByCategory(string)` | Shows only elements of one IFC category. |
| `IsolateByStorey(string)` | Shows only one storey and everything it contains. |
| `IsolateByAttribute(name, value, exactMatch)` | Shows only elements carrying that attribute or property value. |
| `IsolateLocalIds(IEnumerable<int>)` | Replaces the whole visible set. |
| `SetVisibleLocalIds(ids, visible)` | Toggles one set without disturbing the rest — the primitive behind the rest. |
| `SetCategoryVisible(category, visible)` | Toggles a category. |
| `SetStoreyVisible(storey, visible)` | Toggles a storey. |
| `ClearFilter()` | Restores everything. |

Hiding turns off renderers **and** colliders, so hidden elements are not
pickable either — a ray passes through them to whatever is behind. That is
usually what you want from an isolate; it does mean a pick that "stopped
working" may simply be filtered out.

How precisely a filter can hide depends on the import mode: the merged modes
hide a whole chunk at a time, and each call logs one warning saying so.
`FragmentVisibilityIndex.SupportsElementFiltering` on the model root tells you
which kind of build you have — it reads the build rather than the setting, so a
merged mode that fell back to a flat build reports element granularity.
[ImportModes.md](ImportModes.md) sets out the granularity per mode.

Filtering hides through a separate channel from the one the build uses to hide
`IfcSpace`-class volumes, so clearing a filter can never reveal geometry that
was meant to stay hidden. `ClearFilter` sweeps the serialized target list rather
than a runtime record, so it still works after a domain reload.

## Click to isolate

The local id from a pick is the same id `FragmentFilter` takes, so
click-to-isolate is one call:

```csharp
if (FragmentPicker.TryGetLocalId(hit, out int localId))
{
    hit.collider.GetComponentInParent<FragmentFilter>(true)
        .IsolateLocalIds(new[] { localId });
}
```
