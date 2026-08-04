using UnityEditor;

namespace FragmentsUnity.Editor
{
    /// <summary>Inspector for a spawned element: its IFC metadata inline, with the same copy affordance as the model root.</summary>
    [CustomEditor(typeof(FragmentElementReference))]
    public sealed class FragmentElementInspector : UnityEditor.Editor
    {
        private bool _showAttributes = true;
        private bool _showPropertySets;
        private bool _showMaterials;
        private bool _showClassifications;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var reference = target as FragmentElementReference;
            FragmentItemMetadata item = reference != null ? reference.GetMetadata() : null;
            if (!FragmentMetadataFormatter.HasDisplayableMetadata(item))
            {
                FragmentMetadataInspectorGUI.DrawMissingMetadataHelp();
                return;
            }

            EditorGUILayout.Space();
            FragmentMetadataInspectorGUI.DrawIdentity(item);
            _showAttributes = FragmentMetadataInspectorGUI.DrawAttributes(item, _showAttributes);
            _showPropertySets = FragmentMetadataInspectorGUI.DrawPropertySets(item, _showPropertySets);
            _showMaterials = FragmentMetadataInspectorGUI.DrawMaterials(item, _showMaterials);
            _showClassifications = FragmentMetadataInspectorGUI.DrawClassifications(item, _showClassifications);
            FragmentMetadataInspectorGUI.DrawCopyButton(item);
        }
    }
}
