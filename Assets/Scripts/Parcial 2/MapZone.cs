using UnityEngine;

public enum ZoneType { Corridor, Start, HuntingGround, SafeZone, Exit }

//Zona del mapa (Zona inicial, Coto de caza, Zona segura o Salida). Se ve por el color del piso y con Gizmos.
public class MapZone : MonoBehaviour
{
    [SerializeField]
    private ZoneType _type = ZoneType.Corridor;
    [SerializeField]
    private string _zoneName = "Zona";
    [SerializeField]
    private Vector2 _size = new(6f, 6f);
    [SerializeField]
    private Color _color = Color.white;

    private LineIndicator _highlight;

    public ZoneType Type => _type;
    public string ZoneName => _zoneName;
    public Vector3 Center => transform.position.SetY(0f);

    public void Setup(ZoneType type, string zoneName, Vector2 size, Color color)
    {
        _type = type;
        _zoneName = zoneName;
        _size = size;
        _color = color;
    }

    public bool Contains(Vector3 position, float padding = 0f)
    {
        Vector3 local = position - Center;
        return Mathf.Abs(local.x) <= _size.x / 2f - padding && Mathf.Abs(local.z) <= _size.y / 2f - padding;
    }

    //Resalta la zona (por ejemplo la Zona segura que eligio el Cazador para huir)
    public void SetHighlight(bool active, Color color)
    {
        if (active)
        {
            if (_highlight == null) _highlight = new LineIndicator(transform, "Resaltado", 0.35f, color, 0.3f);
            _highlight.SetColor(color);
            _highlight.SetRect(Center, _size + Vector2.one * 0.4f);
        }
        else
        {
            _highlight?.Hide();
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(_color.r, _color.g, _color.b, 0.25f);
        Gizmos.DrawCube(Center + Vector3.up * 0.05f, new Vector3(_size.x, 0.05f, _size.y));
        Gizmos.color = _color;
        GizmoUtils.DrawRect(Center + Vector3.up * 0.1f, _size);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(Center + Vector3.up * 0.5f, _zoneName);
#endif
    }
}
