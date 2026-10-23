using UnityEngine;
using UnityEngine.UI;

public class GameHUD : MonoBehaviour
{
    [SerializeField] private Text scoreText;
    [SerializeField] private Text livesText;
    [SerializeField] private GameObject messagePanel;
    [SerializeField] private Text messageText;
    [SerializeField] private PlayerHealth playerHealth;

    private int coinCount;

    public void Configure(
        Text score,
        Text lives,
        GameObject panel,
        Text message,
        PlayerHealth health
    )
    {
        scoreText = score;
        livesText = lives;
        messagePanel = panel;
        messageText = message;
        playerHealth = health;
    }

    private void OnEnable()
    {
        if (playerHealth == null)
        {
            playerHealth = FindObjectOfType<PlayerHealth>();
        }

        if (playerHealth != null)
        {
            playerHealth.LivesChanged += OnLivesChanged;
        }
    }

    private void Start()
    {
        UpdateScoreText();

        if (playerHealth != null)
        {
            OnLivesChanged(playerHealth.CurrentLives);
        }

        if (messagePanel != null)
        {
            messagePanel.SetActive(false);
        }
    }

    private void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.LivesChanged -= OnLivesChanged;
        }
    }

    public void AddCoin()
    {
        coinCount++;
        UpdateScoreText();
    }

    public void ShowLevelComplete()
    {
        ShowMessage("LEVEL COMPLETE!\nPRESS R TO RESTART");
    }

    private void OnLivesChanged(int lives)
    {
        if (livesText != null)
        {
            livesText.text = "LIVES  " + lives;
        }

        if (lives <= 0)
        {
            ShowMessage("GAME OVER\nPRESS R TO RESTART");
        }
    }

    private void UpdateScoreText()
    {
        if (scoreText != null)
        {
            scoreText.text = coinCount.ToString();
        }
    }

    private void ShowMessage(string message)
    {
        if (messageText != null)
        {
            messageText.text = message;
        }

        if (messagePanel != null)
        {
            messagePanel.SetActive(true);
        }
    }
}
