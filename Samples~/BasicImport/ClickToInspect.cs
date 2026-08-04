using UnityEngine;

namespace FragmentsUnity.Samples
{
    /// <summary>Logs the IFC identity of whichever imported element is clicked.</summary>
    public sealed class ClickToInspect : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private float _maxDistance = 500f;
        [SerializeField] private LayerMask _pickLayers = ~0;

        private void Awake()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }
        }

        private void Update()
        {
            if (_camera == null || !Input.GetMouseButtonDown(0))
            {
                return;
            }

            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, _maxDistance, _pickLayers))
            {
                return;
            }

            if (!FragmentPicker.TryGetMetadata(hit, out FragmentItemMetadata item))
            {
                Debug.Log("Hit something that is not a fragment element, or the model was imported without metadata.");
                return;
            }

            string storey = string.IsNullOrEmpty(item.StoreyName) ? "(none)" : item.StoreyName;
            Debug.Log($"{item.Category} '{item.Name}' | GlobalId {item.GlobalId} | storey {storey}");
        }
    }
}
