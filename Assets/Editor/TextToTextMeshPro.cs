using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class TextToTextMeshPro : Editor
{
    [MenuItem("Tools/Replace Text Component With TextMeshPro", true)]
    private static bool Validate()
    {
        return Selection.activeGameObject != null;
    }

    [MenuItem("Tools/Replace Text Component With TextMeshPro")]
    private static void Convert()
    {
        foreach (var go in Selection.gameObjects)
        {
            var textComp = go.GetComponent<Text>();
            if (textComp == null) continue;

            var rect = go.GetComponent<RectTransform>();

            // === CACHE EVERYTHING BEFORE DESTROYING ===
            string oldText = textComp.text;
            float oldFontSize = textComp.fontSize;
          //  Color oldColor = textComp.color;
            TextAnchor oldAlignment = textComp.alignment;
            FontStyle oldFontStyle = textComp.fontStyle;
            float oldLineSpacing = textComp.lineSpacing;
            bool oldWrap = textComp.horizontalOverflow == HorizontalWrapMode.Wrap;
            bool oldBestFit = textComp.resizeTextForBestFit;
            int oldMinSize = textComp.resizeTextMinSize;
            int oldMaxSize = textComp.resizeTextMaxSize;
            bool oldRaycastTarget = textComp.raycastTarget;

            // Cache full RectTransform
            Vector2 oldSizeDelta = rect.sizeDelta;
            Vector2 oldAnchoredPosition = rect.anchoredPosition;
            Vector2 oldAnchorMin = rect.anchorMin;
            Vector2 oldAnchorMax = rect.anchorMax;
            Vector2 oldPivot = rect.pivot;

            Undo.RecordObject(go, "Convert Text to TMP");

            // Destroy old Text component first
            Undo.DestroyObjectImmediate(textComp);

            // Add TMP component
            var tmp = Undo.AddComponent<TextMeshProUGUI>(go);

            // === RESTORE RECTTRANSFORM EXACTLY ===
            rect.sizeDelta = oldSizeDelta;
            rect.anchoredPosition = oldAnchoredPosition;
            rect.anchorMin = oldAnchorMin;
            rect.anchorMax = oldAnchorMax;
            rect.pivot = oldPivot;

            // === COPY TEXT PROPERTIES ===
            tmp.text = oldText;
            tmp.fontSize = oldFontSize;
           // tmp.color = oldColor;
            tmp.alignment = ConvertAlignment(oldAlignment);   // ← Proper paragraph alignment
            tmp.fontStyle = (FontStyles)oldFontStyle;
            tmp.lineSpacing = oldLineSpacing;
            tmp.enableWordWrapping = oldWrap;
            tmp.raycastTarget = oldRaycastTarget;

            // Best Fit → Auto Sizing
            tmp.enableAutoSizing = oldBestFit;
            if (oldBestFit)
            {
                tmp.fontSizeMin = oldMinSize;
                tmp.fontSizeMax = oldMaxSize;
            }

            // Force TMP to update its mesh immediately
            tmp.ForceMeshUpdate();
        }
    }

    // Proper mapping for paragraph alignment (fixes the old cast issue)
    private static TextAlignmentOptions ConvertAlignment(TextAnchor anchor)
    {
        switch (anchor)
        {
            case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
            case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
            case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
            case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
            case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
            default: return TextAlignmentOptions.TopLeft;
        }
    }
}