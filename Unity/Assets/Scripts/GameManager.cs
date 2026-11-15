using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public enum State { Title, Play, Finale, Win }
    public static GameManager I;
    public FlamePlayer player;
    public State state;
    public bool Playing => state == State.Play;

    Vector3 cp;
    float deadT, playT, winT;
    int deaths;
    string winTitle = "Продолжение следует";
    GUIStyle style;
    public float fade; // чёрная пелена финала, 0..1
    public int Deaths => deaths;

    public Vector3[] zoneStart; // старт зоны N = zoneStart[N-1]; F1..F6 в Editor/Development build
    public Vector3 Checkpoint => cp;

    // (x, y, текст; P-4: сетка расширена, подсказки пересчитаны): виден, пока игрок ближе 7 м по x и 6 м по y. Координаты сетки OgonokBuilder.
    static readonly (float x, float y, string t)[] Hints =
    {
        (3.5f, 81.6f, "Стрелки / A D — идти, Пробел — прыжок"), (19, 81.6f, "Shift — вспышка (−15% топлива)"),
        (40, 81.6f, "Окно дует порывами. Занавеска вздувается заранее — прячься за мебель"),
        (84, 81.6f, "Капли и лужи отнимают топливо"),
        (98, 81.6f, "Кладовка: паутина сгорает и забирает кусочек топлива"), (140, 81.6f, "Лёд в проёме: жми W / ↑ — большое пламя плавит лёд (S / ↓ — меньше)"),
        (170, 81.6f, "Второе окно. Прячься за мебель и пережидай порыв"), (112 + 90, 85.6f, "Держи Пробел у верёвки: сгорит, и шкаф ляжет мостом"),
        (214 + 70, 65.6f, "Прыгни к верёвке: она роняет полку-мост через провал"), (214 + 80 + 82, 65.6f, "Впереди котёл: подойди и держи E, чтобы отдать часть пламени"),
        (214 + 80 + 106, 65.6f, "S / ↓ — меньше: стань фитильком"),
        (12, 25.5f, "Вода гасит мгновенно. Прыгай по уступам, жги свет факелов"), (40, 25.5f, "Плоты плывут по течению: прыгай на них, пока не утонули"),
        (131, 25.5f, "Течение навстречу: перепрыгивай с плота на плот, на одном не стой"),
        (85, 25.5f, "Пузырь метана: коснись — вспышка и подпитка. Большая форма откинет вбок"),
        (134, 25.5f, "Тёплый воздух из трубы: держи Пробел и лети наверх"), (244, 25.5f, "Тёплый воздух из трубы: держи Пробел и лети наверх"),
        (166 + 110, 32.5f, "Тёплые потоки несут вверх: держи Пробел"),
        (164 + 110, 44.5f, "Лопасти гасят. Проскочи между ними у стены или пережги ремень"), (307, 37f, "Решётка поперёк потока: стань фитильком (S / ↓) и держи Пробел"),
        (174 + 110, 58.5f, "S / ↓ — меньше: стань фитильком"),
        // зона 5 (лес)
        (6, 3.6f, "Холодно: в лесу пламя тает быстрее. Снег — это мелкие капли, под кронами его нет"),
        (44, 3.6f, "Ветер дует навстречу. Прячься за пнями и пережидай порыв"),
        (62, 3.6f, "Лёд тает от большого пламени (W / ↑). Тонкий лёд держит только фитилька: S / ↓"),
        (116, 3.6f, "Подпали верёвку: ствол ляжет мостом"),
        (356, 7f, "Подъём без подпитки. Береги топливо: в форме фитилька оно тает медленнее"), (150, 3.6f, "Лёд тоньше: держит только фитилька. Нажми S / ↓ и иди"), (205, 3.6f, "Ветер и лёд: пережидай порыв за пнём"),
        // зона 6
        (411, 13.6f, "Ветер стих. Подойди к костру и держи E: отдай себя целиком"),
    };

    void Awake()
    {
        I = this;
        Application.targetFrameRate = 60;
        QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1); // PC-профиль URP: тени от дополнительных источников
    }

    void Start()
    {
        cp = player.transform.position; Paper.Commit();
        player.flame.Died += () => { deaths++; deadT = 0; };
    }

    public void SetCheckpoint(Vector3 p) { cp = p; Paper.Commit(); } // всё, что взято до чекпоинта, остаётся взятым

    // 334 → «5 мин 34 с», 48 → «48 с», 60 → «1 мин 0 с»
    public static string FormatTime(float sec)
    {
        int t = Mathf.FloorToInt(sec);
        return t < 60 ? $"{t} с" : $"{t / 60} мин {t % 60} с";
    }

    // Отладка (F1..F6) и тесты: старт зоны n (1..6), если она уже собрана.
    public bool JumpToZone(int n)
    {
        if (zoneStart == null || n < 1 || n > zoneStart.Length) return false;
        if (state == State.Title) state = State.Play;
        cp = zoneStart[n - 1]; player.Respawn(cp); player.flame.Revive(0.8f);
        return true;
    }

    // Респаун на последнем чекпоинте: топливо 0.8, форма обычная, подпитки после чекпоинта возвращаются. Прогресс (верёвки, лёд-преграда, паутина) не откатывается; лёд-опора восстанавливается.
    public void Respawn() { player.Respawn(cp); player.flame.Revive(0.8f); Paper.Restore(); Ice.RestoreAll(); GameAudio.Play(Snd.Respawn); }

    public void Win()
    {
        if (!Playing) return;
        state = State.Win; winT = 0;
        Fx.Spark(player.transform.position, 60, 6);
    }

    // Финал у костра: управление и таймер стоят, Bonfire ведёт экран.
    public void StartFinale() { if (Playing) state = State.Finale; }
    public void FinishFinale() { state = State.Win; winT = 0; winTitle = "Не дай погаснуть"; }

    // Страховка: если центр огонька дольше 0.2 с вне всех комнат уровня (с запасом 1 м), он гаснет и вернётся к чекпоинту.
    public Rect[] mapRects; // мировые прямоугольники комнат, задаёт OgonokBuilder
    float outT;
    public bool InMap(Vector2 p)
    {
        if (mapRects == null || mapRects.Length == 0) return true;
        foreach (var r in mapRects) if (p.x > r.xMin - 1 && p.x < r.xMax + 1 && p.y > r.yMin - 1 && p.y < r.yMax + 1) return true;
        return false;
    }

    void Update()
    {
        if (state == State.Play && !player.flame.Dead)
        {
            outT = InMap(player.transform.position) ? 0 : outT + Time.deltaTime;
            if (outT > 0.2f) { outT = 0; player.flame.Kill(); }
        }
        var k = Keyboard.current;
        if (k != null)
        {
            if (state == State.Title && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame)) state = State.Play;
            else if (k.rKey.wasPressedThisFrame || (state == State.Win && k.enterKey.wasPressedThisFrame))
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var fk = new[] { k.f1Key, k.f2Key, k.f3Key, k.f4Key, k.f5Key, k.f6Key };
            for (int i = 0; i < fk.Length; i++) if (fk[i].wasPressedThisFrame) JumpToZone(i + 1);
#endif
        }
        if (state == State.Play) playT += Time.deltaTime;
        if (state == State.Win) winT += Time.deltaTime;
        if (state == State.Play && player.flame.Dead)
        {
            deadT += Time.deltaTime;
            if (deadT > 1.2f) Respawn();
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
            GUI.Label(new Rect(0, Screen.height * .5f, Screen.width, 40), "Самая длинная ночь. Холодный дом. Дойди до большого костра.", style);
            if (Mathf.Sin(Time.time * 3) > -.3f) GUI.Label(new Rect(0, Screen.height * .62f, Screen.width, 40), "Нажми Пробел", style);
            return;
        }
        if (fade > 0) { GUI.color = new Color(0, 0, 0, fade); GUI.DrawTexture(full, Texture2D.whiteTexture); GUI.color = Color.white; }
        if (state == State.Finale) return;
        if (state == State.Win)
        {
            style.fontSize = Screen.height / 9; GUI.Label(new Rect(0, Screen.height * .25f, Screen.width, 100), winTitle, style);
            style.fontSize = Screen.height / 26;
            GUI.Label(new Rect(0, Screen.height * .42f, Screen.width, 40), $"Время {FormatTime(playT)} · погасал {deaths} раз(а) · Enter — заново", style);
            return;
        }
        if (player.flame.Dead) { GUI.Label(new Rect(0, Screen.height * .4f, Screen.width, 60), "Погас…", style); return; }
        if (playT < 9) { style.fontSize = Screen.height / 34; GUI.Label(new Rect(0, 20, Screen.width, 40), "Размер выбираешь сам (W / ↑ больше, S / ↓ меньше), топливо тает всегда. Грейся у свечей.", style); }
        var pp = player.transform.position;
        foreach (var (hx, hy, t) in Hints)
            if (Mathf.Abs(hx - pp.x) < 7 && Mathf.Abs(hy - pp.y) < 6) { style.fontSize = Screen.height / 34; GUI.Label(new Rect(0, Screen.height * .88f, Screen.width, 40), t, style); break; }
    }
}
