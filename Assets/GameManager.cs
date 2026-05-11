using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    private bool isPaused = false;

    private GUIStyle buttonStyle;
    private bool stylesInitialized = false;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            PlayerController player = FindObjectOfType<PlayerController>();
            if (player != null && player.IsDead()) return;

            isPaused = !isPaused;

            if (isPaused)
            {
                Time.timeScale = 0f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                if (player != null) player.enabled = false;
            }
            else
            {
                ResumeGame();
            }
        }
    }

    void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null) player.enabled = true;
    }

    void InitStyles()
    {
        if (stylesInitialized) return;

        buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 18;
        buttonStyle.fontStyle = FontStyle.Bold;
        buttonStyle.normal.textColor = Color.white;
        buttonStyle.hover.textColor = Color.cyan;
        buttonStyle.active.textColor = Color.yellow;
        buttonStyle.alignment = TextAnchor.MiddleCenter;

        stylesInitialized = true;
    }

    void OnGUI()
    {
        if (!isPaused) return;

        InitStyles();

        // 全屏半透明黑色遮罩
        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // 居中菜单框
        float boxWidth = 200f;
        float boxHeight = 250f;
        float boxX = (Screen.width - boxWidth) * 0.5f;
        float boxY = (Screen.height - boxHeight) * 0.5f;

        GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);
        GUI.DrawTexture(new Rect(boxX, boxY, boxWidth, boxHeight), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // 按钮区域
        float btnWidth = 160f;
        float btnHeight = 40f;
        float btnX = boxX + (boxWidth - btnWidth) * 0.5f;
        float startY = boxY + 30f;
        float spacing = 20f;

        // "继续游戏" 按钮
        if (GUI.Button(new Rect(btnX, startY, btnWidth, btnHeight), "继续游戏", buttonStyle))
        {
            ResumeGame();
        }

        // "重新开始" 按钮
        if (GUI.Button(new Rect(btnX, startY + btnHeight + spacing, btnWidth, btnHeight), "重新开始", buttonStyle))
        {
            isPaused = false;
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // "退出游戏" 按钮
        if (GUI.Button(new Rect(btnX, startY + (btnHeight + spacing) * 2, btnWidth, btnHeight), "退出游戏", buttonStyle))
        {
            Application.Quit();
            Debug.Log("Game Quit");
        }
    }
}
