using UnityEngine;

// Лужа высотой 0.34 м на полу: шипит и гасит, пока огонёк в ней. transform.position = верх пола.
public class Puddle : Interactable
{
    public override void Tick(FlamePlayer p, Flame f, float dt)
    {
        var c = transform.position; var pp = p.transform.position;
        if (Mathf.Abs(pp.x - c.x) < p.H + 0.5f && pp.y - p.H < c.y + 0.34f && pp.y + p.H > c.y) f.Burn(0.6f, dt);
    }
}
