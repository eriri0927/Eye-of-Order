using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    private Texture2D bgTexture;

    private GUIStyle titleStyle;
    private GUIStyle buttonStyle;
    private GUIStyle creditStyle;
    private bool stylesInitialized = false;

    void Start()
    {
        bgTexture = Resources.Load<Texture2D>("MainBG");
    }

    void InitStyles()
    {
        if (stylesInitialized) return;

        titleStyle = new GUIStyle();
        titleStyle.fontSize = 80;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = Color.cyan;
        titleStyle.alignment = TextAnchor.MiddleCenter;

        buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 24;
        buttonStyle.fontStyle = FontStyle.Bold;
        buttonStyle.normal.textColor = Color.white;
        buttonStyle.hover.textColor = Color.cyan;
        buttonStyle.active.textColor = Color.yellow;
        buttonStyle.alignment = TextAnchor.MiddleCenter;

        creditStyle = new GUIStyle();
        creditStyle.fontSize = 20;
        creditStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        creditStyle.alignment = TextAnchor.LowerRight;

        stylesInitialized = true;
    }

    void OnGUI()
    {
        InitStyles();

        // 全屏背景
        if (bgTexture != null)
        {
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), bgTexture, ScaleMode.ScaleAndCrop);
        }
        else
        {
            GUI.color = new Color(0.05f, 0.05f, 0.1f, 1f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        // 艺术字标题
        GUI.Label(new Rect(0, Screen.height * 0.2f, Screen.width, 100), "秩 序 之 眼", titleStyle);

        // 按钮区域
        float btnWidth = 200f;
        float btnHeight = 60f;
        float btnX = (Screen.width - btnWidth) * 0.5f;
        float startY = Screen.height * 0.55f;

        // "开始游戏" 按钮
        if (GUI.Button(new Rect(btnX, startY, btnWidth, btnHeight), "开始游戏", buttonStyle))
        {
            SceneManager.LoadScene(1);
        }

        // "退出游戏" 按钮
        if (GUI.Button(new Rect(btnX, startY + btnHeight + 40, btnWidth, btnHeight), "退出游戏", buttonStyle))
        {
            Application.Quit();
            Debug.Log("退出游戏");
        }

        // 作者署名
        GUI.Label(new Rect(Screen.width - 220, Screen.height - 60, 200, 50), "作者：戚雷", creditStyle);
    }
}
