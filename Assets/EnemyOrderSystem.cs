using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum EnemyState
{
    Normal,
    Telegraphing,
    Recovery,
    Stunned
}

public class EnemyOrderSystem : MonoBehaviour
{
    [Header("血量")]
    public float maxHealth = 1000f;
    private float currentHealth;

    [Header("战斗参数")]
    public float waitDuration = 3f;
    public float telegraphDuration = 1.5f;
    public float recoveryDuration = 1.5f;
    public float stunDuration = 5f;
    public float damageReduction = 0.1f;
    public float critMultiplier = 2.0f;

    [Header("招式0：直线冲锋")]
    public float chargeSpeed = 20f;
    public float chargeDuration = 0.3f;
    public float chargeDamageRange = 5f;
    public float chargeDamage = 25f;

    [Header("招式1：扇形横扫")]
    public float sweepDamageRange = 4f;
    public float sweepDamage = 20f;

    [Header("招式2：震地冲击波")]
    public float shockwaveExpandRadius = 12f;
    public float shockwaveExpandDuration = 0.6f;
    public float shockwaveDamage = 35f;
    public float shockwaveGroundThreshold = 1.5f;

    [Header("招式3：隐形追踪飞弹")]
    public int missileCount = 3;
    public float missileInterval = 0.4f;
    public float missileDamage = 15f;

    [Header("血条样式")]
    public float healthBarWidth = 200f;
    public float healthBarHeight = 20f;
    public float healthBarOffsetY = 50f;

    // 视觉层对象
    private GameObject chaosVisual;
    private GameObject orderCore;
    private GameObject warningArea;
    private GameObject shockwaveRing;
    private GameObject telegraphFlash;

    // 材质缓存
    private Material warningMat;
    private Material shockwaveMat;

    // 状态
    private bool useURP;
    private EnemyState currentState = EnemyState.Normal;
    private bool isOrderVision = false;
    private int currentAttackType = -1;

    // 协程句柄
    private Coroutine attackCycleCoroutine;
    private Coroutine stunTimerCoroutine;

    private PlayerController playerRef;
    private List<HomingProjectile> activeMissiles = new List<HomingProjectile>();

    // 新增：招式0线段判定宽度 / 扇形方块列表
    private float chargeWidth = 1.5f;
    private float sweepDamageAngle = 90f;
    private List<GameObject> sweepCubes = new List<GameObject>();
    private GameObject[] sweepCubePool;

    void Awake()
    {
        maxHealth = 1000f;
        waitDuration = 3f;
        telegraphDuration = 1.5f;
        recoveryDuration = 1.5f;
        stunDuration = 5f;
        damageReduction = 0.1f;
        critMultiplier = 2.0f;
        chargeSpeed = 20f;
        chargeDuration = 0.3f;
        chargeDamageRange = 5f;
        chargeDamage = 25f;
        sweepDamageRange = 4f;
        sweepDamage = 20f;
        shockwaveExpandRadius = 12f;
        shockwaveExpandDuration = 0.6f;
        shockwaveDamage = 35f;
        shockwaveGroundThreshold = 1.5f;
        missileCount = 3;
        missileInterval = 0.4f;
        missileDamage = 15f;
        healthBarWidth = 200f;
        healthBarHeight = 20f;
        healthBarOffsetY = 50f;
    }

    #region 初始化

    void Start()
    {
        currentHealth = maxHealth;
        useURP = Shader.Find("Universal Render Pipeline/Lit") != null;
        playerRef = FindObjectOfType<PlayerController>();

        CreateChaosVisual();
        CreateOrderCore();
        CreateWarningArea();
        CreateShockwaveRing();
        CreateTelegraphFlash();
        CreateSweepCubePool();
        SetInitialVisuals();

        attackCycleCoroutine = StartCoroutine(AttackCycle());
    }

    void Update()
    {
        for (int i = activeMissiles.Count - 1; i >= 0; i--)
        {
            if (activeMissiles[i] == null)
            {
                activeMissiles.RemoveAt(i);
                continue;
            }
            activeMissiles[i].SetVisible(isOrderVision);
        }

        if (telegraphFlash != null && telegraphFlash.activeSelf)
            telegraphFlash.SetActive(isOrderVision);
    }

    #endregion

