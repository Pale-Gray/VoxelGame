using System;
using System.Collections.Generic;
using OpenTK.Mathematics;
using OpenTK.Platform;

namespace VoxelGame.Util;

public class Input
{
    private static Dictionary<Key, bool> _currentKeysDown = new();
    private static Dictionary<Key, bool> _previousKeysDown = new();
    private static Dictionary<MouseButton, bool> _currentMouseButtonsDown = new();
    private static Dictionary<MouseButton, bool> _previousMouseButtonsDown = new();
    private static Vector2 _previousMousePosition;
    private static Vector2 _currentMousePosition;

    public static Vector2 MouseDelta => _currentMousePosition - _previousMousePosition;
    public static Vector2 MousePosition => _currentMousePosition;
    public static bool IsMouseFocused => Toolkit.Window.GetCursorCaptureMode(Config.Window) == CursorCaptureMode.Locked;
    public static void Init()
    {
        foreach (Key key in Enum.GetValues(typeof(Key)))
        {
            _currentKeysDown.Add(key, false);
            _previousKeysDown.Add(key, false);
        }

        foreach (MouseButton mouseButton in Enum.GetValues(typeof(MouseButton)))
        {
            _currentMouseButtonsDown.Add(mouseButton, false);
            _previousMouseButtonsDown.Add(mouseButton, false);
        }
    }
    
    public static void OnMouseMove(Vector2 mouseCoordinates)
    {
        _currentMousePosition = mouseCoordinates;
    }

    public static void OnMouseButtonDown(MouseButton mouseButton)
    {
        _currentMouseButtonsDown[mouseButton] = true;
    }

    public static void OnMouseButtonUp(MouseButton mouseButton)
    {
        _currentMouseButtonsDown[mouseButton] = false;
    }
    public static void OnKeyDown(Key key)
    {
        _currentKeysDown[key] = true;
    }

    public static void OnKeyUp(Key key)
    {
        _currentKeysDown[key] = false;
    }

    public static void Poll()
    {
        foreach (KeyValuePair<Key, bool> pair in _currentKeysDown)
        {
            _previousKeysDown[pair.Key] = pair.Value;
        }

        foreach (KeyValuePair<MouseButton, bool> pair in _currentMouseButtonsDown)
        {
            _previousMouseButtonsDown[pair.Key] = pair.Value;
        }
        
        _previousMousePosition = _currentMousePosition;
    }

    public static bool IsMouseButtonDown(MouseButton mouseButton)
    {
        return _currentMouseButtonsDown[mouseButton];
    }

    public static bool IsMouseButtonPressed(MouseButton mouseButton)
    {
        return _currentMouseButtonsDown[mouseButton] && !_previousMouseButtonsDown[mouseButton];
    }

    public static bool IsMouseButtonReleased(MouseButton mouseButton)
    {
        return !_currentMouseButtonsDown[mouseButton] && _previousMouseButtonsDown[mouseButton];
    }

    public static bool IsKeyDown(Key key)
    {
        return _currentKeysDown[key];
    }

    public static bool IsKeyPressed(Key key)
    {
        return _currentKeysDown[key] && !_previousKeysDown[key];
    }

    public static bool IsKeyReleased(Key key)
    {
        return !_currentKeysDown[key] && _previousKeysDown[key];
    }
}