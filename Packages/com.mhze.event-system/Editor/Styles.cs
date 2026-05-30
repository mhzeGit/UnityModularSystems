using UnityEditor;
using UnityEngine;

namespace MHZE.EventSystem.Editor
{
    internal static class Styles
    {
        public static class Colors
        {
            public static readonly Color SurfaceCard = new Color(0.110f, 0.110f, 0.118f, 1f);
            public static readonly Color SurfaceParam = new Color(0.137f, 0.137f, 0.145f, 1f);
            public static readonly Color SurfaceHeader = new Color(0.082f, 0.082f, 0.086f, 1f);
            public static readonly Color SurfaceBindingBg = new Color(0.094f, 0.094f, 0.102f, 1f);

            public static readonly Color BorderCard = new Color(0.173f, 0.173f, 0.180f, 1f);
            public static readonly Color BorderCardLight = new Color(0.220f, 0.220f, 0.227f, 1f);
            public static readonly Color BorderAccent = new Color(0.271f, 0.271f, 0.278f, 1f);

            public static readonly Color TextPrimary = new Color(0.961f, 0.961f, 0.969f, 1f);
            public static readonly Color TextSecondary = new Color(0.596f, 0.596f, 0.616f, 1f);
            public static readonly Color TextMuted = new Color(0.388f, 0.388f, 0.400f, 1f);

            public static readonly Color Blue = new Color(0.039f, 0.518f, 1.000f, 1f);
            public static readonly Color Green = new Color(0.188f, 0.820f, 0.345f, 1f);
            public static readonly Color Orange = new Color(1.000f, 0.624f, 0.039f, 1f);
            public static readonly Color Red = new Color(1.000f, 0.271f, 0.227f, 1f);
            public static readonly Color Purple = new Color(0.749f, 0.353f, 0.949f, 1f);
            public static readonly Color Teal = new Color(0.353f, 0.784f, 0.980f, 1f);

            public static readonly Color BlueDim = new Color(0.039f, 0.518f, 1.000f, 0.3f);
            public static readonly Color OrangeDim = new Color(1.000f, 0.624f, 0.039f, 0.3f);
            public static readonly Color CardDim = new Color(0.110f, 0.110f, 0.118f, 0.5f);
            public static readonly Color BadgeBg = new Color(0.749f, 0.353f, 0.949f, 0.2f);
        }

        private static class Tex
        {
            private static Texture2D Make(Color color)
            {
                var tex = new Texture2D(1, 1);
                tex.SetPixel(0, 0, color);
                tex.Apply();
                tex.hideFlags = HideFlags.HideAndDontSave;
                return tex;
            }

            public static readonly Texture2D White = Make(Color.white);
            public static readonly Texture2D CardBg = Make(Colors.SurfaceCard);
            public static readonly Texture2D CardBgDim = Make(Colors.CardDim);
            public static readonly Texture2D HeaderBg = Make(Colors.SurfaceHeader);
            public static readonly Texture2D BindingBg = Make(Colors.SurfaceBindingBg);
            public static readonly Texture2D ParamBg = Make(Colors.SurfaceParam);
            public static readonly Texture2D Badge = Make(Colors.BadgeBg);
        }

        public static readonly GUIStyle BindingFoldout;
        public static readonly GUIStyle BindingLabel;
        public static readonly GUIStyle ListenerCountBadge;
        public static readonly GUIStyle AddButtonMain;
        public static readonly GUIStyle AddButtonFooter;

        public static readonly GUIStyle Card;
        public static readonly GUIStyle CardHeaderBg;
        public static readonly GUIStyle CardHeaderLabel;
        public static readonly GUIStyle CardHeaderLabelDim;
        public static readonly GUIStyle RemoveButton;

        public static readonly GUIStyle FieldLabel;
        public static readonly GUIStyle FieldLabelCompact;
        public static readonly GUIStyle PopupButton;

        public static readonly GUIStyle ParamHeaderLabel;
        public static readonly GUIStyle ParamSectionFoldout;

        public static readonly GUIStyle ToggleStyle;

        static Styles()
        {
            BindingFoldout = new GUIStyle(EditorStyles.foldout)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Colors.TextPrimary },
                onNormal = { textColor = Colors.TextPrimary },
                hover = { textColor = Colors.TextPrimary },
                onHover = { textColor = Colors.TextPrimary },
                active = { textColor = Colors.TextPrimary },
                padding = new RectOffset(16, 4, 2, 2),
                fixedHeight = 22,
                stretchWidth = true
            };

