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
        HandlePlayerCollision(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // 위로 지나가며 먼저 들어온 뒤 낙하하는 경우에도
        // 머리 밟기 조건을 계속 검사한다.
        if (zoneType == BeetleCollisionZoneType.Head)
        {
            HandlePlayerCollision(other);
        }
    }

    private void HandlePlayerCollision(Collider2D other)
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
