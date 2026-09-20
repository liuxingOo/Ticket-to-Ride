using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    public void OnSinglePlayerClick()
    {
        SceneManager.LoadScene("GameScene");
    }
}