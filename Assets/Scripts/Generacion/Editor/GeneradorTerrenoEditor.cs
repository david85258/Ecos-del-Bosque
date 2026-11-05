using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector del GeneradorTerreno con botones para ver una preview del terreno sin dar a Play.
/// La preview es un objeto temporal: no se guarda en la escena y se borra al entrar en Play.
/// </summary>
[CustomEditor(typeof(GeneradorTerreno))]
public class GeneradorTerrenoEditor : Editor
{
    private const string ClaveAutoActualizar = "GeneradorTerreno.AutoActualizar";

    private static GeneradorTerreno pendiente;
    private static double momentoActualizar;

    public override void OnInspectorGUI()
    {
        var generador = (GeneradorTerreno)target;

        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        bool cambiado = EditorGUI.EndChangeCheck();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Preview en el editor", EditorStyles.boldLabel);

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("En Play el terreno se genera automaticamente.", MessageType.Info);
            return;
        }

        bool auto = EditorPrefs.GetBool(ClaveAutoActualizar, true);
        bool nuevoAuto = EditorGUILayout.Toggle("Actualizar al cambiar valores", auto);
        if (nuevoAuto != auto)
            EditorPrefs.SetBool(ClaveAutoActualizar, nuevoAuto);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Generar preview"))
                generador.GenerarPreview(false);

            if (GUILayout.Button("Nueva semilla"))
            {
                Undo.RecordObject(generador, "Nueva semilla");
                generador.GenerarPreview(true);
                EditorUtility.SetDirty(generador);
            }

            using (new EditorGUI.DisabledScope(!generador.HayPreview))
            {
                if (GUILayout.Button("Borrar preview"))
                    generador.BorrarPreview();
            }
        }

        EditorGUILayout.HelpBox(
            "La preview usa la semilla actual. Si 'Semilla Aleatoria' esta activo, al dar a Play saldra otro mapa: " +
            "desactivalo para jugar exactamente el de la preview.\n" +
            "Circulo verde = inicio del jugador. Circulo rojo = claro de la casa.",
            MessageType.None);

        // Regenerar poco despues de cambiar un valor (no en cada pulsacion).
        if (cambiado && nuevoAuto && generador.HayPreview)
        {
            pendiente = generador;
            momentoActualizar = EditorApplication.timeSinceStartup + 0.4;
            EditorApplication.update -= ActualizarPendiente;
            EditorApplication.update += ActualizarPendiente;
        }
    }

    private static void ActualizarPendiente()
    {
        if (EditorApplication.timeSinceStartup < momentoActualizar)
            return;

        EditorApplication.update -= ActualizarPendiente;
        if (pendiente && !Application.isPlaying)
            pendiente.GenerarPreview(false);
        pendiente = null;
    }

    // ---------------------------------------------------------------- Limpieza automatica

    [InitializeOnLoadMethod]
    private static void Registrar()
    {
        EditorApplication.playModeStateChanged += estado =>
        {
            if (estado == PlayModeStateChange.ExitingEditMode)
                BorrarTodas();
        };
        AssemblyReloadEvents.beforeAssemblyReload += BorrarTodas;
        UnityEditor.SceneManagement.EditorSceneManager.sceneClosing += (escena, quitada) => BorrarTodas();
    }

    private static void BorrarTodas()
    {
        foreach (var generador in Object.FindObjectsByType<GeneradorTerreno>(FindObjectsInactive.Include))
            generador.BorrarPreview();

        // Por si quedo alguna preview huerfana.
        foreach (var t in Resources.FindObjectsOfTypeAll<Terrain>())
        {
            if (t && t.name == GeneradorTerreno.NombrePreview && (t.gameObject.hideFlags & HideFlags.DontSave) != 0)
            {
                if (t.terrainData) Object.DestroyImmediate(t.terrainData);
                Object.DestroyImmediate(t.gameObject);
            }
        }
    }
}
