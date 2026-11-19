using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Animator))]
public class Gato : MonoBehaviour
{
    private static readonly int Dance = Animator.StringToHash("Dance");
    private static readonly int DanceFull = Animator.StringToHash("DanceFull");
    private static readonly int Stop = Animator.StringToHash("Stop");
    [SerializeField] private Collider triggerCollider;
    [SerializeField] private GameObject avisoInteractuar;
    private Animator animator;
    
    private bool jugadorDentro;

    private int contadorAcaricias;
    
    void Start()
    {
        avisoInteractuar.SetActive(false);
        animator = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        if (!jugadorDentro || contadorAcaricias == -1 || !animator.GetCurrentAnimatorStateInfo(0).IsName("GatoIdle")) return;

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            Interact();
        }
    }

    private void Interact()
    {
        if (contadorAcaricias < 3)
        {
            contadorAcaricias++;
            animator.SetTrigger(Dance);
            //runs in 3s
            Invoke("StopAnim", 2f);
        }
        else
        {
            contadorAcaricias = -1;
            animator.SetTrigger(DanceFull);
        }
    }
    
    private void StopAnim()
    {
        animator.SetTrigger(Stop);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.GetComponent<PlayerMovement>())
            return;

        jugadorDentro = true;

        avisoInteractuar.SetActive(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.GetComponent<PlayerMovement>())
            return;

        jugadorDentro = false;

        avisoInteractuar.SetActive(false);
    }
}
