using System;
using System.Globalization;
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
        static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");
        const float W=390f;

        Canvas canvas;
        RectTransform root, content, background;
        Font font, buttonFont;
        Texture2D artMax, artEva, artRifle, sectionWallpaper;
        readonly Texture2D[] heroCovers = new Texture2D[2];
        readonly Texture2D[] chapterSkies = new Texture2D[3];
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
        WeaponType inspectedWeapon;
        int levelPage;
        Vector2 pageRest;
        float pageTime;
        const float PageDuration = .24f;
        readonly Text[] hudBonuses = new Text[5];
        readonly int[] bonusSeconds = new int[5];
        UnityEngine.UI.Button firstButton;
        const float H = 844f;
        Text hudAmmo, hudCoins, hudHealth, hudMessage, hudGrenade, hudMed, hudDrone;
        RectTransform hpFill, progressFill, bossFill, bossBackdrop;
        Text hudBoss, hudRadio, hudRadioSpeaker, hudPace;
        RectTransform radioCard, paceFill;
        string lastRadioLine;
        ZSRPace lastPace;
        int lastPaceSeconds;

        void Awake()
        {
            artMax = Resources.Load<Texture2D>("Artwork/max");
            artEva = Resources.Load<Texture2D>("Artwork/eva");
            artRifle = Resources.Load<Texture2D>("Artwork/rifle");
            string[] weaponFiles = { "pistol", "rifle", "shotgun", "minigun" };
            for (int i=0; i<weaponFiles.Length; i++)
                weaponArts[i] = Resources.Load<Texture2D>("Artwork/Weapons/" + weaponFiles[i]);
            sectionWallpaper = Resources.Load<Texture2D>("UI/section_wallpaper");
            primarySprite = MakeSlicedSprite("UI/btn_primary");
            secondarySprite = MakeSlicedSprite("UI/btn_secondary");
            mutedSprite = MakeSlicedSprite("UI/btn_muted");
            panelSprite = MakeSlicedSprite("UI/panel");
            buttonFont = Resources.Load<Font>("UI/ZSRButtonFont");
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
            Action callback, int textSize = 16, bool enabled = true, bool redraw = true, Color? skinTint = null)
        {
            var t = Rect("BUTTON: " + txt, content, x, y, w, h);
            var img = t.gameObject.AddComponent<UnityEngine.UI.Image>();
            bool primary = accent.r > .80f && accent.g < .70f;
            Sprite sprite = primary ? primarySprite : secondarySprite;
            if (skinTint.HasValue && enabled) sprite = panelSprite;
            if (!enabled) sprite = mutedSprite;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = UnityEngine.UI.Image.Type.Sliced;
                img.color = enabled && skinTint.HasValue ? skinTint.Value : Color.white;
            }
            else img.color = primary ? new Color(.77f, .28f, .066f) : PanelLight;
            var button = t.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = img;
            button.interactable = enabled;
            var colors = button.colors;
            colors.colorMultiplier = 1.1f;
            colors.normalColor = new Color(.91f, .91f, .91f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(.78f, .84f, .90f);
            colors.selectedColor = new Color(.96f, .97f, 1f);
            colors.disabledColor = new Color(.64f, .69f, .74f, .85f);
            colors.fadeDuration = .13f;
            button.colors = colors;
            button.onClick.AddListener(() => { callback?.Invoke(); if (redraw) Invalidate(); });
            var caption = Label(txt, 5, 3, w - 10, h - 8, textSize, enabled ? Color.white : TextMuted,
                TextAnchor.MiddleCenter, buttonFont == null, t);
            if (buttonFont != null) caption.font = buttonFont;
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
            NavButton("Главная", 0, ZSRState.Menu, Gold, g.GoMenu);
            NavButton("Герои", 1, ZSRState.Heroes, new Color(1f, .46f, .56f), g.GoHeroes);
            NavButton("Арсенал", 2, ZSRState.Arsenal, Cyan, g.GoArsenal);
            NavButton("Предметы", 3, ZSRState.Items, Green, g.GoItems);
            NavButton("Уровни", 4, ZSRState.Levels, new Color(.77f, .58f, 1f), g.GoLevels);

            void NavButton(string label, int index, ZSRState state, Color color, Action action)
            {
                float x = 15 + index * step;
                bool selected = g.State == state;
                Color ink = Color.Lerp(new Color(.036f, .054f, .085f), color, selected ? .37f : .22f);
                Button(label, x, y + 11, width, 51, Cyan, selected ? null : action, 11, redraw: !selected, skinTint: ink);
                Accent(x + 16, y + (selected ? 57 : 14), width - 32, color);
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
            if (g.State == ZSRState.Arsenal && lastState != ZSRState.Arsenal) inspectedWeapon = g.Weapon;
            if (g.State == ZSRState.Levels && lastState != ZSRState.Levels)
                levelPage = (g.Level - 1) / ZSRLevelPlan.SectorSize;
            bool viewportChanged = Mathf.Abs(root.rect.width - lastWidth) > 1.5f ||
                                   Mathf.Abs(root.rect.height - lastHeight) > 1.5f;
            lastSafeArea = Screen.safeArea;
            lastHeight = root.rect.height; lastWidth = root.rect.width; lastState = g.State;
            lastCoins = g.Coins; lastLevel = g.Level; lastHero = g.SelectedHero; lastWeapon = (int)g.Weapon;
            refresh = false;
            if (content != null) Retire(content);
            int nextWallpaper = inBattle ? -1 : g.State == ZSRState.Menu ? g.Chapter : 3;
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

            hudAmmo = hudCoins = hudHealth = hudMessage = hudGrenade = hudMed = hudDrone = null;
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
            bool home = g.State == ZSRState.Menu;
            BlockStretch("Cinematic base", Dark, background);
            StretchImage(home ? ChapterSky(g.Chapter) : sectionWallpaper, background, 1f, true);
            BlockStretch("Art contrast", new Color(.01f, .019f, .034f, home ? .61f : .48f), background);
        }

        Texture2D ChapterSky(int chapter)
        {
            if (chapterSkies[chapter] == null) chapterSkies[chapter] = Resources.Load<Texture2D>(ZSRCampaignStyle.For(chapter).SkyResource);
            return chapterSkies[chapter];
        }

        void Menu(ZSRGame g)
        {
            var style = ZSRCampaignStyle.For(g.Chapter);
            string heroName = g.SelectedHero == 0 ? "МАКС" : "ЕВА";
            int selected = g.SelectedHero;
            if (heroCovers[selected] == null)
                heroCovers[selected] = Resources.Load<Texture2D>("UI/campaign_cover_" + (selected == 0 ? "max" : "eva"));
            Block("Progress pill", 18, 16, 211, 46, new Color(.03f, .05f, .079f, .94f));
            Label("● " + g.Coins.ToString("N0", Russian), 30, 16, 102, 46, 16, Gold, TextAnchor.MiddleLeft);
            Label(g.UnlockedLevel + " / " + ZSRGame.MaxLevels, 122, 16, 99, 46, 13, TextMuted);
            Button(g.MusicLabel, 240, 16, 132, 46, Cyan, g.CycleMusic, 11,
                skinTint: Color.Lerp(Dark, style.Accent, .25f));
            Label("ZOMBIE SURVIVAL", 22, 75, 278, 46, 29, Color.white, TextAnchor.MiddleLeft);
            var wordmark = Label("RUN", 294, 75, 78, 46, 31, style.Accent);
            wordmark.fontStyle = FontStyle.BoldAndItalic;

            Block("hero frame card", 15, 138, 360, 401, Color.Lerp(Dark, style.Accent, .37f));
            Image(heroCovers[selected] ?? (selected == 0 ? artMax : artEva), 17, 140, 356, 397);
            Button(heroName + "  ›", 29, 479, 123, 44, Cyan, g.GoHeroes, 14,
                skinTint: Color.Lerp(Dark, style.Accent, .20f));

            Block("mission card", 16, 552, 358, 183, Color.Lerp(Dark, style.Accent, .13f));
            Accent(30, 557, 64, style.Accent);
            Label(g.Level <= 15 ? style.Title.ToUpperInvariant() : "РАЙОН " + (g.Plan.Sector + 1) + " / " + ZSRLevelPlan.SectorCount,
                30, 570, 215, 21, 12, style.Accent, TextAnchor.MiddleLeft);
            Label(g.Level + " / " + ZSRGame.MaxLevels, 248, 570, 111, 21, 13, Gold, TextAnchor.MiddleRight);
            Label(g.LevelTitle, 30, 596, 330, 30, 19, Color.white, TextAnchor.MiddleLeft);
            string mode = g.Level <= g.ClearedLevel ? "Повторная вылазка" : "Новый рубеж";
            Label(g.IsBossLevel ? mode + "  •  Босс" : mode, 30, 620, 330, 29, 13, TextMuted, TextAnchor.MiddleLeft, false);
            bool continuing = g.ClearedLevel > 0 && g.Level == g.ClearedLevel + 1;
            var play = Button(continuing ? "ПРОДОЛЖИТЬ" : "ИГРАТЬ", 29, 653, 218, 63, Orange, () => g.StartLevel(g.Level), continuing ? 18 : 23);
            firstButton = play;
            Button("БОНУСЫ", 258, 653, 102, 63, Cyan, g.GoBonuses, 13,
                skinTint: Color.Lerp(Dark, style.Accent, .25f));
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
            Label(g.SelectedHero == 0 ? "СНАРЯЖЕНИЕ МАКСА" : "СНАРЯЖЕНИЕ ЕВЫ", 18, 105, 235, 23, 12, Cyan, TextAnchor.MiddleLeft);
            Label("● " + g.Coins.ToString("N0", Russian), 255, 105, 117, 23, 14, Gold);
            for (int i = 0; i < WeaponNames.Length; i++)
            {
                float x = 14 + i * 93;
                var chosen = (WeaponType)i;
                bool equipped = g.Weapon == chosen;
                bool open = g.WeaponLevelUnlocked(chosen);
                Color tint = Color.Lerp(Cyan, Orange, i / 3f);
                Block("weapon slot", x, 139, 83, 107, Color.Lerp(Panel, tint, inspectedWeapon == chosen ? .27f : .10f));
                Accent(x, 139, 83, inspectedWeapon == chosen ? tint : TextMuted);
                Image(weaponArts[i], x + 3, 148, 77, 50, false, alpha: open ? 1f : .54f);
                Label(WeaponNames[i], x + 2, 193, 79, 22, 10, Color.white);
                string availability = equipped ? "ВЫБРАНО" : g.WeaponOwned(chosen) ? "В НАЛИЧИИ" :
                    open ? "● " + Money(g.WeaponPrice(chosen)) : "ОТ УР. " + g.WeaponUnlockLevel(chosen);
                Label(availability, x + 2, 218, 79, 20, 10, equipped ? Gold : TextMuted);
                Button("ОСМОТР", x, 244, 83, 44, tint, () => inspectedWeapon = chosen, 10,
                    skinTint: Color.Lerp(Dark, tint, .28f));
            }

            var weapon = inspectedWeapon;
            var data = ZSRWeaponProfile.For(weapon);
            int level = g.WeaponLevelFor(weapon);
            bool unlocked = g.WeaponLevelUnlocked(weapon);
            bool owned = g.WeaponOwned(weapon);
            bool max = level >= ZSRGame.MaxUpgradeLevel;
            int cost = g.WeaponUpgradeCost(weapon);
            int price = g.WeaponPrice(weapon);
            Color accent = Color.Lerp(Cyan, Orange, (int)weapon / 3f);
            Block("weapon showcase", 13, 303, 364, 160, Color.Lerp(Panel, accent, .19f));
            Accent(13, 303, 364, accent);
            Image(weaponArts[(int)weapon] ?? (weapon == WeaponType.Rifle ? artRifle : null), 25, 307, 340, 107, false);
            Label(WeaponNames[(int)weapon], 23, 410, 343, 29, 20, Color.white);
            Label(data.Role, 23, 439, 343, 22, 13, accent, TextAnchor.MiddleCenter, false);

            Block("gun stats card", 13, 474, 364, 130, Panel);
            Label("РАНГ " + level + " / " + ZSRGame.MaxUpgradeLevel, 25, 478, 340, 25, 13, Gold, TextAnchor.MiddleLeft);
            string damage = data.Damage(level).ToString();
            if (!max && owned) damage += " → " + data.Damage(level + 1);
            WeaponStat("УРОН", damage, 25, 516, 103, accent);
            WeaponStat("ВЫСТР./С", (1f / data.ShotInterval(g.SelectedHero)).ToString("0.#", Russian), 137, 516, 103, accent);
            WeaponStat("МАГАЗИН", data.Magazine.ToString(), 249, 516, 113, accent);
            Label("Перезарядка  " + data.Reload.ToString("0.##", Russian) + " с", 25, 576, 340, 21, 12, TextMuted);

            bool canEquip = unlocked && g.Weapon != weapon && (owned || g.Coins >= price);
            string action = !unlocked ? "ОТКРОЕТСЯ НА УРОВНЕ " + data.UnlockLevel :
                g.Weapon == weapon ? "ОРУЖИЕ ВЫБРАНО" : owned ? "ВЫБРАТЬ ОРУЖИЕ" :
                g.Coins >= price ? "КУПИТЬ И ВЫБРАТЬ  ● " + price : "НЕ ХВАТАЕТ  ● " + (price - g.Coins);
            Button(action, 16, 616, 358, 50, Cyan, () => g.SelectWeapon(weapon), 14, canEquip,
                skinTint: Color.Lerp(Dark, accent, .29f));
            bool rankUnlocked = level < g.CampaignUpgradeCap;
            string upgrade = max ? "МАКСИМАЛЬНЫЙ РАНГ" : !rankUnlocked ? RankUnlock(level + 1) :
                g.Coins >= cost ? "УЛУЧШИТЬ  ● " + Money(cost) : "ДО УЛУЧШЕНИЯ  ● " + Money(cost - g.Coins);
            if (owned) Button(upgrade, 16, 675, 358, 54, Orange, () => g.UpgradeWeapon(weapon), 15,
                !max && rankUnlocked && g.Coins >= cost);
        }

        void WeaponStat(string name, string value, float x, float y, float width, Color accent)
        {
            Block("weapon stat", x, y, width, 54, Color.Lerp(Panel, accent, .13f));
            Label(name, x, y + 4, width, 18, 10, TextMuted);
            Label(value, x, y + 20, width, 28, 17, Color.white);
        }

        static readonly BonusKind[] TimedBonuses =
        {
            BonusKind.DoubleDamage, BonusKind.RapidFire, BonusKind.Shield, BonusKind.Magnet, BonusKind.Turret
        };

        void Bonuses(ZSRGame g)
        {
            TitleBar("БОНУСЫ В ЗАБЕГЕ", g);
            Label("Выбирайте ворота по знаку на трассе.", 18, 102, 354, 27, 13, Cyan, TextAnchor.MiddleLeft, false);
            for (int i = 0; i < 6; i++)
            {
                var kind = (BonusKind)i;
                var art = ZSRVeilGateView.Appearance(kind);
                float y = 138 + i * 94;
                Block("bonus card", 15, y, 360, 85, Color.Lerp(Panel, art.tint, .12f));
                Block("bonus mark background", 25, y + 10, 80, 63, Color.Lerp(Panel, art.tint, .20f));
                Accent(25, y + 10, 80, art.tint);
                Label(art.label, 27, y + 16, 76, 50, art.label.Length > 3 ? 11 : 25, art.tint);
                Label(kind == BonusKind.Heal ? "Лечение" : art.title, 118, y + 8, 195, 25, 16, Color.white, TextAnchor.MiddleLeft);
                string detail = kind switch
                {
                    BonusKind.DoubleDamage => "Удваивает урон выстрелов",
                    BonusKind.RapidFire => "+" + Mathf.RoundToInt((1f / ZSRGame.RapidFireInterval - 1f) * 100f) + "% к темпу стрельбы",
                    BonusKind.Shield => "Поглощает весь входящий урон",
                    BonusKind.Heal => "Восстанавливает до " + ZSRGame.GateHealAmount + " HP",
                    BonusKind.Magnet => "Притягивает монеты до " + ZSRGame.MagnetRadius.ToString("0.#", Russian) + " м",
                    _ => "Вызывает дрон  •  " + ZSRGame.DroneDamage(g.ItemLevel(ItemType.Drone)) + " урона / выстрел"
                };
                Label(detail, 118, y + 35, 242, 38, 13, Color.white, TextAnchor.MiddleLeft, false);
                Label(kind == BonusKind.Heal ? "" : g.BonusDuration(kind).ToString("0.#", Russian) + " с",
                    313, y + 8, 52, 24, 12, art.tint);
            }
            Label("Двойной урон и высокий темп заменяют друг друга.", 17, 707, 356, 30, 12, TextMuted, TextAnchor.MiddleCenter, false);
        }

        void Items(ZSRGame g)
        {
            TitleBar("ПРЕДМЕТЫ И МОДУЛИ", g);
            Label("УЛУЧШЕНИЯ НА ВСЮ КАМПАНИЮ", 17, 103, 225, 24, 11, Cyan, TextAnchor.MiddleLeft);
            Label("● " + g.Coins.ToString("N0", Russian), 244, 103, 128, 24, 14, Gold);
            string[] names = { "АПТЕЧКА", "ГРАНАТА", "ДРОН", "БРОНЕМОДУЛЬ" };
            string[] marks = { "HP", "ВЗРЫВ", "ДРОН", "ЩИТ" };
            Color[] accents = { Green, Orange, Cyan, new Color(.78f, .64f, 1f) };
            for (int i = 0; i < names.Length; i++)
            {
                float x = 16 + i % 2 * 185;
                float y = 135 + i / 2 * 275;
                var item = (ItemType)i;
                int level = g.ItemLevel(item);
                int max = ZSRGame.MaximumItemRank(item);
                int next = Mathf.Min(max, level + 1);
                int cost = g.ItemUpgradeCost(item);
                bool rankUnlocked = level < g.ItemUpgradeCap(item);
                Color tint = accents[i];
                Block("item card", x, y, 173, 258, Color.Lerp(Panel, tint, .13f));
                Accent(x, y, 173, tint);
                Block("item mark", x + 13, y + 13, 147, 42, Color.Lerp(Panel, tint, .25f));
                Label(marks[i], x + 13, y + 13, 147, 42, 18, tint);
                Label(names[i], x + 6, y + 63, 161, 24, 15, Color.white);
                Label("РАНГ " + level + " / " + max, x + 7, y + 88, 159, 22, 12, tint);
                Label(ItemStats(item, level, next), x + 8, y + 115, 157, 61, 13, Color.white,
                    TextAnchor.UpperCenter, false);
                if (item != ItemType.Armor)
                    Label("На старте: " + StatChange(ZSRGame.ItemStartCount(item, level), ZSRGame.ItemStartCount(item, next)),
                        x + 8, y + 178, 157, 23, 12, TextMuted);
                string upgrade = level == next ? "МАКСИМУМ" : !rankUnlocked ? RankUnlock(next) :
                    g.Coins >= cost ? "УЛУЧШИТЬ  ● " + Money(cost) : "ЕЩЁ  ● " + Money(cost - g.Coins);
                Button(upgrade, x + 8, y + 205, 157, 44, tint, () => g.UpgradeItem(item), 11,
                    level < next && rankUnlocked && g.Coins >= cost);
            }
        }

        static string StatChange(float current, float next) =>
            current.ToString("0.#", Russian) + (current == next ? "" : " → " + next.ToString("0.#", Russian));

        static string ItemStats(ItemType item, int level, int next) => item switch
        {
            ItemType.Medkit => "+" + StatChange(ZSRGame.MedkitHealth(level), ZSRGame.MedkitHealth(next)) + " HP",
            ItemType.Grenade => "Урон " + StatChange(ZSRGame.GrenadeDamage(level), ZSRGame.GrenadeDamage(next)) +
                "\nРадиус " + StatChange(ZSRGame.GrenadeRadius(level), ZSRGame.GrenadeRadius(next)) + " м",
            ItemType.Drone => StatChange(ZSRGame.DroneDuration(level), ZSRGame.DroneDuration(next)) +
                " с в бою\nУрон " + StatChange(ZSRGame.DroneDamage(level), ZSRGame.DroneDamage(next)),
            _ => "Защита " + StatChange(ZSRGame.ArmorReduction(level) * 100f, ZSRGame.ArmorReduction(next) * 100f) +
                "%\nЩит " + StatChange(ZSRGame.ShieldSeconds(level), ZSRGame.ShieldSeconds(next)) + " с"
        };

        static string Money(int amount) => amount.ToString("N0", Russian);
        static string RankUnlock(int rank) => "РАНГ " + rank + " С УР. " + ZSRLevelPlan.UnlockForRank(rank);

        void Levels(ZSRGame g)
        {
            TitleBar("КАМПАНИЯ", g);
#if UNITY_EDITOR
            Button("ТЕСТ", 295, 16, 77, 50, Cyan, g.UnlockForTesting, 11);
#endif
            const int pageSize = ZSRLevelPlan.SectorSize;
            int lower = levelPage * pageSize + 1, upper = lower + pageSize - 1;
            int environment = new ZSRLevelPlan(lower).Environment;
            var style = ZSRCampaignStyle.For(environment);
            Block("district card", 14, 105, 362, 69, Color.Lerp(Panel, style.Accent, .18f));
            Image(ChapterSky(environment), 16, 107, 358, 65, alpha: .34f);
            Label(ZSRLevelPlan.SectorName(levelPage), 26, 111, 337, 28, 18, Color.white, TextAnchor.MiddleLeft);
            string range = "Уровни " + lower + "–" + upper;
            Label(range, 26, 141, 221, 23, 13, style.Accent, TextAnchor.MiddleLeft);
            Label((levelPage + 1) + " / " + ZSRLevelPlan.SectorCount, 260, 141, 101, 23, 12, TextMuted, TextAnchor.MiddleRight);

            Button("−100", 15, 182, 60, 44, Cyan, () => levelPage = Mathf.Max(0, levelPage - 4), 13, levelPage > 0);
            Button("‹", 81, 182, 48, 44, Cyan, () => levelPage--, 24, levelPage > 0);
            Button("К ФРОНТУ", 135, 182, 120, 44, Cyan, () =>
            {
                g.SelectLevel(g.UnlockedLevel);
                levelPage = (g.UnlockedLevel - 1) / pageSize;
            }, 13);
            Button("›", 261, 182, 48, 44, Cyan, () => levelPage++, 24, levelPage + 1 < ZSRLevelPlan.SectorCount);
            Button("+100", 315, 182, 60, 44, Cyan, () => levelPage = Mathf.Min(ZSRLevelPlan.SectorCount - 1, levelPage + 4),
                13, levelPage + 1 < ZSRLevelPlan.SectorCount);

            for (int i = 0; i < pageSize; i++)
            {
                int id = lower + i;
                var plan = new ZSRLevelPlan(id);
                float x = 15 + (i % 5) * 73f, y = 243 + (i / 5) * 56f;
                bool available = id <= g.UnlockedLevel;
                Color tint = id == g.Level ? Gold : plan.IsBoss ? Orange : id <= g.ClearedLevel ? Green : style.Accent;
                Button(id.ToString(), x, y, 68, 49, tint, () => g.SelectLevel(id), 18, available,
                    skinTint: Color.Lerp(Dark, tint, id == g.Level ? .53f : .23f));
                if (available && (plan.IsBoss || id <= g.ClearedLevel || id == g.Level))
                    Accent(x + 17, y + 42, 34, tint);
            }

            Block("selected level card", 15, 529, 360, 140, Panel);
            Label(g.Level + "  •  " + g.LevelTitle, 26, 535, 337, 29, 15, Color.white, TextAnchor.MiddleLeft);
            bool replay = g.Level <= g.ClearedLevel;
            string earnings = (replay ? " + 65% добычи" : " + добыча") + (g.IsBossLevel ? "  •  БОСС" : "");
            Label("● " + Money(g.Plan.VictoryGrant(!replay)) + earnings, 26, 570, 337, 26, 13, Gold, TextAnchor.MiddleLeft);
            Button(replay ? "ПОВТОРИТЬ УРОВЕНЬ " + g.Level : "ИГРАТЬ УРОВЕНЬ " + g.Level, 24, 604, 342, 54, Orange,
                () => g.StartLevel(g.Level), 18);

            int previous = g.ReplayLevel;
            if (previous > 0)
                Button("ЗАРАБОТАТЬ МОНЕТЫ  •  УР. " + previous, 24, 680, 342, 49, Cyan, () => g.StartLevel(previous), 13);
        }

        void DrawHUD(ZSRGame g)
        {
            Block("HUD top", 0, 0, W, 107, new Color(.026f, .041f, .061f, .87f));
            Accent(9, 104, 372, Orange);
            Label("УРОВЕНЬ " + g.Level + " / " + ZSRGame.MaxLevels, 62, 13, 210, 31, 16, Color.white);
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
            hudMessage = Label("", 8, 172, 374, 27, 15, Gold);
            radioCard = Block("radio scene panel", 16, 201, 358, 106, new Color(.027f, .059f, .087f, .94f));
            Block("radio accent", 8, 13, 3, 78, Cyan, radioCard);
            Label(g.HasStoryBeat ? g.StoryBeat.Title.ToUpperInvariant() : "", 22, 8, 212, 23, 13, Color.white,
                TextAnchor.MiddleLeft, true, radioCard);
            hudRadioSpeaker = Label("", 224, 8, 118, 23, 11, Cyan, TextAnchor.MiddleRight, true, radioCard);
            hudRadio = Label("", 22, 36, 318, 62, 15, Color.white, TextAnchor.UpperLeft, false, radioCard);
            radioCard.gameObject.SetActive(false);
            lastRadioLine = null;
            lastPaceSeconds = -1;
            float bottom = H - 107;
            Block("HUD bottom", 0, bottom - 15, W, 122, new Color(.026f, .041f, .061f, .85f));
            Accent(9, bottom - 15, 372, Orange);
            hudAmmo = Label("", 16, bottom - 10, 358, 25, 17, Color.white, TextAnchor.MiddleLeft);
            hudPace = Label("", 16, bottom + 14, 358, 17, 11, Cyan, TextAnchor.MiddleLeft);
            paceFill = Block("pace ready", 287, bottom + 21, 84, 3, Cyan);
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
            hudCoins.text = "● " + g.Earned;
            hudHealth.text = "ЗДОРОВЬЕ " + g.Hero.Health;
            hudAmmo.text = WeaponNames[(int)g.Weapon] + "   " + g.Ammo + " / " + g.Magazine + (g.IsReloading ? "  •  ПЕРЕЗАРЯДКА" : "");
            hudMessage.text = g.CurrentNotice;
            var radio = g.Radio;
            if (radio.line != lastRadioLine)
            {
                lastRadioLine = radio.line;
                radioCard.gameObject.SetActive(radio.line != null);
                if (radio.line != null)
                {
                    hudRadioSpeaker.text = radio.speaker == 0 ? "РАЦИЯ  /  МАКС" : "РАЦИЯ  /  ЕВА";
                    hudRadioSpeaker.color = radio.speaker == 0 ? Orange : Cyan;
                    hudRadio.text = radio.line;
                }
            }
            int rechargeSeconds = Mathf.CeilToInt(g.PaceRecharge);
            if (g.Pace != lastPace || rechargeSeconds != lastPaceSeconds)
            {
                lastPace = g.Pace;
                lastPaceSeconds = rechargeSeconds;
                hudPace.text = g.Pace switch
                {
                    ZSRPace.Surge => "РЫВОК",
                    ZSRPace.Walk => "ШАГ",
                    _ => rechargeSeconds > 0 ? "РЫВОК / ШАГ ЧЕРЕЗ " + rechargeSeconds + " С" : "↑ РЫВОК     ↓ ШАГ"
                };
            }
            paceFill.sizeDelta = new Vector2(84f * (1f - g.PaceRecharge / ZSRRunPace.RechargeSeconds), 3f);
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

        float ModalDark(float height)
        {
            var scrim = Block("pause scrim", 0, 0, W, H, new Color(.006f, .012f, .018f, .83f));
            scrim.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            float top = (H - height) * .5f;
            Block("modal panel", 17, top, 356, height, Panel);
            Accent(30, top + 5, 68, Orange);
            return top;
        }

        void PauseScreen(ZSRGame g)
        {
            float top = ModalDark(360);
            Label("ПАУЗА", 32, top + 24, 326, 57, 30, Color.white);
            Button("ПРОДОЛЖИТЬ", 39, top + 112, 312, 68, Orange, g.TogglePause, 20);
            Button("ЗАНОВО", 39, top + 188, 312, 47, Cyan, g.Retry, 18);
            Button("В МЕНЮ", 39, top + 242, 312, 47, Cyan, g.GoMenu, 18);
            Button(g.MusicLabel, 39, top + 297, 312, 46, Cyan, g.CycleMusic, 14);
        }

        void ResultScreen(ZSRGame g, bool victory)
        {
            float top = ModalDark(428);
            bool lastLevel = g.Level == ZSRGame.MaxLevels;
            string title = victory ? (lastLevel ? "КАМПАНИЯ ПРОЙДЕНА" : "ПОБЕДА") : "ПОРАЖЕНИЕ";
            Label(title, 28, top + 20, 334, 44, lastLevel && victory ? 24 : 31, victory ? Gold : Orange);
            string reward = "ЗОМБИ: " + g.Eliminated + "   •   ● +" + Money(g.Payout);
            Label(reward, 28, top + 66, 334, 27, 16, Gold);

            int previous = g.ReplayLevel;
            if (victory && g.HasStoryBeat)
                Label(g.StoryBeat.After, 37, top + 97, 316, 65, 14, Color.white, TextAnchor.MiddleCenter, false);
            else if (!victory && previous > 0 && g.WeaponLevel < g.CampaignUpgradeCap && g.Coins < g.UpgradeCost)
                Label("До улучшения оружия не хватает " + Money(g.UpgradeCost - g.Coins) +
                    " монет. Их можно заработать на пройденном уровне.", 37, top + 97, 316, 65, 14,
                    TextMuted, TextAnchor.MiddleCenter, false);
            else if (victory)
                Label(lastLevel ? "Финальный рубеж взят." : g.Replaying ? "Монеты за повтор зачислены." : "Следующий рубеж открыт.", 37, top + 103, 316, 43, 14, TextMuted);
            else
                Label("В Арсенале можно сменить оружие или улучшить его.", 37, top + 103, 316, 57, 14, TextMuted);

            bool canAdvance = victory && !lastLevel;
            Button(canAdvance ? "СЛЕДУЮЩИЙ УРОВЕНЬ" : "ПОВТОРИТЬ", 39, top + 175, 312, 59, Orange,
                canAdvance ? new Action(g.NextLevel) : g.Retry, 18);
            Button("АРСЕНАЛ", 39, top + 244, 312, 49, Cyan, g.GoArsenal, 16);
            int replay = victory ? g.Level : previous;
            if (replay > 0 && (canAdvance || !victory))
                Button("ЗАРАБОТАТЬ МОНЕТЫ  •  УР. " + replay, 39, top + 301, 312, 49, Cyan,
                    () => g.StartLevel(replay), 13);
            Button("В МЕНЮ", 39, top + 359, 312, 49, Cyan, g.GoMenu, 16);
        }
    }
}
