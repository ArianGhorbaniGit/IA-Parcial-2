using System;
using System.Collections.Generic;
using UnityEngine;

//Grafo de navegacion. Los nodos estan puestos a mano por pasillos y salas,
//y las conexiones se generan con raycasts (SphereCast con el ancho de un agente) para no atravesar paredes.
public class Graph : MonoBehaviour
{
    [Header("Grafo")]
    [SerializeField]
    private List<Node> _nodes = new();
    [SerializeField, Tooltip("Distancia maxima para conectar dos nodos")]
    private float _maxConnectionDistance = 8.5f;
    [SerializeField, Tooltip("Ancho libre que tiene que haber entre dos nodos para conectarlos")]
    private float _agentRadius = 0.45f;
    [SerializeField]
    private LayerMask _wallMask;
    [SerializeField]
    private bool _drawGizmos = true;

    private readonly List<Node> _sortedNodes = new();

    public static Graph Current { get; private set; }
    public List<Node> GetAllNodes => _nodes;
    public LayerMask WallMask => _wallMask;

    private void Awake()
    {
        if (Current != null && Current != this) Destroy(gameObject);
        else Current = this;

        //Por las dudas: si la escena se guardo sin conexiones, las genero al empezar
        foreach (Node node in _nodes)
        {
            if (node != null && node.Neighbors.Count > 0) return;
        }
        Debug.LogWarning("El grafo no tenia conexiones, se generan ahora");
        GenerateConnections();
    }

    public void Setup(LayerMask wallMask, float maxConnectionDistance, float agentRadius)
    {
        _wallMask = wallMask;
        _maxConnectionDistance = maxConnectionDistance;
        _agentRadius = agentRadius;
    }

    public List<Node> GetNeighbors(Node current) => current.Neighbors;

    [ContextMenu("Buscar nodos hijos")]
    public void CollectNodes()
    {
        _nodes.Clear();
        _nodes.AddRange(GetComponentsInChildren<Node>());
    }

    [ContextMenu("Generar conexiones")]
    public void GenerateConnections()
    {
        Physics.SyncTransforms();
        foreach (Node node in _nodes) node.ClearNeighbors();

        for (int i = 0; i < _nodes.Count; i++)
        {
            for (int j = i + 1; j < _nodes.Count; j++)
            {
                Node a = _nodes[i];
                Node b = _nodes[j];
                if (a.Position.FlatDistance(b.Position) > _maxConnectionDistance) continue;
                if (!Vision.HasClearPath(a.Position, b.Position, _agentRadius, _wallMask)) continue;

                a.AddNeighbor(b);
                b.AddNeighbor(a);
            }
        }
    }

    //Nodo mas cercano que se pueda usar, priorizando los que estan a la vista
    public Node GetClosestNode(Vector3 position, Func<Node, bool> isTransitable)
    {
        _sortedNodes.Clear();
        foreach (Node node in _nodes)
        {
            if (node != null && (isTransitable == null || isTransitable(node))) _sortedNodes.Add(node);
        }
        if (_sortedNodes.Count == 0) return null;

        Vector3 flatPosition = position.SetY(0f);
        _sortedNodes.Sort((a, b) => (a.Position - flatPosition).sqrMagnitude.CompareTo((b.Position - flatPosition).sqrMagnitude));

        for (int i = 0; i < _sortedNodes.Count && i < 12; i++)
        {
            if (Vision.InLineOfSight(flatPosition, _sortedNodes[i].Position, _wallMask)) return _sortedNodes[i];
        }
        return _sortedNodes[0];
    }

    public List<Node> GetNodesInZone(ZoneType zone)
    {
        List<Node> result = new();
        foreach (Node node in _nodes)
        {
            if (node != null && node.Zone == zone) result.Add(node);
        }
        return result;
    }

    public static Color GetZoneColor(ZoneType zone) => zone switch
    {
        ZoneType.SafeZone => new Color(0.78f, 0.49f, 1f),
        ZoneType.HuntingGround => new Color(1f, 0.75f, 0.3f),
        ZoneType.Start => new Color(0.4f, 1f, 0.5f),
        ZoneType.Exit => new Color(0.4f, 0.65f, 1f),
        _ => Color.white
    };

    private void OnDrawGizmos()
    {
        if (!_drawGizmos) return;

        foreach (Node node in _nodes)
        {
            if (node == null) continue;
            Vector3 position = node.Position + Vector3.up * 0.2f;

            Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
            foreach (Node neighbor in node.Neighbors)
            {
                if (neighbor != null) Gizmos.DrawLine(position, neighbor.Position + Vector3.up * 0.2f);
            }

            Gizmos.color = GetZoneColor(node.Zone);
            Gizmos.DrawSphere(position, 0.22f);
        }
    }
}
