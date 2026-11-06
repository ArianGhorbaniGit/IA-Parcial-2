using UnityEngine;

//Sensor de Field of View / Line of Sight.
//Un punto se ve si esta en rango, dentro del angulo de vision y sin paredes en el medio.
//Con angulo 360 funciona como sensor de distancia + linea de vision (lo usa el Cazador para los lobos).
public class FieldOfView : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField]
    private float _viewRadius = 10f;
    [SerializeField, Range(0f, 360f)]
    private float _viewAngle = 110f;

    [Header("References")]
    [SerializeField]
    private LayerMask _wallMask;

    [Header("Cono en pantalla")]
    [SerializeField]
    private bool _showCone = true;
    [SerializeField]
    private int _coneRays = 32;
    [SerializeField]
    private Color _gizmoColor = Color.white;

    private Mesh _coneMesh;
    private MeshRenderer _coneRenderer;
    private Vector3[] _vertices;
    private Color[] _colors;
    private int[] _triangles;
    private Color _coneColor = new(1f, 1f, 1f, 0.15f);
    private float _refreshTimer;

    public float ViewRadius => _viewRadius;
    public float ViewAngle => _viewAngle;

    public void Setup(float viewRadius, float viewAngle, LayerMask wallMask, bool showCone)
    {
        _viewRadius = viewRadius;
        _viewAngle = viewAngle;
        _wallMask = wallMask;
        _showCone = showCone;
    }

    private void Awake()
    {
        if (_wallMask.value == 0) _wallMask = LayerMask.GetMask("Obstacle");
        if (_showCone) CreateCone();
    }

    #region Sensores

    public bool InRange(Vector3 targetPosition)
    {
        return transform.position.FlatDistance(targetPosition) <= _viewRadius;
    }

    public bool InViewAngle(Vector3 targetPosition)
    {
        if (_viewAngle >= 360f) return true;
        Vector3 dir = (targetPosition - transform.position).SetY(0f);
        return Vector3.Angle(transform.forward.SetY(0f), dir.normalized) <= _viewAngle / 2f;
    }

    public bool InLineOfSight(Vector3 targetPosition)
    {
        return Vision.InLineOfSight(transform.position, targetPosition, _wallMask);
    }

    public bool InFieldOfView(Vector3 targetPosition)
    {
        return InRange(targetPosition) && InViewAngle(targetPosition) && InLineOfSight(targetPosition);
    }

    #endregion

    #region Cono visible en el juego

    public void SetConeColor(Color color) => _coneColor = color;

    private void CreateCone()
    {
        GameObject cone = new("FOV");
        cone.transform.SetParent(transform, false);
        cone.transform.localPosition = new Vector3(0f, 0.06f, 0f);

        _coneMesh = new Mesh { name = "FOV" };
        _coneMesh.MarkDynamic();
        cone.AddComponent<MeshFilter>().sharedMesh = _coneMesh;
        _coneRenderer = cone.AddComponent<MeshRenderer>();
        _coneRenderer.sharedMaterial = FeedbackResources.LineMaterial;
        _coneRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _coneRenderer.receiveShadows = false;

        int rays = Mathf.Max(2, _coneRays);
        _vertices = new Vector3[rays + 2];
        _colors = new Color[rays + 2];
        _triangles = new int[rays * 3];
    }

    private void LateUpdate()
    {
        if (_coneMesh == null) return;

        _refreshTimer -= Time.deltaTime;
        if (_refreshTimer > 0f) return;
        _refreshTimer = 0.05f;

        int rays = _vertices.Length - 2;
        Vector3 eye = transform.position.SetY(Vision.EyeHeight);
        _vertices[0] = Vector3.zero;
        _colors[0] = _coneColor;

        //Cada rayo se corta en la primera pared que encuentra
        for (int i = 0; i <= rays; i++)
        {
            float angle = Mathf.Lerp(-_viewAngle / 2f, _viewAngle / 2f, i / (float)rays);
            Vector3 localDir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 worldDir = transform.TransformDirection(localDir).SetY(0f).normalized;

            float distance = _viewRadius;
            if (Physics.Raycast(eye, worldDir, out RaycastHit hit, _viewRadius, _wallMask, QueryTriggerInteraction.Ignore))
            {
                distance = hit.distance;
            }

            _vertices[i + 1] = localDir * distance;
            _colors[i + 1] = new Color(_coneColor.r, _coneColor.g, _coneColor.b, _coneColor.a * 0.35f);
        }

        for (int i = 0; i < rays; i++)
        {
            _triangles[i * 3] = 0;
            _triangles[i * 3 + 1] = i + 1;
            _triangles[i * 3 + 2] = i + 2;
        }

        _coneMesh.Clear();
        _coneMesh.vertices = _vertices;
        _coneMesh.colors = _colors;
        _coneMesh.triangles = _triangles;
        _coneMesh.RecalculateBounds();
    }

    #endregion

    private void OnDrawGizmos()
    {
        Gizmos.color = _gizmoColor;
        Vector3 origin = transform.position + Vector3.up * 0.08f;

        if (_viewAngle >= 360f)
        {
            GizmoUtils.DrawCircle(origin, _viewRadius, 48);
            return;
        }

        GizmoUtils.DrawViewCone(origin, transform.forward, _viewAngle, _viewRadius);
    }
}
