using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider))]
public class Puerta : MonoBehaviour
{
    [Header("Destino")]
    [SerializeField] private string escenaDestino;

    [Header("Interaccion")]
    [SerializeField] private GameObject avisoInteractuar;

    private bool jugadorDentro;

    void Reset()
    {
        Collider[] colliders = GetComponents<Collider>();

        if (colliders.Length == 1)
            colliders[0].isTrigger = true;
    }

    void Start()
    {
        if (avisoInteractuar)
            avisoInteractuar.SetActive(false);
    }

    void Update()
    {
        if (!jugadorDentro || Keyboard.current == null)
            return;

        if (Keyboard.current.eKey.wasPressedThisFrame)
            Interactuar();
    }

    private void Interactuar()
    {
        if (string.IsNullOrEmpty(escenaDestino))
        {
            Debug.LogWarning($"La puerta '{name}' no tiene escena destino asignada.", this);
            return;
        }

        CambiarEscena.Cambiar(escenaDestino);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.GetComponent<PlayerMovement>())
            return;

        jugadorDentro = true;

        if (avisoInteractuar)
            avisoInteractuar.SetActive(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.GetComponent<PlayerMovement>())
            return;

        jugadorDentro = false;

        if (avisoInteractuar)
            avisoInteractuar.SetActive(false);
    }
}
