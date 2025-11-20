using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class BackgroundScroller : MonoBehaviour
{
    public Vector2 scrollSpeed = new Vector2(0.03f, 0f);
    private RawImage rawImage;
    private Rect uvRect;

    void Start()
    {
        rawImage = GetComponent<RawImage>();
        uvRect = rawImage.uvRect;
    }

    void Update()
    {
        uvRect.x += scrollSpeed.x * Time.unscaledDeltaTime; // unscaled so music/menus keep moving when paused
        uvRect.y += scrollSpeed.y * Time.unscaledDeltaTime;
        rawImage.uvRect = uvRect;
    }
}
