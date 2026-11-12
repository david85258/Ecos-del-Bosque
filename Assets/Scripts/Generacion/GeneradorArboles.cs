using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Planta arboles en el terreno usando el sistema de arboles del propio Terrain
/// (TreePrototypes + TreeInstances): instancing, culling y distancia de dibujado los gestiona Unity.
///
/// Los modelos se organizan en grupos (estilos) para no mezclar arboles que no pegan entre si.
/// Acepta prefabs normales y modelos importados (.glb / .fbx) tal cual.
///
/// Ponlo en el mismo objeto que el GeneradorTerreno o en un hijo: se ejecuta solo, con la misma semilla.
/// </summary>
public class GeneradorArboles : GeneradorSobreTerreno
{
    public enum ModoGrupos
    {
        [Tooltip("Cada partida elige un solo grupo para todo el mapa.")]
        UnoPorPartida,
        [Tooltip("El mapa se divide en zonas grandes y cada zona usa un grupo.")]
        PorZonas
    }

    [Serializable]
    public class ModeloArbol
    {
        [Tooltip("Prefab o modelo importado (.glb / .fbx).")]
        public GameObject modelo;
        [Min(0f), Tooltip("Probabilidad relativa dentro del grupo.")]
        public float probabilidad = 1f;
        [Tooltip("Escala minima y maxima.")]
        public Vector2 escala = new Vector2(0.85f, 1.2f);
        [Min(0f), Tooltip("Radio (m) del collider del tronco. 0 = sin collider. " +
                          "Solo se usa si el modelo no trae ya un CapsuleCollider en la raiz.")]
        public float radioTronco = 0.35f;
        [Min(0f), Tooltip("Altura (m) del collider del tronco.")]
        public float alturaTronco = 4f;
    }

    [Serializable]
    public class GrupoArboles
    {
        public string nombre = "Grupo";
        public bool activo = true;
        [Min(0f), Tooltip("Probabilidad relativa de que salga este grupo (o de su zona).")]
        public float probabilidad = 1f;
        public List<ModeloArbol> modelos = new List<ModeloArbol>();
    }

    [Header("Modelos")]
    [SerializeField] private List<GrupoArboles> grupos = new List<GrupoArboles>();
    [SerializeField] private ModoGrupos modoGrupos = ModoGrupos.UnoPorPartida;
    [Tooltip("Tamano aproximado (m) de cada zona en el modo Por Zonas.")]
    [SerializeField] private float tamanoZonas = 300f;

    [Header("Reparto")]
    [Tooltip("Distancia media (m) entre arboles. Menos = bosque mas denso.")]
    [SerializeField, Min(1f)] private float separacion = 7f;
    [Tooltip("Probabilidad de que haya arbol en cada hueco.")]
    [SerializeField, Range(0f, 1f)] private float densidad = 0.8f;
    [Tooltip("Cuanto varia la densidad por zonas (0 = uniforme, 1 = zonas casi vacias y zonas muy densas).")]
    [SerializeField, Range(0f, 1f)] private float variacionDensidad = 0.6f;
    [SerializeField] private float escalaManchas = 90f;
    [Tooltip("No se ponen arboles con mas pendiente que esta (grados).")]
    [SerializeField, Range(0f, 90f)] private float pendienteMaxima = 32f;
    [Tooltip("Distancia extra (m) a los claros de la casa y del inicio.")]
    [SerializeField] private float margenClaros = 3f;
    [Tooltip("Hundir un poco el arbol (m) para que no se vea la base flotando en pendientes.")]
    [SerializeField] private float hundimiento = 0.15f;
    [Tooltip("Variacion de color (oscurecer) entre arboles. Solo se nota si el shader lo usa.")]
    [SerializeField, Range(0f, 0.5f)] private float variacionColor = 0.15f;

    [Header("Render (culling)")]
    [Tooltip("Distancia maxima (m) a la que se dibujan arboles.")]
    [SerializeField] private float distanciaDibujado = 400f;
    [Tooltip("A partir de esta distancia se usan billboards (solo arboles SpeedTree).")]
    [SerializeField] private float distanciaBillboard = 120f;
    [SerializeField] private int maximoArbolesCompletos = 2000;
    [Tooltip("Anadir un collider al tronco de los modelos que no lo traigan (usa el radio de cada modelo).")]
    [SerializeField] private bool colisiones = true;

    // ---------------------------------------------------------------- Resultados

    public int ArbolesColocados { get; private set; }
    public string GrupoElegido { get; private set; }

    private const string NombreTemporales = "__ArbolesTemporales";
    private GameObject contenedorTemporales;

    public override int Orden => 0;

    // ================================================================ Generacion

