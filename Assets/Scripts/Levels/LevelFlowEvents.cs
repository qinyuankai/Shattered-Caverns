using UnityEngine;

public class LevelFlowEvents : MonoBehaviour
{
    public void CompleteLevel()
    {
        GameFlowController.Instance?.CompleteLevel();
    }

    public void FailLevel()
    {
        GameFlowController.Instance?.FailLevel();
    }

    public void RestartLevel()
    {
        GameFlowController.Instance?.RestartLevel();
    }
}
