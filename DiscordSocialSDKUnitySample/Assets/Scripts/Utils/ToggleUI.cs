using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ToggleUI shows or hides a target UI panel based on the state of the UI Toggle on
/// the same GameObject. Add this alongside a Toggle component and drag the panel's
/// CanvasGroup into the targetCanvasGroup field.
///
/// Visibility is driven through a CanvasGroup (alpha + raycast blocking) rather than
/// SetActive, so the panel's GameObject stays active even while hidden. This matters
/// for panels like the friends list that need to be active to receive Social SDK
/// callbacks and run coroutines from the moment the client connects, not just once
/// the user first opens them.
/// </summary>
[RequireComponent(typeof(Toggle))]
public class ToggleUI : MonoBehaviour
{
    [Tooltip("The CanvasGroup of the UI panel to show when the toggle is on and hide when it is off.")]
    [SerializeField] private CanvasGroup targetCanvasGroup;

    private Toggle toggle;

    void Awake()
    {
        toggle = GetComponent<Toggle>();
    }

    void Start()
    {
        if (targetCanvasGroup == null)
        {
            Debug.LogError($"ToggleUI on '{gameObject.name}' has no targetCanvasGroup assigned. Add a CanvasGroup to the panel you want to show/hide and drag it into the inspector.");
            return;
        }

        toggle.onValueChanged.AddListener(OnToggleChanged);

        // Sync the panel to the toggle's starting value so a toggle that begins "on" shows its panel.
        OnToggleChanged(toggle.isOn);
    }

    void OnDestroy()
    {
        if (toggle != null)
        {
            toggle.onValueChanged.RemoveListener(OnToggleChanged);
        }
    }

    private void OnToggleChanged(bool isOn)
    {
        if (targetCanvasGroup == null)
        {
            return;
        }

        // Keep the GameObject active; only change visibility and interactivity.
        targetCanvasGroup.alpha = isOn ? 1f : 0f;
        targetCanvasGroup.interactable = isOn;
        targetCanvasGroup.blocksRaycasts = isOn;
    }
}
