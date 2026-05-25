using System.Collections;
using UnityEngine;

public class BuildSceneSFX : MonoBehaviour {

    public static BuildSceneSFX Instance;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] buildSounds;

    private bool dontPlaySound;
    
    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
    }

    public void PlayRandomBuildSound() {
        if (dontPlaySound) return;

        audioSource.PlayOneShot(buildSounds[UnityEngine.Random.Range(0, 5)]);
        StartCoroutine(DontAllowNewSound());
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
