using UnityEngine;

//Recursos compartidos por los elementos de feedback (líneas, conos de visión, etiquetas).
//El material se genera desde el builder de escena en Resources/FeedbackLine.mat.
public static class FeedbackResources
{
    private static Material _lineMaterial;
    private static Font _font;

    //Material transparente con color por vértice (shader Sprites/Default).
    public static Material LineMaterial
    {
        get
        {
            if (_lineMaterial == null)
            {
                _lineMaterial = Resources.Load<Material>("FeedbackLine");
                if (_lineMaterial == null)
                {
                    Shader shader = Shader.Find("Sprites/Default");
                    _lineMaterial = new Material(shader) { name = "FeedbackLine (runtime)" };
                }
            }
            return _lineMaterial;
        }
    }

    public static Font LabelFont
    {
        get
        {
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _font;
        }
    }

    public static string ToHex(Color color) => ColorUtility.ToHtmlStringRGB(color);
}
