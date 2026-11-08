using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class SplashLoader : MonoBehaviour
{
    [SerializeField] private Slider loadingBar;
    [SerializeField] private TMP_Text loadingText;
    [SerializeField] private string mainMenuScene = "MainMenu";
    [SerializeField] private float minimumDisplayTime = 25f;

    private IEnumerator Start()
    {
        float startTime = Time.time;
        AsyncOperation op = SceneManager.LoadSceneAsync(mainMenuScene);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f || Time.time - startTime < minimumDisplayTime)
        {
            float real = Mathf.Clamp01(op.progress / 0.9f);
            float time = Mathf.Clamp01((Time.time - startTime) / minimumDisplayTime);
            float shown = Mathf.Min(real, time);

            loadingBar.value = shown;
            loadingText.text = $"Loading... {Mathf.RoundToInt(shown * 100)}%";
            yield return null;
        }

        loadingBar.value = 1f;
        loadingText.text = "Loading... 100%";
        yield return new WaitForSeconds(0.2f);
        op.allowSceneActivation = true;
    }
}