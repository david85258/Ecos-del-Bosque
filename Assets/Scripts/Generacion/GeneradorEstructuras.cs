using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Coloca estructuras (casa, pozo...) en sitios aleatorios del bosque y adapta el terreno a ellas:
/// aplana el suelo bajo cada una y reserva un claro alrededor para que no salgan arboles ni hierba encima.
///
/// El tamano de cada estructura se mide solo: es el circulo que contiene el modelo visto desde arriba.
/// Si la medida no convence (por ejemplo, el modelo trae un suelo muy grande), se pone a mano en 'radio'.
///
/// Ponlo en el objeto del GeneradorTerreno o en un hijo: se ejecuta antes que los arboles y la vegetacion,
/// con la misma semilla.
/// </summary>
public class GeneradorEstructuras : GeneradorSobreTerreno
{
    [Serializable]
    public class Estructura
    {
        public string nombre = "Estructura";
        public bool activo = true;
        [Tooltip("Prefab o modelo importado (.glb / .fbx).")]
        public GameObject modelo;
        [Min(0), Tooltip("Cuantas se ponen en el mapa.")]
        public int cantidad = 1;
        [Min(0f), Tooltip("Radio (m) que ocupa el modelo visto desde arriba. 0 = medirlo solo a partir de la malla.")]
        public float radio = 0f;
        [Min(0f), Tooltip("Espacio (m) sin arboles alrededor del modelo.")]
        public float claro = 6f;
        [Min(0f), Tooltip("Distancia minima (m) al punto donde aparece el jugador. " +
                          "Si el mapa es pequeno y no se puede, se pone lo mas lejos posible.")]
        public float distanciaMinimaJugador = 0f;
        [Tooltip("Hundir un poco el modelo (m) para que no se vea flotando.")]
        public float hundimiento = 0.05f;
        public bool rotacionAleatoria = true;
        [Tooltip("Si el modelo no trae ningun collider, anadir un MeshCollider a cada una de sus mallas.")]
        public bool colisiones = true;
    }

    [Header("Estructuras")]
    [Tooltip("Se colocan en este orden: pon primero las mas grandes o las que tienen mas restricciones.")]
    [SerializeField] private List<Estructura> estructuras = new List<Estructura>();

    [Header("Colocacion")]
    [Tooltip("Pendiente maxima (grados) del suelo donde se puede poner una estructura.")]
    [SerializeField, Range(0f, 45f)] private float pendienteMaxima = 20f;
    [Tooltip("Distancia minima (m) entre los claros de dos estructuras.")]
    [SerializeField, Min(0f)] private float separacionMinima = 15f;
    [Tooltip("Suelo plano extra (m) alrededor del modelo.")]
    [SerializeField, Min(0f)] private float margenAplanado = 2f;
    [Tooltip("Ancho (m) de la rampa entre el suelo aplanado y el terreno normal.")]
    [SerializeField, Min(1f)] private float transicion = 6f;
    [Tooltip("Sitios al azar que se prueban por estructura antes de rendirse.")]
    [SerializeField, Min(1)] private int intentos = 300;

    // ---------------------------------------------------------------- Resultados

    private readonly List<Transform> colocadas = new List<Transform>();

    /// <summary>Todas las estructuras colocadas en esta generacion.</summary>
    public IReadOnlyList<Transform> Colocadas => colocadas;
    public List<Estructura> Estructuras => estructuras;

    /// <summary>Primera estructura colocada con ese nombre (por ejemplo "Casa"), o null si no hay.</summary>
    public Transform Buscar(string nombre) => colocadas.Find(t => t && t.name == nombre);

    private const string NombreContenedor = "__Estructuras";
    private GameObject contenedor;

    public override int Orden => -10; // antes que los arboles, para que ya vean los claros

    // ================================================================ Generacion

    public override void Generar(GeneradorTerreno generador, int semillaTerreno)
    {
        var rng = new System.Random(MezclarSemilla(semillaTerreno, 0x45535452));

        Limpiar();
        contenedor = new GameObject(Application.isPlaying ? "Estructuras" : NombreContenedor);
        if (!Application.isPlaying)
            contenedor.hideFlags = HideFlags.DontSave | HideFlags.NotEditable; // preview: no se guarda en la escena

        foreach (Estructura e in estructuras)
        {
            if (e == null || !e.activo || !e.modelo)
                continue;

            for (int i = 0; i < e.cantidad; i++)
                Colocar(generador, e, rng);
        }
    }

