using System;
using System.Collections;
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

    static T[] All<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsSortMode.None);
    static Fan Window(string n) => GameObject.Find(n).GetComponent<Fan>();
    // капли в котельной (подвал-спуск и слив капают всегда, их не считаем)
    static int DropsAround(Boiler b) => All<Drop>().Count(d => d.transform.position.x > b.transform.position.x - 30f && d.transform.position.x < b.transform.position.x + 8f);
    static Rope RopeId(string id) => All<Rope>().Where(r => r.trigger && r.id == id).OrderBy(r => r.transform.position.y).First();
    // Зоны делят сетку: дом выше всех (y > 60), лес внизу (y < 60). Тесты старых зон выбирают объекты по этому признаку.
    static bool InHouse(Component c) => c.transform.position.y > 80f;
    static bool InForest(Component c) => c.transform.position.y < 60f;
    static Transform HousePit() => All<Pit>().Single(InHouse).transform;

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
        var v = All<Vent>().Single().transform.position;
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
        var pit = HousePit();
        float left = pit.position.x - pit.localScale.x / 2, right = pit.position.x + pit.localScale.x / 2;
        Tp(left - 1.5f, r.y - 1.9f, 1f); // на полу спальни перед провалом
        yield return Run(600, Right, () => p.transform.position.x > right + 1.5f);
        Assert.Greater(p.transform.position.x, right + 1.5f, "перешёл провал по мосту");
        Assert.IsFalse(p.flame.Dead);
    }

    [UnityTest] public IEnumerator PitKillsWithoutBridge()
    {
        var pit = HousePit();
        float left = pit.position.x - pit.localScale.x / 2;
        Tp(left - 1.5f, pit.position.y + 4.1f, 1f);
        yield return Run(300, Right);
        Assert.IsTrue(p.flame.Dead, "в провал упал и погас");
    }

    // Тёплые потоки живут в зоне 4: проверяем механику на фикстуре с геометрией старой пропасти, смещённой вправо за уровень.
    [UnityTest] public IEnumerator PitCrossedWithUpdrafts()
    {
        const float X = 200f;
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
        Tp(40.5f, 81.6f, 1f); float big = p.flame.DrainRate;
        p.flame.size = 0.1f; float small = p.flame.DrainRate;
        Assert.Greater(big / small, 2.5f);
        yield return null;
    }

    [UnityTest] public IEnumerator WindParticlesAppearInGust()
    {
        var fan = Window("WindowFanB"); var fx = fan.transform.position;
        Tp(fx.x - 4f, 81.6f, 1f); SetPhase(fan, 2.9f);
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
        Tp(fx - 3f, 81.6f, 0.8f); SetPhase(fan, 3.0f);
        p.MoveInput(Dt, 0, false, false, false, false);
        Assert.Less(p.windX, 0f, "в порыв на открытом месте дует");
        yield return Run(120, Idle);
        Assert.IsTrue(p.flame.Dead || p.flame.size < 0.5f, "порыв гасит");
        // укрытие: мебель между игроком и окном
        Tp(fx - 9f, 81.6f, 0.8f); p.flame.Revive(0.8f); SetPhase(fan, 3.0f);
        float s0 = p.flame.size;
        yield return Run(60, Idle);
        Assert.AreEqual(0f, p.windX, "за мебелью не дует");
        Assert.IsFalse(p.flame.Dead);
        Assert.Greater(p.flame.size, s0 - 0.08f, "в укрытии тратится только обычный расход");
    }

    [UnityTest] public IEnumerator WindowGustPushOnlyAtFirstWindow()
    {
        var fan = Window("WindowFanA"); float fx = fan.transform.position.x;
        Tp(fx - 2.5f, 81.6f, 0.8f); SetPhase(fan, 3.0f); // правее мебели, левее окна
        p.MoveInput(Dt, 0, false, false, false, false);
        Assert.Less(p.windX, 0f, "первое окно толкает");
        float s0 = p.flame.size;
        yield return Run(30, Idle);
        Assert.IsFalse(p.flame.Dead);
        Assert.Greater(p.flame.size, s0 - 0.06f, "но не гасит");
    }

    [UnityTest] public IEnumerator CurtainBillowsBeforeGust()
    {
        var fan = Window("WindowFanB"); var cur = fan.curtain;
        Tp(fan.transform.position.x - 9f, 81.6f, 1f);
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
        var web = All<Cobweb>().Single(); var wp = web.transform.position;
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
        float basementTop = gm.zoneStart[1].y + 3f; // пол подвала (H = 94: y около 65)
        yield return Run(400, Idle, () => p.Grounded && p.transform.position.y < basementTop);
        yield return null;
        Assert.Less(p.transform.position.y, basementTop, "упал в подвал");
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
        Assert.AreEqual(6, gm.zoneStart.Length, "зоны 3–4 пока заглушки (копии старта зоны 2), зона 5 пятая");
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

    // Проходимость по топливу: между подзарядками (старт зоны, свечи, керосинка) хватает пламени при реальной средней скорости 3 м/с
    // (с запасом на прыжки и ожидание) и среднем размере 0.5 (расход 0.012+0.04·s). Подзарядка свечи +0.5, старт 0.8.
    [UnityTest] public IEnumerator FuelLastsBetweenRefuels()
    {
        const float AvgSpeed = 3f, Avg = 0.5f, Floor = 0.1f;
        float rate = 0.012f + 0.04f * Avg;
        var goal = Object.FindFirstObjectByType<Goal>().transform.position.x;
        foreach (var (from, to) in new[] { (0, 1), (1, 2) })
        {
            // зоны 1–2 живут в верхней части сетки (y > 60); свечи леса на тех же x в счёт не идут
            float x0 = gm.zoneStart[from].x, x1 = to == 1 ? gm.zoneStart[1].x : goal;
            var stops = new[] { x0 }.Concat(All<Candle>().Where(c => c.transform.position.y > 60f).Select(c => c.transform.position.x).Where(x => x > x0 && x < x1)).Append(x1).OrderBy(x => x).ToArray();
            float fuel = 0.8f;
            for (int i = 1; i < stops.Length; i++)
            {
                fuel -= (stops[i] - stops[i - 1]) / AvgSpeed * rate;
                Assert.Greater(fuel, Floor, $"зона {from + 1}: топлива не хватит до x={stops[i]:0}");
                if (i < stops.Length - 1) fuel = Mathf.Min(1f, fuel + 0.5f);
            }
        }
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
        var ice = All<Ice>().Where(InForest).OrderBy(i => i.transform.position.x).ToArray();
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
        }, () => p.transform.position.x > last + 1.5f && p.Grounded);
        Assert.IsFalse(p.flame.Dead, "маленькое прошло по льду");
        Assert.Greater(p.transform.position.x, last + 1.5f);
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

    // Топливо в лесу: холод ×mult и снег (поток снежинок на метр ширины × 1 м хитбокса × цена) поверх расхода, подзарядки смолой +0.5.
    // Обычные отрезки при среднем размере 0.4, последний (подъём + поляна, 70+ м без подпитки) при «экономной» игре, средний размер 0.25 (↓).
    [UnityTest] public IEnumerator FuelLastsThroughForest()
    {
        const float AvgSpeed = 3f, Floor = 0.1f;
        var sp = All<DripSpawner>().First(s => s.name == "Snowfall");
        float snow = sp.cost / sp.interval / (2 * sp.halfWidth);
        var cz = All<ColdZone>().Single(); float zoneEnd = new Bounds(cz.transform.position, cz.transform.localScale).max.x;
        float Rate(float avg, bool forest) => forest ? (0.012f + 0.04f * avg) * cz.mult + snow : 0.012f + 0.04f * avg;
        var stops = new[] { gm.zoneStart[4].x }
            .Concat(All<Candle>().Where(InForest).Select(c => c.transform.position.x)).Append(Object.FindFirstObjectByType<Bonfire>().transform.position.x).OrderBy(x => x).ToArray();
        float fuel = 0.8f, longest = 0; int longestAt = 0;
        for (int i = 1; i < stops.Length; i++)
        {
            float a = stops[i - 1], b = stops[i]; bool last = i == stops.Length - 1;
            if (b - a > longest) { longest = b - a; longestAt = i; }
            float inForest = Mathf.Clamp(Mathf.Min(b, zoneEnd) - a, 0, b - a);
            float avg = last ? 0.25f : 0.4f;
            fuel -= (inForest * Rate(avg, true) + (b - a - inForest) * Rate(avg, false)) / AvgSpeed;
            Assert.Greater(fuel, Floor, $"топлива не хватит до x={b:0}");
            if (!last) fuel = Mathf.Min(1f, fuel + 0.5f);
        }
        Assert.AreEqual(stops.Length - 1, longestAt, "самый длинный отрезок без подпитки последний: подъём к поляне");
        Assert.Greater(longest, 60f);
        yield return null;
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
