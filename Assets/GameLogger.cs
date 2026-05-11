using UnityEngine;
using System.Collections.Generic;

public class GameLogger : MonoBehaviour
{
    private static readonly List<string> logs = new List<string>();
    private const int MaxLines = 10;

    public static void Log(string msg)
    {
        logs.Add(msg);
        while (logs.Count > MaxLines)
            logs.RemoveAt(0);
    }

    void OnGUI()
    {
        if (logs.Count == 0) return;

        float x = Screen.width - 320f;
        float y = 20f;
        float w = 300f;
        float h = 250f;

        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);

        GUI.color = Color.white;
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            wordWrap = true,
            normal = { textColor = Color.white }
        };

        float lineY = y + 8f;
        for (int i = 0; i < logs.Count; i++)
        {
            GUI.Label(new Rect(x + 8f, lineY, w - 16f, 22f), logs[i], style);
            lineY += 22f;
        }
    }
}
