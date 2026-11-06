using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

//Genera la escena del Parcial 1: arena abierta con bounds, 10 Boids, el Cazador (FSM Patrol/Attack/Gather),
//waypoints, cámara cenital y HUD. Menú: IA Parcial > Construir escena Parcial 1.
public static class Parcial1SceneBuilder
{
    public const string ScenePath = BuilderUtils.ScenesFolder + "/Parcial1.unity";

    private static readonly Vector2 ArenaSize = new Vector2(44f, 28f);
    private const int BoidCount = 10;

    [MenuItem("IA Parcial/Construir escena Parcial 1", priority = 1)]
    public static void BuildFromMenu()
    {
        if (!BuilderUtils.ConfirmReplaceScene(ScenePath, true)) return;
        Build();
        EditorUtility.DisplayDialog("Parcial 1", "Escena generada en " + ScenePath + ".\nApretá Play para ver la simulación.", "OK");
    }

    public static void Build()
    {
        BuilderUtils.EnsureProjectFolders();
        BuilderUtils.EnsureLayers();
        BuilderUtils.GetOrCreateFeedbackMaterial();

        GameObject boidPrefab = BuilderUtils.CreateBoidPrefab();
        GameObject interestPrefab = BuilderUtils.CreateInterestObjectPrefab();
        GameObject projectilePrefab = BuilderUtils.CreateProjectilePrefab();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ---------------- Entorno ----------------
        Transform environment = new GameObject("Entorno").transform;
        Camera camera = BuilderUtils.CreateCamera(environment, new Color(0.08f, 0.09f, 0.11f));
        BuilderUtils.CreateSun(environment);

        Material groundMat = BuilderUtils.GetOrCreateMaterial("P1_Ground", new Color(0.13f, 0.14f, 0.17f), true);
        Material arenaMat = BuilderUtils.GetOrCreateMaterial("P1_Arena", new Color(0.2f, 0.22f, 0.26f), true);
        Material borderMat = BuilderUtils.GetOrCreateMaterial("P1_Border", new Color(0.3f, 0.85f, 1f), true);
        Material waypointMat = BuilderUtils.GetOrCreateMaterial("Waypoint", new Color(0.25f, 0.75f, 1f), true);

        BuilderUtils.CreatePrimitive(PrimitiveType.Cube, "Suelo", environment,
            new Vector3(0f, -0.1f, 0f), new Vector3(ArenaSize.x + 12f, 0.1f, ArenaSize.y + 10f), groundMat);
        BuilderUtils.CreatePrimitive(PrimitiveType.Cube, "Arena", environment,
            new Vector3(0f, -0.04f, 0f), new Vector3(ArenaSize.x, 0.05f, ArenaSize.y), arenaMat);
        CreateBorder(environment, borderMat);

        GameObject boundsGo = BuilderUtils.CreateEmpty("Bounds (Boids)", environment, Vector3.zero);
        FlockBounds bounds = boundsGo.AddComponent<FlockBounds>();
        bounds.Setup(ArenaSize.x, ArenaSize.y, 2.5f);

        // ---------------- Waypoints ----------------
        Transform waypointsRoot = new GameObject("Waypoints Cazador").transform;
        Vector3[] waypointPositions =
        {
            new Vector3(-17f, 0f, -9f), new Vector3(0f, 0f, -11f), new Vector3(17f, 0f, -9f),
            new Vector3(17f, 0f, 9f), new Vector3(0f, 0f, 11f), new Vector3(-17f, 0f, 9f)
        };
        List<Transform> waypoints = new List<Transform>();
        for (int i = 0; i < waypointPositions.Length; i++)
        {
            GameObject wp = BuilderUtils.CreateEmpty($"WP {i + 1}", waypointsRoot, waypointPositions[i]);
            BuilderUtils.CreatePrimitive(PrimitiveType.Cylinder, "Marker", wp.transform,
                new Vector3(0f, 0.01f, 0f), new Vector3(0.5f, 0.01f, 0.5f), waypointMat);
            waypoints.Add(wp.transform);
        }

        // ---------------- Cazador ----------------
        HunterP1 hunter = CreateHunter(waypoints.ToArray(), bounds,
            interestPrefab.GetComponent<InterestObject>(), projectilePrefab.GetComponent<Projectile>());

        // ---------------- Boids ----------------
        Transform boidsRoot = new GameObject("Boids").transform;
        Random.State previousState = Random.state;
        Random.InitState(1234);
        Boid[] boids = new Boid[BoidCount];
        for (int i = 0; i < BoidCount; i++)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(boidPrefab, boidsRoot);
            instance.name = $"Boid {i + 1:00}";
            Vector3 position = new Vector3(Random.Range(0f, 15f), 0f, Random.Range(-8f, 8f));
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            boids[i] = instance.GetComponent<Boid>();
            BuilderUtils.Set(boids[i], "_bounds", bounds);
        }
        Random.state = previousState;

