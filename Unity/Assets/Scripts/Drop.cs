using UnityEngine;

public class Drop : MonoBehaviour
{
    public float cost = 0.18f, gravity = 25f, maxSpeed = 99f; // снежинка: дёшево и медленно
    float vy;

    void Update()
    {
        float dt = Time.deltaTime;
        vy = Mathf.Min(vy + gravity * dt, maxSpeed);
        transform.position += Vector3.down * vy * dt;
        var gm = GameManager.I; var p = gm ? gm.player : null;
        if (p && !p.flame.Dead && Mathf.Abs(transform.position.x - p.transform.position.x) < p.H + 0.1f
              && Mathf.Abs(transform.position.y - p.transform.position.y) < p.H + 0.15f)
        {
            p.flame.Add(-cost); Fx.Steam(transform.position, cost < 0.1f ? 1 : 6); GameAudio.Play(cost < 0.1f ? Snd.SnowHit : Snd.DropHit, transform.position); Destroy(gameObject);
        }
        else if (transform.position.y < -5f || Physics.CheckSphere(transform.position, 0.1f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (cost >= 0.1f && OnPuddle(transform.position)) GameAudio.Play(Snd.DropWater, transform.position); // не каждый Destroy: сухой пол и снег молчат
            Destroy(gameObject);
        }
    }

    // Лужа без коллайдера: капля садится на пол под ней. position лужи = верх пола, ширина 1 м.
    static bool OnPuddle(Vector3 p)
    {
        foreach (var i in Interactable.All)
            if (i is Puddle && Mathf.Abs(i.transform.position.x - p.x) < 0.6f && Mathf.Abs(i.transform.position.y - p.y) < 0.6f) return true;
        return false;
    }
}
