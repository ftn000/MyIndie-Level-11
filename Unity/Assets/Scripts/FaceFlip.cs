using UnityEngine;

// Превью арта: спрайт огонька нарисован лицом вправо, отражаем по направлению движения (FlamePlayer.face).
public class FaceFlip : MonoBehaviour
{
    public FlamePlayer player;
    SpriteRenderer sr;

    void LateUpdate()
    {
        if (!sr) sr = GetComponent<SpriteRenderer>();
        if (player && sr) sr.flipX = player.face < 0;
    }
}
