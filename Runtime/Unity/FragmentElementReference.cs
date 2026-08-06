using UnityEngine;

namespace FragmentsUnity
{
    /// <summary>Tags a spawned GameObject with its IFC local id.</summary>
    public sealed class FragmentElementReference : MonoBehaviour
    {
        [SerializeField] private int _localId = -1;

        public int LocalId
        {
            get { return _localId; }
            set { _localId = value; }
        }

        /// <summary>Null when there is no FragmentModel above this element, or the id is unknown.</summary>
        public FragmentItemMetadata GetMetadata()
        {
            // Filtering deactivates geometry-free nodes, so inactive ancestors must still resolve.
            FragmentModel model = GetComponentInParent<FragmentModel>(true);
            return model != null ? model.FindByLocalId(_localId) : null;
        }
    }
}
