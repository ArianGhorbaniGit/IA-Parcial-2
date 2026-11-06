using UnityEngine;

//Datos del mapa del Parcial 2 (variante "anillo"). Coordenadas de mapa: X 0..80, Z 0..52 (1 unidad = 1 metro).
//El builder las convierte a mundo restando Origin para que el mapa quede centrado.
//Todo lo que no es piso es pared.
public static class Parcial2Layout
{
    public const int Width = 80;
    public const int Height = 52;
    public static readonly Vector3 Origin = new Vector3(40f, 0f, 26f);

    //Rectángulos de piso transitable: (xMin, zMin, xMax, zMax).
    public static readonly Rect[] Floors =
    {
        R(10, 4, 70, 8),   // anillo sur
        R(10, 44, 70, 48), // anillo norte
        R(10, 4, 14, 48),  // anillo oeste
        R(66, 4, 70, 48),  // anillo este
        R(28, 18, 52, 34), // Coto de caza
        R(14, 24, 28, 28), // pasillo anillo oeste -> Coto
        R(52, 24, 66, 28), // pasillo Coto -> anillo este
        R(26, 37, 54, 42), // sala norte
        R(29, 42, 33, 44), R(47, 42, 51, 44), // conexiones sala norte - anillo
        R(38, 34, 42, 37), // abertura Coto - sala norte
        R(26, 10, 54, 15), // sala sur
        R(29, 8, 33, 10), R(47, 8, 51, 10),   // conexiones sala sur - anillo
        R(38, 15, 42, 18), // abertura Coto - sala sur
        R(1, 20, 8, 32), R(8, 24, 10, 28),    // Zona inicial + pasillo
        R(72, 20, 79, 32), R(70, 24, 72, 28), // Salida + pasillo
        R(16, 33, 23, 41), R(14, 35, 16, 39), // Zona segura A (callejón sin salida)
        R(57, 11, 64, 19), R(64, 13, 66, 17), // Zona segura B (callejón sin salida)
    };

    public struct ZoneDef
    {
        public ZoneType Type;
        public string Name;
        public Rect Area;
        public Color Color;
        public Vector2 LabelPosition;
    }

    public static readonly ZoneDef[] Zones =
    {
        new ZoneDef { Type = ZoneType.Start, Name = "Zona inicial", Area = R(1, 20, 8, 32), Color = new Color(0.22f, 0.5f, 0.28f), LabelPosition = new Vector2(4.5f, 30.5f) },
        new ZoneDef { Type = ZoneType.HuntingGround, Name = "Coto de caza", Area = R(28, 18, 52, 34), Color = new Color(0.58f, 0.43f, 0.18f), LabelPosition = new Vector2(40f, 33f) },
        new ZoneDef { Type = ZoneType.SafeZone, Name = "Zona segura A", Area = R(16, 33, 23, 41), Color = new Color(0.4f, 0.26f, 0.6f), LabelPosition = new Vector2(19.5f, 40f) },
        new ZoneDef { Type = ZoneType.SafeZone, Name = "Zona segura B", Area = R(57, 11, 64, 19), Color = new Color(0.4f, 0.26f, 0.6f), LabelPosition = new Vector2(60.5f, 18f) },
        new ZoneDef { Type = ZoneType.Exit, Name = "Salida", Area = R(72, 20, 79, 32), Color = new Color(0.18f, 0.32f, 0.58f), LabelPosition = new Vector2(75.5f, 30.5f) },
    };

    public static readonly Vector2 HunterSpawn = new Vector2(4f, 26f);
    public static readonly Vector2 ExitPoint = new Vector2(76f, 26f);

    public struct NodeDef
    {
        public Vector2 Position;
        public ZoneType Zone;
        public NodeDef(float x, float z, ZoneType zone = ZoneType.Corridor)
        {
            Position = new Vector2(x, z);
            Zone = zone;
        }
    }