    // ────────────────────────────────────────────
    #region 材质工具

    Material CreateLitMaterial(Color color)
    {
        Shader shader = useURP
            ? Shader.Find("Universal Render Pipeline/Lit")
            : Shader.Find("Standard");
        return new Material(shader) { color = color };
    }

    void SetMaterialOpaque(Material mat, float smoothness)
    {
        if (useURP)
        {
            mat.SetFloat("_Surface", 0);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0f);
        }
        else
        {
            mat.SetFloat("_Mode", 0);
            mat.SetFloat("_Glossiness", smoothness);
            mat.SetFloat("_Metallic", 0f);
            mat.renderQueue = 2000;
        }
    }

    Material CreateTransparentMaterial(Color color)
    {
        Material mat = CreateLitMaterial(color);
        if (useURP)
        {
            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_Blend", 0);
            mat.SetFloat("_ZWrite", 0);
            mat.SetFloat("_AlphaClip", 0);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.renderQueue = 3000;
        }
        else
        {
            mat.SetFloat("_Mode", 3);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }
        mat.color = color;
        return mat;
    }

    #endregion

    // ────────────────────────────────────────────
    #region 视觉层生成

    void CreateChaosVisual()
    {
        chaosVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        chaosVisual.name = "chaosVisual";
        chaosVisual.transform.SetParent(transform);
        chaosVisual.transform.localPosition = Vector3.zero;
        chaosVisual.transform.localScale = Vector3.one * 1.5f;

        Material mat = CreateLitMaterial(new Color(0.5f, 0.05f, 0.05f));
        SetMaterialOpaque(mat, 0f);
        chaosVisual.GetComponent<Renderer>().material = mat;
    }

    void CreateOrderCore()
    {
        orderCore = GameObject.CreatePrimitive(PrimitiveType.Cube);
        orderCore.name = "orderCore";
        orderCore.transform.SetParent(transform);
        orderCore.transform.localPosition = Vector3.zero;
        orderCore.transform.localScale = Vector3.one * 0.5f;

        Material mat = CreateLitMaterial(Color.blue);
        SetMaterialOpaque(mat, 0.8f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", Color.cyan * 5f);
        orderCore.GetComponent<Renderer>().material = mat;
    }

    void CreateWarningArea()
    {
        warningArea = GameObject.CreatePrimitive(PrimitiveType.Plane);
        warningArea.name = "warningArea";
        warningArea.transform.SetParent(transform);
        // Y=0.1f 防止与地面穿模
        warningArea.transform.localPosition = new Vector3(0f, 0.1f, 4f);
        warningArea.transform.localScale = new Vector3(0.5f, 1f, 8f);

        warningMat = CreateTransparentMaterial(new Color(1f, 0.9f, 0f, 0.45f));
        warningMat.EnableKeyword("_EMISSION");
        warningMat.SetColor("_EmissionColor", new Color(1f, 0.9f, 0f) * 3f);
        warningArea.GetComponent<Renderer>().material = warningMat;
        Destroy(warningArea.GetComponent<Collider>());
    }

    void CreateShockwaveRing()
    {
        shockwaveRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shockwaveRing.name = "shockwaveRing";
        shockwaveRing.transform.SetParent(transform);
        // 贴地但高出 0.15f，防止与地面完全重叠
        shockwaveRing.transform.localPosition = new Vector3(0f, 0.15f, 0f);
        shockwaveRing.transform.localScale = new Vector3(0.5f, 0.08f, 0.5f);

        shockwaveMat = CreateTransparentMaterial(new Color(1f, 0.1f, 0f, 0.6f));
        shockwaveMat.EnableKeyword("_EMISSION");
        shockwaveMat.SetColor("_EmissionColor", new Color(1f, 0.1f, 0f) * 5f);
        shockwaveRing.GetComponent<Renderer>().material = shockwaveMat;
        Destroy(shockwaveRing.GetComponent<Collider>());
    }

    void CreateTelegraphFlash()
    {
        telegraphFlash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        telegraphFlash.name = "telegraphFlash";
        telegraphFlash.transform.SetParent(transform);
        telegraphFlash.transform.localPosition = new Vector3(0f, 1f, 0f);
        telegraphFlash.transform.localScale = Vector3.one * 0.35f;

        Material mat = CreateLitMaterial(Color.yellow);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(1f, 0.9f, 0f) * 10f);
        telegraphFlash.GetComponent<Renderer>().material = mat;
        Destroy(telegraphFlash.GetComponent<Collider>());
        telegraphFlash.SetActive(false);
    }

    void CreateSweepCubePool()
    {
        sweepCubePool = new GameObject[5];
        for (int i = 0; i < 5; i++)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "SweepCube_" + i;
            cube.transform.SetParent(transform);
            cube.transform.localScale = Vector3.one * 0.6f;

            Material mat = CreateLitMaterial(Color.red);
            SetMaterialOpaque(mat, 0.1f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.red * 4f);
            cube.GetComponent<Renderer>().material = mat;
            Destroy(cube.GetComponent<Collider>());
            cube.SetActive(false);
            sweepCubePool[i] = cube;
        }
    }

    void ShowTelegraphFlash()
    {
        if (telegraphFlash == null) return;
        telegraphFlash.transform.localPosition = new Vector3(0f, 1f, 0f);
        telegraphFlash.SetActive(isOrderVision);
    }

    void HideTelegraphFlash()
    {
        if (telegraphFlash == null) return;
        telegraphFlash.SetActive(false);
    }

    void ShowSweepCubes()
    {
        for (int i = 0; i < 5; i++)
        {
            float angle = -90f + 45f * i;
            float rad = angle * Mathf.Deg2Rad;
            float dist = 3.5f;
            Vector3 localPos = new Vector3(Mathf.Sin(rad) * dist, 0.3f, Mathf.Cos(rad) * dist);
            sweepCubePool[i].transform.position = transform.TransformPoint(localPos);
            sweepCubePool[i].SetActive(true);
        }
    }

    void HideSweepCubes()
    {
        for (int i = 0; i < 5; i++)
            sweepCubePool[i].SetActive(false);
    }

    void DestroySweepCubes()
    {
        for (int i = 0; i < 5; i++)
            sweepCubePool[i].SetActive(false);
    }

    float PointToSegmentDistance(Vector3 point, Vector3 segA, Vector3 segB)
    {
        Vector3 AB = segB - segA;
        Vector3 AP = point - segA;
        float ab2 = Vector3.Dot(AB, AB);
        if (ab2 < 0.0001f) return Vector3.Distance(point, segA);
        float t = Mathf.Clamp01(Vector3.Dot(AP, AB) / ab2);
        Vector3 closest = segA + t * AB;
        return Vector3.Distance(point, closest);
    }

    void SetInitialVisuals()
    {
        chaosVisual.SetActive(true);
        orderCore.SetActive(false);
        warningArea.SetActive(false);
        shockwaveRing.SetActive(false);
    }

    #endregion

    // ────────────────────────────────────────────
    #region 视野切换（Shift 显隐铁律）

    // 由 PlayerController 每帧调用
    public void SwitchToOrder(bool isOrder)
    {
        isOrderVision = isOrder;
        ApplyVisibility();
    }

    void ApplyVisibility()
    {
        switch (currentState)
        {
            case EnemyState.Stunned:
                chaosVisual.SetActive(false);
                orderCore.SetActive(true);
                warningArea.SetActive(false);
                // 震地波的ring在瘫痪时不应继续显示
                shockwaveRing.SetActive(false);
                break;

            case EnemyState.Telegraphing:
                chaosVisual.SetActive(!isOrderVision);
                orderCore.SetActive(isOrderVision);
                // Case 2（震地）：ring始终可见，无视Shift；其余预警仅Shift可见
                if (currentAttackType == 2)
                {
                    warningArea.SetActive(false);
                    shockwaveRing.SetActive(true);
                }
                else if (currentAttackType == 3)
                {
                    // 飞弹招式无场地预警
                    warningArea.SetActive(false);
                    shockwaveRing.SetActive(false);
                }
                else
                {
                    // Case 0 & 1：仅 Shift 可见
                    warningArea.SetActive(isOrderVision);
                    shockwaveRing.SetActive(false);
                }
                break;

            case EnemyState.Recovery:
                chaosVisual.SetActive(!isOrderVision);
                orderCore.SetActive(isOrderVision);
                warningArea.SetActive(false);
                shockwaveRing.SetActive(false);
                break;

            default: // Normal
                chaosVisual.SetActive(true);
                orderCore.SetActive(false);
                warningArea.SetActive(false);
                shockwaveRing.SetActive(false);
                break;
        }
    }

    #endregion

    // ────────────────────────────────────────────
    #region 战斗循环主体

    IEnumerator AttackCycle()
    {
        while (true)
        {
            // ── Normal 待机 ──
            currentState = EnemyState.Normal;
            currentAttackType = -1;
            ApplyVisibility();
            yield return new WaitForSeconds(waitDuration);

            // ── 随机选择招式 ──
            currentAttackType = Random.Range(0, 4);
            currentState = EnemyState.Telegraphing;

            switch (currentAttackType)
            {
                case 0: yield return StartCoroutine(Attack0_Dash());     break;
                case 1: yield return StartCoroutine(Attack1_Sweep());    break;
                case 2: yield return StartCoroutine(Attack2_Shockwave()); break;
                case 3: yield return StartCoroutine(Attack3_Missiles()); break;
            }

            // ── Recovery 后摇（期间可被防反） ──
            currentState = EnemyState.Recovery;
            ApplyVisibility();
            yield return new WaitForSeconds(recoveryDuration);
        }
    }

    #endregion

    // ────────────────────────────────────────────
    #region Case 0：直线冲锋

    IEnumerator Attack0_Dash()
    {
        FacePlayer();

        warningArea.transform.localPosition = new Vector3(0f, 0.1f, 4f);
        warningArea.transform.localScale = new Vector3(0.5f, 1f, 8f);
        warningArea.transform.localRotation = Quaternion.identity;
        ApplyVisibility();

        float elapsed = 0f;
        while (elapsed < telegraphDuration)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / telegraphDuration);
            warningArea.transform.localScale = new Vector3(
                Mathf.Lerp(0.5f, 0.73f, p), 1f, Mathf.Lerp(8f, 12f, p));

            if (elapsed >= telegraphDuration - 0.2f)
                ShowTelegraphFlash();

            yield return null;
        }

        HideTelegraphFlash();
        warningArea.SetActive(false);

        Vector3 startPos = transform.position;
        Vector3 chargeDir = transform.forward;
        Vector3 chargeTarget = startPos + chargeDir * 6f;
        elapsed = 0f;
        bool damaged = false;

        while (elapsed < chargeDuration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, chargeTarget, Mathf.Clamp01(elapsed / chargeDuration));

            if (!damaged && playerRef != null)
            {
                float perpDist = PointToSegmentDistance(playerRef.transform.position, startPos, chargeTarget);
                float playerProj = Vector3.Dot(playerRef.transform.position - startPos, chargeDir);
                float segLen = Vector3.Distance(startPos, chargeTarget);
                if (perpDist < chargeWidth && playerProj >= 0f && playerProj <= segLen)
                {
                    playerRef.ReceiveAttack(chargeDamage);
                    damaged = true;
                }
            }
            yield return null;
        }

        transform.position = chargeTarget;
    }

    #endregion

    // ────────────────────────────────────────────
    #region Case 1：扇形横扫

    IEnumerator Attack1_Sweep()
    {
        FacePlayer();

        warningArea.transform.localPosition = new Vector3(0f, 0.1f, 2f);
        warningArea.transform.localScale = new Vector3(2f, 1f, 3f);
        warningArea.transform.localRotation = Quaternion.identity;
        ApplyVisibility();

        float elapsed = 0f;
        while (elapsed < telegraphDuration)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / telegraphDuration);
            warningArea.transform.localScale = new Vector3(
                Mathf.Lerp(2f, 4f, p), 1f, Mathf.Lerp(3f, 5f, p));

            if (elapsed >= telegraphDuration - 0.2f)
                ShowTelegraphFlash();

            yield return null;
        }

        HideTelegraphFlash();
        warningArea.SetActive(false);

        ShowSweepCubes();
        yield return new WaitForSeconds(0.3f);
        HideSweepCubes();

        if (playerRef != null)
        {
            Vector3 toPlayer = playerRef.transform.position - transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;
            float angle = Vector3.Angle(transform.forward, toPlayer);

            if (dist < sweepDamageRange && angle < sweepDamageAngle)
                playerRef.ReceiveAttack(sweepDamage);
        }
    }

    #endregion

    // ────────────────────────────────────────────
    #region Case 2：震地冲击波（特殊：ring无需 Shift 也可见）

    IEnumerator Attack2_Shockwave()
    {
        shockwaveRing.transform.localPosition = new Vector3(0f, 0.15f, 0f);
        shockwaveRing.transform.localScale = new Vector3(0.5f, 0.08f, 0.5f);
        shockwaveMat.color = new Color(1f, 0.1f, 0f, 0.6f);
        shockwaveRing.SetActive(true);
        ApplyVisibility();

        float elapsed = 0f;
        while (elapsed < 1f)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / 1f);
            float s = Mathf.Lerp(0.5f, 2f, p);
            shockwaveRing.transform.localScale = new Vector3(s, 0.08f, s);

            if (elapsed >= 0.8f)
                ShowTelegraphFlash();

            yield return null;
        }

        HideTelegraphFlash();

        elapsed = 0f;
        bool damaged = false;

        while (elapsed < shockwaveExpandDuration)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / shockwaveExpandDuration);
            float s = Mathf.Lerp(2f, shockwaveExpandRadius, p);
            shockwaveRing.transform.localScale = new Vector3(s, 0.08f, s);

            Color c = shockwaveMat.color;
            c.a = Mathf.Lerp(0.6f, 0.05f, p);
            shockwaveMat.color = c;

            if (!damaged && playerRef != null)
            {
                float ringRadius = s * 5f;
                float dist = Vector3.Distance(
                    new Vector3(transform.position.x, 0f, transform.position.z),
                    new Vector3(playerRef.transform.position.x, 0f, playerRef.transform.position.z));

                if (Mathf.Abs(dist - ringRadius) < 1.0f && playerRef.transform.position.y < shockwaveGroundThreshold)
                {
                    playerRef.ReceiveAttack(shockwaveDamage);
                    damaged = true;
                }
            }
            yield return null;
        }

        shockwaveRing.SetActive(false);
        shockwaveMat.color = new Color(1f, 0.1f, 0f, 0.6f);
    }

    #endregion

    // ────────────────────────────────────────────
    #region Case 3：隐形追踪飞弹

    IEnumerator Attack3_Missiles()
    {
        // 无场地预警，直接依次生成飞弹
        for (int i = 0; i < missileCount; i++)
        {
            SpawnHomingMissile();
            yield return new WaitForSeconds(missileInterval);
        }
        // 协程结束后由 AttackCycle 进入 Recovery
    }

    void SpawnHomingMissile()
    {
        Vector3 spawnOffset = new Vector3(
            Random.Range(-0.6f, 0.6f),
            1.2f + Random.Range(0f, 0.6f),
            Random.Range(-0.6f, 0.6f));

        GameObject missileObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        missileObj.name = "EnemyMissile";
        missileObj.transform.position = transform.position + spawnOffset;
        missileObj.transform.localScale = new Vector3(0.3f, 0.3f, 0.5f);

        Renderer rend = missileObj.GetComponent<Renderer>();
        Material mat = CreateLitMaterial(Color.red);
        SetMaterialOpaque(mat, 0.3f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", Color.red * 4f);
        rend.material = mat;
        rend.enabled = isOrderVision; // 初始状态同步

        HomingProjectile homing = missileObj.AddComponent<HomingProjectile>();
        homing.Init(playerRef != null ? playerRef.transform : null, missileDamage);

        activeMissiles.Add(homing);
    }

    #endregion

    // ────────────────────────────────────────────
    #region 工具方法

    void FacePlayer()
    {
        if (playerRef == null) return;
        Vector3 dir = playerRef.transform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(dir);
    }

    #endregion

    // ────────────────────────────────────────────
    #region 伤害结算与破防

    public void TakeDamage(float damage, bool isPerfectCounter)
    {
        if (currentHealth <= 0) return;

        switch (currentState)
        {
            case EnemyState.Normal:
            case EnemyState.Telegraphing:
                float reducedDamage = damage * damageReduction;
                currentHealth -= reducedDamage;
                break;

            case EnemyState.Recovery:
                if (isPerfectCounter)
                {
                    TriggerStun();
                    return;
                }
                currentHealth -= damage;
                break;

            case EnemyState.Stunned:
                float critDamage = damage * critMultiplier;
                currentHealth -= critDamage;
                break;
        }

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
    }

    void TriggerStun()
    {
        // 打断攻击协程
        if (attackCycleCoroutine != null)
        {
            StopCoroutine(attackCycleCoroutine);
            attackCycleCoroutine = null;
        }

        warningArea.SetActive(false);
        shockwaveRing.SetActive(false);
        HideTelegraphFlash();
        HideSweepCubes();

        for (int i = activeMissiles.Count - 1; i >= 0; i--)
        {
            if (activeMissiles[i] != null)
                Destroy(activeMissiles[i].gameObject);
        }
        activeMissiles.Clear();

        currentState = EnemyState.Stunned;
        currentAttackType = -1;

        // 强制暴露核心
        chaosVisual.SetActive(false);
        orderCore.SetActive(true);

        stunTimerCoroutine = StartCoroutine(StunTimer());
    }

    IEnumerator StunTimer()
    {
        yield return new WaitForSeconds(stunDuration);

        currentState = EnemyState.Normal;
        currentAttackType = -1;
        chaosVisual.SetActive(true);
        orderCore.SetActive(false);
        warningArea.SetActive(false);
        shockwaveRing.SetActive(false);

        attackCycleCoroutine = StartCoroutine(AttackCycle());
    }

    void Die()
    {
        if (attackCycleCoroutine != null)
            StopCoroutine(attackCycleCoroutine);
        if (stunTimerCoroutine != null)
            StopCoroutine(stunTimerCoroutine);

        for (int i = activeMissiles.Count - 1; i >= 0; i--)
        {
            if (activeMissiles[i] != null)
                Destroy(activeMissiles[i].gameObject);
        }
        activeMissiles.Clear();

        SpawnDeathEffect();
        Destroy(gameObject);
    }

    void SpawnDeathEffect()
    {
        for (int i = 0; i < 20; i++)
        {
            GameObject fragment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fragment.transform.position = transform.position + Random.insideUnitSphere * 0.5f;
            fragment.transform.localScale = Vector3.one * Random.Range(0.1f, 0.3f);

            Renderer rend = fragment.GetComponent<Renderer>();
            Material mat = CreateLitMaterial(Color.red);
            SetMaterialOpaque(mat, 0.2f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.red * 3f);
            rend.material = mat;

            Rigidbody rb = fragment.AddComponent<Rigidbody>();
            rb.AddExplosionForce(500f, transform.position, 2f);
            Destroy(fragment, 2f);
        }
    }

    #endregion

    // ────────────────────────────────────────────
    #region 血条 OnGUI

    void OnGUI()
    {
        if (Camera.main == null || currentHealth <= 0) return;

        Vector3 worldPos = transform.position + Vector3.up * 2.5f;
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
        if (screenPos.z <= 0) return;

        screenPos.y = Screen.height - screenPos.y;
        float barX = screenPos.x - healthBarWidth * 0.5f;
        float barY = screenPos.y - healthBarOffsetY;

        // 背景
        GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        GUI.DrawTexture(new Rect(barX, barY, healthBarWidth, healthBarHeight), Texture2D.whiteTexture);

        // 血量
        float ratio = currentHealth / maxHealth;
        GUI.color = currentState == EnemyState.Stunned ? Color.cyan : Color.red;
        GUI.DrawTexture(new Rect(barX, barY, healthBarWidth * ratio, healthBarHeight), Texture2D.whiteTexture);

        // 文字
        GUI.color = Color.white;
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12,
            fontStyle = FontStyle.Bold
        };
        GUI.Label(new Rect(barX, barY, healthBarWidth, healthBarHeight),
            $"{Mathf.Ceil(currentHealth)} / {maxHealth}", style);

        // 瘫痪提示
        if (currentState == EnemyState.Stunned)
        {
            GUIStyle stunnedStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            stunnedStyle.normal.textColor = Color.cyan;
            GUI.Label(new Rect(barX, barY - 20f, healthBarWidth, 20f), "【核心暴露！】", stunnedStyle);
        }

        GUI.color = Color.white;
    }

    #endregion
}
