using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public class Object
    {
        public string name { get; set; }
        public HideFlags hideFlags { get; set; }
        public static implicit operator bool(Object value) => !ReferenceEquals(value, null);
        public static Object Instantiate(Object original) => new Object { name = original.name };
        public static void Destroy(Object target) { }
        public static void DestroyImmediate(Object target) { }
    }

    [Flags]
    public enum HideFlags { None = 0, HideInHierarchy = 1, DontSave = 52 }

    public struct Vector2 { public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; } }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3(0f, 0f, 0f);
        public static Vector3 one => new Vector3(1f, 1f, 1f);
    }

    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity => new Quaternion(0f, 0f, 0f, 1f);
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public Color(float r, float g, float b) : this(r, g, b, 1f) { }
        public static Color white => new Color(1f, 1f, 1f, 1f);
    }

    public struct Bounds
    {
        public Vector3 center, size;
        public Bounds(Vector3 center, Vector3 size) { this.center = center; this.size = size; }
    }

    public static class Mathf
    {
        public static float Pow(float f, float p) => (float)Math.Pow(f, p);
    }

    public class Component : Object
    {
        public GameObject gameObject { get; internal set; }
        public Transform transform => gameObject != null ? gameObject.transform : null;
        public T GetComponent<T>() where T : Component => gameObject != null ? gameObject.GetComponent<T>() : null;
        public T GetComponentInParent<T>() where T : Component => GetComponentInParent<T>(false);

        public T GetComponentInParent<T>(bool includeInactive) where T : Component
        {
            GameObject current = gameObject;
            while (current != null)
            {
                if (includeInactive || current.activeSelf)
                {
                    T found = current.GetComponent<T>();
                    if (found != null)
                    {
                        return found;
                    }
                }
                else if (!includeInactive)
                {
                    return null;
                }
                current = current.transform.parent != null ? current.transform.parent.gameObject : null;
            }
            return null;
        }
    }

    public class Behaviour : Component { public bool enabled { get; set; } = true; }

    public class MonoBehaviour : Behaviour { }

    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject => Activator.CreateInstance<T>();
        public static ScriptableObject CreateInstance(Type type) => null;
    }

    public class Transform : Component
    {
        private readonly List<Transform> _children = new List<Transform>();

        public Vector3 localPosition { get; set; }
        public Quaternion localRotation { get; set; } = Quaternion.identity;
        public Transform parent { get; private set; }
        public int childCount => _children.Count;

        public Transform root
        {
            get
            {
                Transform current = this;
                while (current.parent != null)
                {
                    current = current.parent;
                }
                return current;
            }
        }

        public Transform GetChild(int index) => _children[index];

        public void SetParent(Transform newParent) => SetParent(newParent, true);

        public void SetParent(Transform newParent, bool worldPositionStays)
        {
            parent?._children.Remove(this);
            parent = newParent;
            newParent?._children.Add(this);
        }
    }

    public sealed class GameObject : Object
    {
        private readonly List<Component> _components = new List<Component>();

        public GameObject() : this("GameObject") { }

        public GameObject(string name)
        {
            this.name = name;
            transform = new Transform { gameObject = this };
        }

        public Transform transform { get; }

        public int layer { get; set; }

        public bool activeSelf { get; private set; } = true;

        public bool activeInHierarchy
        {
            get
            {
                GameObject current = this;
                while (current != null)
                {
                    if (!current.activeSelf)
                    {
                        return false;
                    }
                    current = current.transform.parent != null ? current.transform.parent.gameObject : null;
                }
                return true;
            }
        }

        public void SetActive(bool value) => activeSelf = value;

        public T AddComponent<T>() where T : Component
        {
            var component = Activator.CreateInstance<T>();
            component.gameObject = this;
            _components.Add(component);
            return component;
        }

        public T GetComponent<T>() where T : Component
        {
            foreach (Component component in _components)
            {
                if (component is T match)
                {
                    return match;
                }
            }
            return null;
        }

        public T[] GetComponentsInChildren<T>() where T : Component => GetComponentsInChildren<T>(false);

        public T[] GetComponentsInChildren<T>(bool includeInactive)
        {
            var found = new List<T>();
            Collect(this, includeInactive, found);
            return found.ToArray();
        }

        public T GetComponentInParent<T>() where T : Component => GetComponentInParent<T>(false);

        public T GetComponentInParent<T>(bool includeInactive) where T : Component
        {
            GameObject current = this;
            while (current != null)
            {
                if (includeInactive || current.activeSelf)
                {
                    T match = current.GetComponent<T>();
                    if (match != null)
                    {
                        return match;
                    }
                }
                current = current.transform.parent != null ? current.transform.parent.gameObject : null;
            }
            return null;
        }

        private static void Collect<T>(GameObject target, bool includeInactive, List<T> found)
        {
            if (!includeInactive && !target.activeSelf)
            {
                return;
            }
            foreach (Component component in target._components)
            {
                if (component is T match)
                {
                    found.Add(match);
                }
            }
            for (int i = 0; i < target.transform.childCount; i++)
            {
                Collect(target.transform.GetChild(i).gameObject, includeInactive, found);
            }
        }
    }

    public sealed class Mesh : Object
    {
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<Color> _colors = new List<Color>();
        private readonly List<int> _triangles = new List<int>();

        public Rendering.IndexFormat indexFormat { get; set; } = Rendering.IndexFormat.UInt16;
        public int vertexCount => _vertices.Count;
        public int subMeshCount { get; set; } = 1;
        public Bounds bounds { get; private set; }

        public void SetVertices(List<Vector3> inVertices) { _vertices.Clear(); _vertices.AddRange(inVertices); }
        public void SetNormals(List<Vector3> inNormals) { _normals.Clear(); _normals.AddRange(inNormals); }
        public void SetColors(List<Color> inColors) { _colors.Clear(); _colors.AddRange(inColors); }
        public void SetUVs(int channel, List<Vector2> uvs) { }
        public void SetTriangles(List<int> triangles, int submesh) => SetTriangles(triangles, submesh, true);

        public void SetTriangles(List<int> triangles, int submesh, bool calculateBounds)
        {
            _triangles.Clear();
            _triangles.AddRange(triangles);
        }

        public void RecalculateBounds() { }
        public void Clear() { _vertices.Clear(); _normals.Clear(); _colors.Clear(); _triangles.Clear(); }

        public Vector3[] vertices => _vertices.ToArray();
        public Vector3[] normals => _normals.ToArray();
        public Color[] colors => _colors.ToArray();
        public int[] triangles => _triangles.ToArray();
    }

    public sealed class Shader : Object
    {
        public static Shader Find(string name) => null;
    }

    public class Material : Object
    {
        private readonly Dictionary<string, float> _floats = new Dictionary<string, float>();

        public Material(Shader shader) { this.shader = shader; }
        public Shader shader { get; set; }
        public bool enableInstancing { get; set; }
        public int renderQueue { get; set; } = -1;
        public void SetInt(string name, int value) => _floats[name] = value;
        public void SetFloat(string name, float value) => _floats[name] = value;
        public float GetFloat(string name) => _floats.TryGetValue(name, out float value) ? value : 0f;
        public bool HasProperty(string name) => _floats.ContainsKey(name);
    }

    public sealed class MeshFilter : Component { public Mesh sharedMesh { get; set; } }

    // Renderer and Collider derive from Component, not Behaviour, and declare their own enabled flag.
    public class Renderer : Component
    {
        public bool enabled { get; set; } = true;
        public bool forceRenderingOff { get; set; }
        public Material sharedMaterial { get; set; }
    }

    public sealed class MeshRenderer : Renderer { }

    public class Collider : Component { public bool enabled { get; set; } = true; }

    public sealed class MeshCollider : Collider
    {
        public Mesh sharedMesh { get; set; }
        public bool convex { get; set; }
    }

    public struct RaycastHit
    {
        public Collider collider { get; set; }
        public int triangleIndex { get; set; }
        public Vector3 point { get; set; }
        public Vector3 normal { get; set; }
        public float distance { get; set; }
        public Transform transform => collider != null ? collider.transform : null;
    }

    // Unity's own Debug has no capture list; tests read this one in place of UnityEngine.TestTools.LogAssert.
    public static class Debug
    {
        public static List<string> capturedWarnings { get; } = new List<string>();

        public static void Log(object message) { }
        public static void Log(object message, Object context) { }
        public static void LogWarning(object message) => capturedWarnings.Add(message?.ToString());
        public static void LogWarning(object message, Object context) => capturedWarnings.Add(message?.ToString());
        public static void LogError(object message) { }
        public static void LogError(object message, Object context) { }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SerializeField : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class HideInInspector : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DisallowMultipleComponent : Attribute { }

    public sealed class Texture2D : Object { }
}

