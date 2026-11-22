using UnityEngine;
using UnityEngine.UI;

namespace ARTD
{
    /// <summary>Styles the existing HUD with standard uGUI components and the project's font.</summary>
    [DisallowMultipleComponent]
    public sealed class PirateHUD : MonoBehaviour
    {
        // 70% transparent = 30% opaque.
        static readonly Color Ink = new Color(15f / 255f, 24f / 255f, 30f / 255f, 0.3f);
        static readonly Color Wood = new Color32(24, 25, 28, 250);
        static readonly Color Brass = new Color32(182, 143, 80, 255);
        static readonly Color Cream = new Color32(247, 231, 195, 255);
        static readonly Color Muted = new Color32(192, 182, 157, 255);
        static readonly Color Teal = new Color32(72, 190, 169, 255);
        static readonly Color Red = new Color32(235, 100, 79, 255);

        [SerializeField] RectTransform safeArea, masthead, treasure, waveCard, ledger, command, toastCard, resultCard;
        [SerializeField] Text brand, phase, treasureLabel, waveLabel, ledgerLabel, commandTitle, footer, resultKicker, resultSummary;
        [SerializeField] Text hp, wave, score, budget, message, toast, result;
        [SerializeField] Button shutter, start, restart;
        [SerializeField] Text shutterHint, startHint, restartHint;
        [SerializeField] Image hpFill;
        [SerializeField] RectTransform hpTrack;
        [SerializeField] CanvasGroup toastGroup;
        [SerializeField] GameObject resultOverlay;
        [SerializeField] RectTransform actions;
        [SerializeField] Image swordsIcon, sharkIcon, skeletonIcon;
        [SerializeField] Text sharkCount, skeletonCount, waveCountdown;
        [SerializeField] CharacterStats[] sharkTypes;
        [SerializeField] CharacterStats skeletonType;
        Vector2 lastSize;
        Rect lastSafeArea;
        bool setup = true;
        bool paused;
        GameState previousState = GameState.Setup;
        float battleNoticeRemaining;
        float displayedHealth = 1f;

        public static PirateHUD Install(Transform root, Text hp, Text wave, Text score, Text budget,
            Text message, Text toast, Text result, Button shutter, Button start, Button restart, GameObject resultOverlay)
        {
            var view = root.GetComponent<PirateHUD>();
            if (!view) view = root.gameObject.AddComponent<PirateHUD>();
            if (view.safeArea) { view.Simplify(); return view; }
            view.hp = hp; view.wave = wave; view.score = score; view.budget = budget;
            view.message = message; view.toast = toast; view.result = result;
            view.shutter = shutter; view.start = start; view.restart = restart; view.resultOverlay = resultOverlay;
            view.Build(root, hp.font);
            view.Simplify();
            return view;
        }

        void Simplify()
        {
            EnsureSimpleLayout();
            foreach (var text in GetComponentsInChildren<Text>(true))
            {
                text.lineSpacing = 1f;
                text.verticalOverflow = VerticalWrapMode.Overflow;
            }
            masthead.gameObject.SetActive(false);
            treasureLabel.text = "宝箱 HP";
            waveLabel.gameObject.SetActive(false);
            ledgerLabel.text = "兵力";
            foreach (var label in new[] { treasureLabel, waveLabel, ledgerLabel })
            { label.fontSize = 21; label.color = Cream; }
            hp.fontSize = 32; wave.fontSize = 25; budget.fontSize = 32;
            message.fontSize = 28; toast.fontSize = 23;
            message.alignment = TextAnchor.MiddleLeft;
            message.supportRichText = false;
            score.text = ""; score.gameObject.SetActive(false);
            commandTitle.gameObject.SetActive(false);
            resultSummary.text = ""; resultSummary.gameObject.SetActive(false);
            result.fontSize = 32; resultSummary.fontSize = 24; resultKicker.fontSize = 30;
            shutterHint.text = "Enter"; startHint.text = "Space"; restartHint.text = "R";
            foreach (var hint in new[] { shutterHint, startHint, restartHint }) hint.fontSize = 18;
            foreach (var button in new[] { shutter, start, restart })
                button.GetComponentInChildren<Text>(true).fontSize = 28;
            shutter.GetComponentInChildren<Text>(true).text = "配置する";
            restart.GetComponentInChildren<Text>(true).text = "もう一度";
            foreach (var panel in new[] { treasure, waveCard, ledger, command, toastCard, resultCard })
            {
                EnsurePanelBorder(panel);
                var inlay = panel.Find("Inlay");
                if (inlay) inlay.GetComponent<Image>().color = Ink;
                var highlight = inlay ? inlay.Find("TopHighlight") : null;
                if (highlight) highlight.gameObject.SetActive(false);
                var shadow = panel.GetComponent<Shadow>();
                if (shadow) shadow.enabled = false;
            }
            // Only the command uses slanted sides; its old rectangular decorations stay hidden.
            command.Find("Border").gameObject.SetActive(false);
            command.Find("Inlay").gameObject.SetActive(false);
            foreach (Transform child in hpTrack)
                if (child.name == "Tick") child.gameObject.SetActive(false);
            Layout(lastSize.x > 0 ? lastSize : new Vector2(1280, 720));
        }