            BindingLabel = new GUIStyle
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Colors.TextPrimary },
                padding = new RectOffset(0, 0, 2, 2),
                alignment = TextAnchor.MiddleLeft,
                fixedHeight = 22
            };

            ListenerCountBadge = new GUIStyle
            {
                normal = { textColor = Colors.Purple },
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 0, 0),
                fixedHeight = 18
            };

            AddButtonMain = new GUIStyle
            {
                normal = { textColor = Colors.Blue },
                hover = { textColor = new Color(0.3f, 0.6f, 1f, 1f) },
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(8, 8, 0, 0),
                fixedHeight = 22
            };

            AddButtonFooter = new GUIStyle
            {
                normal = { background = Tex.BindingBg, textColor = Colors.TextMuted },
                hover = { background = Tex.BindingBg, textColor = Colors.Blue },
                fontSize = 11,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 0, 0),
                fixedHeight = 26,
                stretchWidth = true
            };

            Card = new GUIStyle
            {
                normal = { background = Tex.CardBg, textColor = Colors.TextPrimary },
                border = new RectOffset(1, 1, 1, 1),
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };

            CardHeaderBg = new GUIStyle
            {
                normal = { background = Tex.HeaderBg },
                border = new RectOffset(1, 1, 1, 1),
                padding = new RectOffset(6, 6, 3, 3),
                stretchWidth = true,
                fixedHeight = 24
            };

            CardHeaderLabel = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11,
                normal = { textColor = Colors.TextPrimary },
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(4, 4, 0, 0),
                richText = true
            };

            CardHeaderLabelDim = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                normal = { textColor = Colors.TextSecondary },
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(4, 4, 0, 0)
            };

            RemoveButton = new GUIStyle
            {
                normal = { textColor = Colors.TextMuted },
                hover = { textColor = Colors.Red },
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 0, 0),
                fixedWidth = 22,
                fixedHeight = 22
            };

            FieldLabel = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 10,
                normal = { textColor = Colors.TextMuted },
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(0, 4, 1, 1),
                fixedWidth = 56
            };

            FieldLabelCompact = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 10,
                normal = { textColor = Colors.TextMuted },
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(0, 4, 1, 1),
                fixedWidth = 44
            };

            PopupButton = new GUIStyle(EditorStyles.popup)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(6, 20, 1, 1),
                fixedHeight = 18
            };

            ParamHeaderLabel = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Colors.TextPrimary },
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(0, 0, 0, 0),
                richText = true
            };

            ParamSectionFoldout = new GUIStyle(EditorStyles.foldout)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Colors.TextSecondary },
                onNormal = { textColor = Colors.TextPrimary },
                hover = { textColor = Colors.TextPrimary },
                onHover = { textColor = Colors.TextPrimary },
                padding = new RectOffset(14, 0, 1, 1),
                fixedHeight = 18
            };

            ToggleStyle = new GUIStyle(EditorStyles.toggle)
            {
                padding = new RectOffset(18, 0, 0, 0),
                fixedWidth = 18,
                fixedHeight = 18,
                fontSize = 11
            };
        }

        public static void DrawCardBackground(Rect rect)
        {
            DrawRoundedRect(rect, Colors.SurfaceCard, Colors.BorderCard, 4);
        }

        public static void DrawCardBackgroundDim(Rect rect)
        {
            DrawRoundedRect(rect, Colors.CardDim, Colors.BorderCard, 4);
        }

        public static void DrawParamBackground(Rect rect)
        {
            DrawRoundedRect(rect, Colors.SurfaceParam, Colors.BorderCard, 3);
        }

        public static void DrawAccentStrip(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            var prevColor = Handles.color;
            Handles.color = color;
            var r = new Rect(rect.x, rect.y + 1, 3, rect.height - 2);
            Handles.DrawSolidRectangleWithOutline(r, color, Color.clear);
            Handles.color = prevColor;
        }

        internal static void DrawRoundedRect(Rect rect, Color fill, Color border, int radius)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            var prevColor = Handles.color;
            Handles.color = fill;
            Handles.DrawSolidRectangleWithOutline(rect, fill, border);
            Handles.color = prevColor;
        }

        public static void DrawHorizontalSeparator(Rect rect, Color color)
        {
            var prevColor = Handles.color;
            Handles.color = color;
            float y = rect.y + rect.height * 0.5f;
            Handles.DrawLine(new Vector3(rect.x, y), new Vector3(rect.xMax, y));
            Handles.color = prevColor;
        }

        public static void DrawBadge(Rect rect, string text, GUIStyle style)
        {
            var content = new GUIContent(text);
            var size = style.CalcSize(content);
            var badgeRect = new Rect(rect.x, rect.y + (rect.height - size.y) * 0.5f, size.x + 12, size.y);
            DrawRoundedRect(badgeRect, Colors.BadgeBg, Color.clear, 4);
            GUI.Label(badgeRect, content, style);
        }

        public static string GetIconString(string icon)
        {
            switch (icon)
            {
                case "bolt": return "\u26A1";
                case "check": return "\u2713";
                case "xmark": return "\u2715";
                case "arrow": return "\u2192";
                case "chevron": return "\u25B6";
                case "plus": return "+";
                case "circle": return "\u25CF";
                default: return icon;
            }
        }
    }
}
