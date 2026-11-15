using UnityEngine;

// Паутина: коллайдером перекрывает проход, от касания сгорает и забирает кусочек пламени (cost).
public class Cobweb : Interactable
{
    public float cost = 0.05f;

    public override void Tick(FlamePlayer p, Flame f, float dt)
    {
        var b = new Bounds(transform.position, transform.localScale);
        b.Expand(2 * (p.H + 0.12f)); // игрок упирается в коллайдер на расстоянии H + Skin
        if (!b.Contains(p.transform.position)) return;
        f.Add(-cost); GameAudio.Play(Snd.Cobweb, transform.position); Fx.Spark(transform.position, 10, 3); Destroy(gameObject);
    }
}
