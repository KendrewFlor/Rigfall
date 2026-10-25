// Settings: graphics (Low/Medium/High), audio (five volume groups, jukebox override,
// dynamic music, mute when unfocused, 3D sound), gameplay and accessibility options,
// a controls reference, and resetting progress.
using Rigfall.Core;
using UnityEngine;

namespace Rigfall.Runtime
{
    public sealed class SettingsScreen : UiScreen
    {
        enum Tab { Graphics, Audio, Gameplay, Controls, Data }
        Tab tab = Tab.Graphics;
        RectTransform tabs, content;

        static GameSettings S => Game.Settings;

        protected override void Build(RectTransform root)
        {
            var card = Card(root, 1300f, 900f);
            var title = UIKit.Title(card, "SETTINGS", 46, Palette.Accent, TextAnchor.UpperLeft);
            UIKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -18f), new Vector2(700f, 60f));
            var back = UIKit.Button(card, "Done", () => Game.UI.Close(this), UIKit.Style.Primary, 22);
            UIKit.Place((RectTransform)back.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -22f), new Vector2(160f, 50f));
            FirstSelected = back;

            var tabRow = UIKit.HStack(card, 10, 0, "Tabs");
            tabs = (RectTransform)tabRow.transform;
            UIKit.Place(tabs, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -90f), new Vector2(1244f, 54f));
            tabRow.childForceExpandWidth = true;

            var area = UIKit.Rect("Area", card);
            UIKit.Stretch(area, 28f);
            area.offsetMax = new Vector2(-28f, -160f);
            content = UIKit.ScrollList(area, 6f);
            UIKit.Stretch((RectTransform)content.parent);
            Refresh();
        }

        void Refresh()
        {
            foreach (Transform c in tabs) Object.Destroy(c.gameObject);
            foreach (Tab t in System.Enum.GetValues(typeof(Tab)))
            {
                var tt = t;
                var b = UIKit.Button(tabs, t.ToString(), () => { tab = tt; Refresh(); }, tab == t ? UIKit.Style.Primary : UIKit.Style.Normal, 22);
                UIKit.Size(b, -1, 50, 1);
            }
            foreach (Transform c in content) Object.Destroy(c.gameObject);
            switch (tab)
            {
                case Tab.Graphics: Graphics(); break;
                case Tab.Audio: Audio(); break;
                case Tab.Gameplay: Gameplay(); break;
                case Tab.Controls: Controls(); break;
                case Tab.Data: Data(); break;
            }
        }

        static string Pct(float v) => Mathf.RoundToInt(v * 100f) + "%";
        static float Step(float v, float d) => Mathf.Clamp01(Mathf.Round((v + d) * 20f) / 20f);
        static string OnOff(bool b) => b ? "On" : "Off";

        void Changed()
        {
            GraphicsApplier.Apply(S);
            Game.SaveProfile();
        }

        void Graphics()
        {
            UIKit.Stepper(content, "Graphics quality", () => GraphicsApplier.Names[S.graphicsQuality],
                () => { S.graphicsQuality = (S.graphicsQuality + 2) % 3; Changed(); }, () => { S.graphicsQuality = (S.graphicsQuality + 1) % 3; Changed(); },
                "Low: no shadows, fewer particles and props. Medium: hard shadows. High: soft shadows, 4x anti-aliasing, all particles. Props apply from the next field you load.");
            UIKit.Stepper(content, "Frame-rate cap", () => S.fpsCap + " fps",
                () => { S.fpsCap = S.fpsCap == 120 ? 60 : 30; Changed(); }, () => { S.fpsCap = S.fpsCap == 30 ? 60 : 120; Changed(); });
            UIKit.Stepper(content, "Screen shake", () => Pct(S.screenShake),
                () => { S.screenShake = Step(S.screenShake, -0.1f); Changed(); }, () => { S.screenShake = Step(S.screenShake, 0.1f); Changed(); });
            UIKit.Stepper(content, "Camera distance", () => Pct(S.cameraZoom),
                () => { S.cameraZoom = Mathf.Max(0.7f, S.cameraZoom - 0.05f); Changed(); }, () => { S.cameraZoom = Mathf.Min(1.4f, S.cameraZoom + 0.05f); Changed(); });
            UIKit.Stepper(content, "Interface size", () => Pct(S.uiScale),
                () => { S.uiScale = Mathf.Max(0.8f, S.uiScale - 0.05f); Changed(); }, () => { S.uiScale = Mathf.Min(1.3f, S.uiScale + 0.05f); Changed(); },
                "Text size applies to screens opened after the change.");
            UIKit.Stepper(content, "Damage numbers", () => OnOff(S.showDamageNumbers),
                () => { S.showDamageNumbers = !S.showDamageNumbers; Changed(); }, () => { S.showDamageNumbers = !S.showDamageNumbers; Changed(); });
        }

        void Audio()
        {
            Volume("Master volume", () => S.masterVolume, v => S.masterVolume = v);
            Volume("Music", () => S.musicVolume, v => S.musicVolume = v);
            Volume("Sound effects", () => S.sfxVolume, v => S.sfxVolume = v);
            Volume("Ambience", () => S.ambienceVolume, v => S.ambienceVolume = v);
            Volume("Interface sounds", () => S.uiVolume, v => S.uiVolume = v);
            var tracks = Game.Db.Music;
            UIKit.Stepper(content, "Music track", () =>
                {
                    if (string.IsNullOrEmpty(S.musicTrack)) return "Automatic";
                    var t = tracks.Find(x => x.id == S.musicTrack);
                    return t != null ? t.title : "Automatic";
                },
                () => CycleTrack(-1), () => CycleTrack(1),
                "Automatic follows the field and the time of day. Pick a track to hear it everywhere (the Jukebox in Extras lets you preview).");
            UIKit.Stepper(content, "Dynamic music", () => OnOff(S.dynamicMusic),
                () => { S.dynamicMusic = !S.dynamicMusic; Changed(); }, () => { S.dynamicMusic = !S.dynamicMusic; Changed(); },
                "The night music swells as more enemies reach the pad.");
            UIKit.Stepper(content, "3D sound", () => OnOff(S.spatialAudio),
                () => { S.spatialAudio = !S.spatialAudio; Changed(); }, () => { S.spatialAudio = !S.spatialAudio; Changed(); });
            UIKit.Stepper(content, "Mute in background", () => OnOff(S.muteWhenUnfocused),
                () => { S.muteWhenUnfocused = !S.muteWhenUnfocused; Changed(); }, () => { S.muteWhenUnfocused = !S.muteWhenUnfocused; Changed(); });
        }

        void Volume(string label, System.Func<float> get, System.Action<float> set)
        {
            UIKit.Stepper(content, label, () => Pct(get()),
                () => { set(Step(get(), -0.05f)); Changed(); AudioService.Ui("sfx_coin", 1f, 1f); },
                () => { set(Step(get(), 0.05f)); Changed(); AudioService.Ui("sfx_coin", 1f, 1f); });
        }

        void CycleTrack(int dir)
        {
            var tracks = Game.Db.Music;
            int i = tracks.FindIndex(x => x.id == S.musicTrack); // -1 = automatic
            i += dir;
            if (i < -1) i = tracks.Count - 1;
            if (i >= tracks.Count) i = -1;
            S.musicTrack = i < 0 ? "" : tracks[i].id;
            Changed();
            if (i >= 0) AudioService.Preview(tracks[i].id);
        }

        void Gameplay()
        {
            UIKit.Stepper(content, "Hold to build", () => OnOff(S.holdToBuild),
                () => { S.holdToBuild = !S.holdToBuild; Changed(); }, () => { S.holdToBuild = !S.holdToBuild; Changed(); },
                "Off: buildings are bought the moment you press the build button.");
            UIKit.Stepper(content, "Auto-attack at night", () => OnOff(S.autoAttack),
                () => { S.autoAttack = !S.autoAttack; Changed(); }, () => { S.autoAttack = !S.autoAttack; Changed(); },
                "Your hero swings at enemies in range without holding the attack button. Good for one-handed or touch play.");
            UIKit.Stepper(content, "Show tower ranges", () => OnOff(S.showRanges),
                () => { S.showRanges = !S.showRanges; Changed(); }, () => { S.showRanges = !S.showRanges; Changed(); });
        }

        void Controls()
        {
            string[] lines =
            {
                "<b>Keyboard and mouse</b>",
                "Move: WASD or arrow keys",
                "Attack: Left mouse or J (hold to keep attacking; the mouse aims ranged weapons)",
                "Class ability: Right mouse or K",
                "Dash: Space or Left Shift",
                "Build / upgrade: stand on a slot and hold E",
                "Choose an upgrade option: Q or Tab, or the number keys 1-4",
                "Command units: hold F (nearby units follow), release F (they hold that spot); tap F to send them back to their posts",
                "Start the night: hold N",
                "Pause: Esc or P",
                "",
                "<b>Gamepad</b>",
                "Move: left stick · Attack: A · Ability: B · Dash: LB",
                "Build: hold X · Next option: Back/View · Command: hold RB · Night: hold Y · Pause: Start",
                "",
                "<b>Touch</b>",
                "Left stick to move, and the buttons on the right for everything else.",
            };
            foreach (var l in lines)
            {
                var t = UIKit.Label(content, l, 22, Palette.Ui, TextAnchor.MiddleLeft);
                UIKit.Size(t, -1, string.IsNullOrEmpty(l) ? 16 : 40);
            }
        }

        void Data()
        {
            var t = UIKit.Label(content, "Your progress is saved in this browser (or on this computer). Resetting deletes every unlock, record, weapon level and the saved run. It cannot be undone.", 22, Palette.UiDim, TextAnchor.UpperLeft);
            UIKit.Size(t, -1, 90);
            var b = UIKit.Button(content, "Reset all progress", () => Game.UI.Open(new ConfirmDialog("Reset all progress?",
                "Every unlock, record, weapon level, achievement and the saved run will be deleted.", "Delete everything", () =>
                {
                    SaveService.WipeEverything();
                    Ui.Toast("Progress reset. Restarting…");
                    Game.I.ResetProfile();
                }, true)), UIKit.Style.Danger);
            UIKit.Size(b, 420, 60);
        }
    }
}