    //Nodos del grafo. Las conexiones se generan con SphereCasts (Graph.GenerateConnections).
    public static NodeDef[] BuildNodes()
    {
        var list = new System.Collections.Generic.List<NodeDef>
        {
            // Zona inicial y su pasillo
            new NodeDef(3, 22, ZoneType.Start), new NodeDef(3, 30, ZoneType.Start), new NodeDef(5, 26, ZoneType.Start),
            new NodeDef(9, 26),
            // Zona segura A (+ pasillo)
            new NodeDef(15, 37),
            new NodeDef(19.5f, 37, ZoneType.SafeZone), new NodeDef(19.5f, 34.5f, ZoneType.SafeZone), new NodeDef(19.5f, 39.5f, ZoneType.SafeZone),
            // Zona segura B (+ pasillo)
            new NodeDef(65, 15),
            new NodeDef(60.5f, 15, ZoneType.SafeZone), new NodeDef(60.5f, 12.5f, ZoneType.SafeZone), new NodeDef(60.5f, 17.5f, ZoneType.SafeZone),
            // Conexiones salas - anillo y aberturas al Coto
            new NodeDef(31, 43), new NodeDef(49, 43), new NodeDef(31, 9), new NodeDef(49, 9),
            new NodeDef(40, 35.5f), new NodeDef(40, 16.5f),
            // Salida y su pasillo
            new NodeDef(71, 26),
            new NodeDef(76, 26, ZoneType.Exit), new NodeDef(74, 22, ZoneType.Exit), new NodeDef(74, 30, ZoneType.Exit),
        };

        foreach (float z in new float[] { 6, 12, 18, 26, 32, 37, 42, 46 }) list.Add(new NodeDef(12, z));       // anillo oeste
        foreach (float z in new float[] { 6, 11, 15, 20, 26, 32, 39, 46 }) list.Add(new NodeDef(68, z));       // anillo este
        foreach (float x in new float[] { 18, 24, 31, 37, 43, 49, 56, 62 })
        {
            list.Add(new NodeDef(x, 46)); // anillo norte
            list.Add(new NodeDef(x, 6));  // anillo sur
        }
        foreach (float x in new float[] { 28, 31, 35, 40, 45, 49, 52 })
        {
            list.Add(new NodeDef(x, 39.5f)); // sala norte
            list.Add(new NodeDef(x, 12.5f)); // sala sur
        }
        foreach (float x in new float[] { 17, 22, 26 }) list.Add(new NodeDef(x, 26)); // pasillo oeste
        foreach (float x in new float[] { 54, 59, 64 }) list.Add(new NodeDef(x, 26)); // pasillo este
        foreach (float x in new float[] { 31, 37, 43, 49 })
        {
            foreach (float z in new float[] { 21, 26, 31 }) list.Add(new NodeDef(x, z, ZoneType.HuntingGround)); // Coto
        }

        return list.ToArray();
    }

    public struct WolfDef
    {
        public string Name;
        public Vector2[] Waypoints;
        public PatrolMode Mode;
        public Color Identity;
    }

    public static readonly WolfDef[] Wolves =
    {
        new WolfDef { Name = "Lobo 1", Mode = PatrolMode.PingPong, Identity = new Color(1f, 0.35f, 0.35f),
            Waypoints = new[] { V(12, 6), V(12, 26), V(12, 46) } },        // anillo oeste (ida y vuelta)
        new WolfDef { Name = "Lobo 2", Mode = PatrolMode.Loop, Identity = new Color(1f, 0.4f, 0.8f),
            Waypoints = new[] { V(18, 46), V(40, 39.5f), V(62, 46) } },    // anillo norte + sala norte
        new WolfDef { Name = "Lobo 3", Mode = PatrolMode.PingPong, Identity = new Color(1f, 0.6f, 0.1f),
            Waypoints = new[] { V(68, 46), V(68, 26), V(68, 6) } },        // anillo este (ida y vuelta)
        new WolfDef { Name = "Lobo 4", Mode = PatrolMode.Loop, Identity = new Color(0.3f, 0.75f, 1f),
            Waypoints = new[] { V(31, 6), V(31, 12.5f), V(49, 12.5f), V(49, 6) } }, // anillo sur + sala sur
    };

    public static Vector3 ToWorld(Vector2 mapPoint) => new Vector3(mapPoint.x, 0f, mapPoint.y) - Origin;

    public static Vector3 ToWorld(Rect r) => ToWorld(r.center);

    //Grilla de celdas transitables (1x1) a partir de los rectángulos de piso.
    public static bool[,] BuildWalkableGrid()
    {
        bool[,] grid = new bool[Width, Height];
        for (int x = 0; x < Width; x++)
        {
            for (int z = 0; z < Height; z++)
            {
                Vector2 cell = new Vector2(x + 0.5f, z + 0.5f);
                foreach (Rect floor in Floors)
                {
                    if (floor.Contains(cell))
                    {
                        grid[x, z] = true;
                        break;
                    }
                }
            }
        }
        return grid;
    }

    //Agrupa las celdas de pared en rectángulos grandes (menos colliders).
    public static System.Collections.Generic.List<RectInt> BuildWallRects(bool[,] walkable)
    {
        var rects = new System.Collections.Generic.List<RectInt>();
        bool[,] used = new bool[Width, Height];

        for (int z = 0; z < Height; z++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (walkable[x, z] || used[x, z]) continue;

                int w = 1;
                while (x + w < Width && !walkable[x + w, z] && !used[x + w, z]) w++;

                int h = 1;
                bool canGrow = true;
                while (canGrow && z + h < Height)
                {
                    for (int i = 0; i < w; i++)
                    {
                        if (walkable[x + i, z + h] || used[x + i, z + h])
                        {
                            canGrow = false;
                            break;
                        }
                    }
                    if (canGrow) h++;
                }

                for (int i = 0; i < w; i++)
                    for (int j = 0; j < h; j++)
                        used[x + i, z + j] = true;

                rects.Add(new RectInt(x, z, w, h));
            }
        }
        return rects;
    }

    private static Rect R(float xMin, float zMin, float xMax, float zMax) => Rect.MinMaxRect(xMin, zMin, xMax, zMax);
    private static Vector2 V(float x, float z) => new Vector2(x, z);
}
