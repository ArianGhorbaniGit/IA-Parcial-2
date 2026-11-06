using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

//Genera la escena del Parcial 2: mapa con pasillos, salas y zonas; grafo de navegación (nodos + conexiones por
//SphereCast); 4 lobos con rutas de patrulla; el Cazador (FSM Search/Attack/Gather/Flee/Exit/Dead);
//10 Boids en el Coto de caza; cámara y HUD. Menú: IA Parcial > Construir escena Parcial 2.
public static class Parcial2SceneBuilder
{
    public const string ScenePath = BuilderUtils.ScenesFolder + "/Parcial2.unity";
    private const int BoidCount = 10;
    private const float WallHeight = 1.6f;

    [MenuItem("IA Parcial/Construir escena Parcial 2", priority = 2)]
    public static void BuildFromMenu()
    {
        if (!BuilderUtils.ConfirmReplaceScene(ScenePath, true)) return;
        Build();
        EditorUtility.DisplayDialog("Parcial 2", "Escena generada en " + ScenePath + ".\nApretá Play para ver la simulación.", "OK");
    }

    public static void Build()
    {
        BuilderUtils.EnsureProjectFolders();
        BuilderUtils.EnsureLayers();
        BuilderUtils.GetOrCreateFeedbackMaterial();

        GameObject boidPrefab = LoadOrCreatePrefab<Boid>("Boid", BuilderUtils.CreateBoidPrefab);
        GameObject interestPrefab = LoadOrCreatePrefab<InterestObject>("InterestObject", BuilderUtils.CreateInterestObjectPrefab);
        GameObject projectilePrefab = LoadOrCreatePrefab<Projectile>("Projectile", BuilderUtils.CreateProjectilePrefab);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Transform environment = new GameObject("Entorno").transform;
        Camera camera = BuilderUtils.CreateCamera(environment, new Color(0.07f, 0.08f, 0.1f));
        BuilderUtils.CreateSun(environment);

        // ---------------- Mapa ----------------
        BuildFloorsAndWalls(environment);
        Dictionary<string, MapZone> zones = BuildZones(environment);
        MapZone coto = zones["Coto de caza"];
        FlockBounds cotoBounds = coto.gameObject.AddComponent<FlockBounds>();
        Rect cotoRect = Parcial2Layout.Zones[1].Area;
        cotoBounds.Setup(cotoRect.width, cotoRect.height, 2.5f);

        // ---------------- Grafo ----------------
        Graph graph = BuildGraph();

        // ---------------- Puntos clave ----------------
        Transform markers = new GameObject("Puntos clave").transform;
        Transform spawnPoint = BuilderUtils.CreateEmpty("Spawn Cazador", markers, Parcial2Layout.ToWorld(Parcial2Layout.HunterSpawn)).transform;
        Transform exitPoint = BuilderUtils.CreateEmpty("Punto de Salida", markers, Parcial2Layout.ToWorld(Parcial2Layout.ExitPoint)).transform;
        Material exitMat = BuilderUtils.GetOrCreateMaterial("P2_ExitMarker", new Color(0.45f, 0.7f, 1f), true);
        BuilderUtils.CreatePrimitive(PrimitiveType.Cylinder, "Marcador", exitPoint, new Vector3(0f, 0.02f, 0f), new Vector3(1.6f, 0.02f, 1.6f), exitMat);

        // ---------------- Cazador ----------------
        List<MapZone> safeZones = new List<MapZone> { zones["Zona segura A"], zones["Zona segura B"] };
        HunterP2 hunter = CreateHunter(spawnPoint, exitPoint, zones["Zona inicial"], coto, cotoBounds, safeZones.ToArray(),
            interestPrefab.GetComponent<InterestObject>(), projectilePrefab.GetComponent<Projectile>());

        // ---------------- Lobos ----------------
        Wolf[] wolves = CreateWolves();

        // ---------------- Boids ----------------
        Transform boidsRoot = new GameObject("Boids").transform;
        Random.State previous = Random.state;
        Random.InitState(4321);
        Boid[] boids = new Boid[BoidCount];
        for (int i = 0; i < BoidCount; i++)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(boidPrefab, boidsRoot);
            instance.name = $"Boid {i + 1:00}";
            Vector3 position = cotoBounds.Center + new Vector3(Random.Range(-9f, 9f), 0f, Random.Range(-5f, 5f));
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            boids[i] = instance.GetComponent<Boid>();
            BuilderUtils.Set(boids[i], "_bounds", cotoBounds);
            EnlargeLabel(instance, 0.22f, new Vector3(0f, 1.8f, 0.9f));
        }
        Random.state = previous;

