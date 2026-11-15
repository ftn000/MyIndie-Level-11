using UnityEngine;

// Плот течения: всплывает у истока, плывёт в одну сторону (dir) и у конца канала (endX) проседает за SinkTime и уходит под воду.
// Двигается в Tick (вместе с симуляцией игрока), стоящего сверху увозит с собой; тонущий плот игрока не держит: он падает в воду (Pit) и гаснет.
// Коллайдер обычный куб; верх плота = transform.y + scale.y / 2. Создаёт и настраивает RaftStream.
public class Raft : Interactable
{
    public const float RiseTime = 0.4f, SinkTime = 0.5f, SinkDepth = 0.5f, RiseDepth = 0.6f;
    public float dir = 1, speed = 1.5f, endX;
    public bool Sinking { get; private set; }
    public bool Risen => age >= RiseTime;
    float age, sinkT, baseY;
    bool wasRiding;

    void Awake() => baseY = transform.position.y; // центр на плаву: задаётся до AddComponent

    public void Init(float dir, float speed, float endX, float age) { this.dir = dir; this.speed = speed; this.endX = endX; this.age = age; Place(transform.position.x); }

    public float Top => transform.position.y + transform.localScale.y / 2;

    void Place(float x)
    {
        float depth = Sinking ? SinkDepth * Mathf.Clamp01(sinkT / SinkTime) : RiseDepth * (1 - Mathf.Clamp01(age / RiseTime));
        transform.position = new Vector3(x, baseY - depth, transform.position.z);
    }

    public override void Tick(FlamePlayer p, Flame f, float dt)
    {
        age += dt;
        float old = transform.position.x, nx = old, pp = p.transform.position.x;
        bool riding = Risen && !Sinking && p.Grounded && Mathf.Abs(pp - old) < transform.localScale.x / 2 + p.H && Mathf.Abs(p.transform.position.y - p.H - Top) < 0.15f;
        if (riding && !wasRiding) GameAudio.Play(Snd.Raft, transform.position);
        wasRiding = riding;
        if (!Sinking)
        {
            nx = old + dir * speed * dt;
            if ((nx - endX) * dir >= 0) { nx = endX; Sinking = true; }
        }
        else sinkT += dt;
        if (sinkT >= SinkTime) { Destroy(gameObject); return; }
        Place(nx);
        if (riding) p.transform.position += Vector3.right * (nx - old);
        Physics.SyncTransforms();
    }
}
