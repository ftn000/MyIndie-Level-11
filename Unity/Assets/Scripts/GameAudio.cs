using System;
using System.Collections.Generic;
using UnityEngine;

public enum Snd
{
    Jump, Dash, TransformBig, TransformSmall, TransformNormal, Death, Respawn, Pickup, Candle, BoilerGive, BoilerIgnite, BonfireIgnite,
    WindWarning, WindGust, CurtainFlap, DropWater, DropHit, SnowHit, IceMelt, IceRefreeze, RopeBurn, Wardrobe, Shelf, Log, Hatch, Cobweb,
    Raft, Methane, FanStop, FanLoop, UpdraftLoop, CrackleLoop, HissLoop, UiClick, UiPause, AmbHouse, AmbSewer, AmbShaft, AmbForest,
}

// Весь звук игры. Один компонент на сцену (создаёт OgonokBuilder): клипы привязаны через Slot, поиска по строкам и Resources нет.
// Механики вызывают одну строку: GameAudio.Play(Snd.X, позиция). Без GameAudio в сцене всё молчит и не падает.
// Слушатель (AudioListener) стоит на игроке, а не на камере: камера на z = -13, мировые звуки иначе тонули бы в затухании.
public class GameAudio : MonoBehaviour
{
    [Serializable] public class Slot { public AudioClip[] clips; public float volume = 1, minGap; public bool spatial; }

    public static GameAudio I;
    public static float MusicMul = 1, SfxMul = 1; // настройки игрока 0..1, PlayerPrefs
    public AudioClip musicClip;
    public const float MusicMenu = 0.6f, MusicGame = 0.18f;
    public Slot[] slots;
    public float sfxVolume = 1, ambVolume = 0.5f, fireVolume = 1; // shortcut: группы громкости вместо AudioMixer, подключить микшер, когда появятся настройки
    public const float MinDist = 3f, MaxDist = 18f;
    const int Pool = 8;

    readonly AudioSource[] pool = new AudioSource[Pool];
    readonly float[] startT = new float[Pool];
    readonly float[] lastT = new float[Enum.GetValues(typeof(Snd)).Length];
    readonly int[] counts = new int[Enum.GetValues(typeof(Snd)).Length];
    readonly AudioSource[] amb = new AudioSource[2];
    AudioSource crackle, hiss, give, music, ui;
    bool crackleOn, hissOn, giveWant;
    object giveOwner;
    int cur;
    float silence;
    bool fireBack;
    class Loop { public AudioSource src; public float vol; public Vector3 size; public bool stopping; }
    readonly List<Loop> loops = new();

    public int Zone { get; private set; }
    public AudioClip AmbientClip => amb[cur].clip;
    public float AmbientVolume => amb[cur].volume;
    public float CrackleVolume => crackle.volume;
    public float HissVolume => hiss.volume;
    public int FireStarts { get; private set; } // сколько раз запускались петли огня (после респауна +1, у порога топлива не растёт)
    public int Count(Snd id) => counts[(int)id];

    void Awake()
    {
        I = this;
        MusicMul = PlayerPrefs.GetFloat("vol_music", 1); SfxMul = PlayerPrefs.GetFloat("vol_sfx", 1);
        for (int i = 0; i < Pool; i++) pool[i] = MakeSource("Sfx" + i, false);
        crackle = MakeSource("Crackle", true); hiss = MakeSource("Hiss", true); give = MakeSource("GiveFlame", false);
        amb[0] = MakeSource("AmbA", true); amb[1] = MakeSource("AmbB", true);
        music = MakeSource("Music", true); ui = MakeSource("Ui", false);
        music.ignoreListenerPause = ui.ignoreListenerPause = true; // пауза глушит мир, не музыку и меню
        music.clip = musicClip; if (musicClip) music.Play();
        crackle.clip = Clip(Snd.CrackleLoop); hiss.clip = Clip(Snd.HissLoop); give.clip = Clip(Snd.BoilerGive);
    }

    void OnDestroy() { if (I == this) I = null; }

    AudioSource MakeSource(string n, bool loop)
    {
        var go = new GameObject(n); go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false; s.loop = loop; s.volume = 0; s.spatialBlend = 0;
        s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = MinDist; s.maxDistance = MaxDist;
        return s;
    }

    Slot SlotOf(Snd id) => slots != null && (int)id < slots.Length ? slots[(int)id] : null;
    AudioClip Clip(Snd id) { var s = SlotOf(id); return s != null && s.clips != null && s.clips.Length > 0 ? s.clips[0] : null; }

    // ---------- API механик ----------

