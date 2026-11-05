using UnityEngine;

/// <summary>
/// Ruido Perlin 2D con semilla propia. Es determinista y seguro de usar desde varios hilos,
/// asi que lo pueden compartir todos los generadores (terreno, vegetacion, arboles...).
/// </summary>
public class Ruido
{
    private readonly int[] perm = new int[512];

    public Ruido(int semilla)
    {
        var rng = new System.Random(semilla);
        int[] p = new int[256];

        for (int i = 0; i < 256; i++)
            p[i] = i;

        for (int i = 255; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (p[i], p[j]) = (p[j], p[i]);
        }

        for (int i = 0; i < 512; i++)
            perm[i] = p[i & 255];
    }

    /// <summary>Ruido Perlin clasico, aproximadamente en [-1, 1].</summary>
    public float Perlin(float x, float y)
    {
        int xi = Mathf.FloorToInt(x);
        int yi = Mathf.FloorToInt(y);
        float xf = x - xi;
        float yf = y - yi;
        xi &= 255;
        yi &= 255;

        float u = Suavizar(xf);
        float v = Suavizar(yf);

        int aa = perm[perm[xi] + yi];
        int ab = perm[perm[xi] + yi + 1];
        int ba = perm[perm[xi + 1] + yi];
        int bb = perm[perm[xi + 1] + yi + 1];

        float x1 = Mathf.Lerp(Gradiente(aa, xf, yf), Gradiente(ba, xf - 1f, yf), u);
        float x2 = Mathf.Lerp(Gradiente(ab, xf, yf - 1f), Gradiente(bb, xf - 1f, yf - 1f), u);
        return Mathf.Lerp(x1, x2, v);
    }

    /// <summary>Ruido fractal (varias octavas) normalizado aproximadamente a [0, 1].</summary>
    public float Fbm(float x, float y, int octavas, float persistencia = 0.5f, float lacunaridad = 2f)
    {
        float suma = 0f, amplitud = 1f, frecuencia = 1f, total = 0f;

        for (int o = 0; o < octavas; o++)
        {
            float desfase = o * 17.31f;
            suma += Perlin(x * frecuencia + desfase, y * frecuencia - desfase) * amplitud;
            total += amplitud;
            amplitud *= persistencia;
            frecuencia *= lacunaridad;
        }

        return suma / total * 0.5f + 0.5f;
    }

    /// <summary>Ruido de crestas (lomas afiladas), en [0, 1].</summary>
    public float Crestas(float x, float y, int octavas, float persistencia = 0.5f, float lacunaridad = 2f)
    {
        float suma = 0f, amplitud = 1f, frecuencia = 1f, total = 0f;

        for (int o = 0; o < octavas; o++)
        {
            float desfase = o * 23.71f;
            float n = 1f - Mathf.Abs(Perlin(x * frecuencia + desfase, y * frecuencia + desfase));
            suma += n * n * amplitud;
            total += amplitud;
            amplitud *= persistencia;
            frecuencia *= lacunaridad;
        }

        return suma / total;
    }

    private static float Suavizar(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

    private static float Gradiente(int hash, float x, float y)
    {
        switch (hash & 7)
        {
            case 0: return x + y;
            case 1: return -x + y;
            case 2: return x - y;
            case 3: return -x - y;
            case 4: return x;
            case 5: return -x;
            case 6: return y;
            default: return -y;
        }
    }
}
