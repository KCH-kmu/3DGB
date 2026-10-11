using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class BeetleReturnCollision : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        BeetleMovement beetle =
            other.GetComponentInParent<BeetleMovement>();

        if (beetle != null)
        {
            beetle.TurnAround();
        }
    }
}
