using System.Collections.Generic;
using System.Text;
using UnityEngine;

//Panel de feedback en pantalla (IMGUI): estado del Cazador, resumen de agentes,
//registro de acciones y controles de visualización (velocidad de simulación, etiquetas, cámara).
public class SimulationHUD : MonoBehaviour
{
    [SerializeField] private string _title = "IA - Simulación";
    [SerializeField, Tooltip("Componentes que implementan IHudInfo (Cazador, resumen de Boids, Lobos...).")]
    private MonoBehaviour[] _infoSources;
    [SerializeField] private CameraRig _cameraRig;
    [SerializeField, Range(0.15f, 0.4f)] private float _panelScreenFraction = 0.24f;

    private readonly List<IHudInfo> _sources = new List<IHudInfo>();
    private readonly StringBuilder _sb = new StringBuilder(2048);
    private GUIStyle _textStyle;
    private GUIStyle _titleStyle;
    private GUIStyle _boxStyle;
    private Texture2D _background;
    private Vector2 _scroll;
    private static readonly float[] TimeScales = { 0.5f, 1f, 2f, 3f };

    private void Awake()
    {
        if (_infoSources != null)
        {
            foreach (MonoBehaviour mb in _infoSources)
            {
                if (mb is IHudInfo info) _sources.Add(info);
            }
        }
        _sources.Sort((a, b) => a.HudOrder.CompareTo(b.HudOrder));
    }

    private void OnDestroy()
    {
        if (_background != null) Destroy(_background);
        Time.timeScale = 1f;
    }

    private void EnsureStyles()
    {
        if (_textStyle != null) return;

        float scale = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2f);

        _background = new Texture2D(1, 1);
        _background.SetPixel(0, 0, new Color(0.06f, 0.07f, 0.09f, 0.88f));
        _background.Apply();

        _boxStyle = new GUIStyle(GUI.skin.box) { normal = { background = _background } };
        _textStyle = new GUIStyle(GUI.skin.label)
        {
            richText = true,
            wordWrap = true,
            fontSize = Mathf.RoundToInt(14 * scale),
            normal = { textColor = new Color(0.92f, 0.93f, 0.95f) }
        };
        _titleStyle = new GUIStyle(_textStyle)
        {
            fontSize = Mathf.RoundToInt(17 * scale),
            fontStyle = FontStyle.Bold
        };
    }

    private void OnGUI()
    {
        EnsureStyles();

        float width = Screen.width * _panelScreenFraction;
        Rect panel = new Rect(0f, 0f, width, Screen.height);
        GUI.Box(panel, GUIContent.none, _boxStyle);

        GUILayout.BeginArea(new Rect(10f, 8f, width - 20f, Screen.height - 16f));
        GUILayout.Label(_title, _titleStyle);
        DrawControls();

        _scroll = GUILayout.BeginScrollView(_scroll);

        _sb.Clear();
        foreach (IHudInfo source in _sources)
        {
            source.AppendHudInfo(_sb);
            _sb.AppendLine();
        }
        AppendEventLog(_sb);
        GUILayout.Label(_sb.ToString(), _textStyle);

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawControls()
    {
        GUILayout.Label("Velocidad de la simulacion:", _textStyle);
        GUILayout.BeginHorizontal();
        foreach (float ts in TimeScales)
        {
            bool active = Mathf.Approximately(Time.timeScale, ts);
            if (GUILayout.Toggle(active, $"x{ts}", GUI.skin.button) && !active) Time.timeScale = ts;
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        WorldLabel.GlobalVisible = GUILayout.Toggle(WorldLabel.GlobalVisible, "Etiquetas", GUI.skin.button);
        if (_cameraRig != null && _cameraRig.CanFollow)
        {
            _cameraRig.Following = GUILayout.Toggle(_cameraRig.Following, "Seguir Cazador", GUI.skin.button);
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(4f);
    }

    private static void AppendEventLog(StringBuilder sb)
    {
        sb.AppendLine("<b>REGISTRO DE ACCIONES</b>");
        IReadOnlyList<EventLog.Entry> entries = EventLog.Entries;
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            EventLog.Entry e = entries[i];
            sb.Append("<color=#8A93A0>").Append(e.Time.ToString("0.0")).Append("s</color> ")
              .Append("<color=#").Append(FeedbackResources.ToHex(e.Color)).Append('>')
              .Append(e.Message).AppendLine("</color>");
        }
    }

    public void Configure(string hudTitle, MonoBehaviour[] sources, CameraRig rig)
    {
        _title = hudTitle;
        _infoSources = sources;
        _cameraRig = rig;
    }
}
