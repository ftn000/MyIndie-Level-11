using UnityEngine;

// Капля раз в interval секунд. Игра ждёт «Play», чтобы тайминг не уходил на титульном экране.
// Снегопад: тот же спавнер с малой ценой, halfWidth (случайный x), частым interval и activeRange (не капать вдали от игрока).
public class DripSpawner : MonoBehaviour
{
    public GameObject dropPrefab;
    public float phase;
    public float interval = 2.2f, cost = 0.18f, halfWidth, gravity = 25f, maxSpeed = 99f, scale = 1f, activeRange = float.MaxValue;
    float ph;

    public float TimeToDrop => interval - ph;

    void Start() => ph = phase;

    void Update()
    {
        var gm = GameManager.I;
        if (gm == null || !gm.Playing || Vector2.Distance(gm.player.transform.position, transform.position) > activeRange) return;
        ph += Time.deltaTime;
        if (ph < interval) return;
        ph -= interval;
        var pos = transform.position + new Vector3(halfWidth > 0 ? Random.Range(-halfWidth, halfWidth) : 0, -0.3f, 0);
        var d = Instantiate(dropPrefab, pos, Quaternion.identity);
        d.transform.localScale *= scale;
        var drop = d.GetComponent<Drop>(); drop.cost = cost; drop.gravity = gravity; drop.maxSpeed = maxSpeed;
    }
}
