using UnityEngine;

// In full VR the mode menu's clipboard is shown whole: the game's picture of
// it stops at the bottom of the screen, so while the menu is tall (VRRig) it
// is swapped for the same clipboard with its bottom finished off
// (Resources/VR/ClipboardWhole, made from the game's own by Jen's son), the
// options written on it are spaced further apart down its length, and the
// black covers the game keeps just outside its screen are put away so the
// space past the screen's edge shows. All put back when the menu is not tall.
public static class MenuClipboard
{
    private const string Holder = "FunBG";
    private const float Added = 24f;    // menu units the whole picture adds at the bottom

    private static UnityEngine.UI.Image board;
    private static Sprite whole;
    private static Sprite original;
    private static Vector2 originalSize, originalAt;
    private static float nextLook;
    private const string Settings = "FunSetting";   // FunSetting1 to FunSetting4
    private const float Spacing = 44f;              // menu units between them (the game's is 36)
    private static readonly System.Collections.Generic.List<(RectTransform rect, Vector2 at)> spaced = new System.Collections.Generic.List<(RectTransform, Vector2)>();
    private static readonly System.Collections.Generic.List<GameObject> covers = new System.Collections.Generic.List<GameObject>();

    public static void Update(bool tall)
    {
        if (board == null && Time.unscaledTime >= nextLook)
        {
            nextLook = Time.unscaledTime + 0.5f;
            GameObject found = GameObject.Find(Holder);
            board = found != null ? found.GetComponent<UnityEngine.UI.Image>() : null;
            original = null;
            smallWhole = false;
        }
        if (board == null) return;
        // Not tall (Jen's son): the whole clipboard all the same, a little
        // smaller so all of it fits in the game's screen.
        if (!tall && original == null && board.sprite != whole)
        {
            if (!LoadWhole()) return;
            RectTransform rect = board.rectTransform;
            original = board.sprite;
            originalSize = rect.sizeDelta;
            originalAt = rect.anchoredPosition;
            board.sprite = whole;
            float k = originalSize.y / (originalSize.y + Added);
            rect.sizeDelta = new Vector2(originalSize.x * k, originalSize.y);
            smallWhole = true;
            return;
        }
        if (tall && smallWhole && original != null)
        {
            Put();
        }
        if (tall && original == null)
        {
            if (!LoadWhole()) return;
            RectTransform rect = board.rectTransform;
            original = board.sprite;
            originalSize = rect.sizeDelta;
            originalAt = rect.anchoredPosition;
            board.sprite = whole;
            rect.sizeDelta = new Vector2(originalSize.x, originalSize.y + Added);
            rect.anchoredPosition = originalAt - new Vector2(0f, Added * 0.5f);
            Transform menu = rect.parent;
            for (int i = 1; i <= 4; i++)
            {
                Transform setting = menu != null ? menu.Find("FunSettings/" + Settings + i) : null;
                if (setting == null) continue;
                RectTransform settingRect = (RectTransform)setting;
                spaced.Add((settingRect, settingRect.anchoredPosition));
                Vector2 first = i == 1 ? settingRect.anchoredPosition : spaced[0].at;
                settingRect.anchoredPosition = new Vector2(settingRect.anchoredPosition.x, first.y - (i - 1) * Spacing);
            }
        }
        else if (!tall && original != null && !smallWhole)
        {
            Put();
        }
    }

    private static bool smallWhole;

    private static bool LoadWhole()
    {
        if (whole != null) return true;
        Texture2D picture = Resources.Load<Texture2D>("VR/ClipboardWhole");
        if (picture == null) return false;
        picture.filterMode = FilterMode.Point;
        whole = Sprite.Create(picture, new Rect(0, 0, picture.width, picture.height), new Vector2(0.5f, 0.5f));
        return true;
    }

    // The game's own clipboard back as it was.
    private static void Put()
    {
        RectTransform rect = board.rectTransform;
        board.sprite = original;
        rect.sizeDelta = originalSize;
        rect.anchoredPosition = originalAt;
        original = null;
        smallWhole = false;
        foreach (var (settingRect, at) in spaced) if (settingRect != null) settingRect.anchoredPosition = at;
        spaced.Clear();
    }

    // Each menu screen keeps black covers just outside the game's screen
    // (under "Bottom"); while the menu is tall, those showing are put away,
    // looked for again as other menu screens open.
    private static float nextCovers;
    // The game's menu canvases are laid out for its 480 by 360 screen; while
    // the menu has no edge they are laid out for the bigger area instead, at
    // the same size, so the game's screen sits in the middle of it.
    private static readonly Vector2 Screen = new Vector2(480f, 360f);
    private static readonly System.Collections.Generic.List<UnityEngine.UI.CanvasScaler> widened = new System.Collections.Generic.List<UnityEngine.UI.CanvasScaler>();
    // Each menu screen's plain-colour background is the game's screen size;
    // while the menu has no edge it is stretched out to the whole area.
    private static readonly System.Collections.Generic.List<UnityEngine.UI.Image> grown = new System.Collections.Generic.List<UnityEngine.UI.Image>();
    private static readonly System.Collections.Generic.List<GameObject> copies = new System.Collections.Generic.List<GameObject>();

    public static void Covers(bool tall)
    {
        if (tall)
        {
            if (Time.unscaledTime < nextCovers) return;
            nextCovers = Time.unscaledTime + 0.25f;
            foreach (Transform cover in Object.FindObjectsOfType<Transform>())
            {
                if (cover.name.StartsWith("Border") && cover.parent != null && cover.parent.name == "Bottom" && cover.gameObject.activeSelf)
                {
                    cover.gameObject.SetActive(false);
                    covers.Add(cover.gameObject);
                }
            }
            Vector2 area = new Vector2(VRRig.MenuArea.x, VRRig.MenuArea.y);
            Vector2 middle = new Vector2(0.5f, 0.5f);
            foreach (UnityEngine.UI.Image image in Object.FindObjectsOfType<UnityEngine.UI.Image>(true))
            {
                RectTransform rect = image.rectTransform;
                if (rect.sizeDelta != Screen || rect.anchorMin != middle || rect.anchorMax != middle || image.name.EndsWith(" (all round)") || grown.Contains(image)) continue;
                // A picture (the chalkboard, the title) is left as it is: past
                // its edge is the same white as all round the player.
                if (image.sprite != null) continue;
                grown.Add(image);
                rect.sizeDelta = area;
            }
            foreach (UnityEngine.UI.CanvasScaler scaler in Object.FindObjectsOfType<UnityEngine.UI.CanvasScaler>(true))
            {
                if (scaler.referenceResolution == Screen)
                {
                    scaler.referenceResolution = area;
                    widened.Add(scaler);
                }
            }
        }
        else if (covers.Count > 0 || widened.Count > 0 || grown.Count > 0)
        {
            foreach (UnityEngine.UI.Image image in grown)
            {
                if (image != null && image.sprite == null) image.rectTransform.sizeDelta = Screen;
            }
            grown.Clear();
            foreach (GameObject copy in copies) if (copy != null) Object.Destroy(copy);
            copies.Clear();
            foreach (GameObject cover in covers) if (cover != null) cover.SetActive(true);
            covers.Clear();
            foreach (UnityEngine.UI.CanvasScaler scaler in widened) if (scaler != null) scaler.referenceResolution = Screen;
            widened.Clear();
        }
    }
}
