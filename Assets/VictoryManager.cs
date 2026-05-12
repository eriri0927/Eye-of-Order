using UnityEngine;
using UnityEngine.SceneManagement;

public class VictoryManager : MonoBehaviour
{
    public static VictoryManager Instance { get; private set; }

    private bool victoryTriggered = false;
    private GUIStyle btnStyle;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void TriggerVictory()
    {
        if (victoryTriggered) return;
        victoryTriggered = true;

        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public static void Trigger()
    {
        if (Instance == null)
        {
            var go = new GameObject("VictoryManager");
            Instance = go.AddComponent<VictoryManager>();
        }
        Instance.TriggerVictory();
    }

    void InitButtonStyle()
    {
        if (btnStyle != null) return;
        btnStyle = new GUIStyle(GUI.skin.button);
        btnStyle.fontSize = 22;
        btnStyle.fontStyle = FontStyle.Bold;
        btnStyle.alignment = TextAnchor.MiddleCenter;
        btnStyle.normal.textColor = Color.white;
        btnStyle.normal.background = Texture2D.whiteTexture;
        btnStyle.hover.textColor = Color.cyan;
        btnStyle.hover.background = Texture2D.whiteTexture;
        btnStyle.active.textColor = Color.yellow;
    }

    void OnGUI()
    {
        if (!victoryTriggered) return;

        InitButtonStyle();

        GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.6f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 120;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.normal.textColor = new Color(0f, 0.9f, 1f, 1f);
        titleStyle.hover.textColor = new Color(0f, 0.9f, 1f, 1f);
        titleStyle.active.textColor = new Color(0f, 0.9f, 1f, 1f);
        titleStyle.focused.textColor = new Color(0f, 0.9f, 1f, 1f);

        float titleY = Screen.height * 0.25f;
        GUI.Label(new Rect(0, titleY, Screen.width, 150f), "胜  利", titleStyle);

        float btnWidth = 220f;
        float btnHeight = 50f;
        float btnX = (Screen.width - btnWidth) * 0.5f;
        float btnStartY = Screen.height * 0.55f;
        float spacing = 25f;

        GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        GUI.DrawTexture(new Rect(btnX, btnStartY, btnWidth, btnHeight), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(btnX, btnStartY + btnHeight + spacing, btnWidth, btnHeight), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.contentColor = Color.black;

        if (GUI.Button(new Rect(btnX, btnStartY, btnWidth, btnHeight), "重新开始", btnStyle))
        {
            victoryTriggered = false;
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        if (GUI.Button(new Rect(btnX, btnStartY + btnHeight + spacing, btnWidth, btnHeight), "返回菜单", btnStyle))
        {
            victoryTriggered = false;
            Time.timeScale = 1f;
            SceneManager.LoadScene(0);
        }

        GUI.contentColor = Color.white;
    }
}
