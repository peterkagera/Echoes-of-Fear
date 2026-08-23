using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource footstepSource;
    [SerializeField] private AudioSource ambientSource;

    [Header("Action Clips")]
    [SerializeField] private AudioClip flashlightToggleSFX;
    [SerializeField] private AudioClip batteryPickupSFX;
    [SerializeField] private AudioClip[] footstepSFXArray;

    [Header("Ambient Horror SFX")]
    [SerializeField] private AudioClip[] ambientHorrorClips;
    [SerializeField] private Vector2 ambientIntervalRange = new Vector2(10f, 25f);

    private float ambientTimer;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        ResetAmbientTimer();
    }

    private void Update()
    {
        HandleAmbientHorror();
    }

    public void PlayFlashlightToggle() => PlaySFX(flashlightToggleSFX);
    public void PlayBatteryPickup() => PlaySFX(batteryPickupSFX);

    public void PlayFootstep()
    {
        if (footstepSFXArray == null || footstepSFXArray.Length == 0 || footstepSource == null || footstepSource.isPlaying) return;
        AudioClip clip = footstepSFXArray[Random.Range(0, footstepSFXArray.Length)];
        footstepSource.pitch = Random.Range(0.85f, 1.15f);
        footstepSource.PlayOneShot(clip);
    }

    private void PlaySFX(AudioClip clip)
    {
        if (clip != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(clip);
        }
    }

    private void HandleAmbientHorror()
    {
        if (ambientHorrorClips == null || ambientHorrorClips.Length == 0) return;

        ambientTimer -= Time.deltaTime;
        if (ambientTimer <= 0f)
        {
            PlayRandomAmbient();
            ResetAmbientTimer();
        }
    }

    private void PlayRandomAmbient()
    {
        if (ambientSource == null) return;
        AudioClip clip = ambientHorrorClips[Random.Range(0, ambientHorrorClips.Length)];
        ambientSource.PlayOneShot(clip);
    }

    private void ResetAmbientTimer()
    {
        ambientTimer = Random.Range(ambientIntervalRange.x, ambientIntervalRange.y);
    }
}