using UnityEngine;

// Люк в полу: дети-плиты держат пол. Сгорела верёвка щеколды (Rope.Burned с тем же id): плиты исчезают, игрок падает.
// Когда игрок провалился ниже люка, плиты возвращаются: назад дороги нет.
public class Hatch : MonoBehaviour
{
    public string id;
    public bool Open { get; private set; }

    void OnEnable() => Rope.Burned += OnBurned;
    void OnDisable() => Rope.Burned -= OnBurned;

    void OnBurned(string rope) { if (rope == id && !Open) Set(true); }

    void Update()
    {
        if (Open && Flame.I && Flame.I.transform.position.y < transform.position.y - 3f) Set(false);
    }

    void Set(bool open)
    {
        Open = open;
        foreach (Transform c in transform) { c.gameObject.SetActive(!open); Fx.Steam(c.position, 3); }
    }
}
