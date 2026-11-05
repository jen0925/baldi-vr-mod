using System.Collections.Generic;
using UnityEngine;

// The game reads input through Rewired, a paid plug-in whose copy in the shipped
// game was stripped down when the game was built, so it cannot be built for the
// web. This stands in for the part of Rewired the game's own InputManager uses,
// with the same action names, read straight from the keyboard and mouse. (Quest
// controllers come in through QuestInput.)
namespace Rewired
{
    public enum ControllerType { Keyboard, Mouse, Joystick, Custom }

    public class ControllerMap
    {
        public ControllerType controllerType;
    }

    public class ActionElementMap
    {
        public ControllerMap controllerMap;
        public string elementIdentifierName;
    }

    internal static class Bindings
    {
        // Button actions -> keys, as the game's default keyboard and mouse layout.
        public static readonly Dictionary<string, KeyCode[]> Buttons = new Dictionary<string, KeyCode[]>
        {
            { "Interact", new[] { KeyCode.Mouse0 } },
            { "MouseSubmit", new[] { KeyCode.Mouse0 } },
            { "Click", new[] { KeyCode.Mouse0 } },
            { "Run", new[] { KeyCode.LeftShift, KeyCode.RightShift } },
            { "LookBack", new[] { KeyCode.Space } },
            { "UseItem", new[] { KeyCode.Mouse1 } },
            { "ItemLeft", new[] { KeyCode.Q } },
            { "ItemRight", new[] { KeyCode.E } },
            { "Pause", new[] { KeyCode.Escape } },
            { "Map", new[] { KeyCode.Tab } },
            { "MouseBoost", new[] { KeyCode.LeftControl } },
            { "Item1", new[] { KeyCode.Alpha1 } },
            { "Item2", new[] { KeyCode.Alpha2 } },
            { "Item3", new[] { KeyCode.Alpha3 } },
            { "Item4", new[] { KeyCode.Alpha4 } },
            { "Item5", new[] { KeyCode.Alpha5 } },
            { "Item6", new[] { KeyCode.Alpha6 } },
        };

        // Axis actions handled by the mouse (the game insists these exist on the mouse).
        public static readonly HashSet<string> MouseAxes = new HashSet<string>
        {
            "CursorXDelta", "CursorYDelta", "CameraXDelta", "CameraYDelta", "ItemSelect",
        };

        public static float Key(KeyCode positive, KeyCode negative)
        {
            return (Input.GetKey(positive) ? 1f : 0f) - (Input.GetKey(negative) ? 1f : 0f);
        }

        public static float Mouse(string axis)
        {
            try { return Input.GetAxisRaw(axis); }
            catch (System.ArgumentException) { return 0f; }
        }

        public static float Axis(string id)
        {
            switch (id)
            {
                case "MovementX": return Key(KeyCode.D, KeyCode.A);
                case "MovementY": return Key(KeyCode.W, KeyCode.S);
                case "CameraX": return Key(KeyCode.RightArrow, KeyCode.LeftArrow);
                case "CursorX": return Key(KeyCode.RightArrow, KeyCode.LeftArrow);
                case "CursorY": return Key(KeyCode.UpArrow, KeyCode.DownArrow);
                case "CameraXDelta":
                case "CursorXDelta": return Mouse("Mouse X");
                case "CameraYDelta":
                case "CursorYDelta": return Mouse("Mouse Y");
                case "ItemSelect": return Mouse("Mouse ScrollWheel") * 10f;
                default: return 0f;
            }
        }
    }

    public class ControllerMapHelper
    {
        private static int Add(ControllerType type, string id, List<ActionElementMap> results)
        {
            bool has = type == ControllerType.Mouse
                ? Bindings.MouseAxes.Contains(id) || id == "MouseSubmit"
                : type == ControllerType.Keyboard && Bindings.Buttons.ContainsKey(id);
            if (!has) return 0;
            results?.Add(Map(type, id));
            return 1;
        }

        private static ActionElementMap Map(ControllerType type, string id)
        {
            string name = Bindings.Buttons.TryGetValue(id, out KeyCode[] keys) ? keys[0].ToString() : id;
            return new ActionElementMap { controllerMap = new ControllerMap { controllerType = type }, elementIdentifierName = name };
        }

        public int GetAxisMapsWithAction(ControllerType type, string id, bool skipDisabledMaps, List<ActionElementMap> results) => Add(type, id, results);
        public int GetButtonMapsWithAction(ControllerType type, string id, bool skipDisabledMaps, List<ActionElementMap> results) => Add(type, id, results);
        public void LoadDefaultMaps(ControllerType type) { }

        public IEnumerable<ActionElementMap> ButtonMapsWithAction(string id, bool skipDisabledMaps)
        {
            if (Bindings.Buttons.ContainsKey(id)) yield return Map(ControllerType.Keyboard, id);
        }

        public ActionElementMap GetFirstElementMapWithAction(string id, bool skipDisabledMaps) => Map(ControllerType.Keyboard, id);
    }

    public class PlayerControllers
    {
        public readonly ControllerMapHelper maps = new ControllerMapHelper();
    }

    public class Player
    {
        public readonly PlayerControllers controllers = new PlayerControllers();

        public bool GetButton(string id)
        {
            if (!Bindings.Buttons.TryGetValue(id, out KeyCode[] keys)) return false;
            foreach (KeyCode key in keys) if (Input.GetKey(key)) return true;
            return false;
        }

        public bool GetButtonDown(string id)
        {
            if (!Bindings.Buttons.TryGetValue(id, out KeyCode[] keys)) return false;
            foreach (KeyCode key in keys) if (Input.GetKeyDown(key)) return true;
            return false;
        }

        public float GetAxis(string id) => Bindings.Axis(id);

        public void SetVibration(int motor, float strength, float duration) { }
    }

    public class PlayerHelper
    {
        private readonly Player player = new Player();
        public Player GetPlayer(int index) => player;
    }

    public class ControllerHelper
    {
        public bool GetAnyButton() => Input.anyKey;
        public bool GetAnyButtonDown() => Input.anyKeyDown;
    }

    public static class ReInput
    {
        public static readonly PlayerHelper players = new PlayerHelper();
        public static readonly ControllerHelper controllers = new ControllerHelper();
    }
}
