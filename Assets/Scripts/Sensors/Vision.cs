using UnityEngine;

//Chequeos de linea de vision contra paredes
public static class Vision
{
    //Altura de los "ojos": las paredes la tapan, asi que cortan la vision
    public const float EyeHeight = 0.6f;

    public static bool InLineOfSight(Vector3 from, Vector3 to, LayerMask wallMask)
    {
        return !Physics.Linecast(from.SetY(EyeHeight), to.SetY(EyeHeight), wallMask, QueryTriggerInteraction.Ignore);
    }

    public static bool InLineOfSight(Vector3 from, Vector3 to, LayerMask wallMask, out RaycastHit hit)
    {
        return !Physics.Linecast(from.SetY(EyeHeight), to.SetY(EyeHeight), out hit, wallMask, QueryTriggerInteraction.Ignore);
    }

    //Igual que InLineOfSight pero con el ancho de un agente: sirve para saber si puede ir en linea recta
    public static bool HasClearPath(Vector3 from, Vector3 to, float radius, LayerMask wallMask)
    {
        Vector3 dir = (to - from).SetY(0f);
        float distance = dir.magnitude;
        if (distance < 0.01f) return true;
        return !Physics.SphereCast(from.SetY(EyeHeight), radius, dir / distance, out _, distance, wallMask, QueryTriggerInteraction.Ignore);
    }
}
