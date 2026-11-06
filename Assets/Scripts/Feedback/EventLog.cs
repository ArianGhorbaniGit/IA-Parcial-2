using System.Collections.Generic;
using UnityEngine;

//Registro de eventos de la simulación (acciones realizadas y cambios de comportamiento).
//Es solo feedback: los agentes escriben acá, el HUD lo muestra en pantalla y también va a la Consola.
//Ningún agente lee este registro para tomar decisiones.
public static class EventLog
{
    public struct Entry
    {
        public float Time;
        public string Message;
        public Color Color;
    }

    private const int Capacity = 16;
    private static readonly List<Entry> _entries = new List<Entry>(Capacity + 1);

    public static IReadOnlyList<Entry> Entries => _entries;

    //Si es true, cada evento también se escribe en la Consola de Unity.
    public static bool MirrorToConsole = true;

    public static void Add(string message, Color color)
    {
        Entry entry = new Entry { Time = UnityEngine.Time.time, Message = message, Color = color };
        _entries.Add(entry);
        if (_entries.Count > Capacity) _entries.RemoveAt(0);

        if (MirrorToConsole)
        {
            Debug.Log($"[{entry.Time:0.0}s] {message}");
        }
    }

    public static void Add(string message) => Add(message, Color.white);

    // Limpia el registro al entrar en Play (también con "Enter Play Mode Options" sin domain reload).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        _entries.Clear();
    }
}
