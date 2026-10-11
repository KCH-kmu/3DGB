using UnityEngine;

public enum BeetleCollisionZoneType
{
    Head,
    Damage
}

[RequireComponent(typeof(BoxCollider2D))]
public class BeetleCollisionZone : MonoBehaviour
{
    [SerializeField] private BeetleCollisionZoneType zoneType =
        BeetleCollisionZoneType.Head;

    private BeetleMovement beetle;

    private void Awake()
    {
        beetle = GetComponentInParent<BeetleMovement>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerMovement player =
            other.GetComponentInParent<PlayerMovement>();

        if (beetle == null || player == null)
        {
            return;
        }

        if (zoneType == BeetleCollisionZoneType.Head)
        {
            beetle.TryFreezeFromHead(player);
        }
        else
        {
            beetle.HandlePlayerDamage(player);
        }
    }
}
