using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Rush : MonoBehaviour
{

    public static int MinutesTimer;
    public static int SecondsTimer;
    public static float MiliTimer;
    public static string MilliDisplay;

    public GameObject minutesBox;
    public GameObject secondsBox;
    public GameObject miliBox;
    public static float countdownText;
    public GameObject winScreen;
    public GameObject loseScreen;

    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
               MiliTimer += Time.deltaTime * 10;
        MilliDisplay = MiliTimer.ToString("F0");
        miliBox.GetComponent<Text>().text = MilliDisplay;

        if (MiliTimer >= 10)
        {
            MiliTimer = 0;
            SecondsTimer += 1;
        }

        // Update secondsBox text with two-digit formatting
        secondsBox.GetComponent<Text>().text = SecondsTimer.ToString("00") + ".";

        if (SecondsTimer >= 60)
        {
            SecondsTimer = 0;
            MinutesTimer += 1;
        }

        // Update minutesBox text with two-digit formatting
        minutesBox.GetComponent<Text>().text = MinutesTimer.ToString("00") + ":"; 
    }
}
