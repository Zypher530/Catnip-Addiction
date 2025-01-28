using UnityEngine;

public class SetUnityProps : MonoBehaviour
{
    private void Start()
    {
        QualitySettings.vSyncCount = 0; // Set vSyncCount to 0 so that using .targetFrameRate is enabled.
        Application.targetFrameRate = 240;
    }
}
