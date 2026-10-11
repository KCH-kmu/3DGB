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
    private Collider2D zoneCollider;

    private void Awake()
    {
        beetle = GetComponentInParent<BeetleMovement>();
        zoneCollider = GetComponent<Collider2D>();
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
            beetle.TryFreezeFromHead(
                player,
                other,
                zoneCollider
            );
        }
        else
        {
            beetle.HandlePlayerDamage(player);
        }
    }
}
