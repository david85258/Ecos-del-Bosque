using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Crea por codigo los recursos de vegetacion (texturas PNG de hierba y hojas, materiales, mallas y prefabs)
/// y los asigna al GeneradorVegetacion de la escena.
///
/// La hierba son matas "en estrella" (3 planos cruzados) con el shader Ecos del Bosque/Vegetacion:
/// se ven llenas desde cualquier angulo, se mueven con el viento y reciben la luz como el suelo.
///
/// Menu: Herramientas > Vegetacion. Se puede volver a ejecutar: sobrescribe los archivos manteniendo
/// sus GUID, asi que las referencias de la escena no se pierden.
/// </summary>
public static class RecursosVegetacion
{
    private const string CarpetaTexturas = "Assets/Texturas/Vegetacion";
    private const string CarpetaMateriales = "Assets/Materiales/Vegetacion";
    private const string CarpetaModelos = "Assets/Modelos/Vegetacion";
    private const string CarpetaPrefabs = "Assets/Prefabs/Vegetacion";
    private const string NombreShader = "Ecos del Bosque/Vegetacion";

    private const string RutaHierbaAlta = CarpetaTexturas + "/Hierba_Alta.png";
    private const string RutaHierbaCorta = CarpetaTexturas + "/Hierba_Corta.png";
    private const string RutaHierbaFlores = CarpetaTexturas + "/Hierba_Flores.png";
    private const string RutaHojas = CarpetaTexturas + "/Hojas_Caidas.png";
    private const string RutaMallasHojas = CarpetaModelos + "/Hojas_Caidas_Mallas.asset";
    private const string RutaMallaHierba = CarpetaModelos + "/Mata_Hierba.asset";

    private static readonly string[] Variantes = { "A", "B", "C", "D" };
    private static readonly string[] Hierbas = { "Hierba_Corta", "Hierba_Alta", "Hierba_Flores" };

    // ================================================================ Menu

    [MenuItem("Herramientas/Vegetacion/Generar texturas y prefabs")]
    public static void GenerarRecursos()
    {
        foreach (var c in new[] { CarpetaTexturas, CarpetaMateriales, CarpetaModelos, CarpetaPrefabs })
            CrearCarpeta(c);

        GuardarPng(RutaHierbaAlta, PintarHierba(512, 512, 11, 70, 0.55f, 0.97f, 0.035f, 0.18f, 0f));
        GuardarPng(RutaHierbaCorta, PintarHierba(512, 512, 23, 110, 0.35f, 0.85f, 0.045f, 0.1f, 0f));
        GuardarPng(RutaHierbaFlores, PintarHierba(512, 512, 37, 80, 0.35f, 0.8f, 0.04f, 0.12f, 0.12f));
        GuardarPng(RutaHojas, PintarHojas(1024, 51));

        ConfigurarImportacion(RutaHierbaAlta, 512);
        ConfigurarImportacion(RutaHierbaCorta, 512);
        ConfigurarImportacion(RutaHierbaFlores, 512);
        ConfigurarImportacion(RutaHojas, 1024);

        // Hierba: una malla en estrella compartida y un material por textura.
        Mesh mata = CrearMallaMata();
        foreach (string h in Hierbas)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{CarpetaTexturas}/{h}.png");
            Material mat = CrearMaterial($"{CarpetaMateriales}/{h}.mat", tex, viento: 0.12f, oscurecerBase: 0.45f);
            CrearPrefab($"{CarpetaPrefabs}/{h}.prefab", mata, mat, sombras: true);
        }

        // Hojas: planos en el suelo, sin viento.
        Material hojas = CrearMaterial($"{CarpetaMateriales}/Hojas_Caidas.mat",
                                       AssetDatabase.LoadAssetAtPath<Texture2D>(RutaHojas), viento: 0f, oscurecerBase: 0f);
        hojas.SetFloat("_CantidadVariacion", 0.2f);
        Mesh[] mallas = CrearMallasHojas();
        for (int i = 0; i < mallas.Length; i++)
            CrearPrefab($"{CarpetaPrefabs}/Hojas_Caidas_{Variantes[i]}.prefab", mallas[i], hojas, sombras: false);

