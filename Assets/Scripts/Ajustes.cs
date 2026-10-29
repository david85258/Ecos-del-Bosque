using UnityEngine;

public class Ajustes : MonoBehaviour
{
    private static readonly float SensibilidadDefault = 0.5f;
    private static readonly float VolumenDefault = 1f;
    private static readonly float MusicaDefault = 0.5f;

    public static float GetSensibilidad()
    {
        return PlayerPrefs.GetFloat("sensibilidad", SensibilidadDefault);
    }

    public static void SetSensibilidad(float sensibilidad)
    {
        PlayerPrefs.SetFloat("sensibilidad", sensibilidad);
        PlayerPrefs.Save();
    }

    public static float GetVolumen()
    {
        return PlayerPrefs.GetFloat("volumen", VolumenDefault);
    }

    public static void SetVolumen(float volumen)
    {
        PlayerPrefs.SetFloat("volumen", volumen);
        PlayerPrefs.Save();
    }

    public static float GetMusica()
    {
        return PlayerPrefs.GetFloat("musica", MusicaDefault);
    }

    public static void SetMusica(float musica)
    {
        PlayerPrefs.SetFloat("musica", musica);
        PlayerPrefs.Save();
    }
}
