using UnityEngine;

// Провал глубже прыжка: центр огонька в объёме (transform.position, localScale) гасит его, чтобы не застрять на дне.
public class Pit : Interactable
{
    public override void Tick(FlamePlayer p, Flame f, float dt)
    {
        if (new Bounds(transform.position, transform.localScale).Contains(p.transform.position)) f.Kill();
    }
}