        AssetDatabase.SaveAssets();
        Debug.Log("Vegetacion: texturas, materiales y prefabs creados en " + CarpetaTexturas + " y " + CarpetaPrefabs);
    }

    [MenuItem("Herramientas/Vegetacion/Configurar en la escena")]
    public static void ConfigurarEscena()
    {
        var terreno = Object.FindAnyObjectByType<GeneradorTerreno>(FindObjectsInactive.Include);
        if (!terreno)
        {
            EditorUtility.DisplayDialog("Vegetacion", "No hay ningun GeneradorTerreno en la escena abierta.", "Vale");
            return;
        }

        if (!AssetDatabase.LoadAssetAtPath<GameObject>($"{CarpetaPrefabs}/Hierba_Corta.prefab"))
            GenerarRecursos();

        var veg = terreno.GetComponentInChildren<GeneradorVegetacion>(true);
        if (!veg)
            veg = Undo.AddComponent<GeneradorVegetacion>(terreno.gameObject);

        Undo.RecordObject(veg, "Configurar vegetacion");

        GameObject Prefab(string nombre) => AssetDatabase.LoadAssetAtPath<GameObject>($"{CarpetaPrefabs}/{nombre}.prefab");

        // Las texturas sueltas (planos de Unity) ya no se usan: todo son mallas con el shader propio.
        veg.Hierbas.Clear();
        veg.Detalles.Clear();

        veg.Detalles.Add(new GeneradorVegetacion.TipoDetalle
        {
            nombre = "Hierba corta (alfombra)", modelo = Prefab("Hierba_Corta"),
            ancho = new Vector2(0.45f, 0.7f), alto = new Vector2(0.28f, 0.45f), alinearAlSuelo = 0.25f,
            densidad = 8f, cobertura = 1f, escalaManchas = 20f, suelo = GeneradorVegetacion.Suelo.Hierba
        });
        veg.Detalles.Add(new GeneradorVegetacion.TipoDetalle
        {
            nombre = "Hierba alta", modelo = Prefab("Hierba_Alta"),
            ancho = new Vector2(0.55f, 0.85f), alto = new Vector2(0.5f, 0.85f), alinearAlSuelo = 0.15f,
            densidad = 2.5f, cobertura = 0.6f, escalaManchas = 25f, suelo = GeneradorVegetacion.Suelo.Hierba
        });
        veg.Detalles.Add(new GeneradorVegetacion.TipoDetalle
        {
            nombre = "Hierba con flores", modelo = Prefab("Hierba_Flores"),
            ancho = new Vector2(0.4f, 0.6f), alto = new Vector2(0.35f, 0.55f), alinearAlSuelo = 0.2f,
            densidad = 1f, cobertura = 0.35f, escalaManchas = 20f, suelo = GeneradorVegetacion.Suelo.Hierba
        });
        veg.Detalles.Add(new GeneradorVegetacion.TipoDetalle
        {
            nombre = "Hierba bajo los arboles", modelo = Prefab("Hierba_Corta"),
            ancho = new Vector2(0.35f, 0.55f), alto = new Vector2(0.2f, 0.35f), alinearAlSuelo = 0.25f,
            densidad = 2.5f, cobertura = 0.6f, escalaManchas = 15f, suelo = GeneradorVegetacion.Suelo.Hojarasca
        });

        foreach (string v in Variantes)
        {
            veg.Detalles.Add(new GeneradorVegetacion.TipoDetalle
            {
                nombre = "Hojas caidas " + v, modelo = Prefab("Hojas_Caidas_" + v),
                ancho = new Vector2(0.55f, 1.05f), alto = new Vector2(0.6f, 1.4f), alinearAlSuelo = 1f,
                densidad = 0.6f, cobertura = 0.9f, escalaManchas = 15f, suelo = GeneradorVegetacion.Suelo.Hojarasca
            });
        }

        var so = new SerializedObject(veg);
        so.FindProperty("distanciaDibujado").floatValue = 60f;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(veg);
        EditorSceneManager.MarkSceneDirty(veg.gameObject.scene);
        Selection.activeObject = veg;

        if (terreno.HayPreview)
            terreno.RegenerarDecoracion();

        Debug.Log("Vegetacion configurada en " + veg.gameObject.name, veg);
    }

    // ================================================================ Lienzo

    /// <summary>Imagen RGBA en floats con mezcla "over" (color recto, sin premultiplicar).</summary>
    private class Lienzo
    {
        public readonly int ancho, alto;
        public readonly Color[] px;

        public Lienzo(int ancho, int alto, Color fondo)
        {
            this.ancho = ancho;
            this.alto = alto;
            px = new Color[ancho * alto];
            fondo.a = 0f;
            for (int i = 0; i < px.Length; i++) px[i] = fondo;
        }

        public void Mezclar(int x, int y, Color c, float cobertura)
        {
            if (x < 0 || y < 0 || x >= ancho || y >= alto || cobertura <= 0f)
                return;

            int i = y * ancho + x;
            Color d = px[i];
            float a = Mathf.Clamp01(cobertura) * c.a;
            float aOut = a + d.a * (1f - a);
            if (aOut <= 0f) return;

            Color r = (c * a + d * (d.a * (1f - a))) / aOut;
            r.a = aOut;
            px[i] = r;
        }

        /// <summary>Circulo con borde suavizado.</summary>
        public void Disco(float cx, float cy, float radio, Color c)
        {
            int x0 = Mathf.FloorToInt(cx - radio - 1), x1 = Mathf.CeilToInt(cx + radio + 1);
            int y0 = Mathf.FloorToInt(cy - radio - 1), y1 = Mathf.CeilToInt(cy + radio + 1);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    Mezclar(x, y, c, radio - d + 0.5f);
                }
        }
    }

    // ================================================================ Hierba

    /// <param name="altMin">Altura minima de las briznas (0..1 del alto de la imagen).</param>
    /// <param name="anchoBase">Ancho de la base de cada brizna (0..1 del ancho).</param>
    /// <param name="secas">Proporcion de briznas secas/amarillentas.</param>
    /// <param name="flores">Proporcion de briznas que acaban en flor.</param>
    private static Texture2D PintarHierba(int w, int h, int semilla, int briznas, float altMin, float altMax,
                                          float anchoBase, float secas, float flores)
    {
        var rng = new System.Random(semilla);
        var lienzo = new Lienzo(w, h, new Color(0.25f, 0.35f, 0.12f));
        var flor = new List<(float x, float y, Color c, float r)>();

        // Primero las de atras (mas oscuras), luego las de delante.
        for (int b = 0; b < briznas; b++)
        {
            float profundidad = b / (float)briznas;                 // 0 = fondo, 1 = delante
            float centro = Gauss(rng) * 0.2f;                       // mata: mas briznas en el centro
            float x0 = Mathf.Clamp(0.5f + centro, 0.08f, 0.92f) * w;
            float altura = Mathf.Lerp(altMin, altMax, Mathf.Pow(R(rng), 0.7f)) * h;
            if (Mathf.Abs(centro) > 0.2f) altura *= 0.8f;          // los bordes mas bajos
            float inclinacion = (centro * 1.6f + (R(rng) - 0.5f) * 0.5f) * altura;
            // Que la punta no se salga de la imagen (se veria cortada).
            inclinacion = Mathf.Clamp(x0 + inclinacion, w * 0.04f, w * 0.96f) - x0;
            float grosor = anchoBase * w * Mathf.Lerp(0.6f, 1.2f, R(rng));
            bool esFlor = R(rng) < flores;
            if (esFlor) grosor *= 0.45f;

            float luz = Mathf.Lerp(0.55f, 1.05f, profundidad) * Mathf.Lerp(0.85f, 1.1f, R(rng));
            Color baseC, puntaC;
            if (R(rng) < secas)
            {
                baseC = new Color(0.30f, 0.30f, 0.10f);
                puntaC = new Color(0.72f, 0.66f, 0.34f);
            }
            else
            {
                float tono = R(rng);
                baseC = Color.Lerp(new Color(0.10f, 0.20f, 0.05f), new Color(0.14f, 0.24f, 0.06f), tono);
                puntaC = Color.Lerp(new Color(0.38f, 0.55f, 0.16f), new Color(0.52f, 0.62f, 0.20f), tono);
            }

            // Curva cuadratica: base -> control -> punta.
            Vector2 p0 = new Vector2(x0, -2f);
            Vector2 p1 = new Vector2(x0 + inclinacion * 0.25f, altura * 0.55f);
            Vector2 p2 = new Vector2(x0 + inclinacion, altura);
            int pasos = Mathf.CeilToInt(altura * 1.5f);

            for (int s = 0; s <= pasos; s++)
            {
                float t = s / (float)pasos;
                Vector2 p = (1 - t) * (1 - t) * p0 + 2 * (1 - t) * t * p1 + t * t * p2;
                float r = grosor * 0.5f * (esFlor ? 1f - t * 0.3f : Mathf.Pow(1f - t, 0.8f));
                if (r < 0.35f) r = 0.35f;
                float sombra = Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(t * 4f)); // base oscura (oclusion)
                Color c = Color.Lerp(baseC, puntaC, Mathf.Pow(t, 0.8f)) * (luz * sombra);
                c.a = 1f;
                lienzo.Disco(p.x, p.y, r, c);
            }

            if (esFlor)
            {
                Color[] colores =
                {
                    new Color(0.95f, 0.95f, 0.90f), new Color(0.98f, 0.85f, 0.25f),
                    new Color(0.70f, 0.55f, 0.90f), new Color(0.95f, 0.60f, 0.70f)
                };
                flor.Add((p2.x, p2.y, colores[rng.Next(colores.Length)], w * Mathf.Lerp(0.018f, 0.028f, R(rng))));
            }
        }

        // Flores encima de todo: 5 petalos + centro.
        foreach (var f in flor)
        {
            float giro = R(rng) * Mathf.PI * 2f;
            for (int p = 0; p < 5; p++)
            {
                float a = giro + p * Mathf.PI * 2f / 5f;
                lienzo.Disco(f.x + Mathf.Cos(a) * f.r, f.y + Mathf.Sin(a) * f.r * 0.8f, f.r * 0.75f, f.c * 0.95f);
            }
            lienzo.Disco(f.x, f.y, f.r * 0.5f, new Color(0.95f, 0.75f, 0.15f));
        }

        return ATextura(lienzo);
    }

    // ================================================================ Hojas caidas

    /// <summary>Atlas 2x2 con cuatro grupos de hojas secas (uno por variante de prefab).</summary>
    private static Texture2D PintarHojas(int tamano, int semilla)
    {
        var rng = new System.Random(semilla);
        var lienzo = new Lienzo(tamano, tamano, new Color(0.40f, 0.25f, 0.11f));
        int mitad = tamano / 2;

        Color[] paleta =
        {
            new Color(0.45f, 0.26f, 0.10f), // marron
            new Color(0.62f, 0.33f, 0.09f), // naranja tostado
            new Color(0.74f, 0.55f, 0.16f), // amarillo ocre
            new Color(0.30f, 0.18f, 0.08f), // marron oscuro
            new Color(0.50f, 0.47f, 0.18f), // verde amarillento
            new Color(0.55f, 0.20f, 0.08f)  // rojizo
        };

        for (int q = 0; q < 4; q++)
        {
            float ox = (q % 2) * mitad + mitad * 0.5f;
            float oy = (q / 2) * mitad + mitad * 0.5f;
            int numero = 7 + rng.Next(6);

            for (int i = 0; i < numero; i++)
            {
                // Dentro de un circulo para no salirse de su cuadrante.
                float ang = R(rng) * Mathf.PI * 2f;
                float dist = Mathf.Sqrt(R(rng)) * mitad * 0.28f;
                float largo = mitad * Mathf.Lerp(0.16f, 0.28f, R(rng));
                Color c = paleta[rng.Next(paleta.Length)] * Mathf.Lerp(0.8f, 1.1f, R(rng));
                int forma = rng.Next(3);
                PintarHoja(lienzo, rng, ox + Mathf.Cos(ang) * dist, oy + Mathf.Sin(ang) * dist,
                           R(rng) * Mathf.PI * 2f, largo, forma, c);
            }
        }

        return ATextura(lienzo);
    }

    /// <param name="forma">0 = ovalada, 1 = lobulada (roble), 2 = alargada.</param>
    private static void PintarHoja(Lienzo lienzo, System.Random rng, float cx, float cy, float angulo,
                                   float largo, int forma, Color color)
    {
        float anchoRel = forma == 2 ? 0.22f : forma == 1 ? 0.42f : 0.36f;
        float semi = largo * 0.5f;
        float cos = Mathf.Cos(angulo), sin = Mathf.Sin(angulo);
        float curva = (R(rng) - 0.5f) * 0.25f;     // hoja algo doblada
        float semilla = R(rng) * 100f;
        int lobulos = 3 + rng.Next(3);
        Color borde = color * 0.65f;
        borde.a = 1f;

        int alcance = Mathf.CeilToInt(largo * 0.7f);
        for (int y = -alcance; y <= alcance; y++)
            for (int x = -alcance; x <= alcance; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                // A coordenadas de la hoja: s a lo largo (-1 base, 1 punta), q a lo ancho.
                float s = (px * cos + py * sin) / semi;
                float q = (-px * sin + py * cos) / semi;
                q -= curva * (1f - s * s);

                float cobertura;
                bool esTallo = false;
                if (s >= -1f && s <= 1f)
                {
                    float t = (s + 1f) * 0.5f;
                    float ancho = anchoRel * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.85f)), 0.8f);
                    if (forma == 1)
                        ancho *= 0.7f + 0.3f * Mathf.Abs(Mathf.Cos(t * Mathf.PI * lobulos));
                    cobertura = (ancho - Mathf.Abs(q)) * semi + 0.5f;
                }
                else if (s < -1f && s > -1.22f)
                {
                    cobertura = (0.025f - Mathf.Abs(q)) * semi + 0.5f;
                    esTallo = true;
                }
                else continue;

                if (cobertura <= 0f) continue;

                Color c = color;
                if (!esTallo)
                {
                    // Nervio central, nervios laterales, lado en sombra y manchas.
                    float nervio = Mathf.Clamp01(1f - Mathf.Abs(q) * semi / 1.2f);
                    float laterales = Mathf.Clamp01(1f - Mathf.Abs(Mathf.Repeat((s - Mathf.Abs(q) * 1.2f) * 5f, 1f) - 0.5f) * 8f)
                                      * Mathf.Clamp01(1f - Mathf.Abs(q) / anchoRel);
                    float manchas = Mathf.PerlinNoise(px * 0.08f + semilla, py * 0.08f - semilla);
                    c *= 1f + nervio * 0.25f + laterales * 0.08f;
                    c *= q > 0f ? 0.88f : 1.04f;
                    c *= Mathf.Lerp(0.8f, 1.1f, manchas);
                    float distBorde = Mathf.Clamp01(cobertura / (semi * 0.06f));
                    c = Color.Lerp(borde, c, distBorde);
                }
                else c *= 0.7f;

                c.a = 1f;
                lienzo.Mezclar(Mathf.FloorToInt(cx + x), Mathf.FloorToInt(cy + y), c, cobertura);
            }
    }

    // ================================================================ Assets

    private static Material CrearMaterial(string ruta, Texture2D textura, float viento, float oscurecerBase)
    {
        Shader shader = Shader.Find(NombreShader);
        if (!shader)
            throw new System.Exception("No se encuentra el shader " + NombreShader);

        var material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (!material)
        {
            material = new Material(shader) { name = Path.GetFileNameWithoutExtension(ruta) };
            AssetDatabase.CreateAsset(material, ruta);
        }
        else if (material.shader != shader)
            material.shader = shader;

        material.SetTexture("_BaseMap", textura);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Cutoff", 0.5f);
        material.SetFloat("_FuerzaViento", viento);
        material.SetFloat("_OscurecerBase", oscurecerBase);
        material.enableInstancing = true; // obligatorio para los detalles de malla del Terrain
        EditorUtility.SetDirty(material);
        return material;
    }

    /// <summary>Mata "en estrella": 3 planos verticales cruzados a 60 grados, de 1 x 1 m, con la base en el origen.
    /// Dos tramos de altura para que el viento la doble en curva. Normales hacia arriba (las usa el shader).</summary>
    private static Mesh CrearMallaMata()
    {
        Mesh malla = ObtenerMalla(RutaMallaHierba, "Mata_Hierba");
        malla.Clear();

        var v = new List<Vector3>();
        var uv = new List<Vector2>();
        var t = new List<int>();
        const int tramos = 2;

        for (int p = 0; p < 3; p++)
        {
            float ang = p * 60f * Mathf.Deg2Rad;
            Vector3 eje = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 0.5f;
            int inicio = v.Count;

            for (int s = 0; s <= tramos; s++)
            {
                float y = s / (float)tramos;
                v.Add(-eje + Vector3.up * y); uv.Add(new Vector2(0f, y));
                v.Add(eje + Vector3.up * y); uv.Add(new Vector2(1f, y));
            }
            for (int s = 0; s < tramos; s++)
            {
                int a = inicio + s * 2;
                t.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
            }
        }

        malla.SetVertices(v);
        malla.SetUVs(0, uv);
        var normales = new Vector3[v.Count];
        for (int i = 0; i < normales.Length; i++) normales[i] = Vector3.up;
        malla.normals = normales;
        malla.SetTriangles(t, 0);
        malla.RecalculateBounds();
        EditorUtility.SetDirty(malla);
        AssetDatabase.SaveAssets();
        return malla;
    }

    /// <summary>Un plano horizontal por variante, cada uno con un cuadrante del atlas.</summary>
    private static Mesh[] CrearMallasHojas()
    {
        var mallas = new Mesh[Variantes.Length];
        for (int i = 0; i < Variantes.Length; i++)
        {
            Mesh malla = ObtenerMalla(RutaMallasHojas, "Hojas_Caidas_" + Variantes[i]);
            malla.Clear();

            float u0 = (i % 2) * 0.5f, v0 = (i / 2) * 0.5f;
            const float y = 0.03f; // un poco por encima del suelo; la escala Y de cada instancia evita parpadeos
            malla.vertices = new[]
            {
                new Vector3(-0.5f, y, -0.5f), new Vector3(0.5f, y, -0.5f),
                new Vector3(-0.5f, y, 0.5f), new Vector3(0.5f, y, 0.5f)
            };
            malla.uv = new[]
            {
                new Vector2(u0, v0), new Vector2(u0 + 0.5f, v0),
                new Vector2(u0, v0 + 0.5f), new Vector2(u0 + 0.5f, v0 + 0.5f)
            };
            malla.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            malla.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            malla.RecalculateBounds();
            EditorUtility.SetDirty(malla);
            mallas[i] = malla;
        }

        AssetDatabase.SaveAssets();
        return mallas;
    }

    /// <summary>Devuelve la malla con ese nombre dentro del asset (la crea si no existe), conservando su GUID.</summary>
    private static Mesh ObtenerMalla(string ruta, string nombre)
    {
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(ruta))
            if (o is Mesh m && m.name == nombre)
                return m;

        var nueva = new Mesh { name = nombre };
        if (!File.Exists(ruta)) AssetDatabase.CreateAsset(nueva, ruta);
        else AssetDatabase.AddObjectToAsset(nueva, ruta);
        return nueva;
    }

    private static void CrearPrefab(string ruta, Mesh malla, Material material, bool sombras)
    {
        var go = new GameObject(Path.GetFileNameWithoutExtension(ruta));
        go.AddComponent<MeshFilter>().sharedMesh = malla;
        var render = go.AddComponent<MeshRenderer>();
        render.sharedMaterial = material;
        render.shadowCastingMode = sombras ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        render.receiveShadows = true;
        PrefabUtility.SaveAsPrefabAsset(go, ruta);
        Object.DestroyImmediate(go);
    }

    private static void GuardarPng(string ruta, Texture2D textura)
    {
        File.WriteAllBytes(ruta, textura.EncodeToPNG());
        Object.DestroyImmediate(textura);
        AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceUpdate);
    }

    private static void ConfigurarImportacion(string ruta, int maximo)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(ruta);
        imp.textureType = TextureImporterType.Default;
        imp.sRGBTexture = true;
        imp.alphaSource = TextureImporterAlphaSource.FromInput;
        imp.alphaIsTransparency = true;           // rellena el color de los pixeles transparentes (sin halos)
        imp.mipmapEnabled = true;
        imp.mipMapsPreserveCoverage = true;       // la hierba no "adelgaza" a lo lejos
        imp.alphaTestReferenceValue = 0.5f;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.maxTextureSize = maximo;
        imp.SaveAndReimport();
    }

    private static Texture2D ATextura(Lienzo l)
    {
        var t = new Texture2D(l.ancho, l.alto, TextureFormat.RGBA32, false);
        t.SetPixels(l.px);
        t.Apply();
        return t;
    }

    private static void CrearCarpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;
        string padre = Path.GetDirectoryName(ruta).Replace('\\', '/');
        CrearCarpeta(padre);
        AssetDatabase.CreateFolder(padre, Path.GetFileName(ruta));
    }

    private static float R(System.Random rng) => (float)rng.NextDouble();

    private static float Gauss(System.Random rng)
    {
        double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
        return (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2.0 * System.Math.PI * u2));
    }
}