    public static void Play(Snd id, Vector3 pos = default, float pitch = 1f) { if (I) I.PlayAt(id, pos, pitch); }

    public static void SetVolume(bool musicVol, float v)
    {
        v = Mathf.Clamp01(v);
        if (musicVol) MusicMul = v; else SfxMul = v;
        PlayerPrefs.SetFloat(musicVol ? "vol_music" : "vol_sfx", v);
    }

    // Звук меню: идёт и на паузе.
    public static void Ui(Snd id)
    {
        var a = I; var c = a ? a.Clip(id) : null;
        if (c) a.ui.PlayOneShot(c, a.Vol(id) * SfxMul);
    }

    // Музыка: меню/пауза/победа 0.6, игра 0.18, финал уходит в тишину вместе с остальным. Фейд ~1.5 с на unscaled времени.
    void MusicUpdate(GameManager gm)
    {
        if (!music.clip) return;
        var st = gm.state;
        float t = st == GameManager.State.Win || st == GameManager.State.Title || gm.paused ? MusicMenu
            : st == GameManager.State.Finale ? (fireBack ? MusicMenu : MusicGame * (1 - silence)) : MusicGame;
        music.volume = Mathf.MoveTowards(music.volume, t * MusicMul, Time.unscaledDeltaTime * MusicMenu / 1.5f);
        if (!music.isPlaying) music.Play();
    }

    // Удержание E у котла/костра: звук идёт, пока идёт прогресс; owner различает котлы между собой.
    public static void Hold(object owner, bool on)
    {
        var a = I; if (!a) return;
        if (on) { if (a.giveOwner == owner && a.giveWant) return; a.giveOwner = owner; a.giveWant = true; a.counts[(int)Snd.BoilerGive]++; a.give.volume = 0; if (a.give.clip) a.give.Play(); }
        else if (a.giveOwner == owner) a.giveWant = false;
    }

    // Финал: 0..1, насколько убран окружающий звук и огонь. FireBack: огонь Игни снова слышен при возвращении.
    public static void Silence(float k) { if (I) I.silence = k; }
    public static void FireBack() { if (I) I.fireBack = true; }

    // Локальная 3D-петля на объекте (поток, вентилятор). size > 0: звук идёт от ближайшей точки объёма. Слышна только вблизи.
    public static AudioSource LoopAt(GameObject host, Snd id, Vector3 size = default)
    {
        var a = I; var sl = a ? a.SlotOf(id) : null;
        if (sl == null || sl.clips == null || sl.clips.Length == 0) return null;
        var s = a.MakeSource("Loop_" + id, true); s.transform.SetParent(host.transform, false);
        s.clip = sl.clips[0]; s.spatialBlend = 1;
        a.loops.Add(new Loop { src = s, vol = sl.volume, size = size });
        return s;
    }

    public static void StopLoop(AudioSource s)
    {
        if (!I || !s) return;
        foreach (var l in I.loops) if (l.src == s) l.stopping = true;
    }

    // ---------- одиночные звуки ----------

    Vector3 Listener => Flame.I ? Flame.I.transform.position : Vector3.zero;

    void PlayAt(Snd id, Vector3 pos, float pitch)
    {
        var s = SlotOf(id);
        if (s == null || s.clips == null || s.clips.Length == 0) return;
        if (s.minGap > 0 && Time.time < lastT[(int)id] + s.minGap) return;
        if (s.spatial && ((new Vector2(pos.x, pos.y) - (Vector2)Listener).magnitude > MaxDist || OtherZone(pos))) return; // не слышно: голос не тратим
        lastT[(int)id] = Time.time; counts[(int)id]++;
        int k = 0;
        for (int i = 1; i < Pool; i++) if (startT[i] < startT[k]) k = i; // самый давний (свободные имеют меньшее startT, если не играют)
        for (int i = 0; i < Pool; i++) if (!pool[i].isPlaying) { k = i; break; }
        var src = pool[k]; startT[k] = Time.time;
        src.Stop();
        src.clip = s.clips[s.clips.Length == 1 ? 0 : UnityEngine.Random.Range(0, s.clips.Length)];
        src.volume = s.volume * sfxVolume * SfxMul;
        src.pitch = pitch * (s.minGap > 0 ? UnityEngine.Random.Range(0.95f, 1.05f) : 1f);
        src.spatialBlend = s.spatial ? 1 : 0;
        src.transform.position = s.spatial ? new Vector3(pos.x, pos.y, 0) : Listener;
        src.Play();
    }

    // ---------- петли огня, фон, локальные петли ----------

