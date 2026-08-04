using UnityEngine;

namespace FragmentsUnity
{
    /// <summary>Tags a spawned GameObject with its IFC local id so metadata resolves through the model root.</summary>
    public sealed class FragmentElementReference : MonoBehaviour
    {
        [SerializeField] private int _localId = -1;

        public int LocalId
        {
            get { return _localId; }
            set { _localId = value; }
        }

        /// <summary>Resolves this element's metadata via the FragmentModel above it; null when detached or unknown.</summary>
        public FragmentItemMetadata GetMetadata()
        {
            // Filtering hides whole storeys, so inactive ancestors must still resolve; mirrors FragmentsMetadataComponent.cpp:201
            FragmentModel model = GetComponentInParent<FragmentModel>(true);
            return model != null ? model.FindByLocalId(_localId) : null;
        }
    }
}
