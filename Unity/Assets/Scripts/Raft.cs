using UnityEngine;

// Плот: платформа плывёт туда-обратно по x между a и b (центр). Двигается в Tick (вместе с симуляцией игрока),
// стоящего сверху увозит с собой. Коллайдер обычный куб; верх плота = transform.y + scale.y / 2.
public class Raft : Interactable
{
    public float a, b, speed = 1.5f, phase;
    float t;

    float XAt(float time) => a + Mathf.PingPong(time * speed + phase * (b - a), b - a);

    void Awake() => transform.position = new Vector3(XAt(0), transform.position.y, transform.position.z);

    public float Top => transform.position.y + transform.localScale.y / 2;

    public override void Tick(FlamePlayer p, Flame f, float dt)
    {
        t += dt;
        float old = transform.position.x, nx = XAt(t), pp = p.transform.position.x;
        bool riding = p.Grounded && Mathf.Abs(pp - old) < transform.localScale.x / 2 + p.H && Mathf.Abs(p.transform.position.y - p.H - Top) < 0.15f;
        transform.position = new Vector3(nx, transform.position.y, transform.position.z);
        if (riding) p.transform.position += Vector3.right * (nx - old);
        Physics.SyncTransforms();
    }
}
