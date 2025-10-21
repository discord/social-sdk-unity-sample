using UnityEngine;
using UnityEngine.UI;
using TMPro; // needed for TextMeshPro
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Collections;

public class HighlightsManager : MonoBehaviour
{
    [System.Serializable]
    public class TutorialStep
    {
        public RectTransform target;
        public string instruction;

        [System.NonSerialized] 
        public System.Action onStep;
    }

    public List<TutorialStep> steps;
    public Image highlightBox;
    public TextMeshProUGUI instructionText;
    public Button nextButton;
    // public Button deleteTokenButton;
    public Image overlay;
    public SettingsUI settingsUI;
    public FriendListAnimation friendListAnimator;
    public GameObject panel;

    public GameObject highlightPanel;
    public GameObject oauthImage;

    private int currentStep = 0;

    private RectTransform currentTarget;

    void Start()
    {
        steps[0].onStep = () => { friendListAnimator.ShowFriendsList(); };
        steps[2].onStep = () => { friendListAnimator.HideFriendsList(); };
        steps[3].onStep = () => { settingsUI.openSettings(); };
        steps[6].onStep = () => { settingsUI.CloseAll(); settingsUI.OpenConnectToDiscord(); };
        steps[9].onStep = () => { settingsUI.CloseAll(); oauthImage.SetActive(true); };
        steps[11].onStep = () => { oauthImage.SetActive(false); settingsUI.ShowAccountLinkedOnly(); };
        steps[12].onStep = () => { settingsUI.CloseAll(); };

        nextButton.onClick.AddListener(() =>
   {
       Debug.Log("[Highlight Debug] Next button clicked!");
       steps[currentStep].onStep?.Invoke();
       NextStep();
   });

    }
    
    public void StartTutorial()
    {
        highlightPanel.SetActive(false);
        panel.gameObject.SetActive(false);
        highlightBox.gameObject.SetActive(false);
        ShowStep(0);
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
            highlightPanel.gameObject.SetActive(false);
            panel.gameObject.SetActive(false);
            return;
        }
        highlightPanel.SetActive(true);
        panel.gameObject.SetActive(true);
        highlightBox.gameObject.SetActive(true);
        Debug.Log("Showing step: " + index);

        var step = steps[index];
        instructionText.text = step.instruction;

        currentTarget = step.target;
    }

    void Update()
    {
        if (currentTarget == null || highlightBox == null) return;

        Canvas canvas = highlightBox.canvas;
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();

        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, currentTarget);

        Vector2 targetPos = new Vector2(bounds.center.x, bounds.center.y);
        Vector2 targetSize = new Vector2(Mathf.Abs(bounds.size.x), Mathf.Abs(bounds.size.y));

        float speed = 8f;
        float t = 1f - Mathf.Exp(-speed * Time.deltaTime);

        var rt = highlightBox.rectTransform;
        rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, targetPos, t);
        rt.sizeDelta = Vector2.Lerp(rt.sizeDelta, targetSize, t);
    }

    void NextStep()
    {
        Debug.Log("Next step!");
        currentStep++;
        ShowStep(currentStep);
    }
}
