using UnityEngine;

// Выход из зоны: вошёл в объём (transform.position, localScale), экран гаснет ~0.6 с, на середине телепорт на старт зоны target
// (чекпоинт там же). Если зоны target ещё нет, показываем «Продолжение следует». Отменяется, если игрок вышел из объёма до телепорта.
public class ZoneExit : Interactable
{
    public int target;
    const float Half = 0.3f;
    float t = -1, fade;
    bool jumped;

    bool Inside(FlamePlayer p) => new Bounds(transform.position, transform.localScale).Contains(p.transform.position);

    public override void Tick(FlamePlayer p, Flame f, float dt)
    {
        if (t < 0) { if (!Inside(p)) return; t = 0; jumped = false; }
        if (!jumped && !Inside(p)) { t = -1; fade = 0; return; } // умер и респаунился, не телепортируем
        t += dt;
        fade = Mathf.Clamp01((t < Half ? t : 2 * Half - t) / Half);
        if (!jumped && t >= Half)
        {
            jumped = true;
            if (!GameManager.I.JumpToZone(target)) { GameManager.I.Win(); t = -1; fade = 0; return; }
        }
        if (t >= 2 * Half) { t = -1; fade = 0; }
    }

    void OnGUI()
    {
        if (fade <= 0) return;
        GUI.color = new Color(0, 0, 0, fade); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture); GUI.color = Color.white;
    }
}