        // ---------------- HUD y cámara ----------------
        GameObject hudGo = new GameObject("HUD");
        FlockSummary summary = hudGo.AddComponent<FlockSummary>();
        summary.Setup(boids);
        SimulationHUD hud = hudGo.AddComponent<SimulationHUD>();
        CameraRig rig = camera.gameObject.AddComponent<CameraRig>();
        rig.Configure(Vector2.zero, ArenaSize, hunter.transform);
        hud.Configure("PARCIAL 1 - Boids + Cazador (FSM)", new MonoBehaviour[] { hunter, summary }, rig);

        EditorSceneManager.SaveScene(scene, ScenePath);
        BuilderUtils.RegisterScenesInBuildSettings(ScenePath, Parcial2ScenePath());
        AssetDatabase.SaveAssets();
        Debug.Log("[Builder] Escena del Parcial 1 generada: " + ScenePath);
    }

    private static string Parcial2ScenePath() => BuilderUtils.ScenesFolder + "/Parcial2.unity";

    private static void CreateBorder(Transform parent, Material material)
    {
        float t = 0.15f;
        float hx = ArenaSize.x * 0.5f;
        float hz = ArenaSize.y * 0.5f;
        BuilderUtils.CreatePrimitive(PrimitiveType.Cube, "Borde N", parent, new Vector3(0f, 0f, hz), new Vector3(ArenaSize.x + t, 0.05f, t), material);
        BuilderUtils.CreatePrimitive(PrimitiveType.Cube, "Borde S", parent, new Vector3(0f, 0f, -hz), new Vector3(ArenaSize.x + t, 0.05f, t), material);
        BuilderUtils.CreatePrimitive(PrimitiveType.Cube, "Borde E", parent, new Vector3(hx, 0f, 0f), new Vector3(t, 0.05f, ArenaSize.y + t), material);
        BuilderUtils.CreatePrimitive(PrimitiveType.Cube, "Borde O", parent, new Vector3(-hx, 0f, 0f), new Vector3(t, 0.05f, ArenaSize.y + t), material);
    }

    private static HunterP1 CreateHunter(Transform[] waypoints, FlockBounds bounds, InterestObject interestPrefab, Projectile projectilePrefab)
    {
        GameObject root = AgentVisualFactory.CreateHunterBody("Cazador", out Renderer bodyRenderer);
        root.transform.position = waypoints[0].position;
        BuilderUtils.SetLayerRecursively(root, BuilderUtils.Layer("Hunter"));

        HunterP1 hunter = root.AddComponent<HunterP1>();
        BuilderUtils.Set(hunter, "_maxSpeed", 5.5f);
        BuilderUtils.Set(hunter, "_maxForce", 18f);
        BuilderUtils.Set(hunter, "_radius", 0.45f);
        BuilderUtils.Set(hunter, "TBA", 3f);
        BuilderUtils.Set(hunter, "RangeAttackRadius", 8f);
        BuilderUtils.Set(hunter, "MeleeAttackRadius", 3.5f);
        BuilderUtils.Set(hunter, "VisionRadius", 11f);
        BuilderUtils.Set(hunter, "_meleeHitDistance", 1.2f);
        BuilderUtils.Set(hunter, "_meleeDamage", 100f);
        BuilderUtils.Set(hunter, "_rangedDamage", 100f);
        BuilderUtils.Set(hunter, "_gatherTime", 2.5f);
        BuilderUtils.Set(hunter, "_spawnInterval", 4f);
        BuilderUtils.Set(hunter, "_maxObjects", 5);
        BuilderUtils.Set(hunter, "_spawnArea", bounds);
        BuilderUtils.Set(hunter, "_projectilePrefab", projectilePrefab);
        BuilderUtils.Set(hunter, "_interestObjectPrefab", interestPrefab);
        BuilderUtils.Set(hunter, "_boidMask", BuilderUtils.Mask("Boid"));
        BuilderUtils.Set(hunter, "_renderers", new Renderer[] { bodyRenderer });
        BuilderUtils.Set(hunter, "_patrolData.waypoints", waypoints);
        BuilderUtils.Set(hunter, "_patrolData.mode", PatrolMode.Loop);
        BuilderUtils.Set(hunter, "_patrolData.waypointCheckDistance", 0.8f);
        BuilderUtils.Set(hunter, "_patrolSpeed", 4f);
        return hunter;
    }
}
