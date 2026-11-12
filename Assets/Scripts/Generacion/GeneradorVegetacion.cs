using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Pone hierba y hojas caidas en el terreno usando los "detalles" del propio Terrain
/// (DetailPrototypes): instancing, viento, culling y distancia de dibujado los gestiona Unity.
///
/// - Hierba: texturas 2D con transparencia (Unity las dibuja como planos que se mueven con el viento).
/// - Detalles de malla: prefabs planos (hojas caidas) o pequenos modelos 3D (ramitas, setas...).
/// - Ademas oscurece el suelo con la textura de hojarasca alrededor de cada arbol.
///
/// Ponlo en el objeto del GeneradorTerreno o en un hijo: se ejecuta despues de los arboles, con la misma semilla.
/// Las texturas y prefabs por defecto se crean con el menu Herramientas > Vegetacion.
/// </summary>
public class GeneradorVegetacion : GeneradorSobreTerreno
{
    public enum Suelo
    {
        [Tooltip("Sale donde el terreno tiene textura de hierba.")]
        Hierba,
        [Tooltip("Sale donde el terreno tiene hojarasca (bajo los arboles).")]
        Hojarasca,
        [Tooltip("Sale en cualquier suelo (menos roca).")]
        Cualquiera
    }

    [Serializable]
    public class TipoHierba
    {
        public string nombre = "Hierba";
        public bool activo = true;
        [Tooltip("Textura con transparencia. La base de las briznas debe tocar el borde de abajo.")]
        public Texture2D textura;
        [Tooltip("Ancho minimo y maximo (m).")]
        public Vector2 ancho = new Vector2(0.6f, 1f);
        [Tooltip("Alto minimo y maximo (m).")]
        public Vector2 alto = new Vector2(0.4f, 0.8f);
        [Tooltip("Tinte de las matas sanas (blanco = color original de la textura).")]
        public Color colorSano = Color.white;
        [Tooltip("Tinte de las matas secas. Se mezcla con el sano por manchas.")]
        public Color colorSeco = new Color(0.95f, 0.85f, 0.6f);
        [Tooltip("Mirar siempre a la camara. Parece mas densa, pero gira al mover la vista.")]
        public bool billboard;
        [Tooltip("En que textura del terreno sale.")]
        public Suelo suelo = Suelo.Hierba;
        [Range(0f, 16f), Tooltip("Matas por m2 en las zonas mas densas.")]
        public float densidad = 3f;
        [Range(0f, 1f), Tooltip("Que parte del suelo cubre (en manchas). 1 = todo.")]
        public float cobertura = 0.7f;
        [Tooltip("Tamano (m) de las manchas.")]
        public float escalaManchas = 30f;
    }

    [Serializable]
    public class TipoDetalle
    {
        public string nombre = "Hojas";
        public bool activo = true;
        [Tooltip("Prefab con MeshFilter y MeshRenderer en la raiz. Su material debe tener 'Enable GPU Instancing'.")]
        public GameObject modelo;
        [Tooltip("Escala horizontal (X y Z) minima y maxima.")]
        public Vector2 ancho = new Vector2(0.6f, 1.1f);
        [Tooltip("Escala vertical (Y) minima y maxima.")]
        public Vector2 alto = new Vector2(0.6f, 1.4f);
        [Range(0f, 1f), Tooltip("1 = se inclina con la pendiente del suelo (lo normal para hojas planas).")]
        public float alinearAlSuelo = 1f;
        [Tooltip("En que textura del terreno sale.")]
        public Suelo suelo = Suelo.Hojarasca;
        [Range(0f, 16f), Tooltip("Instancias por m2 en las zonas mas densas.")]
        public float densidad = 0.5f;
        [Range(0f, 1f), Tooltip("Que parte del suelo cubre (en manchas). 1 = todo.")]
        public float cobertura = 0.9f;
        [Tooltip("Tamano (m) de las manchas.")]
        public float escalaManchas = 15f;
    }

    [Header("Hierba (texturas 2D)")]
    [SerializeField] private List<TipoHierba> hierbas = new List<TipoHierba>();

    [Header("Hojas caidas y otros detalles (mallas)")]
    [SerializeField] private List<TipoDetalle> detalles = new List<TipoDetalle>();

    [Header("Reparto")]
    [Tooltip("Celdas de detalle por lado del terreno. Mas = reparto mas fino (y mas memoria).")]
    [SerializeField, Range(256, 2048)] private int resolucion = 1024;
    [Tooltip("No sale vegetacion con mas pendiente que esta (grados).")]
    [SerializeField, Range(0f, 90f)] private float pendienteMaxima = 35f;
    [Tooltip("Radio (m) alrededor del centro de la casa sin vegetacion (para que no atraviese el suelo).")]
    [SerializeField, Min(0f)] private float radioLibreCasa = 9f;

