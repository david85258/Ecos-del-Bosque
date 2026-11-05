using System;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Genera un terreno de bosque aleatorio al iniciar la escena: una meseta con colinas suaves
/// rodeada de acantilados, un claro aleatorio para la casa y un claro pequeno donde aparece el jugador.
///
/// Los demas generadores (vegetacion, arboles, objetos) deben leer de aqui
/// en su Start() o suscribirse a <see cref="TerrenoGenerado"/>.
/// En el editor se puede ver una preview con los botones del inspector.
/// </summary>
[DefaultExecutionOrder(-100)]
public class GeneradorTerreno : MonoBehaviour
{
    [Header("Semilla")]
    [Tooltip("Si esta activo, cada partida usa una semilla nueva. Desactivalo para repetir siempre el mismo mapa.")]
    [SerializeField] private bool semillaAleatoria = true;
    [SerializeField] private int semilla = 12345;

    [Header("Terreno")]
    [Tooltip("Terreno de la escena a usar. Si se deja vacio se crea uno nuevo.")]
    [SerializeField] private Terrain terreno;
    [Tooltip("Opcional. Si se deja vacio se usa el material de terreno del pipeline (URP).")]
    [SerializeField] private Material materialTerreno;
    [SerializeField] private float tamano = 1024f;
    [SerializeField] private float alturaMaxima = 120f;
    [Tooltip("Debe ser 2^n + 1 (513, 1025, 2049).")]
    [SerializeField] private int resolucionAltura = 1025;
    [Tooltip("Potencia de 2 (512, 1024, 2048).")]
    [SerializeField] private int resolucionTexturas = 1024;

    [Header("Relieve")]
    [Tooltip("Altura (m) del suelo del bosque sobre el fondo de los acantilados.")]
    [SerializeField] private float alturaMeseta = 70f;
    [Tooltip("Diferencia de altura (m) entre los valles y las colinas del bosque.")]
    [SerializeField] private float alturaColinas = 25f;
    [SerializeField] private float escalaColinas = 280f;
    [SerializeField, Range(1, 8)] private int octavas = 5;
    [SerializeField, Range(0f, 1f)] private float persistencia = 0.45f;
    [SerializeField] private float lacunaridad = 2f;
    [SerializeField, Range(0f, 1f)] private float intensidadCrestas = 0.15f;
    [SerializeField] private float escalaDeformacion = 250f;
    [SerializeField] private float fuerzaDeformacion = 60f;
    [Tooltip("Mayor que 1 = valles mas anchos y planos.")]
    [SerializeField, Range(0.5f, 3f)] private float exponenteValles = 1.4f;

    [Header("Acantilados")]
    [SerializeField] private bool acantilados = true;
    [Tooltip("Distancia media (m) desde el borde del mapa hasta el acantilado.")]
    [SerializeField] private float distanciaAcantilado = 70f;
    [Tooltip("Cuanto entra y sale (m) la linea del acantilado.")]
    [SerializeField] private float irregularidadBorde = 35f;
    [Tooltip("Ancho (m) de la pared del acantilado. Menos = mas vertical.")]
    [SerializeField] private float anchoCaida = 12f;
    [Tooltip("Altura (m) del fondo, abajo del acantilado.")]
    [SerializeField] private float alturaFondo = 2f;

    [Header("Claros")]
    [SerializeField] private float radioClaroCasa = 18f;
    [SerializeField] private float radioClaroInicio = 6f;
    [Tooltip("Distancia minima (m) entre el jugador y la casa.")]
    [SerializeField] private float distanciaMinimaCasa = 300f;

    [Header("Texturas (opcional; las vacias se generan por codigo)")]
    [SerializeField] private TerrainLayer capaHierba;
    [SerializeField] private TerrainLayer capaHojarasca;
    [SerializeField] private TerrainLayer capaRoca;
    [SerializeField] private float anguloRocaMin = 30f;
    [SerializeField] private float anguloRocaMax = 45f;

    [Header("Jugador")]
    [Tooltip("Si se asigna, se coloca en el claro de inicio al empezar.")]
    [SerializeField] private Transform jugador;

