using UnityEditor;
using UnityEngine;

// The settings the mod needs that live in the game's own prefabs and the
// Oculus settings, done here so a player who unpacks their own copy of the
// game does not have to click through Unity for them: the desks and chairs
// get the WoodDesk colour, Playtime's jump rope a slower beat, and the
// headset Phase Sync. The installer runs SetUpAndBuild, which then sets up
// and builds the Quest app (ApkBuild).
// The shared mod can't carry the VR pictures made from the game's art, so
// MakePictures makes them again from the player's own copy, the same way they
// were first made: the hand is the game's cursor enlarged (Scale2x three times, then
// Pillow's Lanczos down to 256, worked out to match bit for bit), the upright stamina meter is pieces of the
// game's meter turned upright, and the 3D title's layers are the launcher
// picture cut out along the shapes in VR/Shapes~ (outlines only), with
// its back painted white where Shapes~/back-white says; the pad's shape
// (a plain white outline) comes with the mod. A picture
// that is already there is left alone.
public static class ModSetup
{
    private static readonly string[] WoodPrefabs =
    {
        "BigDesk", "BigDesk_NoSpawn", "Chairs_Desk_Base", "Chairs_Desk_Perfect", "NullBlocker_SmallDesk",
        "NullProjectile_Desk", "BillboardChair", "Chair_Test", "NullProjectile_Chair", "RoundTable_Chairs_1", "RoundTable_Chairs_2",
    };

    private const float RopeDelay = 1.7f;   // each jump a second slower (the game's is 1)

