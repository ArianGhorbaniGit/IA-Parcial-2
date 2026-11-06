using UnityEngine;

public static class Extensions
{
    public static Vector3 SetY(this Vector3 vector, float yValue)
    {
        vector.y = yValue;
        return vector;
    }

    //Distancia en el plano XZ (ignora la altura)
    public static float FlatDistance(this Vector3 a, Vector3 b)
    {
        return Vector3.Distance(a.SetY(0f), b.SetY(0f));
    }
}
