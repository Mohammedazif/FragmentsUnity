using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height)
        {
            this.x = x; this.y = y; this.width = width; this.height = height;
        }
    }

    public class GUIContent
    {
        public GUIContent() { }
        public GUIContent(string text) { this.text = text; }
        public GUIContent(string text, string tooltip) { this.text = text; this.tooltip = tooltip; }
        public string text { get; set; }
        public string tooltip { get; set; }
    }

    public class GUIStyle
    {
        public GUIStyle() { }
        public GUIStyle(GUIStyle other) { }
        public bool wordWrap { get; set; }
        public bool richText { get; set; }
        public FontStyle fontStyle { get; set; }
    }

    public enum FontStyle { Normal = 0, Bold = 1, Italic = 2, BoldAndItalic = 3 }

    public static class GUILayoutUtils
    {
        public static readonly GUILayoutOption[] None = Array.Empty<GUILayoutOption>();
    }

    public sealed class GUILayoutOption { }

    public static class GUILayout
    {
        public static bool Button(string text, params GUILayoutOption[] options) => false;
        public static bool Button(GUIContent content, params GUILayoutOption[] options) => false;
        public static void Label(string text, params GUILayoutOption[] options) { }
        public static void Label(GUIContent content, params GUILayoutOption[] options) { }
        public static void Space(float pixels) { }
        public static void FlexibleSpace() { }
        public static void BeginHorizontal(params GUILayoutOption[] options) { }
        public static void EndHorizontal() { }
        public static void BeginVertical(params GUILayoutOption[] options) { }
        public static void EndVertical() { }
        public static Vector2 BeginScrollView(Vector2 scrollPosition, params GUILayoutOption[] options) => scrollPosition;
        public static void EndScrollView() { }
        public static GUILayoutOption Width(float width) => new GUILayoutOption();
        public static GUILayoutOption Height(float height) => new GUILayoutOption();
        public static GUILayoutOption ExpandWidth(bool expand) => new GUILayoutOption();
    }

    public static class GUIUtility
    {
        public static string systemCopyBuffer { get; set; } = string.Empty;
    }

    public static class GUI
    {
        public static bool enabled { get; set; } = true;
    }
}

namespace UnityEngine.Rendering
{
    public class RenderPipelineAsset : UnityEngine.ScriptableObject { }

    public static class GraphicsSettings
    {
        public static RenderPipelineAsset currentRenderPipeline { get; set; }
        public static RenderPipelineAsset defaultRenderPipeline { get; set; }
    }
}

namespace UnityEditor
{
    public class Editor : UnityEngine.ScriptableObject
    {
        public UnityEngine.Object target { get; set; }
        public UnityEngine.Object[] targets { get; set; } = Array.Empty<UnityEngine.Object>();
        public virtual void OnInspectorGUI() { }
        public void DrawDefaultInspector() { }
        public static Editor CreateEditor(UnityEngine.Object obj) => null;
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CustomEditor : Attribute
    {
        public CustomEditor(Type inspectedType) { }
        public CustomEditor(Type inspectedType, bool editorForChildClasses) { }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MenuItem : Attribute
    {
        public MenuItem(string itemName) { }
        public MenuItem(string itemName, bool isValidateFunction) { }
        public MenuItem(string itemName, bool isValidateFunction, int priority) { }
    }

    public class EditorWindow : UnityEngine.ScriptableObject
    {
        public UnityEngine.GUIContent titleContent { get; set; } = new UnityEngine.GUIContent();
        public UnityEngine.Rect position { get; set; }
        public static T GetWindow<T>() where T : EditorWindow => UnityEngine.ScriptableObject.CreateInstance<T>();
        public static T GetWindow<T>(string title) where T : EditorWindow => UnityEngine.ScriptableObject.CreateInstance<T>();
        public static T GetWindow<T>(bool utility, string title, bool focus) where T : EditorWindow
            => UnityEngine.ScriptableObject.CreateInstance<T>();
        public void Show() { }
        public void Close() { }
        public void Repaint() { }
        public virtual void OnGUI() { }
    }

    public static class EditorGUILayout
    {
        public static void LabelField(string label, params UnityEngine.GUILayoutOption[] options) { }
        public static void LabelField(string label, string label2, params UnityEngine.GUILayoutOption[] options) { }
        public static void LabelField(UnityEngine.GUIContent label, params UnityEngine.GUILayoutOption[] options) { }
        public static void SelectableLabel(string text, params UnityEngine.GUILayoutOption[] options) { }
        public static string TextField(string label, string text, params UnityEngine.GUILayoutOption[] options) => text;
        public static bool Foldout(bool foldout, string content, bool toggleOnLabelClick) => foldout;
        public static bool ToggleLeft(string label, bool value, params UnityEngine.GUILayoutOption[] options) => value;
        public static bool Toggle(string label, bool value, params UnityEngine.GUILayoutOption[] options) => value;
        public static int Popup(string label, int selectedIndex, string[] displayedOptions) => selectedIndex;
        public static void Space() { }
        public static void HelpBox(string message, MessageType type) { }
        public static void BeginHorizontal(params UnityEngine.GUILayoutOption[] options) { }
        public static void EndHorizontal() { }
        public static void BeginVertical(params UnityEngine.GUILayoutOption[] options) { }
        public static void EndVertical() { }
        public static UnityEngine.Object ObjectField(
            string label, UnityEngine.Object obj, Type objType, bool allowSceneObjects) => obj;
    }

    public enum MessageType { None = 0, Info = 1, Warning = 2, Error = 3 }

    public static class EditorGUIUtility
    {
        public static float singleLineHeight => 18f;
        public static void PingObject(UnityEngine.Object obj) { }
    }

    public static class EditorUtility
    {
        public static bool DisplayCancelableProgressBar(string title, string info, float progress) => false;
        public static void DisplayProgressBar(string title, string info, float progress) { }
        public static void ClearProgressBar() { }
        public static bool DisplayDialog(string title, string message, string ok) => true;
        public static bool DisplayDialog(string title, string message, string ok, string cancel) => true;
        public static void SetDirty(UnityEngine.Object target) { }
    }

    public static class Selection
    {
        public static UnityEngine.GameObject activeGameObject { get; set; }
        public static UnityEngine.Object activeObject { get; set; }
        public static UnityEngine.GameObject[] gameObjects { get; set; } = Array.Empty<UnityEngine.GameObject>();
    }

    public static class EditorApplication
    {
        public static Action update { get; set; }
        public static bool isPlaying { get; set; }
    }
}
