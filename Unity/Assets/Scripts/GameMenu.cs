using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Главное меню (State.Title) и пауза (Esc). IMGUI, ввод в Update (работает при timeScale 0): клавиатура и мышь.
// Добавляется на GameManager сам, в сцене ничего собирать не нужно.
[RequireComponent(typeof(GameManager))]
public class GameMenu : MonoBehaviour
{
    enum Scr { None, Main, Pause, Controls, Settings }
    Scr scr = Scr.None;
    int sel;
    GameManager gm;
    GUIStyle st;

    static readonly string[] ControlLines =
    {
        "A D / стрелки  —  идти", "Пробел  —  прыжок, в потоке держать для подъёма", "W / стрелка вверх  —  пламя больше", "S / стрелка вниз  —  пламя меньше",
        "Shift / X  —  вспышка-рывок", "E (держать)  —  отдать пламя", "Esc  —  пауза",
    };

    void Awake() => gm = GetComponent<GameManager>();

    bool WebGL =>
#if UNITY_WEBGL
        true;
#else
        false;
#endif

    Scr Home => gm.state == GameManager.State.Title ? Scr.Main : Scr.Pause;

    string[] Items() => scr switch
    {
        Scr.Main => WebGL ? new[] { "Играть", "Управление", "Настройки" } : new[] { "Играть", "Управление", "Настройки", "Выход" },
        Scr.Pause => new[] { "Продолжить", "Начать с чекпоинта", "Настройки", "Управление", "В главное меню" },
        Scr.Settings => new[] { "Музыка", "Звуки", "Назад" },
        _ => new[] { "Назад" },
    };

    Rect Row(int i)
    {
        float h = Screen.height / 13f, w = Screen.height * 1.1f;
        float y0 = scr == Scr.Main ? .5f : scr == Scr.Controls ? .78f : .3f;
        return new Rect((Screen.width - w) / 2, Screen.height * y0 + i * h, w, h);
    }

    static Rect Bar(Rect r) => new Rect(r.x + r.width * .5f + 20, r.y + r.height * .3f, r.width * .5f - 20, r.height * .4f);
    bool Slider(int i) => scr == Scr.Settings && i < 2;
    static float Val(int i) => i == 0 ? GameAudio.MusicMul : GameAudio.SfxMul;

    void Go(Scr s) { scr = s; sel = 0; }

    void Update()
    {
        var k = Keyboard.current; var m = Mouse.current;
        bool open = gm.state == GameManager.State.Title || gm.paused;
        bool esc = k != null && k.escapeKey.wasPressedThisFrame;
        if (!open)
        {
            scr = Scr.None;
            if (esc && gm.Playing) { gm.SetPause(true); GameAudio.Ui(Snd.UiPause); Go(Scr.Pause); } // Playing учитывает съеденный кадр: Esc, пропустивший пролог, паузу не открывает
            return;
        }
        if (scr == Scr.None) Go(Home);
        var items = Items();
        if (esc) { Back(); return; }

        if (k != null)
        {
            int d = (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame ? 1 : 0) - (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame ? 1 : 0);
            if (d != 0) sel = (sel + d + items.Length) % items.Length;
            int h = (k.rightArrowKey.wasPressedThisFrame ? 1 : 0) - (k.leftArrowKey.wasPressedThisFrame ? 1 : 0);
            if (h != 0 && Slider(sel)) GameAudio.SetVolume(sel == 0, Mathf.Round((Val(sel) + h * .1f) * 10) / 10);
            if (k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame) Activate(sel);
        }
        if (m == null) return;
        var p = m.position.ReadValue(); p.y = Screen.height - p.y;
        int hover = -1;
        for (int i = 0; i < items.Length; i++) if (Row(i).Contains(p)) hover = i;
        if (hover >= 0 && m.delta.ReadValue() != Vector2.zero) sel = hover;
        if (hover < 0) return;
        if (Slider(hover) && m.leftButton.isPressed)
        {
            var b = Bar(Row(hover));
            if (p.x >= b.x - 10 && p.x <= b.xMax + 10) GameAudio.SetVolume(hover == 0, Mathf.Round(Mathf.Clamp01((p.x - b.x) / b.width) * 10) / 10);
        }
        else if (m.leftButton.wasPressedThisFrame) { sel = hover; Activate(hover); }
    }

