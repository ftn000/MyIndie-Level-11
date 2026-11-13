using UnityEngine;

// Невидимый чекпоинт зоны: вошёл в объём (transform.position, localScale) — респаун в point.
public class Checkpoint : Interactable
{
    public Vector3 point;
    bool done;

    public override void Tick(FlamePlayer p, Flame f, float dt)
    {
        if (done || !new Bounds(transform.position, transform.localScale).Contains(p.transform.position)) return;
        done = true; GameManager.I.SetCheckpoint(point);
    }
}
