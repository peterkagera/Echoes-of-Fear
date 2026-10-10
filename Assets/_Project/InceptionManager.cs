using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class InceptionManager : MonoBehaviour
{
    public static InceptionManager Instance;

    [Header("Dream Layer Configuration")]
    [Range(1, 3)] public int currentDreamLayer = 1;
    public int maxDreamLayers = 3;

    [Header("Scene References")]
    public Volume globalVolume;
    public WindZone sceneWind;

    private ColorAdjustments colorAdjustments;
    private bool isTransitioning = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Cache URP Color Adjustments component from the Global Volume profile
        if (globalVolume != null && globalVolume.profile.TryGet(out colorAdjustments))
        {
            colorAdjustments.saturation.value = 0f; // Layer 1: Normal reality
        }
    }

    /// <summary>
    /// Triggers a drop into a deeper dream layer when captured by the Fire Dog.
    /// </summary>
    public void TriggerDreamCollapse()
    {
        if (isTransitioning) return;

        if (currentDreamLayer < maxDreamLayers)
        {
            currentDreamLayer++;
            StartCoroutine(ExecuteLayerTransitionRoutine());
        }
        else
        {
            // Max depth reached: Trigger actual Game Over / Final Death
            Debug.Log("[InceptionManager] Max dream depth reached. Triggering Game Over.");
            if (JumpscareManager.Instance != null)
            {
                // Fallback to standard game over handling
            }
        }
    }

    private IEnumerator ExecuteLayerTransitionRoutine()
    {
        isTransitioning = true;
        float transitionDuration = 2.0f;
        float timer = 0f;

        float startSaturation = colorAdjustments != null ? colorAdjustments.saturation.value : 0f;
        float targetSaturation = -30f * (currentDreamLayer - 1); // Deeper layers become more monochrome

        float startFogDensity = RenderSettings.fogDensity;
        float targetFogDensity = startFogDensity + (0.015f * (currentDreamLayer - 1)); // Fog thickens

        float startWindTurbulence = sceneWind != null ? sceneWind.turbulence : 0.5f;
        float targetWindTurbulence = startWindTurbulence + (1.0f * currentDreamLayer); // Trees whip wildly

        // Smoothly interpolate parameters over time
        while (timer < transitionDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / transitionDuration;

            if (colorAdjustments != null)
            {
                colorAdjustments.saturation.value = Mathf.Lerp(startSaturation, targetSaturation, progress);
            }

            RenderSettings.fogDensity = Mathf.Lerp(startFogDensity, targetFogDensity, progress);

            if (sceneWind != null)
            {
                sceneWind.turbulence = Mathf.Lerp(startWindTurbulence, targetWindTurbulence, progress);
            }

            yield return null;
        }

        // Scale enemy speeds dynamically per dream layer to increase tension
        EnemyAI[] activeEnemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        foreach (EnemyAI enemy in activeEnemies)
        {
            enemy.moveSpeed += 0.6f; // Fire Dog and Stalker become progressively faster
        }

        isTransitioning = false;
        Debug.Log($"[InceptionManager] Successfully transitioned to Dream Layer {currentDreamLayer}");
    }
}