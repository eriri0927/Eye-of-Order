using UnityEngine;
using UnityEngine.SceneManagement;

public class StoryManager : MonoBehaviour
{
    private string storyText =
@"2077 年，物理学崩坏。
世界陷入了「无序态」。

所有物质都在疯狂坍缩，
肉眼所见，皆是致命的混沌。

唯有植入「秩序之眼」的躯壳，
才能在毁灭的洪流中，
捕捉到那一丝名为「真相」的蓝光。

在秩序崩坏的废墟上，
你的眼，即是最后的准星。


——— 操作指引 ———

[ WASD ]         移动
   [ 鼠标 ]          视角旋转
[ 空格 ]          跳跃
[ 左键 ]          射击
[ 右键 ]          闪避 

[ 按住 Shift ]    开启秩序之眼

警告：秩序视野下你将完全定身，失去一切机动。
你将看见敌人的攻击预警、听见秩序的韵律，
但代价是——你无法移动分毫。

博弈：在观察与行动之间快速切换，
观察前摇 · 记忆轨迹 · 精确闪避 · 抓取后摇。

当怪物在技能收束中短暂停顿时，
那便是核心暴露的唯一瞬间——
你的子弹，就是秩序的终章。";

    private float scrollSpeed = 56f;
    private float posY;

    private GUIStyle  textStyle;
    private GUIStyle  hintStyle;
    private GUIStyle  titleStyle;
    private bool      stylesReady;

    private float textTotalHeight;
    private float textAreaWidth;

    private Color accentCyan  = new Color(0f, 0.9f, 1f, 1f);
    private Color accentGold  = new Color(1f, 0.85f, 0.3f, 1f);
    private Color subtleWhite = new Color(0.85f, 0.85f, 0.9f, 1f);

    private float hintPulse;

    void Start()
    {
        posY = Screen.height;
    }

    void InitStyles()
    {
        if (stylesReady) return;

        textStyle = new GUIStyle();
        textStyle.fontSize  = 24;
        textStyle.normal.textColor = subtleWhite;
        textStyle.alignment = TextAnchor.UpperCenter;
        textStyle.wordWrap  = true;
        textStyle.richText  = true;

        titleStyle = new GUIStyle();
        titleStyle.fontSize  = 20;
        titleStyle.normal.textColor = accentCyan;
        titleStyle.alignment = TextAnchor.UpperCenter;

        hintStyle = new GUIStyle();
        hintStyle.fontSize  = 17;
        hintStyle.normal.textColor = accentCyan;
        hintStyle.alignment = TextAnchor.LowerRight;

        stylesReady = true;
    }

    void Update()
    {
        posY -= scrollSpeed * Time.deltaTime;
        hintPulse = (Mathf.Sin(Time.time * 2.3f) + 1f) * 0.5f;

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
            SceneManager.LoadScene(2);

        if (heightCalculated() && posY + textTotalHeight < 0)
            SceneManager.LoadScene(2);
    }

    bool heightCalculated()
    {
        if (textAreaWidth <= 0f) return false;
        return textTotalHeight > 0f;
    }

    void OnGUI()
    {
        InitStyles();

        if (textAreaWidth <= 0f)
            textAreaWidth = Screen.width * 0.72f;

        if (textTotalHeight <= 0f)
            textTotalHeight = textStyle.CalcHeight(new GUIContent(storyText), textAreaWidth);

        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float areaX = (Screen.width - textAreaWidth) * 0.5f;
        GUI.Label(new Rect(areaX, posY, textAreaWidth, Mathf.Max(textTotalHeight, Screen.height * 2f)),
                  storyText, textStyle);

        float lineY = Screen.height - 76f;
        float dividerWidth = Mathf.Lerp(120f, 260f, hintPulse);
        float dividerX = Screen.width * 0.88f - dividerWidth;

        GUI.color = new Color(accentCyan.r, accentCyan.g, accentCyan.b, 0.35f + hintPulse * 0.35f);
        GUI.DrawTexture(new Rect(dividerX, lineY + 28f, dividerWidth, 1f), Texture2D.whiteTexture);

        GUI.color = Color.Lerp(accentCyan, accentGold, hintPulse);
        hintStyle.normal.textColor = GUI.color;

        GUI.Label(new Rect(Screen.width * 0.48f, Screen.height - 54f,
                           Screen.width * 0.48f, 38f),
                  "按 [ 回车 ] 踏入秩序", hintStyle);

        GUI.color = Color.white;
    }
}