        void EnsureSimpleLayout()
        {
            if (!actions) actions = Rect("Actions", safeArea);
            shutter.transform.SetParent(actions, false); start.transform.SetParent(actions, false);
            shutterHint.transform.SetParent(actions, false); startHint.transform.SetParent(actions, false);
            footer.transform.SetParent(actions, false);
            footer.text = "マーカーを映して「配置する」を押そう";
            footer.alignment = TextAnchor.MiddleCenter; footer.fontSize = 21; footer.color = Cream;
            var banner = command.Find("SlantedBackground");
            if (!banner)
            {
                var shape = Rect("SlantedBackground", command).gameObject.AddComponent<SlantedPanel>();
                shape.color = Ink; shape.raycastTarget = false;
                Stretch(shape.rectTransform); shape.transform.SetAsFirstSibling();
            }
            if (!swordsIcon) swordsIcon = Fill("UI_Swords", ledger, Color.white);
            if (!sharkIcon) sharkIcon = Fill("Sharky", waveCard, Color.white);
            if (!skeletonIcon) skeletonIcon = Fill("Skeleton", waveCard, Color.white);
            foreach (var icon in new[] { swordsIcon, sharkIcon, skeletonIcon }) icon.preserveAspect = true;
            if (!sharkCount) sharkCount = Label("SharkCount", waveCard, hp.font, "×0", 30, Cream);
            if (!skeletonCount) skeletonCount = Label("SkeletonCount", waveCard, hp.font, "×0", 30, Cream);
            if (!waveCountdown) waveCountdown = Label("Countdown", waveCard, hp.font, "", 19, Muted);
            treasure.gameObject.SetActive(false); waveCard.gameObject.SetActive(false);
        }

        public void SetArt(Sprite swords, Sprite shark, Sprite skeleton, CharacterStats[] sharks, CharacterStats skull)
        {
            swordsIcon.sprite = swords; sharkIcon.sprite = shark; skeletonIcon.sprite = skeleton;
            sharkTypes = sharks; skeletonType = skull;
        }

