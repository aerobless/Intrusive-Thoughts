using UnityEngine;
using TMPro;

public class AgentTextOutput : MonoBehaviour
{
    [SerializeField] private Canvas speechBubbleCanvas;
    [SerializeField] private TMP_Text speechText;
    [SerializeField] private float displayDuration = 3f;
    private Camera mainCamera;
    private float timer;

    void Start()
    {
        mainCamera = Camera.main;
        speechBubbleCanvas.enabled = false;
    }

    void Update()
    {
        if (!speechBubbleCanvas) return;
        
        speechBubbleCanvas.transform.LookAt(mainCamera.transform);
        speechBubbleCanvas.transform.Rotate(0, 180, 0);

        if (speechBubbleCanvas.enabled)
        {
            timer -= Time.deltaTime;
            if (timer <= 0) speechBubbleCanvas.enabled = false;
        }
    }

    public void ShowSpeech(string text, float duration = -1f)
    {
        if (!speechText) return;
        speechText.text = text;
        speechBubbleCanvas.enabled = true;
        timer = duration > 0 ? duration : displayDuration;
    }
}