    public override void Generar(GeneradorTerreno generador, int semillaTerreno)
    {
        Terrain terreno = generador.Terreno;
        TerrainData datos = terreno.terrainData;
        int semilla = MezclarSemilla(semillaTerreno, 0x4152424C);
        var rng = new System.Random(semilla);
        var ruido = new Ruido(semilla);

        Limpiar();
        ArbolesColocados = 0;
        GrupoElegido = null;

        // Grupos validos y sus prototipos.
        var gruposValidos = new List<GrupoValido>();
        var prototipos = new List<TreePrototype>();
        foreach (GrupoArboles g in grupos)
        {
            if (g == null || !g.activo || g.probabilidad <= 0f)
                continue;

            var valido = new GrupoValido { grupo = g };
            foreach (ModeloArbol m in g.modelos)
            {
                if (m == null || !m.modelo || m.probabilidad <= 0f)
                    continue;

                prototipos.Add(new TreePrototype { prefab = PrepararModelo(m), bendFactor = 0f });
                valido.indices.Add(prototipos.Count - 1);
                valido.modelos.Add(m);
                valido.pesoTotal += m.probabilidad;
            }

            if (valido.indices.Count > 0)
                gruposValidos.Add(valido);
        }

        datos.SetTreeInstances(Array.Empty<TreeInstance>(), false);
        datos.treePrototypes = prototipos.ToArray();

        if (gruposValidos.Count == 0)
        {
            Debug.LogWarning("GeneradorArboles: no hay ningun grupo con modelos asignados.", this);
            return;
        }

        // En modo UnoPorPartida se elige un solo grupo para todo el mapa.
        GrupoValido unico = null;
        if (modoGrupos == ModoGrupos.UnoPorPartida || gruposValidos.Count == 1)
        {
            float total = 0f;
            foreach (var g in gruposValidos) total += g.grupo.probabilidad;
            float r = (float)rng.NextDouble() * total;
            unico = gruposValidos[gruposValidos.Count - 1];
            foreach (var g in gruposValidos)
            {
                r -= g.grupo.probabilidad;
                if (r <= 0f) { unico = g; break; }
            }
            GrupoElegido = unico.grupo.nombre;
        }
        else
        {
            GrupoElegido = "(por zonas)";
        }

        // Rejilla con desplazamiento aleatorio: reparto natural sin arboles pegados.
        float tamano = generador.Tamano;
        Vector3 origen = terreno.transform.position;
        int celdas = Mathf.CeilToInt(tamano / separacion);
        var instancias = new List<TreeInstance>(celdas * celdas / 2);

        for (int cz = 0; cz < celdas; cz++)
        {
            for (int cx = 0; cx < celdas; cx++)
            {
                // Consumir siempre los mismos numeros aleatorios por celda: asi cambiar un filtro
                // no desplaza todo el resto del bosque.
                float jx = (float)rng.NextDouble(), jz = (float)rng.NextDouble();
                float azar = (float)rng.NextDouble();
                float azarModelo = (float)rng.NextDouble();
                float azarEscala = (float)rng.NextDouble();
                float azarAncho = (float)rng.NextDouble();
                float azarRotacion = (float)rng.NextDouble();
                float azarColor = (float)rng.NextDouble();

                float wx = (cx + 0.1f + jx * 0.8f) * separacion;
                float wz = (cz + 0.1f + jz * 0.8f) * separacion;
                if (wx >= tamano || wz >= tamano)
                    continue;

                // Densidad variable: manchas mas cerradas y zonas mas abiertas.
                float mancha = ruido.Fbm(wx / escalaManchas + 1500f, wz / escalaManchas + 1500f, 3);
                float probabilidad = densidad * Mathf.Lerp(1f, Mathf.Clamp01(mancha * 2f - 0.3f), variacionDensidad);
                if (azar > probabilidad)
                    continue;

                Vector3 posicion = origen + new Vector3(wx, 0f, wz);
                if (!generador.EsZonaLibre(posicion, margenClaros))
                    continue;
                if (generador.PendienteEn(posicion) > pendienteMaxima)
                    continue;

                GrupoValido grupo = unico ?? GrupoDeZona(gruposValidos, ruido, wx, wz);
                ModeloArbol modelo;
                int prototipo = ElegirModelo(grupo, azarModelo, out modelo);

                float escala = Mathf.Lerp(modelo.escala.x, modelo.escala.y, azarEscala);
                float ancho = escala * Mathf.Lerp(0.9f, 1.1f, azarAncho);
                float y = (generador.AlturaEn(posicion) - origen.y - hundimiento * escala) / datos.size.y;
                float tono = 1f - azarColor * variacionColor;

                instancias.Add(new TreeInstance
                {
                    prototypeIndex = prototipo,
                    position = new Vector3(wx / tamano, Mathf.Max(0f, y), wz / tamano),
                    heightScale = escala,
                    widthScale = ancho,
                    rotation = azarRotacion * Mathf.PI * 2f,
                    color = new Color(tono, tono, tono, 1f),
                    lightmapColor = Color.white
                });
            }
        }

        datos.SetTreeInstances(instancias.ToArray(), false);
        ArbolesColocados = instancias.Count;

        terreno.treeDistance = distanciaDibujado;
        terreno.treeBillboardDistance = distanciaBillboard;
        terreno.treeMaximumFullLODCount = maximoArbolesCompletos;
        terreno.drawTreesAndFoliage = true;

        terreno.Flush();
    }

