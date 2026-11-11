using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.SpatialTracking;
using WebXR;

// Puts the game in the headset. In VR the game's cameras that would draw to the
// monitor draw into one 4:3 picture instead, and overlay canvases (which draw to
// the monitor without a camera) are drawn into it on top.
//
// Menus: the picture is shown on a big screen floating in front of the player.
// Playing a level: the player is inside the school. Our eyes take the place of
// the game's main camera (its position and facing, plus the way the head turns),
// seeing what it and the billboard camera see, at the school's scale. The game
// still draws its HUD, pop-ups and screen effects into the picture, now with a
// see-through background, and the picture floats in front of the eyes over the
// school.
public static class VRRig
{
    private const float ScreenWidth = 4.0f;    // metres; about 90 degrees wide, most of the view
    private const float ScreenDistance = 2.0f; // metres in front of the player
    private const float EyeHeight = 1.5f;      // metres above the floor
    private const float BoardHeight = 1.75f;   // the menu whiteboard's centre, metres above the floor
    private const int UILayer = 5;
    private const int HudLayer = 26;           // an unused layer for the floating HUD

    // Game units per metre in the school: the player's eyes are 5 units up, a
    // person's about 1.6 m, so the head and the gap between the eyes are scaled
    // to match and the school looks its real size.
    private const float SchoolScale = 5f / 1.6f;
    private const float HudDistance = 0.4f;    // metres in front of the eyes
    private const float EndingWidth = 0.75f;   // metres at the HUD's distance: the win screen fills the view
    private const float HudWidth = 0.8f;       // metres; about 90 degrees, so the HUD sits round the edges

    // The You Can Think Pad, while it is open, is held in the left hand (as in
    // Baldi's Basics Classic VR) with everything else dark.
    private const float PadWidth = 0.32f;      // metres
    private const float HandPadWidth = 0.34f;  // metres; the pad held while playing
    public static bool PadOpen;
    public static TMPro.TMP_FontAsset NotebookFont;  // the HUD's notebook counter font
    private static TMPro.TextMeshProUGUI gameFontCounter;

    // The pause menu (and the debug menu inside it) is held in the left hand the
    // same way, with the school still around it.
    public static bool MenuOpen;

    // The debug menu opened with the 0035 code on the pad (see YCTP).
    public static bool DebugUnlocked;

    // The win screen: it fills the whole view, not the left hand.
    public static bool EndingShown;

    // The stamina the game shows on its staminometer (HudManager), and whether
    // the player is inside the school right now.
    public static float Stamina = 1f;
    public static string NotebookText = "";
    public static bool TransitionActive;
    // How far the game's dither (the pixel fade between screens) has got, 0 to
    // 16, or -1 when there is none (GlobalCam).
    public static int DitherStep = -1;

    // The game's three item slots (HudManager) and the one touched on the pad
    // (picked up by ItemManager).
    public static readonly Sprite[] ItemIcons = new Sprite[3];
    public static int SelectedSlot;
    public static string ItemName = "";
    private static int touchedSlot = -1;
    private static int touchingSlot = -1;
    public static int TakeTouchedSlot()
    {
        int slot = touchedSlot;
        touchedSlot = -1;
        return slot;
    }

    // The pad held in the left hand while playing: notebook counter and items.
    private static GameObject handPad;

    // The right hand: the game's pointing-hand cursor, held where the right
    // controller is. Its fingertip touches the pad's item boxes; sliding it up
    // and down over them picks the one under it.
    private static RectTransform rightHand;
    private static UnityEngine.UI.Text handPadCounter;
    private static Transform handPadBack;
    private static UnityEngine.UI.Image handPadBaldi;
    public static Sprite BaldicatorSprite;   // Baldi's head while it pops up (HudManager)
    private const float BaldicatorSize = 92f;    // pad pixels (the game's is 64)
    private const float BaldicatorSlide = 0.2f;  // seconds to slide up or down
    private static float baldicatorShown;
    // The pad held while playing, for things the game puts on it (HudManager's
    // staminometer).
    public static Transform HandPadRoot => handPad != null ? handPad.transform : null;

