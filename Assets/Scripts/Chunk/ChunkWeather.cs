using UnityEngine;

[DisallowMultipleComponent]
public sealed class ChunkWeather : MonoBehaviour
{
    [SerializeField] private bool hasSunlight = true;
    [SerializeField] private bool hasRain;
    [SerializeField] private bool canFreezePlayer;

    public bool HasSunlight => hasSunlight;
    public bool HasRain => hasRain;
    public bool CanFreezePlayer => canFreezePlayer;
}
