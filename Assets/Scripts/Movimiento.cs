using UnityEngine;
using UnityEngine.InputSystem;

public class Movimiento : MonoBehaviour
{
    public float velocidadCaminar = 5f;
    public float fuerzaSalto = 6f;

    // Usaremos un radio esférico más grande para abrazar las pendientes laterales
    public float radioSuelo = 0.4f;
    // Y un desfase vertical ligero para que la esfera no se hunda en el piso
    public float desfaseSuelo = -0.1f;
    public LayerMask capaSuelo;

    // Componentes del Personaje
    private Rigidbody rb;
    private Animator animator;

    // Variables de Estado Internas
    private bool estaEnElSuelo;
    private float inputXActual;
    private float inputYActual;
    private float tiempoProximoSalto = 0f;

    // Control de Altura Máxima
    private float alturaMaximaCaida = 0f;
    private bool empezoACaer = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();

        if (rb != null)
        {
            rb.freezeRotation = true;
        }
    }

    void Update()
    {
        // 1. CAPTURA DE INPUTS
        float targetX = 0f;
        float targetY = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) targetY = 1f;
            if (Keyboard.current.sKey.isPressed) targetY = -1f;
            if (Keyboard.current.dKey.isPressed) targetX = 1f;
            if (Keyboard.current.aKey.isPressed) targetX = -1f;
        }

        inputXActual = Mathf.MoveTowards(inputXActual, targetX, Time.deltaTime * 8f);
        inputYActual = Mathf.MoveTowards(inputYActual, targetY, Time.deltaTime * 8f);

        // 2. DETECTAR EL SUELO (CORREGIDO PARA PENDIENTES EMPINADAS)
        // Usamos una esfera de detección (`CheckSphere`) ligeramente elevada y con un radio amplio
        // Esto permite que el personaje detecte el suelo inclinado que tiene a los lados de sus pies.
        Vector3 origenSuelo = transform.position + Vector3.up * (radioSuelo + desfaseSuelo);
        estaEnElSuelo = Physics.CheckSphere(origenSuelo, radioSuelo, capaSuelo);

        // 3. ACCIÓN DE SALTO
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            if (estaEnElSuelo && Time.time > tiempoProximoSalto)
            {
                // Aplicamos el salto de forma directa e inmediata en el eje Y
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, fuerzaSalto, rb.linearVelocity.z);

                if (animator != null)
                {
                    animator.SetTrigger("JumpTrigger");
                }

                tiempoProximoSalto = Time.time + 0.2f;
                empezoACaer = false;
                alturaMaximaCaida = 0f;
            }
        }

        // 4. CONTROL DE FOTOGRAMA EN EL AIRE
        if (animator != null)
        {
            animator.SetFloat("xVal", inputXActual);
            animator.SetFloat("yVal", inputYActual);

            if (Time.time < tiempoProximoSalto)
            {
                animator.SetBool("isGrounded", false);
            }
            else
            {
                animator.SetBool("isGrounded", estaEnElSuelo);
            }

            // --- LÍNEA DE TIEMPO SEGÚN ALTURA ---
            if (!estaEnElSuelo)
            {
                RaycastHit hit;
                // El rayo de caída libre debe dispararse desde la misma altura calibrada del sensor de suelo
                Vector3 origenRayoCaida = transform.position + Vector3.up * (radioSuelo + desfaseSuelo + 0.1f);

                if (Physics.Raycast(origenRayoCaida, Vector3.down, out hit, 150f, capaSuelo))
                {
                    // Ajustamos la distancia para que la animación termine en el piso real, no en el aire
                    float distanciaAlPisoAjustada = hit.distance - (radioSuelo + desfaseSuelo);
                    if (distanciaAlPisoAjustada < 0f) distanciaAlPisoAjustada = 0f;

                    if (rb.linearVelocity.y <= 0 && !empezoACaer)
                    {
                        alturaMaximaCaida = distanciaAlPisoAjustada;
                        empezoACaer = true;
                    }

                    if (empezoACaer && alturaMaximaCaida > 0.1f)
                    {
                        float porcentajeDelViaje = 1f - (distanciaAlPisoAjustada / alturaMaximaCaida);

                        // Calibración final del frame de vuelo: lo estiramos un poco más hasta 0.5f 
                        // para que no se vea tan estático en caídas muy largas
                        porcentajeDelViaje = Mathf.Clamp(porcentajeDelViaje, 0f, 0.5f);

                        animator.SetFloat("VelocidadAdaptativa", porcentajeDelViaje);
                    }
                }
            }
            else
            {
                // Limpiamos la velocidad vertical al impacto para un movimiento fluido
                if (rb.linearVelocity.y < -0.1f)
                {
                    rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                }

                empezoACaer = false;
                animator.SetFloat("VelocidadAdaptativa", 0f);
            }
        }
    }

    void FixedUpdate()
    {
        // Movimiento relativo al personaje
        Vector3 direccionLocal = new Vector3(inputXActual, 0f, inputYActual).normalized;
        Vector3 direccionMovimiento = transform.TransformDirection(direccionLocal);
        Vector3 velocidadFisica = direccionMovimiento * velocidadCaminar;

        rb.linearVelocity = new Vector3(velocidadFisica.x, rb.linearVelocity.y, velocidadFisica.z);

        if (Mathf.Abs(inputXActual) > 0.1f)
        {
            float velocidadRotacion = inputXActual * 120f * Time.fixedDeltaTime;
            transform.Rotate(0f, velocidadRotacion, 0f);
        }
    }

    // Dibujamos la esfera de detección en rojo en el editor para calibrar visualmente
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        // Calculamos el origen de la esfera exactamente igual que en el script
        Vector3 origenSuelo = transform.position + Vector3.up * (radioSuelo + desfaseSuelo);
        Gizmos.DrawWireSphere(origenSuelo, radioSuelo);
    }
}