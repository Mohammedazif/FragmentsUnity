using UnityEngine;

namespace FragmentsUnity
{
    /// <summary>Resolves a raycast hit on an imported model back to the element that was struck.</summary>
    public static class FragmentPicker
    {
        /// <summary>Reads the metadata behind a hit; false with a null item when the hit is not fragment geometry.</summary>
        public static bool TryGetMetadata(RaycastHit hit, out FragmentItemMetadata metadata)
        {
            metadata = null;

            if (!TryGetLocalId(hit, out int localId))
            {
                return false;
            }

            // Filtering may deactivate whole storeys, so the model root is searched through inactive parents.
            FragmentModel model = hit.collider.GetComponentInParent<FragmentModel>(true);
            if (model == null)
            {
                return false;
            }

            metadata = model.FindByLocalId(localId);
            return metadata != null;
        }

        /// <summary>Reads the element local id behind a hit; false with -1 when the hit is not fragment geometry.</summary>
        public static bool TryGetLocalId(RaycastHit hit, out int localId)
        {
            localId = -1;

            Collider collider = hit.collider;
            if (collider == null)
            {
                return false;
            }

            // Unity reports triangleIndex natively, so UE's UV0 part-index fallback is not ported; see UNITY_PORT.md section 6.
            FragmentElementTable table = collider.GetComponent<FragmentElementTable>();
            if (table != null)
            {
                // A merged chunk answers for itself alone; an unknown triangle stays unknown. Mirrors FragmentsActor.cpp:894-898
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