        void Build(Transform root, Font font)
        {
            safeArea = Rect("PirateHUD", root);
            Stretch(safeArea);
            safeArea.SetAsFirstSibling();
            masthead = Rect("Masthead", safeArea);
            brand = Label("Brand", masthead, font, "BLACK TIDE  /  海賊防衛戦", 20, Cream);
            phase = Label("Phase", masthead, font, "01  /  配置準備", 17, Brass, TextAnchor.MiddleRight);
            var rule = Fill("BrassRule", masthead, Brass);
            rule.rectTransform.anchorMin = new Vector2(0, 0); rule.rectTransform.anchorMax = new Vector2(1, 0);
            rule.rectTransform.offsetMin = Vector2.zero; rule.rectTransform.offsetMax = new Vector2(0, 1);

            treasure = Panel("Treasure", safeArea, Wood);
            treasureLabel = Label("Caption", treasure, font, "TREASURE  /  宝箱の耐久", 14, Brass);
            Move(hp, treasure, 23, Cream);
            hpTrack = Fill("HealthTrack", treasure, new Color32(6, 13, 16, 255)).rectTransform;
            hpFill = Fill("HealthFill", hpTrack, Teal);
            Stretch(hpFill.rectTransform);
            // Separators are decorative; the continuous fill remains proportional to HP.
            for (int i = 1; i < 10; i++)
            {
                var tick = Fill("Tick", hpTrack, new Color32(6, 13, 16, 150)).rectTransform;
                tick.anchorMin = tick.anchorMax = new Vector2(i / 10f, .5f);
                tick.sizeDelta = new Vector2(2, 12);
            }

            waveCard = Panel("Wave", safeArea, Ink);
            waveLabel = Label("Caption", waveCard, font, "RAID  /  襲撃状況", 14, Brass);
            Move(wave, waveCard, 23, Cream);
            ledger = Panel("Ledger", safeArea, Ink);
            ledgerLabel = Label("Caption", ledger, font, "QUARTERMASTER  /  戦闘記録", 14, Brass);
            Move(budget, ledger, 21, Cream);
            Move(score, ledger, 17, Muted);

            command = Panel("CommandDeck", safeArea, Wood);
            commandTitle = Label("Caption", command, font, "船長の指令  /  PREPARE", 16, Brass);
            Move(message, command, 21, Cream);
            footer = Label("Footer", command, font, "宝箱を守り、すべての襲撃を退けろ。", 14, Muted);
            shutterHint = StyleButton(shutter, command, font, "陣地を固定", "ENTER  /  認識中のマーカーを配置", new Color32(30, 69, 71, 255));
            startHint = StyleButton(start, command, font, "戦闘開始", "SPACE  /  宝箱を配置すると出撃可能", new Color32(138, 54, 40, 255));

            toastCard = Panel("Signal", safeArea, Ink);
            toastGroup = toastCard.gameObject.AddComponent<CanvasGroup>();
            toastGroup.interactable = false; toastGroup.blocksRaycasts = false; toastGroup.alpha = 0;
            Move(toast, toastCard, 21, Cream, TextAnchor.MiddleCenter);
            Stretch(toast.rectTransform, 16);

            var dim = resultOverlay.GetComponent<Image>();
            if (dim) { dim.color = new Color32(5, 14, 19, 225); dim.raycastTarget = true; }
            resultCard = Panel("CaptainsReport", resultOverlay.transform, Wood);
            resultKicker = Label("Kicker", resultCard, font, "CAPTAIN'S REPORT", 20, Brass, TextAnchor.MiddleCenter);
            Move(result, resultCard, 42, Cream, TextAnchor.MiddleCenter);
            resultSummary = Label("Summary", resultCard, font, "", 22, Muted, TextAnchor.MiddleCenter);
            restartHint = StyleButton(restart, resultCard, font, "再び出撃する", "R  /  新たな防衛戦へ", new Color32(138, 54, 40, 255));
            resultOverlay.transform.SetAsLastSibling();
        }

        public void Refresh(GameState state, bool trackingPaused, bool hasTower, float healthRatio, EnemySpawner spawner, float toastRemaining)
        {
            bool isSetup = state == GameState.Setup;
            if (setup != isSetup) { setup = isSetup; lastSize = Vector2.zero; }
            paused = trackingPaused;
            if (state == GameState.Playing && previousState != GameState.Playing) battleNoticeRemaining = 4f;
            previousState = state;
            if (Application.isPlaying && !paused) battleNoticeRemaining = Mathf.Max(0, battleNoticeRemaining - Time.unscaledDeltaTime);
            message.color = paused ? Red : Cream;
            float target = hasTower ? healthRatio : 0f;
            displayedHealth = Application.isPlaying ? Mathf.MoveTowards(displayedHealth, target, Time.unscaledDeltaTime * 1.5f) : target;
            hpFill.rectTransform.anchorMax = new Vector2(displayedHealth, 1);
            hpFill.color = healthRatio <= .25f ? Red : Teal;
            hp.color = hasTower && healthRatio <= .25f ? Red : Cream;
            toastGroup.alpha = Mathf.Clamp01(toastRemaining * 4);
            bool ended = state == GameState.Victory || state == GameState.GameOver;
            command.gameObject.SetActive(!ended && (setup || paused || battleNoticeRemaining > 0));
            treasure.gameObject.SetActive(state == GameState.Playing);
            waveCard.gameObject.SetActive(state == GameState.Playing);
            ledger.gameObject.SetActive(setup);
            actions.gameObject.SetActive(setup);
            toastCard.gameObject.SetActive(!ended);
            int sharks = 0;
            if (spawner && sharkTypes != null)
                foreach (var type in sharkTypes) if (type) sharks += spawner.LivingCount(type);
            int skeletons = spawner && skeletonType ? spawner.LivingCount(skeletonType) : 0;
            sharkCount.text = $"×{sharks}"; skeletonCount.text = $"×{skeletons}";
            waveCountdown.text = spawner && spawner.NextWaveCountdown > 0
                ? $"次の襲撃まで {Mathf.CeilToInt(spawner.NextWaveCountdown)} 秒" : "";
            resultKicker.text = state == GameState.Victory ? "勝利！" : "敗北";
            resultKicker.color = state == GameState.Victory ? Brass : Red;
        }

