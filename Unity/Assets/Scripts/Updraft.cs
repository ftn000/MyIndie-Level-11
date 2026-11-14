using UnityEngine;

// Тёплый поток: объём = transform.localScale вокруг transform.position. Держи прыжок, чтобы подниматься.
public class Updraft : Interactable
{
    public override void Tick(FlamePlayer p, Flame f, float dt)
    {
        if (new Bounds(transform.position, transform.localScale).Contains(p.transform.position)) p.inUpdraft = true;
    }
}