    [MenuItem("Baldi VR/Set up the mod")]
    public static void Run()
    {
        Material wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/WoodDesk.mat");
        foreach (string name in WoodPrefabs)
        {
            string path = FindPrefab(name);
            if (path == null || wood == null) continue;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != null && materials[i].name == "WoodSimple")
                    {
                        materials[i] = wood;
                        changed = true;
                    }
                }
                if (changed) renderer.sharedMaterials = materials;
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        string rope = FindPrefab("Jumprope");
        if (rope != null)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(rope);
            // Found by name: the game's scripts are not visible from this
            // editor assembly.
            MonoBehaviour jumprope = null;
            foreach (MonoBehaviour part in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (part != null && part.GetType().Name == "Jumprope") jumprope = part;
            }
            if (jumprope != null)
            {
                SerializedObject settings = new SerializedObject(jumprope);
                SerializedProperty delay = settings.FindProperty("ropeDelay");
                if (delay != null)
                {
                    delay.floatValue = RopeDelay;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            PrefabUtility.SaveAsPrefabAsset(root, rope);
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Baldi VR mod: prefab settings done.");
    }

    // For the installer: the settings above, the Quest set-up, Phase Sync,
    // then the Quest app.
    public static void SetUpAndBuild()
    {
        MakePictures();
        Run();
        ApkBuild.Setup();
        Unity.XR.Oculus.OculusSettings oculus = AssetDatabase.LoadAssetAtPath<Unity.XR.Oculus.OculusSettings>("Assets/XR/Settings/Oculus Settings.asset");
        if (oculus != null)
        {
            oculus.PhaseSync = true;
            EditorUtility.SetDirty(oculus);
            AssetDatabase.SaveAssets();
        }
        ApkBuild.Build();
    }

    private const string Pictures = "Assets/Resources/VR/";

    [MenuItem("Baldi VR/Make the VR pictures")]
    public static void MakePictures()
    {
        Picture cursor = Picture.Load("Assets/Texture2D/CursorSprite.png");
        if (cursor != null) Save("HandCursor", cursor.Smoothed(3).Lanczos(256, 256));


        Picture meter = Picture.Load("Assets/Texture2D/StaminometerSheet.png");
        if (meter != null)
        {
            Picture upright = meter.TurnedLeft();
            // Jen's son touched up a few of the frame's pixels: those are laid on top.
            Picture frame = upright.Cut(0, 5, 63, 187);
            Picture edits = Picture.Load("Assets/VR/Shapes~/stamina-edits.png");
            Save("StaminaUpright", edits != null ? frame.Under(edits) : frame);
            Save("StaminaUprightBar", upright.Cut(74, 55, 22, 137));
            Save("StaminaUprightNeedle", upright.Cut(64, 23, 32, 8));
        }

        // The whole clipboard: the game's, 24 rows taller, with Jen's son's
        // drawing of its bottom and his touch-ups laid on.
        Picture clipboard = Picture.Load("Assets/Texture2D/ClipboardSmall.png");
        Picture clipEdits = Picture.Load("Assets/VR/Shapes~/clipboard-edits.png");
        if (clipboard != null && clipEdits != null) Save("ClipboardWhole", clipboard.Cut(0, 0, clipEdits.width, clipEdits.height).Under(clipEdits));

        Picture launcher = Picture.Load("Assets/Texture2D/ClassicLaunhcerImage.png");
        if (launcher != null)
        {
            // The back layer is the launcher with the parts that stand out
            // painted white.
            Picture white = Picture.Load("Assets/VR/Shapes~/back-white.png");
            Save("Cover3D/back", white != null ? launcher.Under(white) : launcher);
            // The app's picture in the Quest library: the launcher picture in
            // the middle of a black square.
            Picture icon = new Picture(launcher.height, launcher.height);
            for (int i = 0; i < icon.pixels.Length; i++) icon.pixels[i] = new Color32(0, 0, 0, 255);
            int left = (icon.width - launcher.width) / 2;
            for (int y = 0; y < launcher.height; y++)
            {
                for (int x = 0; x < launcher.width; x++) icon.pixels[y * icon.width + left + x] = launcher.At(x, y);
            }
            SaveAt("Assets/VR/Icon/AppIcon.png", icon);
            foreach (string layer in new[] { "title", "baldi", "banners", "ribbon", "board" })
            {
                Picture shape = Picture.Load("Assets/VR/Shapes~/" + layer + ".png");
                if (shape != null) Save("Cover3D/" + layer, launcher.CutOut(shape));
            }
        }
        AssetDatabase.Refresh();
    }

    private static void Save(string name, Picture picture)
    {
        SaveAt(Pictures + name + ".png", picture);
    }

    private static void SaveAt(string path, Picture picture)
    {
        if (picture == null || System.IO.File.Exists(path)) return;
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        System.IO.File.WriteAllBytes(path, picture.ToPng());
    }

    // A picture's pixels, read straight from its PNG, top row first.
    private class Picture
    {
        public int width, height;
        public Color32[] pixels;

        public Picture(int width, int height)
        {
            this.width = width;
            this.height = height;
            pixels = new Color32[width * height];
        }

        public static Picture Load(string path)
        {
            if (!System.IO.File.Exists(path)) return null;
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(System.IO.File.ReadAllBytes(path))) return null;
            Picture picture = new Picture(texture.width, texture.height);
            Color32[] bottomUp = texture.GetPixels32();
            for (int y = 0; y < picture.height; y++)
            {
                System.Array.Copy(bottomUp, (picture.height - 1 - y) * picture.width, picture.pixels, y * picture.width, picture.width);
            }
            Object.DestroyImmediate(texture);
            return picture;
        }

        public Color32 At(int x, int y) => pixels[y * width + x];

        public byte[] ToPng()
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color32[] bottomUp = new Color32[pixels.Length];
            for (int y = 0; y < height; y++)
            {
                System.Array.Copy(pixels, y * width, bottomUp, (height - 1 - y) * width, width);
            }
            texture.SetPixels32(bottomUp);
            byte[] png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            return png;
        }

        // Smoothly enlarged (bilinear, from pixel centres).
        public Picture Scaled(int newWidth, int newHeight)
        {
            Picture made = new Picture(newWidth, newHeight);
            for (int y = 0; y < newHeight; y++)
            {
                float sy = Mathf.Clamp((y + 0.5f) * height / newHeight - 0.5f, 0f, height - 1);
                int y0 = (int)sy, y1 = Mathf.Min(y0 + 1, height - 1);
                float fy = sy - y0;
                for (int x = 0; x < newWidth; x++)
                {
                    float sx = Mathf.Clamp((x + 0.5f) * width / newWidth - 0.5f, 0f, width - 1);
                    int x0 = (int)sx, x1 = Mathf.Min(x0 + 1, width - 1);
                    float fx = sx - x0;
                    Color top = Color.Lerp(At(x0, y0), At(x1, y0), fx);
                    Color bottom = Color.Lerp(At(x0, y1), At(x1, y1), fx);
                    made.pixels[y * newWidth + x] = Color.Lerp(top, bottom, fy);
                }
            }
            return made;
        }

        // Its outline in one colour.
        public Picture Recoloured(Color32 colour)
        {
            Picture made = new Picture(width, height);
            for (int i = 0; i < pixels.Length; i++) made.pixels[i] = new Color32(colour.r, colour.g, colour.b, pixels[i].a);
            return made;
        }

