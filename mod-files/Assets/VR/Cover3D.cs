using UnityEngine;
using UnityEngine.SceneManagement;

// The launcher's cover art in VR, made physical: its parts (Baldi, the title,
// the chalkboard, the ribbon, the corner banners) stand out from the
// background at different depths, each a thick solid cut-out, so the cover
// is three-dimensional and shifts as the head moves (Jen's son's request).
// The layers are the cover cut apart (Resources/VR/Cover3D), laid exactly
// over where the game shows the cover on the floating screen.
public static class Cover3D
{
    private const string CoverTexture = "ClassicLaunhcerImage";
    private const float MetresPerUnit = 4.0f / 1280f;   // the screen's size per picture pixel (VRRig)
    private const int Sides = 4;                         // solid thickness behind each part
    private const float SideStep = 0.008f;               // metres between them

    // Each part and how far it stands out from the screen, in metres.
    private static readonly (string name, float depth)[] Parts =
    {
        ("banners", 0.03f), ("board", 0.06f), ("ribbon", 0.10f), ("title", 0.14f), ("baldi", 0.20f),
    };

    private static RectTransform holder;
    private static float nextLook;

    private static UnityEngine.UI.Graphic coverGraphic;

    // Called each frame while the floating screen is showing; 'screen' is the
    // screen's own RectTransform (the picture fills it).
    public static void Update(RectTransform screen, bool showing)
    {
        bool launcher = showing && screen != null && SceneManager.GetActiveScene().name == "ClassicLauncher";
        if (!launcher)
        {
            if (holder != null && holder.gameObject.activeSelf) holder.gameObject.SetActive(false);
            return;
        }
        if (coverGraphic == null || !coverGraphic.isActiveAndEnabled)
        {
            if (Time.unscaledTime < nextLook) return;
            nextLook = Time.unscaledTime + 0.5f;
            coverGraphic = FindCover();
            if (coverGraphic == null) return;
        }
        if (holder == null) Build(screen);
        if (!holder.gameObject.activeSelf) holder.gameObject.SetActive(true);
        Place(screen);
    }

    private static UnityEngine.UI.Graphic FindCover()
    {
        foreach (UnityEngine.UI.Image image in Object.FindObjectsOfType<UnityEngine.UI.Image>())
        {
            if (image.sprite != null && image.sprite.texture != null && image.sprite.texture.name == CoverTexture) return image;
        }
        foreach (UnityEngine.UI.RawImage raw in Object.FindObjectsOfType<UnityEngine.UI.RawImage>())
        {
            if (raw.texture != null && raw.texture.name == CoverTexture) return raw;
        }
        return null;
    }

    private static void Build(RectTransform screen)
    {
        holder = new GameObject("Cover 3D", typeof(RectTransform)).GetComponent<RectTransform>();
        holder.SetParent(screen, false);
        holder.anchorMin = holder.anchorMax = new Vector2(0.5f, 0.5f);
        holder.pivot = new Vector2(0.5f, 0.5f);
        // Back to front, as UI draws in order: the background, then each part
        // with its solid side behind it.
        Add("back", 0.002f, Color.white);
        foreach (var part in Parts)
        {
            for (int k = Sides; k >= 1; k--)
            {
                float shade = 0.55f + 0.06f * (Sides - k);
                Add(part.name, part.depth - SideStep * k, new Color(shade, shade, shade));
            }
            Add(part.name, part.depth, Color.white);
        }
        holder.gameObject.layer = screen.gameObject.layer;
        foreach (Transform child in holder) child.gameObject.layer = screen.gameObject.layer;
    }

    private static void Add(string name, float depth, Color tint)
    {
        Texture2D art = Resources.Load<Texture2D>("VR/Cover3D/" + name);
        // A layer whose picture isn't there (the shared mod leaves them out)
        // is skipped, rather than a white sheet over the title.
        if (art == null) return;
        UnityEngine.UI.RawImage image = new GameObject("Cover " + name, typeof(RectTransform)).AddComponent<UnityEngine.UI.RawImage>();
        image.transform.SetParent(holder, false);
        image.texture = art;
        image.color = tint;
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        // toward the viewer is -z for the screen's canvas
        rect.localPosition = new Vector3(0f, 0f, -Mathf.Max(depth, 0.001f) / MetresPerUnit);
    }

    // Lay the layers exactly over where the cover sits in the game's picture.
    private static void Place(RectTransform screen)
    {
        Canvas canvas = coverGraphic.canvas != null ? coverGraphic.canvas.rootCanvas : null;
        if (canvas == null) return;
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        Vector3[] corners = new Vector3[4];
        coverGraphic.rectTransform.GetWorldCorners(corners);
        Rect area = canvasRect.rect;
        Vector2 low = canvasRect.InverseTransformPoint(corners[0]);
        Vector2 high = canvasRect.InverseTransformPoint(corners[2]);
        Vector2 u0 = new Vector2((low.x - area.xMin) / area.width, (low.y - area.yMin) / area.height);
        Vector2 u1 = new Vector2((high.x - area.xMin) / area.width, (high.y - area.yMin) / area.height);
        Rect s = screen.rect;
        Vector2 a = new Vector2(s.xMin + u0.x * s.width, s.yMin + u0.y * s.height);
        Vector2 b = new Vector2(s.xMin + u1.x * s.width, s.yMin + u1.y * s.height);
        holder.sizeDelta = b - a;
        holder.localPosition = new Vector3((a.x + b.x) * 0.5f, (a.y + b.y) * 0.5f, 0f);
    }
}