    // Places a piece of the game's HUD on the pad: at a point in the pad
    // picture's pixels (its top middle there), this much bigger, drawn like
    // the pad over the school.
    public static void PutOnPad(RectTransform part, float x, float y, float scale)
    {
        part.anchorMin = part.anchorMax = new Vector2(0.5f, 0.5f);
        part.pivot = new Vector2(0.5f, 1f);
        part.anchoredPosition3D = new Vector3(x - 240f, 180f - y, -1f);
        part.localRotation = Quaternion.identity;
        part.localScale = Vector3.one * scale;
        foreach (UnityEngine.UI.Graphic graphic in part.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
        {
            graphic.material = handMaterial != null ? handMaterial : overSchoolMaterial;
            graphic.raycastTarget = false;
        }
        SetLayer(part, HudLayer);
    }

    private static Material overSchoolMaterial;

    // The right hand and the pad in the left hand, for their shadows (Shadows).
    public static RectTransform HandShape => inSchool && rightHand != null ? rightHand : null;
    public static RectTransform PadShape => inSchool && handPad != null ? handPad.GetComponent<RectTransform>() : null;
    private static UnityEngine.UI.Text handPadItemName;
    private static readonly UnityEngine.UI.Image[] handPadIcons = new UnityEngine.UI.Image[3];
    private static readonly UnityEngine.UI.Image[] handPadFrames = new UnityEngine.UI.Image[3];
    public static bool InFullVR => inSchool;

    // Where the player's head is (for SchoolSpeed's hiding of what is behind walls).
    public static Vector3 HeadPosition => head != null ? head.position : Vector3.zero;

    private static int schoolMask;

    private static Transform rig;
    private static Transform head;
    private static TrackedPoseDriver tracking;
    private static Transform headTracker;
    private static Camera eyes;
    private static Camera rightEye;
    private static GameObject hud;
    private static Transform screen;
    private static bool inSchool;

    // How to play in VR, asked each time the headset goes on: inside the school
    // (full VR) or on the big floating screen (flat, as on a monitor). Pressing
    // both thumbsticks in switches between them at any time.
    public static bool FullVR = true;
    private static bool modeChosen;
    public static bool ModeChosen => modeChosen;
    // Set by the warning screen: the question comes right after it.
    public static bool AskMode;
    private static bool promptArmed;
    private static GameObject modePrompt;
    private static bool sticksWereIn;

    // Where the player's eyes are while inside the school, for sprites to face.
    public static Vector3? EyePosition => inSchool && rig != null ? rig.position : (Vector3?)null;

    // In VR the game draws a fixed 4:3 picture, whatever size the headset makes
    // the page's canvas; the game sizes its HUD and menus from this (PlayerFileManager).
    // It is large, and smoothed, so text stays sharp in the headset.
    private const int PictureWidth = 1280;
    private const int PictureHeight = 960;

    public static Vector2Int GameSize => InVR ? (TallMenus ? new Vector2Int(MenuArea.x * 2, MenuArea.y * 2) : new Vector2Int(PictureWidth, PictureHeight)) : new Vector2Int(Screen.width, Screen.height);

    // In full VR the main menu has no edge (Jen's son's request): the game's
    // menus are drawn the same size into a bigger area, 720 by 600 menu units
    // round its 480 by 360 screen, so whatever hangs off any side of its
    // screen (the bottom of the clipboard, Baldi's shadow) shows, the space
    // round it is the same white as all round the player, and the cursor can
    // go anywhere in it. The game's screen is still as big in front of you.
    public static readonly Vector2Int MenuArea = new Vector2Int(720, 600);
    private static bool TallMenus => InVR && FullVR && !inSchool && SceneManager.GetActiveScene().name == "MainMenu";
    public static bool MenusUnbounded => TallMenus;
    // How much further each way than the game's own screen the menus go,
    // in the game's menu units.
    public static Vector2 MenuOverhang => TallMenus ? new Vector2((MenuArea.x - 480) * 0.5f, (MenuArea.y - 360) * 0.5f) : Vector2.zero;

    // The menu cursor in that space: where it is (menu units from the top-left
    // of the game's own 480 by 360 screen, as the game measures it), its
    // picture, whether it shows, and how far it may go - three screens across
    // and three high round the menu.
    public static Vector2 MenuCursor;
    public static Sprite MenuCursorSprite;
    private static int menuCursorFrame = -10;
    private static bool MenuCursorShown => Time.frameCount - menuCursorFrame <= 1;

    // Called by the game's cursor that is showing, every frame.
    public static void ShowMenuCursor(Vector2 at, Sprite picture)
    {
        MenuCursor = at;
        MenuCursorSprite = picture;
        menuCursorFrame = Time.frameCount;
    }
    private static readonly Vector2 MenuMiddle = new Vector2(240f, -180f);
    private static float nextCursorLog;
    // The cursor goes anywhere on the menu's flat plane, far past the menu
    // itself: out to 75 degrees either side of the player and 70 up and down
    // (further on, a flat plane runs off too far to see).
    private const float MetresPerUnit = ScreenWidth / 480f;
    private static Vector2 FlatReach => new Vector2(Mathf.Tan(75f * Mathf.Deg2Rad), Mathf.Tan(70f * Mathf.Deg2Rad)) * (ScreenDistance / MetresPerUnit);
    public static Vector2 CursorLow => MenuMiddle - FlatReach;
    public static Vector2 CursorHigh => MenuMiddle + FlatReach;
    private static RectTransform worldCursor;
    private static UnityEngine.UI.Image worldCursorImage;

    // The cursor drawn on the floating screen's plane, wherever it is - on the
    // menu or out past it in the white.
    private static void ShowWorldCursor(bool show)
    {
        if (!show)
        {
            if (worldCursor != null && worldCursor.gameObject.activeSelf) worldCursor.gameObject.SetActive(false);
            return;
        }
        if (worldCursor == null)
        {
            GameObject holder = new GameObject("VR Menu Cursor", typeof(RectTransform));
            holder.transform.SetParent(screen, false);
            Canvas canvas = holder.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 2000;
            worldCursorImage = new GameObject("Cursor", typeof(RectTransform)).AddComponent<UnityEngine.UI.Image>();
            worldCursorImage.transform.SetParent(holder.transform, false);
            worldCursorImage.raycastTarget = false;
            Material overAll = new Material(overSchoolMaterial);
            overAll.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            worldCursorImage.material = overAll;
            worldCursor = (RectTransform)holder.transform;
            SetLayer(holder.transform, UILayer);
        }
        // Menu units to the screen's picture pixels; the game's cursor picture
        // is 64 units, a little right of and below the point.
        float pixels = (float)GameSize.x / MenuArea.x;
        Vector2 fromMiddle = MenuCursor - MenuMiddle;
        worldCursor.anchorMin = worldCursor.anchorMax = new Vector2(0.5f, 0.5f);
        worldCursor.sizeDelta = Vector2.zero;
        worldCursor.anchoredPosition = fromMiddle * pixels;
        worldCursorImage.sprite = MenuCursorSprite;
        RectTransform pictureRect = worldCursorImage.rectTransform;
        pictureRect.sizeDelta = Vector2.one * 64f * pixels;
        pictureRect.anchoredPosition = new Vector2(8f, -14f) * pixels;
        if (!worldCursor.gameObject.activeSelf) worldCursor.gameObject.SetActive(true);
        if (Time.unscaledTime >= nextCursorLog)
        {
            nextCursorLog = Time.unscaledTime + 2f;
            Debug.Log($"[VR cursor] at {MenuCursor} sprite {(MenuCursorSprite != null ? MenuCursorSprite.name : "none")} world {worldCursorImage.transform.position} screen {screen.position} layer {worldCursor.gameObject.layer}");
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    // The Quest app: Meta's own VR system (the Oculus XR plug-in) runs the headset.
    public static bool InVR => UnityEngine.XR.XRSettings.isDeviceActive;
#else
    public static bool InVR => WebXRManager.Instance != null && WebXRManager.Instance.XRState == WebXRState.VR;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded += (scene, mode) => Apply();
        WebXRManager.OnXRChange += (state, views, leftRect, rightRect) =>
        {
            // Ask again how to play the next time the headset goes on.
            if (state != WebXRState.VR) { modeChosen = false; AskMode = false; }
            Apply();
        };
        Application.onBeforeRender += Follow;
        // Colours exactly as the game made them (no extra saturation).
        Shader.SetGlobalFloat("_VRSaturation", 1f);
        Camera.onPreCull += HideSchoolFromPicture;
        Camera.onPostRender += RestoreAfterPicture;
        // The game creates canvases and cameras after a scene loads; check again
        // four times a second so later ones get the same treatment.
        GameObject runner = new GameObject("VR Rig Runner");
        Object.DontDestroyOnLoad(runner);
        runner.AddComponent<Runner>();
        Apply();
    }

    private class Runner : MonoBehaviour
    {
        private float next;

        private void Update()
        {
            AimCameras();
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 0.5f;
            Apply();
        }
    }

    public static void Apply()
    {
        // Menus clicked through Rewired's UI input module, which is gone (see
        // RewiredShim); give any event system Unity's standard one instead.
        foreach (UnityEngine.EventSystems.EventSystem events in Object.FindObjectsOfType<UnityEngine.EventSystems.EventSystem>(true))
        {
            if (events.GetComponent<UnityEngine.EventSystems.BaseInputModule>() == null)
            {
                events.gameObject.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }
        if (!InVR) return;

        if (rig == null) BuildRig();
        foreach (Camera camera in Object.FindObjectsOfType<Camera>(true))
        {
            // The game's cameras stay out of the eye views.
            if (!IsOurs(camera)) camera.stereoTargetEye = StereoTargetEyeMask.None;
        }
        foreach (Canvas canvas in Object.FindObjectsOfType<Canvas>(true))
        {
            if (canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay) PutOnScreen(canvas);
        }
    }

    // Every game camera that would draw to the monitor draws into the picture
    // instead: the title, the menus and the school itself (with its stacked
    // layers, which CameraStacks sends to the same picture). Runs every frame,
    // before CameraStacks lays the stacks out in LateUpdate.
    private static void AimCameras()
    {
        if (!InVR || rig == null) return;
        if (picture != null && (picture.width != GameSize.x || picture.height != GameSize.y))
        {
            // The menus went tall (or back): a new picture the new size, and
            // everything that drew into or showed the old one moves to it.
            RenderTexture old = picture;
            picture = null;
            foreach (Camera aimedCamera in aimed) if (aimedCamera != null && aimedCamera.targetTexture == old) aimedCamera.targetTexture = null;
            aimed.Clear();
            overlays.targetTexture = null;
            old.Release();
            Object.Destroy(old);
        }
        if (picture == null)
        {
            picture = new RenderTexture(GameSize.x, GameSize.y, 24);
            screen.GetComponent<RectTransform>().sizeDelta = new Vector2(GameSize.x, GameSize.y);
            // The game's 480-wide screen stays the floating screen's width.
            float screenPixels = TallMenus ? 480f * GameSize.x / MenuArea.x : PictureWidth;
            screen.localScale = Vector3.one * (ScreenWidth / screenPixels);
            picture.filterMode = FilterMode.Bilinear;
            screenImage.texture = picture;
            hudImage.texture = picture;
            foreach (UnityEngine.UI.RawImage side in padSides) side.texture = picture;
            overlays.targetTexture = picture;
        }
        int count = AllCameras();
        for (int i = 0; i < count; i++)
        {
            Camera camera = cameraBuffer[i];
            if (camera == null || IsOurs(camera) || camera.targetTexture != null) continue;
            UniversalAdditionalCameraData data = camera.GetComponent<UniversalAdditionalCameraData>();
            if (data != null && data.renderType != CameraRenderType.Base) continue;
            camera.targetTexture = picture;
            aimed.Add(camera);
        }
        aimed.RemoveWhere(IsGone);
    }

    private static RenderTexture picture;
    private static UnityEngine.UI.RawImage screenImage;
    private static UnityEngine.UI.RawImage hudImage;
    private static Camera overlays;
    private static readonly HashSet<Camera> aimed = new HashSet<Camera>();

    // The game's own view while a level is being played: its main camera and the
    // billboard camera drawn with it (NPCs and items are sprites on that layer).
    // Found once and kept while they last: looking them up by name every frame
    // makes garbage, and clearing that up stalls the game now and then.
    private static Camera foundMain;
    private static Camera foundBillboards;
    private static float nextLook;
    private static Camera[] cameraBuffer = new Camera[16];
    private static int cameraCount;

    // The active cameras, in cameraBuffer, without making a new array.
    private static readonly System.Predicate<Camera> IsGone = camera => camera == null;

    private static int AllCameras()
    {
        if (Camera.allCamerasCount > cameraBuffer.Length) cameraBuffer = new Camera[Camera.allCamerasCount * 2];
        cameraCount = Camera.GetAllCameras(cameraBuffer);
        return cameraCount;
    }

    private static Camera MainCamera(out Camera billboards)
    {
        bool lost = foundMain == null || !foundMain.isActiveAndEnabled;
        if (lost && Time.unscaledTime >= nextLook)
        {
            nextLook = Time.unscaledTime + 0.2f;
            foundMain = null;
            foundBillboards = null;
            int count = AllCameras();
            for (int i = 0; i < count; i++)
            {
                if (cameraBuffer[i] != null && cameraBuffer[i].name == "MainCamera") foundMain = cameraBuffer[i];
            }
            if (foundMain != null)
            {
                foreach (Camera camera in foundMain.transform.root.GetComponentsInChildren<Camera>(true))
                {
                    if (camera.name == "BillboardCamera") foundBillboards = camera;
                }
            }
        }
        if (foundMain != null && !foundMain.isActiveAndEnabled) foundMain = null;
        billboards = foundBillboards;
        return foundMain;
    }

    // Before each headset frame: inside the school, our eyes stand where the
    // game's camera is and see what it sees; in the menus they face the screen.
    private static void Follow()
    {
        if (!InVR || rig == null) return;
        ChooseMode();
        MenuClipboard.Update(TallMenus);
        if (TallMenus && Time.unscaledTime >= nextCursorLog && !(MenuCursorShown && MenuCursorSprite != null))
        {
            nextCursorLog = Time.unscaledTime + 2f;
            Debug.Log($"[VR cursor] not drawn: shown {MenuCursorShown} sprite {(MenuCursorSprite != null ? MenuCursorSprite.name : "none")}");
        }
        ShowWorldCursor(TallMenus && MenuCursorShown && MenuCursorSprite != null);
        MenuClipboard.Covers(TallMenus);
        Camera main = MainCamera(out Camera billboards);
        bool playing = main != null && FullVR && modeChosen;
        if (playing != inSchool)
        {
            inSchool = playing;
            Shader.SetGlobalFloat("_BaldiFaceEyes", playing ? 1f : 0f);
            hud.SetActive(playing);
            // Inside the school only the head's turning is used, so walking
            // round the room never takes the eyes through a wall.
            tracking.trackingType = playing ? TrackedPoseDriver.TrackingType.RotationOnly : TrackedPoseDriver.TrackingType.RotationAndPosition;
            head.localPosition = Vector3.zero;
        }
        if (playing)
        {
            Cover3D.Update(screen as RectTransform, false);
            MenuSurround.Loading(Vector3.zero, UILayer, false);
            ShowLoadWord(false);
            if (fadeBall != null && fadeBall.activeSelf) fadeBall.SetActive(false);
            screenImage.enabled = true;
            // Coming in from the loading screen, its heads stay all round the
            // player and the game's pixel fade eats them away, all round, to
            // show the school (rather than on the flat picture).
            bool dissolving = TransitionActive && DitherStep >= 0 && MenuSurround.JustLoaded;
            MenuSurround.Dissolve(dissolving, main.transform.position, SchoolScale, HudLayer, DitherStep);
            hudImage.enabled = !dissolving;
            rig.SetPositionAndRotation(main.transform.position, main.transform.rotation);
            rig.localScale = Vector3.one * SchoolScale;
            // What the school's cameras show; kept while the game is paused, when
            // it has them draw nothing, so the school stays round the menu.
            int seen = main.cullingMask | (billboards != null ? billboards.cullingMask : 0);
            if (seen != 0) schoolMask = seen;
            // The right hand is put away while the You Can Think Pad is open.
            TrackArmSwing();
            LightHands();
            // The right hand stays on the You Can Think Pad too: its keys are
            // pressed with the fingertip.
            ShowRightHand(true);
            // The pad and the pause menu are held in the left hand, and so is a
            // screen fade, so it covers only that small object, not the view.
            if (!PadOpen) ShowPadSides(false);
            if (EndingShown)
            {
                // The win picture in front of the eyes, as big as the view,
                // with nothing else to see.
                handPad.SetActive(false);
                hud.transform.SetParent(head, false);
                hud.transform.localPosition = new Vector3(0f, 0f, HudDistance);
                hud.transform.localRotation = Quaternion.identity;
                hud.transform.localScale = Vector3.one * (EndingWidth / PictureWidth);
                SetEyes(1 << HudLayer, CameraClearFlags.SolidColor, Color.black);
            }
            else if ((PadOpen || MenuOpen || (TransitionActive && !dissolving)) && QuestInput.LeftHand(out Vector3 hand, out Quaternion grip))
            {
                // The pad in the left hand, tilted up to face the player, and
                // nothing else to see. Hand positions are relative to the real
                // head, since the eyes stay put at the player's camera.
                hud.transform.SetParent(rig, false);
                hud.transform.localPosition = hand - headTracker.localPosition + grip * new Vector3(0f, 0.06f, 0.08f);
                hud.transform.localRotation = grip * Quaternion.Euler(50f, 0f, 0f);
                hud.transform.localScale = Vector3.one * (PadWidth / PictureWidth);
                handPad.SetActive(false);
                ShowPadSides(PadOpen);
                if (PadOpen)
                {
                    SetEyes(1 << HudLayer, CameraClearFlags.SolidColor, Color.black);
                }
                else
                {
                    int around = schoolMask | (1 << HudLayer);
                    SetEyes(around, main.clearFlags == CameraClearFlags.Skybox ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor, main.backgroundColor);
                }
            }
            else
            {
                ShowHandPad();
                hud.transform.SetParent(head, false);
                hud.transform.localPosition = new Vector3(0f, 0f, HudDistance);
                hud.transform.localRotation = Quaternion.identity;
                hud.transform.localScale = Vector3.one * (HudWidth / PictureWidth);
                int mask = schoolMask | (1 << HudLayer);
                SetEyes(mask, main.clearFlags == CameraClearFlags.Skybox ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor, main.backgroundColor);
            }
        }
        else
        {
            handPad.SetActive(false);
            ShowRightHand(false);
            hudImage.enabled = true;
            rig.SetPositionAndRotation(new Vector3(0f, 5000f, 0f), Quaternion.identity);
            rig.localScale = Vector3.one;
            // In full VR the loading screen is all round - its heads, moving, on
            // white, with no flat screen. Otherwise it is the original loading
            // screen on the floating screen.
            bool loading = MenuSurround.Loading(new Vector3(0f, 5000f + EyeHeight, 0f), UILayer, FullVR);
            ShowLoadWord(loading);
            screenImage.enabled = !loading;
            // Black round the launcher, white round the menus, all the way round.
            bool menus = SceneManager.GetActiveScene().name == "MainMenu";
            Color around = loading || menus ? Color.white : Color.black;
            SetEyes(1 << UILayer, CameraClearFlags.SolidColor, around);
            FadeAround(around);
            Cover3D.Update(screen as RectTransform, false);   // the plain original launcher
        }
    }

    // The loading screen (Jen's son): the game's own "L O A D" (its words,
    // font, size and colour, copied from the loading screen) floats in the
    // middle of the view, as big as it is on the game's screen; and while it
    // shows, the headset draws sharper than usual - the screen is simple, so
    // the extra pixels cost nothing that shows.
    private static TMPro.TextMeshPro loadWord;
    private const float LoadingSharpness = 1.7f;   // times the headset's usual resolution
    private static bool sharpened;

    private static void ShowLoadWord(bool show)
    {
        if (show != sharpened)
        {
            sharpened = show;
            UnityEngine.XR.XRSettings.eyeTextureResolutionScale = show ? LoadingSharpness : 1f;
        }
        if (!show)
        {
            if (loadWord != null && loadWord.gameObject.activeSelf) loadWord.gameObject.SetActive(false);
            return;
        }
        if (loadWord == null)
        {
            GameObject screen = GameObject.Find("LoadingScreen");
            Transform words = screen != null ? screen.transform.Find("Text/Text1") : null;
            TMPro.TMP_Text original = words != null ? words.GetComponent<TMPro.TMP_Text>() : null;
            if (original == null) return;
            loadWord = new GameObject("VR Load Word").AddComponent<TMPro.TextMeshPro>();
            Object.DontDestroyOnLoad(loadWord.gameObject);
            loadWord.font = original.font;
            loadWord.fontSharedMaterial = original.fontSharedMaterial;
            loadWord.text = original.text;
            loadWord.color = original.color;
            loadWord.characterSpacing = original.characterSpacing;
            loadWord.alignment = TMPro.TextAlignmentOptions.Center;
            loadWord.enableWordWrapping = false;
            // The game's 36 on its 480-wide screen, at the screen's size and distance.
            loadWord.fontSize = original.fontSize * MetresPerUnit * 10f;
            loadWord.rectTransform.sizeDelta = new Vector2(4f, 1f);
            loadWord.gameObject.layer = UILayer;
        }
        Transform view = eyes != null ? eyes.transform : head;
        if (loadWord.transform.parent != view) loadWord.transform.SetParent(view, false);
        loadWord.transform.localPosition = new Vector3(0f, 0f, ScreenDistance);
        loadWord.transform.localRotation = Quaternion.identity;
        if (!loadWord.gameObject.activeSelf) loadWord.gameObject.SetActive(true);
    }

    // When the colour all round changes (black round the launcher, white round
    // the title menu), the old colour stays all round and the game's pixel
    // fade eats it away with the flat screen's (Jen's son's request).
    private static Color shownAround = Color.black;
    private static Color? fadingFrom;
    private static float fadeSince;
    private static bool fadeStarted;
    private static GameObject fadeBall;
    private static Material fadeMaterial;

    private static void FadeAround(Color around)
    {
        if (around != shownAround)
        {
            fadingFrom = shownAround;
            shownAround = around;
            fadeSince = Time.unscaledTime;
            fadeStarted = false;
        }
        if (fadingFrom.HasValue)
        {
            bool fading = TransitionActive && DitherStep >= 0;
            if (fading) fadeStarted = true;
            // done once the fade has run, or if none comes
            if ((fadeStarted && !fading) || (!fadeStarted && Time.unscaledTime - fadeSince > 1.5f)) fadingFrom = null;
        }
        if (!fadingFrom.HasValue)
        {
            if (fadeBall != null && fadeBall.activeSelf) fadeBall.SetActive(false);
            return;
        }
        if (fadeBall == null)
        {
            fadeBall = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fadeBall.name = "VR Fade All Round";
            Object.Destroy(fadeBall.GetComponent<Collider>());
            Object.DontDestroyOnLoad(fadeBall);
            fadeMaterial = new Material(Resources.Load<Shader>("VR/DitherAround"));
            fadeBall.GetComponent<Renderer>().material = fadeMaterial;
            fadeBall.transform.localScale = Vector3.one * 6.4f;   // 3.2 m round, beyond the floating screen
            fadeBall.layer = UILayer;
        }
        fadeBall.transform.position = new Vector3(0f, 5000f + EyeHeight, 0f);
        fadeMaterial.SetColor("_Color", fadingFrom.Value);
        fadeMaterial.SetFloat("_Step", TransitionActive && DitherStep >= 0 ? DitherStep : 0);
        if (!fadeBall.activeSelf) fadeBall.SetActive(true);
    }

    // The hands and the pad held in them take the school's light where the
    // player stands (Jen's son's request), the same light the walls, the
    // characters and the items get: the game's light map, one colour per
    // 10-unit tile, eased the way the school's shaders ease it.
    private static Texture2D lightMap;
    private static float nextLightMapLook;
    private static Material handMaterial;   // the hands' own copy of the over-the-school material, tinted
    private static Color handLight = Color.white;

    private static void LightHands()
    {
        if (lightMap == null && Time.unscaledTime >= nextLightMapLook)
        {
            nextLightMapLook = Time.unscaledTime + 1f;
            foreach (Material material in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (material.HasProperty("_LightMap") && material.GetTexture("_LightMap") is Texture2D map && map.isReadable && map.width > 1)
                {
                    lightMap = map;
                    break;
                }
            }
        }
        if (handMaterial == null && overSchoolMaterial != null)
        {
            handMaterial = new Material(overSchoolMaterial);
            foreach (Transform holder in new Transform[] { rightHand, handPad != null ? handPad.transform : null })
            {
                if (holder == null) continue;
                foreach (UnityEngine.UI.Graphic graphic in holder.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                {
                    if (graphic.material == overSchoolMaterial) graphic.material = handMaterial;
                }
            }
        }
        Color target = Color.white;
        if (lightMap != null)
        {
            Vector3 at = rig.position;
            Color tile = lightMap.GetPixel(Mathf.FloorToInt(at.x / 10f), Mathf.FloorToInt(at.z / 10f));
            target = new Color(Mathf.Clamp01((tile.r * 0.9f + 0.08f) * 1.06f), Mathf.Clamp01((tile.g * 0.9f + 0.08f) * 1.06f), Mathf.Clamp01((tile.b * 0.9f + 0.08f) * 1.06f), 1f);
        }
        // Eased, so walking into a dark room dims the hands smoothly.
        handLight = Color.Lerp(handLight, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 6f));
        if (handMaterial != null) handMaterial.color = handLight;
    }

    // The pad in the left hand while playing, and picking an item by touching
    // its slot with the right controller.
    private static void ShowRightHand(bool show)
    {
        if (!show || !QuestInput.RightHand(out Vector3 right, out Quaternion grip))
        {
            rightHand.gameObject.SetActive(false);
            Fingertip = null;
            MenuPressed = MenuHeld = false;
            return;
        }
        rightHand.gameObject.SetActive(true);
        // The fingertip a little ahead of the controller, and the hand turns
        // with it: the finger points where the controller points, the back of
        // the hand on top, so twisting the wrist rolls the hand the same way.
        Vector3 tip = rig.TransformPoint(right - headTracker.localPosition + grip * new Vector3(0f, 0f, 0.05f));
        // The hand is solid: reaching into a wall, door, desk or anyone, it
        // stops at their surface instead of going through.
        Vector3 eye = rig.position;
        Vector3 reach = tip - eye;
        if (reach.sqrMagnitude > 0.000001f && Physics.Raycast(eye, reach.normalized, out RaycastHit surface, reach.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            tip = surface.point - reach.normalized * HandStandOff;
        }
        // And it stops flat on the front of the menu held in the left hand,
        // which is how its options are pressed.
        menuTouching = false;
        if ((MenuOpen || PadOpen) && hud != null && hud.activeInHierarchy && hudImage != null)
        {
            RectTransform menu = hudImage.rectTransform;
            Vector3 onMenu = menu.InverseTransformPoint(tip);
            Rect area = menu.rect;
            if (onMenu.x >= area.xMin && onMenu.x <= area.xMax && onMenu.y >= area.yMin && onMenu.y <= area.yMax && onMenu.z > -MenuTouchDepth)
            {
                menuTouching = true;
                if (onMenu.z > 0f)
                {
                    onMenu.z = 0f;
                    tip = menu.TransformPoint(onMenu);
                }
            }
        }
        MenuPressed = menuTouching && !MenuHeld;
        MenuHeld = menuTouching;
        rightHand.position = tip;
        rightHand.localRotation = grip * Quaternion.Euler(90f, 0f, 0f);
        Fingertip = tip;
        OrderHand();
    }

    private static void ShowHandPad()
    {
        if (!QuestInput.LeftHand(out Vector3 hand, out Quaternion grip))
        {
            handPad.SetActive(false);
            return;
        }
        handPad.SetActive(true);
        handPad.transform.localPosition = hand - headTracker.localPosition + grip * new Vector3(0f, 0.05f, 0.06f);
        handPad.transform.localRotation = grip * Quaternion.Euler(50f, 0f, 0f);
        // The pad is drawn over everything in its own order, so whichever side
        // faces the eyes is drawn last: the green back when it is turned round.
        bool fromBehind = Vector3.Dot(handPad.transform.forward, handPad.transform.position - eyes.transform.position) < 0f;
        if (handPadBack != null)
        {
            if (fromBehind) handPadBack.SetAsLastSibling();
            else handPadBack.SetAsFirstSibling();
        }
        // Stamina 0 at the bottom of the bar, 1 at the top, a little over when overfull.
        staminaNeedle.rectTransform.anchoredPosition = MeterPixel(31f, 161f - 137f * Mathf.Clamp(Stamina, 0f, 1.06f));
        // The Baldicator slides smoothly up into the box and back down (the
        // game's own slide moves in jumps), a bit bigger than the box.
        if (BaldicatorSprite != null) handPadBaldi.sprite = BaldicatorSprite;
        float target = BaldicatorSprite != null ? 1f : 0f;
        baldicatorShown = Mathf.MoveTowards(baldicatorShown, target, Time.unscaledDeltaTime / BaldicatorSlide);
        float eased = baldicatorShown * baldicatorShown * (3f - 2f * baldicatorShown);
        handPadBaldi.enabled = eased > 0.001f && handPadBaldi.sprite != null;
        handPadBaldi.rectTransform.anchoredPosition = PadPixel(124f, 270f) + new Vector2(0f, -BaldicatorSize * (1f - eased));
        handPadCounter.text = NotebookText;
        // The notebook count in the game's own HUD font, once the HUD has said
        // which it is.
        if (gameFontCounter == null && NotebookFont != null)
        {
            gameFontCounter = new GameObject("Counter (game font)", typeof(RectTransform)).AddComponent<TMPro.TextMeshProUGUI>();
            gameFontCounter.transform.SetParent(handPadCounter.transform.parent, false);
            gameFontCounter.rectTransform.anchoredPosition = handPadCounter.rectTransform.anchoredPosition;
            gameFontCounter.rectTransform.sizeDelta = handPadCounter.rectTransform.sizeDelta;
            gameFontCounter.font = NotebookFont;
            Material onTop = new Material(NotebookFont.material);
            onTop.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            gameFontCounter.fontSharedMaterial = onTop;
            gameFontCounter.fontSize = 34f;
            gameFontCounter.color = Color.white;
            gameFontCounter.alignment = TMPro.TextAlignmentOptions.Center;
            gameFontCounter.enableWordWrapping = false;
            gameFontCounter.raycastTarget = false;
            gameFontCounter.gameObject.layer = HudLayer;
            handPadCounter.enabled = false;
        }
        if (gameFontCounter != null) gameFontCounter.text = NotebookText;
        handPadItemName.text = ItemName;
        for (int i = 0; i < 3; i++)
        {
            handPadIcons[i].sprite = ItemIcons[i];
            handPadIcons[i].enabled = ItemIcons[i] != null;
            handPadFrames[i].color = i == SelectedSlot ? new Color(1f, 1f, 1f, 0.3f) : Color.clear;  // the chosen item: a lighter box
        }
        int touching = -1;
        if (rightHand.gameObject.activeSelf)
        {
            Vector3 fingertip = rightHand.position;
            // The nearest slot within a couple of centimetres of the controller.
            float nearest = 0.03f * SchoolScale;
            for (int i = 0; i < 3; i++)
            {
                float distance = Vector3.Distance(handPadFrames[i].transform.position, fingertip);
                if (distance < nearest) { nearest = distance; touching = i; }
            }
        }
        if (touching >= 0 && touching != touchingSlot) touchedSlot = touching;
        touchingSlot = touching;
    }

    // The official You Can Think Pad picture (480 x 360), laid out in its own
    // pixels: the three small answer boxes on the left hold the items, the
    // screen shows the notebook counter, the lower box the item's name.
    private static GameObject BuildHandPad(Material overSchool)
    {
        const float width = 480f, height = 360f;
        GameObject holder = new GameObject("VR Hand Pad", typeof(RectTransform));
        holder.transform.SetParent(rig, false);
        Canvas canvas = holder.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = eyes;
        canvas.sortingOrder = 1001;
        RectTransform rect = holder.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one * (HandPadWidth / width);
        Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        Texture2D art = Resources.Load<Texture2D>("VR/YCTP_Base");
        Sprite artSprite = art != null ? Sprite.Create(art, new Rect(0, 0, art.width, art.height), Vector2.one * 0.5f) : null;
        // Thickness: the pad's shape in layers behind it, so it is a solid slab
        // in the hand rather than a sheet. The sides are a darker green and the
        // back is solid green, the pad's own green from the front.
        Texture2D shape = Resources.Load<Texture2D>("VR/YCTP_Shape");
        Sprite shapeSprite = shape != null ? Sprite.Create(shape, new Rect(0, 0, shape.width, shape.height), Vector2.one * 0.5f) : artSprite;
        Color padGreen = new Color32(63, 182, 0, 255);
        for (int k = 7; k >= 1; k--)
        {
            Color side = k == 7 ? padGreen : padGreen * 0.7f;
            side.a = 1f;
            UnityEngine.UI.Image layer = Box(holder.transform, k == 7 ? "Back" : "Side " + k, side, Vector2.zero, new Vector2(width, height), overSchool);
            layer.sprite = shapeSprite;
            layer.rectTransform.localPosition = new Vector3(0f, 0f, 4f * k);
            if (k == 7) handPadBack = layer.transform;
        }
        // The pad's see-through screen and boxes are filled black behind it.
        Box(holder.transform, "Screen Fill", Color.black, PadPixel(245, 180), new Vector2(195f, 105f), overSchool);
        Box(holder.transform, "Left Box Fill", Color.black, PadPixel(125, 272), new Vector2(80f, 58f), overSchool);
        Box(holder.transform, "Right Box Fill", Color.black, PadPixel(265, 272), new Vector2(160f, 58f), overSchool);
        float[] fillRows = { 141f, 179f, 216f };
        foreach (float row in fillRows) Box(holder.transform, "Slot Fill", Color.black, PadPixel(112, row), new Vector2(40f, 28f), overSchool);
        // The Baldicator, in the lower left box where Baldi's head is on the
        // You Can Think Pad, seen through the pad's window.
        handPadBaldi = Box(holder.transform, "Baldicator", Color.white, PadPixel(124, 270), new Vector2(BaldicatorSize, BaldicatorSize), overSchool);
        handPadBaldi.preserveAspect = true;
        handPadBaldi.enabled = false;
        UnityEngine.UI.Image body = Box(holder.transform, "Pad", Color.white, Vector2.zero, new Vector2(width, height), overSchool);
        body.sprite = artSprite;
        handPadCounter = Label(body.transform, "Counter", font, 30, PadPixel(245, 165), new Vector2(180f, 50f), overSchool);
        handPadItemName = Label(body.transform, "Item Name", font, 22, PadPixel(265, 272), new Vector2(150f, 50f), overSchool);
        float[] slotRows = { 141f, 179f, 216f };
        for (int i = 0; i < 3; i++)
        {
            Vector2 at = PadPixel(112, slotRows[i]);
            handPadFrames[i] = Box(body.transform, "Slot " + (i + 1), Color.clear, at, new Vector2(50f, 36f), overSchool);
            handPadIcons[i] = Box(handPadFrames[i].transform, "Item", Color.white, Vector2.zero, new Vector2(44f, 44f), overSchool);
            handPadIcons[i].preserveAspect = true;
        }
        // The staminometer, standing upright in the pad's empty green space
        // where the number buttons are when doing maths: full at the top, empty
        // at the bottom, its name down the side.
        RectTransform meter = new GameObject("Stamina", typeof(RectTransform)).GetComponent<RectTransform>();
        meter.SetParent(holder.transform, false);
        meter.anchoredPosition = PadPixel(422f, 220f);
        meter.sizeDelta = new Vector2(63f, 187f);
        UnityEngine.UI.Image bar = Box(meter, "Bar", Color.white, MeterPixel(31f, 92.5f), new Vector2(22f, 137f), overSchool);
        bar.sprite = ResourceSprite("VR/StaminaUprightBar");
        UnityEngine.UI.Image frame = Box(meter, "Frame", Color.white, Vector2.zero, new Vector2(63f, 187f), overSchool);
        frame.sprite = ResourceSprite("VR/StaminaUpright");
        staminaNeedle = Box(meter, "Needle", Color.white, MeterPixel(31f, 161f), new Vector2(32f, 8f), overSchool);
        staminaNeedle.sprite = ResourceSprite("VR/StaminaUprightNeedle");
        // Without the meter's pictures (the shared mod leaves them out), a
        // plain meter in colours instead of white squares: a dark frame, a
        // green bar and a red needle.
        if (frame.sprite == null) frame.color = new Color(0.12f, 0.12f, 0.12f, 0.85f);
        if (bar.sprite == null) bar.color = new Color(0.35f, 0.85f, 0.35f, 1f);
        if (staminaNeedle.sprite == null) staminaNeedle.color = new Color(0.9f, 0.15f, 0.15f, 1f);
        SetLayer(holder.transform, HudLayer);
        holder.SetActive(false);
        return holder;
    }

    private static UnityEngine.UI.Image staminaNeedle;

    // A point in the upright staminometer picture's pixels (from its top left).
    private static Vector2 MeterPixel(float x, float y) => new Vector2(x - 31.5f, 93.5f - y);

    private static Sprite ResourceSprite(string path)
    {
        Texture2D art = Resources.Load<Texture2D>(path);
        return art != null ? Sprite.Create(art, new Rect(0, 0, art.width, art.height), Vector2.one * 0.5f) : null;
    }

    private static RectTransform BuildRightHand(Material overSchool)
    {
        const float size = 64f;
        GameObject holder = new GameObject("VR Right Hand", typeof(RectTransform));
        holder.transform.SetParent(rig, false);
        Canvas canvas = holder.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = eyes;
        canvas.sortingOrder = 1002;
        RectTransform rect = holder.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(size, size);
        // The picture's fingertip (about 26, 6 from its top left) is the hand's
        // point, so that is what touches things.
        rect.pivot = new Vector2(26f / size, 1f - 6f / size);
        rect.localScale = Vector3.one * (0.18f / size);
        Texture2D art = Resources.Load<Texture2D>("VR/HandCursor");
        Sprite artSprite = art != null ? Sprite.Create(art, new Rect(0, 0, art.width, art.height), Vector2.one * 0.5f) : null;
        pointingSprite = artSprite;
        Texture2D open = Resources.Load<Texture2D>("VR/HandOpen");
        openSprite = open != null ? Sprite.Create(open, new Rect(0, 0, open.width, open.height), Vector2.one * 0.5f) : artSprite;
        // Thickness, like the characters and things in the school: the hand's
        // picture in layers behind it, darker with depth.
        for (int k = HandLayers; k >= 1; k--)
        {
            float shade = 0.85f - 0.05f * k;
            UnityEngine.UI.Image layer = Box(holder.transform, "Hand Side " + k, new Color(shade, shade, shade), Vector2.zero, new Vector2(size, size), overSchool);
            Fill(layer.rectTransform);
            layer.sprite = artSprite;
            // Only pushed back: stretched over the hand, its place across and up
            // is already set by the hand's own shape (setting x and y here moved
            // the layers off the front picture into a second hand above it).
            Vector3 place = layer.rectTransform.localPosition;
            layer.rectTransform.localPosition = new Vector3(place.x, place.y, 0.8f * k);
            handSides[k - 1] = layer.transform;
        }
        UnityEngine.UI.Image picture = Box(holder.transform, "Hand", Color.white, Vector2.zero, new Vector2(size, size), overSchool);
        Fill(picture.rectTransform);
        picture.sprite = artSprite;
        handFront = picture.transform;
        SetLayer(holder.transform, HudLayer);
        holder.SetActive(false);
        return rect;
    }

    // Where the right hand is and the way its finger points, flat along the
    // floor, for spraying the BSODA from the hand.
    public static bool HandAim(out Vector3 position, out Vector3 forward)
    {
        position = Vector3.zero;
        forward = Vector3.forward;
        if (!inSchool || rightHand == null || !rightHand.gameObject.activeSelf) return false;
        position = rightHand.position;
        Vector3 finger = rightHand.up;
        forward = new Vector3(finger.x, 0f, finger.z);
        if (forward.sqrMagnitude < 0.0001f) forward = new Vector3(rig.forward.x, 0f, rig.forward.z);
        forward.Normalize();
        return true;
    }

    // Where the right hand's fingertip is over the menu held in the left hand,
    // from 0 to 1 across and up the menu's picture; false when it is off it.
    // Where the right fingertip is in the school, while the hand is shown; the
    // game presses whatever clickable thing it touches (PlayerClick).
    public static Vector3? Fingertip;
    private const float HandStandOff = 0.05f;    // game units kept off a surface
    private const float MenuTouchDepth = 12f;    // menu units (about 3 mm): touching the menu
    private static bool menuTouching;
    // The finger on the menu (held) and just meeting it (a press), worked out
    // once a frame for the menu's cursor.
    public static bool MenuHeld;
    public static bool MenuPressed;

    public static bool MenuPointer(out Vector2 point)
    {
        point = Vector2.zero;
        if (!(MenuOpen || PadOpen) || !inSchool || rightHand == null || !rightHand.gameObject.activeSelf || hudImage == null) return false;
        RectTransform menu = hudImage.rectTransform;
        Vector3 local = menu.InverseTransformPoint(rightHand.position);
        Rect area = menu.rect;
        point = new Vector2((local.x - area.xMin) / area.width, (local.y - area.yMin) / area.height);
        return point.x >= 0f && point.x <= 1f && point.y >= 0f && point.y <= 1f;
    }

    // The math pad (the You Can Think Pad while answering) is as thick as the
    // pad held while playing: its picture in layers behind it, darker with
    // depth, and a green back.
    private const int PadSideLayers = 7;
    private static readonly UnityEngine.UI.RawImage[] padSides = new UnityEngine.UI.RawImage[PadSideLayers];
    private static bool padSidesShown = true;

    private static void BuildPadSides(RectTransform holder, Material overSchool)
    {
        for (int k = PadSideLayers; k >= 1; k--)
        {
            UnityEngine.UI.RawImage side = new GameObject(k == PadSideLayers ? "Pad Back" : "Pad Side " + k, typeof(RectTransform)).AddComponent<UnityEngine.UI.RawImage>();
            side.transform.SetParent(holder, false);
            side.raycastTarget = false;
            side.material = overSchool;
            float shade = 0.75f - 0.06f * k;
            side.color = k == PadSideLayers ? new Color(0.5f, 1f, 0.3f) : new Color(shade, shade, shade);
            Fill(side.rectTransform);
            Vector3 place = side.rectTransform.localPosition;
            side.rectTransform.localPosition = new Vector3(place.x, place.y, 11f * k);
            side.gameObject.layer = HudLayer;
            padSides[k - 1] = side;
        }
        hudImage.transform.SetAsLastSibling();
        ShowPadSides(false);
    }

    private static void ShowPadSides(bool show)
    {
        if (show)
        {
            // Whichever side faces the eyes is drawn last.
            bool fromBehind = Vector3.Dot(hud.transform.forward, hud.transform.position - eyes.transform.position) < 0f;
            if (fromBehind)
            {
                hudImage.transform.SetSiblingIndex(0);
                for (int k = 1; k <= PadSideLayers; k++) padSides[k - 1].transform.SetSiblingIndex(k);
            }
            else
            {
                for (int k = PadSideLayers; k >= 1; k--) padSides[k - 1].transform.SetSiblingIndex(PadSideLayers - k);
                hudImage.transform.SetSiblingIndex(PadSideLayers);
            }
        }
        if (show == padSidesShown) return;
        padSidesShown = show;
        foreach (UnityEngine.UI.RawImage side in padSides) side.enabled = show;
    }

    private static Sprite pointingSprite, openSprite, handShown;
    // Set by the game's PlayerClick: something clickable (a door, a notebook,
    // an item) is within reach of the fingertip.
    public static bool FingerNearPressable;

    private static bool NearPressable(Vector3 tip) => FingerNearPressable;

    private static void SetHandPicture(Sprite picture)
    {
        if (picture == null || picture == handShown || handFront == null) return;
        handShown = picture;
        handFront.GetComponent<UnityEngine.UI.Image>().sprite = picture;
        foreach (Transform side in handSides) side.GetComponent<UnityEngine.UI.Image>().sprite = picture;
    }

    // Arm swinging, for running (PlayerMovement): how fast the two hands are
    // pumping up and down, in metres a second, smoothed.
    public static float ArmSwing;
    public const float ArmSwingToRun = 0.7f;
    private static float lastLeftY, lastRightY;
    private static bool swingStarted;

    private static void TrackArmSwing()
    {
        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.001f);
        if (!QuestInput.LeftHand(out Vector3 left, out _) || !QuestInput.RightHand(out Vector3 right, out _) || PadOpen || MenuOpen)
        {
            ArmSwing = Mathf.MoveTowards(ArmSwing, 0f, dt * 4f);
            swingStarted = false;
            return;
        }
        if (swingStarted)
        {
            float leftSpeed = (left.y - lastLeftY) / dt;
            float rightSpeed = (right.y - lastRightY) / dt;
            // running pumps the hands, usually one up while the other is down
            float pump = (Mathf.Abs(leftSpeed) + Mathf.Abs(rightSpeed)) * 0.5f;
            ArmSwing = Mathf.Lerp(ArmSwing, pump, 1f - Mathf.Exp(-dt / 0.25f));
        }
        lastLeftY = left.y;
        lastRightY = right.y;
        swingStarted = true;
    }

    private const int HandLayers = 6;
    private static readonly Transform[] handSides = new Transform[HandLayers];
    private static Transform handFront;

    private static void Fill(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // The hand is drawn over everything in its own order, so the layer nearest
    // the eyes goes last: the front picture normally, the deepest side when
    // the hand is turned over.
    private static void OrderHand()
    {
        if (handFront == null) return;
        bool fromBehind = Vector3.Dot(rightHand.forward, rightHand.position - eyes.transform.position) < 0f;
        if (fromBehind)
        {
            handFront.SetSiblingIndex(0);
            for (int k = 1; k <= HandLayers; k++) handSides[k - 1].SetSiblingIndex(k);
        }
        else
        {
            for (int k = HandLayers; k >= 1; k--) handSides[k - 1].SetSiblingIndex(HandLayers - k);
            handFront.SetSiblingIndex(HandLayers);
        }
    }

    // A point given in the pad picture's pixels (from its top left).
    private static Vector2 PadPixel(float x, float y) => new Vector2(x - 240f, 180f - y);

    private static UnityEngine.UI.Image Box(Transform parent, string name, Color color, Vector2 at, Vector2 size, Material material)
    {
        UnityEngine.UI.Image image = new GameObject(name, typeof(RectTransform)).AddComponent<UnityEngine.UI.Image>();
        image.transform.SetParent(parent, false);
        image.color = color;
        image.material = material;
        image.raycastTarget = false;
        image.rectTransform.anchoredPosition = at;
        image.rectTransform.sizeDelta = size;
        return image;
    }

    private static UnityEngine.UI.Text Label(Transform parent, string name, Font font, int size, Vector2 at, Vector2 box, Material material)
    {
        UnityEngine.UI.Text text = new GameObject(name, typeof(RectTransform)).AddComponent<UnityEngine.UI.Text>();
        text.transform.SetParent(parent, false);
        text.font = font;
        text.fontSize = size;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.material = material;
        text.raycastTarget = false;
        text.rectTransform.anchoredPosition = at;
        text.rectTransform.sizeDelta = box;
        return text;
    }

    private static void ChooseMode()
    {
        // Asked after the warning screen, or if the headset goes on later in
        // the game. The answer must be a fresh press, not the one that closed
        // the warning.
        bool asking = !modeChosen && (AskMode || MainCamera(out _) != null);
        if (asking)
        {
            bool held = QuestInput.RightA || QuestInput.RightB;
            if (!promptArmed) promptArmed = !held;
            else if (QuestInput.RightA) { FullVR = true; modeChosen = true; }
            else if (QuestInput.RightB) { FullVR = false; modeChosen = true; }
        }
        else
        {
            promptArmed = false;
        }
        bool sticksIn = QuestInput.BothSticksIn;
        if (sticksIn && !sticksWereIn && modeChosen) FullVR = !FullVR;
        sticksWereIn = sticksIn;
        // After the warning the question is the warning screen's own page;
        // this panel only asks when the headset goes on during a level.
        modePrompt.SetActive(asking && !modeChosen && !AskMode);
    }

    // The question shown on the floating screen when the headset goes on.
    private static GameObject ModePrompt()
    {
        GameObject holder = new GameObject("VR Mode Prompt", typeof(RectTransform));
        Object.DontDestroyOnLoad(holder);
        Canvas canvas = holder.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = eyes;
        RectTransform rect = holder.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(900f, 500f);
        rect.localScale = Vector3.one * (1.6f / 900f);
        rect.position = new Vector3(0f, 5000f + EyeHeight, ScreenDistance - 0.05f);
        UnityEngine.UI.Image back = new GameObject("Back", typeof(RectTransform)).AddComponent<UnityEngine.UI.Image>();
        back.transform.SetParent(holder.transform, false);
        back.color = new Color(0f, 0f, 0f, 0.92f);
        Stretch(back.rectTransform);
        UnityEngine.UI.Text text = new GameObject("Text", typeof(RectTransform)).AddComponent<UnityEngine.UI.Text>();
        text.transform.SetParent(holder.transform, false);
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 46;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = "How do you want to play?\n\n<color=#7CFC00>A</color>   Full VR - be inside the school\n<color=#FF6347>B</color>   Flat Screen - play on a big screen\n\n<size=30>Click both thumbsticks in any time to switch.</size>";
        Stretch(text.rectTransform);
        SetLayer(holder.transform, UILayer);
        return holder;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetEyes(int mask, CameraClearFlags clear, Color background)
    {
        SetEye(eyes, mask, clear, background);
        SetEye(rightEye, mask, clear, background);
    }

    private static void SetEye(Camera eye, int mask, CameraClearFlags clear, Color background)
    {
        eye.cullingMask = mask;
        eye.clearFlags = clear;
        eye.backgroundColor = background;
    }

    // Inside the school the eyes draw the school, so the picture keeps only what
    // the game draws over it (HUD, pop-ups, effects): the main and billboard
    // cameras draw nothing into it and the main one clears it to see-through.
    private static readonly Dictionary<Camera, (int mask, CameraClearFlags clear, Color background)> hidden = new Dictionary<Camera, (int, CameraClearFlags, Color)>();

    private static void HideSchoolFromPicture(Camera camera)
    {
        if (!inSchool || camera.targetTexture != picture) return;
        bool main = camera.name == "MainCamera";
        if (!main && camera.name != "BillboardCamera") return;
        hidden[camera] = (camera.cullingMask, camera.clearFlags, camera.backgroundColor);
        camera.cullingMask = 0;
        if (main)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
        }
    }

    private static void RestoreAfterPicture(Camera camera)
    {
        if (!hidden.TryGetValue(camera, out var own)) return;
        hidden.Remove(camera);
        camera.cullingMask = own.mask;
        camera.clearFlags = own.clear;
        camera.backgroundColor = own.background;
    }

    // Our cameras follow the head. WebXR Export's display mode does not work with
    // the built-in render pipeline (it shows a purple screen), so it runs in its
    // camera mode instead (DisableXRDisplaySubsystem, set by WebBuild): its
    // WebXRCamera switches a left and a right eye camera on in VR and gives them
    // the headset's projections, and the head pose comes from the tracked device
    // below. The menu screen and the overlay camera stand apart from the rig, so
    // they stay put while the rig moves into the school.
    private static void BuildRig()
    {
        rig = new GameObject("VR Rig").transform;
        rig.position = new Vector3(0f, 5000f, 0f);
        Object.DontDestroyOnLoad(rig.gameObject);

        GameObject headObject = new GameObject("VR Head");
        headObject.SetActive(false);
        head = headObject.transform;
        head.SetParent(rig, false);
        tracking = headObject.AddComponent<TrackedPoseDriver>();
        tracking.SetPoseSource(TrackedPoseDriver.DeviceType.GenericXRDevice, TrackedPoseDriver.TrackedPose.Center);
        tracking.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
        tracking.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;

        // Follows the real head's position (the eyes may only turn), to place
        // the hands relative to it.
        headTracker = new GameObject("VR Head Tracker").transform;
        headTracker.SetParent(rig, false);
        TrackedPoseDriver headPose = headTracker.gameObject.AddComponent<TrackedPoseDriver>();
        headPose.SetPoseSource(TrackedPoseDriver.DeviceType.GenericXRDevice, TrackedPoseDriver.TrackedPose.Center);
        headPose.trackingType = TrackedPoseDriver.TrackingType.PositionOnly;
        headPose.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;

#if UNITY_ANDROID && !UNITY_EDITOR
        // The Quest app: Unity draws one camera into both eyes itself.
        eyes = Eye(head, "VR Eyes", 1 << UILayer);
        eyes.stereoTargetEye = StereoTargetEyeMask.Both;
        eyes.enabled = true;
        rightEye = eyes;
        headObject.SetActive(true);
#else
        // The flat-mode camera WebXRCamera wants: it must draw nothing over the game.
        Camera main = Eye(head, "VR Flat", 0);
        main.clearFlags = CameraClearFlags.Nothing;
        main.cullingMask = 0;
        main.depth = -100f;
        eyes = Eye(head, "VR Left Eye", 1 << UILayer);
        rightEye = Eye(head, "VR Right Eye", 1 << UILayer);
        Camera arLeft = Eye(head, "VR AR Left", 0);
        Camera arRight = Eye(head, "VR AR Right", 0);

        WebXRCamera webXRCamera = headObject.AddComponent<WebXRCamera>();
        System.Reflection.BindingFlags field = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        typeof(WebXRCamera).GetField("cameraMain", field).SetValue(webXRCamera, main);
        typeof(WebXRCamera).GetField("cameraL", field).SetValue(webXRCamera, eyes);
        typeof(WebXRCamera).GetField("cameraR", field).SetValue(webXRCamera, rightEye);
        typeof(WebXRCamera).GetField("cameraARL", field).SetValue(webXRCamera, arLeft);
        typeof(WebXRCamera).GetField("cameraARR", field).SetValue(webXRCamera, arRight);
        typeof(WebXRCamera).GetField("updateCameraTag", field).SetValue(webXRCamera, false);
        headObject.SetActive(true);
#endif

        // The menu screen: the game's picture, floating in front of the player.
        screenImage = Picture("VR Screen", UILayer, out RectTransform screenRect, null);
        screen = screenRect;
        Object.DontDestroyOnLoad(screen.gameObject);
        screenRect.localScale = Vector3.one * (ScreenWidth / PictureWidth);
        screenRect.position = new Vector3(0f, 5000f + BoardHeight, ScreenDistance);

        // The HUD while playing: the same picture, see-through where the game
        // draws nothing, in front of the eyes and drawn over the school.
        hudImage = Picture("VR HUD", HudLayer, out RectTransform hudRect, head);
        hudRect.localScale = Vector3.one * (HudWidth / PictureWidth);
        hudRect.localPosition = new Vector3(0f, 0f, HudDistance);
        Material overSchool = new Material(hudImage.material);
        overSchool.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
        hudImage.material = overSchool;
        overSchoolMaterial = overSchool;
        hud = hudRect.gameObject;
        BuildPadSides(hudRect, overSchool);
        hud.SetActive(false);

        handPad = BuildHandPad(overSchool);
        rightHand = BuildRightHand(overSchool);

        modePrompt = ModePrompt();

        // Draws the overlay canvases into the picture last, as the monitor would.
        // It sits well away from everything so no eye ever sees what it draws.
        overlays = new GameObject("VR Overlays").AddComponent<Camera>();
        Object.DontDestroyOnLoad(overlays.gameObject);
        overlays.transform.position = new Vector3(1000f, 5000f, 0f);
        overlays.clearFlags = CameraClearFlags.Depth;
        overlays.cullingMask = 1 << UILayer;
        overlays.nearClipPlane = 0.01f;
        overlays.farClipPlane = 10f;
        overlays.depth = 1000f;
        overlays.stereoTargetEye = StereoTargetEyeMask.None;
    }

    private static UnityEngine.UI.RawImage Picture(string name, int layer, out RectTransform rect, Transform parent)
    {
        GameObject holder = new GameObject(name, typeof(RectTransform));
        if (parent != null) holder.transform.SetParent(parent, false);
        Canvas canvas = holder.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = eyes;
        canvas.sortingOrder = layer == HudLayer ? 1000 : -1000;
        UnityEngine.UI.RawImage image = new GameObject("Picture", typeof(RectTransform)).AddComponent<UnityEngine.UI.RawImage>();
        image.transform.SetParent(holder.transform, false);
        image.raycastTarget = false;
        RectTransform imageRect = image.rectTransform;
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;
        rect = holder.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(PictureWidth, PictureHeight);
        SetLayer(holder.transform, layer);
        return image;
    }

    private static Camera Eye(Transform parent, string name, int mask)
    {
        Camera camera = new GameObject(name).AddComponent<Camera>();
        camera.transform.SetParent(parent, false);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.cullingMask = mask;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 1000f;
        camera.depth = 100f;
        camera.stereoTargetEye = StereoTargetEyeMask.None;
        camera.enabled = false;
        return camera;
    }

    // An overlay canvas is drawn into the picture by our overlay camera, after the
    // game's cameras, keeping the size and scale the game gives it there.
    private static void PutOnScreen(Canvas canvas)
    {
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = overlays;
        canvas.planeDistance = 1f;
        SetLayer(canvas.transform, UILayer);
    }

    private static bool IsOurs(Camera camera) => rig != null && (camera.transform.IsChildOf(rig) || camera == overlays);

    private static void SetLayer(Transform transform, int layer)
    {
        transform.gameObject.layer = layer;
        foreach (Transform child in transform) SetLayer(child, layer);
    }
}
