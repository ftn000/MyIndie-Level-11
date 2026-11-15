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
// Символы: # камень, m мебель/ящик, I лёд, i тонкий лёд (рушится сразу, если форма не маленькая или рывок), H лёд только от котла, R занавеска, T верёвка-мост (id wardrobe), L верёвка щеколды (id hatch),
// b плита моста, h плита люка, P провал (смерть), w паутина (вертикальная колонна = одна паутина), v решётка, p бумага, ~ лужа,
// d капля, e капля, которую гасит котёл, c свеча, k керосинка, f газовый факел, g мусорный пакет (бумага), j верёвка-ремень вентилятора (id fanbelt), G выход из зоны (ZoneExit в зону 3).
// Зоны 5–6 (P-3): n пень/бревно (коллайдер, укрытие от ветра), o крона (коллайдер, под ней снега нет), r смола (чекпоинт).
public static partial class OgonokBuilder
{
    // P-4: вставки в зоны (м): дом, подвал, канализация, лес. Зоны 2, 4, 6 сдвинуты вправо на сумму вставок слева от них.
    const int D1 = 90, D2 = 80, D3 = 110, D5 = 190, W = 442, H = 94;
    const float Depth = 4f;
    static char[,] g;
    static Material mWall, mIce, mRope, mWax, mPaper, mWater, mGrate, mWood, mFire, mSparkAdd, mSteam, mDrop, mFurn, mWindow, mCurtain, mWeb, mLamp, mPipe, mRaft, mBlade, mBag, mBubble;
    static Material mTrunk, mCrown, mSnowCap, mResin, mRune, mFar;
    static Material mHouse, mCellar, mSewer, mRock, mCloth; // P-13: поверхности '#' по зонам и ткань
    static Sprite flameSprite, dotSprite;
    static GameObject dropPrefab, bubblePrefab;
    static readonly System.Collections.Generic.List<(string name, int x0, int y0, int x1, int y1, string hex)> rooms = new();
    static readonly System.Collections.Generic.List<System.Action<Transform>> extras = new(); // объекты зон, которым нужны ссылки на уже собранные
    static readonly System.Collections.Generic.List<Ice> heatIce = new();
    static readonly System.Collections.Generic.List<DripSpawner> boilerDrips = new();
    static readonly System.Collections.Generic.List<Vector3> zoneStarts = new();
    static int dripN;

    // Превью арта команды (P-4): отдельная сцена Level1Art, грейбокс без него собирается как раньше (Level1). Файлы: Assets/Art/Preview.
    [MenuItem("Ogonok/Build Level 1")]
    public static void Build() => BuildLevel(false);
    [MenuItem("Ogonok/Build Level 1 (Art Preview)")]
    public static void BuildArt() => BuildLevel(true);
    static bool art;
    static Sprite wispSprite;
    static Material mWisp;

    static void BuildLevel(bool withArt)
    {
        art = withArt;
        foreach (var d in new[] { "Materials", "Art", "Prefabs", "Scenes" }) Directory.CreateDirectory(Path.Combine(Application.dataPath, d));
        MakeAssets();
        BeginBoxMeshes();
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
        BuildAudio();
        BuildVolume();
        var gm = new GameObject("GameManager").AddComponent<GameManager>();
        gm.player = player; gm.zoneStart = zoneStarts.ToArray();
        gm.mapRects = ZoneRects();

        if (art) BuildArtBackdrop();
        EndBoxMeshes();
        EditorSceneManager.SaveScene(scene, art ? "Assets/Scenes/Level1Art.unity" : "Assets/Scenes/Level1.unity");
        if (!art) EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Level1.unity", true) };
        AssetDatabase.SaveAssets();
        Debug.Log(art ? "Ogonok: Level1Art built" : "Ogonok: Level1 built");
    }

    // Габариты зон в метрах (страховка «вне карты»): bbox непустых клеток в полосе сетки каждой зоны.
    // Полосы (кол. от-до, строки от-до) совпадают с раскладкой LayoutGrid: зоны 3 и 4 делят строки, зоны 5 и 6 тоже.
    static Rect[] ZoneRects()
    {
        var bands = new[] { (0, W - 1, 0, 13), (0, W - 1, 14, 29), (0, 269, 56, 73), (270, W - 1, 30, 73), (0, 389, 74, H - 1), (390, W - 1, 74, H - 1) };
        var res = new System.Collections.Generic.List<Rect>();
        foreach (var (c0, c1, r0, r1) in bands)
        {
            int ax = int.MaxValue, bx = -1, ay = int.MaxValue, by = -1;
            for (int y = r0; y <= r1; y++) for (int x = c0; x <= c1; x++)
                if (g[y, x] != '.') { ax = Mathf.Min(ax, x); bx = Mathf.Max(bx, x); ay = Mathf.Min(ay, y); by = Mathf.Max(by, y); }
            if (bx >= 0) res.Add(Rect.MinMaxRect(ax, H - (by + 1), bx + 1, H - ay));
        }
        return res.ToArray();
    }

    // ---------- уровень: сетка из зон ----------
    static void Box(int a, int b, int c, int d, char ch) { for (int y = b; y <= d; y++) for (int x = a; x <= c; x++) g[y, x] = ch; }

    static void LayoutGrid()
    {
        g = new char[H, W];
        Box(0, 0, W - 1, H - 1, '.');
        rooms.Clear(); extras.Clear(); heatIce.Clear(); boilerDrips.Clear(); zoneStarts.Clear(); dripN = 0;
        Zone1House(0, 0);
        Zone2Basement(124 + D1, 14);
        Zone3Sewer(0, 56);
        Zone4Shaft(160 + D3, 30);
        Zone5Forest(0, 74);
        Zone6Bonfire(200 + D5, 74);
    }

