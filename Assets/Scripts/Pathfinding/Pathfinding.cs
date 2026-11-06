using System;
using System.Collections.Generic;
using UnityEngine;

public static class Pathfinding
{
    public enum HeuristicMethod { Distance, SQRMagnitude, Manhattan }

    public static float GetHeuristic(Vector3 posA, Vector3 posB, HeuristicMethod method)
    {
        return method switch
        {
            HeuristicMethod.Distance => (posA - posB).magnitude,
            HeuristicMethod.SQRMagnitude => (posB - posA).sqrMagnitude,
            HeuristicMethod.Manhattan => Mathf.Abs(posA.x - posB.x) + Mathf.Abs(posA.z - posB.z),
            _ => Vector3.Distance(posA, posB),
        };
    }

    //A*: costo = distancia recorrida por las conexiones, prioridad = costo + heuristica.
    //isTransitable permite descartar nodos (los lobos no usan los de las Zonas seguras).
    public static List<Node> AStar(Node startingNode, Node endNode, Func<Node, bool> isTransitable = null, HeuristicMethod method = HeuristicMethod.Distance)
    {
        if (startingNode == null || endNode == null)
        {
            Debug.LogError("Invalid node parameters");
            return new();
        }

        if (isTransitable != null && (!isTransitable(startingNode) || !isTransitable(endNode))) return new();

        PriorityQueue<Node> frontier = new();
        frontier.Enqueue(startingNode, 0);

        Dictionary<Node, Node> cameFrom = new();
        cameFrom.Add(startingNode, null);

        Dictionary<Node, float> costSoFar = new();
        costSoFar.Add(startingNode, 0);

        Node current;
        Vector3 endPosition = endNode.Position;
        while (frontier.Count != 0)
        {
            current = frontier.Dequeue();

            if (current == endNode)
            {
                List<Node> path = new();
                while (current != null)
                {
                    path.Add(current);
                    current = cameFrom[current];
                }
                path.Reverse();
                return path;
            }

            foreach (Node neighbor in current.Neighbors)
            {
                if (neighbor == null) continue;
                if (isTransitable != null && !isTransitable(neighbor)) continue;

                float newCost = costSoFar[current] + Vector3.Distance(current.Position, neighbor.Position);
                if (cameFrom.ContainsKey(neighbor) && newCost >= costSoFar[neighbor]) continue;

                frontier.Enqueue(neighbor, newCost + GetHeuristic(neighbor.Position, endPosition, method));
                cameFrom[neighbor] = current;
                costSoFar[neighbor] = newCost;
            }
        }

        return new();
    }

    public static float GetPathLength(List<Node> path)
    {
        float length = 0f;
        for (int i = 1; i < path.Count; i++) length += Vector3.Distance(path[i - 1].Position, path[i].Position);
        return length;
    }
}
