using UnityEngine;

// Люк в полу: дети-плиты держат пол. Сгорела верёвка щеколды (Rope.Burned с тем же id): плиты исчезают, игрок падает.
// Обратно люк не закрывается: шахта выше любого прыжка (тест), подняться нельзя, а закрытый люк мог запереть уровень (P-7).
public class Hatch : MonoBehaviour
{
    public string id;
    public bool Open { get; private set; }

    void OnEnable() => Rope.Burned += OnBurned;
    void OnDisable() => Rope.Burned -= OnBurned;

    void OnBurned(string rope)
    {
        if (rope != id || Open) return;
        Open = true; GameAudio.Play(Snd.Hatch, transform.position);
        foreach (Transform c in transform) { c.gameObject.SetActive(false); Fx.Steam(c.position, 3); }
    }
}
