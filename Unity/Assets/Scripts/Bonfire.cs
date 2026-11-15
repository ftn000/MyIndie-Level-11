using UnityEngine;

// Финальный костёр. GiveFlame(all) -> экран гаснет почти в ноль, секунда тишины, вспышка, огонёк выпрыгивает, титр «Не дай погаснуть».
[RequireComponent(typeof(GiveFlame))]
public class Bonfire : MonoBehaviour
{
    public GameObject bigFlame, snow;
    public Light glow;
    public Renderer[] runes;
    public const float FadeEnd = 1.6f, IgniteAt = 2.6f, RiseEnd = 4.2f, HopEnd = 5.2f; // секунды от момента, когда отдал пламя
    public bool Lit { get; private set; }
    float t = -1;
    bool hopping;

    void Awake() => GetComponent<GiveFlame>().Given += () => { t = 0; GameManager.I.StartFinale(); };

    void Update()
    {
        if (t < 0) return;
        var gm = GameManager.I; var p = gm.player;
        t += Time.deltaTime;
        if (t < IgniteAt) { gm.fade = Mathf.SmoothStep(0, 0.97f, t / FadeEnd); GameAudio.Silence(gm.fade / 0.97f); return; } // потом ~1 с тишины в темноте
        if (!Lit) Ignite();
        float k = Mathf.SmoothStep(0, 1, (t - IgniteAt) / (RiseEnd - IgniteAt)); // свет идёт волной от костра
        glow.range = 32 * k; glow.intensity = 26 * k; gm.fade = 0.97f * (1 - k);
        if (t < RiseEnd) return;
        if (!hopping) { hopping = true; p.flame.Revive(0.25f); GameAudio.FireBack(); GameAudio.Play(Snd.Respawn); }
        float u = Mathf.Clamp01((t - RiseEnd) / (HopEnd - RiseEnd));
        var from = transform.position + Vector3.up * 1.2f; var to = transform.position + new Vector3(3.2f, p.H, 0);
        p.Respawn(Vector3.Lerp(from, to, u) + Vector3.up * Mathf.Sin(Mathf.PI * u) * 2.5f);
        if (u >= 1) { t = -1; gm.fade = 0; gm.FinishFinale(); }
    }

    void Ignite()
    {
        Lit = true; GameAudio.Play(Snd.BonfireIgnite);
        if (snow) snow.SetActive(false);
        bigFlame.SetActive(true); glow.enabled = true;
        foreach (var r in runes) r.material.color = new Color(1f, 0.62f, 0.2f);
        Fx.Spark(transform.position + Vector3.up, 80, 7);
    }
}
