using UnityEngine;

//Crea la representación visual (primitivas) y el collider de los agentes que viven en escena.
public static class AgentVisualFactory
{
    //Cuerpo del Cazador: cápsula + visor + arma. El pivot queda en el suelo (y = 0).
    public static GameObject CreateHunterBody(string objectName, out Renderer bodyRenderer)
    {
        Material bodyMat = BuilderUtils.GetOrCreateMaterial("Hunter", new Color(0.25f, 0.75f, 1f));
        Material detailMat = BuilderUtils.GetOrCreateMaterial("HunterDetail", new Color(0.12f, 0.12f, 0.15f));

        GameObject root = new GameObject(objectName);
        CapsuleCollider collider = BuilderUtils.AddKinematicBody<CapsuleCollider>(root);
        collider.radius = 0.45f;
        collider.height = 1.6f;
        collider.center = new Vector3(0f, 0.8f, 0f);

        GameObject body = BuilderUtils.CreatePrimitive(PrimitiveType.Capsule, "Body", root.transform,
            new Vector3(0f, 0.8f, 0f), new Vector3(0.9f, 0.8f, 0.9f), bodyMat);
        BuilderUtils.CreatePrimitive(PrimitiveType.Cube, "Visor", root.transform,
            new Vector3(0f, 1.15f, 0.36f), new Vector3(0.55f, 0.18f, 0.25f), detailMat);
        BuilderUtils.CreatePrimitive(PrimitiveType.Cube, "Arma", root.transform,
            new Vector3(0.42f, 0.85f, 0.38f), new Vector3(0.14f, 0.14f, 0.8f), detailMat);

        BuilderUtils.AddLabel(root, new Vector3(0f, 2.5f, 1.3f), 0.19f, Color.white);
        bodyRenderer = body.GetComponent<Renderer>();
        return root;
    }

    //Cuerpo del Lobo: torso + cabeza + orejas + cola, mirando a +Z.
    public static GameObject CreateWolfBody(string objectName, out Renderer[] tintRenderers)
    {
        Material furMat = BuilderUtils.GetOrCreateMaterial("Wolf", new Color(0.55f, 0.57f, 0.6f));
        Material detailMat = BuilderUtils.GetOrCreateMaterial("WolfDetail", new Color(0.15f, 0.15f, 0.17f));

        GameObject root = new GameObject(objectName);
        SphereCollider collider = BuilderUtils.AddKinematicBody<SphereCollider>(root);
        collider.radius = 0.5f;
        collider.center = new Vector3(0f, 0.5f, 0f);

        GameObject torso = BuilderUtils.CreatePrimitive(PrimitiveType.Cube, "Torso", root.transform,
            new Vector3(0f, 0.45f, -0.1f), new Vector3(0.6f, 0.45f, 1.1f), furMat);
        GameObject head = BuilderUtils.CreatePrimitive(PrimitiveType.Cube, "Cabeza", root.transform,
            new Vector3(0f, 0.65f, 0.6f), new Vector3(0.42f, 0.38f, 0.45f), furMat);
        BuilderUtils.CreatePrimitive(PrimitiveType.Cube, "Hocico", root.transform,
            new Vector3(0f, 0.58f, 0.9f), new Vector3(0.2f, 0.16f, 0.25f), detailMat);
        BuilderUtils.CreatePrimitive(PrimitiveType.Cube, "Oreja I", root.transform,
            new Vector3(-0.13f, 0.92f, 0.52f), new Vector3(0.1f, 0.18f, 0.08f), detailMat);
        BuilderUtils.CreatePrimitive(PrimitiveType.Cube, "Oreja D", root.transform,
            new Vector3(0.13f, 0.92f, 0.52f), new Vector3(0.1f, 0.18f, 0.08f), detailMat);
        BuilderUtils.CreatePrimitive(PrimitiveType.Cube, "Cola", root.transform,
            new Vector3(0f, 0.55f, -0.8f), new Vector3(0.12f, 0.12f, 0.45f), detailMat);

        BuilderUtils.AddLabel(root, new Vector3(0f, 1.8f, 0.9f), 0.12f, Color.white);
        tintRenderers = new[] { torso.GetComponent<Renderer>(), head.GetComponent<Renderer>() };
        return root;
    }
}
