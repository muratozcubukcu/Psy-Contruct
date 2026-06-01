using System.Collections;
using UnityEngine;

public class BuildSceneSFX : MonoBehaviour {

    public static BuildSceneSFX Instance;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] buildSounds;
    [SerializeField] private AudioClip invalidPlacementSound;
    [SerializeField] private AudioClip[] removePartSounds;

    private bool dontPlaySound;
    
    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
    }

    public void PlayRandomBuildSound() {
        if (dontPlaySound || buildSounds == null || buildSounds.Length == 0) return;

        AudioClip clip = buildSounds[UnityEngine.Random.Range(0, buildSounds.Length)];
        PlayClip(clip);
        StartCoroutine(DontAllowNewSound());
    }

    public void PlayInvalidPlacementSound() {
        if (dontPlaySound) return;

        PlayClip(invalidPlacementSound);
        StartCoroutine(DontAllowNewSound());
    }

    public void PlayRandomRemovePartSound() {
        if (dontPlaySound || removePartSounds == null || removePartSounds.Length == 0) return;

        AudioClip clip = removePartSounds[UnityEngine.Random.Range(0, removePartSounds.Length)];
        PlayClip(clip);
        StartCoroutine(DontAllowNewSound());
    }

    private void PlayClip(AudioClip clip) {
        if (clip == null || audioSource == null) return;

        float v = 1f;
        if (Settings.Instance != null) v *= Settings.Instance.sfxVolume;
        if (v > 0f) audioSource.PlayOneShot(clip, v);
    }

    private IEnumerator DontAllowNewSound() {
        dontPlaySound = true;
        yield return new WaitForSeconds(0.1f);
        dontPlaySound = false;
    }
    
    private void OnDestroy() {
        if (Instance == this) Instance = null;
    }
}
