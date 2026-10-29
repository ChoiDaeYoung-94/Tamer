// Editor API audit and visual-only prefab work; never runs production gameplay.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public static class FaceliftEditor
{
    private static Color Ink => new Color32(18, 39, 36, 255);
    private static Color Surface => new Color32(25, 53, 48, 245);
    private static Color Cream => new Color32(246, 241, 221, 255);
    private static Color Gold => new Color32(237, 191, 100, 255);
    private static Color Mint => new Color32(112, 209, 170, 255);
    private static TMP_FontAsset Sans => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/ThirdPartyAssets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
    private static Material PixelMaterial(TMP_FontAsset font)
    {
        const string path = "Assets/Art/Branding/DungGeunMo-Facelift.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(font.material) { name = "DungGeunMo Facelift" };
        if (material.HasProperty(ShaderUtilities.ID_OutlineWidth)) material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0);
        if (material.HasProperty(ShaderUtilities.ID_FaceColor)) material.SetColor(ShaderUtilities.ID_FaceColor, Color.white);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
    private static Sprite Rounded => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    public static readonly string[] Prefabs = {
        "Assets/Prefabs/UI/LoginCanvas.prefab",
        "Assets/Resources/Prefabs/MainAndGameUICanvas/Canvas.prefab",
        "Assets/Resources/Prefabs/JoyStick/Canvas.prefab",
        "Assets/Prefabs/UI/NextSceneCanvas.prefab",
        "Assets/Prefabs/Manager/Manager.prefab",
        "Assets/Prefabs/Main/ShopMan.prefab",
        "Assets/Prefabs/Main/BuffingMan.prefab",
        "Assets/Prefabs/Main/GameMan.prefab" };
    private static string Root => Directory.GetParent(Application.dataPath).FullName;
    [Serializable] private class Report { public List<PrefabInfo> prefabs = new List<PrefabInfo>(); }
    [Serializable] private class PrefabInfo { public string path; public List<Element> elements = new List<Element>(); }
    [Serializable] private class Element {
        public string path, components, text, font, sprite, color, rect;
        public bool active;
        public float fontSize;
        public string[] clicks;
    }
    public static object Audit()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
        var report = new Report();
        foreach (string path in Prefabs)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var info = new PrefabInfo { path = path };
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    var tmp = t.GetComponent<TMP_Text>();
                    var image = t.GetComponent<UnityEngine.UI.Image>();
                    var button = t.GetComponent<UnityEngine.UI.Button>();
                    var rect = t as RectTransform;
                    var graphic = t.GetComponent<UnityEngine.UI.Graphic>();
                    info.elements.Add(new Element { path = PathOf(t, root.transform), active = t.gameObject.activeSelf,
                        components = string.Join(",", t.GetComponents<Component>().Select(c => c == null ? "MISSING" : c.GetType().Name)),
                        text = tmp?.text, font = tmp?.font == null ? null : AssetDatabase.GetAssetPath(tmp.font),
                        fontSize = tmp == null ? 0 : tmp.fontSize,
                        sprite = image?.sprite == null ? null : AssetDatabase.GetAssetPath(image.sprite),
                        color = graphic == null ? null : ColorUtility.ToHtmlStringRGBA(graphic.color),
                        rect = rect == null ? null : $"anchor={rect.anchorMin}/{rect.anchorMax};pos={rect.anchoredPosition};size={rect.sizeDelta}",
                        clicks = button == null ? null : Enumerable.Range(0, button.onClick.GetPersistentEventCount()).Select(i =>
                            button.onClick.GetPersistentTarget(i)?.GetType().Name + "." + button.onClick.GetPersistentMethodName(i)).ToArray() });
                }
                report.prefabs.Add(info);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        string destination = Path.Combine(Root, ".revival-local", "facelift-1029", "prefab-audit.json");
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        File.WriteAllText(destination, Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
        return new { report = destination, prefabs = report.prefabs.Select(p => new { p.path,
            elements = p.elements.Count, text = p.elements.Count(e => e.text != null),
            buttons = p.elements.Count(e => e.clicks != null) }).ToArray() };
    }
    private static string PathOf(Transform t, Transform root)
    {
        return t == root ? t.name : PathOf(t.parent, root) + "/" + t.name;
    }

    public static object PrepareBrandingFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Art/Branding")) AssetDatabase.CreateFolder("Assets/Art", "Branding");
        return new { folder = "Assets/Art/Branding" };
    }

    public static object AuditCharacterScene()
    {
        const string path = "Assets/Scenes/SetCharacter.unity";
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try {
            var result = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Canvas>(true))
                .Select(c => new { path = c.name, text = c.GetComponentsInChildren<TMP_Text>(true).Select(t =>
                    new { path = PathOf(t.transform, c.transform), t.text, t.fontSize, font = AssetDatabase.GetAssetPath(t.font),
                        rect = t.rectTransform.sizeDelta.ToString() }).ToArray(),
                    buttons = c.GetComponentsInChildren<UnityEngine.UI.Button>(true).Select(b => PathOf(b.transform, c.transform)).ToArray() }).ToArray();
            return new { scene = path, canvases = result };
        } finally { EditorSceneManager.CloseScene(scene, true); }
    }

    public static object RenderLoginBefore() => RenderPrefab(Prefabs[0], "login-before", null);
    public static object RenderLoginAfter() => RenderPrefab(Prefabs[0], "login-after", null);
    public static object RenderHudBefore() => RenderPrefab(Prefabs[1], "hud-before", "MainAndGameUICanvasSafeArea/panel_Capture");
    public static object RenderHudAfter() => RenderPrefab(Prefabs[1], "hud-after", "MainAndGameUICanvasSafeArea/panel_Capture");
    public static object RenderLoginNickname() => RenderPrefab(Prefabs[0], "login-nickname-after", "Panel_Nickname");
    public static object RenderSettingsAfter() => RenderPrefab("Assets/Prefabs/Manager/Manager.prefab", "settings-after", "Popup_settings");
    public static object RenderShopAfter() => RenderPrefab("Assets/Prefabs/Main/ShopMan.prefab", "shop-after", "Canvas/Popup_Shop");
    public static object RenderShortShopBefore() => RenderPrefab("Assets/Prefabs/Main/ShopMan.prefab", "shop-short-before", "Canvas/Popup_Shop", 1080, 1440);
    public static object RenderResponsiveShop() => new[] {
        RenderPrefab("Assets/Prefabs/Main/ShopMan.prefab", "shop-responsive-9x16", "Canvas/Popup_Shop", 1080, 1920, true),
        RenderPrefab("Assets/Prefabs/Main/ShopMan.prefab", "shop-responsive-9x20", "Canvas/Popup_Shop", 1080, 2400, true),
        RenderPrefab("Assets/Prefabs/Main/ShopMan.prefab", "shop-responsive-3x4", "Canvas/Popup_Shop", 1080, 1440, true)
    };
    public static object RenderCharacterAfter() => RenderPrefab("Assets/Scenes/SetCharacter.unity", "character-ui-after", null);
    public static object RenderFinalPages()
    {
        return new { login = RenderLoginAfter(), nickname = RenderLoginNickname(), hud = RenderHudAfter(),
            settings = RenderSettingsAfter(), shop = RenderShopAfter(), character = RenderCharacterAfter() };
    }

    private static object RenderPrefab(string path, string label, string activate, int width = 1080, int height = 1920, bool requireShopSpacing = false)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required.");
        var scene = EditorSceneManager.NewPreviewScene();
        RenderTexture target = null;
        Texture2D picture = null;
        var previous = RenderTexture.active;
        try
        {
            GameObject root;
            if (path.EndsWith(".unity")) {
                var sourceScene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try {
                    var canvas = sourceScene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Canvas>(true)).Single();
                    root = UnityEngine.Object.Instantiate(canvas.gameObject);
                    SceneManager.MoveGameObjectToScene(root, scene);
                } finally { EditorSceneManager.CloseScene(sourceScene, true); }
            }
            else root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), scene);
            root.SetActive(false);
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (component != null && component.GetType().Assembly.GetName().Name == "Assembly-CSharp")
                    UnityEngine.Object.DestroyImmediate(component);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            foreach (var t in root.GetComponentsInChildren<RectTransform>(true))
                if (t.name.EndsWith("SafeArea")) { t.anchorMin = Vector2.zero; t.anchorMax = Vector2.one; t.offsetMin = t.offsetMax = Vector2.zero; }
            if (activate != null) root.transform.Find(activate).gameObject.SetActive(true);
            if (path == Prefabs[0] && activate != null) root.transform.Find("Loading").gameObject.SetActive(false);
            var cameraObject = new GameObject("Facelift preview camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>();
            camera.scene = scene;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = path == Prefabs[1] ? new Color32(126, 174, 77, 255) : new Color32(27, 53, 48, 255);
            target = new RenderTexture(width, height, 24);
            camera.targetTexture = target;
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true)) {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
            }
            root.SetActive(true);
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            picture = new Texture2D(width, height, TextureFormat.RGB24, false);
            picture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            picture.Apply();
            var output = Path.Combine(Root, ".revival-local", "facelift-1029", label + ".png");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllBytes(output, picture.EncodeToPNG());
            object shopGeometry = null;
            if (path == "Assets/Prefabs/Main/ShopMan.prefab" && activate == "Canvas/Popup_Shop") {
                Rect Pixels(string child) {
                    var corners = new Vector3[4];
                    Require<RectTransform>(root, "Canvas/Popup_Shop/" + child).GetWorldCorners(corners);
                    Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
                    Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
                    return new Rect(min, max - min);
                }
                Rect close = Pixels("FaceliftClose"), tab = Pixels("Shop_0/Btn_Shop_2"), back = Pixels("Back");
                bool overlap = close.Overlaps(tab);
                bool frameInside = back.xMin >= -.1f && back.yMin >= -.1f && back.xMax <= width + .1f && back.yMax <= height + .1f;
                if (requireShopSpacing && (overlap || !frameInside)) throw new InvalidOperationException("Shop header overlap or frame outside the viewport.");
                shopGeometry = new { close = close.ToString(), tab = tab.ToString(), back = back.ToString(), overlap, frameInside };
            }
            return new { image = output, visualOnly = true, gameplayOrAuthenticationRun = false, width, height, shopGeometry,
                canvasSizes = root.GetComponentsInChildren<Canvas>(true).Select(c => ((RectTransform)c.transform).rect.ToString()).ToArray() };
        }
        finally {
            RenderTexture.active = previous;
            if (picture != null) UnityEngine.Object.DestroyImmediate(picture);
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    public static object ApplyLogin()
    {
        Edit(Prefabs[0], root => {
            var bg = Require<UnityEngine.UI.Image>(root, "BG");
            bg.color = new Color32(205, 215, 205, 255);
            bg.raycastTarget = false;
            var top = Decoration(root.transform, "FaceliftTopPanel", Ink);
            Stretch(top.rectTransform, new Vector2(0, .79f), Vector2.one, Vector2.zero, Vector2.zero);
            top.transform.SetSiblingIndex(1);
            var bottom = Decoration(root.transform, "FaceliftBottomPanel", Ink);
            Stretch(bottom.rectTransform, Vector2.zero, new Vector2(1, .17f), Vector2.zero, Vector2.zero);
            bottom.transform.SetSiblingIndex(2);
            var logo = Require<UnityEngine.UI.Image>(root, "Logo");
            logo.color = Color.clear;
            Position(logo.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -72), new Vector2(920, 260));
            var title = Require<TMP_Text>(root, "Logo/Text (TMP)");
            TextStyle(title, 122, Gold, true);
            title.text = "MONSTER\nTAMER";
            title.characterSpacing = 8;
            title.lineSpacing = -16;
            title.alignment = TextAlignmentOptions.Center;
            var sub = Caption(root.transform, "FaceliftSubtitle", "TAME  /  FIGHT  /  EXPLORE", 26, Cream);
            Position(sub.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -342), new Vector2(920, 44));
            var loading = Require<RectTransform>(root, "Loading");
            Stretch(loading, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 185), new Vector2(0, 250));
            var spinner = Require<UnityEngine.UI.Image>(root, "Loading/circle_half_rotating_fading_7_1");
            spinner.color = Gold;
            Position(spinner.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 18), new Vector2(64, 64));
            var status = Require<TMP_Text>(root, "Loading/TMP_Loading");
            TextStyle(status, 34, Cream, true);
            Position(status.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -62), new Vector2(900, 80));
            var studio = Caption(root.transform, "FaceliftStudio", "QUAD D", 24, Gold);
            studio.characterSpacing = 10;
            Position(studio.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 40), new Vector2(600, 44));
            foreach (string panel in new[] { "Panel_Retry", "Panel_Nickname", "Panel_NicknameRule", "Panel_NicknameConflict" }) {
                var t = Require<RectTransform>(root, panel);
                Position(t, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(900, 590));
                Card(t.gameObject, Surface);
                foreach (var text in t.GetComponentsInChildren<TMP_Text>(true)) TextStyle(text, text.name == "TMP_Contents" ? 38 : 42, Cream, false);
                foreach (var button in t.GetComponentsInChildren<UnityEngine.UI.Button>(true)) {
                    StyleButton(button, Gold, Ink);
                    Position((RectTransform)button.transform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 48), new Vector2(590, 132));
                }
            }
            var input = Require<UnityEngine.UI.Image>(root, "Panel_Nickname/InputField_Nickname");
            Card(input.gameObject, new Color32(227, 235, 224, 255));
            Position(input.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -10), new Vector2(720, 112));
            foreach (var text in input.GetComponentsInChildren<TMP_Text>(true)) TextStyle(text, 36, Ink, false);
            foreach (var text in input.GetComponentsInChildren<TMP_Text>(true)) { text.alignment = TextAlignmentOptions.Left; text.fontStyle = FontStyles.Normal; }
            var nicknameTitle = Require<TMP_Text>(root, "Panel_Nickname/TMP_Content");
            nicknameTitle.text = "Choose your name";
            TextStyle(nicknameTitle, 44, Cream, true);
        });
        return new { loginStyled = true, eventsAndInputReferencesPreserved = true };
    }

    public static object ApplyHud()
    {
        Edit(Prefabs[1], root => {
            const string safe = "MainAndGameUICanvasSafeArea/";
            const string top = safe + "Top_Menu/";
            const string info = top + "panel_playerInfo/";
            Stretch(Require<RectTransform>(root, safe.TrimEnd('/')), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var header = Require<RectTransform>(root, safe + "Top_Menu");
            Stretch(header, new Vector2(0, 1), Vector2.one, new Vector2(0, -132), new Vector2(-56, 216));
            var player = Require<RectTransform>(root, top + "panel_playerInfo");
            Stretch(player, new Vector2(0, 0), new Vector2(1, 1), new Vector2(-84, 0), new Vector2(-168, 0));
            Card(player.gameObject, Surface);
            var nick = Require<TMP_Text>(root, info + "TMP_playerNickName");
            TextStyle(nick, 36, Cream, false);
            Position(nick.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -24), new Vector2(720, 58));
            nick.alignment = TextAlignmentOptions.Left;
            nick.textWrappingMode = TextWrappingModes.NoWrap;
            nick.overflowMode = TextOverflowModes.Ellipsis;
            var count = Require<TMP_Text>(root, info + "TMP_captureCapacity");
            TextStyle(count, 32, Gold, true);
            Position(count.rectTransform, new Vector2(0, 1), new Vector2(0, .5f), new Vector2(28, -150), new Vector2(230, 52));
            count.alignment = TextAlignmentOptions.Left;
            var caption = Caption(player, "FaceliftCompanions", "COMPANIONS", 21, Cream);
            Position(caption.rectTransform, new Vector2(0, 1), new Vector2(0, .5f), new Vector2(28, -103), new Vector2(240, 30));
            caption.alignment = TextAlignmentOptions.Left;
            var hp = Require<RectTransform>(root, info + "Slider_HP");
            Position(hp, new Vector2(0, 1), new Vector2(0, .5f), new Vector2(288, -143), new Vector2(270, 50));
            Card(Require<UnityEngine.UI.Image>(root, info + "Slider_HP/Background").gameObject, Ink);
            Card(Require<UnityEngine.UI.Image>(root, info + "Slider_HP/Fill Area/Fill").gameObject, Mint);
            TextStyle(Require<TMP_Text>(root, info + "Slider_HP/TMP_HP"), 26, Cream, true);
            var gold = Require<TMP_Text>(root, info + "TMP_gold");
            TextStyle(gold, 29, Gold, true);
            Position(gold.rectTransform, new Vector2(0, 1), new Vector2(0, .5f), new Vector2(612, -143), new Vector2(240, 70));
            gold.alignment = TextAlignmentOptions.Left;
            var coin = Require<UnityEngine.UI.Image>(root, info + "Image_gold");
            Position(coin.rectTransform, new Vector2(0, 1), new Vector2(0, .5f), new Vector2(574, -143), new Vector2(32, 32));
            coin.color = Color.white;
            var settings = Require<RectTransform>(root, top + "panel_settings");
            Position(settings, Vector2.one, Vector2.one, Vector2.zero, new Vector2(152, 216));
            Card(settings.gameObject, Surface);
            settings.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            var gear = Require<UnityEngine.UI.Image>(root, top + "panel_settings/Image_setting");
            Position(gear.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(144, 144));
            gear.color = Cream;
            var gearGraphic = Decoration(gear.transform, "FaceliftGear", Cream);
            gearGraphic.sprite = gear.sprite;
            Position(gearGraphic.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(88, 88));
            gear.color = Color.clear;
            gear.GetComponent<UnityEngine.UI.Button>().targetGraphic = gearGraphic;
            var capture = Require<UnityEngine.UI.Button>(root, safe + "panel_Capture/Btn_Capture");
            StyleButton(capture, Gold, Ink);
            Position((RectTransform)capture.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-44, 88), new Vector2(330, 150));
            const string map = top + "panel_gamesceneUI/";
            foreach (string child in new[] { "bg", "MiniMap" })
                Position(Require<RectTransform>(root, map + child), Vector2.one, Vector2.one, new Vector2(0, -260), new Vector2(260, 260));
            Require<UnityEngine.UI.Image>(root, map + "bg").color = Ink;
            foreach (var border in Require<RectTransform>(root, map + "MiniMap/tile").GetComponentsInChildren<UnityEngine.UI.Image>(true)) {
                var size = border.rectTransform.sizeDelta;
                border.rectTransform.sizeDelta = new Vector2(size.x == 0 ? 0 : 6, size.y == 0 ? 0 : 6);
                border.color = Gold; border.raycastTarget = false;
            }
            var recall = Require<UnityEngine.UI.Image>(root, map + "Image_Recall");
            Position(recall.rectTransform, Vector2.one, Vector2.one, new Vector2(0, -548), new Vector2(144, 144));
            recall.color = Cream;
            const string popup = safe + "Popup_playerInfo/Popup_playerInfo";
            Require<UnityEngine.UI.Image>(root, safe + "Popup_playerInfo").color = new Color32(5, 18, 16, 195);
            var panel = Require<RectTransform>(root, popup);
            Position(panel, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(920, 1080));
            Card(panel.gameObject, Surface);
            var title = Require<TMP_Text>(root, popup + "/TMP_status");
            TextStyle(title, 52, Gold, true); title.text = "PLAYER";
            Position(title.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -50), new Vector2(800, 80));
            var names = new[] { "TMP_nickname", "TMP_captureCapacity", "TMP_gold", "TMP_power", "TMP_attackSpeed", "TMP_moveSpeed" };
            for (int i = 0; i < names.Length; i++) {
                var text = Require<TMP_Text>(root, popup + "/" + names[i]);
                TextStyle(text, 38, Cream, false); text.alignment = TextAlignmentOptions.Left;
                Position(text.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(54, -175 - i * 105), new Vector2(812, 80));
            }
            var close = Require<UnityEngine.UI.Button>(root, popup + "/Btn_ok");
            StyleButton(close, Gold, Ink);
            Position((RectTransform)close.transform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 48), new Vector2(600, 130));
        });
        return new { hudStyled = true, buttonMethodsUnchanged = true };
    }

    public static object ApplyPopups()
    {
        Edit("Assets/Prefabs/Manager/Manager.prefab", root => {
            foreach (string name in new[] { "Popup_exit", "Popup_lobby", "Popup_RewardInGame", "Popup_gameOver" }) {
                var backdrop = Require<UnityEngine.UI.Image>(root, name);
                backdrop.sprite = null; backdrop.color = new Color32(5, 18, 16, 195);
                var panel = Require<RectTransform>(root, name + "/" + name);
                Position(panel, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(900, 590));
                Card(panel.gameObject, Surface);
                var message = Require<TMP_Text>(root, name + "/" + name + "/TMP_content");
                TextStyle(message, 40, Cream, false);
                Position(message.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -70), new Vector2(780, 270));
                var buttons = panel.GetComponentsInChildren<UnityEngine.UI.Button>(true).Where(b => b.transform != panel).ToArray();
                for (int i = 0; i < buttons.Length; i++) {
                    bool cancel = buttons[i].name.ToLowerInvariant().Contains("cancel");
                    StyleButton(buttons[i], cancel ? new Color32(53, 82, 72, 255) : Gold, cancel ? Cream : Ink, false);
                    Position((RectTransform)buttons[i].transform, new Vector2(.5f, 0), new Vector2(.5f, 0),
                        new Vector2(buttons.Length == 1 ? 0 : (cancel ? 190 : -190), 52), new Vector2(330, 140));
                    if (cancel) buttons[i].GetComponentInChildren<TMP_Text>(true).text = "Cancel";
                }
            }
            const string settings = "Popup_settings/Popup_settings";
            Require<UnityEngine.UI.Image>(root, "Popup_settings").color = new Color32(5, 18, 16, 195);
            var card = Require<RectTransform>(root, settings);
            Position(card, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(900, 900));
            Card(card.gameObject, Surface);
            var heading = Caption(card, "FaceliftSettingsTitle", "SETTINGS", 48, Gold);
            Position(heading.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -40), new Vector2(780, 80));
            foreach (string child in new[] { "Btn_bgm", "Btn_sfx" }) {
                var icon = Require<UnityEngine.UI.Image>(root, settings + "/" + child);
                icon.color = Cream;
                Position(icon.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(child == "Btn_bgm" ? -185 : 185, 0), new Vector2(168, 168));
                var caption = Caption(card, child + "FaceliftLabel", child == "Btn_bgm" ? "MUSIC" : "SOUND", 26, Cream);
                Position(caption.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(child == "Btn_bgm" ? -185 : 185, -130), new Vector2(260, 50));
            }
            var close = Require<UnityEngine.UI.Button>(root, settings + "/Btn_ok");
            StyleButton(close, Gold, Ink, false);
            Position((RectTransform)close.transform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 52), new Vector2(600, 140));
        });
        return new { settingsAndGamePopupsStyled = true, ageAndPrivacyFontStillSupportsKorean = true };
    }

    public static object ApplyShop()
    {
        Edit("Assets/Prefabs/Main/ShopMan.prefab", root => {
            const string popup = "Canvas/Popup_Shop/";
            var backdrop = Require<UnityEngine.UI.Image>(root, popup.TrimEnd('/'));
            backdrop.sprite = null; backdrop.color = new Color32(5, 18, 16, 195);
            var panel = Require<RectTransform>(root, popup + "Back");
            Stretch(panel, new Vector2(.5f, 0), new Vector2(.5f, 1), Vector2.zero, new Vector2(960, -360));
            Card(panel.gameObject, Surface);
            var title = Require<TMP_Text>(root, popup + "TMP_Shop");
            TextStyle(title, 64, Gold, true); title.text = "SHOP";
            Position(title.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -185), new Vector2(850, 90));
            string[] tabs = { "GEAR", "ALLIES", "NO ADS" };
            for (int group = 0; group < 3; group++) {
                string path = popup + "Shop_" + group;
                var area = Require<RectTransform>(root, path);
                Stretch(area, new Vector2(.08f, 0), new Vector2(.92f, 1), new Vector2(0, -125), new Vector2(0, -710));
                for (int tab = 0; tab < 3; tab++) {
                    var button = Require<UnityEngine.UI.Button>(root, path + "/Btn_Shop_" + tab);
                    StyleButton(button, group == tab ? Gold : new Color32(44, 77, 65, 255), group == tab ? Ink : Cream);
                    Position((RectTransform)button.transform, new Vector2(.5f, 1), new Vector2(.5f, .5f), new Vector2((tab - 1) * 300, 60), new Vector2(280, 120));
                    var label = Caption(button.transform, "FaceliftTabLabel", tabs[tab], 32, group == tab ? Ink : Cream);
                    Stretch(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-24, -16));
                }
                var scroll = Require<UnityEngine.UI.ScrollRect>(root, path + "/Scroll View");
                scroll.horizontal = false; scroll.vertical = true;
                scroll.GetComponent<UnityEngine.UI.Image>().color = Ink;
                var content = scroll.content;
                Stretch(content, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, content.sizeDelta.y));
                content.pivot = new Vector2(.5f, 1);
                var layout = content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
                layout.spacing = 16; layout.padding = new RectOffset(12, 12, 16, 16);
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
                var fitter = content.GetComponent<UnityEngine.UI.ContentSizeFitter>();
                fitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
                foreach (Transform row in content) {
                    var button = row.GetComponent<UnityEngine.UI.Button>();
                    if (button == null) continue;
                    Card(row.gameObject, new Color32(36, 68, 58, 255));
                    var size = row.GetComponent<UnityEngine.UI.LayoutElement>() ?? row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                    size.minHeight = size.preferredHeight = 200;
                    var text = row.GetComponentInChildren<TMP_Text>(true);
                    if (text != null) {
                        TextStyle(text, 36, Cream, false); text.alignment = TextAlignmentOptions.Left;
                        Stretch(text.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(115, 0), new Vector2(-270, -24));
                        if (group == 2) text.text = "Remove Ads\nPrice shown in Google Play";
                    }
                }
            }
            foreach (string name in new[] { "Popup_itemInfo", "Popup_AfterBuy" }) {
                Require<UnityEngine.UI.Image>(root, "Canvas/" + name).color = new Color32(5, 18, 16, 195);
                var card = Require<RectTransform>(root, "Canvas/" + name + "/" + name);
                Position(card, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero,
                    new Vector2(920, name == "Popup_itemInfo" ? 1200 : 590));
                Card(card.gameObject, Surface);
                foreach (var text in card.GetComponentsInChildren<TMP_Text>(true)) TextStyle(text, 38, Cream, false);
                var buttons = card.GetComponentsInChildren<UnityEngine.UI.Button>(true).Where(b => b.transform != card).ToArray();
                for (int i = 0; i < buttons.Length; i++) {
                    bool cancel = buttons[i].name == "Btn_no";
                    StyleButton(buttons[i], cancel ? new Color32(53, 82, 72, 255) : Gold, cancel ? Cream : Ink);
                    Position((RectTransform)buttons[i].transform, new Vector2(.5f, 0), new Vector2(.5f, 0),
                        new Vector2(buttons.Length == 1 ? 0 : (cancel ? 190 : -190), 48), new Vector2(330, 140));
                }
            }
            var info = Require<TMP_Text>(root, "Canvas/Popup_itemInfo/Popup_itemInfo/TMP_itemInfo");
            Position(info.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -60), new Vector2(800, 690));
            var prompt = Require<TMP_Text>(root, "Canvas/Popup_itemInfo/Popup_itemInfo/TMP_content");
            Position(prompt.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 240), new Vector2(800, 170));
            var result = Require<TMP_Text>(root, "Canvas/Popup_AfterBuy/Popup_AfterBuy/TMP_content");
            Position(result.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -70), new Vector2(800, 270));
            var closeImage = Decoration(Require<RectTransform>(root, popup.TrimEnd('/')), "FaceliftClose", new Color32(44, 77, 65, 255));
            var close = closeImage.GetComponent<UnityEngine.UI.Button>() ?? closeImage.gameObject.AddComponent<UnityEngine.UI.Button>();
            StyleButton(close, new Color32(44, 77, 65, 255), Cream);
            closeImage.raycastTarget = true;
            Position(closeImage.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-100, -205), new Vector2(144, 144));
            var cross = Caption(close.transform, "FaceliftCloseLabel", "X", 42, Cream);
            Stretch(cross.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            if (close.onClick.GetPersistentEventCount() == 0) {
                var type = Type.GetType("AD.PopupObject, Assembly-CSharp", true);
                var target = Require<RectTransform>(root, popup.TrimEnd('/')).GetComponent(type);
                var action = (UnityEngine.Events.UnityAction)Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), target, type.GetMethod("DisablePop", Type.EmptyTypes));
                UnityEditor.Events.UnityEventTools.AddPersistentListener(close.onClick, action);
            }
        });
        return new { shopStyled = true, catalogAndPurchaseEventsUnchanged = true, hardcodedIapPriceRemoved = true };
    }

    public static object ApplyResponsiveShop()
    {
        Edit("Assets/Prefabs/Main/ShopMan.prefab", root => {
            const string popup = "Canvas/Popup_Shop/";
            // Retain the 1920-high design while keeping the header and close button
            // apart on shorter portrait displays. Only the scrollable body shrinks.
            Stretch(Require<RectTransform>(root, popup + "Back"), new Vector2(.5f, 0), new Vector2(.5f, 1),
                Vector2.zero, new Vector2(960, -360));
            for (int group = 0; group < 3; group++)
                Stretch(Require<RectTransform>(root, popup + "Shop_" + group), new Vector2(.08f, 0), new Vector2(.92f, 1),
                    new Vector2(0, -125), new Vector2(0, -710));
        });
        return new { headerSpacingFixed = true, scrollAreaResizes = true, purchaseEventsUnchanged = true };
    }

    public static object InspectCharacterAndIconApis()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SetCharacter.unity", OpenSceneMode.Additive);
        try {
            var canvas = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Canvas>(true)).Single();
            return new { elements = canvas.GetComponentsInChildren<RectTransform>(true).Select(t => new {
                path = PathOf(t, canvas.transform), position = t.anchoredPosition.ToString(), size = t.sizeDelta.ToString(),
                components = string.Join(",", t.GetComponents<Component>().Select(c => c?.GetType().Name)),
                sprite = SpriteName(t) }).ToArray(),
                iconMethods = typeof(PlayerSettings).GetMethods().Where(m => m.Name.Contains("PlatformIcon") || m.Name == "SetIconsForTargetGroup").Select(m => m.ToString()).ToArray(),
                iconKinds = typeof(UnityEditor.Android.AndroidPlatformIconKind).GetFields().Select(f => f.Name).ToArray() };
        } finally { EditorSceneManager.CloseScene(scene, true); }
    }
    private static string SpriteName(Transform t)
    {
        var image = t.GetComponent<UnityEngine.UI.Image>();
        return image == null || image.sprite == null ? null : image.sprite.name;
    }

    public static object ApplyCharacter()
    {
        const string path = "Assets/Scenes/SetCharacter.unity";
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try {
            var canvas = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Canvas>(true)).Single().gameObject;
            var safe = Require<RectTransform>(canvas, "PanelSafeArea");
            Stretch(safe, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var top = Require<RectTransform>(canvas, "PanelSafeArea/PanelTopText");
            Stretch(top, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -140), new Vector2(0, 280));
            Card(top.gameObject, Surface);
            var heading = Require<TMP_Text>(canvas, "PanelSafeArea/PanelTopText/Text (TMP)");
            TextStyle(heading, 56, Gold, true); heading.text = "CHOOSE YOUR TAMER";
            Stretch(heading.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-96, -80));
            foreach (string name in new[] { "ButtonLeft", "ButtonRight" }) {
                var button = Require<UnityEngine.UI.Button>(canvas, "PanelSafeArea/" + name);
                StyleButton(button, Surface, Cream);
                Position((RectTransform)button.transform, new Vector2(name == "ButtonLeft" ? 0 : 1, .5f),
                    new Vector2(name == "ButtonLeft" ? 0 : 1, .5f), new Vector2(name == "ButtonLeft" ? 30 : -30, 0), new Vector2(180, 220));
                var arrow = Require<UnityEngine.UI.Image>(canvas, "PanelSafeArea/" + name + "/Image");
                Position(arrow.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(96, 96));
                arrow.color = Gold; arrow.raycastTarget = false;
            }
            var play = Require<UnityEngine.UI.Button>(canvas, "PanelSafeArea/ButtonPlay");
            StyleButton(play, Gold, Ink);
            Position((RectTransform)play.transform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 80), new Vector2(780, 156));
            play.GetComponentInChildren<TMP_Text>(true).text = "BEGIN ADVENTURE";
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Character UI was not saved.");
            return new { characterUiStyled = true, characterSelectionEventsUnchanged = true };
        } finally { EditorSceneManager.CloseScene(scene, true); }
    }

    public static object ApplySecondaryUi()
    {
        Edit(Prefabs[2], root => {
            var ring = Require<UnityEngine.UI.Image>(root, "JoyStickSafeArea/Img_handleArea");
            ring.color = new Color32(112, 209, 170, 130);
            Require<UnityEngine.UI.Image>(root, "JoyStickSafeArea/Img_handleArea/Img_handle").color = new Color32(246, 241, 221, 220);
        });
        Edit(Prefabs[3], root => {
            Require<UnityEngine.UI.Image>(root, "BG").color = Ink;
            var spinner = Require<UnityEngine.UI.Image>(root, "Loading/circle_half_rotating_fading_7_1");
            spinner.color = Gold;
            Position(spinner.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(80, 80));
            var text = Require<TMP_Text>(root, "Loading/TMP_Loading");
            TextStyle(text, 36, Cream, true);
            Position(text.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -100), new Vector2(900, 100));
        });
        return new { joystickAndTransitionStyled = true };
    }

    public static object InspectIconLayers()
    {
        var kind = UnityEditor.Android.AndroidPlatformIconKind.Adaptive;
        var property = typeof(PlatformIconKind).GetProperty("customLayerLabels", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return new { labels = property?.GetValue(kind),
            methods = typeof(PlatformIcon).GetMethods().Where(m => m.Name.Contains("Texture")).Select(m => m.ToString()).ToArray() };
    }

    public static object ConfigureIcons()
    {
        const string squarePath = "Assets/Art/Branding/app-icon-facelift-20261029.png";
        const string foregroundPath = "Assets/Art/Branding/app-icon-adaptive-foreground-20261029.png";
        const string backgroundPath = "Assets/Art/Branding/app-icon-adaptive-background-20261029.png";
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required.");
        if (!File.Exists(Path.Combine(Root, backgroundPath))) {
            var solid = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            try { solid.SetPixel(0, 0, Ink); solid.Apply(); File.WriteAllBytes(Path.Combine(Root, backgroundPath), solid.EncodeToPNG()); }
            finally { UnityEngine.Object.DestroyImmediate(solid); }
        }
        foreach (string path in new[] { squarePath, foregroundPath, backgroundPath }) {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing icon texture: " + path);
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.alphaIsTransparency = path == foregroundPath;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }
        var square = AssetDatabase.LoadAssetAtPath<Texture2D>(squarePath);
        var foreground = AssetDatabase.LoadAssetAtPath<Texture2D>(foregroundPath);
        var background = AssetDatabase.LoadAssetAtPath<Texture2D>(backgroundPath);
        PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { square }, IconKind.Any);
        var platform = UnityEditor.Build.NamedBuildTarget.Android;
        foreach (var kind in new[] { UnityEditor.Android.AndroidPlatformIconKind.Legacy, UnityEditor.Android.AndroidPlatformIconKind.Round }) {
            var slots = PlayerSettings.GetPlatformIcons(platform, kind);
            if (slots.Length == 0) throw new InvalidOperationException("Android icon slots are missing.");
            foreach (var slot in slots) slot.SetTexture(square);
            PlayerSettings.SetPlatformIcons(platform, kind, slots);
        }
        var adaptive = UnityEditor.Android.AndroidPlatformIconKind.Adaptive;
        var labels = (string[])typeof(PlatformIconKind).GetProperty("customLayerLabels", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(adaptive);
        if (labels == null || labels.Length != 2 || !labels.Contains("Foreground") || !labels.Contains("Background"))
            throw new InvalidOperationException("Unrecognized adaptive layer order.");
        var icons = PlayerSettings.GetPlatformIcons(platform, adaptive);
        foreach (var icon in icons) icon.SetTextures(labels.Select(l => l == "Foreground" ? foreground : background).ToArray());
        PlayerSettings.SetPlatformIcons(platform, adaptive, icons);
        AssetDatabase.SaveAssets();
        return new { defaultIcon = squarePath, adaptiveForeground = foregroundPath, adaptiveBackground = backgroundPath,
            adaptiveLabels = labels, adaptiveSlots = icons.Length };
    }

    private static void Edit(string path, Action<GameObject> edit)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required.");
        var root = PrefabUtility.LoadPrefabContents(path);
        try { edit(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static T Require<T>(GameObject root, string path) where T : Component
    {
        var target = root.transform.Find(path);
        var component = target == null ? null : target.GetComponent<T>();
        if (component == null) throw new InvalidOperationException("Missing UI target: " + path + "/" + typeof(T).Name);
        return component;
    }
    private static void Position(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
    {
        rect.anchorMin = min; rect.anchorMax = max; rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position; rect.sizeDelta = size;
    }
    private static void TextStyle(TMP_Text text, float size, Color color, bool sans)
    {
        if (sans) { if (Sans == null) throw new InvalidOperationException("Existing sans font is missing."); text.font = Sans; text.fontSharedMaterial = Sans.material; }
        else if (AssetDatabase.GetAssetPath(text.font) == "Assets/Fonts/DungGeunMo SDF.asset") text.fontSharedMaterial = PixelMaterial(text.font);
        text.color = color; text.fontSize = size; text.fontSizeMax = size; text.fontSizeMin = size * .8f;
        text.enableAutoSizing = true; text.raycastTarget = false;
    }
    private static UnityEngine.UI.Image Decoration(Transform parent, string name, Color color)
    {
        var t = parent.Find(name);
        if (t == null) { t = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)).transform; t.SetParent(parent, false); }
        var image = t.GetComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = false; return image;
    }
    private static TMP_Text Caption(Transform parent, string name, string value, float size, Color color)
    {
        var t = parent.Find(name);
        if (t == null) { t = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).transform; t.SetParent(parent, false); }
        var text = t.GetComponent<TMP_Text>(); text.text = value; TextStyle(text, size, color, true);
        text.alignment = TextAlignmentOptions.Center; return text;
    }
    private static void Card(GameObject target, Color color)
    {
        var image = target.GetComponent<UnityEngine.UI.Image>() ?? target.AddComponent<UnityEngine.UI.Image>();
        image.sprite = Rounded; image.type = UnityEngine.UI.Image.Type.Sliced; image.color = color;
    }
    private static void StyleButton(UnityEngine.UI.Button button, Color background, Color label, bool sans = true)
    {
        Card(button.gameObject, background);
        button.targetGraphic = button.GetComponent<UnityEngine.UI.Image>();
        button.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
        var colors = button.colors; colors.normalColor = Color.white;
        colors.highlightedColor = new Color(.92f, 1, .95f); colors.pressedColor = new Color(.76f, .86f, .81f);
        colors.disabledColor = new Color(.55f, .55f, .55f, .65f); button.colors = colors;
        foreach (var text in button.GetComponentsInChildren<TMP_Text>(true)) TextStyle(text, 42, label, sans);
    }
}
