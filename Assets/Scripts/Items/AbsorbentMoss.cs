using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class AbsorbentMoss : MonoBehaviour
{
    [SerializeField] private BoxCollider2D dryMoss;
    [SerializeField] private BoxCollider2D wetMoss;
    [SerializeField] private Transform skyCheckPoint;
    [SerializeField] private LayerMask solidLayers;
    [SerializeField] private LayerMask blockingLayers;
    [SerializeField, Min(0f)] private float absorptionSeconds = 3f;

    private readonly List<RaycastHit2D> skyHits = new List<RaycastHit2D>();
    private readonly List<Collider2D> overlaps = new List<Collider2D>();
    private ChunkWeather weather;
    private float absorbedSeconds;
    private bool isSaturated;

    private void Awake()
    {
        weather = GetComponentInParent<ChunkWeather>();
        if (weather == null || dryMoss == null || wetMoss == null || skyCheckPoint == null)
        {
            Debug.LogError("AbsorbentMoss requires a parent ChunkWeather and its prefab references.", this);
            enabled = false;
        }
    }

    private void FixedUpdate()
    {
        if (wetMoss.gameObject.activeSelf)
        {
            return;
        }

        if (!isSaturated)
        {
            if (!weather.HasRain || !WeatherMechanismPhysics.HasClearSky(
                skyCheckPoint.position, solidLayers, dryMoss, skyHits))
            {
                return;
            }

            absorbedSeconds += Time.fixedDeltaTime;
            isSaturated = absorbedSeconds >= absorptionSeconds;
        }

        if (isSaturated && WeatherMechanismPhysics.HasRoomFor(
            wetMoss, blockingLayers, dryMoss, overlaps))
        {
            dryMoss.gameObject.SetActive(false);
            wetMoss.gameObject.SetActive(true);
        }
    }
}