    // ---------------------------------------------------------------- Resultados publicos

    public event Action TerrenoGenerado;

    public bool Generado { get; private set; }
    public Terrain Terreno => terrenoActivo;
    public int Semilla => semilla;
    public float Tamano => tamano;
    public float RadioClaroCasa => radioClaroCasa;
    public float RadioClaroInicio => radioClaroInicio;

    /// <summary>Punto (en el suelo) donde aparece el jugador.</summary>
    public Vector3 PuntoInicio { get; private set; }
    /// <summary>Centro del claro de la casa (en el suelo).</summary>
    public Vector3 PosicionCasa { get; private set; }
    /// <summary>Rotacion aleatoria para la casa.</summary>
    public Quaternion RotacionCasa { get; private set; }

    // ---------------------------------------------------------------- Datos internos

    private const int Hierba = 0, Hojarasca = 1, Roca = 2;
    private static TerrainLayer[] capasGeneradas;

    private Ruido ruido;
    private System.Random rng;
    private float[,] alturas;       // [z, x] normalizadas 0..1
    private float[] mascaraMeseta;  // 1 = encima de la meseta, 0 = fondo del acantilado
    private Vector2 inicioNorm, casaNorm;
    private Terrain terrenoActivo;

    void Awake()
    {
        if (semillaAleatoria)
            NuevaSemilla();

        Construir(false);
    }

    [ContextMenu("Regenerar (en Play)")]
    public void Generar()
    {
        if (semillaAleatoria)
            NuevaSemilla();

        Construir(false);
    }

    private void NuevaSemilla() =>
        semilla = new System.Random().Next(int.MinValue, int.MaxValue);

    private void Construir(bool esPreview)
    {
        var reloj = System.Diagnostics.Stopwatch.StartNew();

        rng = new System.Random(semilla);
        ruido = new Ruido(semilla);
        resolucionAltura = Mathf.ClosestPowerOfTwo(Mathf.Max(32, resolucionAltura - 1)) + 1;
        resolucionTexturas = Mathf.Clamp(Mathf.ClosestPowerOfTwo(resolucionTexturas), 16, 2048);
        alturaMaxima = Mathf.Max(alturaMaxima, alturaMeseta + alturaColinas + 1f);

        alturas = GenerarRelieve();
        ElegirPuntosClave();
        Aplanar(casaNorm, radioClaroCasa, radioClaroCasa * 0.8f);
        Aplanar(inicioNorm, radioClaroInicio, radioClaroInicio);

        TerrainData datos = CrearDatos(PintarTexturas());

#if UNITY_EDITOR
        if (esPreview)
            terrenoActivo = AplicarAPreview(datos);
        else
#endif
            terrenoActivo = AplicarAlTerreno(datos);

        CalcularResultados();
        Generado = true;

        if (esPreview)
            return;

        ColocarJugador();
        Debug.Log($"Terreno generado (semilla {semilla}) en {reloj.ElapsedMilliseconds} ms.", this);
        TerrenoGenerado?.Invoke();
    }

    // ================================================================ Consultas para otros generadores

    /// <summary>Altura del suelo (Y de mundo) en esa posicion.</summary>
    public float AlturaEn(Vector3 posicion) =>
        terrenoActivo.SampleHeight(posicion) + terrenoActivo.transform.position.y;

    /// <summary>Devuelve la posicion con la Y pegada al suelo.</summary>
    public Vector3 EnSuelo(Vector3 posicion) =>
        new Vector3(posicion.x, AlturaEn(posicion), posicion.z);

    /// <summary>Pendiente del suelo en grados.</summary>
    public float PendienteEn(Vector3 posicion)
    {
        Vector2 n = ANormalizado(posicion);
        return terrenoActivo.terrainData.GetSteepness(n.x, n.y);
    }

    public Vector3 NormalEn(Vector3 posicion)
    {
        Vector2 n = ANormalizado(posicion);
        return terrenoActivo.terrainData.GetInterpolatedNormal(n.x, n.y);
    }