    // Зона 1. Дом: пол этажа = строка 13, потолок 0, верх пола y = 17. Спальня «наверху»: её пол = строка 9 (y = 21).
    // Гостиная 1..29, коридор 31..68, кухня 70..95; P-4: кладовка 97..120, ванная 121..148, холл со сквозняком 149..186 (вставка D1 = 90 м);
    // спальня (97..138)+D1, люк в подвал a = (133..134)+D1. Чекпоинты (свечи): кухня 93, спальня 103+D1. Остальное топливо: бумага.
    static void Zone1House(int x0, int y0)
    {
        void B(int a, int b, int c, int d, char ch) => Box(x0 + a, y0 + b, x0 + c, y0 + d, ch);
        void S(int a, int b, char ch) => g[y0 + b, x0 + a] = ch;
        void Bd(int a, int b, int c, int d, char ch) => Box(x0 + D1 + a, y0 + b, x0 + D1 + c, y0 + d, ch); // спальня и дальше: сдвиг на вставку
        void Sd(int a, int b, char ch) => g[y0 + b, x0 + D1 + a] = ch;
        void Room(string n, int a, int b, int c, int d, string hex) => rooms.Add((n, x0 + a, y0 + b, x0 + c, y0 + d, hex));
        B(0, 0, 139 + D1, 0, '#'); B(0, 13, 139 + D1, 13, '#'); B(0, 0, 0, 13, '#'); B(139 + D1, 0, 139 + D1, 13, '#');
        Room("Гостиная", 1, 1, 29, 12, "#4a3030"); Room("Коридор", 31, 1, 68, 12, "#2c3a4c"); Room("Кухня", 70, 1, 95, 12, "#2f4a3a");
        Room("Кладовка", 97, 1, 120, 12, "#2e2824"); Room("Ванная", 121, 1, 148, 12, "#2a4650"); Room("Холл", 149, 1, 186, 12, "#3a3048");
        Room("Спальня", 97 + D1, 1, 138 + D1, 8, "#40324f"); Room("Лестница", 97 + D1, 9, 99 + D1, 12, "#40324f");
        zoneStarts.Add(C(x0 + 3, y0 + 12) + Vector3.up * 0.1f);

        // 1. Гостиная (обучение): камин, журнальный столик, кресло с газетой, комод с газетой
        extras.Add(l => BuildFireplace(l, x0 + 3, y0 + 12, false));
        if (art) extras.Add(l => Room1Final(l)); // P-12: гостиная из слоёв room 1 вместо грейбокс-мебели
        else { B(8, 11, 9, 12, 'm'); B(12, 9, 13, 12, 'm'); S(12, 8, 'p'); S(19, 12, 'p'); B(24, 11, 26, 12, 'm'); S(25, 10, 'p'); }
        B(30, 1, 30, 5, '#');

        // 2. Коридор: окно A (порыв только толкает), окно B (порыв гасит). Укрытия: мебель между игроком и окном.
        B(37, 11, 38, 12, 'm'); B(41, 11, 42, 12, 'm');
        S(37, 10, 'p'); S(41, 10, 'p');
        B(51, 11, 52, 12, 'm'); B(56, 11, 56, 12, 'm'); B(60, 11, 61, 12, 'm'); S(60, 10, 'p');
        extras.Add(l => { BuildWindow(l, x0 + 47, y0 + 12, 0f, 13f, "WindowFanA", 0f); BuildWindow(l, x0 + 67, y0 + 12, 0.75f, 19f, "WindowFanB", 1.5f); });
        S(66, 12, 'p'); B(69, 1, 69, 5, '#');

        // 3. Кухня: три капли, лужи, газеты; свеча (чекпоинт) и лёд на подоконнике-проёме
        B(72, 11, 73, 12, 'm'); S(72, 10, 'p');
        foreach (var a in new[] { 74, 79, 84 }) S(a, 1, 'd');
        foreach (var a in new[] { 76, 77, 81, 82, 87, 88, 89 }) S(a, 12, '~');
        S(84, 12, 'p'); S(93, 12, 'c');
        B(96, 1, 96, 10, '#'); S(96, 11, 'I'); S(96, 12, 'I');

        // 3b. Кладовка (темно): полки с газетами и колонны паутины, каждая берёт 0.05
        foreach (var a in new[] { 100, 107, 114 }) { B(a, 11, a + 1, 12, 'm'); S(a, 10, 'p'); }
        foreach (var a in new[] { 98, 103, 110, 117 }) B(a, 1, a, 12, 'w');
        S(120, 12, 'p'); B(120, 1, 120, 5, '#');

        // 3c. Ванная: капли над лужами, ванна-ящик, лёд на выходе (нужно большое пламя: газеты перед ним)
        foreach (var a in new[] { 124, 130, 136 }) { S(a, 1, 'd'); B(a - 1, 12, a + 1, 12, '~'); }
        B(139, 11, 140, 12, 'm'); S(139, 10, 'p'); S(142, 12, 'p'); S(144, 12, 'p'); S(145, 12, 'p');
        B(146, 1, 146, 10, '#'); S(146, 11, 'I'); S(146, 12, 'I');

        // 3d. Холл: сквозняк окна C (справа), мебель-укрытия, между ними паутина. Пережидай порыв за мебелью
        S(150, 12, 'p');
        foreach (var a in new[] { 153, 160, 167, 174 }) { B(a, 11, a + 1, 12, 'm'); S(a, 10, 'p'); }
        foreach (var a in new[] { 157, 164, 171, 178 }) B(a, 1, a, 12, 'w');
        S(182, 12, 'p');
        extras.Add(l => BuildWindow(l, x0 + 185, y0 + 12, 0.4f, 30f, "WindowFanC", 0.8f));

        // 4. Спальня: ступени, свеча (чекпоинт), паутина, занавеска, верёвка шкафа, провал 116..125 с мостом, щеколда и люк
        Bd(98, 11, 99, 12, 'm'); Bd(100, 9, 138, 13, '#');
        Sd(103, 8, 'c'); Bd(106, 4, 106, 8, 'w'); Bd(110, 1, 110, 8, 'R'); Bd(113, 2, 113, 6, 'T');
        Bd(116, 9, 125, 12, '.'); Bd(116, 12, 125, 12, 'P'); Bd(116, 9, 125, 9, 'b');
        Sd(128, 8, 'p'); Bd(133, 2, 133, 6, 'L');
        Bd(133, 10, 134, 13, '.'); Sd(133, 9, 'h'); Sd(134, 9, 'h');
        Sd(121, 6, 'p'); // сверху над мостом: подпитка за прыжок, если решился перепрыгнуть
    }

