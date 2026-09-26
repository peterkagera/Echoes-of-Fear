using UnityEngine;

public class MobilePerformanceManager : MonoBehaviour
{
    [Header("Target Frame Rate & Resolution")]
    [Tooltip("If true, automatically sets frame rate target to match 90Hz/120Hz screens.")]
    [SerializeField] private bool autoMatchScreenRefreshRate = true;
    [SerializeField] private int fallbackTargetFPS = 60;
    [SerializeField] private float targetDpiFactor = 0.7f;
    [SerializeField] private float mobileShadowDistance = 25f;

    private void Awake()
    {
        // VSync must be 0 for Application.targetFrameRate to function on mobile
        QualitySettings.vSyncCount = 0;

        if (autoMatchScreenRefreshRate)
        {
            int deviceRefreshRate = (int)Screen.currentResolution.refreshRateRatio.value;
            Application.targetFrameRate = deviceRefreshRate > 30 ? deviceRefreshRate : fallbackTargetFPS;
        }
        else
        {
            Application.targetFrameRate = fallbackTargetFPS;
        }

        // Prevent screen dimming during active gameplay
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        // Reduces viewport render resolution on dense displays to save GPU bandwidth
        QualitySettings.resolutionScalingFixedDPIFactor = targetDpiFactor;
        QualitySettings.shadowDistance = mobileShadowDistance;
    }
}