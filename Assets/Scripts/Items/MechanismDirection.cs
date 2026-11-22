using UnityEngine;

public sealed class MechanismDirection : MonoBehaviour
{
    public Vector2 WorldDirection => ((Vector2)transform.TransformVector(Vector3.up)).normalized;

    public bool IsFacing(Vector2 worldDirection)
    {
        return Mathf.Approximately(Vector2.Dot(WorldDirection, worldDirection.normalized), 1f);
    }
}
