using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class BlockUndersideSensor : MonoBehaviour
{
    [SerializeField] private InteractiveBlock owner;

    public void Configure(InteractiveBlock block)
    {
        owner = block;
    }

private void OnTriggerEnter2D(Collider2D other)
    {
        NotifyOwner(other);
    }

private void OnTriggerStay2D(Collider2D other)
    {
        NotifyOwner(other);
    }



private void NotifyOwner(Collider2D other)
    {
        if (owner == null)
        {
            return;
        }

        PlayerMovement player =
            other.GetComponentInParent<PlayerMovement>();

        if (player != null)
        {
            owner.TryHitFromBelow(player);
        }
    }
}
