using UnityEngine;

// Лёд тает от большой формы или от рывка. heatOnly: только от котла (Melt).
// thin: тонкий лёд проваливается сразу при касании (не плавится 0.6 с), держит только маленькую форму.
// refreeze: лёд-опора (над водой). Не уничтожается, а выключается и через RefreezeWait + FreezeTime намерзает обратно;
// пока Игни в объёме льдины, она не твердеет. Лёд-преграда (refreeze = false) тает навсегда.
public class Ice : Interactable
{
    public const float RefreezeWait = 3f, FreezeTime = 1f;
    public bool heatOnly, thin, refreeze;
    float melt, broken = -1f; // broken < 0: цел; иначе секунды с момента таяния
    bool freezing; // звук намерзания уже сыграл в этом восстановлении
    Collider col; Renderer rend; Vector3 size;

    public bool Broken => broken >= 0f;

    void Awake() { col = GetComponent<Collider>(); rend = GetComponent<Renderer>(); size = transform.localScale; }

    public void Melt()
    {
        Fx.Steam(transform.position, 5); GameAudio.Play(Snd.IceMelt, transform.position);
        if (!refreeze) { Destroy(gameObject); return; }
        broken = 0f; melt = 0f; freezing = false; col.enabled = false; rend.enabled = false;
    }

    // Респаун: опора возвращается сразу, без ожидания.
    public void Restore() { broken = -1f; melt = 0f; freezing = false; col.enabled = true; rend.enabled = true; transform.localScale = size; }

    void Update()
    {
        if (!Broken) return;
        broken += Time.deltaTime;
        if (broken < RefreezeWait) return;
        var p = GameManager.I != null ? GameManager.I.player : null;
        float half = p ? p.H + 0.5f : 0f; // Игни внутри объёма: ждём, пока уйдёт (иначе замуруем)
        if (p && Mathf.Abs(p.transform.position.x - transform.position.x) < half && Mathf.Abs(p.transform.position.y - transform.position.y) < half)
        { broken = RefreezeWait; return; }
        if (!freezing) { freezing = true; GameAudio.Play(Snd.IceRefreeze, transform.position); }
        float k = Mathf.Clamp01((broken - RefreezeWait) / FreezeTime); // иней растёт от 15% до целой льдины
        rend.enabled = true; transform.localScale = size * Mathf.Lerp(0.15f, 1f, k);
        if (Random.value < 0.15f) Fx.Steam(transform.position, 1);
        if (k >= 1f) Restore();
    }

    public static void RestoreAll() { foreach (var i in All) if (i is Ice ice && ice.refreeze) ice.Restore(); }

    public override void Tick(FlamePlayer p, Flame f, float dt)
    {
        if (Broken || heatOnly || Cheb(p, transform.position) > p.H + (thin ? 0.55f : 0.8f) || !(thin ? !f.IsSmall : f.IsBig) && !p.Dashing) return;
        melt += dt;
        if (Random.value < 0.3f) Fx.Steam(transform.position, 1);
        if (melt > (thin ? 0f : 0.6f)) { f.Add(-0.03f); Melt(); }
    }
}
