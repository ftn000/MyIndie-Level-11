using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Собирает сцену Level1: длинный уровень из зон. Каждая зона это метод ZoneN(x0, y0) со смещением в сетке,
// зоны 3–6 дописываются новыми методами. Меню: Ogonok/Build Level 1. Batchmode: -executeMethod OgonokBuilder.Build
//
// Сетка: ty растёт вниз, мир: y центра строки r = H-1-r+0.5. Дом сверху (строки 0..13), подвал под ним и правее (14..29).
// Символы: # камень, m мебель/ящик, I лёд, H лёд только от котла, R занавеска, T верёвка-мост (id wardrobe), L верёвка щеколды (id hatch),
// b плита моста, h плита люка, P провал (смерть), w паутина (вертикальная колонна = одна паутина), v решётка, p бумага, ~ лужа,
// d капля, e капля, которую гасит котёл, c свеча, k керосинка, G временная цель.
// Зоны 5–6 (P-3): n пень/бревно (коллайдер, укрытие от ветра), o крона (коллайдер, под ней снега нет), r смола (чекпоинт).
public static class OgonokBuilder
{
    const int W = 252, H = 94;
    const float Depth = 4f;
    static char[,] g;
    static Material mStone, mWall, mIce, mRope, mWax, mPaper, mWater, mGrate, mWood, mFire, mSparkAdd, mSteam, mDrop, mFurn, mWindow, mCurtain, mWeb, mLamp, mPipe;
    static Material mTrunk, mCrown, mSnowCap, mResin, mRune, mFar;
    static Sprite flameSprite, dotSprite;
    static GameObject dropPrefab;
    static readonly System.Collections.Generic.List<(string name, int x0, int y0, int x1, int y1, string hex)> rooms = new();
    static readonly System.Collections.Generic.List<System.Action<Transform>> extras = new(); // объекты зон, которым нужны ссылки на уже собранные
    static readonly System.Collections.Generic.List<Ice> heatIce = new();
    static readonly System.Collections.Generic.List<DripSpawner> boilerDrips = new();
    static readonly System.Collections.Generic.List<Vector3> zoneStarts = new();
    static int dripN;

    [MenuItem("Ogonok/Build Level 1")]
    public static void Build()
    {
        foreach (var d in new[] { "Materials", "Art", "Prefabs", "Scenes" }) Directory.CreateDirectory(Path.Combine(Application.dataPath, d));
        MakeAssets();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.02f, 0.022f, 0.035f);
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = new Color(0.02f, 0.025f, 0.04f); RenderSettings.fogDensity = 0.012f;