        void LateUpdate()
        {
            if (!safeArea || Screen.width <= 0 || Screen.height <= 0) return;
            Rect safe = Screen.safeArea;
            if (lastSafeArea != safe)
            {
                safeArea.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
                safeArea.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
                safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
                lastSafeArea = safe;
            }
            var scaler = GetComponent<CanvasScaler>();
            Vector2 reference = Screen.height > Screen.width ? new Vector2(720, 1280) : new Vector2(1280, 720);
            if (scaler && scaler.referenceResolution != reference) scaler.referenceResolution = reference;
            Vector2 size = safeArea.rect.size;
            if (size.x > 0 && size.y > 0 && (size - lastSize).sqrMagnitude > 1) Layout(size);
        }

        // Also used by the editor preview to verify narrow and wide layouts.
        public void Layout(Vector2 size)
        {
            lastSize = size;
            bool narrow = size.x < 950;
            float margin = narrow ? 18 : 24;
            float width = size.x - margin * 2;
            float rightWidth = narrow ? 228 : 276;
            float leftWidth = Mathf.Min(width - rightWidth - 16, 600);
            float rightX = size.x - margin - rightWidth;
            Top(command, margin, setup ? margin : margin + 126, leftWidth, setup ? 104 : 90);
            Stretch(message.rectTransform);
            message.rectTransform.offsetMin = new Vector2(30, 10);
            message.rectTransform.offsetMax = new Vector2(-30, -10);
            message.fontSize = narrow ? 25 : 29;

            Top(treasure, margin, margin, Mathf.Min(leftWidth, 400), 112);
            float hpWidth = treasure.sizeDelta.x;
            Top(treasureLabel.rectTransform, 18, 10, hpWidth - 36, 30);
            Top(hp.rectTransform, 18, 42, hpWidth - 36, 48);
            Top(hpTrack, 18, 94, hpWidth - 36, 8);
            Top(ledger, rightX, margin, rightWidth, 104);
            Top(ledgerLabel.rectTransform, 18, 10, rightWidth - 36, 30);
            Top(swordsIcon.rectTransform, 14, 41, 54, 54);
            Top(budget.rectTransform, 78, 42, rightWidth - 90, 48);

            Top(waveCard, rightX, margin, rightWidth, 224);
            Top(wave.rectTransform, 18, 12, rightWidth - 36, 38);
            Top(sharkIcon.rectTransform, 18, 54, 62, 62);
            Top(sharkCount.rectTransform, 94, 54, rightWidth - 110, 62);
            Top(skeletonIcon.rectTransform, 18, 122, 62, 62);
            Top(skeletonCount.rectTransform, 94, 122, rightWidth - 110, 62);
            Top(waveCountdown.rectTransform, 18, 188, rightWidth - 36, 30);
            wave.fontSize = narrow ? 24 : 26;
            waveCountdown.fontSize = narrow ? 17 : 19;

            float actionsWidth = Mathf.Min(width, 540);
            Bottom(actions, (size.x - actionsWidth) / 2, margin, actionsWidth, 142);
            footer.gameObject.SetActive(true);
            Top(footer.rectTransform, 0, 0, actionsWidth, 34);
            float buttonWidth = (actionsWidth - 16) / 2;
            Top((RectTransform)shutter.transform, 0, 44, buttonWidth, 64);
            Top((RectTransform)start.transform, buttonWidth + 16, 44, buttonWidth, 64);
            Top(shutterHint.rectTransform, 0, 112, buttonWidth, 28);
            Top(startHint.rectTransform, buttonWidth + 16, 112, buttonWidth, 28);
            shutterHint.gameObject.SetActive(setup && !Application.isMobilePlatform); startHint.gameObject.SetActive(setup && !Application.isMobilePlatform);
            restartHint.gameObject.SetActive(!Application.isMobilePlatform);
            float noticeWidth = Mathf.Min(width - 24, 720);
            Bottom(toastCard, (size.x - noticeWidth) / 2, margin + (setup ? 156 : 0), noticeWidth, 62);
            float reportWidth = Mathf.Min(width - 12, 580);
            resultCard.anchorMin = resultCard.anchorMax = resultCard.pivot = new Vector2(.5f, .5f);
            resultCard.anchoredPosition = Vector2.zero;
            resultCard.sizeDelta = new Vector2(reportWidth, 300);
            Top(resultKicker.rectTransform, 24, 26, reportWidth - 48, 48);
            Top(result.rectTransform, 24, 86, reportWidth - 48, 70);
            result.fontSize = 28;
            Top((RectTransform)restart.transform, (reportWidth - 280) / 2, 188, 280, 64);
            Top(restartHint.rectTransform, 24, 260, reportWidth - 48, 30);
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
        static Image Fill(string name, Transform parent, Color color)
        {
            var image = Rect(name, parent).gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false; return image;
        }
        static RectTransform Panel(string name, Transform parent, Color color)
        {
            var frame = Fill(name, parent, Color.clear);
            EnsurePanelBorder(frame.rectTransform);
            var fill = Fill("Inlay", frame.transform, color);
            Stretch(fill.rectTransform, 2);
            var line = Fill("TopHighlight", fill.transform, new Color32(216, 177, 112, 50));
            line.rectTransform.anchorMin = new Vector2(0, 1); line.rectTransform.anchorMax = Vector2.one;
            line.rectTransform.pivot = new Vector2(.5f, 1); line.rectTransform.sizeDelta = new Vector2(-16, 1);
            line.rectTransform.anchoredPosition = new Vector2(0, -6);
            var shadow = frame.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, .35f); shadow.effectDistance = new Vector2(0, -4);
            return frame.rectTransform;
        }

