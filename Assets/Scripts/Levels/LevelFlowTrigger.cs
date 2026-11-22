using UnityEngine;

public enum LevelFlowTriggerAction
{
    CompleteLevel,
    FailLevel,
    RespawnPlayer,
    RestartLevel
}

[RequireComponent(typeof(Collider2D))]
public class LevelFlowTrigger : MonoBehaviour
{
    [SerializeField] private LevelFlowTriggerAction action;
    [SerializeField] private bool triggerOnce = true;

    private bool hasTriggered;

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (action != LevelFlowTriggerAction.FailLevel && other.isTrigger) return;
        if (hasTriggered || !IsPlayer(other)) return;

        GameFlowController flow = GameFlowController.Instance;
        if (flow == null || flow.State != GameFlowState.Playing) return;

        if (triggerOnce) hasTriggered = true;
        switch (action)
        {
            case LevelFlowTriggerAction.CompleteLevel:
                flow.CompleteLevel();
                break;
            case LevelFlowTriggerAction.FailLevel:
                flow.FailLevel();
                break;
            case LevelFlowTriggerAction.RespawnPlayer:
                flow.RespawnPlayer();
                break;
            case LevelFlowTriggerAction.RestartLevel:
                flow.RestartLevel();
                break;
        }
    }

    private static bool IsPlayer(Collider2D other)
    {
        return other.CompareTag("Player")
            || other.attachedRigidbody != null && other.attachedRigidbody.CompareTag("Player");
    }
}
