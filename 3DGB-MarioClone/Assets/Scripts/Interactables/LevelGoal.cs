using UnityEngine;

public class LevelGoal : MonoBehaviour
{
    private bool completed;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (completed)
        {
            return;
        }

        PlayerMovement player =
            other.GetComponentInParent<PlayerMovement>();

        if (player == null)
        {
            return;
        }

        completed = true;

        Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();

        if (playerBody != null)
        {
            playerBody.velocity = Vector2.zero;
        }

        player.enabled = false;

        GameHUD hud = FindObjectOfType<GameHUD>();

        if (hud != null)
        {
            hud.ShowLevelComplete();
        }
    }
}
