using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HealthBarUI : MonoBehaviour
{
    [Header("References")]
    public PlayerHealth playerHealth;     
    public Image barFill;                 
    public TextMeshProUGUI healthText;    // HealthText
    public Image backgroundPanel;         

    [Header("Bar Colors")]
    public Color colorFull  = Color.green;                        // > 80%
    public Color colorHigh  = Color.yellow;                       // 60–80
    public Color colorMid   = new Color(1f, 0.5f, 0f);            // orange
    public Color colorLow   = Color.red;                          // < 40%

    void Awake()
    {
        if (playerHealth == null)
            playerHealth = FindObjectOfType<PlayerHealth>();
    }

    void OnEnable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += UpdateUI;
            UpdateUI(playerHealth.currentHealth, playerHealth.maxHealth);
        }
    }

    void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.OnHealthChanged -= UpdateUI;
    }

    public void UpdateUI(int current, int max)
    {
        if (max <= 0) max = 1;

        float percent = (float)current / max;

        if (barFill != null)
            barFill.fillAmount = percent;

        if (healthText != null)
            healthText.text = current.ToString() + "/" + max.ToString();

        // Choose colour based on thresholds
        Color c;
        if (percent > 0.8f)        c = colorFull;   // 80–100%
        else if (percent > 0.6f)   c = colorHigh;   // 60–80%
        else if (percent > 0.4f)   c = colorMid;    // 40–60%
        else                       c = colorLow;    // 0–40%

        if (barFill != null)
            barFill.color = c;

        if (backgroundPanel != null)
        {
            Color bg = c;
            bg.a = 0.25f;          // mostly transparent
            backgroundPanel.color = bg;
        }
    }
}
