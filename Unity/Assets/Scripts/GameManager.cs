using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public enum State { Title, Play, Win }
    public static GameManager I;
    public FlamePlayer player;
    public State state;
    public bool Playing => state == State.Play;

    Vector3 cp;
    float deadT, playT, winT;
    int deaths;
    GUIStyle style;

    static readonly (float x, string t)[] Hints =
    {
        (3, "Стрелки / A D — идти, Пробел — прыжок"), (19, "Shift — вспышка (−15% пламени)"),
        (29, "Капли и лужи гасят пламя"), (51, "Большое пламя плавит лёд"),
        (58, "Зажми ↓ — стань фитильком"), (70, "Сквозняк тушит. Прячься за камни"),
        (92, "Держи прыжок в тёплом потоке"), (113, "Верёвки горят. Одна из них — не простая"),
    };

    void Awake()
    {
        I = this;
        Application.targetFrameRate = 60;
        QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1); // PC-профиль URP: тени от дополнительных источников
    }

    void Start()
    {
        cp = player.transform.position;
        player.flame.Died += () => { deaths++; deadT = 0; };
    }

    public void SetCheckpoint(Vector3 p) => cp = p;

    public void Win()
    {
        if (!Playing) return;
        state = State.Win; winT = 0;
        Fx.Spark(player.transform.position, 60, 6);
    }

    void Update()
    {
        var k = Keyboard.current; if (k == null) return;
        if (state == State.Title && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame)) state = State.Play;
        else if (k.rKey.wasPressedThisFrame || (state == State.Win && k.enterKey.wasPressedThisFrame))
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        if (state == State.Play) playT += Time.deltaTime;
        if (state == State.Win) winT += Time.deltaTime;
        if (state == State.Play && player.flame.Dead)
        {
            deadT += Time.deltaTime;
            if (deadT > 1.2f) { player.Respawn(cp); player.flame.Revive(0.7f); }
        }
    }

    void OnGUI()
    {
        style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic };
        style.fontSize = Screen.height / 28;
        style.normal.textColor = new Color(1f, .82f, .58f);
        var full = new Rect(0, 0, Screen.width, Screen.height);
        if (state == State.Title)
        {
            GUI.color = new Color(0, 0, 0, .6f); GUI.DrawTexture(full, Texture2D.whiteTexture); GUI.color = Color.white;
            style.fontSize = Screen.height / 8; GUI.Label(new Rect(0, Screen.height * .25f, Screen.width, Screen.height * .2f), "Огонёк", style);
            style.fontSize = Screen.height / 28;
            GUI.Label(new Rect(0, Screen.height * .5f, Screen.width, 40), "Последняя спичка. Холодный дом. Доберись до камина.", style);
            if (Mathf.Sin(Time.time * 3) > -.3f) GUI.Label(new Rect(0, Screen.height * .62f, Screen.width, 40), "Нажми Пробел", style);
            return;
        }
        if (state == State.Win)
        {
            style.fontSize = Screen.height / 9; GUI.Label(new Rect(0, Screen.height * .25f, Screen.width, 100), "Ты дома", style);
            style.fontSize = Screen.height / 26;
            GUI.Label(new Rect(0, Screen.height * .42f, Screen.width, 40), $"Время {playT:0.0} с · погасал {deaths} раз(а) · Enter — заново", style);
            return;
        }
        if (player.flame.Dead) { GUI.Label(new Rect(0, Screen.height * .4f, Screen.width, 60), "Погас…", style); return; }
        if (playT < 9) { style.fontSize = Screen.height / 34; GUI.Label(new Rect(0, 20, Screen.width, 40), "Пламя — это и жизнь, и свет. Грейся у свечей.", style); }
        var x = player.transform.position.x;
        foreach (var (hx, t) in Hints)
            if (Mathf.Abs(hx - x) < 7) { style.fontSize = Screen.height / 34; GUI.Label(new Rect(0, Screen.height * .88f, Screen.width, 40), t, style); break; }
    }
}
