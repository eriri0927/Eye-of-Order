using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 12f;
    public float gravity = -40f;
    public float jumpHeight = 2.5f;
    public float fallGravityMultiplier = 1.5f;
    public float mouseSensitivity = 2f;
    public float dodgeSpeed = 6f;
    public float dodgeDuration = 0.15f;
    public float invincibleDuration = 0.35f;
    public float groundStickForce = -50f;
    public float coyoteTime = 0.12f;

    public float maxStamina = 100f;
    public float staminaRegenRate = 10f;
    public float dodgeCost = 25f;
    public float jumpCost = 15f;

    public float fireRate = 0.2f;
    public float playerMaxHealth = 100f;

    private CharacterController controller;
    private EnemyOrderSystem enemyOrderSystem;
    private Camera mainCamera;
    private Vector3 velocity;
    private float cameraPitch;
    private bool isDodging;
    private float currentStamina;
    private float nextFireTime;
    private float playerCurrentHealth;
    private float lastGroundedTime;
    private bool isInvincible;
    private bool hasPerfectDodgeBuff;
    private bool isWitchTime;
    private bool isDead;
    private GUIStyle deathButtonStyle;
    private float dodgeFlashTimer;
    private const float dodgeFlashDuration = 0.2f;
    private float dodgeBufferTimer;
    private Vector3 dodgeDirection;
    private Vector3 cameraVelocity;
    private float dodgeCooldownTimer;
    private const float dodgeCooldown = 0.35f;

    [HideInInspector] public bool isTransitionFrozen = false;

    void Awake()
    {
        dodgeSpeed = 6f;
        dodgeDuration = 0.15f;
        invincibleDuration = 0.5f;
        dodgeCost = 25f;
        dodgeCooldownTimer = 0f;
        isTransitionFrozen = false;
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        controller.height = 1.5f;
        controller.center = new Vector3(0f, 0f, 0f);

        currentStamina = maxStamina;
        playerCurrentHealth = playerMaxHealth;

        CreatePlayerVisual();
        ApplyGroundColor();

        mainCamera = GetComponentInChildren<Camera>();
        if (mainCamera == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            mainCamera = camObj.AddComponent<Camera>();
        }
        mainCamera.transform.SetParent(null);
        mainCamera.transform.position = transform.position - transform.forward * 4.5f + transform.right * 1.2f + Vector3.up * 2f;

        enemyOrderSystem = FindObjectOfType<EnemyOrderSystem>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void CreatePlayerVisual()
    {
        GameObject playerVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        playerVisual.name = "PlayerVisual";
        playerVisual.transform.SetParent(transform);
        playerVisual.transform.localPosition = Vector3.zero;
        playerVisual.transform.localScale = new Vector3(0.5f, 1.5f, 0.5f);

        Renderer renderer = playerVisual.GetComponent<Renderer>();
        renderer.material = new Material(Shader.Find("Standard"));
        renderer.material.color = Color.white;

        Destroy(playerVisual.GetComponent<Collider>());

        GameObject visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visor.name = "Visor";
        visor.transform.SetParent(transform);
        visor.transform.localPosition = new Vector3(0f, 0.5f, 0.26f);
        visor.transform.localScale = new Vector3(0.4f, 0.2f, 0.1f);

        Renderer visorRenderer = visor.GetComponent<Renderer>();
        Material visorMat = new Material(Shader.Find("Standard"));
        visorMat.color = Color.black;
        visorMat.EnableKeyword("_EMISSION");
        visorMat.SetColor("_EmissionColor", new Color(0.05f, 0.05f, 0.05f));
        visorRenderer.material = visorMat;

        Destroy(visor.GetComponent<Collider>());
    }

    void ApplyGroundColor()
    {
        GameObject ground = GameObject.Find("Ground");
        if (ground == null) return;

        Renderer renderer = ground.GetComponent<Renderer>();
        if (renderer == null) return;

        Material groundMat = renderer.material;
        groundMat.color = new Color(0.36f, 0.25f, 0.15f);
        groundMat.SetFloat("_Glossiness", 0.1f);
        groundMat.SetFloat("_Metallic", 0f);
    }

    void Update()
    {
        if (isDead) return;

        if (dodgeFlashTimer > 0f)
            dodgeFlashTimer -= Time.deltaTime;

        if (dodgeBufferTimer > 0f)
            dodgeBufferTimer -= Time.deltaTime;

        if (dodgeCooldownTimer > 0f)
            dodgeCooldownTimer -= Time.deltaTime;

        if (isTransitionFrozen)
        {
            velocity = Vector3.zero;
            controller.Move(Vector3.zero);
            return;
        }

        if (Input.GetMouseButtonDown(1) && !Input.GetKey(KeyCode.LeftShift))
            dodgeBufferTimer = 0.15f;

        HandleEnemyOrder();

        HandleMouseLook();

        if (controller.isGrounded && velocity.y < 0f)
            velocity.y = groundStickForce;

        if (controller.isGrounded)
            lastGroundedTime = Time.time;

        if (!isDodging)
            currentStamina = Mathf.Min(currentStamina + staminaRegenRate * Time.deltaTime, maxStamina);

        bool canAct = !Input.GetKey(KeyCode.LeftShift);

        Vector3 move = Vector3.zero;
        if (isDodging)
            move = dodgeDirection * dodgeSpeed;
        else if (canAct)
            move = GetMovementInput() * moveSpeed;

        if (canAct)
        {
            HandleJump();
            HandleDodge();
            HandleShoot();
        }

        if (!isDodging && move.sqrMagnitude < 0.001f)
        {
            velocity.x = 0f;
            velocity.z = 0f;
        }

        if (velocity.y < 0f)
            velocity.y += gravity * fallGravityMultiplier * Time.deltaTime;
        else
            velocity.y += gravity * Time.deltaTime;

        controller.Move((move + velocity) * Time.deltaTime);
    }

    void LateUpdate()
    {
        if (mainCamera != null && !isDead)
            CameraFollow();
    }

    void HandleEnemyOrder()
    {
        if (enemyOrderSystem == null) return;
        enemyOrderSystem.SwitchToOrder(CanPierceCore);
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(Vector3.up * mouseX);

        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, -60f, 60f);
    }

    void CameraFollow()
    {
        Vector3 desiredPos = transform.position
            - transform.forward * 4.5f
            + transform.right * 1.2f
            + Vector3.up * 2f;

        mainCamera.transform.position = Vector3.SmoothDamp(
            mainCamera.transform.position, desiredPos, ref cameraVelocity, 0.05f);

        Vector3 lookTarget = transform.position + transform.forward * 10f + Vector3.up * 1.5f;
        mainCamera.transform.LookAt(lookTarget);
        mainCamera.transform.Rotate(cameraPitch, 0f, 0f);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 严格死区 + 摄像机向量包裹：
    //   1. magnitude <= 0.1f 时一刀切回 Vector3.zero，绝不让亚阈值噪声进入向量管线
    //   2. 仅在通过死区检查后，才读取 camera.right / camera.forward
    //      并强制抹平 Y 轴（消除摄像机俯仰角对水平移动的污染）
    //   3. 这样无输入时 camera 向量根本不会被取值 → 不可能"悄悄累加"
    // ─────────────────────────────────────────────────────────────────────────
    Vector3 GetMovementInput()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical   = Input.GetAxisRaw("Vertical");
        Vector2 moveInput = new Vector2(horizontal, vertical);

        if (moveInput.magnitude <= 0.1f) return Vector3.zero;

        Vector3 camRight   = mainCamera.transform.right;
        Vector3 camForward = mainCamera.transform.forward;
        camRight.y   = 0f;
        camForward.y = 0f;
        camRight.Normalize();
        camForward.Normalize();

        return (camRight * moveInput.x + camForward * moveInput.y).normalized;
    }

    void HandleJump()
    {
        bool wasGroundedRecently = Time.time - lastGroundedTime <= coyoteTime;
        if (wasGroundedRecently && Input.GetKeyDown(KeyCode.Space) && currentStamina >= jumpCost)
        {
            currentStamina -= jumpCost;
            lastGroundedTime = 0f;
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }

    void HandleDodge()
    {
        if (dodgeBufferTimer > 0f && dodgeCooldownTimer <= 0f && !Input.GetKey(KeyCode.LeftShift) && controller.isGrounded && !isDodging && currentStamina >= dodgeCost)
        {
            dodgeBufferTimer = 0f;
            dodgeCooldownTimer = dodgeCooldown;
            currentStamina -= dodgeCost;
            AudioManager.PlayDodge();
            StartCoroutine(DodgeBackward());
        }
    }

    IEnumerator DodgeBackward()
    {
        isDodging = true;
        isInvincible = true;

        // 严格抹平 Y 轴：冲刺方向必须纯水平，不允许任何垂直分量混入
        Vector3 fwd = transform.forward;
        fwd.y = 0f;
        dodgeDirection = -fwd.normalized;

        float elapsed = 0f;
        while (elapsed < dodgeDuration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 退出冲刺：彻底清空残余冲力变量，防止"幽灵冲刺方向"持续作用
        dodgeDirection = Vector3.zero;
        isDodging = false;
        velocity.x = 0f;
        velocity.z = 0f;

        float remainTime = invincibleDuration - dodgeDuration;
        if (remainTime > 0f)
            yield return new WaitForSeconds(remainTime);

        isInvincible = false;
    }

    public void ReceiveAttack(float damage)
    {
        if (isDead) return;
        if (isInvincible)
        {
            hasPerfectDodgeBuff = true;
            dodgeFlashTimer = dodgeFlashDuration;
            GameLogger.Log("完美闪避！触发子弹时间！");
            AudioManager.PlayPerfectDodge();
            StartCoroutine(PerfectDodgeFeedback());
            return;
        }

        playerCurrentHealth -= damage;
        GameLogger.Log($"玩家受到 {damage:F1} 点伤害！剩余血量: {playerCurrentHealth:F0}");

        if (playerCurrentHealth <= 0)
        {
            playerCurrentHealth = 0;
            Die();
        }
    }

    void Die()
    {
        isDead = true;
        isWitchTime = false;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        GameLogger.Log("玩家阵亡！");
    }

    public bool IsDead()
    {
        return isDead;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 秩序态对外暴露：按住 Shift = isOrder == true
    // CanPierceCore：统一的"破核权限"
    //   满足以下任一条件即可破核：
    //     1. isOrder（按住 Shift 进入秩序态）
    //     2. isWitchTime（完美闪避后的 0.5s 慢动作窗口）
    //     3. hasPerfectDodgeBuff（完美闪避后的完整 Buff 窗口，含慢动作 + 2s 余韵）
    // EnemyOrderSystem 用 CanPierceCore 判断视觉显隐、攻击预警、伤害结算
    // ─────────────────────────────────────────────────────────────────────────
    public bool isOrder => !isDead && Input.GetKey(KeyCode.LeftShift);
    public bool IsWitchTime => isWitchTime;
    public bool CanPierceCore => isOrder || isWitchTime || hasPerfectDodgeBuff;

    IEnumerator PerfectDodgeFeedback()
    {
        isWitchTime = true;
        Time.timeScale = 0.2f;
        yield return new WaitForSecondsRealtime(0.5f);
        Time.timeScale = 1f;
        isWitchTime = false;

        yield return new WaitForSeconds(2f);
        hasPerfectDodgeBuff = false;
        GameLogger.Log("防反破核Buff已消失");
    }

    void HandleShoot()
    {
        // GetMouseButtonDown 确保每次点击只判定一次，杜绝音效被 GetMouseButton 长按触发每帧重复
        if (Input.GetMouseButtonDown(0) && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + fireRate;
            AudioManager.PlayFire();
            FireBullet();
        }
    }

    void FireBullet()
    {
        GameObject bullet = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bullet.name = "Bullet";
        bullet.transform.position = mainCamera.transform.position;
        bullet.transform.rotation = mainCamera.transform.rotation;
        bullet.transform.localScale = new Vector3(0.05f, 0.05f, 0.2f);

        Renderer renderer = bullet.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = Color.cyan;
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", Color.cyan * 5f);
        renderer.material = mat;

        Destroy(bullet.GetComponent<Collider>());

        BulletBehaviour bulletBehaviour = bullet.AddComponent<BulletBehaviour>();
        bulletBehaviour.Init(10f, hasPerfectDodgeBuff);
    }

    void InitDeathButtonStyle()
    {
        if (deathButtonStyle != null) return;
        deathButtonStyle = new GUIStyle(GUI.skin.button);
        deathButtonStyle.fontSize = 22;
        deathButtonStyle.fontStyle = FontStyle.Bold;
        deathButtonStyle.normal.textColor = Color.white;
        deathButtonStyle.normal.background = Texture2D.whiteTexture;
        deathButtonStyle.hover.textColor = Color.cyan;
        deathButtonStyle.hover.background = Texture2D.whiteTexture;
        deathButtonStyle.active.textColor = Color.yellow;
        deathButtonStyle.alignment = TextAnchor.MiddleCenter;
    }

    void OnGUI()
    {
        if (isDead)
        {
            DrawDeathUI();
            return;
        }

        float crosshairSize = 20f;
        float crosshairThickness = 2f;
        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;

        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(cx - crosshairSize * 0.5f, cy - crosshairThickness * 0.5f, crosshairSize, crosshairThickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(cx - crosshairThickness * 0.5f, cy - crosshairSize * 0.5f, crosshairThickness, crosshairSize), Texture2D.whiteTexture);

        if (dodgeFlashTimer > 0f)
        {
            Vector3 playerScreen = mainCamera.WorldToScreenPoint(transform.position + Vector3.up * 0.5f);
            if (playerScreen.z > 0f)
            {
                float flashAlpha = dodgeFlashTimer / dodgeFlashDuration;
                float flashRadius = Mathf.Lerp(30f, 80f, 1f - flashAlpha);
                float px = playerScreen.x;
                float py = Screen.height - playerScreen.y;
                GUI.color = new Color(1f, 1f, 1f, flashAlpha * 0.7f);
                GUI.DrawTexture(new Rect(px - flashRadius, py - flashRadius, flashRadius * 2f, flashRadius * 2f), Texture2D.whiteTexture);
            }
        }

        float healthX = 20f;
        float healthY = Screen.height - 30f;
        float healthWidth = 150f;
        float healthHeight = 15f;
        float healthRatio = playerCurrentHealth / playerMaxHealth;

        GUI.color = Color.gray;
        GUI.DrawTexture(new Rect(healthX, healthY, healthWidth, healthHeight), Texture2D.whiteTexture);

        GUI.color = Color.green;
        GUI.DrawTexture(new Rect(healthX, healthY, healthWidth * healthRatio, healthHeight), Texture2D.whiteTexture);

        Vector3 centerWorldPos = transform.position + Vector3.up * 0.5f + mainCamera.transform.right * 0.4f;
        Vector3 screenPos = mainCamera.WorldToScreenPoint(centerWorldPos);

        if (screenPos.z > 0f)
        {
            float guiY = Screen.height - screenPos.y;
            float radius = 50f;
            float staminaRatio = currentStamina / maxStamina;
            float currentMaxAngle = -60f + 120f * staminaRatio;

            Texture2D pointTex = Texture2D.whiteTexture;

            for (float angle = -60f; angle <= currentMaxAngle; angle += 3f)
            {
                float rad = angle * Mathf.Deg2Rad;
                float x = screenPos.x + Mathf.Cos(rad) * radius;
                float y = guiY - Mathf.Sin(rad) * radius;
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(x - 1f, y - 1f, 6, 6), pointTex);
            }

            GUI.color = currentStamina > 30f ? Color.yellow : Color.red;

            for (float angle = -60f; angle <= currentMaxAngle; angle += 3f)
            {
                float rad = angle * Mathf.Deg2Rad;
                float x = screenPos.x + Mathf.Cos(rad) * radius;
                float y = guiY - Mathf.Sin(rad) * radius;
                GUI.DrawTexture(new Rect(x, y, 4, 4), pointTex);
            }
        }

        GUI.color = Color.white;
    }

    void DrawDeathUI()
    {
        InitDeathButtonStyle();

        GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.6f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 120;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.normal.textColor = Color.red;
        titleStyle.hover.textColor = Color.red;
        titleStyle.active.textColor = Color.red;
        titleStyle.focused.textColor = Color.red;

        float titleY = Screen.height * 0.25f;
        GUI.Label(new Rect(0, titleY, Screen.width, 150f), "卡 了", titleStyle);

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

        if (GUI.Button(new Rect(btnX, btnStartY, btnWidth, btnHeight), "重新开始", deathButtonStyle))
        {
            isDead = false;
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        if (GUI.Button(new Rect(btnX, btnStartY + btnHeight + spacing, btnWidth, btnHeight), "返回菜单", deathButtonStyle))
        {
            isDead = false;
            Time.timeScale = 1f;
            SceneManager.LoadScene(0);
        }

        GUI.contentColor = Color.white;
    }
}
