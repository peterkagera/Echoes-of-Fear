using UnityEngine;

public class MobilePerformanceManager : MonoBehaviour
{
    [Header("Target Frame Rate & Resolution")]
    [SerializeField] private int targetFPS = 60;
    [SerializeField] private float targetDpiFactor = 0.7f;
    [SerializeField] private float mobileShadowDistance = 25f;

    private void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = targetFPS;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        // Mobile DPI Downscaling for high-density screens
        QualitySettings.resolutionScalingFixedDPIFactor = targetDpiFactor;
        QualitySettings.shadowDistance = mobileShadowDistance;
    }
}