using UnityEngine;

public class LevelGoal : MonoBehaviour
{
    [SerializeField] private AudioClip levelCompleteSound;

    private bool completed;
    private AudioSource audioSource;

    public void Configure(AudioClip sound)
    {
        levelCompleteSound = sound;
    }

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

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

        if (levelCompleteSound != null)
        {
            audioSource.PlayOneShot(levelCompleteSound, 0.9f);
        }

        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();

        if (playerHealth != null)
        {
            playerHealth.CompleteLevel();
        }
        else
        {
            Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();

            if (playerBody != null)
            {
                playerBody.velocity = Vector2.zero;
                playerBody.simulated = false;
            }

            player.enabled = false;
        }

        GameHUD hud = FindObjectOfType<GameHUD>();

        if (hud != null)
        {
            hud.ShowLevelComplete();
        }
    }
}
