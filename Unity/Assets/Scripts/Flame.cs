using System;
using UnityEngine;

// Огонёк: размер = жизнь = свет = топливо. Все числа из веб-прототипа (implementation-notes.md).
public class Flame : MonoBehaviour
{
    public static Flame I;
    [Range(0, 1)] public float size = 0.8f;
    public Light flameLight;
    public Transform visual;
    public ParticleSystem sparks, steam;

    public bool Dead { get; private set; }
    public event Action Died;
    public float Radius => (10 + 16 * size) / 64f; // половина хитбокса, м (1 тайл = 1 м)
    public float DrainRate => 0.012f + 0.04f * size;
    public float wind; // горизонтальный ветер на огонёк, м/с (задаёт FlamePlayer)
    bool warm;
    float lean;
    Billboard visBoard;

    void Awake() { I = this; Fx.SparksPS = sparks; Fx.SteamPS = steam; if (visual) visBoard = visual.GetComponent<Billboard>(); }

    public void Add(float v) { size = Mathf.Clamp01(size + v); if (size <= 0) Kill(); }
    public void Burn(float perSecond, float dt) { Add(-perSecond * dt); if (UnityEngine.Random.value < .5f) Fx.Steam(transform.position + Vector3.down * Radius, 1); }
    public void Warm() => warm = true;

    // Раз в кадр из FlamePlayer: расход, подогрев у свечи, «приглушить» (↓).
    public void Step(float dt, bool dim)
    {
        if (Dead) return;
        size += warm ? 0.25f * dt : -DrainRate * dt;
        warm = false;
        if (dim && size > 0.12f) size = Mathf.Max(0.12f, size - 0.5f * dt);
        size = Mathf.Clamp01(size);
        if (size <= 0) Kill();
    }

    public void Kill()
    {
        if (Dead) return;
        Dead = true; size = 0; Fx.Steam(transform.position, 8);
        Died?.Invoke();
    }

    public void Revive(float s) { size = s; Dead = false; }

    void LateUpdate() => Apply();

    public void Apply()
    {
        if (visual) { visual.gameObject.SetActive(!Dead); visual.localScale = Vector3.one * Radius * 4.8f * (0.95f + 0.05f * Mathf.Sin(Time.time * 18)); }
        if (flameLight)
        {
            flameLight.enabled = !Dead;
            flameLight.range = 3 + 11 * size;
            flameLight.intensity = Mathf.Lerp(1.5f, 14, size) * (1 + 0.04f * Mathf.Sin(Time.time * 13));
        }
        // ветер гнёт пламя по движению воздуха (windX < 0 = влево) и сдувает искры
        lean = Mathf.Lerp(lean, Mathf.Clamp(-wind * 6f, -40f, 40f), 1 - Mathf.Exp(-12f * Time.deltaTime));
        if (visBoard) visBoard.roll = lean;
        if (sparks)
        {
            var e = sparks.emission; e.rateOverTime = Dead ? 0 : 12 + 20 * size;
            var fo = sparks.forceOverLifetime; fo.enabled = true; fo.x = new ParticleSystem.MinMaxCurve(wind * 2.5f);
        }
    }
}

public static class Fx
{
    public static ParticleSystem SparksPS, SteamPS;

    public static void Spark(Vector3 p, int n, float speed = 2.5f) => Emit(SparksPS, p, n, speed, 1);
    public static void Steam(Vector3 p, int n) => Emit(SteamPS, p, n, 0.8f, 0.2f);

    static void Emit(ParticleSystem ps, Vector3 p, int n, float speed, float up)
    {
        if (!ps) return;
        var ep = new ParticleSystem.EmitParams();
        for (int i = 0; i < n; i++)
        {
            ep.position = p; ep.velocity = UnityEngine.Random.insideUnitSphere * speed + Vector3.up * speed * up;
            ep.velocity = new Vector3(ep.velocity.x, ep.velocity.y, 0);
            ps.Emit(ep, 1);
        }
    }
}