    // Зона 2. Подвал: потолок строка 0 (глобально 14), пол 15, верх пола y = 1. Шахта люка a = 9..10 (под люком дома).
    // Спуск 1..29, затопленный пол 31..62, P-4: винный погреб 64..143 (вставка D2 = 80: бочки и мост-полка на верёвке, капельная галерея,
    // второй мост-полка под каплями), котельная (64..95)+D2, сухой слив (97..126)+D2. Чекпоинт: керосинка перед котлом.
    static void Zone2Basement(int x0, int y0)
    {
        void B(int a, int b, int c, int d, char ch) => Box(x0 + a, y0 + b, x0 + c, y0 + d, ch);
        void S(int a, int b, char ch) => g[y0 + b, x0 + a] = ch;
        void B2(int a, int b, int c, int d, char ch) => Box(x0 + D2 + a, y0 + b, x0 + D2 + c, y0 + d, ch);
        void S2(int a, int b, char ch) => g[y0 + b, x0 + D2 + a] = ch;
        void Room(string n, int a, int b, int c, int d, string hex) => rooms.Add((n, x0 + a, y0 + b, x0 + c, y0 + d, hex));
        B(0, 0, 127 + D2, 0, '#'); B(0, 15, 127 + D2, 15, '#'); B(0, 0, 0, 15, '#'); B(127 + D2, 0, 127 + D2, 15, '#');
        B(9, 0, 10, 0, '.'); // шахта люка
        Room("Спуск", 1, 1, 29, 14, "#2a3238"); Room("Затопленный пол", 31, 1, 62, 14, "#1f3a48"); Room("Винный погреб", 64, 1, 142, 14, "#3a2830");
        Room("Котельная", 64 + D2, 1, 95 + D2, 14, "#4a2e26");
        Room("Слив", 97 + D2, 12, 126 + D2, 14, "#26262c"); Room("Слив (камера)", 111 + D2, 6, 125 + D2, 11, "#26262c");
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

        // 2b. Винный погреб. Пол = строка 15, мост-полка кладётся в строке 15 кодом (extras), под ним пустота и Pit в той же строке.
        // A (64..95): бочки, провал 76..83: верёвка 'U' (id shelf1) роняет полку, над ней две капли.
        S(66, 14, 'p'); B(68, 13, 69, 14, 'm'); S(68, 12, 'p');
        B(73, 2, 73, 12, 'U'); B(76, 15, 83, 15, 'P'); S(78, 1, 'd'); S(81, 1, 'd');
        B(88, 13, 89, 14, 'm'); S(88, 12, 'p'); S(92, 14, 'p');
        // B (96..119): капельная галерея, лужи под каплями, газеты между ними
        foreach (var a in new[] { 98, 103, 108, 113, 118 }) { S(a, 1, 'd'); B(a, 14, a + 1, 14, '~'); }
        S(101, 14, 'p'); S(105, 14, 'p'); S(111, 14, 'p'); S(116, 14, 'p');
        // C (120..142): второй провал 128..137, верёвка 'V' (id shelf2), три капли над полкой
        B(122, 13, 123, 14, 'm'); S(125, 14, 'p'); B(124, 2, 124, 12, 'V'); B(128, 15, 137, 15, 'P');
        S(130, 1, 'd'); S(133, 1, 'd'); S(136, 1, 'd'); B(140, 13, 141, 14, 'm'); S(140, 12, 'p'); S(139, 14, 'p');
        B(143, 1, 143, 8, '#');
        extras.Add(l =>
        {
            foreach (var (id, a, c) in new[] { ("shelf1", 76, 83), ("shelf2", 128, 137) })
            {
                var br = new GameObject("Bridge_" + id); br.transform.SetParent(l, false); var bc = br.AddComponent<Bridge>(); bc.id = id; bc.kind = Bridge.Kind.Shelf;
                for (int x = a; x <= c; x++) Cube("ShelfPlank", br.transform, C(x0 + x, y0 + 15), new Vector3(1, 1, Depth), mWood);
            }
        });

        // 3. Котельная: керосинка (чекпоинт), капли над котлом (гаснут после запуска), котёл
        S2(66, 14, 'k');
        foreach (var a in new[] { 76, 81, 86 }) S2(a, 1, 'e');
        extras.Add(l => BuildBoiler(l, x0 + D2 + 89, y0 + 14));

        // 4. Сухой слив: низкий тоннель 12..14, лёд только от котла, последняя капля, решётка, камера с целью
        B2(96, 1, 126, 11, '#'); B2(98, 12, 98, 14, 'H'); S2(104, 12, 'd');
        B2(110, 12, 110, 13, '#'); S2(110, 14, 'v');
        B2(111, 6, 125, 11, '.'); S2(118, 14, 'G');
    }

    // Зона 3. Канализация: потолок строка 0, дно 15, уступы = камень в строках 13..14 (верх уступа = верх строки 13), вода = Pit в тех же строках.
    // Вход 1..34 (три канала по 4 м), плоты 35..69, метановая галерея 70..125, P-4: вторые плоты 126..181 (4 канала) и тёмный тоннель 182..235
    // с метановым провалом 206..221 (вставка D3 = 110), развилка (126..159)+D3: внизу вода (тупик), наверху платформа,
    // на неё поднимает тёплый поток; платформа входит в шахту (зона 4, x >= 160+D3). Чекпоинты (факелы): 119 и 188; остальное топливо: мешки.
    static void Zone3Sewer(int x0, int y0)
    {
        void B(int a, int b, int c, int d, char ch) => Box(x0 + a, y0 + b, x0 + c, y0 + d, ch);
        void S(int a, int b, char ch) => g[y0 + b, x0 + a] = ch;
        void Room(string n, int a, int b, int c, int d, string hex) => rooms.Add((n, x0 + a, y0 + b, x0 + c, y0 + d, hex));
        void Ledge(int a, int c) => B(a, 13, c, 14, '#');
        void Water(int a, int c) => B(a, 13, c, 14, 'P');
        B(0, 0, 159 + D3, 0, '#'); B(0, 15, 159 + D3, 15, '#'); B(0, 0, 0, 15, '#');
        Room("Сток: вход", 1, 1, 34, 14, "#10161a"); Room("Сток: плоты", 35, 1, 69, 14, "#0e1a1c");
        Room("Сток: галерея", 70, 1, 125, 14, "#14181a"); Room("Сток: плоты 2", 126, 1, 181, 14, "#0e1a1c");
        Room("Сток: тоннель", 182, 1, 235, 14, "#0a1012"); Room("Сток: развилка", 126 + D3, 1, 159 + D3, 14, "#12161c");
        zoneStarts.Add(C(x0 + 3, y0 + 12) + Vector3.up * 0.1f);
        float top = FloorTop(y0 + 12); // верх уступов и вода по этому уровню

        // 1. Вход: уступы над потоком
        Ledge(1, 9); Water(10, 13); Ledge(14, 15); Water(16, 19); Ledge(20, 21); Water(22, 25); Ledge(26, 34);
        S(4, 12, 'g'); S(15, 12, 'g'); S(30, 12, 'g');
        // 2. Плоты: каналы с плотами и острова между ними
        Water(35, 45); Ledge(46, 48); Water(49, 57); Ledge(58, 60); Water(61, 69);
        S(47, 12, 'g'); S(59, 12, 'g');
        extras.Add(l =>
        {
            BuildRaftStream(l, "RaftStream1", x0 + 35, x0 + 46, top, 1, 0f); BuildRaftStream(l, "RaftStream2", x0 + 49, x0 + 58, top, 1, 0.4f); BuildRaftStream(l, "RaftStream3", x0 + 61, x0 + 70, top, 1, 0.7f);
        });
        // 3. Метановая галерея: широкий канал, пузыри как трамплины
        Ledge(70, 81); Water(82, 107); Ledge(108, 128);
        S(74, 12, 'g'); S(78, 12, 'g'); S(119, 12, 'f'); S(122, 12, 'g');
        extras.Add(l => { for (int i = 0; i < 9; i++) BuildMethaneVent(l, x0 + 85.5f + 2.5f * i, top, i * 0.7f); });
        // 3b. Вторые плоты: четыре канала, те же щели 1.5 м до плота
        Water(129, 139); Ledge(140, 142); Water(143, 152); Ledge(153, 155); Water(156, 164); Ledge(165, 167); Water(168, 178); Ledge(179, 181);
        S(127, 12, 'g'); S(141, 12, 'g'); S(154, 12, 'g'); S(166, 12, 'g'); S(180, 12, 'g');
        extras.Add(l =>
        {
            BuildRaftStream(l, "RaftStream4", x0 + 129, x0 + 140, top, -1, 0.1f); BuildRaftStream(l, "RaftStream5", x0 + 143, x0 + 153, top, -1, 0.55f);
            BuildRaftStream(l, "RaftStream6", x0 + 156, x0 + 165, top, -1, 0.3f); BuildRaftStream(l, "RaftStream7", x0 + 168, x0 + 179, top, -1, 0.8f);
        });
        // 3c. Тёмный тоннель: один факел-чекпоинт в начале, метановый провал в середине
        Ledge(182, 205); Water(206, 221); Ledge(222, 235);
        S(188, 12, 'f'); S(196, 12, 'g'); S(202, 12, 'g'); S(225, 12, 'g'); S(228, 12, 'g'); S(233, 12, 'g');
        extras.Add(l => { for (int i = 0; i < 5; i++) BuildMethaneVent(l, x0 + 209.5f + 2.5f * i, top, i * 0.7f); });
        // 4. Развилка: внизу вода (не туда), наверху платформа, к ней тёплый поток
        Ledge(126 + D3, 143 + D3); Water(144 + D3, 159 + D3); B(144 + D3, 6, 159 + D3, 7, '#');
        S(130 + D3, 12, 'g'); S(136 + D3, 12, 'g');
        extras.Add(l =>
        {
            var u = new GameObject("Updraft"); u.transform.SetParent(l, false);
            float yTop = H - (y0 + 2); // верх строки 2
            u.transform.position = new Vector3(x0 + D3 + 142.5f, (top + yTop) / 2, 0); u.transform.localScale = new Vector3(3, yTop - top, 4);
            u.AddComponent<Updraft>();
        });
    }

