using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [SerializeField]
    private string gameSceneName = "IsometricSetupScene";

    public void NewGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    public void ContinueGame()
    {
        Debug.Log("No saved game is available yet.");
    }
}