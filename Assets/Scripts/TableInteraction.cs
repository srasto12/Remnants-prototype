using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class TableInteraction : MonoBehaviour
{
    public string shopSceneName = "ShopScene";
    public GameObject pressEText; // UI text shown when near table
    private bool playerNearby = false;

    private void Start()
    {
        if (pressEText != null)
            pressEText.SetActive(false);
    }

    private void Update()
    {
        if (playerNearby && Input.GetKeyDown(KeyCode.E))
        {
            PlayerDataManager.Instance.SaveLastScene();
            SceneManager.LoadScene(shopSceneName);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNearby = true;
            if (pressEText != null)
                pressEText.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNearby = false;
            if (pressEText != null)
                pressEText.SetActive(false);
        }
    }
}
