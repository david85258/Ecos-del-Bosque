using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector del GeneradorEstructuras: avisa de estructuras sin modelo y actualiza la preview del terreno
/// (solo la decoracion, sin rehacer el relieve) al cambiar valores.
/// </summary>
[CustomEditor(typeof(GeneradorEstructuras))]
public class GeneradorEstructurasEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var estructuras = (GeneradorEstructuras)target;
        var terreno = estructuras.GetComponentInParent<GeneradorTerreno>();

        if (!terreno)
            EditorGUILayout.HelpBox("Pon este componente en el objeto del GeneradorTerreno o en un hijo suyo.", MessageType.Error);

        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        bool cambiado = EditorGUI.EndChangeCheck();

        foreach (var e in estructuras.Estructuras)
        {
            if (e != null && e.activo && !e.modelo)
                EditorGUILayout.HelpBox($"'{e.nombre}' no tiene modelo y se ignorara.", MessageType.Warning);
        }

        string resumen = $"Estructuras colocadas: {estructuras.Colocadas.Count}";

        if (Application.isPlaying)
        {
            if (terreno && terreno.Generado)
                EditorGUILayout.HelpBox(resumen, MessageType.Info);
            return;
        }

        EditorGUILayout.Space(10);
        using (new EditorGUI.DisabledScope(!terreno))
        {
            if (GUILayout.Button(terreno && terreno.HayPreview ? "Actualizar estructuras en la preview" : "Generar preview"))
            {
                if (terreno.HayPreview) terreno.RegenerarDecoracion();
                else terreno.GenerarPreview(false);
                SceneView.RepaintAll();
            }
        }

        if (terreno && terreno.HayPreview)
            EditorGUILayout.HelpBox(resumen + "\nCirculos rojos (con el GeneradorTerreno seleccionado) = claros de las estructuras.",
                                    MessageType.None);

        if (cambiado && terreno)
            GeneradorTerrenoEditor.ProgramarPreview(terreno, true);
    }
}