    /// <summary>True si la posicion esta encima de la meseta (no en la pared ni abajo del acantilado).</summary>
    public bool EnMeseta(Vector3 posicion)
    {
        Vector2 n = ANormalizado(posicion);
        return Muestrear(mascaraMeseta, resolucionAltura, n.x, n.y) > 0.98f;
    }

    /// <summary>True si la posicion esta dentro del claro de la casa o del de inicio (mas un margen).</summary>
    public bool EnClaro(Vector3 posicion, float margen = 0f)
    {
        Vector2 p = new Vector2(posicion.x, posicion.z);
        return Vector2.Distance(p, new Vector2(PosicionCasa.x, PosicionCasa.z)) < radioClaroCasa + margen ||
               Vector2.Distance(p, new Vector2(PuntoInicio.x, PuntoInicio.z)) < radioClaroInicio + margen;
    }

    /// <summary>True si se puede poner algo ahi: encima de la meseta y fuera de los claros.</summary>
    public bool EsZonaLibre(Vector3 posicion, float margenClaro = 0f) =>
        EnMeseta(posicion) && !EnClaro(posicion, margenClaro);

    /// <summary>Posicion de mundo a coordenadas normalizadas del terreno (0..1).</summary>
    public Vector2 ANormalizado(Vector3 posicion)
    {
        Vector3 local = posicion - terrenoActivo.transform.position;
        return new Vector2(Mathf.Clamp01(local.x / tamano), Mathf.Clamp01(local.z / tamano));
    }

    // ================================================================ Relieve

    private float[,] GenerarRelieve()
    {
        int res = resolucionAltura;
        var h = new float[res, res];
        mascaraMeseta = new float[res * res];

        Parallel.For(0, res, z =>
        {
            for (int x = 0; x < res; x++)
            {
                float wx = x / (float)(res - 1) * tamano;
                float wz = z / (float)(res - 1) * tamano;

                // Deformar las coordenadas para que las colinas tengan formas mas organicas.
                float qx = ruido.Fbm(wx / escalaDeformacion + 13.1f, wz / escalaDeformacion + 7.7f, 3) * 2f - 1f;
                float qz = ruido.Fbm(wx / escalaDeformacion + 91.3f, wz / escalaDeformacion + 45.2f, 3) * 2f - 1f;
                float px = (wx + qx * fuerzaDeformacion) / escalaColinas;
                float pz = (wz + qz * fuerzaDeformacion) / escalaColinas;

                float colinas = ruido.Fbm(px, pz, octavas, persistencia, lacunaridad);
                float crestas = ruido.Crestas(px * 1.3f + 200f, pz * 1.3f + 200f,
                                              Mathf.Max(1, octavas - 2), persistencia, lacunaridad);

                h[z, x] = Mathf.Lerp(colinas, crestas, intensidadCrestas);
            }
        });

        float min = float.MaxValue, max = float.MinValue;
        foreach (float v in h)
        {
            if (v < min) min = v;
            if (v > max) max = v;
        }
        float rango = Mathf.Max(0.0001f, max - min);

        float mesetaN = alturaMeseta / alturaMaxima;
        float colinasN = alturaColinas / alturaMaxima;
        float fondoN = alturaFondo / alturaMaxima;

        Parallel.For(0, res, z =>
        {
            for (int x = 0; x < res; x++)
            {
                float t = (h[z, x] - min) / rango;
                float valor = mesetaN + Mathf.Pow(t, exponenteValles) * colinasN;
                float meseta = 1f;

                if (acantilados)
                {
                    float wx = x / (float)(res - 1) * tamano;
                    float wz = z / (float)(res - 1) * tamano;
                    float distBorde = Mathf.Min(Mathf.Min(wx, tamano - wx), Mathf.Min(wz, tamano - wz));

                    // Linea del acantilado irregular: entrantes grandes y algo de detalle.
                    float irregular = (ruido.Fbm(wx / 180f + 300f, wz / 180f + 300f, 4) * 2f - 1f) * irregularidadBorde
                                    + ruido.Perlin(wx / 25f + 50f, wz / 25f + 50f) * irregularidadBorde * 0.15f;

                    float caida = SuaveEntre(distanciaAcantilado + anchoCaida * 0.5f,
                                             distanciaAcantilado - anchoCaida * 0.5f,
                                             distBorde + irregular);

                    float fondo = fondoN + ruido.Fbm(wx / 60f + 800f, wz / 60f + 800f, 3) * 4f / alturaMaxima;
                    valor = Mathf.Lerp(valor, fondo, caida);
                    meseta = 1f - caida;
                }

                h[z, x] = Mathf.Clamp01(valor);
                mascaraMeseta[z * res + x] = meseta;
            }
        });

        return h;
    }

