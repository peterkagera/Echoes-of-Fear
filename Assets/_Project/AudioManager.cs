using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource footstepSource;

    [Header("Audio Clips")]
    [SerializeField] private AudioClip flashlightToggleSFX;
    [SerializeField] private AudioClip batteryPickupSFX;
    [SerializeField] private AudioClip[] footstepSFXArray;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void PlayFlashlightToggle() => PlaySFX(flashlightToggleSFX);
    public void PlayBatteryPickup() => PlaySFX(batteryPickupSFX);

    public void PlayFootstep()
    {
        if (footstepSFXArray.Length == 0 || footstepSource.isPlaying) return;
        AudioClip clip = footstepSFXArray[Random.Range(0, footstepSFXArray.Length)];
        footstepSource.pitch = Random.Range(0.85f, 1.15f); // Subtle pitch variation
        footstepSource.PlayOneShot(clip);
    }

    private void PlaySFX(AudioClip clip)
    {
        if (clip != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(clip);
        }
    }
}