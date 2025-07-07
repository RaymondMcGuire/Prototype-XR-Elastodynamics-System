using UnityEngine;

public class FPSCounter : MonoBehaviour
{
    // Variable for smoothing deltaTime
    private float deltaTime = 0.0f;

    // Variables for calculating average FPS
    private int frameCount = 0;
    private float totalTime = 0.0f;
    private float averageFPS = 0.0f;
    public float updateInterval = 1.0f; // Time interval for updating average FPS

    // Cached GUI style and display area
    private GUIStyle style;
    private Rect rect;

    // Initialize GUIStyle and Rect in Start() (rect's height is expanded to display two lines of text)
    void Start()
    {

        // Disable vSync to unlock frame rate
        QualitySettings.vSyncCount = 0;
        // Optionally, set targetFrameRate to 0 (0 means no limit)
        Application.targetFrameRate = 0;

        style = new GUIStyle();
        style.alignment = TextAnchor.UpperLeft;
        style.fontSize = Screen.height * 2 / 100;
        // Optimized font color: Changed to green for improved readability
        style.normal.textColor = Color.green;

        // Expand the rect height to display more information
        rect = new Rect(10, 10, Screen.width, Screen.height * 5 / 100);
    }

    // Per-frame update
    void Update()
    {
        // Exponential smoothing to reduce instantaneous fluctuations
        deltaTime += (Time.deltaTime - deltaTime) * 0.1f;

        // Accumulate data for calculating average FPS
        frameCount++;
        totalTime += Time.deltaTime;

        // When the accumulated time exceeds the update interval, calculate the average FPS and reset counters
        if (totalTime >= updateInterval)
        {
            averageFPS = frameCount / totalTime;
            frameCount = 0;
            totalTime = 0.0f;
        }

        // Check if the Escape key was pressed
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // If running in the Unity Editor, stop play mode
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            // Quit the application when built
            Application.Quit();
#endif
        }
    }

    // Display FPS information in OnGUI()
    void OnGUI()
    {
        // Calculate instantaneous FPS for display
        float msec = deltaTime * 1000.0f;
        float instantFPS = 1.0f / deltaTime;
        string text = string.Format("Instant: {0:0.0} ms ({1:0.} fps)\nAverage: {2:0.} fps", msec, instantFPS, averageFPS);
        GUI.Label(rect, text, style);
    }
}