    // Зона 4. Шахта: строки 30..71. Внизу вода (под входной платформой), вход слева строки 32..33 (продолжение платформы зоны 3).
    // Шахта 1 (x 1..12): поток D1 (7..9) к уступу A (1..5, строка 20), поток D2 (2..7) через вентилятор (строка 13) к уступу B (8..12, строка 6),
    // вправо тоннелем (13..24, строки 3..5) с решёткой (16), затем падение по колодцу (22..24) на строку 32.
    // Шахта 2 (P-4): та же шахта со сдвигом +25 (x 26..37): вторая решётка поперёк потока D1 (строка 26: пройти фитильком, держа прыжок),
    // свой ремень (id fanbelt2), тоннель 38..45 с решёткой (41) к ZoneExit в зону 5. Чекпоинты (свечи): уступ B шахты 1 и старт шахты 2.
    static void Zone4Shaft(int x0, int y0)
    {
        void B(int a, int b, int c, int d, char ch) => Box(x0 + a, y0 + b, x0 + c, y0 + d, ch);
        void S(int a, int b, char ch) => g[y0 + b, x0 + a] = ch;
        void Room(string n, int a, int b, int c, int d, string hex) => rooms.Add((n, x0 + a, y0 + b, x0 + c, y0 + d, hex));
        const int X2 = 25;
        B(0, 0, 46, 2, '#'); B(0, 0, 0, 25, '#'); B(13, 6, 21, 41, '#'); B(0, 41, 12, 41, '#');
        B(0, 39, 12, 40, 'P');
        B(X2, 0, X2, 25, '#'); B(22, 32, 24, 41, '#'); B(X2, 41, X2 + 12, 41, '#'); B(X2 + 13, 6, X2 + 20, 41, '#'); B(X2 + 21, 0, X2 + 21, 41, '#');
        B(X2, 39, X2 + 12, 40, 'P');
        Room("Шахта", 1, 3, 12, 38, "#1a1d22"); Room("Шахта: тоннель", 13, 3, 24, 5, "#1a1d22"); Room("Шахта: колодец", 22, 6, 24, 31, "#14171c");
        Room("Шахта 2", X2 + 1, 3, X2 + 12, 38, "#1a1d22"); Room("Шахта 2: тоннель", X2 + 13, 3, X2 + 20, 5, "#1a1d22");
        var start = C(x0 + 3, y0 + 31) + Vector3.up * 0.1f; zoneStarts.Add(start);
        extras.Add(l =>
        {
            var cp = new GameObject("ZoneStart4"); cp.transform.SetParent(l, false);
            cp.transform.position = C(x0 + 3, y0 + 29); cp.transform.localScale = new Vector3(5, 4, 4);
            cp.AddComponent<Checkpoint>().point = start;
        });
        void Draft(int a, int c, int rTop, int rBottom) // тайлы a..c по x, от строки rTop сверху до верха уступа под rBottom
        {
            extras.Add(l =>
            {
                float yTop = H - (y0 + rTop), yBot = FloorTop(y0 + rBottom);
                var u = new GameObject("Updraft"); u.transform.SetParent(l, false);
                u.transform.position = new Vector3(x0 + (a + c + 1) / 2f, (yTop + yBot) / 2, 0); u.transform.localScale = new Vector3(c - a + 1, yTop - yBot, 4);
                u.AddComponent<Updraft>();
            });
        }
        foreach (var o in new[] { 0, X2 })
        {
            bool second = o != 0;
            // 1. Тёплые потоки с уступами
            B(o, 32, o + 9, 33, '#'); S(o + 2, 31, second ? 'c' : 'p'); S(o + 5, 31, 'g'); Draft(o + 7, o + 9, 17, 31);
            B(o + 1, 20, o + 5, 21, '#'); S(o + 2, 19, 'p'); S(o + 4, 19, 'p'); B(o + 3, 14, o + 3, 18, second ? 'J' : 'j');
            // 2. Вентилятор: поток вдоль левой стены, лопасти на строке 13
            Draft(o + 2, o + 7, 3, 19);
            float fx = x0 + o + 7f; string belt = second ? "fanbelt2" : "fanbelt";
            extras.Add(l => BuildShaftFan(l, fx, H - 1 - (y0 + 13) + 0.5f, belt));
            B(o + 8, 6, o + 12, 7, '#'); S(o + 10, 5, second ? 'p' : 'c');
        }
        B(X2 + 1, 26, X2 + 12, 26, 'v'); // 1b. решётка поперёк шахты 2: только фитильком, держа прыжок в потоке
        S(X2 + 2, 25, 'p');
        // 3. Решётки в тоннелях: фитилёк
        S(14, 5, 'p'); B(16, 3, 16, 4, '#'); S(16, 5, 'v');
        S(X2 + 14, 5, 'p'); B(X2 + 16, 3, X2 + 16, 4, '#'); S(X2 + 16, 5, 'v');
        extras.Add(l => BuildExit(l, x0 + X2 + 19, y0 + 5, 5));
    }