        LayoutGrid();
        var level = new GameObject("Level").transform;
        BuildTiles(level);
        foreach (var e in extras) e(level);
        BuildDecor(level);
        var player = BuildPlayer(zoneStarts[0]);
        BuildCamera(player);
        BuildVolume();
        var gm = new GameObject("GameManager").AddComponent<GameManager>();
        gm.player = player; gm.zoneStart = zoneStarts.ToArray();

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Level1.unity");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Level1.unity", true) };
        AssetDatabase.SaveAssets();
        Debug.Log("Ogonok: Level1 built");
    }

    // ---------- уровень: сетка из зон ----------
    static void Box(int a, int b, int c, int d, char ch) { for (int y = b; y <= d; y++) for (int x = a; x <= c; x++) g[y, x] = ch; }

    static void LayoutGrid()
    {
        g = new char[H, W];
        Box(0, 0, W - 1, H - 1, '.');
        rooms.Clear(); extras.Clear(); heatIce.Clear(); boilerDrips.Clear(); zoneStarts.Clear(); dripN = 0;
        Zone1House(0, 0);
        Zone2Basement(124, 14);
        zoneStarts.Add(zoneStarts[1]); zoneStarts.Add(zoneStarts[1]); // заглушки зон 3–4: зона 5 должна быть пятой. P-2 заменит их Zone3Sewer/Zone4Shaft
        Zone5Forest(0, 74);
        Zone6Bonfire(200, 74);
    }

    // Зона 1. Дом: пол этажа = строка 13, потолок 0, верх пола y = 17. Спальня «наверху»: её пол = строка 9 (y = 21).
    // Гостиная 1..29, коридор 31..68, кухня 70..95, спальня 97..138; люк в подвал a = 133..134.
    static void Zone1House(int x0, int y0)
    {
        void B(int a, int b, int c, int d, char ch) => Box(x0 + a, y0 + b, x0 + c, y0 + d, ch);
        void S(int a, int b, char ch) => g[y0 + b, x0 + a] = ch;
        void Room(string n, int a, int b, int c, int d, string hex) => rooms.Add((n, x0 + a, y0 + b, x0 + c, y0 + d, hex));
        B(0, 0, 139, 0, '#'); B(0, 13, 139, 13, '#'); B(0, 0, 0, 13, '#'); B(139, 0, 139, 13, '#');
        Room("Гостиная", 1, 1, 29, 12, "#4a3030"); Room("Коридор", 31, 1, 68, 12, "#2c3a4c"); Room("Кухня", 70, 1, 95, 12, "#2f4a3a");
        Room("Спальня", 97, 1, 138, 8, "#40324f"); Room("Лестница", 97, 9, 99, 12, "#40324f");
        zoneStarts.Add(C(x0 + 3, y0 + 12) + Vector3.up * 0.1f);

        // 1. Гостиная (обучение): камин, журнальный столик, кресло с газетой, комод со свечой
        extras.Add(l => BuildFireplace(l, x0 + 3, y0 + 12, false));
        B(8, 11, 9, 12, 'm'); B(12, 9, 13, 12, 'm'); S(12, 8, 'p'); S(19, 12, 'p');
        B(24, 11, 26, 12, 'm'); S(25, 10, 'c');
        B(30, 1, 30, 5, '#');

        // 2. Коридор: окно A (порыв только толкает), окно B (порыв гасит). Укрытия: мебель между игроком и окном.
        B(37, 11, 38, 12, 'm'); B(41, 11, 42, 12, 'm');
        S(37, 10, 'p'); S(41, 10, 'p');
        B(51, 11, 52, 12, 'm'); B(56, 11, 56, 12, 'm'); B(60, 11, 61, 12, 'm'); S(60, 10, 'p');
        extras.Add(l => { BuildWindow(l, x0 + 47, y0 + 12, 0f, 13f, "WindowFanA", 0f); BuildWindow(l, x0 + 67, y0 + 12, 0.75f, 19f, "WindowFanB", 1.5f); });
        S(68, 12, 'c'); B(69, 1, 69, 5, '#');

        // 3. Кухня: три капли, лужи, газеты; свеча и лёд на подоконнике-проёме
        B(72, 11, 73, 12, 'm'); S(72, 10, 'p');
        foreach (var a in new[] { 74, 79, 84 }) S(a, 1, 'd');
        foreach (var a in new[] { 76, 77, 81, 82, 87, 88, 89 }) S(a, 12, '~');
        S(84, 12, 'p'); S(93, 12, 'c');
        B(96, 1, 96, 10, '#'); S(96, 11, 'I'); S(96, 12, 'I');

        // 4. Спальня: ступени, свеча, паутина, занавеска, верёвка шкафа, провал 116..125 с мостом, свеча, щеколда и люк
        B(98, 11, 99, 12, 'm'); B(100, 9, 138, 13, '#');
        S(103, 8, 'c'); B(106, 4, 106, 8, 'w'); B(110, 1, 110, 8, 'R'); B(113, 2, 113, 6, 'T');
        B(116, 9, 125, 12, '.'); B(116, 12, 125, 12, 'P'); B(116, 9, 125, 9, 'b');
        S(128, 8, 'c'); B(133, 2, 133, 6, 'L');
        B(133, 10, 134, 13, '.'); S(133, 9, 'h'); S(134, 9, 'h');
        S(121, 6, 'p'); // сверху над мостом: подпитка за прыжок, если решился перепрыгнуть
    }

    // Зона 2. Подвал: потолок строка 0 (глобально 14), пол 15, верх пола y = 1. Шахта люка a = 9..10 (под люком дома).
    // Спуск 1..29, затопленный пол 31..62, котельная 64..95, сухой слив 97..126.
    static void Zone2Basement(int x0, int y0)
    {
        void B(int a, int b, int c, int d, char ch) => Box(x0 + a, y0 + b, x0 + c, y0 + d, ch);
        void S(int a, int b, char ch) => g[y0 + b, x0 + a] = ch;
        void Room(string n, int a, int b, int c, int d, string hex) => rooms.Add((n, x0 + a, y0 + b, x0 + c, y0 + d, hex));
        B(0, 0, 127, 0, '#'); B(0, 15, 127, 15, '#'); B(0, 0, 0, 15, '#'); B(127, 0, 127, 15, '#');
        B(9, 0, 10, 0, '.'); // шахта люка
        Room("Спуск", 1, 1, 29, 14, "#2a3238"); Room("Затопленный пол", 31, 1, 62, 14, "#1f3a48"); Room("Котельная", 64, 1, 95, 14, "#4a2e26");
        Room("Слив", 97, 12, 126, 14, "#26262c"); Room("Слив (камера)", 111, 6, 125, 11, "#26262c");
        var start = C(x0 + 12, y0 + 14); zoneStarts.Add(start);
        extras.Add(l =>
        {
            var cp = new GameObject("ZoneStart2"); cp.transform.SetParent(l, false);
            cp.transform.position = new Vector3(x0 + 10, H - (y0 + 9 + y0 + 14 + 1) / 2f, 0); cp.transform.localScale = new Vector3(3, 6, 4);
            cp.AddComponent<Checkpoint>().point = start;
        });

        // 1. Спуск: капли по ритму, сухой пол
        S(14, 14, 'p'); foreach (var a in new[] { 16, 21, 26 }) S(a, 1, 'd'); S(29, 14, 'p');
        B(30, 1, 30, 8, '#');

        // 2. Затопленный пол: лужи и ящики
        foreach (var (a, b) in new[] { (33, 35), (38, 41), (44, 47), (51, 52), (55, 57) }) B(a, 14, b, 14, '~');
        B(36, 13, 37, 14, 'm'); B(42, 13, 43, 14, 'm'); B(48, 13, 50, 14, 'm'); B(53, 13, 54, 14, 'm');
        S(37, 12, 'p'); S(43, 12, 'p'); S(49, 12, 'p'); S(60, 14, 'p');
        B(63, 1, 63, 8, '#');

        // 3. Котельная: керосинка, капли над котлом (гаснут после запуска), котёл
        S(66, 14, 'k');
        foreach (var a in new[] { 76, 81, 86 }) S(a, 1, 'e');
        extras.Add(l => BuildBoiler(l, x0 + 89, y0 + 14));

        // 4. Сухой слив: низкий тоннель 12..14, лёд только от котла, последняя капля, решётка, камера с целью
        B(96, 1, 126, 11, '#'); B(98, 12, 98, 14, 'H'); S(104, 12, 'd');
        B(110, 12, 110, 13, '#'); S(110, 14, 'v');
        B(111, 6, 125, 11, '.'); S(118, 14, 'G');
    }

    // Зона 5. Лес: x 0..199, строки 74..93 (локально 0..19). Потолок строка 0, основание 19. Земля = плиты от строки r до 19, игрок стоит в строке r-1.
    // Опушка 1..65 (пни = укрытия от ветра fan x=52, кроны без снега), ручей 66..80 (лёд над водой), бурелом 81..151 (брёвна, овраг 120..127 с мостом-стволом на верёвке),
    // подъём 152..199 (террасы по 1 м через 4 м, без подпитки). Холод ×1.5 на всю зону, снег по всей длине.
    // Смола-чекпоинты: x 30, 58, 86, 112, 130, 154 (между ними ≤ 28 м; от 154 до костра 72 м без подпитки).
    static void Zone5Forest(int x0, int y0)
    {
        void B(int a, int b, int c, int d, char ch) => Box(x0 + a, y0 + b, x0 + c, y0 + d, ch);
        void S(int a, int b, char ch) => g[y0 + b, x0 + a] = ch;
        void Ground(int a, int c, int r)
        {
            B(a, r, c, 19, '#');
            extras.Add(l => Cube("Snow", l, new Vector3(x0 + (a + c + 1) / 2f, H - (y0 + r) + 0.04f, 0), new Vector3(c - a + 1, 0.08f, Depth), mSnowCap, false));
        }
        // дерево: пень-укрытие (1 м) в строке над землёй, крона-коллайдер выше, ствол декором за плоскостью игры
        void Tree(int x, int gr, int cb)
        {
            B(x, gr - 1, x, gr - 1, 'n'); B(x - 2, cb, x + 2, cb + 1, 'o');
            float h = gr - cb - 2;
            extras.Add(l => Cube("TreeTrunk", l, new Vector3(x0 + x + 0.5f, H - (y0 + gr) + h / 2, 1.3f), new Vector3(0.9f, h, 0.9f), mTrunk, false));
        }
        B(0, 0, 199, 0, '#'); B(0, 19, 199, 19, '#'); B(0, 0, 0, 19, '#');
        rooms.Add(("Лес", x0 + 1, y0 + 1, x0 + 198, y0 + 18, "#10201c"));
        zoneStarts.Add(C(x0 + 3, y0 + 16) + Vector3.up * 0.1f);

        // 1. Опушка
        Ground(1, 65, 17);
        foreach (var x in new[] { 12, 19, 26, 33, 41, 47 }) Tree(x, 17, 8);
        S(16, 16, 'p'); S(24, 16, 'p'); S(38, 16, 'p');
        S(30, 16, 'r'); S(58, 16, 'r');
        extras.Add(l => BuildGale(l, x0 + 52.5f, H - (y0 + 17) + 4.5f, 0.1f, 45f));

        // 2. Ручей: лёд (тает от size >= 0.5) над водой-Pit, два просвета для прыжка
        Ground(81, 151, 17);
        B(66, 17, 80, 17, 'I'); B(66, 18, 80, 18, 'P'); B(71, 17, 72, 17, '.'); B(76, 17, 76, 17, '.');
        S(86, 16, 'r');

        // 3. Бурелом: брёвна (1 м), кроны; овраг 120..127: верёвка 118 роняет ствол-мост (id log)
        foreach (var (a, b) in new[] { (90, 91), (94, 96), (104, 105), (109, 110), (133, 135), (139, 140), (145, 146) }) B(a, 16, b, 16, 'n');
        foreach (var x in new[] { 100, 108, 137, 143 }) Tree(x, 17, 8);
        S(102, 16, 'p'); S(106, 16, 'p'); S(141, 16, 'p');
        S(112, 16, 'r'); S(130, 16, 'r');
        B(115, 8, 121, 9, 'o');
        B(120, 17, 127, 18, '.'); B(120, 18, 127, 18, 'P');
        extras.Add(l =>
        {
            for (int r = 10; r <= 14; r++) { var rp = Cube("Rope", l, C(x0 + 118, y0 + r), new Vector3(0.2f, 1, 0.2f), mRope, false).AddComponent<Rope>(); rp.trigger = true; rp.id = "log"; }
            var br = new GameObject("LogBridge"); br.transform.SetParent(l, false); br.AddComponent<Bridge>().id = "log";
            Cube("LogPlank", br.transform, new Vector3(x0 + 124f, H - (y0 + 17) - 0.5f, 0), new Vector3(8, 1, Depth), mTrunk);
        });

        // 4. Подъём: 11 террас, ни свечи, ни бумаги. Ветер только на верхней площадке
        for (int k = 0; k <= 10; k++) Ground(152 + 4 * k, 155 + 4 * k, 17 - k);
        Ground(196, 199, 7);
        S(154, 16, 'r');
        foreach (var k in new[] { 1, 3, 5 }) Tree(154 + 4 * k, 17 - k, 3);
        foreach (var k in new[] { 7, 9 }) B(154 + 4 * k, 17 - k - 1, 154 + 4 * k, 17 - k - 1, 'n');
        extras.Add(l => BuildGale(l, x0 + 198.5f, H - (y0 + 7) + 4.5f, 0.1f, 40f));

        // снег по всей зоне (цена 0.03, под кронами нет), холод, общий план в начале, далёкий круг камней
        extras.Add(l =>
        {
            for (int x = 6; x <= 198; x += 8)
            {
                var sn = new GameObject("Snowfall"); sn.transform.SetParent(l, false); sn.transform.position = C(x0 + x, y0 + 1);
                var sp = sn.AddComponent<DripSpawner>(); sp.dropPrefab = dropPrefab; sp.phase = x % 5 * 0.07f;
                sp.interval = 0.5f; sp.cost = 0.03f; sp.halfWidth = 4f; sp.gravity = 3f; sp.maxSpeed = 2.2f; sp.scale = 0.5f; sp.activeRange = 30f;
            }
            var cz = new GameObject("ColdZone"); cz.transform.SetParent(l, false);
            cz.transform.position = new Vector3(x0 + 100f, H - (y0 + 10), 0); cz.transform.localScale = new Vector3(200, 20, 6); cz.AddComponent<ColdZone>().mult = 1.5f;
            var gt = H - (y0 + 17);
            var zi = new GameObject("ZoneIntro5"); zi.transform.SetParent(l, false);
            zi.transform.position = new Vector3(x0 + 9f, gt + 4f, 0); zi.transform.localScale = new Vector3(14, 10, 6);
            var intro = zi.AddComponent<ZoneIntro>(); intro.focus = new Vector3(x0 + 26f, gt + 6f, 0); intro.seconds = 2f;
            for (int i = 0; i < 10; i++) // круг камней на далёком холме: виден только в общем плане
            {
                float a = i / 10f * Mathf.PI * 2;
                var st = Cube("FarStone", l, new Vector3(x0 + 42 + 6 * Mathf.Cos(a), gt + 11 + 0.7f * Mathf.Sin(a), 45), new Vector3(0.9f, 2f, 0.9f), mFar, false);
            }
            var glow = Sprite("FarGlow", l, flameSprite, mSparkAdd, 10f); glow.transform.position = new Vector3(x0 + 42, gt + 11.5f, 44);
        });
    }

    // Зона 6. Костёр: x 200..251, строки 74..93. Поляна на высоте строки 7 (продолжение подъёма), ветра нет, в центре холодный костёр x 226.
    static void Zone6Bonfire(int x0, int y0)
    {
        void B(int a, int b, int c, int d, char ch) => Box(x0 + a, y0 + b, x0 + c, y0 + d, ch);
        B(0, 0, 51, 0, '#'); B(0, 19, 51, 19, '#'); B(51, 0, 51, 19, '#'); B(0, 7, 51, 19, '#');
        extras.Add(l => Cube("Snow", l, new Vector3(x0 + 26f, H - (y0 + 7) + 0.04f, 0), new Vector3(52, 0.08f, Depth), mSnowCap, false));
        rooms.Add(("Поляна", x0, y0 + 1, x0 + 50, y0 + 6, "#17202e"));
        zoneStarts.Add(C(x0 + 3, y0 + 6) + Vector3.up * 0.1f);
        extras.Add(l => BuildBonfire(l, x0 + 26, y0 + 6));
    }

    // Порыв ветра влево без окна и лопастей: Fan в режиме окна, укрытия = коллайдеры стволов.
    static void BuildGale(Transform level, float x, float y, float burn, float range)
    {
        var root = new GameObject("GaleFan"); root.transform.SetParent(level, false); root.transform.position = new Vector3(x, y, 0);
        var fan = root.AddComponent<Fan>(); fan.burn = burn; fan.range = range;
    }

    // Смола на сосне: Candle (чекпоинт и подпитка) с янтарной каплей вместо свечи.
    static void BuildResin(Transform level, int tx, int ty)
    {
        var root = new GameObject("Resin"); root.transform.SetParent(level, false);
        root.transform.position = new Vector3(tx + 0.5f, FloorTop(ty), 0);
        Cube("Pine", root.transform, root.transform.position + new Vector3(0, 1.4f, 1.1f), new Vector3(0.8f, 3f, 0.8f), mTrunk, false);
        var amber = Cube("Amber", root.transform, root.transform.position + new Vector3(0, 0.3f, 0.2f), new Vector3(0.3f, 0.3f, 0.3f), mResin, false);
        amber.transform.rotation = Quaternion.Euler(0, 0, 45);
        var fl = Sprite("Flame", root.transform, flameSprite, mSparkAdd, 0.45f); fl.transform.localPosition = Vector3.up * 0.6f;
        var lt = new GameObject("Light").AddComponent<Light>(); lt.transform.SetParent(root.transform, false); lt.transform.localPosition = Vector3.up * 0.7f;
        lt.type = LightType.Point; lt.color = new Color(1, .68f, .22f); lt.range = 6; lt.intensity = 4; lt.shadows = LightShadows.None;
        var cd = root.AddComponent<Candle>(); cd.lightSrc = lt; cd.flameVis = fl;
    }

    // Холодный костёр под снегом, круг камней-рун. GiveFlame(all) + Bonfire = финал.
    static void BuildBonfire(Transform level, int tx, int ty)
    {
        var root = new GameObject("Bonfire"); root.transform.SetParent(level, false);
        var c = C(tx, ty); root.transform.position = c;
        foreach (var rot in new[] { -20f, 20f, 0f })
            Cube("Log", root.transform, c + new Vector3(0, -0.1f, 0.2f), new Vector3(2.6f, 0.4f, 0.5f), mWood, false).transform.rotation = Quaternion.Euler(0, rot * 2, rot);
        var snow = Cube("SnowCover", root.transform, c + new Vector3(0, 0.35f, 0.2f), new Vector3(2.8f, 0.8f, 1.4f), mSnowCap, false);
        var flame = Sprite("BigFlame", root.transform, flameSprite, mSparkAdd, 5f); flame.transform.position = c + new Vector3(0, 1.6f, 0.2f); flame.SetActive(false);
        var lt = new GameObject("Glow").AddComponent<Light>(); lt.transform.SetParent(root.transform, false); lt.transform.position = c + new Vector3(0, 1.6f, -1f);
        lt.type = LightType.Point; lt.color = new Color(1, .6f, .25f); lt.range = 0; lt.intensity = 0; lt.shadows = LightShadows.None; lt.enabled = false;
        var runes = new System.Collections.Generic.List<Renderer>();
        for (int i = 0; i < 9; i++)
        {
            float a = i / 8f * Mathf.PI;
            runes.Add(Cube("Rune", root.transform, c + new Vector3(8 * Mathf.Cos(a), 0.4f, 0.2f + 2.6f * Mathf.Sin(a)), new Vector3(0.9f, 1.8f, 0.7f), mRune, false).GetComponent<Renderer>());
        }
        var give = root.AddComponent<GiveFlame>(); give.all = true; give.hold = 1.2f; give.reach = 2.8f;
        var b = root.AddComponent<Bonfire>(); b.bigFlame = flame; b.snow = snow; b.glow = lt; b.runes = runes.ToArray();
    }

    static Vector3 C(int tx, int ty, float z = 0) => new(tx + 0.5f, H - 1 - ty + 0.5f, z);
    static float FloorTop(int ty) => H - 1 - ty; // верх тайла, лежащего в строке ty

    static GameObject Cube(string n, Transform parent, Vector3 center, Vector3 scale, Material m, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = n; go.transform.SetParent(parent, false);
        go.transform.position = center; go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = m;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static void BuildTiles(Transform level)
    {
        var br = new GameObject("Bridge"); br.transform.SetParent(level, false); br.AddComponent<Bridge>().id = "wardrobe";
        var hatch = new GameObject("Hatch"); hatch.transform.SetParent(level, false); hatch.AddComponent<Hatch>().id = "hatch";
        for (int ty = 0; ty < H; ty++)
            for (int tx = 0; tx < W; tx++)
            {
                char c = g[ty, tx];
                if (c == '#' || c == 'm' || c == 'n' || c == 'o')
                {
                    int e = tx; while (e + 1 < W && g[ty, e + 1] == c) e++;
                    int len = e - tx + 1;
                    var (sn, sm) = c == '#' ? ("Stone", mStone) : c == 'm' ? ("Furniture", mFurn) : c == 'n' ? ("Trunk", mTrunk) : ("Crown", mCrown);
                    Cube(sn, level, new Vector3(tx + len / 2f, H - 1 - ty + 0.5f, 0), new Vector3(len, 1, Depth), sm);
                    tx = e; continue;
                }
                var p = C(tx, ty);
                switch (c)
                {
                    case 'I': Cube("Ice", level, p, new Vector3(1, 1, 2), mIce).AddComponent<Ice>(); break;
                    case 'H': var hi = Cube("IceHot", level, p, new Vector3(1, 1, 2), mIce).AddComponent<Ice>(); hi.heatOnly = true; heatIce.Add(hi); break;
                    case 'R': Cube("Curtain", level, p, new Vector3(0.8f, 1, 2), mRope).AddComponent<Rope>(); break;
                    case 'T': case 'L':
                        var rp = Cube("Rope", level, p, new Vector3(0.2f, 1, 0.2f), mRope, false).AddComponent<Rope>(); rp.trigger = true; rp.id = c == 'T' ? "wardrobe" : "hatch"; break;
                    case 'v':
                        var v = Cube("Vent", level, p, new Vector3(1, 1, Depth), mGrate);
                        v.AddComponent<Vent>().col = v.GetComponent<Collider>(); break;
                    case 'p':
                        var pa = Cube("Paper", level, p + Vector3.down * 0.1f, new Vector3(0.35f, 0.45f, 0.05f), mPaper, false);
                        pa.transform.rotation = Quaternion.Euler(0, 0, -12); pa.AddComponent<Paper>(); break;
                    case '~':
                        var pu = new GameObject("Puddle"); pu.transform.SetParent(level, false); pu.transform.position = new Vector3(tx + 0.5f, FloorTop(ty), 0);
                        pu.AddComponent<Puddle>();
                        Cube("Water", pu.transform, pu.transform.position + Vector3.up * 0.15f, new Vector3(1, 0.3f, 1.6f), mWater, false); break;
                    case 'd': case 'e':
                        var dr = Cube("Drip", level, p, new Vector3(0.35f, 0.4f, 0.35f), mGrate, false);
                        var sp = dr.AddComponent<DripSpawner>(); sp.dropPrefab = dropPrefab; sp.phase = dripN++ % 3 * 0.7f;
                        if (c == 'e') boilerDrips.Add(sp); break;
                    case 'c': case 'k': BuildCandle(level, tx, ty, c == 'k'); break;
                    case 'r': BuildResin(level, tx, ty); break;
                    case 'G': BuildFireplace(level, tx, ty, true); break;
                    case 'b': Cube("Plank", br.transform, p, new Vector3(1, 1, Depth), mWood); break;
                    case 'h': if (hatch.transform.childCount == 0) hatch.transform.position = p; Cube("HatchSlab", hatch.transform, p, new Vector3(1, 1, Depth), mWood); break;
                    case 'w':
                        if (ty > 0 && g[ty - 1, tx] == 'w') break; // колонна уже собрана сверху
                        int ew = ty; while (ew + 1 < H && g[ew + 1, tx] == 'w') ew++;
                        Cube("Cobweb", level, new Vector3(tx + 0.5f, H - (ty + ew + 1) / 2f, 0), new Vector3(0.4f, ew - ty + 1, 0.6f), mWeb).AddComponent<Cobweb>(); break;
                    case 'P':
                        if (tx > 0 && g[ty, tx - 1] == 'P') break;
                        int ep = tx; while (ep + 1 < W && g[ty, ep + 1] == 'P') ep++;
                        var pit = new GameObject("Pit"); pit.transform.SetParent(level, false);
                        pit.transform.position = new Vector3(tx + (ep - tx + 1) / 2f, p.y, 0); pit.transform.localScale = new Vector3(ep - tx + 1, 1, 3);
                        pit.AddComponent<Pit>(); break;
                }
            }
        foreach (var (name, a, b, c2, d, hex) in rooms)
        {
            ColorUtility.TryParseHtmlString(hex, out var col);
            Cube("Room_" + name, level, new Vector3((a + c2 + 1) / 2f, H - (b + d + 1) / 2f, Depth / 2 + 0.5f), new Vector3(c2 - a + 1, d - b + 1, 1), Lit("Room" + rooms.FindIndex(r => r.name == name), col), false);
        }
    }

    static void BuildCandle(Transform level, int tx, int ty, bool lamp)
    {
        var root = new GameObject(lamp ? "Lamp" : "Candle"); root.transform.SetParent(level, false);
        root.transform.position = new Vector3(tx + 0.5f, FloorTop(ty), 0);
        var wax = GameObject.CreatePrimitive(PrimitiveType.Cylinder); wax.name = "Wax"; Object.DestroyImmediate(wax.GetComponent<Collider>());
        wax.transform.SetParent(root.transform, false); wax.transform.localPosition = Vector3.up * (lamp ? 0.3f : 0.2f);
        wax.transform.localScale = lamp ? new Vector3(0.36f, 0.3f, 0.36f) : new Vector3(0.22f, 0.2f, 0.22f);
        wax.GetComponent<Renderer>().sharedMaterial = lamp ? mLamp : mWax;
        var fl = Sprite("Flame", root.transform, flameSprite, mSparkAdd, lamp ? 0.8f : 0.55f); fl.transform.localPosition = Vector3.up * (lamp ? 0.85f : 0.62f);
        var lt = new GameObject("Light").AddComponent<Light>(); lt.transform.SetParent(root.transform, false); lt.transform.localPosition = Vector3.up * (lamp ? 0.95f : 0.7f);
        lt.type = LightType.Point; lt.color = lamp ? new Color(1, .5f, .18f) : new Color(1, .62f, .28f); lt.range = lamp ? 10 : 7; lt.intensity = lamp ? 7 : 5; lt.shadows = LightShadows.None;
        var cd = root.AddComponent<Candle>(); cd.lightSrc = lt; cd.flameVis = fl;
    }

    static GameObject Sprite(string n, Transform parent, Sprite s, Material m, float scale)
    {
        var go = new GameObject(n); go.transform.SetParent(parent, false); go.transform.localScale = Vector3.one * scale;
        var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = s; sr.sharedMaterial = m;
        go.AddComponent<Billboard>();
        return go;
    }

    // Разбитое окно на стене = Fan с занавеской. Корень на высоте 4.5 м над полом: от него частицы ветра строятся вверх и вниз.
    static void BuildWindow(Transform level, int tx, int ty, float burn, float range, string n, float offset)
    {
        float fl = FloorTop(ty), x = tx + 0.5f;
        var root = new GameObject(n); root.transform.SetParent(level, false); root.transform.position = new Vector3(x, fl + 4.5f, 0);
        Cube("Pane", root.transform, new Vector3(x, fl + 4.5f, 2.2f), new Vector3(1.8f, 2.6f, 0.1f), mWindow, false);
        var cur = Cube("WindowCurtain", root.transform, new Vector3(x + 0.8f, fl + 4.5f, 1.9f), new Vector3(0.12f, 3f, 0.3f), mCurtain, false);
        var lt = new GameObject("Moon").AddComponent<Light>(); lt.transform.SetParent(root.transform, false); lt.transform.position = new Vector3(x, fl + 4.5f, 0.5f);
        lt.type = LightType.Point; lt.color = new Color(.5f, .62f, 1f); lt.range = 9; lt.intensity = 2.2f; lt.shadows = LightShadows.None;
        var fan = root.AddComponent<Fan>(); fan.curtain = cur.transform; fan.burn = burn; fan.range = range; fan.offset = offset;
    }

    // Котёл: тело и трубы декором за плоскостью игры; GiveFlame на полу перед ним. Лёд слива и капели котельной цепляются после сборки сетки.
    static void BuildBoiler(Transform level, int tx, int ty)
    {
        var root = new GameObject("Boiler"); root.transform.SetParent(level, false); root.transform.position = C(tx, ty);
        var give = root.AddComponent<GiveFlame>(); give.reach = 2.6f;
        var pipes = new System.Collections.Generic.List<Renderer>();
        Cube("BoilerBody", root.transform, root.transform.position + new Vector3(0, 2.5f, 1.2f), new Vector3(5, 6, 0.8f), mFire, false);
        foreach (var (off, sc) in new[] { (new Vector3(0, 8.5f, 1.2f), new Vector3(0.5f, 6f, 0.5f)), (new Vector3(3.5f, 11.5f, 1.2f), new Vector3(8, 0.5f, 0.5f)), (new Vector3(-3.5f, 11.5f, 1.2f), new Vector3(8, 0.5f, 0.5f)) })
            pipes.Add(Cube("Pipe", root.transform, root.transform.position + off, sc, mPipe, false).GetComponent<Renderer>());
        var lt = new GameObject("Glow").AddComponent<Light>(); lt.transform.SetParent(root.transform, false); lt.transform.position = root.transform.position + new Vector3(0, 2.5f, -0.5f);
        lt.type = LightType.Point; lt.color = new Color(1, .45f, .15f); lt.range = 12; lt.intensity = 6; lt.shadows = LightShadows.None; lt.enabled = false;
        var b = root.AddComponent<Boiler>(); b.ice = heatIce.ToArray(); b.drips = boilerDrips.ToArray(); b.pipes = pipes.ToArray(); b.glow = lt;
    }

    // Камин: goal = false — холодный очаг на старте; true — временная цель в конце зоны 2 («продолжение следует»).
    static void BuildFireplace(Transform level, int tx, int ty, bool goal)
    {
        var root = new GameObject(goal ? "Goal" : "Fireplace"); root.transform.SetParent(level, false);
        var c = C(tx, ty); root.transform.position = c;
        if (goal) root.AddComponent<Goal>();
        float z = 1.4f;
        Cube("Left", root.transform, c + new Vector3(-2, 2, z), new Vector3(1, 4, 1), mFire, false);
        Cube("Right", root.transform, c + new Vector3(2, 2, z), new Vector3(1, 4, 1), mFire, false);
        Cube("Top", root.transform, c + new Vector3(0, 4, z), new Vector3(5, 1, 1), mFire, false);
        Cube("Log", root.transform, c + new Vector3(0, -0.3f, 0.4f), new Vector3(2.2f, 0.3f, 0.5f), mWood, false);
        var big = Sprite("BigFlame", root.transform, flameSprite, mSparkAdd, goal ? 3.2f : 0.6f); big.transform.position = c + new Vector3(0, goal ? 1.1f : -0.1f, 0.2f);
        var lt = new GameObject("Light").AddComponent<Light>(); lt.transform.SetParent(root.transform, false); lt.transform.position = c + new Vector3(0, goal ? 1.1f : 0.2f, -1f);
        lt.type = LightType.Point; lt.color = new Color(1, .6f, .25f); lt.range = goal ? 18 : 5; lt.intensity = goal ? 22 : 2; lt.shadows = LightShadows.None;
    }

    // Цветной мебельный декор за плоскостью игры: оживает рядом с огнём. floorY = верх пола в этом диапазоне x.
    static void BuildDecor(Transform level)
    {
        var cols = new[] { "#6b3f2a", "#3f6b52", "#7a5a2a", "#4a3f7a", "#7a3a4a" };
        var mats = new Material[cols.Length];
        for (int i = 0; i < cols.Length; i++) { ColorUtility.TryParseHtmlString(cols[i], out var c); mats[i] = Lit("Deco" + i, c); }
        uint s = 7; float R() { s = (uint)((ulong)s * 16807 % 2147483647); return s / 2147483647f; }
        void Range(float xa, float xb, float floorY, float maxH)
        {
            for (float x = xa; x < xb; x += 2f + R() * 3.4f)
            {
                float k = R(); var m = mats[Mathf.FloorToInt(R() * mats.Length)];
                if (k < 0.55f) { float h = Mathf.Min(maxH, 2.8f + R() * 2.2f), w = 1.8f + R(); Cube("Shelf", level, new Vector3(x, floorY + h / 2, 1.5f), new Vector3(w, h, 0.7f), m, false); }
                else { Cube("Frame", level, new Vector3(x, floorY + 3f + R() * 3f, 2.0f), new Vector3(1f, 1.4f, 0.2f), m, false); }
            }
        }
        Range(3, 96, FloorTop(12), 6); Range(101, 137, FloorTop(8), 4);   // дом: этаж и спальня
        Range(126, 150, FloorTop(28), 8); Range(156, 186, FloorTop(28), 8); Range(190, 218, FloorTop(28), 8); // подвал: спуск, пол, котельная
    }

    // ---------- игрок, камера, пост-обработка ----------
    static FlamePlayer BuildPlayer(Vector3 start)
    {
        var root = new GameObject("Player"); root.transform.position = start;
        var f = root.AddComponent<Flame>(); var fp = root.AddComponent<FlamePlayer>(); fp.flame = f;
        var vis = Sprite("FlameSprite", root.transform, flameSprite, mSparkAdd, 1f); f.visual = vis.transform;
        var lt = new GameObject("FlameLight").AddComponent<Light>(); lt.transform.SetParent(root.transform, false);
        lt.type = LightType.Point; lt.color = new Color(1, .6f, .25f); lt.shadows = LightShadows.Soft; lt.shadowStrength = 1f; lt.shadowBias = 0.05f;
        f.flameLight = lt;
        var sp = MakePS("Sparks", root.transform, mSparkAdd);
        var m = sp.main; m.startLifetime = 1.3f; m.startSpeed = 0.5f; m.startSize = 0.07f; m.gravityModifier = 0.12f; m.startColor = new Color(1, .8f, .35f, 1);
        var sh = sp.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.15f;
        Fade(sp, new Color(1, .85f, .4f), new Color(1, .35f, .05f));
        f.sparks = sp;
        var st = MakePS("Steam", root.transform, mSteam);
        var sm = st.main; sm.startLifetime = 1.1f; sm.startSpeed = 0.4f; sm.startSize = 0.3f; sm.startColor = new Color(.8f, .83f, .9f, .35f);
        var se = st.emission; se.rateOverTime = 0;
        var sz = st.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 2f));
        Fade(st, new Color(.8f, .83f, .9f, .35f), new Color(.8f, .83f, .9f, 0));
        f.steam = st;
        return fp;
    }

    static ParticleSystem MakePS(string n, Transform parent, Material mat)
    {
        var go = new GameObject(n); go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        var m = ps.main; m.simulationSpace = ParticleSystemSimulationSpace.World; m.loop = true; m.playOnAwake = true; m.maxParticles = 600;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
        return ps;
    }

    static void Fade(ParticleSystem ps, Color a, Color b)
    {
        var col = ps.colorOverLifetime; col.enabled = true;
        var gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(a, 0), new GradientColorKey(b, 1) }, new[] { new GradientAlphaKey(a.a, 0), new GradientAlphaKey(0, 1) });
        col.color = gr;
    }

    static void BuildCamera(FlamePlayer target)
    {
        var go = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = go.AddComponent<Camera>(); go.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.02f, 0.025f, 0.04f);
        cam.fieldOfView = 40; cam.nearClipPlane = 0.3f; cam.farClipPlane = 120;
        go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
        var cf = go.AddComponent<CamFollow>(); cf.target = target; cf.maxX = W - 8;
        go.transform.position = new Vector3(8, FloorTop(12) + 3.6f, -13); go.transform.rotation = Quaternion.Euler(11, 0, 0);
    }

    static void BuildVolume()
    {
        const string path = "Assets/Settings/OgonokVolume.asset";
        AssetDatabase.DeleteAsset(path);
        var prof = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(prof, path);
        T Add<T>() where T : VolumeComponent { var c = prof.Add<T>(true); AssetDatabase.AddObjectToAsset(c, prof); return c; }
        var bloom = Add<Bloom>(); bloom.threshold.Override(0.8f); bloom.intensity.Override(1.1f); bloom.scatter.Override(0.65f);
        var vig = Add<Vignette>(); vig.intensity.Override(0.4f); vig.smoothness.Override(0.5f);
        var tm = Add<Tonemapping>(); tm.mode.Override(TonemappingMode.Neutral);
        var ca = Add<ColorAdjustments>(); ca.contrast.Override(12f); ca.saturation.Override(8f);
        EditorUtility.SetDirty(prof);
        var go = new GameObject("PostFX"); var v = go.AddComponent<Volume>(); v.isGlobal = true; v.sharedProfile = prof;
    }

    // ---------- плейсхолдерные ассеты ----------
    static Material Lit(string name, Color c)
    {
        var path = $"Assets/Materials/{name}.mat"; AssetDatabase.DeleteAsset(path);
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", 0.12f);
        AssetDatabase.CreateAsset(m, path); return m;
    }

    static Material Shaded(string name, string shader, Sprite tex, Color tint)
    {
        var path = $"Assets/Materials/{name}.mat"; AssetDatabase.DeleteAsset(path);
        var m = new Material(Shader.Find(shader)); m.mainTexture = tex.texture; m.color = tint;
        AssetDatabase.CreateAsset(m, path); return m;
    }

    static Sprite MakeSprite(string name, int n, System.Func<float, float, Color> f)
    {
        var path = $"Assets/Art/{name}.png";
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) t.SetPixel(x, y, f((x + 0.5f) / n * 2 - 1, (y + 0.5f) / n * 2 - 1));
        File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), path), t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = n; imp.mipmapEnabled = false; imp.alphaIsTransparency = true; imp.wrapMode = TextureWrapMode.Clamp;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void MakeAssets()
    {
        // Плейсхолдер пламени: капля. Заменить на рисованный спрайт от команды (Assets/Art/FlamePlaceholder.png).
        flameSprite = MakeSprite("FlamePlaceholder", 128, (u, v) =>
        {
            float d = Mathf.Sqrt(u * u * (1 + Mathf.Max(0, v) * 1.6f) + (v < 0 ? v * v * 1.3f : v * v * 0.5f));
            float a = Mathf.Clamp01(1 - d); a = a * a * (3 - 2 * a);
            return new Color(Mathf.Lerp(1, 1, a), Mathf.Lerp(0.42f, 0.97f, a * a), Mathf.Lerp(0.08f, 0.75f, a * a), a);
        });
        dotSprite = MakeSprite("Dot", 64, (u, v) => { float a = Mathf.Clamp01(1 - Mathf.Sqrt(u * u + v * v)); return new Color(1, 1, 1, a * a); });
        mSparkAdd = Shaded("SparkAdd", "Mobile/Particles/Additive", dotSprite, Color.white);
        AssetDatabase.DeleteAsset("Assets/Resources/SparkAdd.mat"); Directory.CreateDirectory(Path.Combine(Application.dataPath, "Resources")); AssetDatabase.CopyAsset("Assets/Materials/SparkAdd.mat", "Assets/Resources/SparkAdd.mat"); // Fan грузит через Resources
        mSteam = Shaded("Steam", "Mobile/Particles/Alpha Blended", dotSprite, Color.white);
        mStone = Lit("Stone", new Color(0.30f, 0.32f, 0.40f));
        mWall = Lit("Wall", new Color(0.09f, 0.1f, 0.15f));
        mIce = Lit("Ice", new Color(0.55f, 0.78f, 0.92f)); mIce.SetFloat("_Smoothness", 0.8f);
        mRope = Lit("Rope", new Color(0.62f, 0.45f, 0.28f));
        mWax = Lit("Wax", new Color(0.92f, 0.89f, 0.8f));
        mPaper = Lit("Paper", new Color(0.85f, 0.81f, 0.68f));
        mWater = Lit("Water", new Color(0.27f, 0.5f, 0.8f)); mWater.SetFloat("_Smoothness", 0.9f);
        mGrate = Lit("Grate", new Color(0.18f, 0.19f, 0.26f));
        mWood = Lit("Wood", new Color(0.54f, 0.4f, 0.26f));
        mFire = Lit("Fireplace", new Color(0.29f, 0.25f, 0.22f));
        mDrop = Lit("Drop", new Color(0.44f, 0.69f, 0.91f));
        mFurn = Lit("Furniture", new Color(0.42f, 0.30f, 0.20f));
        mWindow = Lit("Window", new Color(0.35f, 0.45f, 0.62f)); mCurtain = Lit("WindowCurtain", new Color(0.55f, 0.28f, 0.30f));
        mTrunk = Lit("Trunk", new Color(0.25f, 0.17f, 0.12f)); mCrown = Lit("Crown", new Color(0.08f, 0.2f, 0.16f)); mSnowCap = Lit("SnowCap", new Color(0.82f, 0.88f, 0.95f));
        mResin = Lit("Resin", new Color(0.95f, 0.6f, 0.15f)); mRune = Lit("Rune", new Color(0.25f, 0.28f, 0.36f));
        mFar = new Material(Shader.Find("Universal Render Pipeline/Unlit")); mFar.color = new Color(0.16f, 0.18f, 0.24f); AssetDatabase.DeleteAsset("Assets/Materials/Far.mat"); AssetDatabase.CreateAsset(mFar, "Assets/Materials/Far.mat");
        mWeb = Lit("Cobweb", new Color(0.85f, 0.85f, 0.88f)); mLamp = Lit("Lamp", new Color(0.55f, 0.38f, 0.18f)); mPipe = Lit("Pipe", new Color(0.22f, 0.24f, 0.30f));

        var drop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(drop.GetComponent<Collider>());
        drop.transform.localScale = new Vector3(0.12f, 0.18f, 0.12f); drop.GetComponent<Renderer>().sharedMaterial = mDrop;
        drop.AddComponent<Drop>();
        dropPrefab = PrefabUtility.SaveAsPrefabAsset(drop, "Assets/Prefabs/Drop.prefab");
        Object.DestroyImmediate(drop);
    }
}