    private class GrupoValido
    {
        public GrupoArboles grupo;
        public readonly List<int> indices = new List<int>();
        public readonly List<ModeloArbol> modelos = new List<ModeloArbol>();
        public float pesoTotal;
    }

    /// <summary>En modo por zonas, cada grupo "compite" con un ruido propio y gana el mayor:
    /// salen zonas grandes de un solo estilo, sin mezclar arboles sueltos.</summary>
    private GrupoValido GrupoDeZona(List<GrupoValido> lista, Ruido ruido, float wx, float wz)
    {
        GrupoValido mejor = lista[0];
        float mejorValor = float.MinValue;
        for (int i = 0; i < lista.Count; i++)
        {
            float n = ruido.Fbm(wx / tamanoZonas + i * 173.3f + 50f, wz / tamanoZonas + i * 91.7f + 50f, 3);
            float valor = n + Mathf.Log(lista[i].grupo.probabilidad + 0.0001f) * 0.1f;
            if (valor > mejorValor)
            {
                mejorValor = valor;
                mejor = lista[i];
            }
        }
        return mejor;
    }

    private static int ElegirModelo(GrupoValido grupo, float azar, out ModeloArbol modelo)
    {
        float r = azar * grupo.pesoTotal;
        for (int i = 0; i < grupo.modelos.Count; i++)
        {
            r -= grupo.modelos[i].probabilidad;
            if (r <= 0f)
            {
                modelo = grupo.modelos[i];
                return grupo.indices[i];
            }
        }

        int ultimo = grupo.modelos.Count - 1;
        modelo = grupo.modelos[ultimo];
        return grupo.indices[ultimo];
    }

    // ================================================================ Modelos

    /// <summary>
    /// El Terrain solo acepta arboles con un MeshRenderer o un LODGroup en la raiz, y los colliders
    /// de arbol solo si hay un CapsuleCollider en la raiz. Los .glb importados suelen tener la malla
    /// en un hijo, asi que se crea una copia oculta con un LODGroup (y el collider) en la raiz.
    /// Se usa siempre LODGroup porque sin el Unity exige el shader Nature/Soft Occlusion.
    /// Las mallas y materiales se comparten con el original.
    /// </summary>
    private GameObject PrepararModelo(ModeloArbol m)
    {
        GameObject original = m.modelo;
        bool lodEnRaiz = original.GetComponent<LODGroup>();
        bool necesitaCollider = colisiones && m.radioTronco > 0f && !original.GetComponent<CapsuleCollider>();

        // Con LODGroup en la raiz el Terrain no pide el shader Nature/Soft Occlusion (que en URP no aplica).
        if (lodEnRaiz && !necesitaCollider)
            return original;

        if (!contenedorTemporales)
        {
            contenedorTemporales = new GameObject(NombreTemporales) { hideFlags = HideFlags.HideAndDontSave };
            contenedorTemporales.SetActive(false);
        }

        GameObject copia = Instantiate(original, contenedorTemporales.transform);
        copia.name = original.name;
        copia.hideFlags = HideFlags.HideAndDontSave;
        copia.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

        if (!lodEnRaiz)
        {
            var lod = copia.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(0.01f, copia.GetComponentsInChildren<Renderer>(true)) });
            lod.RecalculateBounds();
        }

        if (necesitaCollider)
        {
            var capsula = copia.AddComponent<CapsuleCollider>();
            capsula.radius = m.radioTronco;
            capsula.height = Mathf.Max(m.alturaTronco, m.radioTronco * 2f);
            capsula.center = new Vector3(0f, capsula.height * 0.5f, 0f);
        }

        return copia;
    }

    /// <summary>True si el modelo se puede usar como arbol (tiene alguna malla).</summary>
    public static bool ModeloValido(GameObject modelo) =>
        modelo && modelo.GetComponentsInChildren<Renderer>(true).Length > 0;

    public IReadOnlyList<GrupoArboles> Grupos => grupos;

    public override void Limpiar()
    {
        if (!contenedorTemporales)
            return;

        if (Application.isPlaying) Destroy(contenedorTemporales);
        else DestroyImmediate(contenedorTemporales);
        contenedorTemporales = null;
    }

    /// <summary>Borra copias temporales que se hayan quedado huerfanas (por ejemplo tras recompilar).</summary>
    public static void BorrarTemporalesHuerfanos()
    {
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go && go.name == NombreTemporales && !go.transform.parent &&
                (go.hideFlags & HideFlags.DontSave) != 0 && !UnityEngineObjectIsAsset(go))
                DestroyImmediate(go);
        }
    }

    private static bool UnityEngineObjectIsAsset(GameObject go)
    {
#if UNITY_EDITOR
        return UnityEditor.EditorUtility.IsPersistent(go);
#else
        return false;
#endif
    }
}
