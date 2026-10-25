// On-screen controls for phones and tablets: a movement stick on the left and action
// buttons on the right. Only shown on touch devices, and only during a run.
using UnityEngine;
using UnityEngine.UI;

namespace Rigfall.Runtime
{
    public sealed class TouchControls : MonoBehaviour
    {
        RectTransform root;

        public void Build(RectTransform layer)
        {
            root = UIKit.Rect("Touch", layer);
            UIKit.Stretch(root);

            var stickBg = UIKit.Panel(root, new Color(1f, 1f, 1f, 0.12f));
            stickBg.sprite = UIKit.Circle;
            UIKit.Place(stickBg.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), new Vector2(220f, 260f), new Vector2(260f, 260f));
            var knob = UIKit.Panel(stickBg.transform, new Color(1f, 1f, 1f, 0.35f));
            knob.sprite = UIKit.Circle;
            UIKit.Place(knob.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110f, 110f));
            stickBg.gameObject.AddComponent<TouchJoystick>().knob = knob.rectTransform;

            Button("ATTACK", new Vector2(-170f, 200f), 150f, Palette.Accent, v => InputReader.TouchAttack = v);
            Button("ABILITY", new Vector2(-340f, 150f), 110f, Palette.Friendly, v => InputReader.TouchAbility = v);
            Button("DASH", new Vector2(-170f, 380f), 100f, Palette.PanelLight, v => InputReader.TouchDash = v);
            Button("BUILD", new Vector2(-350f, 320f), 110f, Palette.Barrel, v => InputReader.TouchInteract = v);
            Button("NEXT", new Vector2(-470f, 230f), 80f, Palette.PanelLight, v => InputReader.TouchCycle = v);
            Button("CREW", new Vector2(-480f, 380f), 90f, Palette.Heal, v => InputReader.TouchCommand = v);
            Button("NIGHT", new Vector2(-170f, 540f), 90f, Palette.Danger, v => InputReader.TouchNight = v);
            root.gameObject.SetActive(false);
        }

        void Button(string label, Vector2 pos, float size, Color color, System.Action<bool> set)
        {
            var img = UIKit.Panel(root, new Color(color.r, color.g, color.b, 0.55f));
            img.sprite = UIKit.Circle;
            UIKit.Place(img.rectTransform, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            var t = UIKit.Label(img.transform, label, 18, Color.white, TextAnchor.MiddleCenter, UIKit.BoldFont);
            UIKit.Stretch(t.rectTransform);
            img.gameObject.AddComponent<TouchButton>().onChange = set;
        }

        void Update()
        {
            bool show = (Input.touchSupported || Application.isMobilePlatform) && Run.Current != null && Game.UI != null && !Game.UI.BlocksInput;
            if (root.gameObject.activeSelf != show)
            {
                root.gameObject.SetActive(show);
                if (!show)
                {
                    InputReader.TouchMove = Vector2.zero;
                    InputReader.TouchAttack = InputReader.TouchAbility = InputReader.TouchDash = false;
                    InputReader.TouchInteract = InputReader.TouchCycle = InputReader.TouchCommand = InputReader.TouchNight = false;
                }
            }
        }
    }
}
