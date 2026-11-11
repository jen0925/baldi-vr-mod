using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using WebXR;

// Quest controllers as the game's own input actions. InputManager asks here as
// well as the keyboard and mouse, so every menu and gameplay action works from
// the headset. The controllers are read straight from the browser's WebXR session
// (Plugins/QuestPads.jslib), once a frame.
//
// Left stick: move.  Right stick: turn, and moves the menu cursor.
// Right trigger / A: interact, click.  Left trigger: run.
// B: look back.  Right grip: use item.  X / Y: previous / next item.
// Left grip: pause (the Quest's own menu button belongs to the browser).
public static class QuestInput
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern int QuestPads_Read(float[] values);
#elif UNITY_ANDROID && !UNITY_EDITOR
    // The Quest app reads the controllers through Unity's XR input instead.
    private static int QuestPads_Read(float[] values)
    {
        System.Array.Clear(values, 0, values.Length);
        return ReadHand(UnityEngine.XR.XRNode.LeftHand, values, Left) + ReadHand(UnityEngine.XR.XRNode.RightHand, values, Right);
    }

    private static int ReadHand(UnityEngine.XR.XRNode node, float[] values, int at)
    {
        UnityEngine.XR.InputDevice device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid) return 0;
        device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 stick);
        device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out float trigger);
        device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.grip, out float grip);
        device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool a);
        device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool b);
        device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxisClick, out bool click);
        values[at + StickX] = stick.x;
        values[at + StickY] = stick.y;
        values[at + Trigger] = trigger;
        values[at + Grip] = grip;
        values[at + ButtonA] = a ? 1f : 0f;
        values[at + ButtonB] = b ? 1f : 0f;
        values[at + 6] = click ? 1f : 0f;
        if (node == UnityEngine.XR.XRNode.LeftHand)
        {
            device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out leftPosition);
            device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out leftRotation);
        }
        else
        {
            device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out rightPosition);
            device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out rightRotation);
        }
        return 1;
    }

    private static Vector3 leftPosition;
    private static Vector3 rightPosition;
    private static Quaternion rightRotation = Quaternion.identity;
    private static Quaternion leftRotation = Quaternion.identity;
#else
    private static int QuestPads_Read(float[] values) => 0;
#endif

    private const int Left = 0, Right = 8;
    private const int StickX = 0, StickY = 1, Trigger = 2, Grip = 3, ButtonA = 4, ButtonB = 5;

    private static readonly float[] pads = new float[16];
    private static int hands;
    private static int lastFrame = -1;
    private static readonly Dictionary<string, bool> held = new Dictionary<string, bool>();
    private static readonly Dictionary<string, bool> heldLastFrame = new Dictionary<string, bool>();

    // In VR whenever at least one controller is reported.
    public static bool Active
    {
        get { Refresh(); return hands > 0; }
    }

    private static void Refresh()
    {
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        hands = QuestPads_Read(pads);
        if (hands == 0 && VRRig.InVR)
        {
            // Backup: WebXR Export's own controller data (its Y is already up-positive).
            hands = Fill(Left, leftData) + Fill(Right, rightData);
        }
        heldLastFrame.Clear();
        foreach (KeyValuePair<string, bool> pair in held) heldLastFrame[pair.Key] = pair.Value;
        held.Clear();
    }

    private static WebXRControllerData leftData, rightData;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Listen()
    {
        WebXRManager.OnControllerUpdate += (data) =>
        {
            if (data.hand == (int)WebXRControllerHand.LEFT) leftData = data;
            else if (data.hand == (int)WebXRControllerHand.RIGHT) rightData = data;
        };
    }

    // A button straight off the controllers, for VR's own choices: A or B on the
    // right hand, or a thumbstick pressed in.
    public static bool RightA { get { Refresh(); return On(Right, ButtonA); } }
    public static bool RightB { get { Refresh(); return On(Right, ButtonB); } }
    public static bool BothSticksIn { get { Refresh(); return On(Right, 6) && On(Left, 6); } }

    // A pressed just now (this frame), for clicking menus with the A button.
    public static bool RightAPressed
    {
        get
        {
            if (Time.frameCount != aFrame)
            {
                aFrame = Time.frameCount;
                bool held = RightA;
                aPressed = held && !aWasHeld;
                aWasHeld = held;
            }
            return aPressed;
        }
    }
    private static int aFrame = -1;
    private static bool aPressed, aWasHeld;

    // Where the right controller is held, in the headset's tracking space.
    public static bool RightHand(out Vector3 position, out Quaternion rotation)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        Refresh();
        position = rightPosition;
        rotation = rightRotation;
        return hands > 0;
#endif
        position = rightData != null ? rightData.gripPosition : Vector3.zero;
        rotation = rightData != null ? rightData.gripRotation : Quaternion.identity;
        return rightData != null && rightData.enabled;
    }

    // Where the left controller is held, in the headset's tracking space.
    public static bool LeftHand(out Vector3 position, out Quaternion rotation)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        Refresh();
        position = leftPosition;
        rotation = leftRotation;
        return hands > 0;
#endif
        position = leftData != null ? leftData.gripPosition : Vector3.zero;
        rotation = leftData != null ? leftData.gripRotation : Quaternion.identity;
        return leftData != null && leftData.enabled;
    }

    private static int Fill(int hand, WebXRControllerData data)
    {
        if (data == null || !data.enabled) return 0;
        pads[hand + StickX] = data.thumbstickX;
        pads[hand + StickY] = data.thumbstickY;
        pads[hand + Trigger] = data.trigger;
        pads[hand + Grip] = data.squeeze;
        pads[hand + ButtonA] = data.buttonA;
        pads[hand + ButtonB] = data.buttonB;
        return 1;
    }

    private static bool On(int hand, int what) => pads[hand + what] > 0.6f;

    private static Vector2 Stick(int hand)
    {
        Vector2 value = new Vector2(pads[hand + StickX], pads[hand + StickY]);
        return value.magnitude > 0.15f ? value : Vector2.zero;
    }

    private static bool Pressed(string id)
    {
        switch (id)
        {
            case "Interact":
            case "MouseSubmit":
                return On(Right, Trigger) || On(Right, ButtonA);
            case "Run": return On(Left, Trigger);
            case "LookBack": return On(Right, ButtonB);
            case "UseItem": return On(Right, Grip);
            case "ItemLeft": return On(Left, ButtonA);
            case "ItemRight": return On(Left, ButtonB);
            case "Pause": return On(Left, Grip);
            default: return false;
        }
    }

    public static bool Digital(string id, bool onDown)
    {
        if (!Active) return false;
        bool now = Pressed(id);
        held[id] = now;
        if (!onDown) return now;
        heldLastFrame.TryGetValue(id, out bool before);
        return now && !before;
    }

    public static bool AnyButton(bool onDown)
    {
        return Digital("Interact", onDown) || Digital("Pause", onDown);
    }

    // Values for the game's axis names (MovementX, CameraX, CursorX, ...).
    public static float Axis(string id)
    {
        if (!Active) return 0f;
        switch (id)
        {
            case "MovementX": return Stick(Left).x;
            case "MovementY": return Stick(Left).y;
            case "CameraX": return Stick(Right).x;
            case "CursorX": return Stick(Right).x;
            case "CursorY": return Stick(Right).y;
            default: return 0f;
        }
    }
}