        static void EnsurePanelBorder(RectTransform panel)
        {
            // The original frame was a solid gold rectangle behind the inlay.
            // Draw only its edges so the camera image can show through the center.
            var image = panel.GetComponent<Image>();
            if (image) image.color = Color.clear;
            if (panel.Find("Border")) return;
            var border = Rect("Border", panel);
            Stretch(border);
            border.SetAsFirstSibling();
            BorderEdge(border, "Top", new Vector2(0, 1), Vector2.one, new Vector2(0, 2), new Vector2(0, -1));
            BorderEdge(border, "Bottom", Vector2.zero, new Vector2(1, 0), new Vector2(0, 2), new Vector2(0, 1));
            BorderEdge(border, "Left", Vector2.zero, new Vector2(0, 1), new Vector2(2, -4), new Vector2(1, 0));
            BorderEdge(border, "Right", new Vector2(1, 0), Vector2.one, new Vector2(2, -4), new Vector2(-1, 0));
        }

        static void BorderEdge(Transform parent, string name, Vector2 min, Vector2 max, Vector2 size, Vector2 position)
        {
            var edge = Fill(name, parent, Brass).rectTransform;
            edge.anchorMin = min;
            edge.anchorMax = max;
            edge.sizeDelta = size;
            edge.anchoredPosition = position;
        }
        static Text Label(string name, Transform parent, Font font, string value, int size, Color color, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var text = Rect(name, parent).gameObject.AddComponent<Text>();
            text.font = font; text.text = value;
            Move(text, parent, size, color, alignment);
            return text;
        }
        static void Move(Text text, Transform parent, int size, Color color, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            text.transform.SetParent(parent, false);
            text.fontSize = size; text.fontStyle = FontStyle.Normal; text.color = color;
            text.alignment = alignment; text.lineSpacing = 1f;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextForBestFit = false; text.raycastTarget = false;
            foreach (var outline in text.GetComponents<Outline>()) outline.enabled = false;
        }
        static Text StyleButton(Button button, Transform parent, Font font, string title, string hint, Color fill)
        {
            button.transform.SetParent(parent, false);
            var image = button.GetComponent<Image>();
            image.sprite = null; image.type = Image.Type.Simple; image.color = fill;
            image.raycastTarget = true;
            var outline = button.GetComponent<Outline>();
            if (!outline) outline = button.gameObject.AddComponent<Outline>();
            outline.effectColor = Brass; outline.effectDistance = new Vector2(1.5f, -1.5f);
            var colors = button.colors;
            colors.normalColor = Color.white; colors.highlightedColor = new Color(1.22f, 1.18f, 1.12f);
            colors.pressedColor = new Color(.7f, .73f, .7f); colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(.38f, .38f, .38f, .8f); colors.fadeDuration = .12f;
            button.colors = colors; button.transition = Selectable.Transition.ColorTint;
            var label = button.GetComponentInChildren<Text>(true);
            Move(label, button.transform, 25, Cream, TextAnchor.MiddleCenter); label.text = title;
            Stretch(label.rectTransform, 6);
            return Label(button.name + "Hint", parent, font, hint, 12, Muted, TextAnchor.MiddleCenter);
        }
        static void Top(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
        }
        static void Bottom(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(w, h);
        }
        static void Stretch(RectTransform rt, float inset = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.one * inset; rt.offsetMax = Vector2.one * -inset;
        }
    }
}
