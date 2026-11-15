using System.Linq;
using UnityEditor;
using UnityEngine;

// P-14: GameAudio в сцене + импорт клипов под WebGL. Повторный Build Level 1 пересобирает всё из этой таблицы, ссылки не теряются.
public static partial class OgonokBuilder
{
    // (событие, громкость, 3D, мин. интервал между запусками, клипы; первые из SFX, Amb* из Ambience). Громкости на слух не подбирались, см. implementation-notes.
    static readonly (Snd id, float vol, bool spatial, float gap, string[] clips)[] audioTable =
    {
        (Snd.Jump, 0.7f, false, 0, new[] { "igni_jump" }),
        (Snd.Dash, 0.8f, false, 0, new[] { "igni_dash" }),
        (Snd.TransformBig, 0.4f, false, 0, new[] { "igni_transform_big" }),
        (Snd.TransformSmall, 0.8f, false, 0, new[] { "igni_transform_small" }),
        (Snd.TransformNormal, 0.8f, false, 0, new[] { "igni_transform_normal" }),
        (Snd.Death, 1f, false, 0, new[] { "igni_death" }),
        (Snd.Respawn, 0.8f, false, 0, new[] { "igni_respawn" }),
        (Snd.Pickup, 0.8f, true, 0, new[] { "pickup_paper" }),
        (Snd.Candle, 0.9f, true, 0, new[] { "candle_light" }),
        (Snd.BoilerGive, 0.8f, false, 0, new[] { "boiler_give_flame" }),
        (Snd.BoilerIgnite, 1f, true, 0, new[] { "boiler_ignite" }),
        (Snd.BonfireIgnite, 1f, false, 0, new[] { "bonfire_ignite" }),
        (Snd.WindWarning, 0.8f, true, 0, new[] { "wind_warning" }),
        (Snd.WindGust, 1f, true, 0, new[] { "wind_gust" }),
        (Snd.CurtainFlap, 0.6f, true, 0, new[] { "curtain_flap" }),
        (Snd.DropWater, 0.4f, true, 0.15f, new[] { "drop_fall_water" }), // капля о пол и о лужу: один звук
        (Snd.DropHit, 0.9f, true, 0.15f, new[] { "drop_hit_igni" }),
        (Snd.SnowHit, 0.5f, true, 0.3f, new[] { "snow_hit_igni" }),
        (Snd.IceMelt, 0.9f, true, 0.15f, new[] { "ice_melt" }),
        (Snd.IceRefreeze, 0.7f, true, 0.15f, new[] { "ice_refreeze" }),
        (Snd.RopeBurn, 0.8f, true, 0.3f, new[] { "rope_burn" }),
        (Snd.Wardrobe, 1f, true, 0, new[] { "wardrobe_crash" }),
        (Snd.Shelf, 1f, true, 0, new[] { "shelf_drop" }),
        (Snd.Log, 1f, true, 0, new[] { "log_drop" }),
        (Snd.Hatch, 1f, true, 0, new[] { "hatch_slam" }),
        (Snd.Cobweb, 0.8f, true, 0, new[] { "cobweb_burn" }),
        (Snd.Raft, 0.7f, true, 0.8f, new[] { "raft_creak" }),
        (Snd.Methane, 0.9f, true, 0.1f, new[] { "methane_pop" }),
        (Snd.FanStop, 1f, true, 0, new[] { "fan_stop" }),
        (Snd.FanLoop, 0.25f, true, 0, new[] { "fan_heavy_loop" }),
        (Snd.UpdraftLoop, 0.5f, true, 0, new[] { "updraft_loop" }),
        (Snd.CrackleLoop, 1f, false, 0, new[] { "igni_crackle_loop" }),
        (Snd.HissLoop, 0.6f, false, 0, new[] { "igni_hiss_low" }),
        (Snd.AmbHouse, 1f, false, 0, new[] { "house_cold_loop" }),
        (Snd.AmbSewer, 1f, false, 0, new[] { "sewer_water_loop" }),
        (Snd.AmbShaft, 1f, false, 0, new[] { "shaft_air_loop" }),
        (Snd.AmbForest, 1f, false, 0, new[] { "forest_winter_loop" }),
    };

    static void BuildAudio()
    {
        var ga = new GameObject("GameAudio").AddComponent<GameAudio>();
        ga.slots = new GameAudio.Slot[System.Enum.GetValues(typeof(Snd)).Length];
        foreach (var (id, vol, spatial, gap, names) in audioTable)
        {
            string dir = id >= Snd.AmbHouse ? "Ambience" : "SFX";
            var clips = names.Select(n => LoadClip($"Assets/Audio/{dir}/{n}.wav", n.Contains("_loop"))).Where(c => c).ToArray();
            ga.slots[(int)id] = new GameAudio.Slot { clips = clips, volume = vol, spatial = spatial, minGap = gap };
        }
    }

    static AudioClip LoadClip(string path, bool loop)
    {
        var imp = AssetImporter.GetAtPath(path) as AudioImporter;
        if (imp == null) { Debug.LogError("Ogonok audio: нет клипа " + path); return null; }
        // Короткие SFX: Decompress On Load (запуск без задержки), Vorbis q 0.6. Петли и фоны: Compressed In Memory, Vorbis q 0.5, Load In Background.
        var s = new AudioImporterSampleSettings
        {
            loadType = loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad,
            compressionFormat = AudioCompressionFormat.Vorbis, quality = loop ? 0.5f : 0.6f, sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate,
        };
        var d = imp.defaultSampleSettings;
        if (d.loadType != s.loadType || d.compressionFormat != s.compressionFormat || !Mathf.Approximately(d.quality, s.quality) || imp.loadInBackground != loop || !imp.ContainsSampleSettingsOverride("WebGL"))
        {
            imp.defaultSampleSettings = s; imp.SetOverrideSampleSettings("WebGL", s); imp.loadInBackground = loop;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
}
