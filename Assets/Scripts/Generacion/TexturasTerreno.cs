using UnityEngine;

/// <summary>
/// Genera texturas de terreno sencillas por codigo (hierba, hojarasca y roca).
/// Solo se usan como reserva cuando no se asignan TerrainLayers en el inspector del GeneradorTerreno.
/// Las texturas son repetibles (sin costuras) para que el mosaico no se note.
/// </summary>
public static class TexturasTerreno
{
    private const int Resolucion = 512;
    private const float BrilloAlfa = 0.05f;

    private delegate Color FuncionColor(float u, float v, int semilla);

    public static TerrainLayer CrearHierba(int semilla) =>
        CrearCapa("Hierba", semilla, 6f, ColorHierba);

    public static TerrainLayer CrearHojarasca(int semilla) =>
        CrearCapa("Hojarasca", semilla, 5f, ColorHojarasca);

    public static TerrainLayer CrearRoca(int semilla) =>
        CrearCapa("Roca", semilla, 10f, ColorRoca);

    private static TerrainLayer CrearCapa(string nombre, int semilla, float tamanoMosaico, FuncionColor funcion)
    {
        var textura = new Texture2D(Resolucion, Resolucion, TextureFormat.RGBA32, true)
        {
            name = "Textura_" + nombre,
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 4,
            hideFlags = HideFlags.DontSave
        };

        var pixeles = new Color[Resolucion * Resolucion];

        System.Threading.Tasks.Parallel.For(0, Resolucion, y =>
        {
            for (int x = 0; x < Resolucion; x++)
            {
                Color c = funcion((float)x / Resolucion, (float)y / Resolucion, semilla);
                c.a = BrilloAlfa; // En URP el alfa de la textura es la suavidad (brillo).
                pixeles[y * Resolucion + x] = c;
            }
        });

        textura.SetPixels(pixeles);
        textura.Apply(true);

        return new TerrainLayer
        {
            name = "Capa_" + nombre,
            hideFlags = HideFlags.DontSave,
            diffuseTexture = textura,
            tileSize = new Vector2(tamanoMosaico, tamanoMosaico),
            smoothness = 0f,
            metallic = 0f
        };
    }

    // ---------------------------------------------------------------- Colores

    private static Color ColorHierba(float u, float v, int s)
    {
        float manchas = Fbm(u, v, 4, 5, s);
        float briznas = Fbm(u, v, 64, 3, s + 1);
        float seca = SuaveEntre(0.6f, 0.8f, Fbm(u, v, 3, 3, s + 2));

        Color c = Color.Lerp(new Color(0.18f, 0.28f, 0.09f), new Color(0.32f, 0.42f, 0.15f), manchas);
        c = Color.Lerp(c, new Color(0.44f, 0.42f, 0.20f), seca * 0.45f);
        return c * (0.75f + 0.45f * briznas);
    }

    private static Color ColorHojarasca(float u, float v, int s)
    {
        float baseRuido = Fbm(u, v, 6, 5, s + 10);
        float hojas = Fbm(u, v, 48, 2, s + 11);
        float musgo = SuaveEntre(0.6f, 0.75f, Fbm(u, v, 3, 4, s + 12));

        Color c = Color.Lerp(new Color(0.20f, 0.14f, 0.08f), new Color(0.36f, 0.25f, 0.13f), baseRuido);

        if (hojas > 0.68f)
            c = Color.Lerp(c, new Color(0.52f, 0.30f, 0.12f), (hojas - 0.68f) * 3f);
        else if (hojas < 0.3f)
            c *= 0.7f;

        c = Color.Lerp(c, new Color(0.20f, 0.30f, 0.10f), musgo * 0.6f);
        return c;
    }

    private static Color ColorRoca(float u, float v, int s)
    {
        float baseRuido = Fbm(u, v, 4, 6, s + 20);
        float detalle = Fbm(u, v, 32, 3, s + 21);
        float grieta = 1f - Mathf.Abs(Fbm(u, v, 6, 4, s + 22) * 2f - 1f);

        Color c = Color.Lerp(new Color(0.34f, 0.33f, 0.31f), new Color(0.55f, 0.54f, 0.50f), baseRuido);
        c *= 0.85f + 0.3f * detalle;

        if (grieta > 0.9f)
            c *= Mathf.Lerp(1f, 0.45f, (grieta - 0.9f) * 10f);

        return c;
    }

    // ---------------------------------------------------------------- Ruido periodico (sin costuras)

    private static float Fbm(float u, float v, int periodoBase, int octavas, int semilla)
    {
        float suma = 0f, amplitud = 1f, total = 0f;
        int periodo = periodoBase;

        for (int o = 0; o < octavas; o++)
        {
            suma += ValorPeriodico(u * periodo, v * periodo, periodo, semilla + o * 101) * amplitud;
            total += amplitud;
            amplitud *= 0.5f;
            periodo *= 2;
        }

        return suma / total;
    }

    private static float ValorPeriodico(float x, float y, int periodo, int semilla)
    {
        int x0 = Mathf.FloorToInt(x);
        int y0 = Mathf.FloorToInt(y);
        float fx = x - x0;
        float fy = y - y0;

        int xa = Modulo(x0, periodo), xb = Modulo(x0 + 1, periodo);
        int ya = Modulo(y0, periodo), yb = Modulo(y0 + 1, periodo);

        float sx = fx * fx * (3f - 2f * fx);
        float sy = fy * fy * (3f - 2f * fy);

        float a = Mathf.Lerp(Hash(xa, ya, semilla), Hash(xb, ya, semilla), sx);
        float b = Mathf.Lerp(Hash(xa, yb, semilla), Hash(xb, yb, semilla), sx);
        return Mathf.Lerp(a, b, sy);
    }

    private static int Modulo(int a, int m) => ((a % m) + m) % m;

    private static float Hash(int x, int y, int semilla)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + semilla * 144665);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777215f;
        }
    }

    private static float SuaveEntre(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }
}
