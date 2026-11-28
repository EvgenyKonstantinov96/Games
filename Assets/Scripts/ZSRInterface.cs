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
        Texture2D artMax, artEva, survivorsArt, artRifle, sectionWallpaper, symbolAtlas;
        enum Symbol { Home, Heroes, Arsenal, Pack, Map, Coin, Gate, Music, Medkit, Grenade, Drone, Armor, Chevron }
        Vector2? focusPosition;
        UnityEngine.UI.Button previousControl;
        readonly Texture2D[] heroCovers = new Texture2D[2];
        readonly Texture2D[] chapterSkies = new Texture2D[3];
        readonly Texture2D[] weaponArts = new Texture2D[4];
        readonly Texture2D[] itemArts = new Texture2D[3];
        readonly UnityEngine.UI.Button[] itemButtons = new UnityEngine.UI.Button[3];
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
        UnityEngine.UI.Image damageLeft, damageRight;
        Text hudAmmo, hudCoins, hudHealth, hudMessage, hudGrenade, hudMed, hudDrone;
        RectTransform hpFill, hpTrail, progressFill, bossFill, bossBackdrop, noticeBackdrop;
        float shownHealth = 1f;
        Text hudBoss, hudPace;
        RectTransform paceFill;
        ZSRPace lastPace;
        int lastPaceSeconds;

        void Awake()
        {
            artMax = Resources.Load<Texture2D>("UI/hero_max") ?? Resources.Load<Texture2D>("Artwork/max");
            artEva = Resources.Load<Texture2D>("UI/hero_eva") ?? Resources.Load<Texture2D>("Artwork/eva");
            survivorsArt = Resources.Load<Texture2D>("UI/survivors_cover");
            itemArts[0] = Resources.Load<Texture2D>("UI/kit_medkit");
            itemArts[1] = Resources.Load<Texture2D>("UI/kit_grenade");
            itemArts[2] = Resources.Load<Texture2D>("UI/kit_drone");
            artRifle = Resources.Load<Texture2D>("Artwork/rifle");
            string[] weaponFiles = { "pistol", "rifle", "shotgun", "minigun" };
            for (int i=0; i<weaponFiles.Length; i++)
                weaponArts[i] = Resources.Load<Texture2D>("Artwork/Weapons/" + weaponFiles[i]);
            sectionWallpaper = Resources.Load<Texture2D>("UI/section_wallpaper");
            symbolAtlas = Resources.Load<Texture2D>("UI/menu_symbols");
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
                if (g.State == ZSRState.Briefing || (!IsBattle(g.State) && g.State != ZSRState.Menu)) g.GoMenu();
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
            if (!crop)
            {
                float aspect = tex.width / (float)tex.height;
                if (aspect > w/h)
                {
                    float fitted = w/aspect;
                    y += (h-fitted)*.5f;
                    h = fitted;
                }
                else
                {
                    float fitted = h*aspect;
                    x += (w-fitted)*.5f;
                    w = fitted;
                }
            }
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

        void Icon(Symbol symbol, float x, float y, float size, Color color, Transform parent = null)
        {
            if (symbolAtlas == null) return;
            var rt = Rect(symbol.ToString(), parent ?? content, x, y, size, size);
            var image = rt.gameObject.AddComponent<RawImage>();
            image.texture = symbolAtlas;
            image.color = color;
            image.raycastTarget = false;
            int cell = (int)symbol;
            image.uvRect = new UnityEngine.Rect((cell % 4) * .25f, (3 - cell / 4) * .25f, .25f, .25f);
        }

        Text ButtonCaption(string text, float x, float y, float w, float h, int size, Color color,
            TextAnchor align, Transform parent)
        {
            var caption = Label(text, x, y, w, h, size, color, align, buttonFont == null, parent);
            if (buttonFont != null) caption.font = buttonFont;
            return caption;
        }

        UnityEngine.UI.Button Button(string txt, float x, float y, float w, float h, Color accent,
            Action callback, int textSize = 16, bool enabled = true, bool redraw = true, Color? skinTint = null)
        {
            var button = ButtonSurface(txt, x, y, w, h, accent, callback, enabled, redraw, skinTint);
            ButtonCaption(txt, 5, 3, w - 10, h - 8, textSize, enabled ? Color.white : TextMuted,
                TextAnchor.MiddleCenter, button.transform);
            return button;
        }

        UnityEngine.UI.Button ButtonSurface(string txt, float x, float y, float w, float h, Color accent,
            Action callback, bool enabled = true, bool redraw = true, Color? skinTint = null)
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
            if (enabled && firstButton == null) firstButton = button;
            if (enabled && focusPosition.HasValue &&
                focusPosition.Value.x == x && focusPosition.Value.y == -y) previousControl = button;
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
            const float y = 751f, width = 70f, step = 73f;
            Block("navigation panel", 9, y, 372, 80, new Color(.050f, .076f, .107f, .99f));
            NavButton("Главная", Symbol.Home, ZSRState.Menu, g.GoMenu);
            NavButton("Герои", Symbol.Heroes, ZSRState.Heroes, g.GoHeroes);
            NavButton("Арсенал", Symbol.Arsenal, ZSRState.Arsenal, g.GoArsenal);
            NavButton("Предметы", Symbol.Pack, ZSRState.Items, g.GoItems);
            NavButton("Уровни", Symbol.Map, ZSRState.Levels, g.GoLevels);

            void NavButton(string label, Symbol symbol, ZSRState state, Action action)
            {
                float x = 14 + (int)symbol * step;
                bool selected = g.State == state;
                Color surface = selected ? new Color(.25f,.19f,.14f) : new Color(.09f,.13f,.17f);
                var button = ButtonSurface(label, x, y + 8, width, 64, Cyan, selected ? null : action,
                    redraw: !selected, skinTint: surface);
                Color ink = selected ? Gold : new Color(.75f,.83f,.88f);
                Icon(symbol, 23, 6, 24, ink, button.transform);
                ButtonCaption(label, 1, 33, width - 2, 21, 11, ink, TextAnchor.MiddleCenter, button.transform);
                if (selected) Block("current tab", 24, 58, 22, 2, Gold, button.transform);
            }
        }

        static bool IsBattle(ZSRState state) => state == ZSRState.Briefing || state == ZSRState.Playing || state == ZSRState.Paused ||
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
            focusPosition = null;
            previousControl = null;
            if (!inBattle && lastState == g.State && EventSystem.current != null)
            {
                var selected = EventSystem.current.currentSelectedGameObject;
                if (selected != null && selected.transform.parent == content)
                    focusPosition = ((RectTransform)selected.transform).anchoredPosition;
            }
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
            hpFill = hpTrail = progressFill = bossFill = bossBackdrop = noticeBackdrop = null; hudBoss = null;
            if (g.State == ZSRState.Briefing)
            {
                Dialogue(g);
                return;
            }
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
            if ((motionEnabled || focusPosition.HasValue) && EventSystem.current != null && firstButton != null)
                EventSystem.current.SetSelectedGameObject((previousControl ?? firstButton).gameObject);
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
            Block("wallet card", 15, 15, 152, 44, new Color(.075f, .107f, .14f, .98f));
            Icon(Symbol.Coin, 24, 23, 27, Gold);
            ButtonCaption(Money(g.Coins), 57, 16, 103, 41, 16, Gold, TextAnchor.MiddleLeft, content);
            var music = ButtonSurface(g.MusicLabel, 227, 15, 148, 44, Cyan, g.CycleMusic,
                skinTint: new Color(.10f, .15f, .20f));
            Icon(Symbol.Music, 11, 11, 21, TextMuted, music.transform);
            ButtonCaption(g.MusicLabel, 33, 3, 108, 36, 11, TextMuted, TextAnchor.MiddleCenter, music.transform);

            ButtonCaption("ZOMBIE SURVIVAL", 17, 68, 280, 49, 29, Color.white, TextAnchor.MiddleLeft, content);
            ButtonCaption("RUN", 288, 68, 85, 49, 35, style.Accent, TextAnchor.MiddleRight, content);
            Block("hero portrait frame", 15, 123, 360, 369, Color.Lerp(Dark, style.Accent, .25f));
            Image(survivorsArt ?? heroCovers[selected] ?? (selected == 0 ? artMax : artEva), 15, 123, 360, 369);
            Block("hero nameplate panel", 24, 421, 342, 61, new Color(.035f,.055f,.079f,.94f));
            ButtonCaption("МАКС И ЕВА", 37, 425, 203, 30, 21, Color.white, TextAnchor.MiddleLeft, content);
            Label("В забеге  •  " + heroName, 38, 452, 203, 23, 13, TextMuted, TextAnchor.MiddleLeft, false);
            Button("СМЕНИТЬ", 251, 429, 106, 45, Cyan, g.GoHeroes, 12, skinTint: new Color(.14f,.20f,.26f));

            Block("mission card", 15, 504, 360, 229, new Color(.068f,.093f,.119f,.99f));
            Label(g.Level <= 15 ? style.Title.ToUpperInvariant() : "РАЙОН " + (g.Plan.Sector + 1) + " / " + ZSRLevelPlan.SectorCount,
                30, 514, 215, 21, 11, style.Accent, TextAnchor.MiddleLeft);
            Label("УР. " + g.Level + " / " + ZSRGame.MaxLevels, 239, 514, 119, 21, 12, TextMuted, TextAnchor.MiddleRight);
            ButtonCaption(g.LevelTitle, 29, 539, 332, 45, 21, Color.white, TextAnchor.MiddleLeft, content);
            string mode = g.Level <= g.ClearedLevel ? "Повторная вылазка" : "Новый рубеж";
            Label(g.IsBossLevel ? mode + "  •  Босс" : mode, 30, 577, 330, 21, 13, TextMuted, TextAnchor.MiddleLeft, false);
            var loadout = ButtonSurface("СНАРЯЖЕНИЕ", 27, 602, 335, 44, Cyan, g.GoArsenal,
                skinTint: new Color(.105f,.15f,.19f));
            Icon(Symbol.Arsenal, 9, 10, 22, Cyan, loadout.transform);
            ButtonCaption(WeaponNames[(int)g.Weapon], 40, 3, 142, 35, 12, Color.white, TextAnchor.MiddleLeft, loadout.transform);
            Label("Ранг " + g.WeaponLevel, 183, 3, 111, 35, 12, TextMuted, TextAnchor.MiddleRight, false, loadout.transform);
            Icon(Symbol.Chevron, 306, 13, 16, TextMuted, loadout.transform);

            bool continuing = g.ClearedLevel > 0 && g.Level == g.ClearedLevel + 1;
            var play = Button(continuing ? "ПРОДОЛЖИТЬ" : "ИГРАТЬ", 27, 653, 211, 62, Orange,
                () => g.StartLevel(g.Level), continuing ? 18 : 20);
            firstButton = play;
            var bonuses = ButtonSurface("БОНУСЫ", 245, 653, 117, 62, Cyan, g.GoBonuses,
                skinTint: new Color(.13f,.20f,.26f));
            Icon(Symbol.Gate, 10, 19, 23, Cyan, bonuses.transform);
            ButtonCaption("БОНУСЫ", 37, 7, 73, 47, 13, Color.white, TextAnchor.MiddleLeft, bonuses.transform);
        }

        void Heroes(ZSRGame g)
        {
            TitleBar("ПЕРСОНАЖИ", g);
            for (int index = 0; index < 2; index++)
            {
                int chosen = index;
                bool active = g.SelectedHero == chosen;
                string name = chosen == 0 ? "МАКС" : "ЕВА";
                var portrait = ButtonSurface(name, 15 + index * 184, 110, 176, 337, Cyan, () => g.SelectHero(chosen),
                    skinTint: active ? new Color(.29f,.21f,.14f) : new Color(.10f,.15f,.20f));
                Image(chosen == 0 ? artMax : artEva, 7, 8, 162, 232, true, portrait.transform);
                ButtonCaption(name, 12, 247, 152, 30, 21, Color.white, TextAnchor.MiddleLeft, portrait.transform);
                Label(chosen == 0 ? "Штурмовик" : "Разведчица", 13, 278, 149, 22, 13, TextMuted,
                    TextAnchor.MiddleLeft, false, portrait.transform);
                Label(active ? "✓ В ЗАБЕГЕ" : "ВЫБРАТЬ  ›", 13, 305, 151, 23, 12, active ? Gold : Cyan,
                    TextAnchor.MiddleLeft, true, portrait.transform);
            }

            bool max = g.SelectedHero == 0;
            Block("hero loadout card", 15, 464, 360, 265, Panel);
            ButtonCaption("Параметры бойца", 29, 479, 334, 30, 19, Color.white, TextAnchor.MiddleLeft, content);
            Label("ЗДОРОВЬЕ", 31, 525, 154, 21, 11, TextMuted, TextAnchor.MiddleLeft);
            Label("ТЕМП СТРЕЛЬБЫ", 203, 525, 151, 21, 11, TextMuted, TextAnchor.MiddleLeft);
            // Use the same weapon profile the combat loop reads for the selected survivor.
            float rate = 1f / ZSRWeaponProfile.For(g.Weapon).ShotInterval(g.SelectedHero);
            ButtonCaption((max ? 132 : 112) + " HP", 29, 547, 157, 35, 23, Color.white, TextAnchor.MiddleLeft, content);
            ButtonCaption(rate.ToString("0.#", Russian) + " / с", 201, 547, 150, 35, 23, Color.white, TextAnchor.MiddleLeft, content);
            Block("stat divider", 30, 596, 328, 1, new Color(.22f,.29f,.35f));
            Label(max ? "У Макса на 20 HP больше, чем у Евы." : "Ева делает больше выстрелов в секунду из того же оружия.",
                30, 605, 329, 49, 15, TextMuted, TextAnchor.MiddleLeft, false);
            Button("ОТКРЫТЬ АРСЕНАЛ", 29, 662, 332, 50, Cyan, g.GoArsenal, 13);
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
                bool inspecting = inspectedWeapon == chosen;
                var slot = ButtonSurface(WeaponNames[i], x, 348, 83, 115, Cyan, () => inspectedWeapon = chosen,
                    skinTint: Color.Lerp(Panel, tint, inspecting ? .32f : .10f));
                Image(weaponArts[i], 4, 9, 75, 49, false, slot.transform, open ? 1f : .54f);
                ButtonCaption(WeaponNames[i], 2, 61, 79, 23, 10, Color.white, TextAnchor.MiddleCenter, slot.transform);
                string availability = equipped ? "ВЫБРАНО" : g.WeaponOwned(chosen) ? "В НАЛИЧИИ" :
                    open ? "● " + Money(g.WeaponPrice(chosen)) : "ОТ УР. " + g.WeaponUnlockLevel(chosen);
                Label(availability, 2, 87, 79, 22, 10, equipped ? Gold : TextMuted, parent: slot.transform);
                if (inspecting) Block("selected weapon", 24, 110, 35, 3, tint, slot.transform);
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
            Block("weapon showcase", 13, 139, 364, 194, Color.Lerp(Panel, accent, .19f));
            Accent(13, 139, 364, accent);
            Image(weaponArts[(int)weapon] ?? (weapon == WeaponType.Rifle ? artRifle : null), 25, 166, 340, 122, false);
            Label(WeaponNames[(int)weapon], 23, 146, 343, 29, 20, Color.white);
            Label(data.Role, 23, 301, 343, 22, 13, accent, TextAnchor.MiddleCenter, false);

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
                if (i<itemArts.Length && itemArts[i]!=null)
                    Image(itemArts[i],x+47,y-2,80,66,false);
                else Icon((Symbol)((int)Symbol.Medkit + i), x + 68, y + 15, 37, tint);
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
            damageLeft = Block("left damage feedback", 0, 112, 7, 605, Color.clear).GetComponent<UnityEngine.UI.Image>();
            damageRight = Block("right damage feedback", 383, 112, 7, 605, Color.clear).GetComponent<UnityEngine.UI.Image>();
            Block("HUD top", 0, 0, W, 107, new Color(.026f, .041f, .061f, .87f));
            Accent(9, 104, 372, Orange);
            Label("УРОВЕНЬ " + g.Level + " / " + ZSRGame.MaxLevels, 62, 13, 210, 31, 16, Color.white);
            hudCoins = Label("●", 275, 14, 98, 32, 15, Gold);
            Button("Ⅱ", 9, 10, 48, 46, Cyan, g.TogglePause, 18, g.IsRunning, redraw: false);
            Block("run progression bg", 78, 59, 289, 8, new Color(.20f, .25f, .28f, 1));
            progressFill = Block("run progression", 78, 59, 1, 8, Green);
            hudHealth = Label("HP", 12, 78, 140, 30, 15, Color.white, TextAnchor.MiddleLeft);
            Block("health bar", 145, 88, 222, 10, new Color(.34f, .20f, .20f, 1));
            hpTrail = Block("health damage trail", 145, 88, 222, 10, new Color(.96f,.30f,.16f));
            shownHealth = g.Hero.Health / (float)Mathf.Max(1, g.Hero.MaxHealth);
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
            noticeBackdrop = Block("notice contrast panel", 36, 169, 318, 32, new Color(.025f,.043f,.064f,.91f));
            hudMessage = Label("", 40, 172, 310, 27, 14, Gold);
            lastPaceSeconds = -1;
            float bottom = H - 107;
            Block("HUD bottom", 0, bottom - 15, W, 122, new Color(.026f, .041f, .061f, .85f));
            Accent(9, bottom - 15, 372, Orange);
            hudPace = Label("", 14, bottom - 7, 260, 17, 10, Cyan, TextAnchor.MiddleLeft);
            paceFill = Block("pace ready", 289, bottom + 2, 84, 3, Cyan);
            Block("weapon HUD card",6,bottom+16,146,80,new Color(.07f,.10f,.13f,.98f));
            ButtonCaption(WeaponNames[(int)g.Weapon],13,bottom+17,133,18,10,TextMuted,TextAnchor.MiddleLeft,content);
            Image(weaponArts[(int)g.Weapon],13,bottom+34,130,38,false);
            hudAmmo = ButtonCaption("",13,bottom+69,134,23,15,Color.white,TextAnchor.MiddleLeft,content);
            hudMed = Ability(ItemType.Medkit,"АПТЕЧКА",158,bottom+16,72,Cyan,g.UseMedkit,g.IsRunning);
            hudGrenade = Ability(ItemType.Grenade,"ГРАНАТА",236,bottom+16,72,Orange,g.ThrowGrenade,g.IsRunning);
            hudDrone = Ability(ItemType.Drone,"ДРОН",314,bottom+16,72,Cyan,g.UseDrone,g.IsRunning);
            UpdateHUD(g);
        }

        Text Ability(ItemType item, string caption, float x, float y, float width, Color tint,
            Action action, bool enabled)
        {
            var button = ButtonSurface(caption,x,y,width,80,tint,action,enabled,false,
                skinTint:new Color(.10f,.15f,.19f));
            itemButtons[(int)item] = button;
            if (itemArts[(int)item]!=null) Image(itemArts[(int)item],1,6,width-2,57,false,button.transform);
            else Icon((Symbol)((int)Symbol.Medkit+(int)item),22,20,28,Color.white,button.transform);
            Block("item quantity badge",width-29,3,26,18,new Color(.025f,.043f,.060f,.95f),button.transform);
            ButtonCaption(caption,2,61,width-4,16,9,Color.white,TextAnchor.MiddleCenter,button.transform);
            return Label("",width-29,2,26,19,11,Gold,parent:button.transform);
        }

        void UpdateHUD(ZSRGame g)
        {
            if (hudAmmo == null) return;
            hudCoins.text = "● " + g.Earned;
            hudHealth.text = "ЗДОРОВЬЕ " + g.Hero.Health;
            hudAmmo.text = g.IsReloading ? "ПЕРЕЗАРЯДКА" : g.Ammo + " / " + g.Magazine;
            hudMessage.text = g.CurrentNotice;
            bool acidHit = g.LastDamageKind == ZSRDamageKind.Acid;
            Color damageColor = acidHit ? new Color(.66f, .9f, .17f) : new Color(1f, .21f, .12f);
            float intensity = Mathf.Clamp01(g.DamageFlash / .65f);
            damageColor.a = intensity * .85f;
            damageLeft.color = g.DamageSourceX <= g.Hero.PositionX ? damageColor : new Color(0, 0, 0, 0);
            damageRight.color = g.DamageSourceX >= g.Hero.PositionX ? damageColor : new Color(0, 0, 0, 0);
            hudHealth.color = Color.white;
            hudMessage.color = intensity > 0f ? acidHit ? new Color(.84f,1f,.44f) : new Color(1f,.83f,.64f) : Gold;
            if (noticeBackdrop!=null) noticeBackdrop.gameObject.SetActive(!string.IsNullOrEmpty(g.CurrentNotice));
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
            hudMed.text = g.Medkits.ToString();
            hudGrenade.text = g.Grenades.ToString();
            hudDrone.text = g.DroneSeconds > 0f ? Mathf.CeilToInt(g.DroneSeconds) + "с" : g.Drones.ToString();
            itemButtons[0].interactable = g.IsRunning && g.Medkits>0 && g.Hero.Health<g.Hero.MaxHealth && !g.Hero.IsUsingItem;
            itemButtons[1].interactable = g.IsRunning && g.Grenades>0 && !g.Hero.IsUsingItem;
            itemButtons[2].interactable = g.IsRunning && g.Drones>0;
            if (progressFill != null) progressFill.sizeDelta = new Vector2(289 * g.Progress, 8);
            float health = g.Hero.Health / (float)Mathf.Max(1, g.Hero.MaxHealth);
            if (health >= shownHealth) shownHealth = health;
            else if (g.DamageFlash < .43f && g.IsRunning)
                shownHealth = Mathf.MoveTowards(shownHealth, health, Time.unscaledDeltaTime*.9f);
            if (hpTrail != null) hpTrail.sizeDelta = new Vector2(222f*shownHealth,10);
            if (hpFill != null) hpFill.sizeDelta = new Vector2(222f*health,10);
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

        void Dialogue(ZSRGame g)
        {
            BlockStretch("dialogue base", Dark, content);
            Image(survivorsArt, 15, 12, 360, 400, true);
            Label("УРОВЕНЬ " + g.Level + " / " + ZSRGame.MaxLevels, 26, 24, 337, 26, 13,
                Color.white, TextAnchor.UpperRight);
            const float top = 397f;
            Block("dialogue panel", 15, top, 360, 432, new Color(.045f,.067f,.10f,.99f));
            int speaker = g.DialogueIndex;
            string name = speaker == 0 ? "МАКС" : "ЕВА";
            Color tint = speaker == 0 ? Orange : Cyan;
            Image(Resources.Load<Texture2D>("UI/dialogue_face_" + (speaker == 0 ? "max" : "eva")),
                31, top + 17, 68, 68, false);
            ButtonCaption(name, 114, top + 19, 231, 32, 22, tint, TextAnchor.MiddleLeft, content);
            Label("БРАТ И СЕСТРА  •  " + (speaker + 1) + " / 2", 115, top + 53, 226, 23, 11,
                TextMuted, TextAnchor.MiddleLeft);
            Label((g.Briefing.Title ?? "").ToUpperInvariant(), 31, top + 101, 328, 27, 17,
                Gold, TextAnchor.MiddleLeft);
            Label(g.Briefing.Line(speaker), 31, top + 143, 328, 128, 18,
                Color.white, TextAnchor.UpperLeft, false);
            Button(speaker == 0 ? "ДАЛЕЕ  ›" : "НАЧАТЬ ЗАБЕГ", 31, top + 294, 328, 60,
                Orange, g.AdvanceDialogue, 18);
            Button("В МЕНЮ", 31, top + 367, 328, 46, Cyan, g.GoMenu, 14);
            if (firstButton != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(firstButton.gameObject);
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
