using System.IO;
using UnityEditor;
using UnityEngine;

// P-12: финальная гостиная (зона 1) из слоёв room 1. Только в art-режиме (Level1Art). Масштаб 65 px исходника = 1 клетка (1 м).
// Пересчёт: мир.x = 1 + (x_px − 30)/65, мир.y = 81 + (790 − y_px)/65 (81 = верх пола гостиной). Коллайдеры: невидимые кубы по верхним граням.
public static partial class OgonokBuilder
{
    const float R1Floor = 81f;
    static float Px(float x) => 1f + (x - 30f) / 65f;
    static float Py(float y) => R1Floor + (790f - y) / 65f;
    // Свой материал с текстурой на каждый слой: общий Sprites/Default с property block в превью путал текстуры (слои показывали задник)
    static Material R1Mat(Sprite sp)
    {
        return SaveMat(new Material(Shader.Find("Sprites/Default")) { mainTexture = sp.texture }, $"Assets/Materials/R1_{sp.name}.mat");
    }

    // file | canvas w,h | alpha x,y,w,h | rect x,y,w,h (исходник) | sorting | fit (g среднее, w по ширине, h по высоте) | align (B низ, T верх)
    static readonly string[] R1Sprites =
    {
        "01-fireplace|1024,1536|190,20,645,1483|35,30,310,765|20|g|B",
        "02-armchair|1164,1351|99,72,963,1225|475,579,200,220|30|g|B",
        "03-bookcase|1103,1426|161,33,781,1311|690,350,280,450|20|g|B",
        "04-clock|887,1774|213,50,462,1654|970,362,135,429|25|g|B",
        "05-dresser|1630,965|99,89,1432,798|1115,589,345,210|30|g|B",
        "06-nightstand|1184,1328|184,163,811,1007|1481,653,129,143|30|g|B",
        "07-crate-large|1459,1078|65,155,1329,781|379,721,108,69|30|g|B",
        "08-crate-small|1536,1024|290,275,955,489|411,691,53,36|31|g|B",
        "09-jewelry-box|1536,1024|102,129,1330,822|1160,553,70,44|35|g|B",
        "10-books-stack|1536,1024|197,173,1148,678|1271,549,88,48|35|g|B",
        "11-shelf-chimney-top|2172,724|147,97,1877,532|230,278,130,55|20|w|T",
        "12-shelf-chimney-middle|2172,724|155,157,1862,428|235,348,130,61|20|w|T",
        "13-shelf-chimney-bottom|2172,724|135,201,1902,383|238,431,128,53|20|w|T",
        "14-shelf-upper-small|2172,724|89,155,1996,410|390,292,165,65|20|w|T",
        "15-shelf-upper-long|2172,724|80,163,2010,397|555,350,148,62|20|w|T",
        "16-shelf-candle|2172,724|113,171,1940,420|380,500,225,65|20|w|T",
        "17-shelf-low|2172,724|89,203,1986,378|316,605,144,50|20|w|T",
        "18-floor-platform|2172,724|24,269,2126,198|19,787,1636,140|15|w|T",
        "19-rear-library|1024,1536|114,30,796,1464|1060,229,309,557|5|g|B",
        "20-curtain-rail|2172,724|75,218,2023,291|1373,230,180,30|40|w|B",
        "21-curtain-rest|1169,1345|91,115,985,1156|1195,250,355,305|39|g|T",
        "22-curtain-wind|1220,1289|39,59,1122,1183|1175,250,375,305|39|g|T",
        "23-candle|1024,1536|183,346,660,886|421,460,18,40|35|h|B",
        "24-cobweb|1536,1024|139,9,1259,1006|610,400,110,140|40|g|T",
    };

