using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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
        Texture2D artMax, artEva, artRifle, menuWallpaper, sectionWallpaper;
        readonly Texture2D[] weaponArts = new Texture2D[4];
        static readonly string[] WeaponNames = { "ПИСТОЛЕТ", "АВТОМАТ", "ДРОБОВИК", "ПУЛЕМЁТ" };
        Sprite primarySprite, secondarySprite, mutedSprite, panelSprite;
        ZSRState lastState = (ZSRState)(-1);
        int lastCoins = -1, lastLevel = -1, lastHero = -1, lastWeapon = -1;
        float lastHeight, lastWidth;
        UnityEngine.Rect lastSafeArea;
        bool refresh;
        int wallpaper = -1;
        CanvasGroup pageGroup;
        Vector2 pageRest;
        float pageTime;
        const float PageDuration = .24f;
        readonly Text[] hudBonuses = new Text[5];
        readonly int[] bonusSeconds = new int[5];
        UnityEngine.UI.Button firstButton;
        const float H = 844f;
        Text hudAmmo, hudCoins, hudHealth, hudLevel, hudMessage, hudGrenade, hudMed, hudDrone;
        RectTransform hpFill, progressFill, bossFill, bossBackdrop;
        Text hudBoss;

        void Awake()
        {
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
            font = Resources.Load<Font>("UI/ZSRMenuFont");
            if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial", "Roboto", "Noto Sans", "DejaVu Sans" }, 24);
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
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            {
                if (!IsBattle(g.State) && g.State != ZSRState.Menu) g.GoMenu();
            }
            float hh = root.rect.height;
            if (refresh || lastState != g.State || (g.State != ZSRState.Playing && lastCoins != g.Coins) || lastLevel != g.Level || lastHero != g.SelectedHero || lastWeapon != (int)g.Weapon || Mathf.Abs(hh - lastHeight) > 1.5f || Mathf.Abs(root.rect.width - lastWidth) > 1.5f || Screen.safeArea != lastSafeArea)
                Draw();
            if (g.State == ZSRState.Playing || g.State == ZSRState.Paused) UpdateHUD(g);
            AnimatePage(Time.unscaledDeltaTime);
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

        UnityEngine.UI.Button Button(string txt, float x, float y, float w, float h, Color accent,
            Action callback, int textSize = 16, bool enabled = true, bool redraw = true)
        {
            var t = Rect("BUTTON: " + txt, content, x, y, w, h);
            var img = t.gameObject.AddComponent<UnityEngine.UI.Image>();
            bool primary = accent.r > .80f && accent.g < .70f;
            Sprite sprite = primary ? primarySprite : secondarySprite;
            if (!enabled) sprite = mutedSprite;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = UnityEngine.UI.Image.Type.Sliced;
                img.color = Color.white;
            }
            else img.color = primary ? new Color(.77f, .28f, .066f) : PanelLight;
            var button = t.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = img;
            button.interactable = enabled;
            var colors = button.colors;
            colors.highlightedColor = new Color(.92f, .98f, 1f);
            colors.pressedColor = new Color(.71f, .81f, .91f);
            colors.selectedColor = new Color(.84f, .94f, 1f);
            colors.disabledColor = new Color(.70f, .74f, .79f, .80f);
            colors.fadeDuration = .10f;
            button.colors = colors;
            button.onClick.AddListener(() => { callback?.Invoke(); if (redraw) Invalidate(); });
            Label(txt, 4, 4, w - 8, h - 8, textSize, enabled ? Color.white : TextMuted,
                TextAnchor.MiddleCenter, true, t);
            if (enabled && firstButton == null) firstButton = button;
            return button;
        }

        void TitleBar(string name, ZSRGame g)
        {
            Block("section header", 0, 0, W, 96, new Color(.025f, .040f, .060f, .97f));
            Button("‹", 14, 16, 48, 50, Cyan, g.GoMenu, 30);
            Label(name, 76, 15, 297, 51, 22, Color.white, TextAnchor.MiddleLeft);
            Block("section divider", 14, 86, 362, 1, new Color(.33f, .45f, .54f, .61f));
        }

        void Navigation(ZSRGame g)
        {
            const float y = 755f, width = 68f, step = 73f;
            Block("navigation backdrop", 10, y, 370, 75, new Color(.026f, .041f, .063f, .98f));
            NavButton("Главная", 0, ZSRState.Menu, g.GoMenu);
            NavButton("Герои", 1, ZSRState.Heroes, g.GoHeroes);
            NavButton("Арсенал", 2, ZSRState.Arsenal, g.GoArsenal);
            NavButton("Предметы", 3, ZSRState.Items, g.GoItems);
            NavButton("Уровни", 4, ZSRState.Levels, g.GoLevels);

            void NavButton(string label, int index, ZSRState state, Action action)
            {
                float x = 15 + index * step;
                bool selected = g.State == state;
                Button(label, x, y + 11, width, 51, Cyan, selected ? null : action, 11, redraw: !selected);
                if (selected) Accent(x + 15, y + 58, width - 30, Orange);
            }
        }

        static bool IsBattle(ZSRState state) => state == ZSRState.Playing || state == ZSRState.Paused ||
            state == ZSRState.Victory || state == ZSRState.Defeat;

        static void Retire(RectTransform view)
        {
            view.gameObject.SetActive(false);
            Destroy(view.gameObject);
        }

        void AnimatePage(float dt)
        {
            if (pageGroup == null || pageTime >= PageDuration) return;
            pageTime = Mathf.Min(PageDuration, pageTime + dt);
            float t = pageTime / PageDuration;
            float remaining = (1f - t) * (1f - t) * (1f - t);
            pageGroup.alpha = 1f - remaining;
            pageGroup.blocksRaycasts = t >= .56f;
            pageGroup.interactable = pageGroup.blocksRaycasts;
            content.anchoredPosition = pageRest + Vector2.down * (remaining * 15f);
        }

        void Draw()
        {
            var g = ZSRGame.Instance;
            if (g == null || root == null || root.rect.width < 1f || root.rect.height < 1f) return;
            bool inBattle = IsBattle(g.State);
            bool motionEnabled = !inBattle && lastState != g.State;
            bool viewportChanged = Mathf.Abs(root.rect.width - lastWidth) > 1.5f ||
                                   Mathf.Abs(root.rect.height - lastHeight) > 1.5f;
            lastSafeArea = Screen.safeArea;
            lastHeight = root.rect.height; lastWidth = root.rect.width; lastState = g.State;
            lastCoins = g.Coins; lastLevel = g.Level; lastHero = g.SelectedHero; lastWeapon = (int)g.Weapon;
            refresh = false;
            if (content != null) Retire(content);
            int nextWallpaper = inBattle ? -1 : g.State == ZSRState.Menu ? 0 : 1;
            if (wallpaper != nextWallpaper || (nextWallpaper >= 0 && viewportChanged))
            {
                if (background != null) { Retire(background); background = null; }
                wallpaper = nextWallpaper;
                if (wallpaper >= 0)
                {
                    background = RectStretch("Menu background", root);
                    BuildAdaptiveBackground(g);
                }
            }
            float unitsX = root.rect.width / Mathf.Max(1, Screen.width);
            float unitsY = root.rect.height / Mathf.Max(1, Screen.height);
            float safeWidth = lastSafeArea.width * unitsX;
            float safeHeight = lastSafeArea.height * unitsY;
            float contentScale = Mathf.Min(1f, Mathf.Min(safeWidth / W, safeHeight / H));
            content = Rect("Current screen", root,
                lastSafeArea.xMin * unitsX + (safeWidth - W * contentScale) * .5f,
                (Screen.height - lastSafeArea.yMax) * unitsY + (safeHeight - H * contentScale) * .5f,
                W, H);
            content.localScale = Vector3.one * contentScale;
            pageRest = content.anchoredPosition;
            pageGroup = content.gameObject.AddComponent<CanvasGroup>();
            pageTime = motionEnabled ? 0f : PageDuration;
            firstButton = null;
            AnimatePage(0f);

            hudAmmo = hudCoins = hudHealth = hudLevel = hudMessage = hudGrenade = hudMed = hudDrone = null;
            hpFill = progressFill = bossFill = bossBackdrop = null; hudBoss = null;
            if (inBattle)
            {
                DrawHUD(g);
                firstButton = null;
                if (g.State == ZSRState.Paused) PauseScreen(g);
                if (g.State == ZSRState.Victory || g.State == ZSRState.Defeat) ResultScreen(g, g.State == ZSRState.Victory);
                if (firstButton != null && EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(firstButton.gameObject);
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
            Navigation(g);
            if (motionEnabled && EventSystem.current != null && firstButton != null)
                EventSystem.current.SetSelectedGameObject(firstButton.gameObject);
        }

        void BuildAdaptiveBackground(ZSRGame g)
        {
            BlockStretch("fullscreen cinematic base", Dark, background);
            // Portrait artwork fills the complete Game viewport, not a central square.
            StretchImage(g.State == ZSRState.Menu ? menuWallpaper : sectionWallpaper,
                background, 1f, true);
            BlockStretch("Art contrast", new Color(.01f, .019f, .034f, g.State == ZSRState.Menu ? .07f : .48f), background);
        }

        void Menu(ZSRGame g)
        {
            Block("Progress pill", 18, 16, 211, 46, new Color(.03f, .05f, .079f, .94f));
            Label("● " + g.Coins.ToString("N0"), 30, 16, 102, 46, 16, Gold, TextAnchor.MiddleLeft);
            Label(g.UnlockedLevel + " / " + ZSRGame.MaxLevels, 133, 16, 85, 46, 14, TextMuted);
            Button(g.MusicLabel, 240, 16, 132, 46, Cyan, g.CycleMusic, 11);
            Label("ZOMBIE SURVIVAL", 19, 74, 352, 46, 32, Color.white);
            var wordmark = Label("RUN", 19, 115, 352, 60, 51, new Color(1f, .47f, .20f));
            wordmark.fontStyle = FontStyle.BoldAndItalic;

            Block("mission card", 16, 559, 358, 176, new Color(.038f, .060f, .086f, .99f));
            Accent(30, 560, 52, Orange);
            Label("УРОВЕНЬ " + g.Level + " / " + ZSRGame.MaxLevels, 30, 568, 203, 24, 12, Gold, TextAnchor.MiddleLeft);
            Label(g.LevelTitle, 30, 591, 330, 28, 18, Color.white, TextAnchor.MiddleLeft);
            Label(g.SelectedHero == 0 ? "МАКС" : "ЕВА", 30, 618, 150, 25, 12, TextMuted, TextAnchor.MiddleLeft);
            var play = Button("ИГРАТЬ", 29, 653, 218, 61, Orange, () => g.StartLevel(g.Level), 23);
            firstButton = play;
            Button("БОНУСЫ", 258, 653, 102, 61, Cyan, g.GoBonuses, 13);
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
        }

        void Arsenal(ZSRGame g)
        {
            TitleBar("АРСЕНАЛ", g);
            Label("ОРУЖИЕ И ПРОКАЧКА", 18, 108, 216, 24, 13, TextMuted, TextAnchor.MiddleLeft);
            Label("● " + g.Coins.ToString("N0"), 235, 108, 136, 24, 14, Gold);
            WeaponType[] list = { WeaponType.Pistol, WeaponType.Rifle, WeaponType.Shotgun, WeaponType.Minigun };
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
                Label(WeaponNames[i], x + 2, 195, 79, 22, 10, Color.white);
                Label(open ? (owned ? "В НАЛИЧИИ" : "● " + g.WeaponPrice(list[i])) : ("УРОВ. " + g.WeaponUnlockLevel(list[i])),
                    x + 2, 218, 79, 21, 10, open ? Gold : TextMuted);
                string btn = active ? "✓" : open ? (owned ? "ВЫБРАТЬ" : "КУПИТЬ") : "ЗАКРЫТО";
                Button(btn, x, 242, 83, 44, active ? Orange : Cyan, () => g.SelectWeapon(chosenWeapon), 10, open);
            }
            float artTop = 303;
            Block("weapon showcase", 13, artTop, 364, 178, Panel);
            Accent(13, artTop, 364, Orange);
            Texture2D selectedArt = weaponArts[(int)g.Weapon];
            if (selectedArt == null && g.Weapon == WeaponType.Rifle) selectedArt = artRifle;
            if (selectedArt != null) Image(selectedArt, 24, artTop + 13, 342, 126, false);
            Label("ВЫБРАНО: " + WeaponNames[(int)g.Weapon], 24, artTop + 140, 340, 30, 18, Gold);
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
            Button(g.WeaponLevel >= 5 ? "МАКС. УРОВЕНЬ" : "УЛУЧШИТЬ   ● " + g.UpgradeCost, 16, y, 358, 58, Orange, () => g.UpgradeWeapon(), 19, g.WeaponLevel < 5);
        }

        static readonly BonusKind[] TimedBonuses =
        {
            BonusKind.DoubleDamage, BonusKind.RapidFire, BonusKind.Shield, BonusKind.Magnet, BonusKind.Turret
        };

        void Bonuses(ZSRGame g)
        {
            TitleBar("БОНУСЫ В ЗАБЕГЕ", g);
            for (int i = 0; i < 6; i++)
            {
                var kind = (BonusKind)i;
                var art = ZSRVeilGateView.Appearance(kind);
                float y = 106 + i * 103;
                Block("bonus card", 16, y, 358, 89, Panel);
                Block("bonus mark background", 27, y + 12, 78, 64, new Color(.10f, .15f, .21f));
                Accent(27, y + 12, 78, art.tint);
                Label(art.label, 29, y + 19, 74, 50, art.label.Length > 3 ? 12 : 25, art.tint);
                Label(art.title, 117, y + 10, 243, 29, 17, Color.white, TextAnchor.MiddleLeft);
                string detail = kind switch
                {
                    BonusKind.Heal => "Восстанавливает до " + ZSRGame.GateHealAmount + " HP",
                    BonusKind.RapidFire => "+" + Mathf.RoundToInt((1f / ZSRGame.RapidFireInterval - 1f) * 100f) + "% к темпу • " + g.BonusDuration(kind) + " сек.",
                    _ => g.BonusDuration(kind).ToString("0.#") + " сек. действия"
                };
                Label(detail, 117, y + 42, 242, 34, 13, TextMuted, TextAnchor.MiddleLeft, false);
            }
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
                Label(g.ItemDescription(items[i]), x + 8, y + 154, 157, 34, 12, TextMuted);
                Button(lvl >= 5 ? "МАКС" : ("УЛУЧШИТЬ ● " + cost), x + 10, y + 188, 153, 44, i == 3 ? Cyan : Orange, () => g.UpgradeItem(chosenItem), 12, lvl < 5);
            }
        }

        void Levels(ZSRGame g)
        {
            TitleBar("УРОВНИ", g);
            string[] chapters = { "Руины мегаполиса", "Огненный город", "Эвакуационная зона" };
            for (int chapter = 0; chapter < chapters.Length; chapter++)
            {
                float y = 108 + chapter * 142;
                Block("chapter card", 14, y, 362, 130, Panel);
                Label(chapters[chapter], 26, y + 8, 254, 27, 18, Color.white, TextAnchor.MiddleLeft);
                Label("БОСС", 302, y + 8, 59, 27, 11, Orange);
                for (int stage = 1; stage <= 5; stage++)
                {
                    int id = chapter * 5 + stage;
                    bool locked = id > g.UnlockedLevel;
                    float x = 24 + (stage - 1) * 69;
                    Button(id.ToString("00"), x, y + 46, 64, 65, id == g.Level ? Orange : Cyan,
                        () => g.SelectLevel(id), 24, !locked);
                    if (stage == 5) Accent(x + 20, y + 106, 24, locked ? TextMuted : Gold);
                }
            }
            Label(g.LevelTitle, 24, 542, 342, 38, 19, Color.white);
            Button("ИГРАТЬ УРОВЕНЬ " + g.Level, 24, 592, 342, 62, Orange,
                () => g.StartLevel(g.Level), 20);
#if UNITY_EDITOR
            Button("ТЕСТ: ОТКРЫТЬ ВСЕ УРОВНИ", 24, 680, 342, 46, Cyan, g.UnlockForTesting, 11);
#endif
        }

        void DrawHUD(ZSRGame g)
        {
            Block("HUD top", 0, 0, W, 107, new Color(.026f, .041f, .061f, .87f));
            Accent(9, 104, 372, Orange);
            hudLevel = Label("УРОВЕНЬ", 62, 13, 210, 31, 18, Color.white);
            hudCoins = Label("●", 275, 14, 98, 32, 15, Gold);
            Button("Ⅱ", 9, 10, 48, 46, Cyan, g.TogglePause, 18, g.IsRunning, redraw: false);
            Block("run progression bg", 78, 59, 289, 8, new Color(.20f, .25f, .28f, 1));
            progressFill = Block("run progression", 78, 59, 1, 8, Green);
            hudHealth = Label("HP", 12, 78, 140, 30, 15, Color.white, TextAnchor.MiddleLeft);
            Block("health bar", 145, 88, 222, 10, new Color(.34f, .20f, .20f, 1));
            hpFill = Block("health fill", 145, 88, 1, 10, Green);
            for (int i = 0; i < hudBonuses.Length; i++)
            {
                hudBonuses[i] = Label("", 10, 111, 71, 18, 11, ZSRVeilGateView.Appearance(TimedBonuses[i]).tint);
                bonusSeconds[i] = -1;
            }
            hudBoss = Label(g.LevelTitle, 14, 133, 362, 21, 13, Orange);
            // The boss background and health bar appear only during the three boss encounters.
            bossBackdrop = Block("boss hp backdrop", 35, 158, 320, 9, new Color(.24f,.08f,.10f,.85f));
            bossFill = Block("boss health red", 35, 158, 320, 9, new Color(.91f, .12f, .13f, 1));
            hudMessage = Label("", 8, 172, 374, 34, 17, Gold);
            float bottom = H - 107;
            Block("HUD bottom", 0, bottom - 15, W, 122, new Color(.026f, .041f, .061f, .85f));
            Accent(9, bottom - 15, 372, Orange);
            hudAmmo = Label("", 16, bottom - 8, 355, 31, 19, Color.white, TextAnchor.MiddleLeft);
            Button("АПТЕЧКА", 11, bottom + 33, 115, 64, Cyan, g.UseMedkit, 13, g.IsRunning, redraw: false);
            Button("ГРАНАТА", 138, bottom + 33, 114, 64, Orange, g.ThrowGrenade, 13, g.IsRunning, redraw: false);
            Button("ДРОН", 264, bottom + 33, 114, 64, Cyan, g.UseDrone, 13, g.IsRunning, redraw: false);
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
            hudAmmo.text = WeaponNames[(int)g.Weapon] + "   " + g.Ammo + " / " + g.Magazine + (g.IsReloading ? "  •  ПЕРЕЗАРЯДКА" : "");
            hudMessage.text = g.CurrentNotice;
            int active = 0;
            for (int i = 0; i < hudBonuses.Length; i++)
            {
                int seconds = Mathf.CeilToInt(g.BonusRemaining(TimedBonuses[i]));
                Text tag = hudBonuses[i];
                if (bonusSeconds[i] != seconds)
                {
                    bonusSeconds[i] = seconds;
                    tag.gameObject.SetActive(seconds > 0);
                    if (seconds > 0) tag.text = ZSRVeilGateView.Appearance(TimedBonuses[i]).label + " " + seconds;
                }
                if (seconds > 0) tag.rectTransform.anchoredPosition = new Vector2(9 + active++ * 75, -111);
            }
            if (hudBoss != null) hudBoss.gameObject.SetActive(g.BossAlive);
            if (bossBackdrop != null) bossBackdrop.gameObject.SetActive(g.BossAlive);
            if (bossFill != null)
            {
                bossFill.gameObject.SetActive(g.BossAlive);
                bossFill.sizeDelta = new Vector2(320f * g.BossHealthFraction, 9f);
            }
            hudMed.text = "× " + g.Medkits;
            hudGrenade.text = "× " + g.Grenades;
            hudDrone.text = g.DroneSeconds > 0f
                ? "× " + g.Drones + "   •   " + Mathf.CeilToInt(g.DroneSeconds) + " СЕК"
                : "× " + g.Drones;
            if (progressFill != null) progressFill.sizeDelta = new Vector2(289 * g.Progress, 8);
            if (hpFill != null) hpFill.sizeDelta = new Vector2(222 * (g.Hero.Health / (float)Mathf.Max(1, g.Hero.MaxHealth)), 10);
        }

        void ModalDark()
        {
            var scrim = Block("pause scrim", 0, 0, W, H, new Color(.006f, .012f, .018f, .83f));
            scrim.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            Block("modal panel", 17, H * .25f, 356, 360, Panel);
            Accent(17, H * .25f, 356, Orange);
        }

        void PauseScreen(ZSRGame g)
        {
            ModalDark();
            Label("ПАУЗА", 32, H * .25f + 24, 326, 57, 30, Color.white);
            Button("ПРОДОЛЖИТЬ", 39, H * .25f + 112, 312, 68, Orange, () => g.TogglePause(), 20);
            Button("ЗАНОВО", 39, H * .25f + 188, 312, 47, Cyan, () => g.Retry(), 18);
            Button("В МЕНЮ", 39, H * .25f + 242, 312, 47, Cyan, () => g.GoMenu(), 18);
            Button(g.MusicLabel, 39, H * .25f + 297, 312, 46, Cyan, g.CycleMusic, 14);
        }

        void ResultScreen(ZSRGame g, bool victory)
        {
            ModalDark();
            Label(victory ? "ПОБЕДА!" : "ПОРАЖЕНИЕ", 25, H * .25f + 17, 340, 65, 32, victory ? Gold : Orange);
            Label("ЗОМБИ: " + g.Eliminated + "     •     МОНЕТЫ: " + g.Earned, 25, H * .25f + 92, 340, 46, 15, TextMuted);
            Button(victory && g.Level < ZSRGame.MaxLevels ? "СЛЕДУЮЩИЙ УРОВЕНЬ" : "ПОВТОРИТЬ", 39, H * .25f + 159, 312, 62, Orange,
                () => { if (victory && g.Level < ZSRGame.MaxLevels) g.NextLevel(); else g.Retry(); }, 18);
            if (victory && g.Level < ZSRGame.MaxLevels)
                Button("ЗАНОВО", 39, H * .25f + 230, 312, 50, Cyan, () => g.Retry(), 16);
            Button("В МЕНЮ", 39, H * .25f + 289, 312, 48, Cyan, () => g.GoMenu(), 16);
        }
    }
}