    // Зона 5. Лес: строки 74..93 (локально 0..19). Потолок строка 0, основание 19. Земля = плиты от строки r до 19, игрок стоит в строке r-1.
    // Опушка 1..65 (пни = укрытия от ветра fan x=52, кроны без снега), ручей 66..80 (тонкий лёд над водой), бурелом 81..151 (брёвна, овраг 120..127 с мостом-стволом),
    // P-4 (вставка D5 = 190): ручей 2 154..174 (тонкий лёд), заснеженное поле с ветром 175..236 (лёд 212..222 под порывами), бурелом 2 237..341 (овраг 262..268),
    // подъём (152..199)+D5: террасы по 1 м через 4 м, без подпитки. Холод ×1.5 на всю зону, снег по всей длине.
    // Смола-чекпоинты: x 130 и CL+2 (начало подъёма); остальное топливо: бумага.
    static void Zone5Forest(int x0, int y0)
    {
        const int CL = 152 + D5; // начало подъёма
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
        void Ravine(int a, int c, int ropeX, string id) // овраг a..c: верёвка слева роняет ствол-мост
        {
            B(a, 17, c, 18, '.'); B(a, 18, c, 18, 'P');
            extras.Add(l =>
            {
                for (int r = 10; r <= 14; r++) { var rp = Cube("Rope", l, C(x0 + ropeX, y0 + r), new Vector3(0.2f, 1, 0.2f), mRope, false).AddComponent<Rope>(); rp.trigger = true; rp.id = id; }
                var br = new GameObject("LogBridge_" + id); br.transform.SetParent(l, false); var bc = br.AddComponent<Bridge>(); bc.id = id; bc.kind = Bridge.Kind.Log;
                Cube("LogPlank", br.transform, new Vector3(x0 + (a + c + 1) / 2f, H - (y0 + 17) - 0.5f, 0), new Vector3(c - a + 1, 1, Depth), mTrunk);
            });
        }
        B(0, 0, 199 + D5, 0, '#'); B(0, 19, 199 + D5, 19, '#'); B(0, 0, 0, 19, '#');
        rooms.Add(("Лес", x0 + 1, y0 + 1, x0 + 198 + D5, y0 + 18, "#10201c"));
        zoneStarts.Add(C(x0 + 3, y0 + 16) + Vector3.up * 0.1f);

        // 1. Опушка
        Ground(1, 65, 17);
        foreach (var x in new[] { 12, 19, 26, 33, 41, 47 }) Tree(x, 17, 8);
        S(16, 16, 'p'); S(24, 16, 'p'); S(30, 16, 'p'); S(38, 16, 'p'); S(58, 16, 'p');
        extras.Add(l => BuildGale(l, x0 + 52.5f, H - (y0 + 17) + 4.5f, 0.1f, 45f));

        // 2. Ручей: тонкий лёд над водой-Pit, два просвета для прыжка. Идти маленьким
        Ground(81, 151, 17);
        B(66, 17, 80, 17, 'i'); B(66, 18, 80, 18, 'P'); B(71, 17, 72, 17, '.'); B(76, 17, 76, 17, '.');
        S(86, 16, 'p');

        // 3. Бурелом: брёвна (1 м), кроны; овраг 120..127: верёвка 118 роняет ствол-мост (id log)
        foreach (var (a, b) in new[] { (90, 91), (94, 96), (104, 105), (109, 110), (133, 135), (139, 140), (145, 146) }) B(a, 16, b, 16, 'n');
        foreach (var x in new[] { 100, 108, 137, 143 }) Tree(x, 17, 8);
        S(102, 16, 'p'); S(106, 16, 'p'); S(112, 16, 'p'); S(141, 16, 'p');
        S(130, 16, 'r');
        B(115, 8, 121, 9, 'o');
        Ravine(120, 127, 118, "log");

        // 3b. Ручей 2: тонкий лёд подлиннее, три просвета
        Ground(152, 153, 17); S(148, 16, 'p'); // бумага только до ручья за 6 м и после него: перед тонким льдом большим не стать
        B(154, 17, 174, 17, 'i'); B(154, 18, 174, 18, 'P'); B(158, 17, 159, 17, '.'); B(164, 17, 164, 17, '.'); B(169, 17, 170, 17, '.');
        // 3c. Поле с ветром: пни-укрытия через 8 м, лёд 212..222 под порывами, ветер справа
        Ground(175, 236, 17);
        foreach (var x in new[] { 184, 192, 200, 208, 224, 230 }) B(x, 16, x, 16, 'n');
        foreach (var x in new[] { 188, 196, 204, 228 }) Tree(x, 17, 8);
        B(212, 17, 222, 17, 'i'); B(212, 18, 222, 18, 'P'); B(217, 17, 217, 17, '.');
        foreach (var x in new[] { 178, 182, 190, 198, 202, 226, 232 }) S(x, 16, 'p');
        extras.Add(l => BuildGale(l, x0 + 234.5f, H - (y0 + 17) + 4.5f, 0.1f, 55f));
        // 3d. Бурелом 2: длиннее, овраг 262..268 с верёвкой 260 (id log2)
        Ground(237, 261, 17); Ground(269, CL - 1, 17);
        foreach (var (a, b) in new[] { (240, 241), (244, 246), (252, 253), (257, 258), (272, 274), (278, 279), (283, 285), (290, 291), (298, 300), (306, 307), (312, 314), (322, 323), (330, 332) }) B(a, 16, b, 16, 'n');
        foreach (var x in new[] { 248, 256, 276, 288, 294, 308, 318, 326, 336 }) Tree(x, 17, 8);
        foreach (var x in new[] { 242, 250, 254, 259, 275, 281, 293, 296, 303, 310, 316, 320, 328, 334, 338 }) S(x, 16, 'p');
        Ravine(262, 268, 260, "log2");

        // 4. Подъём: 11 террас, ни свечи, ни бумаги. Ветер только на верхней площадке
        for (int k = 0; k <= 10; k++) Ground(CL + 4 * k, CL + 3 + 4 * k, 17 - k);
        Ground(CL + 44, CL + 47, 7);
        S(CL + 2, 16, 'r');
        foreach (var k in new[] { 1, 3, 5 }) Tree(CL + 2 + 4 * k, 17 - k, 3);
        foreach (var k in new[] { 7, 9 }) B(CL + 2 + 4 * k, 17 - k - 1, CL + 2 + 4 * k, 17 - k - 1, 'n');
        extras.Add(l => BuildGale(l, x0 + CL + 46.5f, H - (y0 + 7) + 4.5f, 0.1f, 40f));

        // снег по всей зоне (цена 0.03, под кронами нет), холод, общий план в начале, далёкий круг камней
        extras.Add(l =>
        {
            for (int x = 6; x <= 198 + D5; x += 8)
            {
                var sn = new GameObject("Snowfall"); sn.transform.SetParent(l, false); sn.transform.position = C(x0 + x, y0 + 1);
                var sp = sn.AddComponent<DripSpawner>(); sp.dropPrefab = dropPrefab; sp.phase = x % 5 * 0.07f;
                sp.interval = 0.5f; sp.cost = 0.03f; sp.halfWidth = 4f; sp.gravity = 3f; sp.maxSpeed = 2.2f; sp.scale = 0.5f; sp.activeRange = 30f;
            }
            var cz = new GameObject("ColdZone"); cz.transform.SetParent(l, false);
            cz.transform.position = new Vector3(x0 + (200 + D5) / 2f, H - (y0 + 10), 0); cz.transform.localScale = new Vector3(200 + D5, 20, 6); cz.AddComponent<ColdZone>().mult = 1.5f;
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
        if (m && m.GetTexture("_BaseMap")) go.GetComponent<MeshFilter>().sharedMesh = BoxMesh(scale, center);
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    // P-13: меши с UV в метрах (BoxUV) — подассеты одного файла на сцену. Периоды текстур делят 8 м,
    // поэтому центр куба по модулю 8 даёт тот же рисунок: одинаковые кубы делят меш.
    static readonly System.Collections.Generic.Dictionary<string, Mesh> boxMeshes = new();
    static string BoxMeshPath => art ? "Assets/Meshes/BoxesArt.asset" : "Assets/Meshes/Boxes.asset";
    static void BeginBoxMeshes()
    {
        boxMeshes.Clear();
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Meshes"));
        AssetDatabase.DeleteAsset(BoxMeshPath);
        AssetDatabase.CreateAsset(new Mesh { name = "Boxes" }, BoxMeshPath);
    }
    static void EndBoxMeshes() { AssetDatabase.SaveAssets(); Debug.Log($"Ogonok: {boxMeshes.Count} box meshes"); }
    static Mesh BoxMesh(Vector3 size, Vector3 center)
    {
        static float M8(float v) => Mathf.Round(((v % 8f) + 8f) % 8f * 100f) / 100f;
        var o = new Vector3(M8(center.x), M8(center.y), M8(center.z));
        var key = $"{size.x:0.###}_{size.y:0.###}_{size.z:0.###}_{o.x}_{o.y}_{o.z}";
        if (boxMeshes.TryGetValue(key, out var mesh)) return mesh;
        mesh = BoxUV.Make(size, o); mesh.name = key;
        AssetDatabase.AddObjectToAsset(mesh, BoxMeshPath);
        return boxMeshes[key] = mesh;
    }

    // Материал '#' по зоне клетки (полосы как в ZoneRects): дом — доски, подвал — кирпич, сток и шахта — кладка, лес и поляна — камень.
    static (string, Material) StoneOf(int tx, int ty) =>
        ty <= 13 ? ("Floor", mHouse) : ty <= 29 ? ("Brick", mCellar) : ty <= 73 ? ("Stone", mSewer) : ("Rock", mRock);

    static void BuildTiles(Transform level)
    {
        var br = new GameObject("Bridge"); br.transform.SetParent(level, false); var wb = br.AddComponent<Bridge>(); wb.id = "wardrobe"; wb.kind = Bridge.Kind.Wardrobe;
        var hatch = new GameObject("Hatch"); hatch.transform.SetParent(level, false); hatch.AddComponent<Hatch>().id = "hatch";
        for (int ty = 0; ty < H; ty++)
            for (int tx = 0; tx < W; tx++)
            {
                char c = g[ty, tx];
                if (c == '#' || c == 'm' || c == 'n' || c == 'o')
                {
                    var (sn, sm) = c == '#' ? StoneOf(tx, ty) : c == 'm' ? ("Furniture", mFurn) : c == 'n' ? ("Trunk", mTrunk) : ("Crown", mCrown);
                    int e = tx; while (e + 1 < W && g[ty, e + 1] == c && !(art && e + 1 == 30) && (c != '#' || StoneOf(e + 1, ty).Item2 == sm)) e++; // art: у гостиной (x < 30) свои невидимые кубы, режем ряд на границе
                    int len = e - tx + 1;
                    Cube(sn, level, new Vector3(tx + len / 2f, H - 1 - ty + 0.5f, 0), new Vector3(len, 1, Depth), sm);
                    tx = e; continue;
                }
                var p = C(tx, ty);
                switch (c)
                {
                    case 'I': Cube("Ice", level, p, new Vector3(1, 1, 2), mIce).AddComponent<Ice>(); break;
                    case 'i': var ic = Cube("Ice", level, p, new Vector3(1, 1, 2), mIce).AddComponent<Ice>(); ic.thin = ic.refreeze = true; break;
                    case 'H': var hi = Cube("IceHot", level, p, new Vector3(1, 1, 2), mIce).AddComponent<Ice>(); hi.heatOnly = true; heatIce.Add(hi); break;
                    case 'R': Cube("Curtain", level, p, new Vector3(0.8f, 1, 2), mCloth).AddComponent<Rope>(); break;
                    case 'T': case 'L': case 'j': case 'U': case 'V': case 'J':
                        var rp = Cube("Rope", level, p, new Vector3(0.2f, 1, 0.2f), mRope, false).AddComponent<Rope>(); rp.trigger = true; rp.id = c == 'T' ? "wardrobe" : c == 'L' ? "hatch" : c == 'U' ? "shelf1" : c == 'V' ? "shelf2" : c == 'J' ? "fanbelt2" : "fanbelt"; break;
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
                    case 'G': BuildExit(level, tx, ty, 3); break;
                    case 'f': BuildCandle(level, tx, ty, false, true); break;
                    case 'g': var bag = Cube("Bag", level, p + Vector3.down * 0.1f, new Vector3(0.5f, 0.4f, 0.3f), mBag, false); bag.AddComponent<Paper>(); break;
                    case 'r': BuildResin(level, tx, ty); break;
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
                        pit.AddComponent<Pit>().water = ty >= 56; // как вода ниже: сток, шахта, лес
                        if (ty >= 56) Cube("Water", level, pit.transform.position + Vector3.down * 0.05f, new Vector3(ep - tx + 1, 0.9f, 2.5f), mWater, false); // вода канализации и шахты; провал спальни без воды
                        break;
                }
            }
        if (!art) foreach (var (name, a, b, c2, d, hex) in rooms) // с артом тёмный фон комнат не нужен: за уровнем видны задники
        {
            ColorUtility.TryParseHtmlString(hex, out var col);
            Cube("Room_" + name, level, new Vector3((a + c2 + 1) / 2f, H - (b + d + 1) / 2f, Depth / 2 + 0.5f), new Vector3(c2 - a + 1, d - b + 1, 1), Lit("Room" + rooms.FindIndex(r => r.name == name), Color.Lerp(col, Color.white, 0.35f), b <= 13 ? "T04" : b <= 29 ? "T05" : b <= 73 ? "T03" : null, b <= 13 ? 4 : 2), false);
        }
    }

    static void BuildCandle(Transform level, int tx, int ty, bool lamp, bool torch = false)
    {
        if (torch) lamp = true; // газовый факел = керосинка другого цвета (только параметры и материал)
        var root = new GameObject(torch ? "Torch" : lamp ? "Lamp" : "Candle"); root.transform.SetParent(level, false);
        root.transform.position = new Vector3(tx + 0.5f, FloorTop(ty), 0);
        var wax = GameObject.CreatePrimitive(PrimitiveType.Cylinder); wax.name = "Wax"; Object.DestroyImmediate(wax.GetComponent<Collider>());
        wax.transform.SetParent(root.transform, false); wax.transform.localPosition = Vector3.up * (lamp ? 0.3f : 0.2f);
        wax.transform.localScale = lamp ? new Vector3(0.36f, 0.3f, 0.36f) : new Vector3(0.22f, 0.2f, 0.22f);
        wax.GetComponent<Renderer>().sharedMaterial = torch ? mPipe : lamp ? mLamp : mWax;
        var fl = Sprite("Flame", root.transform, flameSprite, mSparkAdd, lamp ? 0.8f : 0.55f); fl.transform.localPosition = Vector3.up * (lamp ? 0.85f : 0.62f);
        var lt = new GameObject("Light").AddComponent<Light>(); lt.transform.SetParent(root.transform, false); lt.transform.localPosition = Vector3.up * (lamp ? 0.95f : 0.7f);
        lt.type = LightType.Point; lt.color = torch ? new Color(.55f, .75f, 1f) : lamp ? new Color(1, .5f, .18f) : new Color(1, .62f, .28f); lt.range = lamp ? 10 : 7; lt.intensity = lamp ? 7 : 5; lt.shadows = LightShadows.None;
        if (torch) fl.GetComponent<SpriteRenderer>().color = new Color(.6f, .8f, 1f);
        var cd = root.AddComponent<Candle>(); cd.lightSrc = lt; cd.flameVis = fl;
    }

    // Исток канала течения (P-5): труба у истока, решётка стока, RaftStream спавнит плоты. dir +1: течение вправо.
    static void BuildRaftStream(Transform level, string n, float xa, float xb, float top, int dir, float phase)
    {
        var root = new GameObject(n); root.transform.SetParent(level, false); root.transform.position = new Vector3((xa + xb) / 2, top, 0);
        var s = root.AddComponent<RaftStream>(); s.xa = xa; s.xb = xb; s.top = top; s.dir = dir; s.phase = phase; s.raftMat = mRaft;
        float up = dir > 0 ? xa : xb, down = dir > 0 ? xb : xa;
        Cube("StreamPipe", root.transform, new Vector3(up + dir * 0.5f, top + 0.5f, 1.9f), new Vector3(1.1f, 0.9f, 0.5f), mPipe, false);
        Cube("StreamDrain", root.transform, new Vector3(down - dir * 0.5f, top - 0.3f, 1.9f), new Vector3(1.2f, 0.7f, 0.4f), mGrate, false);
    }

    static void BuildMethaneVent(Transform level, float x, float top, float phase)
    {
        var v = new GameObject("MethaneVent"); v.transform.SetParent(level, false); v.transform.position = new Vector3(x, top, 0);
        var mv = v.AddComponent<MethaneVent>(); mv.bubblePrefab = bubblePrefab; mv.phase = phase;
    }

    // Вентилятор: втулка и две лопасти (визуал двигает ShaftFan.LateUpdate). Ремень = верёвка 'j' в сетке.
    static void BuildShaftFan(Transform level, float x, float y, string belt = "fanbelt")
    {
        var root = new GameObject("ShaftFan"); root.transform.SetParent(level, false); root.transform.position = new Vector3(x, y, 0);
        var fan = root.AddComponent<ShaftFan>(); fan.beltId = belt;
        Cube("Hub", root.transform, root.transform.position, new Vector3(0.8f, 0.8f, 0.8f), mBlade, false);
        fan.blades = new[] { Cube("Blade0", root.transform, root.transform.position, Vector3.one, mBlade, false).transform, Cube("Blade1", root.transform, root.transform.position, Vector3.one, mBlade, false).transform };
    }

    // Выход из зоны: невидимый объём над полом в тайле (tx, ty).
    static void BuildExit(Transform level, int tx, int ty, int target)
    {
        var go = new GameObject("ZoneExit" + target); go.transform.SetParent(level, false);
        go.transform.position = C(tx, ty) + Vector3.up; go.transform.localScale = new Vector3(3, 3, 3);
        go.AddComponent<ZoneExit>().target = target;
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
        Cube("BoilerBody", root.transform, root.transform.position + new Vector3(0, 2.5f, 1.2f), new Vector3(5, 6, 0.8f), mPipe, false); // P-13: котёл железный
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
        for (int i = 0; i < cols.Length; i++) { ColorUtility.TryParseHtmlString(cols[i], out var c); mats[i] = Lit("Deco" + i, Color.Lerp(c, Color.white, 0.4f), "T02"); }
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
        Range(3, 96, FloorTop(12), 6); Range(99, 186, FloorTop(12), 6); Range(101 + D1, 137 + D1, FloorTop(8), 4);   // дом: этаж, новые комнаты, спальня
        Range(126 + D1, 150 + D1, FloorTop(28), 8); Range(156 + D1, 186 + D1, FloorTop(28), 8); Range(190 + D1, 296 + D1, FloorTop(28), 8); // подвал: спуск, пол, погреб и котельная
    }

    // ---------- игрок, камера, пост-обработка ----------
    static FlamePlayer BuildPlayer(Vector3 start)
    {
        var root = new GameObject("Player"); root.transform.position = start;
        var f = root.AddComponent<Flame>(); var fp = root.AddComponent<FlamePlayer>(); fp.flame = f;
        root.AddComponent<AudioListener>();
        var vis = art ? Sprite("FlameSprite", root.transform, wispSprite, mWisp, 1f) : Sprite("FlameSprite", root.transform, flameSprite, mSparkAdd, 1f); f.visual = vis.transform;
        if (art) vis.AddComponent<FaceFlip>().player = fp;
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
        var cam = go.AddComponent<Camera>(); // AudioListener на игроке (BuildPlayer): камера на z -13 глушила бы мировые звуки
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

    // ---------- превью арта команды (P-4): только просмотр, арт не финальный ----------
    // Файлы скопированы из корневой Assets/ в Unity/Assets/Art/Preview (корневую папку не трогаем).
    // platform_tileset.jpg не нарезаем: JPEG без альфы, под 2.5D-кубы не ложится. ParallaxLayer.cs не нужен: камера перспективная, параллакс даёт глубина z.
    static Material mBgFar, mBgMid, mBgGears, mBgChains;

    static Texture2D ImportPreview(string file, bool sprite)
    {
        var path = "Assets/Art/Preview/" + file;
        if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), path))) throw new FileNotFoundException("нет файла превью арта: " + path);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.mipmapEnabled = false; imp.wrapMode = TextureWrapMode.Clamp; imp.maxTextureSize = 1024; imp.alphaIsTransparency = true;
        if (sprite) { imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single; imp.spritePixelsPerUnit = 1024; }
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material TexMat(string name, string shader, Texture2D tex, Color tint)
    {
        var path = $"Assets/Materials/{name}.mat"; AssetDatabase.DeleteAsset(path);
        var m = new Material(Shader.Find(shader)) { mainTexture = tex, color = tint };
        AssetDatabase.CreateAsset(m, path); return m;
    }

    static void LoadArtPreview()
    {
        ImportPreview("ignis_wisp.png", true);
        wispSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Preview/ignis_wisp.png");
        mWisp = TexMat("Wisp", "Sprites/Default", wispSprite.texture, Color.white); // у PNG есть альфа: обычный блендинг, а не аддитивный (тёмное тело иначе исчезло бы)
        // тёмные тинты: задники не перебивают свет огонька
        mBgFar = TexMat("BgFar", "Sprites/Default", ImportPreview("bg_far_cathedral.jpg", false), new Color(0.30f, 0.30f, 0.36f, 1));
        mBgMid = TexMat("BgMid", "Mobile/Particles/Additive", ImportPreview("bg_midground_gears.jpg", false), new Color(0.30f, 0.26f, 0.24f, 1)); // JPEG без альфы: чёрное = прозрачное
        mBgGears = TexMat("BgGears", "Sprites/Default", ImportPreview("bg_layer_gears.png", false), new Color(0.40f, 0.38f, 0.42f, 1));
        mBgChains = TexMat("BgChains", "Sprites/Default", ImportPreview("bg_layer_chains.png", false), new Color(0.45f, 0.45f, 0.50f, 1));
    }

    // Квады за уровнем на разной глубине z (камера FOV 40, z=-13): чем дальше, тем крупнее квад и медленнее параллакс. Соседние плитки зеркалятся: шов не виден.
    static void BuildArtBackdrop()
    {
        var root = new GameObject("ArtPreview").transform;
        void Layer(string n, Material m, float z, float w)
        {
            float h = w * 768f / 1376f; int col = 0;
            for (float x = -w; x < W + w; x += w, col++)
            {
                int row = 0;
                for (float y = -h; y < H + h; y += h, row++)
                {
                    var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(q.GetComponent<Collider>());
                    q.name = n; q.transform.SetParent(root, false); q.transform.position = new Vector3(x + w / 2, y + h / 2, z);
                    q.transform.localScale = new Vector3(col % 2 == 0 ? w : -w, row % 2 == 0 ? h : -h, 1);
                    q.GetComponent<Renderer>().sharedMaterial = m; q.GetComponent<Renderer>().sortingOrder = -200; // P-12: слои комнат (-100..-60) рисуются поверх задников
                }
            }
        }
        Layer("BgFar", mBgFar, 75f, 100f);
        Layer("BgMid", mBgMid, 45f, 64f);
        Layer("BgGears", mBgGears, 24f, 36f);
        Layer("BgChains", mBgChains, 12f, 24f);
    }

    // ---------- плейсхолдерные ассеты ----------
    // tex: T01..T16 из Art/Textures/Greybox (P-13), period: метров на один повтор рисунка (делит 8, см. BoxMesh).
    static Material Lit(string name, Color c, string tex = null, float period = 2f)
    {
        var path = $"Assets/Materials/{name}.mat";
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", 0.12f);
        if (tex != null) { m.SetTexture("_BaseMap", GreyTex(tex)); m.SetTextureScale("_BaseMap", Vector2.one / period); }
        // P-13: существующий .mat перезаписываем на месте — GUID тот же, сборка второй сцены не рвёт ссылки первой
        var old = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!old) { AssetDatabase.CreateAsset(m, path); return m; }
        old.shader = m.shader; old.CopyPropertiesFromMaterial(m); old.shaderKeywords = m.shaderKeywords;
        Object.DestroyImmediate(m); EditorUtility.SetDirty(old); return old;
    }

