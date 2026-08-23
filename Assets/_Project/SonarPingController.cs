using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class SonarPingController : MonoBehaviour
{
    public Material sonarMaterial;
    public float maxRadius = 50f;
    public float pulseSpeed = 15f;
    private float currentRadius = 0f;
    private bool isPinging = false;

    void Update()
    {
        bool eKeyPressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            eKeyPressed = true;
        }
#else
        if (Input.GetKeyDown(KeyCode.E))
        {
            eKeyPressed = true;
        }
#endif

        if (eKeyPressed && !isPinging)
        {
            TriggerPing();
        }

        if (isPinging)
        {
            currentRadius += pulseSpeed * Time.deltaTime;
            sonarMaterial.SetFloat("_PulseRadius", currentRadius);

            if (currentRadius >= maxRadius)
            {
                isPinging = false;
                sonarMaterial.SetFloat("_PulseRadius", 0f);
            }
        }
    }

    public void TriggerPing()
    {
        currentRadius = 0f;
        isPinging = true;
        sonarMaterial.SetVector("_PulseCenter", transform.position);
    }
}