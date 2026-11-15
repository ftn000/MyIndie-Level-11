using System;
using UnityEngine;

// «Отдать пламя»: удерживай E рядом (reach) hold секунд — объект забирает cost топлива (all: всё, что есть).
// Если пламени не хватает, не отдаёт и не убивает. Переиспользуется котлом, финальным костром.
public class GiveFlame : Interactable
{
    public float cost = 0.3f, hold = 1f, reach = 2.2f, minLeft = 0.02f;
    public bool all;
    public bool Done { get; private set; }
    public float Progress => Mathf.Clamp01(t / hold);
    public event Action Given;
    float t;
    bool near, weak;

    public override void Tick(FlamePlayer p, Flame f, float dt)
    {
        if (Done) return;
        near = Cheb(p, transform.position) <= reach; weak = !all && f.fuel < cost + minLeft;
        if (!near || !p.giveHeld || weak) { t = 0; GameAudio.Hold(this, false); return; }
        t += dt; GameAudio.Hold(this, true);
        if (UnityEngine.Random.value < 0.5f) Fx.Spark(p.transform.position, 1, 2f);
        if (t < hold) return;
        Done = true; GameAudio.Hold(this, false);
        if (all) f.Surrender(); else f.Add(-cost);
        Fx.Spark(transform.position, 30, 5);
        Given?.Invoke();
    }

    // Шкала: подсказка рядом, прогресс при удержании E, «слишком слабое», если пламени не хватает.
    void OnGUI()
    {
        if (Done || !near) return;
        float w = Screen.width * .22f, h = Screen.height * .025f, x = (Screen.width - w) / 2, y = Screen.height * .78f;
        var st = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Screen.height / 30, fontStyle = FontStyle.Italic };
        st.normal.textColor = weak ? new Color(1f, .5f, .4f) : new Color(1f, .82f, .58f);
        GUI.Label(new Rect(0, y - h * 2.4f, Screen.width, h * 2), weak ? "Слишком слабое пламя" : Progress > 0 ? "Отдаю пламя…" : "Держи E", st);
        GUI.color = new Color(0, 0, 0, .65f); GUI.DrawTexture(new Rect(x - 3, y - 3, w + 6, h + 6), Texture2D.whiteTexture);
        GUI.color = new Color(1f, .6f, .2f); GUI.DrawTexture(new Rect(x, y, w * Progress, h), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
