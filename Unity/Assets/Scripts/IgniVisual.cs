using UnityEngine;

// Живой Игни поверх одного спрайта (igni_idle): squash & stretch по скорости, наклон при беге, дышащие края из двух
// аддитивных слоёв пламени за телом, шлейф искр по скорости. Масштаб, цвет по топливу и наклон от ветра уже выставил
// Flame.Apply (LateUpdate, порядок 0); здесь домножаем (порядок 50), Billboard поворачивает последним (100).
[DefaultExecutionOrder(50)]
public class IgniVisual : MonoBehaviour
{
    public FlamePlayer player;
    public SpriteRenderer body;
    public Transform[] glows; // слои пламени за телом, дети этого объекта
    public Sprite idle, run; // pivot у обоих в ногах; бег лицом вправо, отражаем по face

    const float MaxTilt = 12f, Spring = 90f, Damp = 11f;
    Billboard board;
    float squash, squashV, prevVy; // squash > 0: сплющен (приземление), отклик пружиной
    bool wasGrounded = true, running;

    void Awake() => board = GetComponent<Billboard>();

    void LateUpdate()
    {
        if (!player || player.flame.Dead) return;
        float dt = Time.deltaTime, t = Time.time;
        var v = player.Velocity;

        // приземление: удар пропорционален скорости падения
        if (player.Grounded && !wasGrounded) squashV += Mathf.Clamp01(-prevVy / 14f) * 6f;
        wasGrounded = player.Grounded; prevVy = v.y;
        squashV += (-Spring * squash - Damp * squashV) * dt; squash += squashV * dt;
        squash = Mathf.Clamp(squash, -0.35f, 0.35f);

        // полёт: вытянут по вертикали пропорционально |vy|; на земле лёгкое дыхание
        float air = player.Grounded ? 0 : Mathf.Clamp01(Mathf.Abs(v.y) / 12f) * 0.22f;
        float breathe = player.Grounded ? 0.03f * Mathf.Sin(t * 3.1f) : 0;
        float sy = 1 + air + breathe - squash, sx = 1 - air * 0.6f - breathe * 0.5f + squash * 0.8f;
        var s = transform.localScale; // Flame.Apply ставит равномерный масштаб каждый кадр
        transform.localScale = new Vector3(s.x * sx, s.y * sy, s.z);
        transform.localPosition = new Vector3(0, -player.H, 0); // pivot спрайта в ногах: ноги на низу хитбокса, сжатие и рост от них

        // поза: бег при заметной горизонтальной скорости (гистерезис, чтобы не мигало), лицом по ходу
        running = Mathf.Abs(v.x) > (running ? 0.8f : 1.5f);
        if (body && run && idle) { body.sprite = running ? run : idle; body.flipX = player.face < 0; }

        // бег: наклон вперёд по ходу (roll < 0 = по часовой = вправо), складывается с ветром от Flame
        if (board) board.roll += -Mathf.Clamp(v.x / 5.9f, -1f, 1f) * MaxTilt;

        // края: слои дышат каждый со своим шумом, цвет как у тела, но мягче
        var c = body ? body.color : Color.white;
        for (int i = 0; i < glows.Length; i++)
        {
            float n = Mathf.PerlinNoise(t * (3.3f + i * 1.7f), i * 7.1f), m = Mathf.PerlinNoise(i * 3.9f, t * (4.1f + i));
            glows[i].localScale = new Vector3(0.7f + 0.12f * n + i * 0.1f, 0.8f + 0.22f * m + i * 0.12f, 1);
            glows[i].localPosition = new Vector3((n - 0.5f) * 0.04f, 0.3f + 0.07f * m, 0.01f * (i + 1));
            var sr = glows[i].GetComponent<SpriteRenderer>(); if (sr) sr.color = new Color(c.r, c.g * 0.8f, c.b * 0.6f, 1) * (0.55f - i * 0.15f);
        }

        // шлейф: больше искр на скорости, ветер искр против хода
        var sp = player.flame.sparks;
        if (sp)
        {
            float k = 1 + v.magnitude / 3f;
            var e = sp.emission; e.rateOverTime = e.rateOverTime.constant * k;
            var fo = sp.forceOverLifetime; fo.x = new ParticleSystem.MinMaxCurve(fo.x.constant - v.x * 0.8f);
        }
    }
}
