using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponManager : MonoBehaviour
{
    // 0: Bat, 1: Sword, 2: Pistol, 3: AK
    public GameObject[] weapons; 

    void Start()
    {
        SelectWeapon(0); // Start with Bat
    }

    void Update()
    {
        
        if (Keyboard.current.digit1Key.wasPressedThisFrame) 
        {
            Debug.Log("Key 1 Pressed! Switching to Bat.");
            SelectWeapon(0); 
        }
        if (Keyboard.current.digit2Key.wasPressedThisFrame) 
        {
            Debug.Log("Key 2 Pressed! Switching to Sword.");
            SelectWeapon(1); 
        }
        if (Keyboard.current.digit3Key.wasPressedThisFrame) 
        {
            Debug.Log("Key 3 Pressed! Switching to Pistol.");
            SelectWeapon(2); 
        }
        if (Keyboard.current.digit4Key.wasPressedThisFrame) 
        {
            Debug.Log("Key 4 Pressed! Switching to AK.");
            SelectWeapon(3); 
        }
    }

    void SelectWeapon(int index)
    {
        for (int i = 0; i < weapons.Length; i++)
        {
            if (i < weapons.Length && weapons[i] != null)
            {
                weapons[i].SetActive(i == index);
            }
        }
    }
}