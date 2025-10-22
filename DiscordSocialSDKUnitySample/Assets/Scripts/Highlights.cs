using UnityEngine;
using UnityEngine.UI;
using TMPro; // needed for TextMeshPro
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class HighlightsManager : MonoBehaviour
{
    [System.Serializable]
    public class TutorialStep
    {
        public RectTransform target;
        public string instruction;
    }

    public List<TutorialStep> steps;
    public Image highlightBox;
    public TextMeshProUGUI instructionText;
    public Button nextButton;
    // public Button deleteTokenButton;
    public Image overlay;
    public SettingsUI settingsUI;
    public GameObject panel; 

    private int currentStep = 0;

    void Start()
    {
        Debug.Log("Highlight Manager started!");
        //deleteTokenButton.onClick.AddListener(DeleteRefreshToken);
        // if (PlayerPrefs.HasKey("RefreshToken")) {
        //     ShowStep(1);
        // }
        //openSettings();
         nextButton.onClick.AddListener(() =>
    {
        Debug.Log("[Highlight Debug] Next button clicked!");
        NextStep();
    });
        ShowStep(0);
    }

    void openSettings() {
        settingsUI.openSettings();
    }

    void ShowStep(int index)
    {
        if (index >= steps.Count)
        {
            Debug.Log("Tutorial completed — disabling all tutorial UI.");
            highlightBox.gameObject.SetActive(false);
            overlay.gameObject.SetActive(false);
            instructionText.gameObject.SetActive(false);
            nextButton.gameObject.SetActive(false);
            panel.gameObject.SetActive(false);
            return;
        }
        Debug.Log("Showing step: " + index);

        var step = steps[index];
        instructionText.text = step.instruction;

        // ====== Highlight positioning logic ======

    Canvas canvas = highlightBox.canvas;
    RectTransform canvasRect = canvas.GetComponent<RectTransform>();
    Camera cam = canvas.worldCamera;

    RectTransform targetRect = step.target;

    // --- Get world-space corners of the target ---
    Vector3[] corners = new Vector3[4];
    targetRect.GetWorldCorners(corners);

    // --- Compute world-space center of target ---
    Vector3 worldCenter = (corners[0] + corners[2]) / 2f;

    // --- Convert to screen-space ---
    Vector2 screenCenter = RectTransformUtility.WorldToScreenPoint(cam, worldCenter);

    // --- Convert screen-space to local point in highlight canvas ---
    Vector2 localPoint;
    RectTransformUtility.ScreenPointToLocalPointInRectangle(
        canvasRect,
        screenCenter,
        cam,
        out localPoint
    );

    // --- Apply position to highlight box ---
    highlightBox.rectTransform.anchoredPosition = localPoint;

    // ====== Compute correct highlight box size in canvas space ======
    Vector2 bl, tr;
    RectTransformUtility.ScreenPointToLocalPointInRectangle(
        canvasRect,
        RectTransformUtility.WorldToScreenPoint(cam, corners[0]),
        cam,
        out bl
    );
    RectTransformUtility.ScreenPointToLocalPointInRectangle(
        canvasRect,
        RectTransformUtility.WorldToScreenPoint(cam, corners[2]),
        cam,
        out tr
    );

    Vector2 canvasSize = tr - bl;
    highlightBox.rectTransform.sizeDelta = new Vector2(Mathf.Abs(canvasSize.x), Mathf.Abs(canvasSize.y));

    Debug.Log($"Highlight anchoredPosition={highlightBox.rectTransform.anchoredPosition} | sizeDelta={highlightBox.rectTransform.sizeDelta}");

    }

    private bool IsInsideViewport(Vector2 screenPos, Vector2 elementSize)
    {
        // Get current screen size
        float screenWidth = Screen.width;
        float screenHeight = Screen.height;

        // Compute boundaries of the element in screen space
        float halfWidth = elementSize.x / 2f;
        float halfHeight = elementSize.y / 2f;

        bool withinHorizontal = (screenPos.x - halfWidth >= 0) && (screenPos.x + halfWidth <= screenWidth);
        bool withinVertical = (screenPos.y - halfHeight >= 0) && (screenPos.y + halfHeight <= screenHeight);

        return withinHorizontal && withinVertical;
    }

    void NextStep()
    {
        Debug.Log("Next step!");
        currentStep++;
        ShowStep(currentStep);
    }

    void DeleteRefreshToken()
    {
        PlayerPrefs.DeleteKey("RefreshToken");
        PlayerPrefs.Save();
        Debug.Log("RefreshToken deleted successfully");
        
        SceneManager.LoadScene("Example");
    }
}