        // ---------------- HUD y cámara ----------------
        GameObject hudGo = new GameObject("HUD");
        FlockSummary flockSummary = hudGo.AddComponent<FlockSummary>();
        flockSummary.Setup(boids);
        WolfPackSummary wolfSummary = hudGo.AddComponent<WolfPackSummary>();
        wolfSummary.Setup(wolves);
        SimulationHUD hud = hudGo.AddComponent<SimulationHUD>();
        CameraRig rig = camera.gameObject.AddComponent<CameraRig>();
        rig.Configure(Vector2.zero, new Vector2(Parcial2Layout.Width, Parcial2Layout.Height), hunter.transform);
        hud.Configure("PARCIAL 2 - Percepción, grafos y A*", new MonoBehaviour[] { hunter, wolfSummary, flockSummary }, rig);

        EditorSceneManager.SaveScene(scene, ScenePath);
        BuilderUtils.RegisterScenesInBuildSettings(Parcial1SceneBuilder.ScenePath, ScenePath);
        AssetDatabase.SaveAssets();

        int connections = 0;
        foreach (Node node in graph.GetAllNodes) connections += node.Neighbors.Count;
        Debug.Log($"[Builder] Escena del Parcial 2 generada: {ScenePath} ({graph.GetAllNodes.Count} nodos, {connections / 2} conexiones)");
    }

    //El mapa del Parcial 2 se ve más lejos que la arena del Parcial 1: etiquetas más grandes.
    private static void EnlargeLabel(GameObject go, float characterSize, Vector3 offset)
    {
        WorldLabel label = go.GetComponent<WorldLabel>();
        if (label == null) return;
        BuilderUtils.Set(label, "_characterSize", characterSize);
        BuilderUtils.Set(label, "_offset", offset);
    }

    //Usa el prefab que ya existe, salvo que le falte el script (por ejemplo, un prefab de una version vieja)
    private static GameObject LoadOrCreatePrefab<T>(string prefabName, System.Func<GameObject> create) where T : Component
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>($"{BuilderUtils.PrefabsFolder}/{prefabName}.prefab");
        return existing != null && existing.GetComponent<T>() != null ? existing : create();
    }

    // ------------------------------------------------------------------ mapa

    private static void BuildFloorsAndWalls(Transform environment)
    {
        Material groundMat = BuilderUtils.GetOrCreateMaterial("P2_Ground", new Color(0.11f, 0.12f, 0.14f), true);
        Material floorMat = BuilderUtils.GetOrCreateMaterial("P2_Floor", new Color(0.47f, 0.45f, 0.4f), true);
        Material wallMat = BuilderUtils.GetOrCreateMaterial("P2_Wall", new Color(0.24f, 0.26f, 0.3f));

        BuilderUtils.CreatePrimitive(PrimitiveType.Cube, "Suelo base", environment,
            new Vector3(0f, -0.15f, 0f), new Vector3(Parcial2Layout.Width + 6f, 0.1f, Parcial2Layout.Height + 6f), groundMat);

        Transform floors = new GameObject("Pisos").transform;
        floors.SetParent(environment, false);
        int index = 0;
        foreach (Rect r in Parcial2Layout.Floors)
        {
            Vector3 center = Parcial2Layout.ToWorld(r);
            BuilderUtils.CreatePrimitive(PrimitiveType.Cube, $"Piso {++index:00}", floors,
                new Vector3(center.x, -0.05f, center.z), new Vector3(r.width, 0.1f, r.height), floorMat);
        }

        Transform walls = new GameObject("Paredes").transform;
        walls.SetParent(environment, false);
        int obstacleLayer = BuilderUtils.Layer("Obstacle");
        bool[,] walkable = Parcial2Layout.BuildWalkableGrid();
        int wallIndex = 0;
        foreach (RectInt cell in Parcial2Layout.BuildWallRects(walkable))
        {
            Vector2 mapCenter = new Vector2(cell.x + cell.width * 0.5f, cell.y + cell.height * 0.5f);
            Vector3 center = Parcial2Layout.ToWorld(mapCenter);
            GameObject wall = BuilderUtils.CreatePrimitive(PrimitiveType.Cube, $"Pared {++wallIndex:00}", walls,
                new Vector3(center.x, WallHeight * 0.5f, center.z), new Vector3(cell.width, WallHeight, cell.height), wallMat, true);
            wall.layer = obstacleLayer;
            wall.isStatic = true;
        }
    }

    private static Dictionary<string, MapZone> BuildZones(Transform environment)
    {
        Dictionary<string, MapZone> result = new Dictionary<string, MapZone>();
        Transform root = new GameObject("Zonas").transform;
        root.SetParent(environment, false);
        int safeZoneLayer = BuilderUtils.Layer("SafeZone");
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        foreach (Parcial2Layout.ZoneDef def in Parcial2Layout.Zones)
        {
            Vector3 center = Parcial2Layout.ToWorld(def.Area);
            GameObject zoneGo = BuilderUtils.CreateEmpty(def.Name, root, center);
            MapZone zone = zoneGo.AddComponent<MapZone>();
            zone.Setup(def.Type, def.Name, new Vector2(def.Area.width, def.Area.height), def.Color);

            Material zoneMat = BuilderUtils.GetOrCreateMaterial("P2_Zone_" + def.Type, def.Color, true);
            BuilderUtils.CreatePrimitive(PrimitiveType.Cube, "Piso de zona", zoneGo.transform,
                new Vector3(0f, -0.04f, 0f), new Vector3(def.Area.width, 0.1f, def.Area.height), zoneMat);

            CreateFloorLabel(zoneGo.transform, def.Name.ToUpper(), Parcial2Layout.ToWorld(def.LabelPosition), font);

            if (def.Type == ZoneType.SafeZone)
            {
                // Barrera invisible: solo bloquea a los lobos (capa SafeZone en su máscara de bloqueo).
                GameObject barrier = new GameObject("Barrera lobos");
                barrier.transform.SetParent(zoneGo.transform, false);
                barrier.layer = safeZoneLayer;
                BoxCollider box = barrier.AddComponent<BoxCollider>();
                box.size = new Vector3(def.Area.width, 2f, def.Area.height);
                box.center = new Vector3(0f, 1f, 0f);
            }

            result[def.Name] = zone;
        }
        return result;
    }

    private static void CreateFloorLabel(Transform parent, string text, Vector3 position, Font font)
    {
        GameObject go = new GameObject("Cartel");
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(position + Vector3.up * 0.05f, Quaternion.Euler(90f, 0f, 0f));
        TextMesh mesh = go.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.font = font;
        mesh.fontSize = 64;
        mesh.characterSize = 0.14f;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.fontStyle = FontStyle.Bold;
        mesh.color = new Color(1f, 1f, 1f, 0.85f);
        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        if (font != null) renderer.sharedMaterial = font.material;
    }

    private static Graph BuildGraph()
    {
        GameObject root = new GameObject("Grafo de navegación");
        Graph graph = root.AddComponent<Graph>();
        graph.Setup(BuilderUtils.Mask("Obstacle"), 8.5f, 0.45f);

        Parcial2Layout.NodeDef[] defs = Parcial2Layout.BuildNodes();
        for (int i = 0; i < defs.Length; i++)
        {
            GameObject nodeGo = BuilderUtils.CreateEmpty($"Nodo {i + 1:000} ({defs[i].Zone})", root.transform, Parcial2Layout.ToWorld(defs[i].Position));
            Node node = nodeGo.AddComponent<Node>();
            node.Setup(defs[i].Zone);
        }

        graph.CollectNodes();
        graph.GenerateConnections();
        EditorUtility.SetDirty(graph);
        foreach (Node node in graph.GetAllNodes) EditorUtility.SetDirty(node);
        return graph;
    }

    // ------------------------------------------------------------------ agentes

    private static HunterP2 CreateHunter(Transform spawnPoint, Transform exitPoint, MapZone startZone, MapZone coto,
        FlockBounds cotoBounds, MapZone[] safeZones, InterestObject interestPrefab, Projectile projectilePrefab)
    {
        GameObject root = AgentVisualFactory.CreateHunterBody("Cazador", out Renderer bodyRenderer);
        root.transform.position = spawnPoint.position;
        BuilderUtils.SetLayerRecursively(root, BuilderUtils.Layer("Hunter"));

        HunterP2 hunter = root.AddComponent<HunterP2>();
        BuilderUtils.Set(hunter, "_maxSpeed", 5.2f);
        BuilderUtils.Set(hunter, "_maxForce", 20f);
        BuilderUtils.Set(hunter, "_radius", 0.45f);
        BuilderUtils.Set(hunter, "TBA", 2.5f);
        BuilderUtils.Set(hunter, "RangeAttackRadius", 6f);
        BuilderUtils.Set(hunter, "MeleeAttackRadius", 1.6f);
        BuilderUtils.Set(hunter, "VisionRadius", 9f);
        BuilderUtils.Set(hunter, "_meleeDamage", 100f);
        BuilderUtils.Set(hunter, "_rangedDamage", 100f);
        BuilderUtils.Set(hunter, "_gatherTime", 2f);
        BuilderUtils.Set(hunter, "_spawnInterval", 4f);
        BuilderUtils.Set(hunter, "_maxObjects", 5);
        BuilderUtils.Set(hunter, "_spawnArea", cotoBounds);
        BuilderUtils.Set(hunter, "_projectilePrefab", projectilePrefab);
        BuilderUtils.Set(hunter, "_interestObjectPrefab", interestPrefab);
        BuilderUtils.Set(hunter, "_boidMask", BuilderUtils.Mask("Boid"));
        BuilderUtils.Set(hunter, "_renderers", new Renderer[] { bodyRenderer });

        BuilderUtils.Set(hunter, "RequiredGathers", 3);
        BuilderUtils.Set(hunter, "RespawnTime", 5f);
        BuilderUtils.Set(hunter, "SafeZoneWaitTime", 4f);
        BuilderUtils.Set(hunter, "_wallMask", BuilderUtils.Mask("Obstacle"));
        BuilderUtils.Set(hunter, "_fleeSpeed", 5.6f);
        BuilderUtils.Set(hunter, "_startZone", startZone);
        BuilderUtils.Set(hunter, "_spawnPoint", spawnPoint);
        BuilderUtils.Set(hunter, "_huntingGround", coto);
        BuilderUtils.Set(hunter, "_safeZones", safeZones);
        BuilderUtils.Set(hunter, "_exitPoint", exitPoint);
        FieldOfView wolfSensor = root.AddComponent<FieldOfView>();
        wolfSensor.Setup(11f, 360f, BuilderUtils.Mask("Obstacle"), false);
        BuilderUtils.Set(wolfSensor, "_gizmoColor", new Color(0.8f, 0.45f, 1f, 0.8f));
        BuilderUtils.Set(hunter, "_wolfSensor", wolfSensor);
        BuilderUtils.Set(hunter, "_wolfMask", BuilderUtils.Mask("Wolf"));
        BuilderUtils.Set(hunter, "_allRenderers", root.GetComponentsInChildren<MeshRenderer>());
        EnlargeLabel(root, 0.3f, new Vector3(0f, 2.8f, 1.6f));
        return hunter;
    }

    private static Wolf[] CreateWolves()
    {
        Transform wolvesRoot = new GameObject("Lobos").transform;
        Transform routesRoot = new GameObject("Rutas de patrulla").transform;
        Material waypointMat = BuilderUtils.GetOrCreateMaterial("Waypoint", new Color(0.25f, 0.75f, 1f), true);

        Wolf[] wolves = new Wolf[Parcial2Layout.Wolves.Length];
        for (int i = 0; i < wolves.Length; i++)
        {
            Parcial2Layout.WolfDef def = Parcial2Layout.Wolves[i];

            Transform route = new GameObject($"Ruta {def.Name} ({def.Mode})").transform;
            route.SetParent(routesRoot, false);
            Transform[] waypoints = new Transform[def.Waypoints.Length];
            for (int w = 0; w < waypoints.Length; w++)
            {
                waypoints[w] = BuilderUtils.CreateEmpty($"{def.Name} WP {w + 1}", route, Parcial2Layout.ToWorld(def.Waypoints[w])).transform;
                BuilderUtils.CreatePrimitive(PrimitiveType.Cylinder, "Marker", waypoints[w],
                    new Vector3(0f, 0.01f, 0f), new Vector3(0.45f, 0.01f, 0.45f), waypointMat);
            }

            GameObject root = AgentVisualFactory.CreateWolfBody(def.Name, out Renderer[] tintRenderers);
            root.transform.SetParent(wolvesRoot, false);
            root.transform.position = waypoints[0].position;
            BuilderUtils.SetLayerRecursively(root, BuilderUtils.Layer("Wolf"));

            FieldOfView fov = root.AddComponent<FieldOfView>();
            fov.Setup(10f, 110f, BuilderUtils.Mask("Obstacle"), true);

            Wolf wolf = root.AddComponent<Wolf>();
            BuilderUtils.Set(wolf, "_maxSpeed", 4.4f);
            BuilderUtils.Set(wolf, "_maxForce", 16f);
            BuilderUtils.Set(wolf, "_radius", 0.5f);
            BuilderUtils.Set(wolf, "_patrolSpeed", 2.6f);
            BuilderUtils.Set(wolf, "_alertSpeed", 3.8f);
            BuilderUtils.Set(wolf, "_chaseSpeed", 4.4f);
            BuilderUtils.Set(wolf, "_patrolData.waypoints", waypoints);
            BuilderUtils.Set(wolf, "_patrolData.mode", def.Mode);
            BuilderUtils.Set(wolf, "_patrolData.waypointCheckDistance", 0.8f);
            BuilderUtils.Set(wolf, "_fieldOfView", fov);
            BuilderUtils.Set(wolf, "_hunterMask", BuilderUtils.Mask("Hunter"));
            BuilderUtils.Set(wolf, "_blockingMask", BuilderUtils.Mask("Obstacle", "SafeZone"));
            BuilderUtils.Set(wolf, "_wolfMask", BuilderUtils.Mask("Wolf"));
            BuilderUtils.Set(fov, "_gizmoColor", def.Identity);
            BuilderUtils.Set(wolf, "_renderers", tintRenderers);
            BuilderUtils.Set(wolf, "_wolfColor", def.Identity);
            EnlargeLabel(root, 0.27f, new Vector3(0f, 2.4f, 1.4f));
            wolves[i] = wolf;
        }
        return wolves;
    }
}
