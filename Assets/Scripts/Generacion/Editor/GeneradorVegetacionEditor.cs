using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector del GeneradorVegetacion: actualiza la preview del terreno (solo la decoracion) al cambiar valores.
/// </summary>
[CustomEditor(typeof(GeneradorVegetacion))]
public class GeneradorVegetacionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var vegetacion = (GeneradorVegetacion)target;
        var terreno = vegetacion.GetComponentInParent<GeneradorTerreno>();

        if (!terreno)
            EditorGUILayout.HelpBox("Pon este componente en el objeto del GeneradorTerreno o en un hijo suyo.", MessageType.Error);

        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        bool cambiado = EditorGUI.EndChangeCheck();

        if (vegetacion.Hierbas.Count == 0 && vegetacion.Detalles.Count == 0)
            EditorGUILayout.HelpBox("No hay vegetacion asignada. Usa Herramientas > Vegetacion > Configurar en la escena " +
                                    "para crear y asignar las texturas por defecto.", MessageType.Info);

        string resumen = $"Hierba: {vegetacion.InstanciasHierba:N0}   Hojas/detalles: {vegetacion.InstanciasDetalles:N0}";

        if (Application.isPlaying)
        {
            if (terreno && terreno.Generado)
                EditorGUILayout.HelpBox(resumen, MessageType.Info);
            return;
        }

        EditorGUILayout.Space(10);
        using (new EditorGUI.DisabledScope(!terreno))
        {
            if (GUILayout.Button(terreno && terreno.HayPreview ? "Actualizar vegetacion en la preview" : "Generar preview"))
            {
                if (terreno.HayPreview) terreno.RegenerarDecoracion();
                else terreno.GenerarPreview(false);
                SceneView.RepaintAll();
            }
        }

        if (terreno && terreno.HayPreview)
            EditorGUILayout.HelpBox(resumen, MessageType.None);

        if (cambiado && terreno)
            GeneradorTerrenoEditor.ProgramarPreview(terreno, true);
    }
}
