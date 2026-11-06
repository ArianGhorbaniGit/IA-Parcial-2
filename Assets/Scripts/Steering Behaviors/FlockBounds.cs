using UnityEngine;

//Limites de la bandada. En el Parcial 1 es toda la arena y en el Parcial 2 es el Coto de caza.
//Cerca del borde empuja a los Boids hacia adentro y nunca los deja salir.
public class FlockBounds : MonoBehaviour
{
    [SerializeField]
    private float _width = 44f;
    [SerializeField]
    private float _height = 28f;
    [SerializeField, Tooltip("Distancia al borde desde la que se empieza a empujar hacia adentro")]
    private float _margin = 2.5f;
    [SerializeField]
    private bool _drawGizmos = true;

    public float Margin => _margin;
    public Vector3 Center => transform.position.SetY(0f);

    public void Setup(float width, float height, float margin)
    {
        _width = width;
        _height = height;
        _margin = margin;
    }

    public bool Contains(Vector3 position, float padding = 0f)
    {
        Vector3 local = position - Center;
        return Mathf.Abs(local.x) <= _width / 2f - padding && Mathf.Abs(local.z) <= _height / 2f - padding;
    }

    public Vector3 ClampPosition(Vector3 position, float padding)
    {
        float halfWidth = Mathf.Max(0f, _width / 2f - padding);
        float halfHeight = Mathf.Max(0f, _height / 2f - padding);
        position.x = Mathf.Clamp(position.x, Center.x - halfWidth, Center.x + halfWidth);
        position.z = Mathf.Clamp(position.z, Center.z - halfHeight, Center.z + halfHeight);
        return position;
    }

    public Vector3 GetRandomPosition(float padding)
    {
        float halfWidth = Mathf.Max(0f, _width / 2f - padding);
        float halfHeight = Mathf.Max(0f, _height / 2f - padding);
        return Center + new Vector3(Random.Range(-halfWidth, halfWidth), 0f, Random.Range(-halfHeight, halfHeight));
    }

    //Direccion hacia adentro si esta cerca del borde (zero si esta lejos)
    public Vector3 GetInwardDirection(Vector3 position)
    {
        Vector3 local = position - Center;
        Vector3 dir = Vector3.zero;

        if (local.x > _width / 2f - _margin) dir.x = -1f;
        else if (local.x < -_width / 2f + _margin) dir.x = 1f;
        if (local.z > _height / 2f - _margin) dir.z = -1f;
        else if (local.z < -_height / 2f + _margin) dir.z = 1f;

        return dir.normalized;
    }

    private void OnDrawGizmos()
    {
        if (!_drawGizmos) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(Center, new Vector3(_width, 0f, _height));
        Gizmos.color = new Color(0f, 1f, 1f, 0.25f);
        Gizmos.DrawWireCube(Center, new Vector3(_width - _margin * 2f, 0f, _height - _margin * 2f));
    }
}
