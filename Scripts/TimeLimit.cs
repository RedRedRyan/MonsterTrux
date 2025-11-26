using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using RaveLands.Collectibles;

namespace RaveLands.GamePlay
{
    public class Timelimit : MonoBehaviour
    {
        [SerializeField] public float startingTime = 20f;
        public Text countDownText;
        [SerializeField] public int goldPoints;
        [SerializeField] public int silverPoints;
        [SerializeField] public int bronzePoints;
        private int points = 0;
        public AudioSource timeAudio;

        // Reference to GameOver script instead of GameObject
        public GameOver gameOverHandler;

        private float currentTime = 0f;
        private CollectibleCount collectibleCount; // To access collected points
        
        // Track if game is over to prevent multiple triggers
        private bool isGameOver = false;

        // Hint text reference - assign your existing TMP Text in inspector
        [SerializeField] private TMP_Text hintText;

        // Countdown warning color
        [SerializeField] private Color warningColor = Color.red; // Color for timer when below 10 seconds
        private Color originalTextColor; // Store original text color
        private bool isWarningActive = false;

        // Timer for beeping every second when below 10s
        private float nextBeepTime = 0f;

        void Start()
        {
            currentTime = startingTime;
            collectibleCount = FindAnyObjectByType<CollectibleCount>();
            isGameOver = false;
            
            // Store original text color
            if (countDownText != null)
            {
                originalTextColor = countDownText.color;
            }
            
            // Ensure time scale is reset when starting
            Time.timeScale = 1f;

            // Show hint for 2 seconds when race starts
            StartCoroutine(ShowHintForSeconds(2f));
        }

        void Update()
        {
            if (isGameOver) return;
            
            currentTime -= Time.deltaTime;
            countDownText.text = currentTime.ToString("0");

            // Handle countdown color change
            HandleCountdownWarning();

            // Beep every second when below 10 seconds
            if (currentTime <= 10f && currentTime > 0f)
            {
                if (Time.time >= nextBeepTime)
                {
                    if (timeAudio != null)
                    {
                        timeAudio.Play();
                    }
                    nextBeepTime = Time.time + 1f; // Schedule next beep 1 second later
                }
            }

            // Handle Game Over
            if (currentTime <= 0)
            {
                currentTime = 0;
                isGameOver = true;

                // Optional: Final beep
                if (timeAudio != null)
                {
                    timeAudio.Play();
                }

                // Get the current points from CollectibleCount
                points = collectibleCount.count;

                // Determine reward and trigger GameOver
                DetermineReward();

                // Stop car audio
                SCC_Audio[] allAudioControllers = FindObjectsByType<SCC_Audio>(FindObjectsSortMode.None);
                foreach (SCC_Audio audioController in allAudioControllers)
                {
                    audioController.enabled = false;
                }

                Time.timeScale = 0f;
            }
        }

        // Coroutine to show hint for specified seconds
        private IEnumerator ShowHintForSeconds(float seconds)
        {
            if (hintText != null)
            {
                hintText.enabled = true;
                yield return new WaitForSeconds(seconds);
                hintText.enabled = false;
            }
        }

        // Handle countdown color change when time is below 10 seconds
        private void HandleCountdownWarning()
        {
            if (currentTime <= 10f && currentTime > 0f)
            {
                if (!isWarningActive)
                {
                    isWarningActive = true;
                    if (countDownText != null)
                    {
                        countDownText.color = warningColor;
                    }
                }
            }
            else if (isWarningActive && currentTime > 10f)
            {
                isWarningActive = false;
                if (countDownText != null)
                {
                    countDownText.color = originalTextColor;
                }
            }
        }

        void DetermineReward()
        {
            if (points >= goldPoints)
            {
                gameOverHandler.ShowGameOver(GameOver.RaceType.Timelimit, true, "Gold");
            }
            else if (points >= silverPoints)
            {
                gameOverHandler.ShowGameOver(GameOver.RaceType.Timelimit, true, "Silver");
            }
            else if (points >= bronzePoints)
            {
                gameOverHandler.ShowGameOver(GameOver.RaceType.Timelimit, true, "Bronze");
            }
            else
            {
                gameOverHandler.ShowGameOver(GameOver.RaceType.Timelimit, false, "");
            }
        }
        
        // Clean up when destroyed (scene changes)
        void OnDestroy()
        {
            Time.timeScale = 1f;
        }
    }
}
