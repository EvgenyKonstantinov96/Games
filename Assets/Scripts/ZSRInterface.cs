using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace ZombieSurvivalRun
{
    /// <summary>Unity Canvas UI. Menu wallpaper is fullscreen; gameplay 3D is never obscured by a solid overlay.</summary>
    public sealed class ZSRInterface : MonoBehaviour
    {
        static readonly Color Dark = new Color(.020f, .031f, .047f, 1);
        static readonly Color Panel = new Color(.062f, .092f, .123f, .95f);
        static readonly Color PanelLight = new Color(.11f, .15f, .19f, .98f);
        static readonly Color Orange = new Color(1f, .56f, .14f, 1);
        static readonly Color Gold = new Color(1f, .80f, .35f, 1);
        static readonly Color Cyan = new Color(.34f, .77f, .93f, 1);
        static readonly Color TextMuted = new Color(.64f, .75f, .80f, 1);
        static readonly Color Green = new Color(.26f, .83f, .39f, 1);
        const float W=390f;

        Canvas canvas;
        RectTransform root, content, background;
        Font font;
        Texture2D artHero, artMax, artEva, artRifle, menuWallpaper, sectionWallpaper;
        readonly Texture2D[] weaponArts = new Texture2D[4];
        Sprite primarySprite, secondarySprite, mutedSprite, panelSprite;
        readonly Texture2D[] levels = new Texture2D[5];
        ZSRState lastState = (ZSRState)(-1);
        int lastCoins = -1, lastLevel = -1, lastHero = -1, lastWeapon = -1;
        float lastHeight, lastWidth;
        UnityEngine.Rect lastSafeArea;
        bool refresh;
        const float H = 844f;
        Text hudAmmo, hudCoins, hudHealth, hudLevel, hudMessage, hudGrenade, hudMed, hudDrone;
        RectTransform hpFill, progressFill, bossFill, bossBackdrop;
        Text hudBoss;

        void Awake()
        {
            artHero = Resources.Load<Texture2D>("Artwork/hero_scene");
            artMax = Resources.Load<Texture2D>("Artwork/max");
            artEva = Resources.Load<Texture2D>("Artwork/eva");
            artRifle = Resources.Load<Texture2D>("Artwork/rifle");
            string[] weaponFiles = { "pistol", "rifle", "shotgun", "minigun" };
            for (int i=0; i<weaponFiles.Length; i++)
                weaponArts[i] = Resources.Load<Texture2D>("Artwork/Weapons/" + weaponFiles[i]);
            menuWallpaper = Resources.Load<Texture2D>("UI/menu_wallpaper");
            sectionWallpaper = Resources.Load<Texture2D>("UI/section_wallpaper");
            primarySprite = MakeSlicedSprite("UI/btn_primary");
            secondarySprite = MakeSlicedSprite("UI/btn_secondary");
            mutedSprite = MakeSlicedSprite("UI/btn_muted");
            panelSprite = MakeSlicedSprite("UI/panel");
            for (int i = 0; i < 5; i++) levels[i] = Resources.Load<Texture2D>("Artwork/level_" + (i + 1));
            font = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial", "Roboto", "Noto Sans", "DejaVu Sans" }, 24);
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var eventSystem = FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                var go = new GameObject("Mobile UI EventSystem");
                go.AddComponent<EventSystem>();
                go.AddComponent<InputSystemUIInputModule>();
            }
            else if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            {
                var old = eventSystem.GetComponent<StandaloneInputModule>();
                if (old != null) old.enabled = false;
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }

            var r = new GameObject("ZSR Premium UI - Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            r.transform.SetParent(transform, false);
            canvas = r.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = r.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390, 844);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            root = r.GetComponent<RectTransform>();
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero; root.offsetMax = Vector2.zero;
        }

        static Sprite MakeSlicedSprite(string resourcePath)
        {
            Texture2D tex = Resources.Load<Texture2D>(resourcePath);
            if (tex == null) return null;
            return Sprite.Create(tex, new UnityEngine.Rect(0, 0, tex.width, tex.height),
                new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(20f, 20f, 20f, 20f));
        }

        void Start() { Draw(); }
        void Update()
        {
            var g = ZSRGame.Instance; if (g == null || root == null) return;
            float hh = root.rect.height;
            if (refresh || lastState != g.State || (g.State != ZSRState.Playing && lastCoins != g.Coins) || lastLevel != g.Level || lastHero != g.SelectedHero || lastWeapon != (int)g.Weapon || Mathf.Abs(hh - lastHeight) > 1.5f || Mathf.Abs(root.rect.width - lastWidth) > 1.5f || Screen.safeArea != lastSafeArea)
                Draw();
            if (g.State == ZSRState.Playing || g.State == ZSRState.Paused) UpdateHUD(g);
        }
        void Invalidate() { refresh = true; }

        RectTransform Rect(string n, Transform p, float x, float y, float w, float h)
        {
            var go = new GameObject(n, typeof(RectTransform));
            go.transform.SetParent(p, false);
            var t = go.GetComponent<RectTransform>();
            t.anchorMin = t.anchorMax = new Vector2(0, 1);
            t.pivot = new Vector2(0, 1);
            t.anchoredPosition = new Vector2(x, -y);
            t.sizeDelta = new Vector2(w, h);
            return t;
        }

        RectTransform RectStretch(string n, Transform p)
        {
            var go = new GameObject(n, typeof(RectTransform));
            go.transform.SetParent(p, false);
            var t = go.GetComponent<RectTransform>();
            t.anchorMin = Vector2.zero;
            t.anchorMax = Vector2.one;
            t.offsetMin = Vector2.zero;
            t.offsetMax = Vector2.zero;
            return t;
        }

        RectTransform Block(string n, float x, float y, float w, float h, Color color, Transform p = null)
        {
            var t = Rect(n, p == null ? content : p, x, y, w, h);
            var image = t.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color; image.raycastTarget = false;
            if (panelSprite != null &&
                (n.Contains("card") || n.Contains("slot") || n.Contains("panel") || n.Contains("resources") || n.Contains("weapon showcase") || n.Contains("soft shadow")))
            {
                image.sprite = panelSprite;
                image.type = UnityEngine.UI.Image.Type.Sliced;
                image.color = new Color(1f, 1f, 1f, color.a);
            }
            return t;
        }

        RectTransform BlockStretch(string n, Color color, Transform p)
        {
            var t = RectStretch(n, p);
            var image = t.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color; image.raycastTarget = false;
            return t;
        }

        void Accent(float x, float y, float w, Color c) { Block("accent", x, y, w, 3, c); }

        Text Label(string s, float x, float y, float w, float h, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter, bool bold = true, Transform parent = null)
        {
            var t = Rect("TEXT: " + s, parent == null ? content : parent, x, y, w, h);
            var label = t.gameObject.AddComponent<Text>();
            label.font = font;
            label.text = s;
            label.fontSize = size;
            label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            label.color = color;
            label.alignment = align;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = false;
            label.raycastTarget = false;
            return label;
        }

        void Image(Texture2D tex, float x, float y, float w, float h, bool crop = true, Transform parent = null, float alpha = 1f)
        {
            if (tex == null) return;
            var rt = Rect("painted game art", parent == null ? content : parent, x, y, w, h);
            var im = rt.gameObject.AddComponent<RawImage>();
            im.texture = tex; im.raycastTarget = false; im.color = new Color(1, 1, 1, alpha);
            if (crop)
            {
                float dst = w / h, src = tex.width / (float)tex.height;
                if (src > dst) { float u = dst / src; im.uvRect = new UnityEngine.Rect((1 - u) * .5f, 0, u, 1); }
                else { float v = src / dst; im.uvRect = new UnityEngine.Rect(0, (1 - v) * .5f, 1, v); }
            }
        }

        void StretchImage(Texture2D tex, Transform parent, float alpha, bool crop = true)
        {
            if (tex == null) return;
            var rt = RectStretch("background art", parent);
            var im = rt.gameObject.AddComponent<RawImage>();
            im.texture = tex; im.raycastTarget = false; im.color = new Color(1, 1, 1, alpha);
            if (crop)
            {
                float dst = Mathf.Max(1f, root.rect.width) / Mathf.Max(1f, root.rect.height);
                float src = tex.width / (float)tex.height;
                if (src > dst)
                {
                    float u = dst / src;
                    im.uvRect = new Rect((1 - u) * .5f, 0, u, 1);
                }
                else
                {
                    float v = src / dst;
                    im.uvRect = new Rect(0, (1 - v) * .5f, 1, v);
                }
            }
        }

        void Button(string txt, float x, float y, float w, float h, Color accent, Action callback, int textSize = 16)
        {
            var t = Rect("BUTTON: " + txt, content, x, y, w, h);
            var img = t.gameObject.AddComponent<UnityEngine.UI.Image>();
            Sprite sprite = accent.r > .80f && accent.g < .70f ? primarySprite :
                            accent.b > .70f && accent.g > .55f ? secondarySprite : mutedSprite;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = UnityEngine.UI.Image.Type.Sliced;
                img.color = Color.white;
            }
            else img.color = new Color(.09f, .14f, .19f, .98f);
            img.raycastTarget = true;
            var button = t.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.16f, 1.16f, 1.16f, 1);
            colors.pressedColor = new Color(.80f, .80f, .80f, 1);
            colors.selectedColor = Color.white;
            button.colors = colors;
            button.onClick.AddListener(() => { if (callback != null) callback.Invoke(); Invalidate(); });
            Label(txt, 5, 4, w - 10, h - 8, textSize,
                accent.r > .80f && accent.g < .70f ? Color.white : Color.white,
                TextAnchor.MiddleCenter, true, t);
        }

        void TitleBar(string name, ZSRGame g)
        {
            Block("title backdrop", 0, 0, W, 95, new Color(.015f, .025f, .038f, .94f));
            Accent(18, 87, 354, Orange);
            Button("<", 14, 17, 43, 48, Cyan, () => g.GoMenu(), 24);
            Label(name, 65, 15, 307, 52, 24, Color.white);
        }

        void FooterBack(ZSRGame g)
        {
            Button("В МЕНЮ", 27, H - 84, 336, 58, Cyan, () => g.GoMenu(), 19);
        }

        void Draw()
        {
            var g = ZSRGame.Instance;
            if (g == null || root == null) return;
            lastSafeArea = Screen.safeArea;
            lastHeight = root.rect.height; lastWidth = root.rect.width; lastState = g.State;
            lastCoins = g.Coins; lastLevel = g.Level; lastHero = g.SelectedHero; lastWeapon = (int)g.Weapon;
            refresh = false;
            if (background != null) { Destroy(background.gameObject); background = null; }
            if (content != null) { Destroy(content.gameObject); content = null; }
            bool inBattle = g.State == ZSRState.Playing || g.State == ZSRState.Paused ||
                            g.State == ZSRState.Victory || g.State == ZSRState.Defeat;
            // CRITICAL: No fullscreen background exists during 3D combat.
            // Previous builds hid the whole 3D scene beneath an opaque overlay Canvas.
            if (!inBattle)
            {
                background = RectStretch("MENU BACKGROUND ONLY", root);
                BuildAdaptiveBackground(g);
            }
            // Fit the entire interactive artboard on unusually narrow phones.
            float unitsX = root.rect.width / Mathf.Max(1, Screen.width);
            float unitsY = root.rect.height / Mathf.Max(1, Screen.height);
            float safeWidth = lastSafeArea.width * unitsX;
            float safeHeight = lastSafeArea.height * unitsY;
            float contentScale = Mathf.Min(1f, Mathf.Min(safeWidth / W, safeHeight / H));
            content = Rect("CURRENT SCREEN", root,
                lastSafeArea.xMin * unitsX + (safeWidth - W * contentScale) * .5f,
                (Screen.height - lastSafeArea.yMax) * unitsY + (safeHeight - H * contentScale) * .5f,
                W, H);
            content.localScale = Vector3.one * contentScale;

            hudAmmo = hudCoins = hudHealth = hudLevel = hudMessage = hudGrenade = hudMed = hudDrone = null;
            hpFill = progressFill = bossFill = bossBackdrop = null; hudBoss = null;
            if (g.State == ZSRState.Playing || g.State == ZSRState.Paused || g.State == ZSRState.Victory || g.State == ZSRState.Defeat)
            {
                DrawHUD(g);
                if (g.State == ZSRState.Paused) PauseScreen(g);
                if (g.State == ZSRState.Victory || g.State == ZSRState.Defeat) ResultScreen(g, g.State == ZSRState.Victory);
                return;
            }
            switch (g.State)
            {
                case ZSRState.Menu: Menu(g); break;
                case ZSRState.Heroes: Heroes(g); break;
                case ZSRState.Arsenal: Arsenal(g); break;
                case ZSRState.Items: Items(g); break;
                case ZSRState.Bonuses: Bonuses(g); break;
                case ZSRState.Levels: Levels(g); break;
            }
        }

        void BuildAdaptiveBackground(ZSRGame g)
        {
            BlockStretch("fullscreen cinematic base", Dark, background);
            // Portrait artwork fills the complete Game viewport, not a central square.
            StretchImage(g.State == ZSRState.Menu ? menuWallpaper : sectionWallpaper,
                background, 1f, true);
            // Soft translucent panel to keep secondary menus readable.
            if (g.State != ZSRState.Menu)
                BlockStretch("screen contrast wash", new Color(.01f, .025f, .04f, .24f), background);
        }

        void Currency(ZSRGame g)
        {
            Block("resources", 123, 8, 252, 43, new Color(.04f, .065f, .095f, .97f));
            Accent(123, 8, 252, Cyan);
            Label("● " + g.Coins.ToString("N0"), 132, 11, 142, 34, 17, Gold);
            Label("◆ " + g.UnlockedLevel + "/15", 260, 11, 107, 34, 14, Cyan);
        }

        void Menu(ZSRGame g)
        {
            Currency(g);
            Button("БОНУСЫ", 12, 8, 94, 45, Cyan, () => g.GoBonuses(), 11);
            Label("ZOMBIE SURVIVAL", 16, 61, 358, 54, 30, Color.white);
            Label("RUN", 28, 107, 334, 62, 44, new Color(1, .34f, .17f));
            Label("МЕГАПОЛИС ПОСЛЕДНЕГО РУБЕЖА", 10, 153, 370, 26, 12, TextMuted);
            // Characters are part of the full-height wallpaper, with no clipped middle rectangle.
            Block("level banner", 27, H - 232, 336, 34, new Color(.025f, .04f, .055f, .83f));
            Label("УРОВЕНЬ " + g.Level + "/15  •  " + g.LevelTitle, 29, H - 231, 332, 31, 12, Color.white);
            Button("▶   ИГРАТЬ", 19, H - 192, 352, 65, Orange, () => g.StartLevel(g.Level), 28);
            float y = H - 107;
            Button("ГЕРОИ", 12, y, 86, 72, Cyan, () => g.GoHeroes(), 12);
            Button("АРСЕНАЛ", 105, y, 86, 72, Orange, () => g.GoArsenal(), 11);
            Button("ПРЕДМЕТЫ", 198, y, 86, 72, Cyan, () => g.GoItems(), 10);
            Button("УРОВНИ", 291, y, 86, 72, Cyan, () => g.GoLevels(), 12);
        }

        void Heroes(ZSRGame g)
        {
            TitleBar("ПЕРСОНАЖИ", g);
            float top = 116;
            int left = 13, right = 202;
            var selected = g.SelectedHero;
            Block("max card", left, top, 175, 310, selected == 0 ? new Color(.23f, .13f, .08f, 1) : Panel);
            Block("eva card", right, top, 175, 310, selected == 1 ? new Color(.23f, .13f, .08f, 1) : Panel);
            Accent(left, top, 175, selected == 0 ? Orange : Cyan);
            Accent(right, top, 175, selected == 1 ? Orange : Cyan);
            Image(artMax, left + 3, top + 4, 169, 243, false);
            Image(artEva, right + 3, top + 4, 169, 243, false);
            Label("МАКС", left + 5, top + 246, 165, 29, 23, Color.white);
            Label("ШТУРМОВИК • ПРОЧНОСТЬ", left + 5, top + 275, 165, 23, 11, TextMuted);
            Label("ЕВА", right + 5, top + 246, 165, 29, 23, Color.white);
            Label("РАЗВЕДЧИЦА • СКОРОСТЬ", right + 5, top + 275, 165, 23, 11, TextMuted);
            Button(selected == 0 ? "✓ ВЫБРАН" : "ВЫБРАТЬ", left, top + 324, 175, 53, selected == 0 ? Orange : Cyan, () => g.SelectHero(0), 16);
            Button(selected == 1 ? "✓ ВЫБРАН" : "ВЫБРАТЬ", right, top + 324, 175, 53, selected == 1 ? Orange : Cyan, () => g.SelectHero(1), 16);
            float s = top + 399;
            Block("hero stats", 14, s, 362, 126, Panel);
            Accent(14, s, 362, Cyan);
            Label("ХАРАКТЕРИСТИКИ", 25, s + 8, 340, 30, 18, Color.white);
            if (selected == 0)
            {
                Label("Здоровье 132     •     Лучше держит урон", 20, s + 42, 350, 29, 15, TextMuted);
                Label("Макс — силовой боец с тяжёлым силуэтом", 20, s + 73, 350, 32, 14, Gold);
            }
            else
            {
                Label("Здоровье 112     •     Быстрее смещается по трассе", 20, s + 42, 350, 29, 15, TextMuted);
                Label("Ева — мобильный герой для агрессивной игры", 20, s + 73, 350, 32, 14, Gold);
            }
            FooterBack(g);
        }

        void Arsenal(ZSRGame g)
        {
            TitleBar("АРСЕНАЛ", g);
            Label("ОРУЖИЕ И ПРОКАЧКА", 18, 108, 216, 24, 13, TextMuted, TextAnchor.MiddleLeft);
            Label("● " + g.Coins.ToString("N0"), 235, 108, 136, 24, 14, Gold);
            WeaponType[] list = { WeaponType.Pistol, WeaponType.Rifle, WeaponType.Shotgun, WeaponType.Minigun };
            string[] names = { "ПИСТОЛЕТ", "АВТОМАТ", "ДРОБОВИК", "ПУЛЕМЁТ" };
            for (int i = 0; i < 4; i++)
            {
                float x = 14 + i * 93;
                WeaponType chosenWeapon = list[i]; // Capture value, never loop index, for UI callbacks.
                bool active = g.Weapon == chosenWeapon;
                bool open = g.WeaponLevelUnlocked(chosenWeapon);
                bool owned = g.WeaponOwned(list[i]);
                Block("weapon slot", x, 140, 83, 112, active ? new Color(.28f, .18f, .075f, 1) : PanelLight);
                Accent(x, 140, 83, active ? Orange : (open ? Cyan : TextMuted));
                Texture2D slotArt = weaponArts[i];
                if (slotArt != null) Image(slotArt, x + 3, 147, 77, 50, false, null, open ? 1f : .50f);
                Label(names[i], x + 2, 195, 79, 22, 10, Color.white);
                Label(open ? (owned ? "В НАЛИЧИИ" : "● " + g.WeaponPrice(list[i])) : ("УРОВ. " + g.WeaponUnlockLevel(list[i])),
                    x + 2, 218, 79, 21, 10, open ? Gold : TextMuted);
                string btn = active ? "✓" : open ? (owned ? "ВЫБРАТЬ" : "КУПИТЬ") : "ЗАКРЫТО";
                Button(btn, x, 242, 83, 39, active ? Orange : (open ? Cyan : TextMuted), () => g.SelectWeapon(chosenWeapon), 10);
            }
            float artTop = 303;
            Block("weapon showcase", 13, artTop, 364, 178, Panel);
            Accent(13, artTop, 364, Orange);
            Texture2D selectedArt = weaponArts[(int)g.Weapon];
            if (selectedArt == null && g.Weapon == WeaponType.Rifle) selectedArt = artRifle;
            if (selectedArt != null) Image(selectedArt, 24, artTop + 13, 342, 126, false);
            Label("ВЫБРАНО: " + names[(int)g.Weapon], 24, artTop + 140, 340, 30, 18, Gold);
            float statTop = artTop + 191;
            Block("gun stats", 13, statTop, 364, 115, Panel);
            Label("УРОВЕНЬ ОРУЖИЯ  " + g.WeaponLevel + " / 5", 21, statTop + 8, 348, 31, 18, Gold);
            int baseDamage = g.Weapon == WeaponType.Pistol ? 27 : g.Weapon == WeaponType.Rifle ? 22 :
                g.Weapon == WeaponType.Shotgun ? 55 : 13;
            string fireRate = g.Weapon == WeaponType.Pistol ? "3.0" : g.Weapon == WeaponType.Rifle ? "7.7" :
                g.Weapon == WeaponType.Shotgun ? "1.8" : "15.4";
            Label("УРОН " + (baseDamage + g.WeaponLevel * 5) + "   •   ВЫСТР/С " + fireRate +
                "   •   МАГАЗИН " + (g.Weapon == WeaponType.Pistol ? 12 :
                g.Weapon == WeaponType.Rifle ? 30 : g.Weapon == WeaponType.Shotgun ? 8 : 70),
                21, statTop + 44, 348, 44, 13, TextMuted);
            float y = H - 170;
            Button(g.WeaponLevel >= 5 ? "МАКС. УРОВЕНЬ" : "УЛУЧШИТЬ   ● " + g.UpgradeCost, 16, y, 358, 62, Orange, () => g.UpgradeWeapon(), 19);
            FooterBack(g);
        }

        void Bonuses(ZSRGame g)
        {
            TitleBar("БОНУСЫ В ЗАБЕГЕ", g);
            string[] title = { "ДВОЙНОЙ УРОН", "ТУРБО-СТРЕЛЬБА", "ЭНЕРГОЩИТ", "МАГНИТ МОНЕТ", "БОЕВАЯ ПОДДЕРЖКА" };
            string[] explain = { "Урон x2 на 10 секунд", "Темп стрельбы +60%", "Защита от урона на 8+ сек", "Притягивает монеты", "Дрон автоматически бьёт врагов" };
            string[] icon = { "×2", "⚡", "◆", "⊂", "+" };
            for (int i = 0; i < 5; i++)
            {
                float y = 117 + i * 107;
                Block("bonus", 16, y, 358, 97, Panel);
                Block("icon", 25, y + 12, 73, 72, new Color(.19f, .13f, .075f, 1));
                Accent(25, y + 12, 73, i == 2 ? Cyan : Orange);
                Label(icon[i], 27, y + 20, 69, 57, 32, i == 2 ? Cyan : Gold);
                Label(title[i], 110, y + 14, 247, 28, 17, Color.white, TextAnchor.MiddleLeft);
                Label(explain[i], 110, y + 44, 245, 45, 13, TextMuted, TextAnchor.MiddleLeft, false);
            }
            FooterBack(g);
        }

        void Items(ZSRGame g)
        {
            TitleBar("ПРЕДМЕТЫ И МОДУЛИ", g);
            Label("● " + g.Coins.ToString("N0"), 244, 102, 128, 24, 14, Gold);
            ItemType[] items = { ItemType.Medkit, ItemType.Grenade, ItemType.Drone, ItemType.Armor };
            string[] names = { "АПТЕЧКА", "ГРАНАТА", "ДРОН", "БРОНЕМОДУЛЬ" };
            string[] glyph = { "+", "✹", "◈", "◆" };
            for (int i = 0; i < 4; i++)
            {
                float x = 16 + (i % 2) * 185;
                float y = 120 + (i / 2) * 252;
                ItemType chosenItem = items[i]; // Capture each item separately.
                int lvl = g.ItemLevel(chosenItem);
                int cost = g.ItemUpgradeCost(items[i]);
                Block("item card", x, y, 173, 236, Panel);
                Accent(x, y, 173, i == 3 ? Cyan : Orange);
                Block("symbol", x + 38, y + 18, 98, 82, new Color(.10f, .15f, .20f, 1));
                Label(glyph[i], x + 38, y + 24, 98, 70, 42, i == 3 ? Cyan : Gold);
                Label(names[i], x + 5, y + 108, 163, 24, 16, Color.white);
                Label("УРОВЕНЬ " + lvl + " / 5", x + 8, y + 136, 157, 20, 12, Gold);
                Label(g.ItemDescription(items[i]), x + 8, y + 160, 157, 36, 12, TextMuted);
                Button(lvl >= 5 ? "МАКС" : ("УЛУЧШИТЬ ● " + cost), x + 10, y + 198, 153, 28, i == 3 ? Cyan : Orange, () => g.UpgradeItem(chosenItem), 10);
            }
            FooterBack(g);
        }

        void Levels(ZSRGame g)
        {
            TitleBar("УРОВНИ 1—15", g);
            Label("ТРИ ГЛАВЫ  •  БОСС КАЖДЫЙ 5-Й УРОВЕНЬ", 17, 101, 356, 28, 12, TextMuted);
            for (int i = 1; i <= ZSRGame.MaxLevels; i++)
            {
                int id = i; // Safe capture for button callback.
                float y = 135 + (i - 1) * 33f;
                bool locked = id > g.UnlockedLevel;
                bool current = id == g.Level;
                Color c = current ? new Color(.23f, .15f, .09f, .96f) :
                          id % 5 == 0 ? new Color(.20f, .09f, .09f, .96f) : new Color(.06f, .10f, .14f, .92f);
                Block("level row", 14, y, 362, 29, c);
                Label(id.ToString("00"), 20, y + 3, 31, 22, 13, id % 5 == 0 ? Orange : Gold);
                string name = ZSRGame.LevelNames[id - 1];
                Label(name, 54, y + 3, 218, 22, 13, Color.white, TextAnchor.MiddleLeft);
                if (locked)
                    Label("ЗАМОК", 278, y + 4, 90, 20, 10, TextMuted);
                else
                    Button(current ? "ВЫБРАН" : "ВЫБРАТЬ", 272, y + 2, 97, 25,
                        current ? Orange : Cyan, () => g.SelectLevel(id), 10);
            }
#if UNITY_EDITOR
            Button("ТЕСТ: ОТКРЫТЬ ВСЕ 15 УРОВНЕЙ", 19, H - 139, 352, 42, Cyan, () => g.UnlockForTesting(), 12);
#endif
            FooterBack(g);
        }

        void DrawHUD(ZSRGame g)
        {
            Block("HUD top", 0, 0, W, 110, new Color(.026f, .041f, .061f, .87f));
            Accent(9, 107, 372, Orange);
            hudLevel = Label("УРОВЕНЬ", 62, 13, 210, 31, 18, Color.white);
            hudCoins = Label("●", 275, 14, 98, 32, 15, Gold);
            Button("II", 9, 12, 44, 42, Cyan, () => g.TogglePause(), 16);
            Block("run progression bg", 78, 59, 289, 8, new Color(.20f, .25f, .28f, 1));
            progressFill = Block("run progression", 78, 59, 1, 8, Green);
            hudHealth = Label("HP", 12, 78, 140, 30, 15, Color.white, TextAnchor.MiddleLeft);
            Block("health bar", 145, 88, 222, 10, new Color(.34f, .20f, .20f, 1));
            hpFill = Block("health fill", 145, 88, 1, 10, Green);
            hudBoss = Label("БОСС: СУПЕР-МУТАНТ", 14, 105, 362, 24, 13, Orange);
            // The boss background and health bar appear only during the three boss encounters.
            bossBackdrop = Block("boss hp backdrop", 35, 131, 320, 9, new Color(.24f,.08f,.10f,.85f));
            bossFill = Block("boss health red", 35, 131, 320, 9, new Color(.91f, .12f, .13f, 1));
            hudMessage = Label("", 8, 145, 374, 38, 20, Gold);
            float bottom = H - 107;
            Block("HUD bottom", 0, bottom - 15, W, 122, new Color(.026f, .041f, .061f, .85f));
            Accent(9, bottom - 15, 372, Orange);
            hudAmmo = Label("", 16, bottom - 8, 355, 31, 19, Color.white, TextAnchor.MiddleLeft);
            Button("АПТЕЧКА", 11, bottom + 33, 115, 64, Cyan, () => g.UseMedkit(), 13);
            Button("ГРАНАТА", 138, bottom + 33, 114, 64, Orange, () => g.ThrowGrenade(), 13);
            Button("ДРОН", 264, bottom + 33, 114, 64, Cyan, () => g.UseDrone(), 13);
            hudMed = Label("", 11, bottom + 33, 115, 17, 12, Gold);
            hudGrenade = Label("", 138, bottom + 33, 114, 17, 12, Gold);
            hudDrone = Label("", 264, bottom + 33, 114, 17, 12, Gold);
            UpdateHUD(g);
        }

        void UpdateHUD(ZSRGame g)
        {
            if (hudAmmo == null) return;
            hudLevel.text = "УРОВЕНЬ " + g.Level + " / 15";
            hudCoins.text = "● " + g.Earned;
            hudHealth.text = "ЗДОРОВЬЕ " + g.Hero.Health;
            hudAmmo.text = g.Weapon.ToString().ToUpperInvariant() + "   " + g.Ammo + " / " + g.Magazine + (g.IsReloading ? "  •  ПЕРЕЗАРЯДКА" : "");
            hudMessage.text = g.CurrentNotice;
            if (hudBoss != null) hudBoss.gameObject.SetActive(g.BossAlive);
            if (bossBackdrop != null) bossBackdrop.gameObject.SetActive(g.BossAlive);
            if (bossFill != null)
            {
                bossFill.gameObject.SetActive(g.BossAlive);
                bossFill.sizeDelta = new Vector2(320f * g.BossHealthFraction, 9f);
            }
            hudMed.text = "× " + g.Medkits;
            hudGrenade.text = "× " + g.Grenades;
            hudDrone.text = "× " + g.Drones;
            if (progressFill != null) progressFill.sizeDelta = new Vector2(289 * g.Progress, 8);
            if (hpFill != null) hpFill.sizeDelta = new Vector2(222 * (g.Hero.Health / (float)Mathf.Max(1, g.Hero.MaxHealth)), 10);
        }

        void ModalDark()
        {
            Block("pause scrim", 0, 0, W, H, new Color(.006f, .012f, .018f, .90f));
            Block("modal panel", 17, H * .25f, 356, 360, Panel);
            Accent(17, H * .25f, 356, Orange);
        }

        void PauseScreen(ZSRGame g)
        {
            ModalDark();
            Label("ПАУЗА", 32, H * .25f + 24, 326, 57, 30, Color.white);
            Button("ПРОДОЛЖИТЬ", 39, H * .25f + 112, 312, 68, Orange, () => g.TogglePause(), 20);
            Button("ЗАНОВО", 39, H * .25f + 192, 312, 60, Cyan, () => g.Retry(), 18);
            Button("В МЕНЮ", 39, H * .25f + 264, 312, 62, Cyan, () => g.GoMenu(), 18);
        }

        void ResultScreen(ZSRGame g, bool victory)
        {
            ModalDark();
            Label(victory ? "ПОБЕДА!" : "ПОРАЖЕНИЕ", 25, H * .25f + 17, 340, 65, 32, victory ? Gold : Orange);
            Label("ЗОМБИ: " + g.Eliminated + "     •     МОНЕТЫ: " + g.Earned, 25, H * .25f + 92, 340, 46, 15, TextMuted);
            Button(victory && g.Level < ZSRGame.MaxLevels ? "СЛЕДУЮЩИЙ УРОВЕНЬ" : "ПОВТОРИТЬ", 39, H * .25f + 159, 312, 62, Orange,
                () => { if (victory && g.Level < ZSRGame.MaxLevels) g.NextLevel(); else g.Retry(); }, 18);
            Button("ЗАНОВО", 39, H * .25f + 230, 312, 50, Cyan, () => g.Retry(), 16);
            Button("В МЕНЮ", 39, H * .25f + 289, 312, 48, Cyan, () => g.GoMenu(), 16);
        }
    }
}