    [Header("Hojarasca bajo los arboles")]
    [Tooltip("Pinta la textura de hojarasca del terreno alrededor de cada arbol.")]
    [SerializeField] private bool hojarascaBajoArboles = true;
    [Tooltip("Radio (m) de la mancha de hojarasca (se multiplica por la escala del arbol).")]
    [SerializeField, Min(0f)] private float radioHojarasca = 4.5f;
    [SerializeField, Range(0f, 1f)] private float intensidadHojarasca = 0.85f;

    [Header("Viento")]
    [SerializeField, Range(0f, 1f)] private float fuerzaViento = 0.35f;
    [SerializeField, Range(0f, 1f)] private float cantidadViento = 0.4f;
    [SerializeField, Range(0f, 1f)] private float velocidadViento = 0.35f;
    [SerializeField] private Color tinteViento = new Color(0.75f, 0.8f, 0.65f);

    [Header("Render")]
    [Tooltip("Distancia maxima (m) a la que se dibuja la vegetacion (max 250).")]
    [SerializeField, Range(10f, 250f)] private float distanciaDibujado = 80f;
    [Tooltip("Multiplicador global de densidad (se puede bajar en los ajustes graficos).")]
    [SerializeField, Range(0f, 1f)] private float densidadGlobal = 1f;

    // ---------------------------------------------------------------- Resultados

    public int InstanciasHierba { get; private set; }
    public int InstanciasDetalles { get; private set; }

    public List<TipoHierba> Hierbas => hierbas;
    public List<TipoDetalle> Detalles => detalles;

    public override int Orden => 10; // despues de los arboles

    // Alphamap original del terreno, para no acumular hojarasca al regenerar solo la decoracion.
    [NonSerialized] private TerrainData datosBase;
    [NonSerialized] private float[,,] alphamapBase;

    // ================================================================ Generacion

    public override void Generar(GeneradorTerreno generador, int semillaTerreno)
    {
        Terrain terreno = generador.Terreno;
        TerrainData datos = terreno.terrainData;
        int semilla = MezclarSemilla(semillaTerreno, 0x56454745);
        var ruido = new Ruido(semilla);

        float[,,] mapa = PrepararAlphamap(datos);
        if (hojarascaBajoArboles && mapa != null)
        {
            PintarHojarasca(datos, mapa, ruido);
            datos.SetAlphamaps(0, 0, mapa);
        }

        ColocarDetalles(generador, datos, mapa, ruido, semilla);

        datos.wavingGrassStrength = fuerzaViento;
        datos.wavingGrassAmount = cantidadViento;
        datos.wavingGrassSpeed = velocidadViento;
        datos.wavingGrassTint = tinteViento;

        terreno.detailObjectDistance = distanciaDibujado;
        // El shader Ecos del Bosque/Vegetacion encoge las matas al acercarse a esta distancia (sin cortes bruscos).
        Shader.SetGlobalFloat("_EcosVegetacionDistancia", distanciaDibujado);
        terreno.detailObjectDensity = densidadGlobal;
        terreno.drawTreesAndFoliage = true;
        terreno.Flush();
    }

    // ================================================================ Hojarasca

    private float[,,] PrepararAlphamap(TerrainData datos)
    {
        if (datos.alphamapLayers <= GeneradorTerreno.Hojarasca)
            return null;

        if (datosBase != datos || alphamapBase == null)
        {
            alphamapBase = datos.GetAlphamaps(0, 0, datos.alphamapWidth, datos.alphamapHeight);
            datosBase = datos;
        }

        return (float[,,])alphamapBase.Clone();
    }

