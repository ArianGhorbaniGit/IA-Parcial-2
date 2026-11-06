using UnityEngine;

//Etiqueta de texto flotante sobre un agente (estado, objetivo, acción...).
//Usa TextMesh para no depender de paquetes externos y siempre mira a la cámara.
public class WorldLabel : MonoBehaviour
{
    //Permite ocultar todas las etiquetas desde el HUD.
    public static bool GlobalVisible = true;

    [SerializeField] private Vector3 _offset = new Vector3(0f, 2.2f, 0.9f);
    [SerializeField] private float _characterSize = 0.16f;
    [SerializeField] private int _fontSize = 48;
    [SerializeField] private Color _color = Color.white;

    private TextMesh _text;
    private MeshRenderer _renderer;
    private Transform _labelTransform;
    private Camera _camera;
    private float _flashUntil;
    private string _flashText;
    private string _baseText = "";

    public Vector3 Offset { get => _offset; set => _offset = value; }

    //Oculta esta etiqueta en particular (por ejemplo, cuando el agente desaparece).
    public bool Hidden { get; set; }

    private void Awake()
    {
        GameObject go = new GameObject("Label");
        _labelTransform = go.transform;
        _labelTransform.SetParent(transform, false);

        _text = go.AddComponent<TextMesh>();
        _text.font = FeedbackResources.LabelFont;
        _text.fontSize = _fontSize;
        _text.characterSize = _characterSize;
        _text.anchor = TextAnchor.LowerCenter;
        _text.alignment = TextAlignment.Center;
        _text.color = _color;
        _text.text = "";

        _renderer = go.GetComponent<MeshRenderer>();
        if (_text.font != null) _renderer.sharedMaterial = _text.font.material;
        _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
    }

    public void SetText(string text)
    {
        _baseText = text ?? "";
    }

    public void SetColor(Color c)
    {
        _color = c;
        if (_text != null) _text.color = c;
    }

    //Muestra un texto temporal por encima del texto normal (ej. "¡AUUU!").
    public void Flash(string text, float duration)
    {
        _flashText = text;
        _flashUntil = Time.time + duration;
    }

    private void LateUpdate()
    {
        if (_camera == null) _camera = Camera.main;

        string display = Time.time < _flashUntil ? _flashText + "\n" + _baseText : _baseText;
        if (_text.text != display) _text.text = display;

        _renderer.enabled = GlobalVisible && !Hidden && display.Length > 0;

        _labelTransform.position = transform.position + _offset;
        if (_camera != null) _labelTransform.rotation = _camera.transform.rotation;
    }
}
