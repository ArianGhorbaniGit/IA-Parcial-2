using System;
using System.Collections.Generic;
using UnityEngine;

//Calcula caminos con A* sobre el grafo y los va recorriendo punto por punto.
//Lo usan el Cazador y los lobos (mismo grafo y mismo A*), cada uno con su filtro de nodos.
public class PathFollower
{
    private readonly List<Vector3> _path = new();
    private readonly Func<Node, bool> _isTransitable;
    private readonly LayerMask _wallMask;
    private readonly float _radius;
    private readonly LineIndicator _line;

    public bool HasPath => _path.Count > 0;
    public Vector3 Destination { get; private set; }
    public float PathLength { get; private set; }
    public int NodeCount { get; private set; }

    public PathFollower(Func<Node, bool> isTransitable, LayerMask wallMask, float radius, LineIndicator line)
    {
        _isTransitable = isTransitable;
        _wallMask = wallMask;
        _radius = radius;
        _line = line;
    }

    public bool CalculatePath(Vector3 from, Vector3 to, Func<Node, bool> extraFilter = null)
    {
        Clear();
        Destination = to.SetY(0f);

        List<Node> nodes = FindPath(from, to, extraFilter);
        if (nodes.Count == 0) return false;

        foreach (Node node in nodes) _path.Add(node.Position);

        //Ultimo tramo: del ultimo nodo al destino exacto, si se puede ir derecho
        Vector3 lastNode = nodes[nodes.Count - 1].Position;
        if (lastNode.FlatDistance(Destination) > 0.3f && Vision.HasClearPath(lastNode, Destination, _radius, _wallMask))
        {
            _path.Add(Destination);
        }

        NodeCount = nodes.Count;
        PathLength = from.FlatDistance(_path[0]);
        for (int i = 1; i < _path.Count; i++) PathLength += _path[i - 1].FlatDistance(_path[i]);
        return true;
    }

    //Largo del camino A* sin cambiar el camino actual (infinito si no hay camino)
    public float GetPathLength(Vector3 from, Vector3 to, Func<Node, bool> extraFilter = null)
    {
        List<Node> nodes = FindPath(from, to, extraFilter);
        if (nodes.Count == 0) return float.PositiveInfinity;

        return from.FlatDistance(nodes[0].Position) + Pathfinding.GetPathLength(nodes) + nodes[nodes.Count - 1].Position.FlatDistance(to);
    }

    //Devuelve el proximo punto a seguir. Devuelve false cuando ya llego al final.
    public bool GetNextPoint(Vector3 position, float reachDistance, out Vector3 point, out bool isLast)
    {
        while (_path.Count > 0 && position.FlatDistance(_path[0]) <= reachDistance) _path.RemoveAt(0);

        if (_path.Count == 0)
        {
            point = default;
            isLast = true;
            _line?.Hide();
            return false;
        }

        //Si ya veo libre el punto siguiente, salteo el actual (el recorrido queda mas natural)
        if (_path.Count > 1 && Vision.HasClearPath(position, _path[1], _radius, _wallMask)) _path.RemoveAt(0);

        point = _path[0];
        isLast = _path.Count == 1;
        _line?.SetPath(position, _path, 0);
        return true;
    }

    public void Clear()
    {
        _path.Clear();
        NodeCount = 0;
        PathLength = 0f;
        _line?.Hide();
    }

    private List<Node> FindPath(Vector3 from, Vector3 to, Func<Node, bool> extraFilter)
    {
        Graph graph = Graph.Current;
        if (graph == null) return new();

        Func<Node, bool> filter = Combine(_isTransitable, extraFilter);
        Node start = graph.GetClosestNode(from, filter);
        Node end = graph.GetClosestNode(to, filter);
        if (start == null || end == null) return new();

        return Pathfinding.AStar(start, end, filter);
    }

    private static Func<Node, bool> Combine(Func<Node, bool> a, Func<Node, bool> b)
    {
        if (a == null) return b;
        if (b == null) return a;
        return node => a(node) && b(node);
    }
}