    // Импорт текстуры грейбокса по INTEGRATION.md: Default, sRGB, Repeat, mipmaps, Trilinear.
    static Texture2D GreyTex(string id)
    {
        var path = Directory.GetFiles(Path.Combine(Application.dataPath, "Art/Textures/Greybox"), id + "-*.png")[0];
        path = "Assets" + path.Substring(Application.dataPath.Length).Replace('\\', '/');
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        if (imp.textureType != TextureImporterType.Default || imp.wrapMode != TextureWrapMode.Repeat || !imp.sRGBTexture || !imp.mipmapEnabled
            || imp.filterMode != FilterMode.Trilinear || imp.anisoLevel != 4 || imp.maxTextureSize != 1024)
        {
            imp.textureType = TextureImporterType.Default; imp.sRGBTexture = true; imp.wrapMode = TextureWrapMode.Repeat; imp.mipmapEnabled = true;
            imp.filterMode = FilterMode.Trilinear; imp.anisoLevel = 4; imp.maxTextureSize = 1024; imp.isReadable = false;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
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
        mHouse = Lit("HouseFloor", new Color(0.85f, 0.8f, 0.75f), "T01"); mCellar = Lit("CellarBrick", new Color(0.75f, 0.72f, 0.72f), "T05");
        mSewer = Lit("SewerStone", new Color(0.7f, 0.74f, 0.8f), "T03"); mRock = Lit("ForestRock", new Color(0.75f, 0.78f, 0.85f), "T16", 4);
        mWall = Lit("Wall", new Color(0.09f, 0.1f, 0.15f));
        mIce = Lit("Ice", new Color(0.8f, 0.92f, 1f), "T09", 1); mIce.SetFloat("_Smoothness", 0.8f);
        mRope = Lit("Rope", new Color(1f, 0.95f, 0.9f), "T11", 1); mCloth = Lit("Cloth", Color.white, "T07", 1);
        mWax = Lit("Wax", new Color(0.92f, 0.89f, 0.8f));
        mPaper = Lit("Paper", Color.white, "T15", 1);
        mWater = Lit("Water", new Color(0.27f, 0.5f, 0.8f)); mWater.SetFloat("_Smoothness", 0.9f);
        mGrate = Lit("Grate", new Color(0.6f, 0.62f, 0.7f), "T06", 1);
        mWood = Lit("Wood", new Color(0.95f, 0.9f, 0.85f), "T02");
        mFire = Lit("Fireplace", new Color(0.9f, 0.85f, 0.82f), "T05");
        mDrop = Lit("Drop", new Color(0.44f, 0.69f, 0.91f));
        mFurn = Lit("Furniture", new Color(0.95f, 0.88f, 0.8f), "T02");
        mWindow = Lit("Window", new Color(0.35f, 0.45f, 0.62f)); mCurtain = Lit("WindowCurtain", Color.white, "T07", 1);
        mRaft = Lit("Raft", Color.white, "T01"); mBlade = Lit("Blade", new Color(0.7f, 0.72f, 0.78f), "T06", 1); mBag = Lit("Bag", new Color(0.12f, 0.16f, 0.12f));
        mBubble = Shaded("Bubble", "Mobile/Particles/Additive", dotSprite, new Color(0.4f, 1f, 0.55f, 1));
        mTrunk = Lit("Trunk", new Color(0.9f, 0.9f, 0.9f), "T08"); mCrown = Lit("Crown", new Color(0.8f, 0.9f, 0.85f), "T13"); mSnowCap = Lit("SnowCap", new Color(0.9f, 0.94f, 1f), "T10");
        mResin = Lit("Resin", new Color(0.95f, 0.6f, 0.15f)); mRune = Lit("Rune", new Color(0.6f, 0.62f, 0.7f), "T16");
        mFar = new Material(Shader.Find("Universal Render Pipeline/Unlit")); mFar.color = new Color(0.16f, 0.18f, 0.24f); AssetDatabase.DeleteAsset("Assets/Materials/Far.mat"); AssetDatabase.CreateAsset(mFar, "Assets/Materials/Far.mat");
        mWeb = Lit("Cobweb", new Color(0.85f, 0.85f, 0.88f)); mLamp = Lit("Lamp", new Color(0.55f, 0.38f, 0.18f)); mPipe = Lit("Pipe", new Color(0.6f, 0.62f, 0.7f), "T06", 1);

        var drop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(drop.GetComponent<Collider>());
        drop.transform.localScale = new Vector3(0.12f, 0.18f, 0.12f); drop.GetComponent<Renderer>().sharedMaterial = mDrop;
        drop.AddComponent<Drop>();
        dropPrefab = PrefabUtility.SaveAsPrefabAsset(drop, "Assets/Prefabs/Drop.prefab");
        Object.DestroyImmediate(drop);

        if (art) LoadArtPreview();
        var bub = Sprite("Bubble", null, dotSprite, mBubble, 0.8f); bub.AddComponent<Methane>();
        bubblePrefab = PrefabUtility.SaveAsPrefabAsset(bub, "Assets/Prefabs/Bubble.prefab");
        Object.DestroyImmediate(bub);
    }
}
