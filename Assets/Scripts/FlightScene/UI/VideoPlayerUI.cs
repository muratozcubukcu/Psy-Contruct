using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class VideoPlayerUI : MonoBehaviour {
    [SerializeField] private string videoFilename;
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private RawImage rawImage;

    private void Start() {
        rawImage.enabled = false;

        videoPlayer.source = VideoSource.Url;
        videoPlayer.url = Path.Combine(Application.streamingAssetsPath, videoFilename)
            .Replace('\\', '/');

        videoPlayer.prepareCompleted += OnPrepareCompleted;
        videoPlayer.Prepare();
    }

    private void OnPrepareCompleted(VideoPlayer src) {
        rawImage.enabled = true;
        videoPlayer.Play();
    }

    private void OnDestroy() {
        videoPlayer.prepareCompleted -= OnPrepareCompleted;
    }
}