    // ================================================================ Claros

    private void ElegirPuntosClave()
    {
        float margenMetros = acantilados
            ? distanciaAcantilado + irregularidadBorde * 1.2f + anchoCaida + radioClaroCasa + 10f
            : radioClaroCasa + 20f;
        float margen = Mathf.Clamp(margenMetros / tamano, 0.02f, 0.45f);

        inicioNorm = PuntoAleatorio(margen);

        // La casa en un sitio aleatorio, lo bastante lejos del jugador.
        casaNorm = PuntoAleatorio(margen);
        float mejorDistancia = 0f;
        for (int i = 0; i < 200; i++)
        {
            Vector2 candidato = PuntoAleatorio(margen);
            float distancia = Vector2.Distance(candidato, inicioNorm) * tamano;
            if (distancia > mejorDistancia)
            {
                mejorDistancia = distancia;
                casaNorm = candidato;
            }
            if (distancia >= distanciaMinimaCasa)
                break;
        }

        RotacionCasa = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
    }

    private Vector2 PuntoAleatorio(float margen) =>
        new Vector2(Mathf.Lerp(margen, 1f - margen, (float)rng.NextDouble()),
                    Mathf.Lerp(margen, 1f - margen, (float)rng.NextDouble()));

    /// <summary>Aplana una zona circular a su altura media, con una transicion suave.</summary>
    private void Aplanar(Vector2 centroNorm, float radio, float transicion)
    {
        int res = resolucionAltura;
        float celda = tamano / (res - 1);
        float cx = centroNorm.x * (res - 1);
        float cz = centroNorm.y * (res - 1);
        int alcance = Mathf.CeilToInt((radio + transicion) / celda);

        int x0 = Mathf.Max(0, Mathf.FloorToInt(cx) - alcance), x1 = Mathf.Min(res - 1, Mathf.CeilToInt(cx) + alcance);
        int z0 = Mathf.Max(0, Mathf.FloorToInt(cz) - alcance), z1 = Mathf.Min(res - 1, Mathf.CeilToInt(cz) + alcance);

        float suma = 0f;
        int cuenta = 0;
        for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz)) * celda;
                if (d > radio) continue;
                suma += alturas[z, x];
                cuenta++;
            }

        if (cuenta == 0)
            return;

        float objetivo = suma / cuenta;
        for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz)) * celda;
                float peso = SuaveEntre(radio + transicion, radio, d);
                alturas[z, x] = Mathf.Lerp(alturas[z, x], objetivo, peso);
            }
    }

    // ================================================================ Texturas

    private float[,,] PintarTexturas()
    {
        int res = resolucionAltura;
        int ar = resolucionTexturas;
        float celda = tamano / (res - 1);

        // Pendiente en grados de cada punto del mapa de alturas.
        var pendientes = new float[res * res];
        Parallel.For(0, res, z =>
        {
            for (int x = 0; x < res; x++)
            {
                int xa = Mathf.Max(0, x - 1), xb = Mathf.Min(res - 1, x + 1);
                int za = Mathf.Max(0, z - 1), zb = Mathf.Min(res - 1, z + 1);
                float ddx = (alturas[z, xb] - alturas[z, xa]) * alturaMaxima / ((xb - xa) * celda);
                float ddz = (alturas[zb, x] - alturas[za, x]) * alturaMaxima / ((zb - za) * celda);
                pendientes[z * res + x] = Mathf.Atan(Mathf.Sqrt(ddx * ddx + ddz * ddz)) * Mathf.Rad2Deg;
            }
        });

        Vector2 casa = casaNorm * tamano, inicio = inicioNorm * tamano;
        var mapa = new float[ar, ar, 3];

        Parallel.For(0, ar, az =>
        {
            for (int ax = 0; ax < ar; ax++)
            {
                float u = (ax + 0.5f) / ar, v = (az + 0.5f) / ar;
                float wx = u * tamano, wz = v * tamano;

                // Roca en las paredes y algo en el fondo de los acantilados.
                float roca = SuaveEntre(anguloRocaMin, anguloRocaMax, Muestrear(pendientes, res, u, v));
                float fondo = 1f - Muestrear(mascaraMeseta, res, u, v);
                roca = Mathf.Max(roca, fondo * 0.6f);

                // Manchas de hojarasca bajo el bosque; los claros quedan con hierba.
                float hojarasca = SuaveEntre(0.42f, 0.58f, ruido.Fbm(wx / 45f + 700f, wz / 45f + 700f, 4));
                Vector2 p = new Vector2(wx, wz);
                float claro = Mathf.Max(SuaveEntre(radioClaroCasa * 1.3f, radioClaroCasa * 0.6f, Vector2.Distance(p, casa)),
                                        SuaveEntre(radioClaroInicio * 1.5f, radioClaroInicio * 0.5f, Vector2.Distance(p, inicio)));
                hojarasca *= 1f - claro;

                float suelo = 1f - roca;
                mapa[az, ax, Hierba] = suelo * (1f - hojarasca);
                mapa[az, ax, Hojarasca] = suelo * hojarasca;
                mapa[az, ax, Roca] = roca;
            }
        });

        return mapa;
    }

    private TerrainLayer[] ObtenerCapas()
    {
        // Las texturas por codigo se crean una sola vez y se reutilizan entre generaciones.
        if (capasGeneradas == null || !capasGeneradas[0] || !capasGeneradas[1] || !capasGeneradas[2])
        {
            capasGeneradas = new[]
            {
                TexturasTerreno.CrearHierba(0),
                TexturasTerreno.CrearHojarasca(0),
                TexturasTerreno.CrearRoca(0)
            };
        }

        return new[]
        {
            capaHierba ? capaHierba : capasGeneradas[0],
            capaHojarasca ? capaHojarasca : capasGeneradas[1],
            capaRoca ? capaRoca : capasGeneradas[2]
        };
    }

    // ================================================================ Aplicar

    private TerrainData CrearDatos(float[,,] mapaTexturas)
    {
        // Siempre un TerrainData nuevo en memoria: asi no se modifica el asset del proyecto.
        var datos = new TerrainData
        {
            name = "TerrenoGenerado",
            heightmapResolution = resolucionAltura,
            alphamapResolution = resolucionTexturas,
            baseMapResolution = 1024
        };
        datos.size = new Vector3(tamano, alturaMaxima, tamano);
        datos.SetHeights(0, 0, alturas);
        datos.terrainLayers = ObtenerCapas();
        datos.SetAlphamaps(0, 0, mapaTexturas);
        return datos;
    }

    private Terrain AplicarAlTerreno(TerrainData datos)
    {
        if (!terreno)
        {
            GameObject go = Terrain.CreateTerrainGameObject(datos);
            go.name = "Terreno";
            go.transform.position = transform.position;
            terreno = go.GetComponent<Terrain>();
        }
        else
        {
            terreno.terrainData = datos;
            var colisionador = terreno.GetComponent<TerrainCollider>();
            if (!colisionador)
                colisionador = terreno.gameObject.AddComponent<TerrainCollider>();
            colisionador.terrainData = datos;
        }

        ConfigurarTerreno(terreno);
        return terreno;
    }

    private void ConfigurarTerreno(Terrain t)
    {
        if (materialTerreno)
            t.materialTemplate = materialTerreno;
        else if (!t.materialTemplate)
            t.materialTemplate = MaterialPorDefecto();

        t.heightmapPixelError = 5f;
        t.basemapDistance = 200f;
        t.drawInstanced = true;
        t.Flush();
    }

    private static Material MaterialPorDefecto()
    {
        var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        if (pipeline && pipeline.defaultTerrainMaterial)
            return pipeline.defaultTerrainMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
        return shader ? new Material(shader) : null;
    }

    private void CalcularResultados()
    {
        Vector3 origen = terrenoActivo.transform.position;
        PuntoInicio = EnSuelo(origen + new Vector3(inicioNorm.x * tamano, 0f, inicioNorm.y * tamano));
        PosicionCasa = EnSuelo(origen + new Vector3(casaNorm.x * tamano, 0f, casaNorm.y * tamano));
    }

    private void ColocarJugador()
    {
        if (!jugador)
            return;

        Quaternion rotacion = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

        var controlador = jugador.GetComponent<CharacterController>();
        if (controlador) controlador.enabled = false;

        jugador.SetPositionAndRotation(PuntoInicio + Vector3.up * 1.2f, rotacion);

        if (controlador) controlador.enabled = true;
        Physics.SyncTransforms();
    }

    // ================================================================ Preview en el editor

