using UnityEngine;

public class SettingsUI : MonoBehaviour
{
    public GameObject friendsList;
    public GameObject settingsPanel;

    public void openSettings() {
        settingsPanel.SetActive(true);
        Debug.Log("Settings panel opened!");
    }
}
