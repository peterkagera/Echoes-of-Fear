using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class InceptionManager : MonoBehaviour
{
    public static InceptionManager Instance;

    [Header("Dream Layer Configuration")]
    [Range(1, 3)] public int currentDreamLayer = 1;
    public int maxDreamLayers = 3;

    [Header("Scene References")]
    public Volume globalVolume;
    public WindZone sceneWind;

    [Header("Visual Feedback References")]
    public Image dreamFlashPanel; // Drag your DreamFlashPanel here

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

        if (globalVolume != null && globalVolume.profile.TryGet(out colorAdjustments))
        {
            colorAdjustments.saturation.value = 0f;
        }

        if (dreamFlashPanel != null)
        {
            Color c = dreamFlashPanel.color;
            c.a = 0f;
            dreamFlashPanel.color = c;
        }
    }

    public void TriggerDreamCollapse()
    {
        if (isTransitioning) return;

        if (currentDreamLayer < maxDreamLayers)
        {
            currentDreamLayer++;
            Debug.Log($"[InceptionManager] >>> DREAM COLLAPSE! Slipped into Dream Layer {currentDreamLayer} <<<");
            StartCoroutine(ExecuteLayerTransitionRoutine());
        }
        else
        {
            Debug.Log("[InceptionManager] Max dream depth reached. Triggering Game Over.");
        }
    }

    private IEnumerator ExecuteLayerTransitionRoutine()
    {
        isTransitioning = true;
        float transitionDuration = 1.5f;
        float timer = 0f;

        float startSaturation = colorAdjustments != null ? colorAdjustments.saturation.value : 0f;
        float targetSaturation = -45f * (currentDreamLayer - 1);

        float startFogDensity = RenderSettings.fogDensity;
        float targetFogDensity = startFogDensity + (0.03f * (currentDreamLayer - 1));

        float startWindTurbulence = sceneWind != null ? sceneWind.windTurbulence : 0.5f;
        float targetWindTurbulence = startWindTurbulence + (2.5f * currentDreamLayer);

        // Flash the screen red/purple at the start of the collapse
        if (dreamFlashPanel != null)
        {
            StartCoroutine(FlashScreenRoutine());
        }

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
                sceneWind.windTurbulence = Mathf.Lerp(startWindTurbulence, targetWindTurbulence, progress);
            }

            yield return null;
        }

        EnemyAI[] activeEnemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        foreach (EnemyAI enemy in activeEnemies)
        {
            enemy.moveSpeed += 0.8f;
        }

        isTransitioning = false;
        Debug.Log($"[InceptionManager] Now active in Dream Layer {currentDreamLayer}");
    }

    private IEnumerator FlashScreenRoutine()
    {
        float flashDuration = 0.5f;
        float flashTimer = 0f;

        // Fade in quickly
        while (flashTimer < flashDuration * 0.3f)
        {
            flashTimer += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, 0.7f, flashTimer / (flashDuration * 0.3f));
            Color c = dreamFlashPanel.color;
            c.a = alpha;
            dreamFlashPanel.color = c;
            yield return null;
        }

        // Fade out slowly
        flashTimer = 0f;
        while (flashTimer < flashDuration * 0.7f)
        {
            flashTimer += Time.deltaTime;
            float alpha = Mathf.Lerp(0.7f, 0f, flashTimer / (flashDuration * 0.7f));
            Color c = dreamFlashPanel.color;
            c.a = alpha;
            dreamFlashPanel.color = c;
            yield return null;
        }

        Color finalColor = dreamFlashPanel.color;
        finalColor.a = 0f;
        dreamFlashPanel.color = finalColor;
    }

    /// <summary>
    /// Reverses the dream collapse when the player collects a Totem, waking them back up toward Layer 1.
    /// </summary>
    public void WakeUpFromDream()
    {
        if (isTransitioning) return;

        if (currentDreamLayer > 1)
        {
            currentDreamLayer--;
            Debug.Log($"[InceptionManager] >>> TOTEM USED! Waking up to Dream Layer {currentDreamLayer} <<<");
            StartCoroutine(ExecuteLayerTransitionRoutine());
        }
    }
}