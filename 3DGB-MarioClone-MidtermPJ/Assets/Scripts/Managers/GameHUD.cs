using UnityEngine;
using TMPro;

public class GameHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text livesText;
    [SerializeField] private TMP_Text coinsText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text bossHealthText;
    [SerializeField] private GameObject messagePanel;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField, Min(1f)] private float startingTime = 180f;
    [SerializeField, Min(0)] private int coinScoreValue = 100;

    private int coinCount;
    private int score;
    private float timeRemaining;
    private int displayedSecond = -1;
    private bool timerStopped;
    private bool timeExpired;

    public int CoinCount => coinCount;
    public int Score => score;
    public float TimeRemaining => timeRemaining;

    public void Configure(
        TMP_Text score,
        TMP_Text lives,
        GameObject panel,
        TMP_Text message,
        PlayerHealth health
    )
    {
        scoreText = score;
        livesText = lives;
        messagePanel = panel;
        messageText = message;
        playerHealth = health;
    }

    public void Configure(
        TMP_Text scoreDisplay,
        TMP_Text livesDisplay,
        TMP_Text coinsDisplay,
        TMP_Text timerDisplay,
        GameObject panel,
        TMP_Text message,
        PlayerHealth health,
        float timerSeconds = 180f
    )
    {
        scoreText = scoreDisplay;
        livesText = livesDisplay;
        coinsText = coinsDisplay;
        timerText = timerDisplay;
        messagePanel = panel;
        messageText = message;
        playerHealth = health;
        startingTime = Mathf.Max(1f, timerSeconds);
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
        timeRemaining = startingTime;
        UpdateCoinText();
        UpdateScoreText();
        UpdateTimerText(true);

        if (playerHealth != null)
        {
            OnLivesChanged(playerHealth.CurrentLives);
        }

        if (messagePanel != null)
        {
            messagePanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (timerStopped)
        {
            return;
        }

        timeRemaining = Mathf.Max(0f, timeRemaining - Time.deltaTime);
        UpdateTimerText(false);

        if (timeRemaining > 0f)
        {
            return;
        }

        timerStopped = true;
        timeExpired = true;

        if (playerHealth != null)
        {
            playerHealth.TimeExpired();
        }

        ShowMessage("TIME UP\nPRESS R TO RESTART");
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
        score += coinScoreValue;
        UpdateCoinText();
        UpdateScoreText();
    }

    public void AddScore(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        score += amount;
        UpdateScoreText();
    }

    public void ConfigureBossHealth(TMP_Text display)
    {
        bossHealthText = display;

        if (bossHealthText != null)
        {
            bossHealthText.gameObject.SetActive(false);
        }
    }

    public void ShowBossHealth(int current, int maximum)
    {
        if (bossHealthText == null)
        {
            return;
        }

        bossHealthText.gameObject.SetActive(true);
        bossHealthText.text =
            "BOSS  " + Mathf.Max(0, current) + " / " + Mathf.Max(1, maximum);
    }

    public void ShowBossVictory()
    {
        timerStopped = true;

        if (bossHealthText != null)
        {
            bossHealthText.gameObject.SetActive(false);
        }

        ShowMessage("BOSS DEFEATED!\nENTER: NEXT LEVEL");
    }

    public void ShowLevelComplete()
    {
        timerStopped = true;
        ShowMessage("LEVEL COMPLETE!\nENTER: NEXT LEVEL");
    }

    private void OnLivesChanged(int lives)
    {
        if (livesText != null)
        {
            livesText.text = "LIVES  " + lives;
        }

        if (lives <= 0)
        {
            timerStopped = true;

            if (!timeExpired)
            {
                ShowMessage("GAME OVER\nPRESS R TO RESTART");
            }
        }
    }

    private void UpdateScoreText()
    {
        if (scoreText != null)
        {
            scoreText.text = "SCORE  " + score.ToString("D6");
        }
    }

    private void UpdateCoinText()
    {
        if (coinsText != null)
        {
            coinsText.text = coinCount.ToString("D2");
        }
    }

    private void UpdateTimerText(bool force)
    {
        int seconds = Mathf.CeilToInt(timeRemaining);

        if (!force && seconds == displayedSecond)
        {
            return;
        }

        displayedSecond = seconds;

        if (timerText != null)
        {
            timerText.text = "TIME  " + seconds.ToString("D3");
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
