using UnityEngine;
using UnityEngine.SceneManagement;

public class StoryManager : MonoBehaviour
{
    private string storyText =
        "2077年，世界陷入了'无序态'。\n" +
        "所有的物质都在疯狂坍缩，\n" +
        "肉眼所见皆是致命的混沌。\n\n" +
        "唯有植入'秩序之眼'的躯壳，\n" +
        "才能在毁灭的洪流中，\n" +
        "捕捉到那一丝名为'真相'的蓝光。\n\n" +
        "欢迎来到，秩序领域。\n\n\n\n" +
        "——— 操 作 指 引 ———\n\n" +
        "[WASD]        在混沌中穿梭\n" +
        "[空格]          跃过震地冲击波\n" +
        "[鼠标右键]    利用闪避踏入虚空\n" +
        "[左键射击]    给予敌人最后的怜悯\n\n" +
        "[按住 Shift]  开启秩序之眼\n" +
        "警告：开启期间，你将失去一切机动。\n" +
        "博弈：观察怪物的攻击后摇，\n" +
        "那是核心暴露的唯一时刻。";

    private float scrollSpeed = 50f;
    private float posY;

    private GUIStyle textStyle;
    private GUIStyle hintStyle;
    private bool stylesInitialized = false;
    private bool heightCalculated = false;
    private float textTotalHeight;

    void Start()
    {
        posY = Screen.height;
    }

    void InitStyles()
    {
        if (stylesInitialized) return;

        textStyle = new GUIStyle();
        textStyle.fontSize = 24;
        textStyle.normal.textColor = Color.white;
        textStyle.alignment = TextAnchor.UpperCenter;
        textStyle.wordWrap = true;

        hintStyle = new GUIStyle();
        hintStyle.fontSize = 18;
        hintStyle.normal.textColor = new Color(0.4f, 0.8f, 1f, 1f);
        hintStyle.alignment = TextAnchor.LowerRight;

        stylesInitialized = true;
    }

    void Update()
    {
        posY -= scrollSpeed * Time.deltaTime;

        // 按回车跳转战斗场景
        if (Input.GetKeyDown(KeyCode.Return))
        {
            SceneManager.LoadScene(2);
        }

        // 文字完全滚出屏幕顶部后自动跳转
        if (heightCalculated && posY + textTotalHeight < 0)
        {
            SceneManager.LoadScene(2);
        }
    }

    void OnGUI()
    {
        InitStyles();

        // 首次渲染时利用 CalcHeight 精确计算文本总高度
        if (!heightCalculated)
        {
            float textAreaWidth = Screen.width * 0.8f;
            textTotalHeight = textStyle.CalcHeight(new GUIContent(storyText), textAreaWidth);
            heightCalculated = true;
        }

        // 全屏黑幕
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // 滚动文本区域
        float areaWidth = Screen.width * 0.8f;
        float areaX = Screen.width * 0.1f;
        GUI.Label(new Rect(areaX, posY, areaWidth, 2000), storyText, textStyle);

        // 底部右下角固定提示
        GUI.Label(new Rect(Screen.width * 0.5f, Screen.height - 50, Screen.width * 0.48f, 40), "按 [回车键] 踏入秩序", hintStyle);
    }
}
