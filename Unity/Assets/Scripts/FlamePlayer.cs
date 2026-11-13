using UnityEngine;
using UnityEngine.InputSystem;

// Кинематический контроллер 2.5D: мир трёхмерный, движение по плоскости XY, z = 0.
public class FlamePlayer : MonoBehaviour
{
    public Flame flame;
    public int face = 1;
    public float windX;
    public bool inUpdraft;
    public bool Dashing => dashT > 0;
    public bool Grounded => grounded;
    [System.NonSerialized] public bool manual; // тесты гоняют MoveInput сами
    public float H => flame.Radius;

    Vector2 vel;
    bool grounded;
    float coyote, buf, dashT, dashCd;
    const float Skin = 0.02f, Speed = 5.9f, Gravity = 31.25f, MaxFall = 16.25f;

    public void Respawn(Vector3 pos) { transform.position = new Vector3(pos.x, pos.y, 0); vel = Vector2.zero; dashT = 0; }

    void Update()
    {
        var k = Keyboard.current;
        if (manual || k == null || GameManager.I == null || !GameManager.I.Playing || flame.Dead) return;
        float dt = Mathf.Min(Time.deltaTime, 0.033f);

        bool left = k.leftArrowKey.isPressed || k.aKey.isPressed, right = k.rightArrowKey.isPressed || k.dKey.isPressed;
        bool held = k.spaceKey.isPressed || k.upArrowKey.isPressed || k.wKey.isPressed;
        bool jumpP = k.spaceKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame;
        bool dashP = k.leftShiftKey.wasPressedThisFrame || k.rightShiftKey.wasPressedThisFrame || k.xKey.wasPressedThisFrame || k.kKey.wasPressedThisFrame;
        bool down = k.downArrowKey.isPressed || k.sKey.isPressed;
        int dir = (right ? 1 : 0) - (left ? 1 : 0);
        if (dir != 0) face = dir;

        MoveInput(dt, dir, held, jumpP, dashP, down);
    }

    // Вынесено из Update, чтобы бот в тестах мог гонять симуляцию без клавиатуры.
    public void MoveInput(float dt, int dir, bool held, bool jumpP, bool dashP, bool down)
    {
        if (dir != 0) face = dir;
        inUpdraft = false; windX = 0; dashCd -= dt;
        var all = Interactable.All;
        for (int i = all.Count - 1; i >= 0; i--) all[i].Tick(this, flame, dt);

        if (dashP && dashCd <= 0 && flame.size > 0.16f)
        {
            flame.Add(-0.15f); dashT = 0.18f; dashCd = 0.5f; Fx.Spark(transform.position, 16, 7);
        }
        flame.wind = windX;
        flame.Step(dt, down);
        if (flame.Dead) return;

        buf -= dt; coyote -= dt;
        if (jumpP) buf = 0.12f;
        if (grounded) coyote = 0.1f;

        if (dashT > 0) { dashT -= dt; vel = new Vector2(face * 16.25f, 0); }
        else
        {
            vel.x += (dir * Speed - vel.x) * Mathf.Min(1, (grounded ? 18 : 9) * dt);
            vel.y = Mathf.Max(vel.y - Gravity * dt, -MaxFall);
            if (inUpdraft && held) vel.y = Mathf.Min(vel.y + 100f * dt, 5.3f);
            if (buf > 0 && coyote > 0) { vel.y = 10.9f + 4.06f * flame.size; buf = coyote = 0; }
            if (!held && vel.y > 4.4f && !inUpdraft) vel.y -= 68.75f * dt;
        }

        var half = new Vector3(H, H, 0.25f);
        float dx = (vel.x + windX) * dt;
        if (Sweep(Vector3.right * dx, half)) vel.x = 0;
        grounded = false;
        if (Sweep(Vector3.up * vel.y * dt, half)) { if (vel.y < 0) grounded = true; vel.y = 0; }

        for (int i = 0; i < 10 && Physics.CheckBox(transform.position, half * 0.98f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore); i++)
            transform.position += Vector3.up * 0.03f; // вырос в стене после подпитки
        var pz = transform.position; pz.z = 0; transform.position = pz;
        if (transform.position.y < -3f) flame.Kill();
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