        // A quarter turn anticlockwise.
        public Picture TurnedLeft()
        {
            Picture made = new Picture(height, width);
            for (int y = 0; y < made.height; y++)
            {
                for (int x = 0; x < made.width; x++) made.pixels[y * made.width + x] = At(width - 1 - y, x);
            }
            return made;
        }

        // A piece of it; anything past its edge is see-through.
        public Picture Cut(int left, int top, int cutWidth, int cutHeight)
        {
            Picture made = new Picture(cutWidth, cutHeight);
            for (int y = 0; y < cutHeight; y++)
            {
                for (int x = 0; x < cutWidth; x++)
                {
                    int sx = left + x, sy = top + y;
                    made.pixels[y * cutWidth + x] = sx < width && sy < height ? At(sx, sy) : new Color32(0, 0, 0, 0);
                }
            }
            return made;
        }

        public byte[] Bytes()
        {
            byte[] bytes = new byte[pixels.Length * 4];
            for (int i = 0; i < pixels.Length; i++) { bytes[i * 4] = pixels[i].r; bytes[i * 4 + 1] = pixels[i].g; bytes[i * 4 + 2] = pixels[i].b; bytes[i * 4 + 3] = pixels[i].a; }
            return bytes;
        }

        public static Picture FromBytes(byte[] bytes, int width, int height)
        {
            Picture made = new Picture(width, height);
            for (int i = 0; i < made.pixels.Length; i++) made.pixels[i] = new Color32(bytes[i * 4], bytes[i * 4 + 1], bytes[i * 4 + 2], bytes[i * 4 + 3]);
            return made;
        }

        // Scale2x, the pixel-art doubler, this many times.
        public Picture Smoothed(int times)
        {
            byte[] bytes = Bytes();
            int w = width, h = height;
            for (int i = 0; i < times; i++) { bytes = PixelArt.Scale2x(bytes, w, h); w *= 2; h *= 2; }
            return FromBytes(bytes, w, h);
        }

        public Picture Lanczos(int newWidth, int newHeight)
        {
            return FromBytes(PixelArt.Resize(Bytes(), width, height, newWidth, newHeight), newWidth, newHeight);
        }

