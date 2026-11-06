using System.Collections.Generic;
using UnityEngine;

//Envoltorio de un LineRenderer para dibujar feedback visible en la vista Game
//(línea al objetivo, camino de A*, anillos de alerta, etc.).
public class LineIndicator
{
    private readonly LineRenderer _line;
    private readonly float _height;

    public LineIndicator(Transform parent, string name, float width, Color color, float height = 0.15f)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        _height = height;
        _line = go.AddComponent<LineRenderer>();
        _line.useWorldSpace = true;
        _line.sharedMaterial = FeedbackResources.LineMaterial;
        _line.startWidth = width;
        _line.endWidth = width;
        _line.positionCount = 0;
        _line.numCapVertices = 2;
        _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _line.receiveShadows = false;
        SetColor(color);
    }

    public bool Visible => _line.positionCount > 0;

    public void SetColor(Color color)
    {
        _line.startColor = color;
        _line.endColor = color;
    }

    public void SetLine(Vector3 from, Vector3 to)
    {
        _line.loop = false;
        _line.positionCount = 2;
        _line.SetPosition(0, Lift(from));
        _line.SetPosition(1, Lift(to));
    }

    //Dibuja una polilínea: desde start recorriendo los puntos a partir de fromIndex.
    public void SetPath(Vector3 start, IReadOnlyList<Vector3> points, int fromIndex)
    {
        int remaining = points.Count - fromIndex;
        if (remaining <= 0)
        {
            Hide();
            return;
        }

        _line.loop = false;
        _line.positionCount = remaining + 1;
        _line.SetPosition(0, Lift(start));
        for (int i = 0; i < remaining; i++)
        {
            _line.SetPosition(i + 1, Lift(points[fromIndex + i]));
        }
    }

    public void SetCircle(Vector3 center, float radius, int segments = 40)
    {
        _line.loop = true;
        _line.positionCount = segments;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector3 point = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            _line.SetPosition(i, Lift(point));
        }
    }

    public void SetRect(Vector3 center, Vector2 size)
    {
        float hx = size.x * 0.5f;
        float hz = size.y * 0.5f;
        _line.loop = true;
        _line.positionCount = 4;
        _line.SetPosition(0, Lift(center + new Vector3(-hx, 0f, -hz)));
        _line.SetPosition(1, Lift(center + new Vector3(-hx, 0f, hz)));
        _line.SetPosition(2, Lift(center + new Vector3(hx, 0f, hz)));
        _line.SetPosition(3, Lift(center + new Vector3(hx, 0f, -hz)));
    }

    public void Hide()
    {
        _line.positionCount = 0;
    }

    private Vector3 Lift(Vector3 p)
    {
        p.y = _height;
        return p;
    }
}