namespace UnityEngine.Rendering
{
    public enum IndexFormat { UInt16 = 0, UInt32 = 1 }
    public enum CullMode { Off = 0, Front = 1, Back = 2 }

    public enum BlendMode
    {
        Zero = 0, One = 1, DstColor = 2, SrcColor = 3, OneMinusDstColor = 4, SrcAlpha = 5,
        OneMinusSrcColor = 6, DstAlpha = 7, OneMinusDstAlpha = 8, SrcAlphaSaturate = 9, OneMinusSrcAlpha = 10
    }

    public enum RenderQueue
    {
        Background = 1000, Geometry = 2000, AlphaTest = 2450, GeometryLast = 2500,
        Transparent = 3000, Overlay = 4000
    }
}

namespace UnityEditor
{
    public static class AssetDatabase
    {
        public static T LoadAssetAtPath<T>(string assetPath) where T : UnityEngine.Object => null;
        public static UnityEngine.Object LoadAssetAtPath(string assetPath, Type type) => null;
        public static bool IsValidFolder(string path) => false;
        public static string CreateFolder(string parentFolder, string newFolderName) => string.Empty;
        public static string GenerateUniqueAssetPath(string path) => path;
        public static void CreateAsset(UnityEngine.Object asset, string path) { }
        public static void SaveAssets() { }
    }
}

namespace UnityEditor.AssetImporters
{
    public class AssetImportContext
    {
        public string assetPath { get; set; }
        public void AddObjectToAsset(string identifier, UnityEngine.Object obj) { }
        public void AddObjectToAsset(string identifier, UnityEngine.Object obj, UnityEngine.Texture2D thumbnail) { }
        public void SetMainObject(UnityEngine.Object obj) { }
        public void LogImportError(string msg) { }
        public void LogImportWarning(string msg) { }
        public void DependsOnSourceAsset(string path) { }
    }

    public abstract class ScriptedImporter : UnityEngine.ScriptableObject
    {
        public abstract void OnImportAsset(AssetImportContext ctx);
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class ScriptedImporterAttribute : Attribute
    {
        public ScriptedImporterAttribute(int version, string ext) { }
        public ScriptedImporterAttribute(int version, string[] exts) { }
    }
}