    // Зона по реальной позиции игрока в габаритах зон (mapRects идут в порядке зон 1..N, этажи не пересекаются по y). Вне всех: прежняя.
    int ZoneAt(GameManager gm) { int z = gm.ZoneOf(gm.player.transform.position); return z != 0 ? z : Zone; }

    // Источник из другой зоны (соседний этаж за полом) не слышен: иначе винт шахты звучит в доме. Вне зон (0) не режем.
    bool OtherZone(Vector3 pos) { var gm = GameManager.I; int z = gm ? gm.ZoneOf(pos) : 0; return z != 0 && Zone != 0 && z != Zone; }

    (AudioClip clip, float vol) AmbFor(int z)
    {
        var id = z == 3 ? Snd.AmbSewer : z == 4 ? Snd.AmbShaft : z >= 5 ? Snd.AmbForest : Snd.AmbHouse;
        return (Clip(id), (SlotOf(id)?.volume ?? 1) * (z == 2 ? 0.5f : 1f)); // подвал: тот же дом, тише
    }

    void Update()
    {
        var gm = GameManager.I; var f = Flame.I;
        if (gm) MusicUpdate(gm);
        if (!gm || !f || !gm.player) return;
        float dt = Time.deltaTime;
        bool on = gm.state != GameManager.State.Title; // до первого нажатия тишина: в браузере AudioContext оживает от нажатия
        Zone = ZoneAt(gm);

        // фон: кроссфейд ~1 с между двумя источниками
        var (clip, v) = AmbFor(Zone);
        if (amb[cur].clip != clip) { cur ^= 1; var n = amb[cur]; n.clip = clip; n.volume = 0; if (clip) n.Play(); }
        for (int i = 0; i < 2; i++)
        {
            float target = i == cur && amb[i].clip ? v * ambVolume * SfxMul * (on ? 1 : 0) * (1 - silence) : 0;
            amb[i].volume = Mathf.MoveTowards(amb[i].volume, target, dt * (silence > 0 ? 2f : 1f));
            if (i != cur && amb[i].volume <= 0 && amb[i].isPlaying) amb[i].Stop();
        }

        // огонь Игни: треск по размеру формы, тревожное шипение по топливу; стоп на Dead
        bool alive = on && !f.Dead;
        float fireK = fireVolume * SfxMul * (1 - (fireBack ? 0 : silence));
        Fire(crackle, ref crackleOn, alive, alive ? Mathf.Lerp(0.25f, 0.6f, f.size) * Vol(Snd.CrackleLoop) * fireK : 0, 3 * dt);
        crackle.pitch = Mathf.Lerp(1.15f, 0.9f, f.size);
        Fire(hiss, ref hissOn, alive && f.fuel < Flame.GuttersBelow, alive ? f.Weak * Vol(Snd.HissLoop) * fireK : 0, 1.5f * dt);

        // E у котла
        if (f.Dead) giveWant = false;
        give.volume = Mathf.MoveTowards(give.volume, giveWant ? Vol(Snd.BoilerGive) * sfxVolume * SfxMul : 0, 8 * dt);
        if (!giveWant && give.volume <= 0 && give.isPlaying) give.Stop();

        var pp = f.transform.position;
        for (int i = loops.Count - 1; i >= 0; i--)
        {
            var l = loops[i];
            if (!l.src) { loops.RemoveAt(i); continue; }
            var cp = new Bounds(l.src.transform.parent.position, l.size).ClosestPoint(pp); cp.z = 0;
            l.src.transform.position = cp;
            float target = !l.stopping && on && ((Vector2)cp - (Vector2)pp).magnitude < MaxDist && !OtherZone(l.src.transform.parent.position) ? l.vol * sfxVolume * SfxMul : 0;
            l.src.volume = Mathf.MoveTowards(l.src.volume, target, dt * 2);
            if (target > 0 && !l.src.isPlaying) l.src.Play();
            else if (l.src.volume <= 0 && l.src.isPlaying) l.src.Stop();
            if (l.stopping && l.src.volume <= 0) loops.RemoveAt(i);
        }
    }

    float Vol(Snd id) => SlotOf(id)?.volume ?? 1;

    // Петля запускается один раз на «жив/мёртв», громкость плавно идёт к цели; Stop только на нуле, поэтому у порога ничего не перезапускается.
    void Fire(AudioSource s, ref bool started, bool want, float target, float step)
    {
        if (!s.clip) return;
        s.volume = Mathf.MoveTowards(s.volume, target, step);
        if (want && !started) { started = true; FireStarts++; s.Play(); }
        else if (!want && started && s.volume <= 0) { started = false; s.Stop(); }
    }
}
