using UnityEngine;

namespace FragmentsUnity
{
    /// <summary>Resolves a raycast hit on an imported model back to the element that was struck.</summary>
    public static class FragmentPicker
    {
        public static bool TryGetMetadata(RaycastHit hit, out FragmentItemMetadata metadata)
        {
            metadata = null;

            if (!TryGetLocalId(hit, out int localId))
            {
                return false;
            }

            // Filtering deactivates geometry-free nodes, so inactive ancestors must still resolve.
            FragmentModel model = hit.collider.GetComponentInParent<FragmentModel>(true);
            if (model == null)
            {
                return false;
            }

            metadata = model.FindByLocalId(localId);
            return metadata != null;
        }

        public static bool TryGetLocalId(RaycastHit hit, out int localId)
        {
            localId = -1;

            Collider collider = hit.collider;
            if (collider == null)
            {
                return false;
            }

            FragmentElementTable table = collider.GetComponent<FragmentElementTable>();
            if (table != null)
            {
                localId = table.FindLocalId(hit.triangleIndex);
                return localId >= 0;
            }

            FragmentElementReference reference = collider.GetComponentInParent<FragmentElementReference>(true);
            if (reference == null || reference.LocalId < 0)
            {
                return false;
            }

            localId = reference.LocalId;
            return true;
        }
    }
}
