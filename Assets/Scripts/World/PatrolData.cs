using System.Collections.Generic;
using UnityEngine;

public enum PatrolMode { Loop, PingPong }

//Recorrido de waypoints. Loop: al terminar vuelve al primero. PingPong: recorre en sentido inverso.
[System.Serializable]
public class PatrolData
{
    public List<Transform> waypoints = new();
    public PatrolMode mode = PatrolMode.Loop;
    public float waypointCheckDistance = 0.8f;

    [System.NonSerialized]
    private int _currentIndex;
    [System.NonSerialized]
    private int _direction = 1;

    public bool HasWaypoints => waypoints != null && waypoints.Count > 0;
    public Transform CurrentWaypoint => HasWaypoints ? waypoints[_currentIndex] : null;

    public void NextWaypoint()
    {
        if (!HasWaypoints || waypoints.Count == 1) return;

        if (mode == PatrolMode.Loop)
        {
            _currentIndex = _currentIndex + 1 < waypoints.Count ? _currentIndex + 1 : 0;
            return;
        }

        if (_direction == 0) _direction = 1;
        int next = _currentIndex + _direction;
        if (next < 0 || next >= waypoints.Count)
        {
            _direction = -_direction;
            next = _currentIndex + _direction;
        }
        _currentIndex = next;
    }

    public void DrawGizmos(Color color)
    {
        if (!HasWaypoints) return;

        Gizmos.color = color;
        for (int i = 0; i < waypoints.Count; i++)
        {
            if (waypoints[i] == null) continue;
            Gizmos.DrawWireSphere(waypoints[i].position, 0.35f);

            int next = i + 1;
            if (next >= waypoints.Count)
            {
                if (mode != PatrolMode.Loop) break;
                next = 0;
            }
            if (waypoints[next] != null) Gizmos.DrawLine(waypoints[i].position, waypoints[next].position);
        }
    }
}
