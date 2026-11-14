using UnityEngine;

// Мост лежит скрытым, пока не сгорит верёвка-триггер с тем же id. Падает 0.4 с сверху без коллайдеров;
// встав на место, давит игрока под собой (Flame.Kill) и становится опорой.
public class Bridge : MonoBehaviour
{
    public const float FallTime = 0.4f, FallHeight = 4f;
    public string id;
    float t = -1;
    public bool Landed => t >= FallTime;
    Vector3[] home;
    void Start() { foreach (Transform c in transform) c.gameObject.SetActive(false); }
    void OnEnable() => Rope.Burned += Drop;
    void OnDisable() => Rope.Burned -= Drop;

    void Drop(string rope)
    {
        if (rope != id || t >= 0) return;
        home = new Vector3[transform.childCount];
        for (int i = 0; i < home.Length; i++)
        {
            var c = transform.GetChild(i); home[i] = c.localPosition;
            c.localPosition += Vector3.up * FallHeight;
            foreach (var col in c.GetComponents<Collider>()) col.enabled = false; // падающая плита не толкает игрока
            c.gameObject.SetActive(true); Fx.Steam(c.position, 3);
        }
        t = 0;
    }

    void Update()
    {
        if (t < 0 || t >= FallTime) return;
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / FallTime); k *= k;
        for (int i = 0; i < home.Length; i++) transform.GetChild(i).localPosition = home[i] + Vector3.up * FallHeight * (1 - k);
        if (t >= FallTime) Land();
    }

    void Land()
    {
        var pl = FindFirstObjectByType<FlamePlayer>();
        var body = pl ? new Bounds(pl.transform.position, new Vector3(pl.H * 1.9f, pl.H * 1.9f, 1)) : default;
        for (int i = 0; i < home.Length; i++)
        {
            var c = transform.GetChild(i); c.localPosition = home[i];
            foreach (var col in c.GetComponents<Collider>()) col.enabled = true;
            var r = c.GetComponent<Renderer>();
            if (pl && r && r.bounds.Intersects(body) && !pl.flame.Dead) { pl.flame.Kill(); Fx.Steam(pl.transform.position, 12); Fx.Spark(pl.transform.position, 12, 4); }
        }
        Physics.SyncTransforms();
    }
}
