using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class Updraft : MonoBehaviour
{
    [SerializeField, Min(0f)] private float riseSpeed = 2f;

    public float RiseSpeed => riseSpeed;

    private void Reset()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }
}