    private void PintarHojarasca(TerrainData datos, float[,,] mapa, Ruido ruido)
    {
        int ar = datos.alphamapResolution;
        int capas = datos.alphamapLayers;
        float celda = datos.size.x / ar;
        var extra = new float[ar * ar];

        foreach (TreeInstance arbol in datos.treeInstances)
        {
            float cx = arbol.position.x * ar, cz = arbol.position.z * ar;
            float variacion = 0.75f + 0.5f * ruido.Fbm(cx * 0.37f + 900f, cz * 0.37f + 900f, 1);
            float r = radioHojarasca * Mathf.Max(0.5f, arbol.widthScale) * variacion / celda;
            if (r <= 0.5f)
                continue;

            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - r)), x1 = Mathf.Min(ar - 1, Mathf.CeilToInt(cx + r));
            int z0 = Mathf.Max(0, Mathf.FloorToInt(cz - r)), z1 = Mathf.Min(ar - 1, Mathf.CeilToInt(cz + r));
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (z + 0.5f - cz) * (z + 0.5f - cz));
                    float peso = SuaveEntre(r, r * 0.35f, d);
                    int i = z * ar + x;
                    if (peso > extra[i]) extra[i] = peso;
                }
        }

        int hoja = GeneradorTerreno.Hojarasca;
        int roca = capas > GeneradorTerreno.Roca ? GeneradorTerreno.Roca : -1;

        Parallel.For(0, ar, z =>
        {
            for (int x = 0; x < ar; x++)
            {
                float e = extra[z * ar + x];
                if (e <= 0f)
                    continue;

                // Borde irregular de las manchas.
                float wx = (x + 0.5f) * celda, wz = (z + 0.5f) * celda;
                e = Mathf.Clamp01(e * (0.7f + 0.6f * ruido.Fbm(wx / 5f + 300f, wz / 5f + 300f, 2))) * intensidadHojarasca;

                float suelo = 1f - (roca >= 0 ? mapa[z, x, roca] : 0f);
                float antes = mapa[z, x, hoja];
                float despues = Mathf.Lerp(antes, suelo, e);
                float restoAntes = suelo - antes;
                float factor = restoAntes > 0.0001f ? (suelo - despues) / restoAntes : 0f;

                for (int c = 0; c < capas; c++)
                    if (c != hoja && c != roca)
                        mapa[z, x, c] *= factor;
                mapa[z, x, hoja] = despues;
            }
        });
    }

    // ================================================================ Detalles

    private void ColocarDetalles(GeneradorTerreno generador, TerrainData datos, float[,,] mapa, Ruido ruido, int semilla)
    {
        InstanciasHierba = 0;
        InstanciasDetalles = 0;

        // Prototipos validos.
        var prototipos = new List<DetailPrototype>();
        var reglas = new List<Regla>();

        foreach (TipoHierba h in hierbas)
        {
            if (h == null || !h.activo || !h.textura || h.densidad <= 0f)
                continue;

            var p = new DetailPrototype
            {
                prototypeTexture = h.textura,
                usePrototypeMesh = false,
                useInstancing = false,
                renderMode = h.billboard ? DetailRenderMode.GrassBillboard : DetailRenderMode.Grass,
                minWidth = h.ancho.x, maxWidth = h.ancho.y,
                minHeight = h.alto.x, maxHeight = h.alto.y,
                healthyColor = h.colorSano,
                dryColor = h.colorSeco,
                noiseSpread = 0.15f,
                noiseSeed = semilla + prototipos.Count,
                useDensityScaling = true
            };
            if (Agregar(p, h.nombre, prototipos))
                reglas.Add(new Regla(h.suelo, h.densidad, h.cobertura, h.escalaManchas, true));
        }

        foreach (TipoDetalle d in detalles)
        {
            if (d == null || !d.activo || !d.modelo || d.densidad <= 0f)
                continue;

            var p = new DetailPrototype
            {
                prototype = d.modelo,
                usePrototypeMesh = true,
                useInstancing = true,
                renderMode = DetailRenderMode.VertexLit,
                minWidth = d.ancho.x, maxWidth = d.ancho.y,
                minHeight = d.alto.x, maxHeight = d.alto.y,
                alignToGround = d.alinearAlSuelo,
                noiseSeed = semilla + prototipos.Count,
                useDensityScaling = true
            };
            if (Agregar(p, d.nombre, prototipos))
                reglas.Add(new Regla(d.suelo, d.densidad, d.cobertura, d.escalaManchas, false));
        }

        int res = Mathf.Clamp(resolucion, 64, 4096);
        datos.SetDetailResolution(res, 32);
        datos.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
        datos.detailPrototypes = prototipos.ToArray();

        if (prototipos.Count == 0)
            return;

        float tamano = datos.size.x;
        float celda = tamano / res;
        float areaCelda = celda * celda;
        float[] apto = CalcularZonasAptas(generador, datos, res);

        // Peso de cada textura del suelo en cada celda.
        int ar = mapa != null ? mapa.GetLength(0) : 0;
        int capas = mapa != null ? mapa.GetLength(2) : 0;
        float PesoSuelo(Suelo s, int x, int z)
        {
            if (s == Suelo.Cualquiera || mapa == null)
                return 1f;
            int capa = s == Suelo.Hierba ? GeneradorTerreno.Hierba : GeneradorTerreno.Hojarasca;
            if (capa >= capas)
                return 1f;
            int ax = Mathf.Min(ar - 1, x * ar / res), az = Mathf.Min(ar - 1, z * ar / res);
            return mapa[az, ax, capa];
        }

        for (int i = 0; i < reglas.Count; i++)
        {
            Regla regla = reglas[i];
            var capa = new int[res, res];
            float umbral = Mathf.Lerp(0.78f, 0.22f, regla.cobertura);
            float desfase = 1000f + i * 97.31f;
            int sal = semilla ^ (i * 7919);
            long total = 0;

            Parallel.For(0, res, () => 0L, (z, _, subtotal) =>
            {
                for (int x = 0; x < res; x++)
                {
                    float f = apto[z * res + x];
                    if (f <= 0f)
                        continue;

                    f *= PesoSuelo(regla.suelo, x, z);
                    if (f <= 0.02f)
                        continue;

                    if (regla.cobertura < 0.999f)
                    {
                        float wx = (x + 0.5f) * celda, wz = (z + 0.5f) * celda;
                        float n = ruido.Fbm(wx / regla.escala + desfase, wz / regla.escala - desfase, 3);
                        f *= SuaveEntre(umbral - 0.1f, umbral + 0.1f, n);
                        if (f <= 0f)
                            continue;
                    }

                    // Redondeo aleatorio: asi densidades bajas tambien reparten algo.
                    float cantidad = regla.densidad * areaCelda * f;
                    int n0 = (int)cantidad;
                    if (Hash(x, z, sal) < cantidad - n0)
                        n0++;

                    n0 = Mathf.Min(n0, 255);
                    capa[z, x] = n0;
                    subtotal += n0;
                }
                return subtotal;
            }, subtotal => System.Threading.Interlocked.Add(ref total, subtotal));

            datos.SetDetailLayer(0, 0, i, capa);
            if (regla.esHierba) InstanciasHierba += (int)total;
            else InstanciasDetalles += (int)total;
        }
    }

    private bool Agregar(DetailPrototype p, string nombre, List<DetailPrototype> lista)
    {
        if (!p.Validate(out string error))
        {
            Debug.LogWarning($"GeneradorVegetacion: '{nombre}' no es valido y se ignora: {error}", this);
            return false;
        }
        lista.Add(p);
        return true;
    }

    /// <summary>0..1 por celda: encima de la meseta, sin mucha pendiente y fuera de la casa.</summary>
    private float[] CalcularZonasAptas(GeneradorTerreno generador, TerrainData datos, int res)
    {
        var apto = new float[res * res];
        Vector3 origen = generador.Terreno.transform.position;
        float tamano = datos.size.x;
        float celda = tamano / res;
        Vector2 casa = new Vector2(generador.PosicionCasa.x - origen.x, generador.PosicionCasa.z - origen.z);

        // Meseta (usa la API del generador, solo hilo principal).
        for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                var p = origen + new Vector3((x + 0.5f) * celda, 0f, (z + 0.5f) * celda);
                apto[z * res + x] = generador.EnMeseta(p) ? 1f : 0f;
            }

        // Pendiente calculada del mapa de alturas (mas rapido que preguntar celda a celda).
        int hr = datos.heightmapResolution;
        float[,] alturas = datos.GetHeights(0, 0, hr, hr);
        float alto = datos.size.y;
        float celdaAltura = tamano / (hr - 1);

        Parallel.For(0, res, z =>
        {
            for (int x = 0; x < res; x++)
            {
                int i = z * res + x;
                if (apto[i] <= 0f)
                    continue;

                float wx = (x + 0.5f) * celda, wz = (z + 0.5f) * celda;
                int hx = Mathf.Clamp(Mathf.RoundToInt(wx / celdaAltura), 1, hr - 2);
                int hz = Mathf.Clamp(Mathf.RoundToInt(wz / celdaAltura), 1, hr - 2);
                float ddx = (alturas[hz, hx + 1] - alturas[hz, hx - 1]) * alto / (2f * celdaAltura);
                float ddz = (alturas[hz + 1, hx] - alturas[hz - 1, hx]) * alto / (2f * celdaAltura);
                float pendiente = Mathf.Atan(Mathf.Sqrt(ddx * ddx + ddz * ddz)) * Mathf.Rad2Deg;

                float f = SuaveEntre(pendienteMaxima, pendienteMaxima - 8f, pendiente);
                if (radioLibreCasa > 0f)
                    f *= SuaveEntre(radioLibreCasa, radioLibreCasa + 2f, Vector2.Distance(new Vector2(wx, wz), casa));
                apto[i] = f;
            }
        });

        return apto;
    }

    private readonly struct Regla
    {
        public readonly Suelo suelo;
        public readonly float densidad, cobertura, escala;
        public readonly bool esHierba;

        public Regla(Suelo suelo, float densidad, float cobertura, float escala, bool esHierba)
        {
            this.suelo = suelo;
            this.densidad = densidad;
            this.cobertura = cobertura;
            this.escala = Mathf.Max(0.5f, escala);
            this.esHierba = esHierba;
        }
    }

    // ================================================================ Utilidades

    private static float Hash(int x, int y, int semilla)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + semilla * 144665);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }

    private static float SuaveEntre(float desde, float hasta, float x)
    {
        float t = Mathf.Clamp01((x - desde) / (hasta - desde));
        return t * t * (3f - 2f * t);
    }
}
