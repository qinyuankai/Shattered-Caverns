using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SunlightBud : MonoBehaviour
{
    [SerializeField] private GameObject closedBud;
    [SerializeField] private BoxCollider2D openPlatform;
    [SerializeField] private Transform skyCheckPoint;
    [SerializeField] private LayerMask solidLayers;
    [SerializeField] private LayerMask blockingLayers;

    private readonly List<RaycastHit2D> skyHits = new List<RaycastHit2D>();
    private readonly List<Collider2D> overlaps = new List<Collider2D>();
    private ChunkWeather weather;

    private void Awake()
    {
        weather = GetComponentInParent<ChunkWeather>();
        if (weather == null || closedBud == null || openPlatform == null || skyCheckPoint == null)
        {
            Debug.LogError("SunlightBud requires a parent ChunkWeather and its prefab references.", this);
            enabled = false;
        }
    }

    private void FixedUpdate()
    {
        bool shouldOpen = weather.HasSunlight && WeatherMechanismPhysics.HasClearSky(
            skyCheckPoint.position, solidLayers, openPlatform, skyHits);

        if (shouldOpen == openPlatform.gameObject.activeSelf)
        {
            return;
        }

        if (shouldOpen && !WeatherMechanismPhysics.HasRoomFor(
            openPlatform, blockingLayers, null, overlaps))
        {
            return;
        }

        openPlatform.gameObject.SetActive(shouldOpen);
        closedBud.SetActive(!shouldOpen);
    }
}
