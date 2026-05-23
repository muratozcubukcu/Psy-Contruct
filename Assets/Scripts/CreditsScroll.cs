using UnityEngine;
using UnityEngine.SceneManagement;

public class CreditsScroll : MonoBehaviour
{
    [SerializeField] private float scrollSpeed = 80f;
    [SerializeField] private string mainMenuScene = "MainMenuScene";

    private RectTransform rectTransform;
    private bool finished = false;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    void Update()
    {
        if (finished) return;

        rectTransform.anchoredPosition += Vector2.up * scrollSpeed * Time.deltaTime;

        if (rectTransform.anchoredPosition.y >= rectTransform.rect.height + Screen.height + 500f)
        {
            finished = true;
            SceneManager.LoadScene(mainMenuScene);
        }
    }
}