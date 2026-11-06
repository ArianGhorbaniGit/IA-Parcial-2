using UnityEngine;

//Cámara cenital ortográfica. Encuadra un rectángulo del escenario dejando lugar para el panel del HUD,
//y opcionalmente puede seguir a un objetivo (por ejemplo, al Cazador).
[RequireComponent(typeof(Camera))]
public class CameraRig : MonoBehaviour
{
    [SerializeField, Tooltip("Centro del área a encuadrar (XZ).")]
    private Vector2 _areaCenter = Vector2.zero;

    [SerializeField, Tooltip("Tamaño del área a encuadrar (XZ).")]
    private Vector2 _areaSize = new Vector2(44f, 28f);

    [SerializeField, Tooltip("Fracción izquierda de la pantalla reservada para el HUD.")]
    private float _hudScreenFraction = 0.24f;

    [SerializeField] private float _margin = 1.5f;
    [SerializeField] private float _height = 60f;

    [Header("Seguimiento")]
    [SerializeField] private Transform _followTarget;
    [SerializeField] private float _followSize = 12f;
    [SerializeField] private float _followSmoothing = 6f;

    private Camera _camera;

    public bool Following { get; set; }
    public bool CanFollow => _followTarget != null;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        _camera.orthographic = true;
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    private void LateUpdate()
    {
        if (Following && _followTarget != null)
        {
            Vector3 target = new Vector3(_followTarget.position.x, _height, _followTarget.position.z);
            transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-_followSmoothing * Time.unscaledDeltaTime));
            _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, _followSize, 1f - Mathf.Exp(-_followSmoothing * Time.unscaledDeltaTime));
            return;
        }

        FitArea();
    }

    //Calcula tamaño y posición para que el área entre completa a la derecha del HUD.
    private void FitArea()
    {
        float aspect = Mathf.Max(0.1f, _camera.aspect);
        float usable = Mathf.Clamp01(1f - _hudScreenFraction);

        float sizeForHeight = _areaSize.y * 0.5f + _margin;
        float sizeForWidth = (_areaSize.x * 0.5f + _margin) / (aspect * usable);
        float size = Mathf.Max(sizeForHeight, sizeForWidth);
        _camera.orthographicSize = size;

        // Desplazar la cámara a la izquierda para que el área quede centrada en la parte libre de la pantalla.
        float viewWidth = size * 2f * aspect;
        float shift = viewWidth * _hudScreenFraction * 0.5f;
        transform.position = new Vector3(_areaCenter.x - shift, _height, _areaCenter.y);
    }

    public void Configure(Vector2 center, Vector2 size, Transform follow)
    {
        _areaCenter = center;
        _areaSize = size;
        _followTarget = follow;
    }
}
