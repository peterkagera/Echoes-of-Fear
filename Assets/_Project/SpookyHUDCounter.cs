using System.Collections;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(TextMeshProUGUI))]
public class SpookyHUDCounter : MonoBehaviour
{
    private TextMeshProUGUI textMesh;

    // Glowing Horror Colors
    private Color baseColor = new Color(0.85f, 0.15f, 0.15f, 1f); // Blood Red
    private Color glitchColor = new Color(1f, 0.4f, 0.1f, 0.8f); // Ember Glow

    private int currentCount = 15;
    private int maxCount = 15;
    private Vector3 initialScale;

    private void Awake()
    {
        textMesh = GetComponent<TextMeshProUGUI>();
        initialScale = transform.localScale;
    }

    private void Start()
    {
        UpdateDisplay();
        StartCoroutine(GlitchRoutine());
        StartCoroutine(PulseRoutine());
    }

    public void UpdateCount(int remaining, int total)
    {
        currentCount = remaining;
        maxCount = total;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (textMesh == null) return;
        textMesh.text = $"<size=70%><color=#AA3333>CONTAINMENT STATUS</color></size>\n<size=130%><b>{currentCount:D2}</b></size><size=80%><color=#888888> / {maxCount} CELLS</color></size>";
    }

    private IEnumerator GlitchRoutine()
    {
        while (true)
        {
            // Trigger glitch every 2 to 4 seconds
            yield return new WaitForSeconds(Random.Range(2f, 4.2f));

            if (textMesh == null) continue;

            float duration = Random.Range(0.12f, 0.35f);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                // Twitch color and horizontal margin
                textMesh.color = (Random.value > 0.4f) ? glitchColor : baseColor;
                textMesh.margin = new Vector4(Random.Range(-4f, 4f), Random.Range(-1f, 1f), 0, 0);

                elapsed += Time.deltaTime;
                yield return null;
            }

            // Reset positioning
            textMesh.color = baseColor;
            textMesh.margin = Vector4.zero;
        }
    }

    private IEnumerator PulseRoutine()
    {
        // Subtle breathing effect for horror atmosphere
        while (true)
        {
            float scaleOffset = Mathf.Sin(Time.time * 2.5f) * 0.03f;
            transform.localScale = initialScale + new Vector3(scaleOffset, scaleOffset, 0f);
            yield return null;
        }
    }
}