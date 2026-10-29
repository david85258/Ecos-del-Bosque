using UnityEngine;
using UnityEngine.UI;

public class MenuAjustes : MonoBehaviour
{
    [SerializeField] private Slider sliderSensibilidad;
    [SerializeField] private Slider sliderVolumen;
    [SerializeField] private Slider sliderMusica;

    void Start()
    {
        sliderSensibilidad.SetValueWithoutNotify(Ajustes.GetSensibilidad());
        sliderVolumen.SetValueWithoutNotify(Ajustes.GetVolumen());
        sliderMusica.SetValueWithoutNotify(Ajustes.GetMusica());
    }
}
