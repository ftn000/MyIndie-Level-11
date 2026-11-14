using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

// Секции уровня гоняются ботом без клавиатуры: FlamePlayer.MoveInput + фиксированный dt.
// Объекты ищутся в сцене по типу/имени, а не по жёстким координатам: раскладка зон меняется, тесты остаются.
public class LevelTests
{
    const float Dt = 1f / 60f;
    FlamePlayer p;
    GameManager gm;

    [UnitySetUp]
    public IEnumerator Load()
    {
        Time.captureFramerate = 60;
        yield return SceneManager.LoadSceneAsync("Level1");
        yield return null;
        p = Object.FindFirstObjectByType<FlamePlayer>();
        gm = GameManager.I; gm.state = GameManager.State.Play; p.manual = true;
    }

    [TearDown] public void Reset() => p.giveHeld = false;

    const float HouseY = 81.6f; // y пола гостиной/коридора: сетка H=94, строка 12 зоны 1
    static T[] All<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsSortMode.None);
    static Fan Window(string n) => GameObject.Find(n).GetComponent<Fan>();
    // капли в котельной (подвал-спуск и слив капают всегда, их не считаем)
    static int DropsAround(Boiler b) => All<Drop>().Count(d => d.transform.position.x > b.transform.position.x - 15f && d.transform.position.x < b.transform.position.x + 8f);
    static Pit HousePit() => All<Pit>().Single(t => t.transform.position.y > 80f); // провал спальни (единственный в доме)
    static Rope RopeId(string id) => All<Rope>().Where(r => r.trigger && r.id == id).OrderBy(r => r.transform.position.y).First();
    // Зоны делят сетку по y: дом 80..93, подвал 64..79, шахта 22..63, канализация 22..37, лес и поляна 0..19.
    static bool InHouse(Component c) => c.transform.position.y > 80f;
    static bool InForest(Component c) => c.transform.position.y < 21f;

    void Tp(float x, float y, float size) { p.transform.position = new Vector3(x, y, 0); p.flame.size = size; Physics.SyncTransforms(); }

    // input(frame) -> (dir, held, jumpPressed, dash, down)
    IEnumerator Run(int frames, Func<int, (int dir, bool held, bool jump, bool dash, bool down)> input, Func<bool> until = null)
    {
        for (int i = 0; i < frames; i++)
        {
            var c = input(i);
            p.MoveInput(Dt, c.dir, c.held, c.jump, c.dash, c.down);
            if (p.flame.Dead || (until != null && until())) yield break;
            yield return null;
        }
    }

    static readonly Func<int, (int, bool, bool, bool, bool)> Right = _ => (1, false, false, false, false);
    static readonly Func<int, (int, bool, bool, bool, bool)> Idle = _ => (0, false, false, false, false);

    // Ставит фазу цикла окна так, чтобы сейчас было ph секунд (порыв: 2.9..4.5).
    static void SetPhase(Fan f, float ph) => f.offset = ((ph - Time.time) % 4.6f + 4.6f) % 4.6f;

    // ---------- старые механики, переехавшие в зоны ----------

    [UnityTest] public IEnumerator IceMeltsAndOpensPath()
    {
        var ice = All<Ice>().Where(i => !i.heatOnly && InHouse(i)).OrderBy(i => i.transform.position.y).First().transform.position; // нижняя льдинка проёма на кухне
        Tp(ice.x - 2f, ice.y + 0.1f, 1f);
        yield return Run(400, Right, () => p.transform.position.x > ice.x + 0.8f);
        Assert.Greater(p.transform.position.x, ice.x + 0.8f, "прошёл сквозь растаявший лёд");
    }

    [UnityTest] public IEnumerator VentNeedsWickMode()
    {
        var v = All<Vent>().Single(v => v.transform.position.y > 60f && v.transform.position.y < 75f).transform.position; // решётка слива подвала
        Tp(v.x - 2.5f, v.y, 1f);
        yield return Run(60, Right);
        Assert.Less(p.transform.position.x, v.x - 0.5f, "на большом пламени решётка не пускает");
        yield return Run(500, _ => (1, false, false, false, true), () => p.transform.position.x > v.x + 1f);
        Assert.Greater(p.transform.position.x, v.x + 1f, "фитилёк прошёл решётку");
    }

    [UnityTest] public IEnumerator RopeDropsWardrobeBridge()
    {
        Assert.IsNull(GameObject.Find("Plank"), "до верёвки мост скрыт");
        var r = RopeId("wardrobe").transform.position;
        Tp(r.x, r.y + 1f, 1f);
        yield return Run(120, Idle);
        Assert.IsNotNull(GameObject.Find("Plank"), "верёвка сгорела, шкаф лёг мостом");
        var pit = HousePit().transform;
        float left = pit.position.x - pit.localScale.x / 2, right = pit.position.x + pit.localScale.x / 2;
        Tp(left - 1.5f, r.y - 1.9f, 1f); // на полу спальни перед провалом
        yield return Run(600, Right, () => p.transform.position.x > right + 1.5f);
        Assert.Greater(p.transform.position.x, right + 1.5f, "перешёл провал по мосту");
        Assert.IsFalse(p.flame.Dead);
    }

    [UnityTest] public IEnumerator PitKillsWithoutBridge()
    {
        var pit = HousePit().transform;
        float left = pit.position.x - pit.localScale.x / 2;
        Tp(left - 1.5f, pit.position.y + 4.1f, 1f);
        yield return Run(300, Right);
        Assert.IsTrue(p.flame.Dead, "в провал упал и погас");
    }

    // Тёплые потоки живут в зоне 4: проверяем механику на фикстуре с геометрией старой пропасти, смещённой вправо за уровень.
    [UnityTest] public IEnumerator PitCrossedWithUpdrafts()
    {
        const float X = 500f; // за пределами уровня (W=442)
        void Slab(float a, float b, float y0, float y1) { var c = GameObject.CreatePrimitive(PrimitiveType.Cube); c.transform.position = new Vector3(X + (a + b) / 2, (y0 + y1) / 2, 0); c.transform.localScale = new Vector3(b - a, y1 - y0, 4); }
        void Draft(float cx, float w) { var u = new GameObject("Updraft"); u.transform.position = new Vector3(X + cx, 8, 0); u.transform.localScale = new Vector3(w, 16, 4); u.AddComponent<Updraft>(); }
        Slab(80, 93, 0, 1); Slab(111, 125, 0, 1); Slab(80, 125, 16, 17); Slab(98, 100, 4, 5); Slab(106, 108, 5, 6); Slab(109, 111, 2, 3);
        Draft(95.5f, 3); Draft(102.5f, 3);
        Physics.SyncTransforms();
        Tp(X + 91.5f, 1.6f, 1f);
        yield return Run(2400, _ =>
        {
            float x = p.transform.position.x - X, y = p.transform.position.y;
            bool climb = p.inUpdraft && y < (x > 100.5f ? 13f : 8.9f);
            bool right = !(climb && (x > 95f && x < 98f || x > 102f)) && !(x > 106.2f && x < 107.8f && !p.Grounded);
            return (right ? 1 : 0, climb || p.Grounded, false, false, false);
        }, () => p.transform.position.x - X > 111.5f && p.Grounded);
        Assert.Greater(p.transform.position.x - X, 111.5f, "пересёк пропасть");
        Assert.IsFalse(p.flame.Dead);
    }

    [UnityTest] public IEnumerator FuelDrainsFasterWhenBig()
    {
        Tp(40.5f, HouseY, 1f); float big = p.flame.DrainRate;
        p.flame.size = 0.1f; float small = p.flame.DrainRate;
        Assert.Greater(big / small, 2.5f);
        yield return null;
    }

    [UnityTest] public IEnumerator WindParticlesAppearInGust()
    {
        var fan = Window("WindowFanB"); var fx = fan.transform.position;
        Tp(fx.x - 4f, HouseY, 1f); SetPhase(fan, 2.9f);
        int maxAlive = 0;
        for (int i = 0; i < 480; i++)
        {
            p.MoveInput(Dt, 0, false, false, false, false);
            p.flame.size = 1f; // долгий замер частиц не должен упираться в смерть игрока
            int alive = 0;
            foreach (var ps in fan.GetComponentsInChildren<ParticleSystem>()) alive += ps.particleCount;
            maxAlive = Mathf.Max(maxAlive, alive);
            if (i == 60) // середина порыва: снимок кадра, если задан OGONOK_OUT
            {
                var path = Environment.GetEnvironmentVariable("OGONOK_OUT");
                if (!string.IsNullOrEmpty(path)) Capture(path);
            }
            yield return null;
        }
        Assert.Greater(maxAlive, 20, "частицы ветра летят в порыв");
    }

    // ---------- 2a. сквозняк из окна ----------

    [UnityTest] public IEnumerator WindowGustKillsInOpenButNotInCover()
    {
        var fan = Window("WindowFanB"); float fx = fan.transform.position.x;
        // открытое место: между последним укрытием (правый край x=62) и окном
        Tp(fx - 3f, HouseY, 0.8f); SetPhase(fan, 3.0f);
        p.MoveInput(Dt, 0, false, false, false, false);
        Assert.Less(p.windX, 0f, "в порыв на открытом месте дует");
        yield return Run(120, Idle);
        Assert.IsTrue(p.flame.Dead || p.flame.size < 0.5f, "порыв гасит");
        // укрытие: мебель между игроком и окном
        Tp(fx - 9f, HouseY, 0.8f); p.flame.Revive(0.8f); SetPhase(fan, 3.0f);
        float s0 = p.flame.size;
        yield return Run(60, Idle);
        Assert.AreEqual(0f, p.windX, "за мебелью не дует");
        Assert.IsFalse(p.flame.Dead);
        Assert.Greater(p.flame.size, s0 - 0.08f, "в укрытии тратится только обычный расход");
    }

    [UnityTest] public IEnumerator WindowGustPushOnlyAtFirstWindow()
    {
        var fan = Window("WindowFanA"); float fx = fan.transform.position.x;
        Tp(fx - 2.5f, HouseY, 0.8f); SetPhase(fan, 3.0f); // правее мебели, левее окна
        p.MoveInput(Dt, 0, false, false, false, false);
        Assert.Less(p.windX, 0f, "первое окно толкает");
        float s0 = p.flame.size;
        yield return Run(30, Idle);
        Assert.IsFalse(p.flame.Dead);
        Assert.Greater(p.flame.size, s0 - 0.06f, "но не гасит");
    }

    // Регрессия: канализация лежит под домом с теми же x, ветер окон туда не достаёт.
    [UnityTest] public IEnumerator HouseWindowDoesNotBlowInSewer()
    {
        var fan = Window("WindowFanB"); float fx = fan.transform.position.x;
        Tp(fx - 3f, 25.5f, 0.8f); SetPhase(fan, 3.0f);
        p.MoveInput(Dt, 0, false, false, false, false);
        Assert.AreEqual(0f, p.windX, "в порыв этажом ниже не дует");
        yield return null;
    }

    [UnityTest] public IEnumerator CurtainBillowsBeforeGust()
    {
        var fan = Window("WindowFanB"); var cur = fan.curtain;
        Tp(fan.transform.position.x - 9f, HouseY, 1f);
        SetPhase(fan, 0.5f); p.MoveInput(Dt, 0, false, false, false, false);
        float calm = cur.localScale.x;
        SetPhase(fan, 2.6f); p.MoveInput(Dt, 0, false, false, false, false);
        Assert.IsFalse(fan.Gusting, "порыва ещё нет");
        Assert.Greater(cur.localScale.x, calm * 3f, "занавеска вздулась за секунду до порыва");
        yield return null;
    }

    // ---------- 2b. паутина ----------

    [UnityTest] public IEnumerator CobwebBurnsAndCostsFlame()
    {
        var web = All<Cobweb>().Where(InHouse).OrderByDescending(c => c.transform.position.x).First(); var wp = web.transform.position; // паутина спальни: крайняя справа
        Tp(wp.x - 1.5f, wp.y - 1.9f, 1f); float s0 = p.flame.size;
        yield return Run(200, Right, () => p.transform.position.x > wp.x + 1f);
        Assert.Greater(p.transform.position.x, wp.x + 1f, "проход открыт");
        Assert.IsTrue(web == null, "паутина сгорела");
        float lost = s0 - p.flame.size;
        Assert.GreaterOrEqual(lost, 0.05f, "забрала ~0.05");
        Assert.Less(lost, 0.05f + 0.06f * 200 * Dt, "и только это (плюс обычный расход)");
    }

    // ---------- 2c. люк в подвал ----------

    [UnityTest] public IEnumerator HatchOpensOnLatchRopeAndClosesBehind()
    {
        var hatch = All<Hatch>().Single(); var r = RopeId("hatch").transform.position;
        Assert.IsFalse(hatch.Open);
        Tp(r.x + 0.3f, r.y - 0.9f, 1f); // стоит на люке, верёвка над головой
        yield return Run(200, Idle, () => hatch.Open);
        Assert.IsTrue(hatch.Open, "щеколда сгорела, люк открылся");
        yield return Run(400, Idle, () => p.Grounded && p.transform.position.y < gm.zoneStart[1].y + 2f);
        yield return null;
        Assert.Less(p.transform.position.y, gm.zoneStart[1].y + 2f, "упал в подвал");
        Assert.IsFalse(p.flame.Dead);
        Assert.IsFalse(hatch.Open, "люк захлопнулся");
        Assert.IsTrue(hatch.transform.GetChild(0).gameObject.activeSelf, "плиты на месте: назад нельзя");
        Assert.Less(Vector3.Distance(gm.Checkpoint, gm.zoneStart[1]), 0.1f, "чекпоинт зоны 2");
    }

    // ---------- 2d. котёл ----------

    [UnityTest] public IEnumerator BoilerRefusesWhenTooWeak()
    {
        var b = All<Boiler>().Single(); var give = b.GetComponent<GiveFlame>();
        Tp(b.transform.position.x - 1f, b.transform.position.y, 0.25f);
        p.giveHeld = true;
        yield return Run(150, Idle);
        Assert.IsFalse(give.Done, "слабому не даёт отдать");
        Assert.IsFalse(p.flame.Dead, "и не убивает");
        Assert.IsFalse(b.Lit);
    }

    [UnityTest] public IEnumerator BoilerThawsDrainAndStopsDrips()
    {
        var b = All<Boiler>().Single(); var give = b.GetComponent<GiveFlame>();
        Assert.Greater(b.ice.Length, 0); Assert.Greater(b.drips.Length, 0);
        Assert.IsTrue(b.ice.All(i => i), "до котла слив закрыт льдом");
        // большое пламя этот лёд не берёт
        var ice = b.ice[0].transform.position;
        Tp(ice.x - 1.5f, ice.y, 1f);
        yield return Run(120, Right);
        Assert.IsTrue(b.ice.All(i => i), "лёд слива тает только от котла");
        // капли до котла падают
        var d0 = b.drips[0].transform.position;
        Tp(d0.x - 12f, b.transform.position.y, 1f);
        int maxDrops = 0;
        for (int i = 0; i < 300; i++) { p.flame.size = 1f; p.MoveInput(Dt, 0, false, false, false, false); maxDrops = Mathf.Max(maxDrops, DropsAround(b)); yield return null; }
        Assert.Greater(maxDrops, 0, "до котла капли падают");
        // отдаём пламя
        Tp(b.transform.position.x - 1f, b.transform.position.y, 0.8f);
        p.giveHeld = true;
        yield return Run(120, Idle, () => give.Done);
        p.giveHeld = false;
        Assert.IsTrue(give.Done, "удержание E отдало пламя");
        Assert.AreEqual(0.5f, p.flame.size, 0.08f, "отдал 0.3");
        yield return null;
        Assert.IsTrue(b.Lit);
        Assert.IsTrue(b.ice.All(i => !i), "лёд на сливе растаял");
        Assert.IsTrue(b.drips.All(d => !d.enabled), "капли выключены");
        yield return Run(240, Idle); // долетают уже упавшие
        int late = 0;
        for (int i = 0; i < 300; i++) { p.flame.size = 1f; p.MoveInput(Dt, 0, false, false, false, false); late = Mathf.Max(late, DropsAround(b)); yield return null; }
        Assert.AreEqual(0, late, "капли больше не падают");
    }

    // ---------- 2e. керосинка = свеча с другим видом ----------

    [UnityTest] public IEnumerator LampIsCandleCheckpoint()
    {
        var lamp = All<Candle>().Single(c => c.name == "Lamp"); var lp = lamp.transform.position;
        Assert.IsFalse(lamp.lit);
        Tp(lp.x, lp.y + 0.6f, 0.4f);
        yield return Run(10, Idle);
        Assert.IsTrue(lamp.lit, "керосинка зажглась");
        Assert.Less(Vector3.Distance(gm.Checkpoint, lp), 1f, "и стала чекпоинтом");
        Assert.Greater(p.flame.size, 0.8f, "подпитка как у свечи");
    }

    // ---------- каркас зон ----------

    [UnityTest] public IEnumerator ZoneStartsTeleportAndSetCheckpoint()
    {
        Assert.AreEqual(6, gm.zoneStart.Length, "шесть зон");
        Assert.IsTrue(gm.JumpToZone(2));
        yield return null;
        Assert.Less(Vector3.Distance(p.transform.position, gm.zoneStart[1]), 0.1f);
        Assert.Less(Vector3.Distance(gm.Checkpoint, gm.zoneStart[1]), 0.1f);
        Assert.IsTrue(gm.JumpToZone(5));
        Assert.Less(p.transform.position.y, 20f, "зона 5 внизу сетки");
        Assert.IsFalse(gm.JumpToZone(7));
        Assert.IsTrue(gm.JumpToZone(1));
        Assert.Less(Vector3.Distance(p.transform.position, gm.zoneStart[0]), 0.1f);
    }

    // Проходимость по топливу: между подзарядками (старт зоны, свечи, бумага, мешки) хватает пламени при реальной средней скорости 3 м/с
    // (с запасом на прыжки и ожидание) и среднем размере 0.5 (расход 0.012+0.04·s). Свеча +0.5, бумага/мешок +0.3, старт 0.8.
    // Подзарядки в пути идут по порядку key; шахта (зона 4) считается как два подъёма и падение по колодцу.
    [UnityTest] public IEnumerator FuelLastsBetweenRefuels()
    {
        const float AvgSpeed = 3f, Avg = 0.5f, Floor = 0.1f;
        float rate = 0.012f + 0.04f * Avg;
        Vector3 Exit(int target) => All<ZoneExit>().Single(e => e.target == target).transform.position;
        IEnumerable<(Vector3 pos, float gain)> Items(Func<Vector3, bool> inZone) =>
            All<Candle>().Select(c => (pos: c.transform.position, gain: 0.5f)).Concat(All<Paper>().Select(c => (pos: c.transform.position, gain: 0.3f))).Where(t => inZone(t.pos));
        void Check(string zone, Vector3 start, Vector3 end, IEnumerable<(Vector3 pos, float gain)> items, Func<Vector3, float> key)
        {
            var stops = new[] { (pos: start, gain: 0f) }.Concat(items.OrderBy(t => key(t.pos))).Append((pos: end, gain: 0f)).ToArray();
            float fuel = 0.8f;
            for (int i = 1; i < stops.Length; i++)
            {
                fuel -= (Mathf.Abs(stops[i].pos.x - stops[i - 1].pos.x) + Mathf.Abs(stops[i].pos.y - stops[i - 1].pos.y)) / AvgSpeed * rate; // путь по Манхэттену
                Assert.Greater(fuel, Floor, $"{zone}: топлива не хватит до ({stops[i].pos.x:0}, {stops[i].pos.y:0})");
                if (i < stops.Length - 1) fuel = Mathf.Min(1f, fuel + stops[i].gain);
            }
        }
        float x4 = gm.zoneStart[3].x - 3.5f;
        Check("зона 1", gm.zoneStart[0], All<Hatch>().Single().transform.position, Items(v => v.y > 80f), v => v.x); // зона 1 кончается люком
        Check("зона 2", gm.zoneStart[1], Exit(3), Items(v => v.y > 60f && v.y < 75f), v => v.x);
        Check("зона 3", gm.zoneStart[2], gm.zoneStart[3], Items(v => v.y > 20f && v.y < 30f), v => v.x);
        var shaft = Items(v => v.y > 30f && v.y < 65f).ToArray();
        Check("зона 4, шахта 1", gm.zoneStart[3], new Vector3(x4 + 23.5f, gm.zoneStart[3].y, 0), shaft.Where(t => t.pos.x < x4 + 22), v => v.y); // конец: низ колодца
        Check("зона 4, шахта 2", new Vector3(x4 + 23.5f, gm.zoneStart[3].y, 0), Exit(5), shaft.Where(t => t.pos.x > x4 + 22), v => v.y);
        yield return null;
    }

    // ---------- P-2: выходы между зонами ----------

    [UnityTest] public IEnumerator ZoneExitTeleportsToNextZone()
    {
        var ex = All<ZoneExit>().Single(e => e.target == 3);
        Tp(ex.transform.position.x, ex.transform.position.y, 0.8f);
        yield return Run(60, Idle, () => Vector3.Distance(p.transform.position, gm.zoneStart[2]) < 0.5f);
        yield return null;
        Assert.Less(Vector3.Distance(p.transform.position, gm.zoneStart[2]), 0.5f, "оказался на старте зоны 3");
        Assert.Less(Vector3.Distance(gm.Checkpoint, gm.zoneStart[2]), 0.1f, "чекпоинт обновлён");
        Assert.AreEqual(GameManager.State.Play, gm.state);
    }

    [UnityTest] public IEnumerator ShaftExitLeadsToForest()
    {
        var ex = All<ZoneExit>().Single(e => e.target == 5);
        Tp(ex.transform.position.x, ex.transform.position.y, 0.5f);
        yield return Run(60, Idle, () => Vector3.Distance(p.transform.position, gm.zoneStart[4]) < 0.5f);
        Assert.Less(Vector3.Distance(p.transform.position, gm.zoneStart[4]), 0.5f, "из шахты попал на старт леса");
        Assert.AreEqual(GameManager.State.Play, gm.state);
    }

    // ---------- P-2: плоты ----------

    [UnityTest] public IEnumerator RaftCarriesPlayer()
    {
        var raft = All<Raft>().OrderBy(r => r.transform.position.x).Skip(1).First(); // средний плот
        Tp(raft.transform.position.x, raft.Top + 0.6f, 0.8f);
        float rx0 = raft.transform.position.x;
        yield return Run(40, Idle);
        float moved = raft.transform.position.x - rx0;
        Assert.Greater(Mathf.Abs(moved), 0.5f, "плот плывёт");
        Assert.IsFalse(p.flame.Dead, $"не упал в воду: p={p.transform.position} raft={raft.transform.position} top={raft.Top} grounded={p.Grounded} size={p.flame.size}");
        Assert.Less(Mathf.Abs(p.transform.position.x - raft.transform.position.x), 0.3f, "уехал вместе с плотом");
        Assert.AreEqual(raft.Top + p.H, p.transform.position.y, 0.1f, "стоит на плоту");
    }

    [UnityTest] public IEnumerator SewerWaterGapsAreJumpable()
    {
        // Любой канал воды без плота и метана уже максимального прыжка (4 тайла при размере 0.4, воздух ~4.7 м). Тупик развилки (x >= 140) не в счёт.
        var rafts = All<Raft>().Select(r => r.transform.position.x).ToArray();
        var vents = All<MethaneVent>().Select(v => v.transform.position.x).ToArray();
        foreach (var pit in All<Pit>().Where(t => t.transform.position.y > 21f && t.transform.position.y < 30f && t.transform.position.x < 250f))
        {
            float l = pit.transform.position.x - pit.transform.localScale.x / 2, r = pit.transform.position.x + pit.transform.localScale.x / 2;
            bool helped = rafts.Concat(vents).Any(x => x > l && x < r) || rafts.Any(x => x > l - 4 && x < r + 4);
            Assert.IsTrue(helped || pit.transform.localScale.x <= 4.01f, $"канал воды x {l:0}..{r:0} шире прыжка и без плота/метана");
        }
        yield return null;
    }

    // ---------- P-2: метан ----------

    Methane Bubble(Vector3 at) { var b = new GameObject("Bubble").AddComponent<Methane>(); b.transform.position = at; return b; }

    [UnityTest] public IEnumerator MethaneSmallFlameLiftsUp()
    {
        Tp(100f, 40f, 0.4f); Bubble(new Vector3(100f, 40f, 0)); float s0 = p.flame.size;
        p.MoveInput(Dt, 0, false, false, false, false);
        Assert.Greater(p.Velocity.y, 11f, "толчок вверх");
        Assert.AreEqual(0f, p.Velocity.x, 0.01f, "малый огонёк не откидывает вбок");
        Assert.Greater(p.flame.size, s0 + 0.15f, "подпитка +0.2");
        yield return null;
    }

    [UnityTest] public IEnumerator MethaneBigFlameKnocksSideways()
    {
        Tp(100f, 40f, 0.9f); Bubble(new Vector3(99.8f, 40f, 0));
        p.MoveInput(Dt, 0, false, false, false, false);
        Assert.Greater(p.Velocity.y, 15f, "сильнее вверх");
        Assert.Greater(Mathf.Abs(p.Velocity.x), 5f, "и откидывает вбок");
        yield return null;
    }

    [UnityTest] public IEnumerator MethaneVentSpawnsBubbles()
    {
        var v = All<MethaneVent>().First();
        Tp(v.transform.position.x, v.transform.position.y + 8f, 1f); // рядом, но не в воде
        yield return null;
        int seen = 0;
        for (int i = 0; i < 400 && seen == 0; i++) { p.flame.size = 1f; seen = All<Methane>().Length; yield return null; }
        Assert.Greater(seen, 0, "пузыри всплывают по таймеру");
    }

    // ---------- P-2: вентилятор шахты ----------

    [UnityTest] public IEnumerator ShaftFanKillsUntilBeltBurns()
    {
        var fan = All<ShaftFan>().OrderBy(f => f.transform.position.x).First(); var c = fan.transform.position;
        fan.angle = 0f; // лопасть вдоль x
        Tp(c.x + 3f, c.y, 1f); Physics.SyncTransforms();
        p.MoveInput(Dt, 0, false, false, false, false);
        Assert.IsTrue(p.flame.Dead, "лопасть гасит");
        p.flame.Revive(0.8f);
        // между лопастями: ребром (угол 90°) у стены свободно
        fan.angle = Mathf.PI / 2; Tp(c.x - 5f, c.y, 1f);
        p.MoveInput(Dt, 0, false, false, false, false);
        Assert.IsFalse(p.flame.Dead, "лопасть ребром: у стены проход свободен");
        // ремень: сгорел, лопасти встали
        var belt = RopeId("fanbelt").transform.position;
        Tp(belt.x, belt.y, 1f); fan.angle = 0.3f;
        yield return Run(120, Idle, () => fan.Stopped);
        Assert.IsTrue(fan.Stopped, "ремень сгорел");
        yield return Run(60, Idle);
        float a0 = fan.angle; yield return Run(60, Idle);
        Assert.AreEqual(a0, fan.angle, 1e-4f, "лопасти стоят");
        Tp(c.x - 5f, c.y, 1f); p.flame.Revive(0.8f);
        yield return Run(60, Idle);
        Assert.IsFalse(p.flame.Dead, "стоящий вентилятор не гасит у стены");
    }

    // ---------- P-2: боты проходят секции зон 3–4 ----------

    static Func<int, (int, bool, bool, bool, bool)> Inp(int dir, bool held, bool jump = false, bool down = false) => _ => (dir, held, jump, false, down);

    [UnityTest] public IEnumerator BotJumpsSewerEntryGaps()
    {
        var gaps = All<Pit>().Where(t => t.transform.position.y < 30f && t.transform.position.x < 30f && Mathf.Abs(t.transform.position.y - 24.5f) < 0.1f)
            .OrderBy(t => t.transform.position.x).ToArray(); // верхняя строка воды (y 24.5..25.5), три канала входа
        Assert.AreEqual(3, gaps.Length);
        foreach (var pit in gaps)
        {
            float l = pit.transform.position.x - pit.transform.localScale.x / 2, r = pit.transform.position.x + pit.transform.localScale.x / 2;
            p.flame.Revive(0.8f); Tp(l - 0.4f, 25.4f, 0.8f); yield return null;
            yield return Run(150, i => (1, true, i == 0, false, false), () => p.transform.position.x > r + 0.3f && p.Grounded);
            Assert.IsFalse(p.flame.Dead, $"упал в воду x {l:0}..{r:0}");
            Assert.Greater(p.transform.position.x, r, $"перепрыгнул канал x {l:0}..{r:0}");
        }
    }

    [UnityTest] public IEnumerator BotCrossesRaftChannels()
    {
        var chans = All<Pit>().Where(t => Mathf.Abs(t.transform.position.y - 24.5f) < 0.1f && All<Raft>().Any(r => Mathf.Abs(r.transform.position.x - t.transform.position.x) < t.transform.localScale.x / 2))
            .OrderBy(t => t.transform.position.x).ToArray();
        Assert.AreEqual(7, chans.Length, "семь каналов с плотами");
        foreach (var pit in chans)
        {
            float l = pit.transform.position.x - pit.transform.localScale.x / 2, r = pit.transform.position.x + pit.transform.localScale.x / 2;
            var raft = All<Raft>().Single(x => x.transform.position.x > l && x.transform.position.x < r);
            var rt = new System.Text.StringBuilder(); int rf = 0; float prevRx = raft.transform.position.x;
            p.flame.Revive(0.8f); Tp(l - 0.4f, 25.4f, 0.8f); yield return null;
            yield return Run(1500, _ =>
            {
                var pos = p.transform.position; float rx = raft.transform.position.x; if (rf++ % 10 == 0) rt.Append($"[{rf} x={pos.x:0.0} y={pos.y:0.0} rx={rx:0.0} g={p.Grounded}] ");
                float drx = rx - prevRx; prevRx = rx;
                bool onRaft = p.Grounded && Mathf.Abs(pos.x - rx) < 1.9f && Mathf.Abs(pos.y - p.H - raft.Top) < 0.2f;
                if (!p.Grounded) return (1, true, false, false, false); // в полёте держим вправо
                bool go = onRaft ? r - (rx + 1.5f) < 2.6f && drx > 0 : pos.x < l + 0.2f && rx - 1.5f - l < 2.6f && drx < 0; // плот плывёт навстречу // прыжок, когда плот подплыл на 2.6 м (щель 1.5..2.5 м)
                return (go ? 1 : 0, true, go, false, false);
            }, () => p.transform.position.x > r + 0.3f && p.Grounded);
            Assert.IsFalse(p.flame.Dead, $"упал в воду канала x {l:0}..{r:0} " + rt);
            Assert.Greater(p.transform.position.x, r, $"переплыл канал x {l:0}..{r:0}");
        }
    }

    [UnityTest] public IEnumerator BotCrossesMethaneGallery()
    {
        // два метановых канала: галерея (26 м) и провал тёмного тоннеля (16 м); плотов в них нет
        var vents = All<MethaneVent>().Select(v => v.transform.position.x).ToArray();
        var pits = All<Pit>().Where(t => Mathf.Abs(t.transform.position.y - 24.5f) < 0.1f && t.transform.position.x < 250f)
            .Where(t => vents.Any(v => Mathf.Abs(v - t.transform.position.x) < t.transform.localScale.x / 2)).OrderBy(t => t.transform.position.x).ToArray();
        Assert.AreEqual(2, pits.Length, "два метановых канала");
        foreach (var pit in pits)
        {
            float l = pit.transform.position.x - pit.transform.localScale.x / 2, r = pit.transform.position.x + pit.transform.localScale.x / 2;
            var trace = new System.Text.StringBuilder(); int fr = 0;
            p.flame.Revive(0.5f); Tp(l - 0.5f, 25.4f, 0.5f);
            yield return Run(240, Idle); p.flame.size = 0.5f; // пузыри уже всплывают (жерла стартуют вместе с игрой)
            yield return null;
            yield return Run(2400, _ =>
            {
                var pos = p.transform.position;
                var b = All<Methane>().Where(m => m.transform.position.x > pos.x - 0.6f && m.transform.position.x < pos.x + 6f && Mathf.Abs(m.transform.position.y - pos.y) < 4f)
                    .OrderBy(m => Mathf.Abs(m.transform.position.x - pos.x)).FirstOrDefault();
                int dir = b == null ? 1 : Mathf.Abs(b.transform.position.x - pos.x) < 0.25f ? 0 : b.transform.position.x > pos.x ? 1 : -1;
                if (fr++ % 10 == 0) trace.Append($"[{fr} x={pos.x:0.0} y={pos.y:0.0} s={p.flame.size:0.00} vy={p.Velocity.y:0} b={(b == null ? "-" : b.transform.position.x.ToString("0.0") + "," + b.transform.position.y.ToString("0.0"))}] ");
                return (dir, true, p.Grounded, false, false);
            }, () => p.transform.position.x > r + 0.3f && p.Grounded);
            Assert.IsFalse(p.flame.Dead, $"упал в воду канала x {l:0}..{r:0} " + trace);
            Assert.Greater(p.transform.position.x, r, $"перелетел канал x {l:0}..{r:0} по пузырям");
        }
    }

    [UnityTest] public IEnumerator BotRidesForkUpdraftToPlatform()
    {
        var u = All<Updraft>().Single(d => d.transform.position.y < 40f && d.transform.position.x < 260f); var c = u.transform.position;
        Tp(c.x, 25.4f, 0.8f); yield return null;
        yield return Run(400, _ => (p.transform.position.y > 33.5f ? 1 : 0, p.transform.position.y < 34.5f, false, false, false), () => p.transform.position.x > c.x + 3f && p.Grounded);
        Assert.IsFalse(p.flame.Dead);
        Assert.Greater(p.transform.position.x, c.x + 1.5f, "вышел на платформу над развилкой");
        Assert.Greater(p.transform.position.y, 30f, "платформа выше дна");
    }

    // Подъём по шахте со сдвигом o (0 = шахта 1, 25 = шахта 2): потоки, ремень, вентилятор, уступ B. С grate: сначала фитилёк под решёткой на строке 26.
    float ShaftX0() => gm.zoneStart[3].x - 3.5f;
    IEnumerator ClimbShaft(float o, ShaftFan fan, bool grate)
    {
        float x0 = ShaftX0() + o, cx = fan.transform.position.x;
        // 1. поток D1 от входной платформы к уступу A (слева); с решёткой держим ↓ и прыжок, пока не пройдём её
        var start = p.transform.position;
        yield return Run(700, _ => { var q = p.transform.position; bool wick = grate && q.y < 39.5f;
            return (q.y > 45.6f ? -1 : q.x < x0 + 8.3f ? 1 : 0, q.y < 46f, false, false, wick); }, () => p.transform.position.y > 43f && p.Grounded);
        Assert.IsFalse(p.flame.Dead, "D1");
        Assert.Greater(p.transform.position.y, start.y + 10f, "поднялся на уступ A");
        Assert.Less(p.transform.position.x, cx - 0.5f);
        // 2. ремень сгорает от касания, лопасти встают
        var tr2 = new System.Text.StringBuilder(); int f2 = 0;
        yield return Run(300, _ => { bool fed = grate && p.flame.size > 0.2f; if (f2++ % 10 == 0) tr2.Append($"[{f2} x={p.transform.position.x:0.0} y={p.transform.position.y:0.0} s={p.flame.size:0.00} g={p.Grounded}] "); float px = p.transform.position.x, tx = fed ? x0 + 3.5f : x0 + 4.5f; int dir = !grate ? 0 : Mathf.Abs(tx - px) < 0.15f ? 0 : tx > px ? 1 : -1; return (dir, fed, fed, false, false); }, () => fan.Stopped); // после решётки огонёк мал: сперва бумага (иначе size 0.12 ремень не зажжёт), потом прыжки к ремню
        Assert.IsTrue(fan.Stopped, $"ремень над уступом A достаётся с уступа: p={p.transform.position} s={p.flame.size} belts={All<Rope>().Count(r => r.id == fan.beltId)} papers={All<Paper>().Count(pp => Vector3.Distance(pp.transform.position, p.transform.position) < 8f)} " + tr2);
        // 3. поток D2 мимо стоящего вентилятора на уступ B
        var tr3 = new System.Text.StringBuilder(); int f3 = 0;
        yield return Run(600, _ => { var q = p.transform.position; if (f3++ % 15 == 0) tr3.Append($"[{f3} x={q.x:0.0} y={q.y:0.0} s={p.flame.size:0.00} up={p.inUpdraft}] "); return (q.y < 52f ? (q.x < x0 + 3.5f ? 1 : 0) : q.y > 59.8f ? 1 : (q.x < x0 + 6.6f ? 1 : 0), q.y < 60.6f, false, false, false); }, () => p.transform.position.y > 57f && p.Grounded && p.transform.position.x > cx + 1f);
        Assert.IsFalse(p.flame.Dead, "D2/вентилятор");
        Assert.Greater(p.transform.position.x, cx + 1f, "на уступе B " + tr3);
    }

    [UnityTest] public IEnumerator BotClimbsShaftBurningBeltThroughGrate()
    {
        var fan = All<ShaftFan>().OrderBy(f => f.transform.position.x).First();
        gm.JumpToZone(4); yield return null;
        yield return ClimbShaft(0, fan, false);
        // 4. тоннель с решёткой фитильком, колодец: падаем на строку 32
        var tr4 = new System.Text.StringBuilder(); int f4 = 0; float x0 = ShaftX0(); float floorY = gm.zoneStart[3].y;
        yield return Run(900, _ => { var q = p.transform.position; if (f4++ % 8 == 0) tr4.Append($"[{f4} x={q.x:0.0} y={q.y:0.00} s={p.flame.size:0.00} g={p.Grounded}] "); return (1, false, false, false, true); }, () => p.transform.position.x > x0 + 21.5f && p.transform.position.y < floorY + 1f && p.Grounded);
        Assert.IsFalse(p.flame.Dead, "тоннель и колодец " + tr4);
        Assert.Less(p.transform.position.y, floorY + 1f, "упал по колодцу на вход шахты 2 " + tr4);
    }

    [UnityTest] public IEnumerator BotClimbsShaft2ThroughGrateInDraftToForest()
    {
        var fan = All<ShaftFan>().OrderBy(f => f.transform.position.x).Last();
        Assert.AreEqual("fanbelt2", fan.beltId);
        gm.JumpToZone(4); yield return null;
        Tp(ShaftX0() + 25 + 3.5f, gm.zoneStart[3].y, 0.8f); yield return null;
        yield return ClimbShaft(25, fan, true);
        var tr4 = new System.Text.StringBuilder(); int f4 = 0;
        yield return Run(900, _ => { var q = p.transform.position; if (f4++ % 8 == 0) tr4.Append($"[{f4} x={q.x:0.0} y={q.y:0.00} s={p.flame.size:0.00} g={p.Grounded}] "); return (1, false, false, false, true); }, () => Vector3.Distance(p.transform.position, gm.zoneStart[4]) < 0.5f);
        Assert.Less(Vector3.Distance(p.transform.position, gm.zoneStart[4]), 0.5f, $"прошёл решётку и ZoneExit в лес: p={p.transform.position} s={p.flame.size} dead={p.flame.Dead} " + tr4);
    }

    [UnityTest] public IEnumerator Shaft2GrateBlocksBigFlameInDraft()
    {
        gm.JumpToZone(4); yield return null;
        float x0 = ShaftX0() + 25;
        Tp(x0 + 8.3f, gm.zoneStart[3].y, 0.8f);
        yield return Run(120, _ => (0, true, false, false, false)); // большой поднимается и упирается в решётку
        Assert.Less(p.transform.position.y, 37f, "решётка поперёк потока держит большое пламя");
        Assert.IsFalse(p.flame.Dead);
    }

    [UnityTest] public IEnumerator FanHasTimingWindowAlongWall()
    {
        var fan = All<ShaftFan>().OrderBy(f => f.transform.position.x).First(); var c = fan.transform.position;
        int free = 0; const int N = 360;
        for (int i = 0; i < N; i++) { fan.angle = i * Mathf.PI * 2 / N; if (!fan.Hits(new Vector3(c.x - 4.5f, c.y, 0), 0.36f)) free++; }
        Assert.Greater(free / (float)N, 0.35f, "между лопастями у стены есть окно по времени");
        Assert.Less(free / (float)N, 0.8f, "а проскочить не тривиально");
        yield return null;
    }

    // ---------- P-3: зоны 5–6 (лес и костёр) ----------

    // Потеря размера за секунду простоя на старте зоны при размере 0.5.
    float IdleLoss(Vector3 start)
    {
        p.flame.Revive(0.5f); p.transform.position = start; Physics.SyncTransforms();
        for (int i = 0; i < 60; i++) p.MoveInput(Dt, 0, false, false, false, false);
        return 0.5f - p.flame.size;
    }

    [UnityTest] public IEnumerator ForestColdRaisesDrain()
    {
        float house = IdleLoss(gm.zoneStart[0]);
        yield return null;
        float forest = IdleLoss(gm.zoneStart[4]);
        Assert.Greater(forest, house * 1.4f, "в лесу при том же размере расход выше");
        Assert.AreEqual(house * 1.5f, forest, house * 0.1f, "множитель 1.5");
        float clearing = IdleLoss(gm.zoneStart[5]);
        Assert.AreEqual(house, clearing, house * 0.1f, "на поляне холода нет");
    }

    [UnityTest] public IEnumerator SnowflakeCostsLittleAndCrownCovers()
    {
        var snow = All<DripSpawner>().First(s => s.name == "Snowfall");
        var drips = All<DripSpawner>().Where(s => s.name != "Snowfall").ToArray();
        Assert.Greater(drips.Length, 0);
        Assert.IsTrue(drips.All(d => d.cost == 0.18f && d.interval == 2.2f), "капли дома и подвала как раньше");
        Assert.Less(snow.cost, 0.05f); Assert.Less(snow.interval, 1f);
        foreach (var s in All<DripSpawner>().Where(s => s.name == "Snowfall")) s.enabled = false; // случайные снежинки не мешают точным замерам
        gm.JumpToZone(5);
        var start = gm.zoneStart[4];
        Drop Flake(Vector3 at) { var d = Object.Instantiate(snow.dropPrefab, at, Quaternion.identity).GetComponent<Drop>(); d.cost = snow.cost; d.gravity = snow.gravity; d.maxSpeed = snow.maxSpeed; return d; }
        Drop Rain(Vector3 at) => Object.Instantiate(snow.dropPrefab, at, Quaternion.identity).GetComponent<Drop>();
        // снежинка отнимает мало, капля как раньше
        Tp(15.5f, start.y, 0.8f);
        var f = Flake(p.transform.position); yield return null;
        Assert.AreEqual(0.8f - snow.cost, p.flame.size, 0.001f, "снежинка отняла 0.03");
        Tp(15.5f, start.y, 0.8f);
        Rain(p.transform.position); yield return null;
        Assert.AreEqual(0.62f, p.flame.size, 0.001f, "капля 0.18");
        // в просвете между кронами снежинка долетает
        Tp(15.5f, start.y, 0.8f);
        var open = Flake(p.transform.position + Vector3.up * 2.5f);
        for (int i = 0; i < 150 && open; i++) yield return null;
        Assert.AreEqual(0.8f - snow.cost, p.flame.size, 0.001f, "без кроны снег достаёт");
        // под кроной (дерево x=12, крона 10..14) снежинка с её верха не доходит
        var crown = All<Transform>().Where(t => t.name == "Crown").OrderBy(t => t.position.x).ThenByDescending(t => t.position.y).First();
        float cx = 12.5f, top = crown.position.y + 0.5f;
        Tp(cx, start.y, 0.8f);
        var covered = Flake(new Vector3(cx, top + 1f, 0));
        for (int i = 0; i < 150 && covered; i++) yield return null;
        Assert.IsTrue(covered == null, "снежинка осела на кроне");
        Assert.AreEqual(0.8f, p.flame.size, 0.001f, "под кроной снега нет");
    }

    [UnityTest] public IEnumerator ThinIceHoldsSmallFlameAndDropsBig()
    {
        var ice = All<Ice>().Where(InForest).Where(i => i.transform.position.x < 100f).OrderBy(i => i.transform.position.x).ToArray(); // ручей 1
        Assert.Greater(ice.Length, 5);
        var xs = ice.Select(i => i.transform.position.x).ToArray();
        float first = xs[0], last = xs[xs.Length - 1], y = ice[0].transform.position.y + 1.1f;
        var gaps = xs.Zip(xs.Skip(1), (a, b) => (a, b)).Where(t => t.b - t.a > 1.5f).Select(t => t.a + 0.5f).ToArray(); // правые края льда перед просветами
        // маленькое: пересекает по льду, прыгая через просветы
        Tp(first - 3f, y, 0.3f);
        yield return Run(600, _ =>
        {
            float x = p.transform.position.x;
            bool j = p.Grounded && gaps.Any(e => x > e - 0.9f && x < e - 0.4f);
            return (1, true, j, false, false);
        }, () => p.transform.position.x > last + 1.0f && p.Grounded); // пень сразу за льдом поля (224) бот не перепрыгивает
        Assert.IsFalse(p.flame.Dead, "маленькое прошло по льду");
        Assert.Greater(p.transform.position.x, last + 1.0f);
        // большое пламя: лёд тает, падает в воду
        Tp(first - 3f, y, 1f);
        yield return Run(300, Right);
        Assert.IsTrue(p.flame.Dead, "большое пламя растопило лёд и упало в воду");
    }

    [UnityTest] public IEnumerator RopeDropsLogBridgeOverRavine()
    {
        Assert.IsNull(GameObject.Find("LogPlank"), "до верёвки ствол не лежит");
        var pit = All<Pit>().Where(InForest).First(t => Mathf.Approximately(t.transform.localScale.x, 8f)).transform; // овраг (ручей шире)
        float left = pit.position.x - 4f, right = pit.position.x + 4f;
        var r = All<Rope>().Where(x => x.trigger && x.id == "log").OrderBy(x => x.transform.position.y).First().transform.position;
        Tp(left - 2f, 3.6f, 0.6f);
        yield return Run(120, Right, () => p.transform.position.x > left - 0.3f); // без моста к краю, не дальше
        Assert.IsFalse(p.flame.Dead);
        Tp(r.x, r.y + 1f, 0.6f);
        yield return Run(120, Idle);
        Assert.IsNotNull(GameObject.Find("LogPlank"), "верёвка сгорела, ствол лёг мостом");
        Tp(left - 1.5f, 3.6f, 0.6f);
        yield return Run(400, Right, () => p.transform.position.x > right + 1.5f);
        Assert.Greater(p.transform.position.x, right + 1.5f, "перешёл овраг по стволу");
        Assert.IsFalse(p.flame.Dead);
    }

    [UnityTest] public IEnumerator GaleHitsOpenSpotButNotCoverOrOtherZones()
    {
        var gale = All<Fan>().Where(InForest).OrderBy(f => f.transform.position.x).First(); float fx = gale.transform.position.x;
        gm.JumpToZone(5);
        Tp(fx - 3f, 3.6f, 0.8f); SetPhase(gale, 3.0f);
        p.MoveInput(Dt, 0, false, false, false, false);
        Assert.Less(p.windX, 0f, "на опушке без укрытия дует");
        Tp(32f, 3.6f, 0.8f); SetPhase(gale, 3.0f); // между пнями 26 и 33
        p.MoveInput(Dt, 0, false, false, false, false);
        Assert.AreEqual(0f, p.windX, "за пнём не дует");
        Tp(10f, 81.6f, 0.8f); SetPhase(gale, 3.0f); // x в зоне действия, но в доме (окна дома там не достают)
        p.MoveInput(Dt, 0, false, false, false, false);
        Assert.AreNotEqual(-6.25f, p.windX, "ветер леса не дует в других зонах");
        yield return null;
    }

    [UnityTest] public IEnumerator ForestIntroPullsCameraBack()
    {
        Assert.IsTrue(gm.JumpToZone(5));
        p.MoveInput(Dt, 0, false, false, false, false);
        Assert.IsTrue(CamFollow.I.Establishing, "общий план на старте зоны 5");
        for (int i = 0; i < 90; i++) yield return null;
        Assert.IsTrue(CamFollow.I.Establishing, "держится ~2 с");
        for (int i = 0; i < 60; i++) yield return null;
        Assert.IsFalse(CamFollow.I.Establishing, "и камера возвращается к игроку");
    }

    [UnityTest] public IEnumerator BonfireFinaleWinsWithoutDeath()
    {
        Assert.IsTrue(gm.JumpToZone(6));
        var bf = Object.FindFirstObjectByType<Bonfire>(); var give = bf.GetComponent<GiveFlame>();
        int deaths = gm.Deaths;
        Tp(bf.transform.position.x - 1.5f, bf.transform.position.y, 0.2f);
        p.giveHeld = true;
        yield return Run(200, Idle, () => give.Done);
        Assert.IsTrue(give.Done, "удержание E отдало пламя");
        Assert.AreEqual(GameManager.State.Finale, gm.state);
        float maxFade = 0;
        for (int i = 0; i < 700 && gm.state != GameManager.State.Win; i++) { maxFade = Mathf.Max(maxFade, gm.fade); yield return null; }
        Assert.AreEqual(GameManager.State.Win, gm.state, "финал дошёл до титра");
        Assert.Greater(maxFade, 0.9f, "экран гаснет почти в ноль");
        Assert.IsTrue(bf.Lit, "костёр вспыхнул");
        Assert.AreEqual(deaths, gm.Deaths, "отдать себя не смерть");
        Assert.IsFalse(p.flame.Dead, "из пламени выпрыгнул огонёк");
        Assert.Greater(p.flame.size, 0.2f);
        Assert.AreEqual(0f, gm.fade, 0.001f);
    }

    // Топливо в лесу: холод ×mult и снег (поток снежинок на метр ширины × 1 м хитбокса × цена) поверх расхода. Подпитки: смола +0.5, бумага +0.3.
    // Обычные отрезки при среднем размере 0.4, последний (подъём + поляна, 70+ м без подпитки) при «экономной» игре, средний размер 0.25 (↓).
    [UnityTest] public IEnumerator FuelLastsThroughForest()
    {
        const float AvgSpeed = 3f, Floor = 0.1f;
        var sp = All<DripSpawner>().First(s => s.name == "Snowfall");
        float snow = sp.cost / sp.interval / (2 * sp.halfWidth);
        var cz = All<ColdZone>().Single(); float zoneEnd = new Bounds(cz.transform.position, cz.transform.localScale).max.x;
        float Rate(float avg, bool forest) => forest ? (0.012f + 0.04f * avg) * cz.mult + snow : 0.012f + 0.04f * avg;
        var stops = new[] { (x: gm.zoneStart[4].x, gain: 0f) }
            .Concat(All<Candle>().Where(InForest).Select(c => (x: c.transform.position.x, gain: 0.5f)))
            .Concat(All<Paper>().Where(InForest).Select(c => (x: c.transform.position.x, gain: 0.3f)))
            .Append((x: Object.FindFirstObjectByType<Bonfire>().transform.position.x, gain: 0f)).OrderBy(s => s.x).ToArray();
        float fuel = 0.8f, longest = 0; int longestAt = 0;
        for (int i = 1; i < stops.Length; i++)
        {
            float a = stops[i - 1].x, b = stops[i].x; bool last = i == stops.Length - 1;
            if (b - a > longest) { longest = b - a; longestAt = i; }
            float inForest = Mathf.Clamp(Mathf.Min(b, zoneEnd) - a, 0, b - a);
            float avg = last ? 0.25f : 0.4f;
            fuel -= (inForest * Rate(avg, true) + (b - a - inForest) * Rate(avg, false)) / AvgSpeed;
            Assert.Greater(fuel, Floor, $"топлива не хватит до x={b:0}");
            if (!last) fuel = Mathf.Min(1f, fuel + stops[i].gain);
        }
        Assert.AreEqual(stops.Length - 1, longestAt, "самый длинный отрезок без подпитки последний: подъём к поляне");
        Assert.Greater(longest, 60f);
        yield return null;
    }


    // ---------- P-4 ----------

    // Тонкий лёд леса: большой огонёк или рывок рушат его за <= 0.1 с (6 кадров) после касания, маленький стоит.
    Ice FirstThinIce() => All<Ice>().Where(i => i.thin).OrderBy(i => i.transform.position.x).First();
    float OnTop(Ice i, float size) => i.transform.position.y + 0.5f + (10 + 16 * size) / 64f + 0.02f;

    [UnityTest] public IEnumerator ThinIceCollapsesAtOnceUnderBigFlame()
    {
        var ice = FirstThinIce(); var ip = ice.transform.position;
        Tp(ip.x, OnTop(ice, 1f), 1f);
        yield return Run(6, _ => (0, true, true, false, false)); // сразу прыгает
        Assert.IsTrue(ice == null, "тонкий лёд провалился за <= 0.1 с, несмотря на прыжок");
    }

    [UnityTest] public IEnumerator ThinIceCollapsesUnderDash()
    {
        var ice = FirstThinIce(); var ip = ice.transform.position;
        Tp(ip.x - 0.3f, OnTop(ice, 0.15f), 0.3f); // рывок отнимает 0.15: после него огонёк ниже, касание остаётся
        var tr = ""; yield return Run(6, i => { tr += $"[{i} x={p.transform.position.x - ip.x:0.00} y={p.transform.position.y - ip.y:0.00} d={p.Dashing}]"; return (1, false, false, i == 0, false); });
        Assert.IsTrue(ice == null, "рывок рушит тонкий лёд " + tr);
    }

    [UnityTest] public IEnumerator ThinIceHoldsSmallFlameStanding()
    {
        var ice = FirstThinIce(); var ip = ice.transform.position;
        Tp(ip.x, OnTop(ice, 0.3f), 0.3f);
        yield return Run(30, Idle);
        Assert.IsTrue(ice != null, "под маленьким лёд цел");
        Assert.IsFalse(p.flame.Dead);
        Assert.AreEqual(ip.y + 0.5f + p.H, p.transform.position.y, 0.1f, "стоит на льду");
    }

    [UnityTest] public IEnumerator KitchenIceStillMeltsSlowly()
    {
        var ice = All<Ice>().Where(i => !i.heatOnly && !i.thin && InHouse(i)).OrderBy(i => i.transform.position.y).First();
        Assert.IsFalse(ice.thin);
        var ip = ice.transform.position;
        Tp(ip.x - 0.3f, ip.y + 0.1f, 1f);
        yield return Run(6, Idle);
        Assert.IsTrue(ice != null, "лёд на кухне таял и раньше: не мгновенно");
    }

    [Test] public void WinTimeFormat()
    {
        Assert.AreEqual("5 мин 34 с", GameManager.FormatTime(334f));
        Assert.AreEqual("48 с", GameManager.FormatTime(48f));
        Assert.AreEqual("1 мин 0 с", GameManager.FormatTime(60f));
        Assert.AreEqual("0 с", GameManager.FormatTime(0.4f));
        Assert.AreEqual("59 с", GameManager.FormatTime(59.9f));
    }

    [UnityTest] public IEnumerator EveryUpdraftHasRunningParticles()
    {
        var ups = All<Updraft>(); Assert.Greater(ups.Length, 0);
        for (int i = 0; i < 90; i++) yield return null;
        foreach (var u in ups)
        {
            var ps = u.GetComponentInChildren<ParticleSystem>();
            Assert.IsNotNull(ps, $"у потока {u.transform.position} нет частиц");
            Assert.IsTrue(ps.isPlaying, "частицы потока идут");
            Assert.Greater(ps.particleCount, 0, "и они есть в кадре");
        }
    }

    // ---------- P-4: расширение уровня ----------

    // Зона по положению: дом y>80, подвал 60..75, канализация и шахта y 20..65 слева/справа от x шахты, лес y<21 до поляны.
    int ZoneOf(Vector3 v)
    {
        float x4 = gm.zoneStart[3].x - 3.5f, x6 = gm.zoneStart[5].x - 3.5f;
        if (v.y > 80f) return 1;
        if (v.y > 60f && v.y < 75f) return 2;
        if (v.y < 21f) return v.x < x6 ? 5 : 6;
        return v.x < x4 ? 3 : 4;
    }

    [UnityTest] public IEnumerator AtMostThreeCheckpointsPerZone()
    {
        // чекпоинт = свеча/керосинка/факел/смола (Candle) + старт зоны
        for (int z = 1; z <= 6; z++)
        {
            int n = 1 + All<Candle>().Count(c => ZoneOf(c.transform.position) == z);
            Assert.LessOrEqual(n, 3, $"зона {z}: {n} чекпоинтов (с началом зоны)");
        }
        yield return null;
    }

    // Длина пути автора: сумма Манхэттена между ключевыми точками зон. Из прохождения 2026-11-14: 334 с при ~690 м, т.е. ~2 м/с живой скорости.
    [UnityTest] public IEnumerator LevelPathLengthMatchesTarget()
    {
        Vector3 Exit(int t) => All<ZoneExit>().Single(e => e.target == t).transform.position;
        float M(Vector3 a, Vector3 b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        var zs = gm.zoneStart; float x4 = zs[3].x - 3.5f;
        var tunnelEnd = new Vector3(x4 + 23f, 58.5f, 0); var chuteBottom = new Vector3(x4 + 23.5f, zs[3].y, 0);
        var legs = new[]
        {
            M(zs[0], All<Hatch>().Single().transform.position), M(zs[1], Exit(3)), M(zs[2], zs[3]),
            M(zs[3], tunnelEnd) + M(tunnelEnd, chuteBottom) + M(chuteBottom, tunnelEnd + Vector3.right * 25) + M(tunnelEnd + Vector3.right * 25, Exit(5)),
            M(zs[4], new Vector3(Object.FindFirstObjectByType<Bonfire>().transform.position.x, zs[4].y, 0)), M(zs[5], Object.FindFirstObjectByType<Bonfire>().transform.position),
        };
        float total = legs.Sum();
        Debug.Log("Ogonok path legs " + string.Join(" + ", legs.Select(l => l.ToString("0"))) + " = " + total.ToString("0") + " m; " + (total / 3f).ToString("0") + " s at 3 m/s; " + (total / 2.06f).ToString("0") + " s at author speed");
        Assert.Greater(total / 2.06f, 540f, "автор: не меньше 9 мин");
        Assert.Less(total / 2.06f, 660f, "автор: не больше 11 мин");
        yield return null;
    }

    [UnityTest] public IEnumerator CellarShelfBridgesDropOnRopeAndHoldPlayer()
    {
        foreach (var d in All<DripSpawner>()) d.enabled = false;
        foreach (var id in new[] { "shelf1", "shelf2" })
        {
            var r = RopeId(id).transform.position;
            var pit = All<Pit>().Where(t => Mathf.Abs(t.transform.position.y - 64.5f) < 0.1f && t.transform.position.x > r.x).OrderBy(t => t.transform.position.x).First().transform;
            float left = pit.position.x - pit.localScale.x / 2, right = pit.position.x + pit.localScale.x / 2;
            var br = All<Bridge>().First(b => b.id == id);
            Assert.IsFalse(br.transform.GetChild(0).gameObject.activeSelf, id + ": полка спрятана до верёвки");
            p.flame.Revive(0.8f); Tp(left - 1.5f, 65.4f, 0.6f);
            yield return Run(120, Right, () => p.flame.Dead);
            Assert.IsTrue(p.flame.Dead, id + ": без полки провал убивает");
            p.flame.Revive(0.8f); Tp(r.x, r.y, 0.6f);
            yield return Run(120, Idle);
            Assert.IsTrue(br.transform.GetChild(0).gameObject.activeSelf, id + ": верёвка сгорела, полка легла");
            p.flame.Revive(0.8f); Tp(left - 1.5f, 65.4f, 0.6f);
            yield return Run(400, Right, () => p.transform.position.x > right + 1.5f);
            Assert.Greater(p.transform.position.x, right + 1.5f, id + ": перешёл по полке");
            Assert.IsFalse(p.flame.Dead);
        }
    }

    [UnityTest] public IEnumerator SecondLogBridgeOverSecondRavine()
    {
        var pit = All<Pit>().Where(InForest).Single(t => Mathf.Approximately(t.transform.localScale.x, 7f) && t.transform.position.x > 200f).transform;
        float left = pit.position.x - 3.5f, right = pit.position.x + 3.5f;
        var r = All<Rope>().Where(x => x.trigger && x.id == "log2").OrderBy(x => x.transform.position.y).First().transform.position;
        var br = All<Bridge>().First(b => b.id == "log2");
        Assert.IsFalse(br.transform.GetChild(0).gameObject.activeSelf);
        Tp(left - 2f, 3.6f, 0.6f);
        yield return Run(120, Right, () => p.transform.position.x > left - 0.3f);
        Assert.IsFalse(p.flame.Dead, "до верёвки к краю дошёл, не дальше");
        Tp(r.x, r.y + 1f, 0.6f);
        yield return Run(120, Idle);
        Assert.IsTrue(br.transform.GetChild(0).gameObject.activeSelf, "ствол лёг мостом");
        Tp(left - 1.5f, 3.6f, 0.6f);
        yield return Run(400, Right, () => p.transform.position.x > right + 1.5f);
        Assert.Greater(p.transform.position.x, right + 1.5f, "перешёл второй овраг");
        Assert.IsFalse(p.flame.Dead);
    }

    // Все три куска тонкого льда (ручьи 1 и 2, поле): маленький проходит, прыгая через просветы (ветер выключен: порывы проверяются отдельно).
    [UnityTest] public IEnumerator SmallFlameCrossesEveryThinIceStretch()
    {
        foreach (var f in All<Fan>().Where(InForest)) f.enabled = false;
        foreach (var s in All<DripSpawner>()) s.enabled = false;
        var xs = All<Ice>().Where(i => i.thin).Select(i => i.transform.position.x).OrderBy(x => x).ToArray();
        var clusters = new List<List<float>> { new List<float> { xs[0] } };
        for (int i = 1; i < xs.Length; i++) { if (xs[i] - xs[i - 1] > 3f) clusters.Add(new List<float>()); clusters[clusters.Count - 1].Add(xs[i]); }
        Assert.AreEqual(3, clusters.Count, "ручей 1, ручей 2, лёд на поле");
        float y = All<Ice>().First(i => i.thin).transform.position.y + 1.1f;
        foreach (var c in clusters)
        {
            float first = c[0], last = c[c.Count - 1];
            var gaps = c.Zip(c.Skip(1), (a, b) => (a, b)).Where(t => t.b - t.a > 1.5f).Select(t => t.a + 0.5f).ToArray();
            p.flame.Revive(0.3f); Tp(first - 3f, y, 0.3f); var tri = new System.Text.StringBuilder(); int fi = 0;
            yield return Run(900, _ =>
            {
                float x = p.transform.position.x;
                bool j = p.Grounded && gaps.Any(e => x > e - 0.9f && x < e - 0.4f);
                if (fi++ % 6 == 0) tri.Append($"[{fi} x={x:0.0} y={p.transform.position.y:0.0} s={p.flame.size:0.00} g={p.Grounded}] ");
                return (1, true, j, false, false);
            }, () => p.transform.position.x > last + 1.0f && p.Grounded); // пень сразу за льдом поля (224) бот не перепрыгивает
            Assert.IsFalse(p.flame.Dead, $"маленькое прошло лёд x {first:0}..{last:0} gaps={string.Join(",", gaps)} " + tri);
            Assert.Greater(p.transform.position.x, last + 1.0f);
        }
    }

    // Пустые комнаты дома проходимы по бумаге/паутине: паутина забирает ровно 0.05 и сгорает, колонны не запирают кладовку.
    [UnityTest] public IEnumerator PantryWebsBurnWithoutBlocking()
    {
        var webs = All<Cobweb>().Where(InHouse).Where(c => c.transform.position.x > 97f && c.transform.position.x < 121f).OrderBy(c => c.transform.position.x).ToArray();
        Assert.AreEqual(4, webs.Length, "четыре колонны паутины в кладовке");
        foreach (var d in All<DripSpawner>()) d.enabled = false;
        Tp(96.5f, HouseY, 0.7f);
        float x1 = webs.Last().transform.position.x + 1.5f;
        yield return Run(900, _ => (1, true, p.Grounded && Physics.CheckBox(p.transform.position + new Vector3(1.1f, 0.3f, 0), new Vector3(0.3f, 0.25f, 0.3f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore), false, false), () => p.transform.position.x > x1); // прыжок через полки
        Assert.IsFalse(p.flame.Dead, "прошёл кладовку");
        Assert.Greater(p.transform.position.x, x1, "паутина не запирает проход");
        Assert.IsTrue(webs.All(w => w == null), "вся паутина по пути сгорела");
    }
    static void Capture(string path)
    {
        LogAssert.ignoreFailingMessages = true; // Camera.Render в batchmode шумит ошибкой шейдера редактора
        var cam = Camera.main; var rt = new RenderTexture(1280, 720, 24);
        cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
        var t = new Texture2D(1280, 720, TextureFormat.RGB24, false); t.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); t.Apply();
        System.IO.File.WriteAllBytes(path, t.EncodeToPNG());
        cam.targetTexture = null; RenderTexture.active = null;
    }
}
