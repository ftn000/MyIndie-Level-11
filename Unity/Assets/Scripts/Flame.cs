using System;
using UnityEngine;

// Огонёк: топливо (fuel, жизнь) и форма (размер, выбирает игрок) независимы. Формулы прыжка, хитбокса, света и расхода
// считаются от размера формы (size), яркость света и спрайта от топлива. Числа из веб-прототипа (implementation-notes.md).
public class Flame : MonoBehaviour
{
    public enum Form { Small, Normal, Big }
    public const float SmallSize = 0.2f, NormalSize = 0.6f, BigSize = 1f;
    public const float BigAt = 0.8f, SmallBelow = 0.3f; // пороги механик по текущему размеру (плавный переход ~0.2 с их пересекает)
    public const float DimFuel = 0.6f, GuttersBelow = 0.25f; // яркость полная от DimFuel, ниже GuttersBelow пламя дрожит и краснеет

    public static Flame I;
    [Range(0, 1)] public float fuel = 0.8f;
    [NonSerialized] public float size = NormalSize; // текущий размер формы, плавно идёт к размеру выбранной формы
    public Light flameLight;
    public Transform visual;
    public ParticleSystem sparks, steam;

    public bool Dead { get; private set; }
    public event Action Died;
    public bool BigPref { get; private set; }  // Q: обычный <-> большой
    public bool Small { get; private set; }    // зажато ↓
    public Form CurrentForm => Small ? Form.Small : BigPref ? Form.Big : Form.Normal;
    public bool IsBig => size >= BigAt;
    public bool IsSmall => size < SmallBelow;
    public static float SizeOf(Form f) => f == Form.Small ? SmallSize : f == Form.Big ? BigSize : NormalSize;
    public static float DrainAt(float s) => 0.012f + 0.04f * s;
    public float Radius => (10 + 16 * size) / 64f; // половина хитбокса, м (1 тайл = 1 м)
    public float DrainRate => DrainAt(size);
    // Яркость от топлива: 1 при fuel >= 0.6, к 0.2 у нуля. Радиус света от неё не зависит.
    public float Brightness => Mathf.Lerp(0.2f, 1f, Mathf.Clamp01(fuel / DimFuel));
    public float Weak => Mathf.Clamp01(1 - fuel / GuttersBelow); // 0 при fuel >= 0.25, 1 у нуля
    public float wind; // горизонтальный ветер на огонёк, м/с (задаёт FlamePlayer)
    bool warm;
    float cold = 1;
    float lean;
    Billboard visBoard;
    SpriteRenderer visSr;

    void Awake()
    {
        I = this; Fx.SparksPS = sparks; Fx.SteamPS = steam;
        if (visual) { visBoard = visual.GetComponent<Billboard>(); visSr = visual.GetComponent<SpriteRenderer>(); }
    }

    public void Add(float v) { fuel = Mathf.Clamp01(fuel + v); if (fuel <= 0) Kill(); }
    public void Burn(float perSecond, float dt) { Add(-perSecond * dt); if (UnityEngine.Random.value < .5f) Fx.Steam(transform.position + Vector3.down * Radius, 1); }
    public void Warm() => warm = true;
    public void Cold(float mult) => cold = Mathf.Max(cold, mult); // множитель расхода от среды (лес), действует один кадр
    public void ToggleBig() => BigPref = !BigPref;
    // Мгновенно (респаун, тесты): выбранная форма и её размер.
    public void SetForm(Form f) { BigPref = f == Form.Big; Small = f == Form.Small; size = SizeOf(f); }
    // Финал: отдал себя костру. Это не смерть: Died не вызывается, респауна и счётчика нет.
    public void Surrender() { if (Dead) return; Dead = true; fuel = 0; }

    // Раз в кадр из FlamePlayer: расход (всегда), подогрев у свечи, переход к выбранной форме (↓ = маленький, топливо не тратит).
    public void Step(float dt, bool small)
    {
        if (Dead) return;
        Small = small;
        size = Mathf.MoveTowards(size, SizeOf(CurrentForm), 4f * dt); // 0.8 хода от маленького до большого за 0.2 с
        fuel += warm ? 0.25f * dt : -DrainRate * cold * dt; cold = 1;
        warm = false;
        fuel = Mathf.Clamp01(fuel);
        if (fuel <= 0) Kill();
    }

    public void Kill()
    {
        if (Dead) return;
        Dead = true; fuel = 0; Fx.Steam(transform.position, 8);
        Died?.Invoke();
    }

    // Респаун: топливо f, форма обычная.
    public void Revive(float f) { fuel = f; Dead = false; SetForm(Form.Normal); }

    void LateUpdate() => Apply();

    public void Apply()
    {
        float weak = Weak, bright = Brightness;
        // «гаснущее» пламя: рваная пульсация яркости при малом топливе
        float flick = 1 - weak * 0.75f * Mathf.PerlinNoise(Time.time * 22f, 3.7f);
        if (visual)
        {
            visual.gameObject.SetActive(!Dead);
            visual.localScale = Vector3.one * Radius * 4.8f * (0.95f + 0.05f * Mathf.Sin(Time.time * 18)) * (1 - 0.15f * weak * Mathf.PerlinNoise(Time.time * 17f, 9f));
            if (visSr)
            {
                var tint = Color.Lerp(Color.white, new Color(0.75f, 0.12f, 0.08f), weak); float k = bright * flick;
                visSr.color = new Color(tint.r * k, tint.g * k, tint.b * k, 1);
            }
        }
        if (flameLight)
        {
            flameLight.enabled = !Dead;
            flameLight.range = 3 + 11 * size;
            flameLight.intensity = Mathf.Lerp(1.5f, 14, size) * bright * flick * (1 + 0.04f * Mathf.Sin(Time.time * 13));
            flameLight.color = Color.Lerp(new Color(1, .6f, .25f), new Color(.7f, .08f, .04f), weak);
        }
        // ветер гнёт пламя по движению воздуха (windX < 0 = влево) и сдувает искры
        lean = Mathf.Lerp(lean, Mathf.Clamp(-wind * 6f, -40f, 40f), 1 - Mathf.Exp(-12f * Time.deltaTime));
        if (visBoard) visBoard.roll = lean;
        if (sparks)
        {
            var e = sparks.emission; e.rateOverTime = Dead ? 0 : (12 + 20 * size) * Mathf.Lerp(1f, 0.25f, weak);
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
