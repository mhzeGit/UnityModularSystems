using UnityEditor;
using UnityEngine;

namespace MHZE.EventSystem.Editor
{
    internal static class Styles
    {
        public static readonly Color DarkCardBg = new Color(0.176f, 0.176f, 0.176f, 1f);
        public static readonly Color DarkCardBorder = new Color(0.235f, 0.235f, 0.235f, 1f);
        public static readonly Color DarkHeaderBg = new Color(0.125f, 0.125f, 0.125f, 1f);
        public static readonly Color AccentBlue = new Color(0.271f, 0.459f, 0.706f, 1f);
        public static readonly Color AccentGreen = new Color(0.271f, 0.659f, 0.376f, 1f);
        public static readonly Color DimText = new Color(0.6f, 0.6f, 0.6f, 1f);
        public static readonly Color ParamBg = new Color(0.15f, 0.15f, 0.15f, 1f);
        public static readonly Color ConstantAccent = new Color(0.271f, 0.459f, 0.706f, 0.5f);
        public static readonly Color ScriptAccent = new Color(0.659f, 0.459f, 0.271f, 0.5f);

        public static readonly GUIStyle Card;
        public static readonly GUIStyle CardHeader;
        public static readonly GUIStyle CardHeaderLabel;
        public static readonly GUIStyle RemoveButton;
        public static readonly GUIStyle AddButton;
        public static readonly GUIStyle ListenerLabel;
        public static readonly GUIStyle ParamLabel;
        public static readonly GUIStyle ParamBgStyle;
        public static readonly GUIStyle Foldout;
        public static readonly GUIStyle Toggle;

        static Styles()
        {
            Card = new GUIStyle
            {
                normal =
                {
                    background = MakeTex(2, 2, DarkCardBg),
                    textColor = EditorStyles.label.normal.textColor
                },
                border = new RectOffset(1, 1, 1, 1),
                padding = new RectOffset(8, 8, 6, 6),
                margin = new RectOffset(0, 0, 2, 2)
            };

            CardHeader = new GUIStyle
            {
                normal =
                {
                    background = MakeTex(2, 2, DarkHeaderBg),
                    textColor = EditorStyles.label.normal.textColor
                },
                border = new RectOffset(1, 1, 1, 1),
                padding = new RectOffset(6, 6, 3, 3),
                margin = new RectOffset(0, 0, 0, 0),
                stretchWidth = true
            };

            CardHeaderLabel = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f, 1f) },
                alignment = TextAnchor.MiddleLeft
            };

            RemoveButton = new GUIStyle(EditorStyles.miniButton)
            {
                normal = { textColor = new Color(0.9f, 0.3f, 0.3f, 1f) },
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                fixedWidth = 20,
                fixedHeight = 16,
                padding = new RectOffset(0, 0, 0, 0),
                alignment = TextAnchor.MiddleCenter
            };

            AddButton = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                fixedHeight = 22,
                padding = new RectOffset(10, 10, 2, 2),
                alignment = TextAnchor.MiddleCenter
            };

            ListenerLabel = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.75f, 0.75f, 0.75f, 1f) },
                padding = new RectOffset(0, 0, 2, 2)
            };

            ParamLabel = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 10,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f, 1f) },
                richText = true,
                padding = new RectOffset(0, 0, 2, 2)
            };

            ParamBgStyle = new GUIStyle
            {
                normal = { background = MakeTex(2, 2, ParamBg) },
                border = new RectOffset(2, 2, 2, 2),
                padding = new RectOffset(6, 6, 4, 4),
                margin = new RectOffset(0, 0, 1, 1)
            };

            Foldout = new GUIStyle(EditorStyles.foldout)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f, 1f) },
                onNormal = { textColor = new Color(0.9f, 0.9f, 0.9f, 1f) }
            };

            Toggle = new GUIStyle(EditorStyles.toggle)
            {
                padding = new RectOffset(16, 0, 2, 2)
            };
        }

        public static void DrawCardBackground(Rect rect)
        {
            DrawRoundedRect(rect, DarkCardBg, DarkCardBorder, 3);
        }

        public static void DrawRoundedRect(Rect rect, Color fill, Color border, int radius)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            var prevColor = Handles.color;
            Handles.color = fill;

            Handles.DrawSolidRectangleWithOutline(rect, fill, border);

            Handles.color = prevColor;
        }

        private static Texture2D MakeTex(int width, int height, Color color)
        {
            var pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = color;
            var tex = new Texture2D(width, height);
            tex.SetPixels(pix);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            return tex;
        }
    }
}
