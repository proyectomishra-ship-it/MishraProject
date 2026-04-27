#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

[InitializeOnLoad]
public static class PlayPauseHotkey
{
    private static Key toggleKey = Key.F9;

    static PlayPauseHotkey()
    {
        EditorApplication.update += OnEditorUpdate;
    }

    private static void OnEditorUpdate()
    {
        if (!Application.isPlaying) return;

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard[toggleKey].wasPressedThisFrame)
        {
            TogglePause();
        }
    }

    private static void TogglePause()
    {
        EditorApplication.isPaused = !EditorApplication.isPaused;

        Debug.Log($"[Editor] Pause: {EditorApplication.isPaused}");
    }
}
#endif