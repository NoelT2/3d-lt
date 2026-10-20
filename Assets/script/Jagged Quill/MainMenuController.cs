using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string practiceRangeSceneName = "Jagged Quill PR";
    public void OnPlayPressed()
    {
        SceneManager.LoadScene(practiceRangeSceneName);
    }

    public void OnQuitPressed()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // Application.Quit() does nothing in the Editor
#else
        Application.Quit();
#endif
    }
}