    private void Colocar(GeneradorTerreno generador, Estructura e, System.Random rng)
    {
        // Se crea el modelo sin girar para medirlo.
        GameObject obj = Instantiate(e.modelo, contenedor.transform);
        obj.name = e.nombre;

        float radio = MedirModelo(obj, out Vector3 centroModelo);
        if (e.radio > 0f)
            radio = e.radio;
        float radioClaro = radio + e.claro;
        float angulo = (float)rng.NextDouble() * 360f;

        if (!BuscarSitio(generador, rng, radio, radioClaro, e.distanciaMinimaJugador, out Vector3 sitio))
        {
            Debug.LogWarning($"GeneradorEstructuras: no hay sitio para '{e.nombre}' (radio {radio:0.#} m). " +
                             "Prueba a bajar su claro o la separacion minima, o a subir la pendiente maxima.", this);
            Destruir(obj);
            return;
        }

        // Adaptar el terreno: suelo plano debajo y claro alrededor.
        generador.AplanarZona(sitio, radio + margenAplanado, transicion);
        generador.ReservarClaro(sitio, radio, radioClaro);

        // 'sitio' es donde va el centro del modelo; el pivote se mueve lo que este descentrado (ya girado).
        Quaternion giro = Quaternion.Euler(0f, e.rotacionAleatoria ? angulo : 0f, 0f);
        Vector3 posicion = generador.EnSuelo(sitio) - giro * centroModelo + Vector3.down * e.hundimiento;
        obj.transform.SetPositionAndRotation(posicion, giro * obj.transform.rotation);

        if (e.colisiones)
            AnadirColisiones(obj);

        colocadas.Add(obj.transform);
    }

    /// <summary>Prueba sitios al azar y se queda con el valido mas lejano del jugador. Para en cuanto uno
    /// esta a la distancia pedida (asi en mapas pequenos tambien sale, lo mas lejos posible).</summary>
    private bool BuscarSitio(GeneradorTerreno generador, System.Random rng, float radio, float radioClaro,
                             float distanciaJugador, out Vector3 sitio)
    {
        Vector3 origen = generador.Terreno.transform.position;
        float tamano = generador.Tamano;
        float mejorDistancia = -1f;
        sitio = Vector3.zero;

        for (int i = 0; i < intentos; i++)
        {
            Vector3 p = generador.EnSuelo(origen + new Vector3((float)rng.NextDouble() * tamano, 0f,
                                                               (float)rng.NextDouble() * tamano));
            if (!SitioValido(generador, p, radio, radioClaro))
                continue;

            float distancia = Vector2.Distance(new Vector2(p.x, p.z),
                                               new Vector2(generador.PuntoInicio.x, generador.PuntoInicio.z));
            if (distancia > mejorDistancia)
            {
                mejorDistancia = distancia;
                sitio = p;
            }
            if (distancia >= distanciaJugador)
                break;
        }

        return mejorDistancia >= 0f;
    }

    /// <summary>Sin pisar otros claros, encima de la meseta y casi plano donde va el modelo.</summary>
    private bool SitioValido(GeneradorTerreno generador, Vector3 sitio, float radio, float radioClaro)
    {
        if (generador.EnClaro(sitio, radioClaro + separacionMinima))
            return false;

        if (!generador.EnMeseta(sitio) || generador.PendienteEn(sitio) > pendienteMaxima)
            return false;

        // 8 direcciones: donde se aplana debe ser casi plano, y todo el claro (con su rampa) encima de la meseta.
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4f;
            Vector3 direccion = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));

            if (generador.PendienteEn(sitio + direccion * (radio + margenAplanado)) > pendienteMaxima)
                return false;
            if (!generador.EnMeseta(sitio + direccion * (radioClaro + transicion)))
                return false;
        }

        return true;
    }

    // ================================================================ Modelos

    /// <summary>
    /// Mide el modelo visto desde arriba: junta las cajas de todas sus mallas y devuelve el radio del circulo
    /// que contiene esa caja. En 'centro' devuelve cuanto esta desplazado el centro de la caja respecto al
    /// pivote (hay modelos con el pivote descentrado).
    /// </summary>
    private static float MedirModelo(GameObject obj, out Vector3 centro)
    {
        Renderer[] mallas = obj.GetComponentsInChildren<Renderer>();
        if (mallas.Length == 0)
        {
            centro = Vector3.zero;
            return 1f;
        }

        Bounds caja = mallas[0].bounds;
        foreach (Renderer r in mallas)
            caja.Encapsulate(r.bounds);

        centro = caja.center - obj.transform.position;
        centro.y = 0f;
        return Mathf.Max(1f, new Vector2(caja.extents.x, caja.extents.z).magnitude);
    }

    private static void AnadirColisiones(GameObject obj)
    {
        if (obj.GetComponentInChildren<Collider>())
            return;

        foreach (MeshFilter malla in obj.GetComponentsInChildren<MeshFilter>())
        {
            if (malla.sharedMesh)
                malla.gameObject.AddComponent<MeshCollider>().sharedMesh = malla.sharedMesh;
        }
    }

    // ================================================================ Limpieza

    public override void Limpiar()
    {
        colocadas.Clear();
        if (!contenedor)
            return;

        Destruir(contenedor);
        contenedor = null;
    }

    private static void Destruir(GameObject go)
    {
        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }

    /// <summary>Borra estructuras de preview que se hayan quedado huerfanas (por ejemplo tras recompilar).</summary>
    public static void BorrarTemporalesHuerfanos()
    {
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go && go.name == NombreContenedor && !go.transform.parent &&
                (go.hideFlags & HideFlags.DontSave) != 0 && !EsAsset(go))
                DestroyImmediate(go);
        }
    }

    private static bool EsAsset(GameObject go)
    {
#if UNITY_EDITOR
        return UnityEditor.EditorUtility.IsPersistent(go);
#else
        return false;
#endif
    }
}