#if UNITY_EDITOR
    public const string NombrePreview = "__PreviewTerreno";

    [NonSerialized] private Terrain preview;

    public bool HayPreview => preview;

    /// <summary>Genera un terreno temporal (no se guarda en la escena) para verlo sin dar a Play.</summary>
    public void GenerarPreview(bool cambiarSemilla)
    {
        if (Application.isPlaying)
            return;

        if (cambiarSemilla)
            NuevaSemilla();

        Construir(true);
    }

    public void BorrarPreview()
    {
        if (preview)
        {
            DestroyImmediate(preview.terrainData);
            DestroyImmediate(preview.gameObject);
        }

        preview = null;
        Generado = false;

        if (terreno)
            UnityEditor.SceneVisibilityManager.instance.Show(terreno.gameObject, false);
    }

    private Terrain AplicarAPreview(TerrainData datos)
    {
        datos.hideFlags = HideFlags.DontSave;

        if (!preview)
        {
            GameObject go = Terrain.CreateTerrainGameObject(datos);
            go.name = NombrePreview;
            go.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
            preview = go.GetComponent<Terrain>();
        }
        else
        {
            TerrainData anterior = preview.terrainData;
            preview.terrainData = datos;
            preview.GetComponent<TerrainCollider>().terrainData = datos;
            if (anterior) DestroyImmediate(anterior);
        }

        preview.transform.position = terreno ? terreno.transform.position : transform.position;
        ConfigurarTerreno(preview);

        // Ocultar el terreno real en la vista Scene mientras se ve la preview.
        if (terreno)
            UnityEditor.SceneVisibilityManager.instance.Hide(terreno.gameObject, false);

        return preview;
    }
