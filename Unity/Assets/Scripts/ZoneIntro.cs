using UnityEngine;

// Один раз при входе в объём: камера на seconds секунд отъезжает к focus (общий план зоны), потом возвращается к игроку.
public class ZoneIntro : Interactable
{
    public Vector3 focus;
    public float seconds = 2f;
    bool done;

    public override void Tick(FlamePlayer p, Flame f, float dt)
    {
        if (done || !new Bounds(transform.position, transform.localScale).Contains(p.transform.position)) return;
        done = true;
        if (CamFollow.I) CamFollow.I.Establish(focus, seconds);
    }
}
