using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    [SerializeField] GameObject pauseMenu;
    
    public void Pause()
    {
        pauseMenu.SetActive(true);
        Time.timeScale = 0;
        
        // Pause all SCC_Audio components using the new method
        SCC_Audio[] allAudioControllers = FindObjectsByType<SCC_Audio>(FindObjectsSortMode.None);
        foreach (SCC_Audio audioController in allAudioControllers)
        {
            audioController.SetPaused(true);
        }
    }
    
    public void Home()
    {
        // Reset time scale before loading scene
        Time.timeScale = 1;
        
        // Unpause audio before changing scenes
        SCC_Audio[] allAudioControllers = FindObjectsByType<SCC_Audio>(FindObjectsSortMode.None);
        foreach (SCC_Audio audioController in allAudioControllers)
        {
            audioController.SetPaused(false);
        }
        
        SceneManager.LoadScene(0);
    }
    
    public void Resume()
    {
        pauseMenu.SetActive(false);
        Time.timeScale = 1;
        
        // Resume all SCC_Audio components
        SCC_Audio[] allAudioControllers = FindObjectsByType<SCC_Audio>(FindObjectsSortMode.None);
        foreach (SCC_Audio audioController in allAudioControllers)
        {
            audioController.SetPaused(false);
        }
    }
    
    public void Restart()
    {
        // CRITICAL: Reset time scale before reloading scene
        Time.timeScale = 1;
        
        // Unpause audio before restarting
        SCC_Audio[] allAudioControllers = FindObjectsByType<SCC_Audio>(FindObjectsSortMode.None);
        foreach (SCC_Audio audioController in allAudioControllers)
        {
            audioController.SetPaused(false);
        }
        
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}