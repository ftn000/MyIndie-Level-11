using UnityEngine;

// Лужа высотой 0.34 м на полу: шипит и гасит, пока огонёк в ней. transform.position = верх пола.
public class Puddle : Interactable
{
    bool wasIn;
    static float lastPsh; // лужа из нескольких клеток: одно «пш» на вход, а не на каждую клетку

    public override void Tick(FlamePlayer p, Flame f, float dt)
    {
        var c = transform.position; var pp = p.transform.position;
        bool inside = !f.Dead && Mathf.Abs(pp.x - c.x) < p.H + 0.5f && pp.y - p.H < c.y + 0.34f && pp.y + p.H > c.y;
        if (inside && !wasIn && Time.time > lastPsh + 0.5f) { lastPsh = Time.time; GameAudio.Play(Snd.DropHit, pp); }
        wasIn = inside;
        if (inside) f.Burn(0.6f, dt);
    }
}
