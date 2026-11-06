using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

//Utilidades compartidas por los builders de escena (materiales, prefabs, capas, asignación de campos serializados).
public static class BuilderUtils
{
    public const string MaterialsFolder = "Assets/Materials";
    public const string PrefabsFolder = "Assets/Prefabs";
    public const string ScenesFolder = "Assets/Scenes";
    public const string MeshesFolder = "Assets/Meshes";
    public const string ResourcesFolder = "Assets/Resources";

    public static readonly string[] RequiredLayers = { "Boid", "Hunter", "Interest", "Obstacle", "Wolf", "SafeZone" };

    // ------------------------------------------------------------------ carpetas y capas

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string folderName = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folderName);
    }

    public static void EnsureProjectFolders()
    {
        EnsureFolder(MaterialsFolder);
        EnsureFolder(PrefabsFolder);
        EnsureFolder(ScenesFolder);
        EnsureFolder(MeshesFolder);
        EnsureFolder(ResourcesFolder);
    }

    //Crea las capas necesarias en el TagManager si todavía no existen.
    public static void EnsureLayers()
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0) return;

        SerializedObject tagManager = new SerializedObject(assets[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        bool changed = false;

        foreach (string layerName in RequiredLayers)
        {
            if (LayerMask.NameToLayer(layerName) >= 0) continue;
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue)) continue;
                slot.stringValue = layerName;
                changed = true;
                break;
            }
        }

        if (changed) tagManager.ApplyModifiedPropertiesWithoutUndo();
    }

    public static int Layer(string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0) throw new InvalidOperationException($"Falta la capa '{layerName}' en Project Settings > Tags and Layers.");
        return layer;
    }

    public static LayerMask Mask(params string[] layerNames) => LayerMask.GetMask(layerNames);

    public static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
    }

    // ------------------------------------------------------------------ materiales

    public static Material GetOrCreateMaterial(string materialName, Color color, bool unlit = false)
    {
        EnsureFolder(MaterialsFolder);
        string path = $"{MaterialsFolder}/{materialName}.mat";
        Shader shader = FindSurfaceShader(unlit);

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        SetMaterialColor(material, color);
        EditorUtility.SetDirty(material);
        return material;
    }

    //Material transparente con color por vértice para líneas y conos de visión (Resources/FeedbackLine).
    public static Material GetOrCreateFeedbackMaterial()
    {
        EnsureFolder(ResourcesFolder);
        string path = $"{ResourcesFolder}/FeedbackLine.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(material, path);
        }
        return material;
    }

    private static Shader FindSurfaceShader(bool unlit)
    {
        string[] candidates = unlit
            ? new[] { "Universal Render Pipeline/Unlit", "Unlit/Color", "Standard" }
            : new[] { "Universal Render Pipeline/Lit", "Standard" };

        foreach (string candidate in candidates)
        {
            Shader shader = Shader.Find(candidate);
            if (shader != null) return shader;
        }
        return Shader.Find("Sprites/Default");
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
    }

    // ------------------------------------------------------------------ geometría

    public static GameObject CreatePrimitive(PrimitiveType type, string objectName, Transform parent,
        Vector3 localPosition, Vector3 localScale, Material material, bool keepCollider = false)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = objectName;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    public static GameObject CreateEmpty(string objectName, Transform parent, Vector3 position)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        return go;
    }

    //Malla de "flecha" (pirámide) apuntando a +Z para representar a los Boids.
    public static Mesh GetOrCreateArrowMesh()
    {
        EnsureFolder(MeshesFolder);
        string path = $"{MeshesFolder}/BoidArrow.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null) return mesh;

        Vector3 tip = new Vector3(0f, 0.25f, 0.6f);
        Vector3 left = new Vector3(-0.38f, 0.18f, -0.4f);
        Vector3 right = new Vector3(0.38f, 0.18f, -0.4f);
        Vector3 top = new Vector3(0f, 0.45f, -0.3f);
        Vector3 bottom = new Vector3(0f, 0.02f, -0.3f);

        Vector3[][] faces =
        {
            new[] { tip, left, top }, new[] { tip, top, right },
            new[] { tip, bottom, left }, new[] { tip, right, bottom },
            new[] { left, bottom, top }, new[] { right, top, bottom }
        };

        Vector3 centroid = (tip + left + right + top + bottom) / 5f;
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        foreach (Vector3[] f in faces)
        {
            Vector3 a = f[0], b = f[1], c = f[2];
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(normal, (a + b + c) / 3f - centroid) < 0f)
            {
                Vector3 tmp = b; b = c; c = tmp; // asegurar que la cara mire hacia afuera
            }
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        }

        mesh = new Mesh { name = "BoidArrow" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    // ------------------------------------------------------------------ componentes

    public static T AddKinematicBody<T>(GameObject go) where T : Collider
    {
        T collider = go.AddComponent<T>();
        collider.isTrigger = true;
        Rigidbody body = go.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        return collider;
    }

    //Asigna un campo serializado (público o privado con [SerializeField]) usando SerializedObject.
    public static void Set(Object target, string propertyPath, object value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(propertyPath);
        if (property == null)
        {
            Debug.LogError($"[Builder] No se encontró '{propertyPath}' en {target.GetType().Name}");
            return;
        }

        switch (value)
        {
            case null:
                property.objectReferenceValue = null;
                break;
            case Object[] array:
                property.arraySize = array.Length;
                for (int i = 0; i < array.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = array[i];
                break;
            case Object reference:
                property.objectReferenceValue = reference;
                break;
            case LayerMask mask:
                property.intValue = mask.value;
                break;
            case Enum enumValue:
                property.intValue = Convert.ToInt32(enumValue);
                break;
            case int intValue:
                property.intValue = intValue;
                break;
            case float floatValue:
                property.floatValue = floatValue;
                break;
            case bool boolValue:
                property.boolValue = boolValue;
                break;
            case string stringValue:
                property.stringValue = stringValue;
                break;
            case Vector2 v2:
                property.vector2Value = v2;
                break;
            case Vector3 v3:
                property.vector3Value = v3;
                break;
            case Color color:
                property.colorValue = color;
                break;
            default:
                Debug.LogError($"[Builder] Tipo no soportado para '{propertyPath}': {value.GetType().Name}");
                return;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static WorldLabel AddLabel(GameObject go, Vector3 offset, float characterSize, Color color)
    {
        WorldLabel label = go.AddComponent<WorldLabel>();
        Set(label, "_offset", offset);
        Set(label, "_characterSize", characterSize);
        Set(label, "_color", color);
        return label;
    }

    // ------------------------------------------------------------------ prefabs compartidos

    public static GameObject SavePrefab(GameObject source, string prefabName)
    {
        EnsureFolder(PrefabsFolder);
        string path = $"{PrefabsFolder}/{prefabName}.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, path);
        Object.DestroyImmediate(source);
        return prefab;
    }

    public static GameObject CreateBoidPrefab()
    {
        Material boidMaterial = GetOrCreateMaterial("Boid", new Color(0.55f, 0.82f, 1f));

        GameObject root = new GameObject("Boid");
        SphereCollider collider = AddKinematicBody<SphereCollider>(root);
        collider.radius = 0.4f;
        collider.center = new Vector3(0f, 0.5f, 0f);

        GameObject body = new GameObject("Body");
        body.transform.SetParent(root.transform, false);
        body.AddComponent<MeshFilter>().sharedMesh = GetOrCreateArrowMesh();
        MeshRenderer bodyRenderer = body.AddComponent<MeshRenderer>();
        bodyRenderer.sharedMaterial = boidMaterial;

        AddLabel(root, new Vector3(0f, 1.5f, 0.7f), 0.15f, Color.white);
        Boid agent = root.AddComponent<Boid>();
        Set(agent, "_maxSpeed", 3.6f);
        Set(agent, "_maxForce", 12f);
        Set(agent, "_radius", 0.38f);
        Set(agent, "_boidMask", Mask("Boid"));
        Set(agent, "_hunterMask", Mask("Hunter"));
        Set(agent, "_foodMask", Mask("Interest"));
        Set(agent, "_renderers", new Renderer[] { bodyRenderer });

        SetLayerRecursively(root, Layer("Boid"));
        return SavePrefab(root, "Boid");
    }

    public static GameObject CreateInterestObjectPrefab()
    {
        Material material = GetOrCreateMaterial("InterestObject", new Color(0.3f, 0.85f, 0.45f));

        GameObject root = new GameObject("InterestObject");
        SphereCollider collider = AddKinematicBody<SphereCollider>(root);
        collider.center = new Vector3(0f, 0.4f, 0f);

        GameObject visual = CreatePrimitive(PrimitiveType.Cylinder, "Visual", root.transform,
            new Vector3(0f, 0.25f, 0f), new Vector3(1.2f, 0.25f, 1.2f), material);

        AddLabel(root, new Vector3(0f, 1.1f, 0.6f), 0.14f, new Color(0.8f, 1f, 0.8f));
        InterestObject interest = root.AddComponent<InterestObject>();
        Set(interest, "_radius", 0.6f);
        Set(interest, "_visual", visual.transform);

        SetLayerRecursively(root, Layer("Interest"));
        return SavePrefab(root, "InterestObject");
    }

    public static GameObject CreateProjectilePrefab()
    {
        Material material = GetOrCreateMaterial("Projectile", new Color(1f, 0.88f, 0.3f), true);

        GameObject root = new GameObject("Projectile");
        CreatePrimitive(PrimitiveType.Sphere, "Visual", root.transform, Vector3.zero, Vector3.one * 0.3f, material);

        TrailRenderer trail = root.AddComponent<TrailRenderer>();
        trail.sharedMaterial = GetOrCreateFeedbackMaterial();
        trail.time = 0.25f;
        trail.startWidth = 0.18f;
        trail.endWidth = 0f;
        trail.startColor = new Color(1f, 0.9f, 0.3f, 0.9f);
        trail.endColor = new Color(1f, 0.6f, 0.2f, 0f);
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        root.AddComponent<Projectile>();
        return SavePrefab(root, "Projectile");
    }

    // ------------------------------------------------------------------ escena

    public static Camera CreateCamera(Transform parent, Color background)
    {
        GameObject go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(new Vector3(0f, 60f, 0f), Quaternion.Euler(90f, 0f, 0f));

        Camera cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 18f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = background;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 200f;
        go.AddComponent<AudioListener>();
        return cam;
    }

    public static Light CreateSun(Transform parent)
    {
        GameObject go = new GameObject("Directional Light");
        go.transform.SetParent(parent, false);
        go.transform.rotation = Quaternion.Euler(60f, -30f, 0f);
        Light light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.45f;
        return light;
    }

    public static void RegisterScenesInBuildSettings(params string[] scenePaths)
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();
        foreach (string path in scenePaths)
        {
            if (File.Exists(path)) scenes.Add(new EditorBuildSettingsScene(path, true));
        }
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    public static bool ConfirmReplaceScene(string scenePath, bool interactive)
    {
        if (!interactive) return true;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
        if (!File.Exists(scenePath)) return true;
        return EditorUtility.DisplayDialog("Reconstruir escena",
            $"Se va a regenerar '{scenePath}' desde cero. ¿Continuar?", "Sí, regenerar", "Cancelar");
    }
}
