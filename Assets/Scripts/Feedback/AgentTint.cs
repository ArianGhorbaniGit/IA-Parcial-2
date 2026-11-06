using UnityEngine;

//Cambia el color de los renderers de un agente sin duplicar materiales (MaterialPropertyBlock).
//Funciona tanto con URP (_BaseColor) como con el pipeline Built-in (_Color).
public class AgentTint
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private readonly Renderer[] _renderers;
    private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
    private Color _current = new Color(-1f, -1f, -1f, -1f);

    public AgentTint(Renderer[] renderers)
    {
        _renderers = renderers ?? new Renderer[0];
    }

    public void SetColor(Color color)
    {
        if (color == _current) return;
        _current = color;

        _block.SetColor(BaseColorId, color);
        _block.SetColor(ColorId, color);
        foreach (Renderer r in _renderers)
        {
            if (r != null) r.SetPropertyBlock(_block);
        }
    }

    public void SetVisible(bool visible)
    {
        foreach (Renderer r in _renderers)
        {
            if (r != null) r.enabled = visible;
        }
    }
}
