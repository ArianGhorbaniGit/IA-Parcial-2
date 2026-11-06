using System.Collections.Generic;
using UnityEngine;

public class Node : MonoBehaviour
{
    [SerializeField]
    private ZoneType _zone = ZoneType.Corridor;
    [SerializeField]
    private List<Node> _neighbors = new();

    public ZoneType Zone => _zone;
    public bool IsSafeZone => _zone == ZoneType.SafeZone;
    public List<Node> Neighbors => _neighbors;
    public Vector3 Position => transform.position.SetY(0f);

    public void Setup(ZoneType zone) => _zone = zone;

    public void ClearNeighbors() => _neighbors.Clear();

    public void AddNeighbor(Node node)
    {
        if (node != null && node != this && !_neighbors.Contains(node)) _neighbors.Add(node);
    }
}
