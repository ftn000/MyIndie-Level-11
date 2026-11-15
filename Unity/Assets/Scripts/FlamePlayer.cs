using UnityEngine;
using UnityEngine.InputSystem;

// Кинематический контроллер 2.5D: мир трёхмерный, движение по плоскости XY, z = 0.
public class FlamePlayer : MonoBehaviour
{
    public Flame flame;
    public int face = 1;
    public float windX;
    public bool inUpdraft;
    public bool giveHeld; // E: «отдать пламя» (GiveFlame)
    public bool Dashing => dashT > 0;
    public bool Grounded => grounded;
    [System.NonSerialized] public bool manual; // тесты гоняют MoveInput сами
    public float H => flame.Radius;

    Vector2 vel;
    bool grounded;
    float coyote, buf, dashT, dashCd, sparkAt;
    readonly Collider[] hits = new Collider[16];
    const float Skin = 0.02f, Speed = 5.9f, Gravity = 31.25f, MaxFall = 16.25f;

    public Vector2 Velocity => vel;
    // Толчок (метан): vy всегда, vx только если задан.
    public void Kick(float vy, float vx = float.NaN) { vel.y = vy; if (!float.IsNaN(vx)) vel.x = vx; dashT = 0; grounded = false; }

    public void Respawn(Vector3 pos) { transform.position = new Vector3(pos.x, pos.y, 0); vel = Vector2.zero; dashT = 0; }

    void Update()
    {
        var k = Keyboard.current;
        if (manual || k == null || GameManager.I == null || !GameManager.I.Playing || flame.Dead) return;
        float dt = Mathf.Min(Time.deltaTime, 0.033f);

        bool left = k.leftArrowKey.isPressed || k.aKey.isPressed, right = k.rightArrowKey.isPressed || k.dKey.isPressed;
        bool held = k.spaceKey.isPressed;
        bool jumpP = k.spaceKey.wasPressedThisFrame;
        bool dashP = k.leftShiftKey.wasPressedThisFrame || k.rightShiftKey.wasPressedThisFrame || k.xKey.wasPressedThisFrame || k.kKey.wasPressedThisFrame;
        int dir = (right ? 1 : 0) - (left ? 1 : 0);
        if (dir != 0) face = dir;
        giveHeld = k.eKey.isPressed;
        int form = (k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame ? 1 : 0) - (k.sKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame ? 1 : 0);

        MoveInput(dt, dir, held, jumpP, dashP, form);
    }

    // Вынесено из Update, чтобы бот в тестах мог гонять симуляцию без клавиатуры.
    public void MoveInput(float dt, int dir, bool held, bool jumpP, bool dashP, int formStep)
    {
        if (dir != 0) face = dir;
        inUpdraft = false; windX = 0; dashCd -= dt;
        var all = Interactable.All;
        for (int i = all.Count - 1; i >= 0; i--) all[i].Tick(this, flame, dt);

        if (dashP && dashCd <= 0 && flame.fuel > 0.16f)
        {
            flame.Add(-0.15f); GameAudio.Play(Snd.Dash); dashT = 0.18f; dashCd = 0.5f; Fx.Spark(transform.position, 16, 7);
        }
        flame.wind = windX;
        float size0 = flame.size;
        flame.StepForm(formStep);
        flame.Step(dt);
        if (flame.Dead) return;
        if (flame.size > size0) Grow(size0);

        buf -= dt; coyote -= dt;
        if (jumpP) buf = 0.12f;
        if (grounded) coyote = 0.1f;

        if (dashT > 0) { dashT -= dt; vel = new Vector2(face * 16.25f, 0); }
        else
        {
            vel.x += (dir * Speed - vel.x) * Mathf.Min(1, (grounded ? 18 : 9) * dt);
            vel.y = Mathf.Max(vel.y - Gravity * dt, -MaxFall);
            if (inUpdraft && held) vel.y = Mathf.Min(vel.y + 100f * dt, 5.3f);
            if (buf > 0 && coyote > 0) { vel.y = 10.9f + 4.06f * flame.size; buf = coyote = 0; GameAudio.Play(Snd.Jump); }
            if (!held && vel.y > 4.4f && !inUpdraft) vel.y -= 68.75f * dt;
        }

        var half = new Vector3(H, H, 0.25f);
        float dx = (vel.x + windX) * dt;
        if (Sweep(Vector3.right * dx, half)) vel.x = 0;
        grounded = false;
        if (Sweep(Vector3.up * vel.y * dt, half)) { if (vel.y < 0) grounded = true; vel.y = 0; }

        Depenetrate(half);
        var pz = transform.position; pz.z = 0; transform.position = pz;
        if (transform.position.y < -3f) flame.Kill();
    }

