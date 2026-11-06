using UnityEngine;

//Ayudas para dibujar Gizmos planos (vista cenital).
public static class GizmoUtils
{
    public static void DrawCircle(Vector3 center, float radius, int segments = 48)
    {
        Vector3 previous = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }

    //Dibuja un cono de visión (arco + dos bordes) en el plano XZ.
    public static void DrawViewCone(Vector3 origin, Vector3 forward, float angle, float radius, int segments = 24)
    {
        forward = forward.SetY(0f).normalized;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;

        float half = angle * 0.5f;
        Vector3 left = Quaternion.Euler(0f, -half, 0f) * forward * radius;
        Vector3 right = Quaternion.Euler(0f, half, 0f) * forward * radius;
        Gizmos.DrawLine(origin, origin + left);
        Gizmos.DrawLine(origin, origin + right);

        Vector3 previous = origin + left;
        for (int i = 1; i <= segments; i++)
        {
            float a = Mathf.Lerp(-half, half, i / (float)segments);
            Vector3 next = origin + Quaternion.Euler(0f, a, 0f) * forward * radius;
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }

    public static void DrawRect(Vector3 center, Vector2 size)
    {
        Gizmos.DrawWireCube(center, new Vector3(size.x, 0.05f, size.y));
    }
}