#endif

    // ================================================================ Utilidades

    private static float SuaveEntre(float desde, float hasta, float x)
    {
        float t = Mathf.Clamp01((x - desde) / (hasta - desde));
        return t * t * (3f - 2f * t);
    }

    private static float Muestrear(float[] m, int res, float u, float v)
    {
        float fx = Mathf.Clamp01(u) * (res - 1), fz = Mathf.Clamp01(v) * (res - 1);
        int x0 = Mathf.Min((int)fx, res - 2), z0 = Mathf.Min((int)fz, res - 2);
        float tx = fx - x0, tz = fz - z0;
        float a = Mathf.Lerp(m[z0 * res + x0], m[z0 * res + x0 + 1], tx);
        float b = Mathf.Lerp(m[(z0 + 1) * res + x0], m[(z0 + 1) * res + x0 + 1], tx);
        return Mathf.Lerp(a, b, tz);
    }

    // ================================================================ Gizmos

    void OnDrawGizmosSelected()
    {
        if (!Generado || !terrenoActivo) return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(PuntoInicio, radioClaroInicio);
        Gizmos.DrawLine(PuntoInicio, PuntoInicio + Vector3.up * 30f);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(PosicionCasa, radioClaroCasa);
        Gizmos.DrawLine(PosicionCasa, PosicionCasa + Vector3.up * 30f);
        Gizmos.DrawRay(PosicionCasa + Vector3.up * 2f, RotacionCasa * Vector3.forward * 8f);
    }
}
