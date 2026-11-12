using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector del GeneradorArboles: avisa de modelos sin malla y actualiza la preview del terreno
/// (solo los arboles, sin rehacer el relieve) al cambiar valores.
/// </summary>
[CustomEditor(typeof(GeneradorArboles))]
public class GeneradorArbolesEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var arboles = (GeneradorArboles)target;
        var terreno = arboles.GetComponentInParent<GeneradorTerreno>();

        if (!terreno)
            EditorGUILayout.HelpBox("Pon este componente en el objeto del GeneradorTerreno o en un hijo suyo.", MessageType.Error);

        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        bool cambiado = EditorGUI.EndChangeCheck();

        foreach (var grupo in arboles.Grupos)
        {
            if (grupo == null) continue;
            foreach (var m in grupo.modelos)
            {
                if (m != null && m.modelo && !GeneradorArboles.ModeloValido(m.modelo))
                    EditorGUILayout.HelpBox($"'{m.modelo.name}' (grupo '{grupo.nombre}') no tiene ninguna malla y se ignorara.",
                                            MessageType.Warning);
            }
        }

        if (Application.isPlaying)
        {
            if (terreno && terreno.Generado)
                EditorGUILayout.HelpBox($"Arboles: {arboles.ArbolesColocados}   Grupo: {arboles.GrupoElegido}", MessageType.Info);
            return;
        }

        EditorGUILayout.Space(10);
        using (new EditorGUI.DisabledScope(!terreno))
        {
            if (GUILayout.Button(terreno && terreno.HayPreview ? "Actualizar arboles en la preview" : "Generar preview"))
            {
                if (terreno.HayPreview) terreno.RegenerarDecoracion();
                else terreno.GenerarPreview(false);
                SceneView.RepaintAll();
            }
        }

        if (terreno && terreno.HayPreview)
            EditorGUILayout.HelpBox($"Arboles en la preview: {arboles.ArbolesColocados}   Grupo: {arboles.GrupoElegido}", MessageType.None);

        if (cambiado && terreno)
            GeneradorTerrenoEditor.ProgramarPreview(terreno, true);
    }
}
