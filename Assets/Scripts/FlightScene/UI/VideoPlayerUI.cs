using System.IO;
using UnityEngine;
using UnityEngine.Video;

public class VideoPlayerUI : MonoBehaviour {
    [SerializeField] private string videoFilename;
    [SerializeField] private VideoPlayer videoPlayer;

    private void Start() {
        videoPlayer.source = VideoSource.Url;
        videoPlayer.url = Path.Combine(Application.streamingAssetsPath, videoFilename)
            .Replace('\\', '/');
        videoPlayer.Play();
    }
}