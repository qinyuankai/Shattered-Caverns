using UnityEngine;

public enum LevelFlowTriggerAction
{
    CompleteLevel,
    FailLevel
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
        if (hasTriggered || !IsPlayer(other)) return;

        GameFlowController flow = GameFlowController.Instance;
        if (flow == null || flow.State != GameFlowState.Playing) return;

        if (triggerOnce) hasTriggered = true;
        if (action == LevelFlowTriggerAction.CompleteLevel) flow.CompleteLevel();
        else flow.FailLevel();
    }

    private static bool IsPlayer(Collider2D other)
    {
        return other.CompareTag("Player")
            || other.attachedRigidbody != null && other.attachedRigidbody.CompareTag("Player");
    }
}