    static Sprite R1Import(string file)
    {
        var path = "Assets/Art/Room1/" + file + ".png";
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = 1; imp.mipmapEnabled = false; imp.alphaIsTransparency = true; imp.wrapMode = TextureWrapMode.Clamp;
        imp.maxTextureSize = 4096; imp.filterMode = FilterMode.Bilinear;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static int[] Ints(string s) => System.Array.ConvertAll(s.Split(','), int.Parse);

    // Кладёт слой так, чтобы видимая часть (alpha bounds) легла на suggested rect; z = 0 (перспектива камеры не искажает масштаб).
    static GameObject R1Place(Transform parent, string key, string nameOverride = null)
    {
        var row = System.Array.Find(R1Sprites, r => r.StartsWith(key)).Split('|');
        var cv = Ints(row[1]); var a = Ints(row[2]); var r = Ints(row[3]); int order = int.Parse(row[4]);
        float sx = (float)r[2] / a[2], sy = (float)r[3] / a[3];
        float s = row[5] == "w" ? sx : row[5] == "h" ? sy : Mathf.Sqrt(sx * sy);
        float boxW = a[2] * s, boxH = a[3] * s, cx = r[0] + r[2] / 2f;
        float cy = row[6] == "B" ? r[1] + r[3] - boxH / 2 : r[1] + boxH / 2; // центр видимого бокса, y вниз
        float ccx = cx + (cv[0] / 2f - (a[0] + a[2] / 2f)) * s, ccy = cy + (cv[1] / 2f - (a[1] + a[3] / 2f)) * s;
        return R1Sprite(parent, nameOverride ?? row[0], R1Import(row[0]), new Vector3(Px(ccx), Py(ccy), 0), s / 65f, order - 100);
    }

    static GameObject R1Sprite(Transform parent, string n, Sprite sp, Vector3 pos, float scale, int order)
    {
        var go = new GameObject(n); go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var ps = parent ? parent.lossyScale : Vector3.one; go.transform.localScale = new Vector3(scale / ps.x, scale / ps.y, scale / ps.z); // родитель-куб с масштабом не искажает слой
        var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sp; sr.sharedMaterial = R1Mat(sp);
        sr.sortingOrder = order; // ниже 0: игрок, огонь и частицы рисуются поверх
        return go;
    }

    // Невидимый коллайдер: клетки a..b от левой стены, высота над полом y0..y1.
    static GameObject R1Box(Transform l, string n, float a, float b, float y0, float y1)
    {
        var c = Cube(n, l, new Vector3(1 + (a + b) / 2, R1Floor + (y0 + y1) / 2, 0), new Vector3(b - a, y1 - y0, Depth), mFurn);
        c.GetComponent<Renderer>().enabled = false; return c;
    }
    static void R1Plate(Transform l, string n, float a, float b, float top) => R1Box(l, n, a, b, top - 0.25f, top);

    static void R1Paper(Transform l, float cell, float top)
    {
        var pa = Cube("Paper", l, new Vector3(1 + cell, R1Floor + top + 0.4f, 0), new Vector3(0.35f, 0.45f, 0.05f), mPaper, false);
        pa.transform.rotation = Quaternion.Euler(0, 0, -12); pa.AddComponent<Paper>();
    }

    static void Room1Final(Transform l)
    {

        // грейбокс-оболочка гостиной (стены, потолок, пол) остаётся коллайдерами, но не рисуется; камин без кубов
        foreach (Transform t in l)
            if (t.name == "Stone" && t.position.x < 30 && t.position.y >= 80) t.GetComponent<Renderer>().enabled = false;
        var fp = l.Find("Fireplace");
        foreach (var n in new[] { "Left", "Right", "Top", "Log" }) Object.DestroyImmediate(fp.Find(n).gameObject);

        // задник 1671×941 в том же масштабе, цельным слоем; справа от него тёмный проём 26..29
        R1Sprite(l, "00-background", R1Import("00-background"), new Vector3(Px(835.5f), Py(470.5f), 0), 1f / 65, -100);
        var door = Cube("DoorwayDark", l, new Vector3(28.1f, R1Floor + 6, 2.5f), new Vector3(4, 12, 1), mWall, false);

        foreach (var k in new[] { "19-", "18-", "01-", "02-", "03-", "04-", "05-", "06-", "07-", "08-", "09-", "10-", "11-", "12-", "13-", "14-", "15-", "16-", "17-", "20-" }) R1Place(l, k);

        // коллайдеры (клетки от левой стены / высота верха над полом)
        R1Plate(l, "Mantel", 0, 4.8f, 4.1f);
        R1Plate(l, "ShelfChimney1", 3.1f, 5.1f, 5.5f); R1Plate(l, "ShelfChimney2", 3.1f, 5.1f, 6.8f); R1Plate(l, "ShelfChimney3", 3.1f, 5.1f, 7.8f);
        R1Plate(l, "ShelfHigh", 5.5f, 8.1f, 7.7f); R1Plate(l, "ShelfLong", 8.1f, 10.4f, 6.8f);
        R1Plate(l, "ShelfCandle", 5.4f, 8.8f, 4.5f); R1Plate(l, "ShelfLow", 4.4f, 5.9f, 2.8f); // правый край короче рисунка (6.6): иначе над ящиками нет места для прыжка на спинку кресла
        R1Box(l, "CrateLarge", 5.4f, 7f, 0, 1.1f); R1Box(l, "CrateSmall", 5.9f, 6.7f, 1.1f, 1.5f);
        R1Box(l, "ChairBack", 6.8f, 7.8f, 0, 3.2f); R1Box(l, "ChairSeat", 7.8f, 9.9f, 0, 1.4f);
        // шкаф: верх с проёмом для выхода с полки 5.1; внутренние полки зигзагом
        R1Plate(l, "BookcaseTopL", 10.2f, 12.4f, 6.8f); R1Plate(l, "BookcaseTopR", 13.5f, 14.5f, 6.8f);
        R1Plate(l, "BookcaseShelf1", 10.6f, 13.0f, 1.7f); R1Plate(l, "BookcaseShelf2", 11.8f, 14.1f, 2.8f);
        R1Plate(l, "BookcaseShelf3", 10.6f, 13.0f, 4.0f); R1Plate(l, "BookcaseShelf4", 11.8f, 14.1f, 5.1f);
        R1Box(l, "Clock", 14.5f, 16.5f, 0, 6.6f);
        R1Box(l, "Dresser", 16.7f, 22f, 0, 3.0f); R1Box(l, "JewelryBox", 17.4f, 18.5f, 3.0f, 3.7f); R1Box(l, "Books", 19.1f, 20.5f, 3.0f, 3.7f);
        R1Box(l, "Nightstand", 22.3f, 24.3f, 0, 2.1f);

        // свеча на полке: чекпоинт, огонь и свет движком
        BuildCandle(l, 0, 0, false);
        var cand = l.GetChild(l.childCount - 1);
        cand.position = new Vector3(Px(430), R1Floor + 4.5f, 0);
        Object.DestroyImmediate(cand.Find("Wax").gameObject);
        R1Place(cand, "23-candle");
        cand.Find("Flame").localPosition = Vector3.up * 0.55f; cand.Find("Light").localPosition = Vector3.up * 0.65f;

        // паутина: перекрывает путь с полки со свечой в шкаф под длинной полкой
        var web = Cube("Cobweb", l, new Vector3(1 + 9.3f, R1Floor + 5.25f, 0), new Vector3(0.5f, 2.5f, 0.6f), mWeb);
        web.GetComponent<Renderer>().enabled = false; web.AddComponent<Cobweb>();
        R1Place(web.transform, "24-cobweb");

        // подпитки: низкая полка, кресло, верх шкафа, комод
        R1Paper(l, 4.9f, 2.8f); R1Paper(l, 8.8f, 1.4f); R1Paper(l, 11.2f, 6.8f); R1Paper(l, 21.2f, 3.0f);

        // окно: порыв влево, первый только толкает; штора двух поз, верх у карниза
        BuildWindow(l, 24, 12, 0f, 24f, "WindowFanRoom1", 0f);
        var win = l.Find("WindowFanRoom1");
        Object.DestroyImmediate(win.Find("Pane").gameObject); Object.DestroyImmediate(win.Find("WindowCurtain").gameObject);
        var fan = win.GetComponent<Fan>();
        fan.curtainRest = R1Place(win, "21-curtain-rest"); fan.curtainWind = R1Place(win, "22-curtain-wind"); fan.curtainWind.SetActive(false);
    }
}