    // Рост (Step уже увеличил size): от ног на земле, при упоре в стену отодвигаемся от неё на прирост полуширины.
    // Места нет (стена с обеих сторон, потолок, решётка): размер откатывается, форма-желание остаётся, пых искр.
    void Grow(float size0)
    {
        float d = H - Flame.RadiusOf(size0), up = grounded ? d : 0;
        var half = new Vector3(H, H, 0.25f);
        for (int i = 0; i < 3; i++)
        {
            var c = transform.position + new Vector3(i == 0 ? 0 : i == 1 ? d : -d, up, 0);
            if (!Fits(c, half)) continue;
            transform.position = c; return;
        }
        flame.size = size0;
        if (Time.time >= sparkAt) { sparkAt = Time.time + 0.4f; Fx.Spark(transform.position, 6, 2f); }
    }

    bool Fits(Vector3 c, Vector3 half)
    {
        if (Physics.CheckBox(c, half * 0.98f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) return false;
        if (flame.size < Flame.SmallBelow) return true; // решётка пропускает только фитилька (её коллайдер выключен, пока он мал)
        var b = new Bounds(c, half * 1.96f);
        foreach (var it in Interactable.All) if (it is Vent v && v.Overlaps(b)) return false;
        return true;
    }

    // Если пересечение всё же возникло (плот прижал, телепорт): минимальный вектор по XY, не больше 0.3 м за кадр.
    // shortcut: геометрия уровня только оси-выровненные кубы, поэтому MTV считаем по Collider.bounds, а не ComputePenetration (у игрока нет коллайдера); сменить, если появятся повёрнутые коллайдеры.
    void Depenetrate(Vector3 half)
    {
        var box = half * 0.98f;
        int n = Physics.OverlapBoxNonAlloc(transform.position, box, hits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        float budget = 0.3f;
        for (int i = 0; i < n && budget > 0; i++)
        {
            var b = hits[i].bounds; var pos = transform.position;
            float ox = Mathf.Min(pos.x + box.x, b.max.x) - Mathf.Max(pos.x - box.x, b.min.x);
            float oy = Mathf.Min(pos.y + box.y, b.max.y) - Mathf.Max(pos.y - box.y, b.min.y);
            if (ox <= 0 || oy <= 0) continue;
            var push = ox < oy ? Vector3.right * ox * (pos.x < b.center.x ? -1 : 1) : Vector3.up * oy * (pos.y < b.center.y ? -1 : 1);
            push = Vector3.ClampMagnitude(push, budget); budget -= push.magnitude;
            transform.position += push;
        }
    }

    bool Sweep(Vector3 d, Vector3 half)
    {
        float dist = d.magnitude;
        if (dist < 1e-6f) return false;
        if (Physics.BoxCast(transform.position, half, d / dist, out var hit, Quaternion.identity, dist + Skin, ~0, QueryTriggerInteraction.Ignore))
        {
            transform.position += d / dist * Mathf.Max(0, hit.distance - Skin);
            return true;
        }
        transform.position += d;
        return false;
    }
}
