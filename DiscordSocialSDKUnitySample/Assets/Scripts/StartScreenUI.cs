using UnityEngine;

public class StartScreenUI : MonoBehaviour
{
    public SettingsUI settingsUI;

    public void OpenSettings()
    {
        settingsUI.openSettings();
    }

    public void ExitGame()
    {
        Application.Quit();
    }

    public void StartGame()
    {
        
    }
}