        // This picture with another laid over it wherever that one has anything.
        public Picture Under(Picture over)
        {
            if (over.width != width || over.height != height) return this;
            Picture made = new Picture(width, height);
            // (A pixel of alpha 1 in the one on top means "see-through here".)
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 top = over.pixels[i];
                made.pixels[i] = top.a == 1 ? new Color32(0, 0, 0, 0) : top.a > 0 ? top : pixels[i];
            }
            return made;
        }

        // This picture where the shape is, see-through everywhere else.
        public Picture CutOut(Picture shape)
        {
            if (shape.width != width || shape.height != height) return null;
            Picture made = new Picture(width, height);
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 c = pixels[i];
                made.pixels[i] = shape.pixels[i].a > 0 ? new Color32(c.r, c.g, c.b, shape.pixels[i].a) : new Color32(0, 0, 0, 0);
            }
            return made;
        }
    }

    // The two steps the hand was made with, done exactly as the tools did
    // them: Scale2x, and Pillow's Lanczos resize with premultiplied alpha.
    private static class PixelArt
    {
        const int Precision = 22;
        static double Sinc(double x) { if (x == 0.0) return 1.0; x *= System.Math.PI; return System.Math.Sin(x) / x; }
        static double Lanczos(double x) { return (x > -3.0 && x < 3.0) ? Sinc(x) * Sinc(x / 3.0) : 0.0; }
        static void Coeffs(int inSize, int outSize, out int[] bounds, out int[] kk, out int ksize)
        {
            double scale = (double)inSize / outSize, filterscale = System.Math.Max(scale, 1.0), support = 3.0 * filterscale;
            ksize = (int)System.Math.Ceiling(support) * 2 + 1;
            bounds = new int[outSize * 2]; kk = new int[outSize * ksize];
            double[] k = new double[ksize];
            for (int xx = 0; xx < outSize; xx++)
            {
                double center = (xx + 0.5) * scale, ww = 0.0, ss = 1.0 / filterscale;
                int xmin = (int)(center - support + 0.5); if (xmin < 0) xmin = 0;
                int xmax = (int)(center + support + 0.5); if (xmax > inSize) xmax = inSize;
                xmax -= xmin;
                for (int x = 0; x < xmax; x++) { double w = Lanczos((x + xmin - center + 0.5) * ss); k[x] = w; ww += w; }
                for (int x = 0; x < xmax; x++) { if (ww != 0.0) k[x] /= ww; }
                for (int x = xmax; x < ksize; x++) k[x] = 0;
                for (int x = 0; x < ksize; x++) kk[xx * ksize + x] = (int)(k[x] < 0 ? System.Math.Truncate(-0.5 + k[x] * (1 << Precision)) : System.Math.Truncate(0.5 + k[x] * (1 << Precision)));
                bounds[xx * 2] = xmin; bounds[xx * 2 + 1] = xmax;
            }
        }
        static byte Clip(long v) { if (v >= ((long)1 << Precision << 8)) return 255; if (v <= 0) return 0; return (byte)(v >> Precision); }
        public static byte[] Resize(byte[] src, int w, int h, int nw, int nh)
        {
            byte[] pm = new byte[src.Length];
            for (int i = 0; i < src.Length; i += 4)
            {
                int a = src[i + 3];
                for (int c = 0; c < 3; c++) { int t = src[i + c] * a + 128; pm[i + c] = (byte)(((t >> 8) + t) >> 8); }
                pm[i + 3] = (byte)a;
            }
            int[] b, k; int ks;
            Coeffs(w, nw, out b, out k, out ks);
            byte[] mid = new byte[nw * h * 4];
            for (int y = 0; y < h; y++)
                for (int xx = 0; xx < nw; xx++)
                {
                    int xmin = b[xx * 2], xmax = b[xx * 2 + 1];
                    for (int c = 0; c < 4; c++)
                    {
                        long ss = 1L << (Precision - 1);
                        for (int x = 0; x < xmax; x++) ss += (long)pm[((y * w) + x + xmin) * 4 + c] * k[xx * ks + x];
                        mid[(y * nw + xx) * 4 + c] = Clip(ss);
                    }
                }
            Coeffs(h, nh, out b, out k, out ks);
            byte[] outp = new byte[nw * nh * 4];
            for (int yy = 0; yy < nh; yy++)
            {
                int ymin = b[yy * 2], ymax = b[yy * 2 + 1];
                for (int x = 0; x < nw; x++)
                    for (int c = 0; c < 4; c++)
                    {
                        long ss = 1L << (Precision - 1);
                        for (int y = 0; y < ymax; y++) ss += (long)mid[((y + ymin) * nw + x) * 4 + c] * k[yy * ks + y];
                        outp[(yy * nw + x) * 4 + c] = Clip(ss);
                    }
            }
            for (int i = 0; i < outp.Length; i += 4)
            {
                int a = outp[i + 3];
                if (a != 255 && a != 0) for (int c = 0; c < 3; c++) { int v = 255 * outp[i + c] / a; outp[i + c] = (byte)(v > 255 ? 255 : v); }
            }
            return outp;
        }
        public static byte[] Scale2x(byte[] p, int w, int h)
        {
            byte[] o = new byte[w * h * 16];
            System.Func<int, int, int> at = (x, y) => (System.Math.Min(System.Math.Max(y, 0), h - 1) * w + System.Math.Min(System.Math.Max(x, 0), w - 1)) * 4;
            System.Func<int, int, bool> same = (i, j) => p[i] == p[j] && p[i + 1] == p[j + 1] && p[i + 2] == p[j + 2] && p[i + 3] == p[j + 3];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int E = at(x, y), B = at(x, y - 1), D = at(x - 1, y), F = at(x + 1, y), H = at(x, y + 1);
                    int e0 = E, e1 = E, e2 = E, e3 = E;
                    if (!same(B, H) && !same(D, F)) { e0 = same(D, B) ? D : E; e1 = same(B, F) ? F : E; e2 = same(D, H) ? D : E; e3 = same(H, F) ? F : E; }
                    int W2 = w * 2;
                    System.Array.Copy(p, e0, o, ((2 * y) * W2 + 2 * x) * 4, 4); System.Array.Copy(p, e1, o, ((2 * y) * W2 + 2 * x + 1) * 4, 4);
                    System.Array.Copy(p, e2, o, ((2 * y + 1) * W2 + 2 * x) * 4, 4); System.Array.Copy(p, e3, o, ((2 * y + 1) * W2 + 2 * x + 1) * 4, 4);
                }
            return o;
        }
    }

    private static string FindPrefab(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets(name + " t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == name) return path;
        }
        return null;
    }
}
