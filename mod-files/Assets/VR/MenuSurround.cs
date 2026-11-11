using UnityEngine;

// The menus in VR fill the whole view (Jen's son's request): round the
// floating screen, the empty space takes the menu's own background colour,
// all the way round; and on the loading screen its Baldi heads, smaller,
// are tiled all the way round the player, with a dome above and below.
public static class MenuSurround
{
    private const float Radius = 3.2f;     // metres, beyond the floating screen
    private const float Height = 6f;
    private const int Segments = 128;
    private const int DomeRings = 48;      // rings up each dome, so it is smooth

    private static Texture2D probe;
    private static float nextProbe;
    private static Color surround = Color.black;
    private static GameObject heads;
    private static Material headsMaterial;
    private static Sprite headsSprite;
    private static float lastLoading = -100f;
    private static bool dissolving;

    // The loading screen showed a moment ago (the game is just starting).
    public static bool JustLoaded => Time.unscaledTime - lastLoading < 3f;

    // The colour the menu shows near its edge (not the black bars at the very
    // edge): read from the picture a couple of times a second.
    public static Color Background(RenderTexture picture)
    {
        if (picture == null) return surround;
        if (Time.unscaledTime < nextProbe) return surround;
        nextProbe = Time.unscaledTime + 0.5f;
        if (probe == null) probe = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        RenderTexture before = RenderTexture.active;
        RenderTexture.active = picture;
        probe.ReadPixels(new Rect(picture.width * 0.1f, picture.height * 0.5f, 1, 1), 0, 0, false);
        RenderTexture.active = before;
        Color c = probe.GetPixel(0, 0);
        surround = new Color(c.r, c.g, c.b, 1f);
        return surround;
    }

    // The loading screen's heads round the player, while it shows (in full
    // VR only), moving as the game moves them. Says whether it is showing.
    public static bool Loading(Vector3 centre, int layer, bool menus)
    {
        GameObject loading = menus ? GameObject.Find("LoadingScreen") : null;
        Sprite face = loading != null ? FindFace(loading.transform) : null;
        if (face == null)
        {
            if (heads != null && heads.activeSelf && !dissolving) heads.SetActive(false);
            return false;
        }
        lastLoading = Time.unscaledTime;
        Show(layer, 1f, false, -1);
        if (heads == null) Build(layer);
        if (headsSprite != face)
        {
            headsSprite = face;
            Vector2 low = Vector2.one, high = Vector2.zero;
            foreach (Vector2 corner in face.uv)
            {
                low = Vector2.Min(low, corner);
                high = Vector2.Max(high, corner);
            }
            headsMaterial.mainTexture = face.texture;
            // Crisp pixel art in the headset, never smoothed.
            face.texture.filterMode = FilterMode.Point;
            headsMaterial.SetVector("_Rect", new Vector4(low.x, low.y, high.x - low.x, high.y - low.y));
        }
        // where the game's ClassicLoadScreen has slid and bobbed the faces to
        Transform faces = loading.transform.Find("Faces");
        if (faces != null) headsMaterial.SetVector("_Offset", faces.localPosition);
        heads.transform.position = centre;
        if (!heads.activeSelf) heads.SetActive(true);
        return true;
    }

    // As the game starts: the loading screen's heads, on white, all round the
    // player in the school, eaten away by the game's own pixel fade (step 0 to
    // 16 of its 4-by-4 pattern) to show the school behind.
    public static void Dissolve(bool on, Vector3 centre, float scale, int layer, int step)
    {
        if (!on)
        {
            if (dissolving)
            {
                dissolving = false;
                if (heads != null) heads.SetActive(false);
            }
            return;
        }
        if (heads == null || headsSprite == null) return;
        dissolving = true;
        Show(layer, scale, true, step);
        heads.transform.position = centre;
        if (!heads.activeSelf) heads.SetActive(true);
    }

    // Plain loading heads (white from the view behind), or the fade's: drawn
    // over the school, white between the heads, with holes in it.
    private static void Show(int layer, float scale, bool over, int step)
    {
        if (heads == null) return;
        heads.layer = layer;
        heads.transform.localScale = Vector3.one * scale;
        headsMaterial.SetFloat("_Fill", over ? 1f : 0f);
        headsMaterial.SetFloat("_ZTest", over ? (float)UnityEngine.Rendering.CompareFunction.Always : (float)UnityEngine.Rendering.CompareFunction.LessEqual);
        headsMaterial.SetFloat("_Step", step);
    }

    private static Sprite FindFace(Transform loading)
    {
        Transform faces = loading.Find("Faces");
        if (faces == null) return null;
        foreach (UnityEngine.UI.Image image in faces.GetComponentsInChildren<UnityEngine.UI.Image>())
        {
            if (image.sprite != null) return image.sprite;
        }
        return null;
    }

    private static void Build(int layer)
    {
        Shader shader = Resources.Load<Shader>("VR/TiledAround");
        headsMaterial = new Material(shader);
        // A cylinder round the player with a smooth dome above and below, so
        // the heads go on overhead and underfoot too. Measured in the game's
        // pixels: the heads are the game's 128 pixels, 22 of its 192-pixel
        // spaces round the side, the same size over the domes.
        float pixel = 2f * Mathf.PI * Radius / (22f * 192f);   // metres
        float curve = Mathf.PI * 0.5f * Radius / pixel, side = Height / pixel;
        headsMaterial.SetVector("_Profile", new Vector4(curve, side, 22f * 192f, 2f * curve + side));
        // Rings from the bottom of the bottom dome to the top of the top one.
        int rings = 2 * (DomeRings + 1);
        float[] heights = new float[rings], radii = new float[rings], ups = new float[rings];
        for (int r = 0; r <= DomeRings; r++)
        {
            float bend = 0.5f * Mathf.PI * r / DomeRings;   // 0 at the bottom, a quarter turn at the side
            heights[r] = -Height * 0.5f - Radius * Mathf.Cos(bend);
            radii[r] = Radius * Mathf.Sin(bend);
            ups[r] = curve * r / DomeRings;
            int top = rings - 1 - r;
            heights[top] = -heights[r];
            radii[top] = radii[r];
            ups[top] = 2f * curve + side - ups[r];
        }
        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        Vector3[] vertices = new Vector3[(Segments + 1) * rings];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[Segments * (rings - 1) * 6];
        int t = 0;
        for (int i = 0; i <= Segments; i++)
        {
            float angle = 2f * Mathf.PI * i / Segments;
            Vector3 round = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            for (int r = 0; r < rings; r++)
            {
                vertices[i * rings + r] = round * radii[r] + Vector3.up * heights[r];
                uvs[i * rings + r] = new Vector2((float)i / Segments, ups[r]);
            }
            if (i == Segments) continue;
            for (int r = 0; r < rings - 1; r++)
            {
                int v = i * rings + r, next = v + rings;
                triangles[t++] = v; triangles[t++] = v + 1; triangles[t++] = next;
                triangles[t++] = v + 1; triangles[t++] = next + 1; triangles[t++] = next;
            }
        }
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        heads = new GameObject("Loading Heads All Round", typeof(MeshFilter), typeof(MeshRenderer));
        Object.DontDestroyOnLoad(heads);
        heads.layer = layer;
        heads.GetComponent<MeshFilter>().sharedMesh = mesh;
        heads.GetComponent<MeshRenderer>().sharedMaterial = headsMaterial;
        heads.SetActive(false);
    }
}
