using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace RaveLands.GamePlay
{
    public class GameOver : MonoBehaviour
    {
        public enum RaceType { Timelimit, Rush, Run }
        public GameObject gameOverPanel;
        public GameObject youwinPanel;
        public GameObject youlosePanel;

        // Medal objects within the youwinPanel
        public GameObject goldMedal;
        public GameObject silverMedal;
        public GameObject bronzeMedal;
        
        // Add a restart button reference if you have one
        public Button restartButton;

        void Start()
        {
            // Add restart listener if button exists
            if (restartButton != null)
            {
                restartButton.onClick.AddListener(RestartGame);
            }
        }

        public void ShowGameOver(RaceType raceType, bool isWin, string medal = "")
        {
            if (isWin)
            {
                // Show the game over panel with the win message
                gameOverPanel.SetActive(true);
                youwinPanel.SetActive(true);
                youlosePanel.SetActive(false);

                // Activate the appropriate medal
                goldMedal.SetActive(medal == "Gold");
                silverMedal.SetActive(medal == "Silver");
                bronzeMedal.SetActive(medal == "Bronze");
            }
            else
            {
                gameOverPanel.SetActive(true);
                youwinPanel.SetActive(false);
                youlosePanel.SetActive(true);
            }

            // Pause all engine audio when game over is shown
            PauseEngineAudio();
        }
        
        // Method to pause engine audio
        private void PauseEngineAudio()
        {
            SCC_Audio[] allAudioControllers = FindObjectsByType<SCC_Audio>(FindObjectsSortMode.None);
            foreach (SCC_Audio audioController in allAudioControllers)
            {
                audioController.SetPaused(true);
            }
        }
        
        // Method to unpause engine audio
        private void UnpauseEngineAudio()
        {
            SCC_Audio[] allAudioControllers = FindObjectsByType<SCC_Audio>(FindObjectsSortMode.None);
            foreach (SCC_Audio audioController in allAudioControllers)
            {
                audioController.SetPaused(false);
            }
        }
        
        // Add this method to handle restarting from game over
        public void RestartGame()
        {
            // Reset time scale and unpause audio
            Time.timeScale = 1;
            
            // Unpause engine audio properly
            UnpauseEngineAudio();
            
            // Reload the current scene
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        
        // Optional: Add a method to go back to main menu
        public void ReturnToMainMenu()
        {
            Time.timeScale = 1;
            UnpauseEngineAudio();
            SceneManager.LoadScene(0); // Assuming scene 0 is main menu
        }
    }
}