    void Back()
    {
        if (scr == Scr.Controls || scr == Scr.Settings) Go(Home);
        else if (scr == Scr.Pause) Resume();
    }

    void Resume() { gm.SetPause(false); scr = Scr.None; }

    void Activate(int i)
    {
        if (Slider(i)) return;
        GameAudio.Ui(Snd.UiClick);
        gm.EatInput();
        switch (scr)
        {
            case Scr.Main:
                if (i == 0) { gm.StartGame(); scr = Scr.None; }
                else if (i == 1) Go(Scr.Controls);
                else if (i == 2) Go(Scr.Settings);
                else Application.Quit();
                break;
            case Scr.Pause:
                if (i == 0) Resume();
                else if (i == 1) { gm.Respawn(); Resume(); }
                else if (i == 2) Go(Scr.Settings);
                else if (i == 3) Go(Scr.Controls);
                else SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); // Awake GameManager снимает паузу
                break;
            default: Go(Home); break;
        }
    }

    void OnGUI()
    {
        if (scr == Scr.None) return;
        GUI.skin.font = GameManager.UiFont;
        GUI.depth = -10;
        st ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic };
        int H = Screen.height;
        var full = new Rect(0, 0, Screen.width, H);
        var warm = new Color(1f, .82f, .58f);
        GUI.color = new Color(0, 0, 0, scr == Scr.Main ? .6f : .75f); GUI.DrawTexture(full, Texture2D.whiteTexture); GUI.color = Color.white;
        st.normal.textColor = warm;
        if (scr == Scr.Main)
        {
            st.fontSize = H / 8; GUI.Label(new Rect(0, H * .15f, Screen.width, H * .2f), "Огонёк", st);
            st.fontSize = H / 32; GUI.Label(new Rect(0, H * .36f, Screen.width, 40), "Самая длинная ночь. Холодный дом. Дойди до большого костра.", st);
        }
        else
        {
            st.fontSize = H / 12;
            GUI.Label(new Rect(0, H * .1f, Screen.width, H * .15f), scr == Scr.Pause ? "Пауза" : scr == Scr.Settings ? "Настройки" : "Управление", st);
        }
        st.fontSize = H / 28;
        if (scr == Scr.Controls)
            for (int i = 0; i < ControlLines.Length; i++) GUI.Label(new Rect(0, H * (.26f + i * .07f), Screen.width, H * .07f), ControlLines[i], st);
        var items = Items();
        for (int i = 0; i < items.Length; i++)
        {
            var r = Row(i); bool on = i == sel;
            if (on) { GUI.color = new Color(1f, .6f, .2f, .22f); GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = Color.white; }
            st.normal.textColor = on ? new Color(1f, .96f, .8f) : new Color(warm.r, warm.g, warm.b, .7f);
            if (!Slider(i)) { GUI.Label(r, items[i], st); continue; }
            st.alignment = TextAnchor.MiddleRight; GUI.Label(new Rect(r.x, r.y, r.width * .5f, r.height), items[i], st); st.alignment = TextAnchor.MiddleCenter;
            var b = Bar(r);
            GUI.color = new Color(1, 1, 1, .15f); GUI.DrawTexture(b, Texture2D.whiteTexture);
            GUI.color = on ? new Color(1f, .75f, .3f) : new Color(1f, .6f, .25f, .7f); GUI.DrawTexture(new Rect(b.x, b.y, b.width * Val(i), b.height), Texture2D.whiteTexture);
            GUI.color = Color.white; GUI.Label(b, Mathf.RoundToInt(Val(i) * 100) + "%", st);
        }
    }
}
