using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("音效资源")]
    public AudioClip dodgeClip;
    public AudioClip perfectDodgeClip;
    public AudioClip fireClip;
    public AudioClip hitCoreClip;
    public AudioClip warningClip;

    private AudioSource _sfxSource;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.playOnAwake = false;
        _sfxSource.loop       = false;
        _sfxSource.spatialBlend = 0f;
    }

    public static void PlayDodge()
    {
        Play(dodgeClip);
    }

    public static void PlayPerfectDodge()
    {
        Play(perfectDodgeClip);
    }

    public static void PlayFire()
    {
        Play(fireClip);
    }

    public static void PlayHitCore()
    {
        Play(hitCoreClip);
    }

    public static void PlayWarning()
    {
        Play(warningClip);
    }

    static void Play(AudioClip clip)
    {
        if (Instance == null || Instance._sfxSource == null || clip == null) return;
        Instance._sfxSource.PlayOneShot(clip);
    }
}
