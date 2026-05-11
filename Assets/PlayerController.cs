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
    public float dodgeCost = 20f;
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

    void Awake()
    {
        dodgeSpeed = 6f;
        dodgeDuration = 0.15f;
        invincibleDuration = 0.5f;
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
            camObj.transform.SetParent(transform);
            camObj.transform.localPosition = new Vector3(0f, 1.2f, -2f);
            camObj.transform.localRotation = Quaternion.identity;
            mainCamera = camObj.AddComponent<Camera>();
        }

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
        if (canAct && !isDodging)
            move = GetMovementInput();

        if (canAct)
        {
            HandleJump();
            HandleDodge();
            HandleShoot();
        }

        if (velocity.y < 0f)
            velocity.y += gravity * fallGravityMultiplier * Time.deltaTime;
        else
            velocity.y += gravity * Time.deltaTime;

        controller.Move((move * moveSpeed + velocity) * Time.deltaTime);
    }

    void HandleEnemyOrder()
    {
        if (enemyOrderSystem == null) return;

        if (Input.GetKey(KeyCode.LeftShift))
            enemyOrderSystem.SwitchToOrder(true);
        else
            enemyOrderSystem.SwitchToOrder(false);
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(Vector3.up * mouseX);

        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, -60f, 60f);
        mainCamera.transform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
    }

    Vector3 GetMovementInput()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        return (transform.right * horizontal + transform.forward * vertical).normalized;
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
        if (dodgeBufferTimer > 0f && !Input.GetKey(KeyCode.LeftShift) && controller.isGrounded && !isDodging && currentStamina >= dodgeCost)
        {
            dodgeBufferTimer = 0f;
            currentStamina -= dodgeCost;
            StartCoroutine(DodgeBackward());
        }
    }

    IEnumerator DodgeBackward()
    {
        isDodging = true;
        isInvincible = true;
        float elapsed = 0f;

        while (elapsed < dodgeDuration)
        {
            controller.Move(-transform.forward * dodgeSpeed * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        isDodging = false;

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
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        GameLogger.Log("玩家阵亡！");
    }

    public bool IsDead()
    {
        return isDead;
    }

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
        if (Input.GetMouseButton(0) && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + fireRate;
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
