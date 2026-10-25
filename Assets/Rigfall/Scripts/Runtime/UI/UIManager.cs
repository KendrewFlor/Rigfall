// Owns the canvas and its layers (world labels, HUD, screens, overlays), the screen stack,
// and the small always-available widgets: build prompts, floating damage numbers, toasts,
// banners, achievement pop-ups, the tutorial coach, the boss bar and the hurt vignette.
using System.Collections.Generic;
using Rigfall.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rigfall.Runtime
{
    public sealed class UIManager : MonoBehaviour
    {
        public Canvas Canvas { get; private set; }
        public CanvasScaler Scaler { get; private set; }
        public Hud Hud { get; private set; }
        public RectTransform WorldLayer { get; private set; }
        public RectTransform HudLayer { get; private set; }
        public RectTransform ScreenLayer { get; private set; }
        public RectTransform OverlayLayer { get; private set; }

        readonly List<UiScreen> stack = new List<UiScreen>();
        public bool BlocksInput { get { foreach (var s in stack) if (s.BlocksInput) return true; return false; } }
        public bool HasScreen => stack.Count > 0;
        public UiScreen Top => stack.Count > 0 ? stack[stack.Count - 1] : null;

        // ---- creation ---------------------------------------------------------------------

        public static UIManager Create(Transform parent)
        {
            var go = new GameObject("UI", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var ui = go.AddComponent<UIManager>();
            ui.Canvas = go.AddComponent<Canvas>();
            ui.Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            ui.Canvas.sortingOrder = 10;
            ui.Scaler = go.AddComponent<CanvasScaler>();
            ui.Scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            ui.Scaler.referenceResolution = new Vector2(1920f, 1080f);
            ui.Scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            ui.Scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.transform.SetParent(parent, false);
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            ui.WorldLayer = UIKit.Stretch(UIKit.Rect("World", go.transform));
            ui.HudLayer = UIKit.Stretch(UIKit.Rect("HUD", go.transform));
            ui.ScreenLayer = UIKit.Stretch(UIKit.Rect("Screens", go.transform));
            ui.OverlayLayer = UIKit.Stretch(UIKit.Rect("Overlay", go.transform));
            ui.Hud = ui.HudLayer.gameObject.AddComponent<Hud>();
            ui.Hud.Build(ui.HudLayer);
            ui.BuildOverlay();
            ui.HudLayer.gameObject.AddComponent<TouchControls>().Build(ui.HudLayer);
            return ui;
        }

        // ---- screen stack -----------------------------------------------------------------

        public T Open<T>(T screen) where T : UiScreen
        {
            if (Top != null) Top.Root.gameObject.SetActive(false);
            screen.Create(ScreenLayer);
            stack.Add(screen);
            screen.OnShow();
            HidePromptNow();
            return screen;
        }

        public void Close(UiScreen screen)
        {
            int i = stack.IndexOf(screen);
            if (i < 0) return;
            stack.RemoveAt(i);
            screen.OnHide();
            screen.Destroy();
            if (Top != null) { Top.Root.gameObject.SetActive(true); Top.Select(); }
        }

        public void CloseAll()
        {
            for (int i = stack.Count - 1; i >= 0; i--) { stack[i].OnHide(); stack[i].Destroy(); }
            stack.Clear();
        }

        /// <summary>Escape/B pressed while a screen is open. Returns true if a screen handled it.</summary>
        public bool Back()
        {
            var top = Top;
            if (top == null) return false;
            if (top.OnBack()) { Close(top); AudioService.Ui("sfx_ui_back"); }
            return true;
        }

        // ---- overlay widgets ---------------------------------------------------------------

        Text toast, banner;
        Image toastBg, bannerBg, vignette, fade;
        float toastUntil, bannerUntil, vignetteAlpha;
        readonly Queue<string> toastQueue = new Queue<string>();
        RectTransform achvCard; Text achvTitle, achvBody; float achvUntil;
        readonly Queue<AchievementDef> achvQueue = new Queue<AchievementDef>();
        RectTransform coach; Text coachTitle, coachBody;
        RectTransform bossBar; Image bossFill; Text bossName; Enemy boss;

        void BuildOverlay()
        {
            vignette = UIKit.Panel(OverlayLayer, new Color(0.8f, 0.05f, 0.05f, 0f), false, "Vignette");
            UIKit.Stretch(vignette.rectTransform);
            vignette.raycastTarget = false;
            vignette.sprite = MakeVignette();

            toastBg = UIKit.Panel(OverlayLayer, new Color(0.92f, 0.95f, 0.95f, 0.95f));
            UIKit.Place(toastBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(760f, 56f));
            toast = UIKit.Label(toastBg.transform, "", 24, new Color(0.06f, 0.12f, 0.15f), TextAnchor.MiddleCenter, UIKit.BoldFont);
            UIKit.Stretch(toast.rectTransform, 8f);
            toastBg.gameObject.SetActive(false);

            bannerBg = UIKit.Panel(OverlayLayer, new Color(0f, 0f, 0f, 0.55f), false, "Banner");
            UIKit.Place(bannerBg.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2200f, 110f));
            banner = UIKit.Title(bannerBg.transform, "", 64, Color.white, TextAnchor.MiddleCenter);
            UIKit.Stretch(banner.rectTransform);
            bannerBg.gameObject.SetActive(false);

            var achvBg = UIKit.Panel(OverlayLayer, Palette.Hex("#FFB000"));
            achvCard = achvBg.rectTransform;
            UIKit.Place(achvCard, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(520f, 120f));
            var inner = UIKit.Panel(achvCard, Palette.Hex("#1D1402"));
            UIKit.Stretch(inner.rectTransform, 3f);
            achvTitle = UIKit.Label(inner.transform, "", 26, Palette.Hex("#FFD76A"), TextAnchor.UpperLeft, UIKit.BoldFont);
            UIKit.Stretch(achvTitle.rectTransform, 14f);
            achvBody = UIKit.Label(inner.transform, "", 20, Palette.Hex("#E8D9A8"), TextAnchor.LowerLeft);
            UIKit.Stretch(achvBody.rectTransform, 14f);
            achvCard.gameObject.SetActive(false);

            var coachBg = UIKit.Panel(OverlayLayer, Palette.Hex("#F2C200"));
            coach = coachBg.rectTransform;
            UIKit.Place(coach, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(900f, 150f));
            var coachInner = UIKit.Panel(coach, Palette.Hex("#F4F6F5"));
            UIKit.Stretch(coachInner.rectTransform, 4f);
            coachTitle = UIKit.Label(coachInner.transform, "", 26, Palette.Hex("#7A5B00"), TextAnchor.UpperLeft, UIKit.BoldFont);
            UIKit.Stretch(coachTitle.rectTransform, 14f);
            coachBody = UIKit.Label(coachInner.transform, "", 22, Palette.Hex("#102026"), TextAnchor.LowerLeft);
            UIKit.Stretch(coachBody.rectTransform, 14f);
            coachBody.rectTransform.offsetMax = new Vector2(-14f, -48f);
            coach.gameObject.SetActive(false);

            var bossBg = UIKit.Panel(OverlayLayer, new Color(0f, 0f, 0f, 0.7f));
            bossBar = bossBg.rectTransform;
            UIKit.Place(bossBar, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(900f, 46f));
            bossFill = UIKit.Panel(bossBar, Palette.Danger);
            bossFill.rectTransform.anchorMin = Vector2.zero;
            bossFill.rectTransform.anchorMax = new Vector2(1f, 1f);
            bossFill.rectTransform.offsetMin = new Vector2(4f, 4f);
            bossFill.rectTransform.offsetMax = new Vector2(-4f, -4f);
            bossName = UIKit.Title(bossBar, "", 26, Color.white, TextAnchor.MiddleCenter);
            UIKit.Stretch(bossName.rectTransform);
            bossBar.gameObject.SetActive(false);

            fade = UIKit.Panel(OverlayLayer, new Color(0f, 0f, 0f, 0f), false, "Fade");
            UIKit.Stretch(fade.rectTransform);
            fade.raycastTarget = false;

            BuildPrompt();
        }

        static Sprite MakeVignette()
        {
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x + 0.5f) / s - 0.5f, dy = (y + 0.5f) / s - 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                    px[y * s + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01((d - 0.55f) * 2.2f) * 255));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
        }

        public void Toast(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (toastBg.gameObject.activeSelf && Time.unscaledTime < toastUntil) { toastQueue.Enqueue(text); return; }
            ShowToastNow(text);
        }

        void ShowToastNow(string text)
        {
            toast.text = text;
            toastBg.gameObject.SetActive(true);
            toastUntil = Time.unscaledTime + 2.6f;
            Tweener.ScaleIn(toastBg.transform, 0.2f, true);
        }

        public void Banner(string text, Color color)
        {
            banner.text = text;
            banner.color = color;
            bannerBg.gameObject.SetActive(true);
            bannerUntil = Time.unscaledTime + 2.2f;
            Tweener.ScaleIn(bannerBg.transform, 0.3f, true);
        }

        public void Achievement(AchievementDef a)
        {
            achvQueue.Enqueue(a);
        }

        public void ShowCoach(TutorialStepDef step)
        {
            if (step == null) { coach.gameObject.SetActive(false); return; }
            coachTitle.text = "TUTORIAL · " + step.title;
            coachBody.text = step.text;
            coach.gameObject.SetActive(true);
            Tweener.ScaleIn(coach, 0.25f, true);
        }

        public void ShowBossBar(Enemy e)
        {
            boss = e;
            bossName.text = e.Def.name.ToUpperInvariant();
            bossBar.gameObject.SetActive(true);
            Tweener.ScaleIn(bossBar, 0.3f, true);
        }

        public void HideBossBar(Enemy e)
        {
            if (boss == e || e == null) { boss = null; if (bossBar != null) bossBar.gameObject.SetActive(false); }
        }

        public void HurtVignette() { vignetteAlpha = Mathf.Min(0.55f, vignetteAlpha + 0.3f); }

        public void FadeTo(float alpha, float seconds)
        {
            float from = fade.color.a;
            Tweener.Value(seconds, x => { var c = fade.color; c.a = Mathf.Lerp(from, alpha, x); fade.color = c; });
        }

        // ---- floating text & world labels -------------------------------------------------

        sealed class Floater { public Text text; public Vector3 world; public float born, life; }
        readonly List<Floater> floaters = new List<Floater>();
        readonly Stack<Text> floaterPool = new Stack<Text>();
        sealed class WorldLabelEntry { public GameObject go; public RectTransform rect; public Vector3 world; }
        readonly List<WorldLabelEntry> worldLabels = new List<WorldLabelEntry>();

        public void Floating(Vector3 world, string text, Color color, float scale)
        {
            if (!Game.Settings.showDamageNumbers && color != Palette.Crit && scale < 1.1f) return;
            if (floaters.Count > 60) return;
            var t = floaterPool.Count > 0 ? floaterPool.Pop() : UIKit.Label(WorldLayer, "", 30, Color.white, TextAnchor.MiddleCenter, UIKit.BoldFont, "Float");
            t.gameObject.SetActive(true);
            t.text = text;
            t.color = color;
            t.fontSize = Mathf.RoundToInt(30 * scale);
            t.rectTransform.sizeDelta = new Vector2(400f, 60f);
            var outline = t.GetComponent<Outline>();
            if (outline == null) { outline = t.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(0f, 0f, 0f, 0.75f); outline.effectDistance = new Vector2(2f, -2f); }
            floaters.Add(new Floater { text = t, world = world + Random.insideUnitSphere * 0.3f, born = Time.time, life = 0.9f });
        }

        public GameObject WorldLabel(Vector3 world, string text, Color color)
        {
            var bg = UIKit.Panel(WorldLayer, new Color(0f, 0f, 0f, 0.6f), true, "WorldLabel");
            bg.rectTransform.sizeDelta = new Vector2(220f, 40f);
            var t = UIKit.Label(bg.transform, text, 20, color, TextAnchor.MiddleCenter, UIKit.BoldFont);
            UIKit.Stretch(t.rectTransform, 4f);
            worldLabels.Add(new WorldLabelEntry { go = bg.gameObject, rect = bg.rectTransform, world = world });
            return bg.gameObject;
        }

        bool ToCanvas(Vector3 world, out Vector2 local)
        {
            local = Vector2.zero;
            var cam = Camera.main;
            if (cam == null) return false;
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z < 0f) return false;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(WorldLayer, sp, null, out local);
        }

        // ---- build prompt -----------------------------------------------------------------

        RectTransform prompt;
        Text promptTitle, promptBody, promptFooter;
        Image promptHold;
        BuildingSlot promptOwner;
        float promptSeen;

        void BuildPrompt()
        {
            var frame = UIKit.Panel(WorldLayer, Palette.Hazard);
            prompt = frame.rectTransform;
            prompt.pivot = new Vector2(0.5f, 0f);
            prompt.sizeDelta = new Vector2(560f, 230f);
            var inner = UIKit.Panel(prompt, new Color(0.07f, 0.13f, 0.16f, 0.96f));
            UIKit.Stretch(inner.rectTransform, 3f);
            promptTitle = UIKit.Label(inner.transform, "", 26, Palette.Hazard, TextAnchor.UpperLeft, UIKit.BoldFont);
            UIKit.Stretch(promptTitle.rectTransform, 14f);
            promptBody = UIKit.Label(inner.transform, "", 21, Palette.Ui, TextAnchor.UpperLeft);
            UIKit.Stretch(promptBody.rectTransform, 14f);
            promptBody.rectTransform.offsetMax = new Vector2(-14f, -50f);
            promptFooter = UIKit.Label(inner.transform, "", 19, Palette.UiDim, TextAnchor.LowerLeft);
            UIKit.Stretch(promptFooter.rectTransform, 14f);
            promptFooter.rectTransform.offsetMin = new Vector2(14f, 18f);
            var holdBg = UIKit.Panel(inner.transform, new Color(0f, 0f, 0f, 0.5f), false);
            UIKit.Place(holdBg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(520f, 8f));
            promptHold = UIKit.Panel(holdBg.transform, Palette.Barrel, false);
            promptHold.rectTransform.anchorMin = Vector2.zero;
            promptHold.rectTransform.anchorMax = new Vector2(0f, 1f);
            promptHold.rectTransform.offsetMin = Vector2.zero;
            promptHold.rectTransform.offsetMax = Vector2.zero;
            prompt.gameObject.SetActive(false);
        }

        /// <summary>Show the build/upgrade prompt for a slot. choices can be null (info only).</summary>
        public void ShowPrompt(BuildingSlot owner, string title, string subtitle, List<UpgradeChoice> choices, int selected, float hold)
        {
            if (BlocksInput) return;
            promptOwner = owner;
            promptSeen = Time.unscaledTime;
            var run = Run.Current;
            promptTitle.text = title;
            var sb = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(subtitle)) sb.Append(subtitle);
            if (choices != null)
            {
                for (int i = 0; i < choices.Count; i++)
                {
                    var c = choices[i];
                    bool afford = run != null && run.CanAfford(c.price);
                    string price = run != null && run.FreeUpgrades > 0 ? "FREE" : c.price + " bbl";
                    string line = (choices.Count > 1 ? "[" + (i + 1) + "] " : "") + c.option.name + "  " +
                                  UIKit.Colored(price, c.locked ? Palette.UiDim : afford ? Palette.Barrel : Palette.Hurt);
                    if (c.locked) line += "  " + UIKit.Colored("(" + c.reason + ")", Palette.UiDim);
                    if (i == selected) line = UIKit.Colored("▶ ", Palette.Hazard) + "<b>" + line + "</b>";
                    else line = "   " + line;
                    sb.Append(line).Append('\n');
                }
                if (selected >= 0 && selected < choices.Count) sb.Append(UIKit.Colored(choices[selected].option.description, Palette.UiDim));
            }
            promptBody.text = sb.ToString();
            int lines = 2 + (choices != null ? choices.Count + 1 : 1);
            prompt.sizeDelta = new Vector2(600f, 90f + lines * 27f);
            if (choices == null || choices.Count == 0) promptFooter.text = "";
            else
            {
                string key = InputReader.UsingTouch ? "Hold BUILD" : "Hold E";
                promptFooter.text = key + (Game.Settings.holdToBuild ? " to buy" : " to buy (instant)") + (choices.Count > 1 ? " · Q/Tab or 1-" + choices.Count + " to choose" : "");
            }
            promptHold.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(hold), 1f);
            if (!prompt.gameObject.activeSelf) { prompt.gameObject.SetActive(true); Tweener.ScaleIn(prompt, 0.18f, true); }
        }

        public void HidePrompt(BuildingSlot owner)
        {
            if (promptOwner == owner) HidePromptNow();
        }

        void HidePromptNow()
        {
            promptOwner = null;
            if (prompt != null) prompt.gameObject.SetActive(false);
        }

        // ---- per-frame --------------------------------------------------------------------

        void LateUpdate()
        {
            float now = Time.unscaledTime;
            Scaler.scaleFactor = 1f;
            Scaler.referenceResolution = new Vector2(1920f, 1080f) / Mathf.Max(0.5f, Game.Settings.uiScale);

            // Floating numbers rise and fade.
            for (int i = floaters.Count - 1; i >= 0; i--)
            {
                var f = floaters[i];
                float age = (Time.time - f.born) / f.life;
                Vector2 local;
                if (age >= 1f || f.text == null || !ToCanvas(f.world + Vector3.up * age * 1.4f, out local))
                {
                    if (f.text != null) { f.text.gameObject.SetActive(false); floaterPool.Push(f.text); }
                    floaters.RemoveAt(i);
                    continue;
                }
                f.text.rectTransform.anchoredPosition = local;
                var c = f.text.color;
                c.a = 1f - Mathf.Clamp01((age - 0.6f) / 0.4f);
                f.text.color = c;
                f.text.rectTransform.localScale = Vector3.one * (1f + 0.35f * Mathf.Clamp01(1f - age * 4f));
            }

            for (int i = worldLabels.Count - 1; i >= 0; i--)
            {
                var w = worldLabels[i];
                if (w.go == null) { worldLabels.RemoveAt(i); continue; }
                Vector2 local;
                bool vis = ToCanvas(w.world, out local);
                if (w.go.activeSelf != vis) w.go.SetActive(vis);
                if (vis) w.rect.anchoredPosition = local;
            }

            if (prompt.gameObject.activeSelf)
            {
                Vector2 local;
                if (promptOwner == null || now - promptSeen > 0.3f || BlocksInput) HidePromptNow();
                else if (ToCanvas(promptOwner.transform.position + Vector3.up * 3.2f, out local)) prompt.anchoredPosition = local;
            }

            if (toastBg.gameObject.activeSelf && now > toastUntil)
            {
                if (toastQueue.Count > 0) ShowToastNow(toastQueue.Dequeue());
                else toastBg.gameObject.SetActive(false);
            }
            if (bannerBg.gameObject.activeSelf && now > bannerUntil) bannerBg.gameObject.SetActive(false);

            if (achvCard.gameObject.activeSelf && now > achvUntil) achvCard.gameObject.SetActive(false);
            if (!achvCard.gameObject.activeSelf && achvQueue.Count > 0)
            {
                var a = achvQueue.Dequeue();
                achvTitle.text = "Achievement: " + a.name;
                achvBody.text = a.description + "\n" + UIKit.Colored(Rigfall.Core.Achievements.RewardText(a, Game.Db), Palette.Barrel);
                achvCard.gameObject.SetActive(true);
                achvUntil = now + 4f;
                Tweener.ScaleIn(achvCard, 0.3f, true);
                AudioService.Ui("sfx_achievement");
            }

            if (boss != null && boss.Health != null)
            {
                bossFill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.001f, boss.Health.Fraction), 1f);
                if (!boss.Alive) HideBossBar(boss);
            }
            else if (bossBar.gameObject.activeSelf) bossBar.gameObject.SetActive(false);

            if (vignetteAlpha > 0f)
            {
                vignetteAlpha = Mathf.MoveTowards(vignetteAlpha, 0f, Time.unscaledDeltaTime * 0.8f);
                var vc = vignette.color;
                vc.a = vignetteAlpha;
                vignette.color = vc;
            }
        }
    }
}
