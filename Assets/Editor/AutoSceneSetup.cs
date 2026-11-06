using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

//Al abrir el proyecto por primera vez genera automáticamente las escenas que falten
//(por si el repositorio se clona sin ellas) y abre la escena del Parcial 2.
//También agrega accesos directos en el menú "IA Parcial".
[InitializeOnLoad]
public static class AutoSceneSetup
{
    private const string SessionKey = "IAParcial.AutoSetupChecked";

    static AutoSceneSetup()
    {
        if (Application.isBatchMode || SessionState.GetBool(SessionKey, false)) return;
        EditorApplication.delayCall += TryRun;
    }

    private static void TryRun()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryRun;
            return;
        }

        SessionState.SetBool(SessionKey, true);

        bool builtAny = false;
        if (!File.Exists(Parcial1SceneBuilder.ScenePath))
        {
            Parcial1SceneBuilder.Build();
            builtAny = true;
        }
        if (!File.Exists(Parcial2SceneBuilder.ScenePath))
        {
            Parcial2SceneBuilder.Build();
            builtAny = true;
        }

        bool nothingOpen = string.IsNullOrEmpty(EditorSceneManager.GetActiveScene().path);
        if ((builtAny || nothingOpen) && File.Exists(Parcial2SceneBuilder.ScenePath))
        {
            EditorSceneManager.OpenScene(Parcial2SceneBuilder.ScenePath, OpenSceneMode.Single);
        }
    }

    [MenuItem("IA Parcial/Abrir escena Parcial 1", priority = 20)]
    private static void OpenParcial1() => Open(Parcial1SceneBuilder.ScenePath, Parcial1SceneBuilder.Build);

    [MenuItem("IA Parcial/Abrir escena Parcial 2", priority = 21)]
    private static void OpenParcial2() => Open(Parcial2SceneBuilder.ScenePath, Parcial2SceneBuilder.Build);

    private static void Open(string path, System.Action build)
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!File.Exists(path)) build();
        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
    }
}
