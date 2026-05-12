// ─────────────────────────────────────────────────────────────────────────────
// EnemyOrderSystem.cs  ── 核心战斗系统完整重构版
//
// 四大招式精确实现：
//   招式 0 ── OBB 矩形冲锋       （dot/cross 精确矩形，误差 = 0）
//   招式 1 ── 程序化扇形横扫     （fanMesh 顶点 = 判定半径/角度，误差 = 0）
//   招式 2 ── 动态空心圆环冲击波 （UpdateRingMesh 每帧更新，innerR/outerR 直接判定）
//   招式 3 ── 贝塞尔三弹道飞弹  （P0→P1→P2 二次贝塞尔，三条不同弧线）
// ─────────────────────────────────────────────────────────────────────────────
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum EnemyState { Normal, Telegraphing, Recovery, Stunned }
public enum BossPhase { Phase1, Transitioning, Phase2 }

public class EnemyOrderSystem : MonoBehaviour
{
    // ─────────────────────────────── Inspector ────────────────────────────────
    [Header("血量")]
    public float maxHealth = 1000f;
    private float currentHealth;

    [Header("战斗参数")]
    public float waitDuration      = 3f;
    public float telegraphDuration = 1.5f;
    public float recoveryDuration  = 1.5f;
    public float stunDuration      = 5f;
    public float damageReduction   = 0.1f;
    public float critMultiplier    = 2.0f;

    [Header("招式 0：直线冲锋")]
    public float chargeSpeed     = 20f;
    public float chargeDuration  = 0.3f;
    public float chargeDamage    = 25f;
    public float chargeLength    = 8f;    // 路径长度（米）
    public float chargeHalfWidth = 1f;    // 矩形半宽（米）→ 全宽 2m

    [Header("招式 1：扇形横扫")]
    public float sweepRadius    = 4.5f;   // 与 fanMesh 顶点半径完全一致
    public float sweepHalfAngle = 90f;    // 180° 扇形，半角 90°
    public float sweepDamage    = 20f;

    [Header("招式 2：震地冲击波")]
    public float shockwaveMaxRadius      = 8f;
    public float shockwaveExpandTime     = 1f;
    public float shockwaveRingThickness  = 1.5f;  // 空心圆环实体厚度（米）
    public float shockwaveDamage         = 35f;
    public float shockwaveGroundY        = 1.5f;  // 玩家 Y 低于此值视为在地面

    [Header("招式 3：贝塞尔飞弹")]
    public int   missileCount    = 3;
    public float missileDamage   = 15f;
    public float missileDuration = 0.7f;   // 单枚飞弹飞行时长（秒）

    [Header("血条样式")]
    public float healthBarWidth   = 200f;
    public float healthBarHeight  = 20f;
    public float healthBarOffsetY = 50f;

    // ──────────────────────────── 视觉层对象 ──────────────────────────────────
    private GameObject warningArea;       // 招式 0 矩形预警 / 招式 3 追踪预警圆
    private GameObject shockwaveRingObj;  // 招式 2 动态圆环（程序化 Mesh）
    private GameObject telegraphFlash;    // 蓄力黄点
    private GameObject sweepFanObj;       // 招式 1 程序化扇形体

    // 程序化 Mesh 引用
    private Mesh ringMesh;
    private Mesh fanMesh;

    // 材质缓存
    private Material warningMat;
    private Material shockwaveMat;
    private Material flashMat;
    private Material sweepFanMat;

    // ──────────────────────────── 三体幻影系统 ────────────────────────────────
    private GameObject[]     drones;
    private GameObject[]     droneCores;
    private Material         droneChaosMat;
    private Material         droneOrderMat;
    private Material         droneCoreMat;
    private int              currentCoreIndex   = -1;
    private Material         transparentMat;
    private Vector3[]        droneBasePositions;
    private const float      orbitRadius   = 8f;
    private const float      orbitBaseY    = 0.5f;
    private const float      attackScale   = 1.8f;

    private struct DronePatrol
    {
        public Vector3 moveDirection;
        public float   stateTimer;
        public bool    isMoving;
    }
    private DronePatrol[] patrolStates;
    private const float patrolSpeed   = 3f;
    private const float patrolMinDist = 3f;
    private const float patrolMaxDist = 12f;

    // ──────────────────────────── 状态 ────────────────────────────────────────
    private bool       useURP;
    private EnemyState currentState      = EnemyState.Normal;
    private bool       isOrderVision     = false;  // 由 playerRef.CanPierceCore 驱动
    private int        currentAttackType = -1;
    private int        currentAttackerIndex = -1;
    private float      hitCooldown = 0f;
    private Transform  activeAttacker;

    private Coroutine attackCycleCoroutine;
    private Coroutine stunTimerCoroutine;
    private Coroutine phaseSplitCoroutine;

    private PlayerController       playerRef;
    private List<GameObject>       activeMissileObjs = new List<GameObject>();

    private bool missileWarningActive = false;
    private bool phaseSplitTriggered = false;

    public BossPhase currentPhase = BossPhase.Phase1;

    private GameObject CurrentCoreDrone   => (drones != null && currentCoreIndex >= 0 && currentCoreIndex < 3) ? drones[currentCoreIndex] : null;
    private GameObject CurrentAttackerDrone => (drones != null && currentAttackerIndex >= 0 && currentAttackerIndex < 3) ? drones[currentAttackerIndex] : null;

    // ─────────────────────────────── Awake ────────────────────────────────────
    void Awake()
    {
        maxHealth = 1000f;
        waitDuration = 3f;  telegraphDuration = 1.5f;
        recoveryDuration = 1.5f;  stunDuration = 5f;
        damageReduction = 0.1f;   critMultiplier = 2.0f;
        chargeSpeed = 20f;  chargeDuration = 0.3f;  chargeDamage = 25f;
        chargeLength = 8f;  chargeHalfWidth = 1f;
        sweepRadius = 4.5f; sweepHalfAngle = 90f;   sweepDamage = 20f;
        shockwaveMaxRadius = 8f;  shockwaveExpandTime = 1f;
        shockwaveRingThickness = 1.5f;  shockwaveDamage = 35f; shockwaveGroundY = 1.5f;
        missileCount = 3;   missileDamage = 15f;    missileDuration = 0.7f;

        currentPhase = BossPhase.Phase1;
        phaseSplitTriggered = false;

        chargeLength           *= attackScale;
        chargeHalfWidth        *= attackScale;
        sweepRadius            *= attackScale;
        shockwaveMaxRadius     *= attackScale;
        shockwaveRingThickness *= attackScale;
        shockwaveGroundY       *= attackScale;
    }

    // ─────────────────────────────── Start ────────────────────────────────────
    void Start()
    {
        currentHealth = maxHealth;
        useURP    = Shader.Find("Universal Render Pipeline/Lit") != null;
        playerRef = FindObjectOfType<PlayerController>();

        CreateDroneMaterials();
        CreateDrones();
        CreateWarningArea();
        CreateShockwaveRing();
        CreateTelegraphFlash();
        CreateSweepFan();
        SetInitialVisuals();

        attackCycleCoroutine = StartCoroutine(AttackCycle());
    }

    void CheckPhaseSplit()
    {
        if (currentPhase == BossPhase.Phase1 && !phaseSplitTriggered && currentHealth <= maxHealth * 0.5f)
        {
            phaseSplitTriggered = true;
            currentPhase = BossPhase.Transitioning;
            if (phaseSplitCoroutine != null) StopCoroutine(phaseSplitCoroutine);
            phaseSplitCoroutine = StartCoroutine(PhaseSplitRoutine());
        }
    }

    IEnumerator PhaseSplitRoutine()
    {
        if (attackCycleCoroutine != null)
        {
            StopCoroutine(attackCycleCoroutine);
            attackCycleCoroutine = null;
        }
        if (stunTimerCoroutine != null)
        {
            StopCoroutine(stunTimerCoroutine);
            stunTimerCoroutine = null;
        }

        warningArea.SetActive(false);
        shockwaveRingObj.SetActive(false);
        telegraphFlash.SetActive(false);
        sweepFanObj.SetActive(false);
        missileWarningActive = false;
        for (int i = activeMissileObjs.Count - 1; i >= 0; i--)
            if (activeMissileObjs[i] != null) Destroy(activeMissileObjs[i]);
        activeMissileObjs.Clear();

        currentState      = EnemyState.Normal;
        currentAttackType = -1;
        currentAttackerIndex = -1;

        if (playerRef != null) playerRef.isTransitionFrozen = true;

        yield return new WaitForSeconds(1f);

        drones[0].transform.localScale = Vector3.one * 1.5f;

        for (int i = 1; i < 3; i++)
        {
            drones[i].SetActive(true);
            drones[i].transform.position = droneBasePositions[i];
        }

        yield return new WaitForSeconds(1f);

        if (playerRef != null) playerRef.isTransitionFrozen = false;

        currentPhase = BossPhase.Phase2;
        currentCoreIndex = Random.Range(0, 3);

        for (int i = 0; i < 3; i++)
        {
            patrolStates[i].moveDirection = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;
            patrolStates[i].stateTimer    = Random.Range(1f, 2f);
            patrolStates[i].isMoving      = false;
        }

        ApplyVisibility();
        attackCycleCoroutine = StartCoroutine(AttackCycle());
        GameLogger.Log("[Boss] 血量降至 50%！Boss进入第二阶段！");
    }

    // ─────────────────────────────── Update ───────────────────────────────────
    void Update()
    {
        if (hitCooldown > 0f) hitCooldown -= Time.deltaTime;

        // ── Drone 核心视觉同步
        if (drones != null && droneCores != null)
        {
            for (int i = 0; i < 3; i++)
            {
                if (drones[i] == null || !drones[i].activeSelf) continue;
                bool isCore = (i == currentCoreIndex);
                var rend = drones[i].GetComponent<MeshRenderer>();
                if (rend != null)
                {
                    if (currentState == EnemyState.Stunned)
                    {
                        rend.material = transparentMat;
                    }
                    else if (isOrderVision && isCore)
                    {
                        rend.material = droneOrderMat;
                    }
                    else
                    {
                        rend.material = droneChaosMat;
                    }
                }
                var coreRend = droneCores[i].GetComponent<MeshRenderer>();
                if (coreRend != null)
                {
                    bool showCore = currentState != EnemyState.Normal && isOrderVision && isCore;
                    coreRend.enabled = showCore;
                }
            }
        }

        // ── Drone 独立巡逻（狼群走停） + 每帧 LookAt + Y 轴锁定
        if (drones != null && playerRef != null && patrolStates != null)
        {
            for (int i = 0; i < 3; i++)
            {
                if (drones[i] == null || !drones[i].activeSelf) continue;
                Vector3 lookTarget = new Vector3(
                    playerRef.transform.position.x,
                    drones[i].transform.position.y,
                    playerRef.transform.position.z);
                if ((lookTarget - drones[i].transform.position).sqrMagnitude > 0.01f)
                    drones[i].transform.LookAt(lookTarget);

                // 冲锋中的 drone 由攻击协程控制，仅锁定 Y，不执行巡逻
                bool isAttacker = (currentState == EnemyState.Telegraphing && i == currentAttackerIndex);
                if (isAttacker)
                {
                    Vector3 aPos = drones[i].transform.position;
                    drones[i].transform.position = new Vector3(aPos.x, orbitBaseY, aPos.z);
                    continue;
                }

                // 独立巡逻计时器倒计时
                patrolStates[i].stateTimer -= Time.deltaTime;
                if (patrolStates[i].stateTimer <= 0f)
                {
                    patrolStates[i].isMoving = !patrolStates[i].isMoving;
                    if (patrolStates[i].isMoving)
                    {
                        patrolStates[i].moveDirection = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;
                        patrolStates[i].stateTimer    = Random.Range(2f, 3f);
                    }
                    else
                    {
                        patrolStates[i].stateTimer = Random.Range(1f, 2f);
                    }
                }

                // 移动状态：沿 moveDirection 行进
                if (patrolStates[i].isMoving)
                {
                    Vector3 newPos = drones[i].transform.position + patrolStates[i].moveDirection * patrolSpeed * Time.deltaTime;
                    newPos.y = orbitBaseY;
                    drones[i].transform.position = newPos;

                    // 防逃逸距离检测：超出 12m 或小于 3m 触发 120° 折返
                    float distToPlayer = Vector3.Distance(drones[i].transform.position, playerRef.transform.position);
                    if (distToPlayer > patrolMaxDist || distToPlayer < patrolMinDist)
                    {
                        patrolStates[i].moveDirection = Quaternion.Euler(0f, 120f, 0f) * patrolStates[i].moveDirection;
                    }

                    // 同伴避让：与其他 drone 过近时也 120° 折返
                    for (int j = 0; j < 3; j++)
                    {
                        if (j == i || drones[j] == null) continue;
                        float distToOther = Vector3.Distance(drones[i].transform.position, drones[j].transform.position);
                        if (distToOther < 3f)
                        {
                            patrolStates[i].moveDirection = Quaternion.Euler(0f, 120f, 0f) * patrolStates[i].moveDirection;
                            break;
                        }
                    }
                }

                // Y 轴强制锁定
                {
                    Vector3 pos = drones[i].transform.position;
                    drones[i].transform.position = new Vector3(pos.x, orbitBaseY, pos.z);
                }
            }
        }

        // ── 飞弹可见性：严格跟随 isOrderVision
        for (int i = activeMissileObjs.Count - 1; i >= 0; i--)
        {
            if (activeMissileObjs[i] == null) { activeMissileObjs.RemoveAt(i); continue; }
            var r = activeMissileObjs[i].GetComponent<MeshRenderer>();
            if (r != null) r.enabled = isOrderVision;
        }

        // ── 蓄力黄点：严格跟随 isOrderVision
        if (telegraphFlash != null && telegraphFlash.activeSelf)
            telegraphFlash.SetActive(isOrderVision);

        // ── 震地波圆环：严格跟随 isOrderVision
        if (shockwaveRingObj != null && shockwaveRingObj.activeSelf)
        {
            var sr = shockwaveRingObj.GetComponent<MeshRenderer>();
            if (sr != null) sr.enabled = isOrderVision;
        }
    }

    // ==========================================================================
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
            mat.SetFloat("_Surface",    0);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic",   0f);
        }
        else
        {
            mat.SetFloat("_Mode",        0);
            mat.SetFloat("_Glossiness",  smoothness);
            mat.SetFloat("_Metallic",    0f);
            mat.renderQueue = 2000;
        }
    }

    Material CreateTransparentMaterial(Color color)
    {
        Material mat = CreateLitMaterial(color);
        if (useURP)
        {
            mat.SetFloat("_Surface",  1);
            mat.SetFloat("_Blend",    0);
            mat.SetFloat("_ZWrite",   0);
            mat.SetFloat("_AlphaClip",0);
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

    // ── 无光照不透明材质（飞弹专用）
    // Unlit/Color 不参与场景光照计算，无论场景多暗都保持原色绝对可见
    Material CreateUnlitOpaqueMaterial(Color color)
    {
        // 优先 Unlit/Color（Standard）或 URP Unlit
        Shader shader = Shader.Find("Unlit/Color")
                     ?? Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Standard");
        var mat = new Material(shader) { color = color };
        // URP Unlit 需要明确设置为不透明
        if (shader.name.Contains("Universal"))
            mat.SetFloat("_Surface", 0);
        return mat;
    }

    // ── 无光照半透明材质（震地波圆环专用）
    // Sprites/Default 是 Unity 内置无光照 alpha-blend shader，Standard 和 URP 均可用
    // 保证在任何光照条件下颜色准确、透明度正确，无需额外 Emission 关键字
    Material CreateUnlitTransparentMaterial(Color color)
    {
        Shader shader = Shader.Find("Sprites/Default")    // 首选：永远无光照+alpha混合
                     ?? Shader.Find("Unlit/Transparent") // 次选
                     ?? Shader.Find("Standard");         // 兜底
        var mat = new Material(shader) { color = color };
        // Sprites/Default 和 Unlit/Transparent 都需要一张白色贴图才能正确显示颜色
        if (mat.mainTexture == null)
            mat.mainTexture = Texture2D.whiteTexture;
        mat.renderQueue = 3001; // 确保渲染在不透明地面之上
        // 兜底 Standard shader：手动配置透明混合
        if (shader.name == "Standard")
        {
            mat.SetFloat("_Mode", 3);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(color.r, color.g, color.b) * 8f);
        }
        return mat;
    }

    #endregion

    // ==========================================================================
    #region 程序化 Mesh 构建与更新

    // ── 水平圆环 Mesh（顶点在 XZ 平面 Y=0，直接以"世界米"为单位）
    // segs 段数 = 64 → 近似误差 < 0.01%
    static Mesh BuildRingMesh(float innerR, float outerR, int segs = 64)
    {
        var verts = new Vector3[(segs + 1) * 2];
        var uvs   = new Vector2[(segs + 1) * 2];
        var tris  = new int[segs * 6];

        for (int i = 0; i <= segs; i++)
        {
            float a = (float)i / segs * Mathf.PI * 2f;
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            verts[i * 2]     = new Vector3(c * outerR, 0f, s * outerR);
            verts[i * 2 + 1] = new Vector3(c * innerR, 0f, s * innerR);
            uvs[i * 2]       = new Vector2((float)i / segs, 1f);
            uvs[i * 2 + 1]   = new Vector2((float)i / segs, 0f);
        }
        for (int i = 0; i < segs; i++)
        {
            int b = i * 2;
            tris[i * 6]     = b;     tris[i * 6 + 1] = b + 2; tris[i * 6 + 2] = b + 1;
            tris[i * 6 + 3] = b + 1; tris[i * 6 + 4] = b + 2; tris[i * 6 + 5] = b + 3;
        }

        var m = new Mesh { name = "Ring" };
        m.vertices  = verts;
        m.uv        = uvs;
        m.triangles = tris;
        m.RecalculateNormals();
        return m;
    }

    // 原地更新圆环顶点（保持 segs 不变，仅改内外半径）。
    // GameObject.transform.localScale 始终保持 Vector3.one，
    // 彻底消除 Cylinder scale 换算误差。
    static void UpdateRingMesh(Mesh m, float innerR, float outerR)
    {
        var verts = m.vertices;
        int segs  = verts.Length / 2 - 1;
        for (int i = 0; i <= segs; i++)
        {
            float a = (float)i / segs * Mathf.PI * 2f;
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            verts[i * 2]     = new Vector3(c * outerR, 0f, s * outerR);
            verts[i * 2 + 1] = new Vector3(c * innerR, 0f, s * innerR);
        }
        m.vertices = verts;
        m.RecalculateBounds();
    }

    // ── 水平扇形 Mesh（面向 +Z 轴展开，halfAngleDeg = sweepHalfAngle）
    // Mesh 顶点半径 = sweepRadius → 与判定判定半径严格相同，误差 = 0
    static Mesh BuildFanMesh(float halfAngleDeg, float radius, int segs = 48)
    {
        var verts = new Vector3[segs + 2]; // 中心点 + 弧线点
        var tris  = new int[segs * 3];

        verts[0] = Vector3.zero;
        float halfRad = halfAngleDeg * Mathf.Deg2Rad;
        for (int i = 0; i <= segs; i++)
        {
            float t = (float)i / segs;
            float a = Mathf.Lerp(-halfRad, halfRad, t);
            // Unity +Z = forward，扇形朝 Boss 前方展开
            verts[i + 1] = new Vector3(Mathf.Sin(a) * radius, 0f, Mathf.Cos(a) * radius);
        }
        for (int i = 0; i < segs; i++)
        {
            tris[i * 3]     = 0;
            tris[i * 3 + 1] = i + 1;
            tris[i * 3 + 2] = i + 2;
        }

        var m = new Mesh { name = "Fan" };
        m.vertices  = verts;
        m.triangles = tris;
        m.RecalculateNormals();
        return m;
    }

    #endregion

    // ==========================================================================
    #region 视觉层生成

    void CreateDroneMaterials()
    {
        droneChaosMat = CreateLitMaterial(new Color(0.5f, 0.05f, 0.05f));
        SetMaterialOpaque(droneChaosMat, 0f);

        droneOrderMat = CreateTransparentMaterial(new Color(0.85f, 0.15f, 0.15f, 0.3f));
        SetMaterialOpaque(droneOrderMat, 0.1f);

        droneCoreMat = CreateLitMaterial(Color.yellow);
        droneCoreMat.EnableKeyword("_EMISSION");
        droneCoreMat.SetColor("_EmissionColor", new Color(1f, 0.9f, 0f) * 8f);
        SetMaterialOpaque(droneCoreMat, 0.9f);

        transparentMat = CreateTransparentMaterial(new Color(0.5f, 0.5f, 0.5f, 0.35f));
    }

    void CreateDrones()
    {
        drones    = new GameObject[3];
        droneCores = new GameObject[3];

        Vector3 pPos = playerRef.transform.position;
        droneBasePositions = new Vector3[3];
        droneBasePositions[0] = pPos + playerRef.transform.forward * 10f;
        droneBasePositions[1] = pPos + (-playerRef.transform.forward * 0.5f - playerRef.transform.right * 0.866f) * 10f;
        droneBasePositions[2] = pPos + (-playerRef.transform.forward * 0.5f + playerRef.transform.right * 0.866f) * 10f;
        for (int j = 0; j < 3; j++) droneBasePositions[j].y = orbitBaseY;

        for (int i = 0; i < 3; i++)
        {
            Vector3 pos = droneBasePositions[i];

            GameObject drone = GameObject.CreatePrimitive(PrimitiveType.Cube);
            drone.name = "Drone_" + i;
            drone.transform.position = pos;
            drone.transform.localScale = Vector3.one * 1.5f;
            drone.GetComponent<Renderer>().material = droneChaosMat;
            drones[i] = drone;

            CreateDroneCore(i);
        }

        // P1：只激活 Drone_0 作为巨物压迫
        drones[0].transform.localScale = Vector3.one * 3f;
        for (int i = 1; i < 3; i++) drones[i].SetActive(false);

        currentCoreIndex = 0;

        patrolStates = new DronePatrol[3];
        for (int i = 0; i < 3; i++)
        {
            patrolStates[i].moveDirection = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;
            patrolStates[i].stateTimer    = Random.Range(1f, 2f);
            patrolStates[i].isMoving      = false;
        }
    }

    void CreateDroneCore(int index)
    {
        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "DroneCore_" + index;
        core.transform.SetParent(drones[index].transform);
        core.transform.localPosition = Vector3.zero;
        core.transform.localScale = Vector3.one * 0.5f;
        core.GetComponent<Renderer>().material = droneCoreMat;
        Destroy(core.GetComponent<Collider>());
        core.SetActive(false);
        droneCores[index] = core;
    }

    // 招式 0 矩形预警：Plane 原生 10×10 units
    //   localScale = (halfWidth*2/10, 1, length/10)
    //   → 世界尺寸 2m × 8m，与 OBB 判定参数 chargeHalfWidth / chargeLength 1:1
    void CreateWarningArea()
    {
        warningArea = GameObject.CreatePrimitive(PrimitiveType.Plane);
        warningArea.name = "warningArea";
        warningArea.transform.SetParent(transform);
        warningArea.transform.localPosition = new Vector3(0f, 0.05f, chargeLength * 0.5f);
        warningArea.transform.localScale    = new Vector3(chargeHalfWidth * 2f / 10f, 1f, chargeLength / 10f);
        warningMat = CreateTransparentMaterial(new Color(1f, 0.9f, 0f, 0.45f));
        warningMat.EnableKeyword("_EMISSION");
        warningMat.SetColor("_EmissionColor", new Color(1f, 0.9f, 0f) * 3f);
        warningArea.GetComponent<Renderer>().material = warningMat;
        Destroy(warningArea.GetComponent<Collider>());
    }

    // 招式 2 空心圆环：程序化 Mesh，localScale 永远 = (1,1,1)
    // 顶点以世界米直接表示，彻底消除任何 scale 换算
    // Y = 0.2f：高于地面 0.2m，防止与地面网格深度冲突（Z-fighting）
    void CreateShockwaveRing()
    {
        ringMesh = BuildRingMesh(0f, 0.01f);

        shockwaveRingObj = new GameObject("shockwaveRing");
        shockwaveRingObj.transform.SetParent(transform);
        shockwaveRingObj.transform.localPosition = new Vector3(0f, 0.2f, 0f);  // Y=0.2f 防穿模
        shockwaveRingObj.transform.localRotation = Quaternion.identity;
        shockwaveRingObj.transform.localScale    = Vector3.one;  // 永不改变

        shockwaveRingObj.AddComponent<MeshFilter>().mesh = ringMesh;
        // 使用无光照透明材质：不参与场景光照，任何亮度条件下均可见
        shockwaveMat = CreateUnlitTransparentMaterial(new Color(1f, 0.9f, 0f, 0.75f));
        shockwaveRingObj.AddComponent<MeshRenderer>().material = shockwaveMat;
    }

    void CreateTelegraphFlash()
    {
        telegraphFlash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        telegraphFlash.name = "telegraphFlash";
        telegraphFlash.transform.SetParent(transform);
        telegraphFlash.transform.localPosition = new Vector3(0f, 1f, 0f);
        telegraphFlash.transform.localScale    = Vector3.one * 0.5f * attackScale;
        flashMat = CreateLitMaterial(Color.yellow);
        flashMat.EnableKeyword("_EMISSION");
        flashMat.SetColor("_EmissionColor", new Color(1f, 0.9f, 0f) * 10f);
        telegraphFlash.GetComponent<Renderer>().material = flashMat;
        Destroy(telegraphFlash.GetComponent<Collider>());
        telegraphFlash.SetActive(false);
    }

    // 招式 1 程序化扇形体：Mesh 顶点半径 = sweepRadius，半角 = sweepHalfAngle
    // 与判定数学完全共享同一常量，视觉/判定绝对同步
    void CreateSweepFan()
    {
        fanMesh = BuildFanMesh(sweepHalfAngle, sweepRadius);

        sweepFanObj = new GameObject("SweepFan");
        sweepFanObj.transform.SetParent(transform);
        sweepFanObj.transform.localPosition = Vector3.zero;
        sweepFanObj.transform.localRotation = Quaternion.identity;

        sweepFanObj.AddComponent<MeshFilter>().mesh = fanMesh;
        sweepFanMat = CreateTransparentMaterial(new Color(1f, 0.05f, 0f, 0.85f));
        sweepFanMat.EnableKeyword("_EMISSION");
        sweepFanMat.SetColor("_EmissionColor", Color.red * 6f);
        sweepFanObj.AddComponent<MeshRenderer>().material = sweepFanMat;
        sweepFanObj.SetActive(false);
    }

    void SetInitialVisuals()
    {
        if (drones != null)
        {
            for (int i = 0; i < drones.Length; i++)
            {
                if (drones[i] == null) continue;
                if (currentPhase == BossPhase.Phase1 && i != 0)
                    drones[i].SetActive(false);
                else
                    drones[i].SetActive(true);
            }
        }
        warningArea.SetActive(false);
        shockwaveRingObj.SetActive(false);
        sweepFanObj.SetActive(false);
    }

    #endregion

    // ==========================================================================
    #region 视野切换（破核权限驱动显隐）

    public void SwitchToOrder(bool canPierce)
    {
        isOrderVision = canPierce;
        ApplyVisibility();
    }

    void ApplyVisibility()
    {
        if (drones == null) return;

        switch (currentState)
        {
            case EnemyState.Stunned:
                for (int i = 0; i < 3; i++)
                {
                    bool isCore = (i == currentCoreIndex);
                    if (drones[i] != null)
                    {
                        var r = drones[i].GetComponent<MeshRenderer>();
                        if (r != null) r.material = (isOrderVision && isCore) ? transparentMat : droneChaosMat;
                    }
                    if (droneCores[i] != null) droneCores[i].SetActive(isOrderVision && isCore);
                }
                warningArea.SetActive(false);
                shockwaveRingObj.SetActive(false);
                break;

            case EnemyState.Telegraphing:
                for (int i = 0; i < 3; i++)
                {
                    if (drones[i] != null)
                    {
                        bool isCore = (i == currentCoreIndex);
                        var r = drones[i].GetComponent<MeshRenderer>();
                        if (r != null) r.material = (isOrderVision && isCore) ? droneOrderMat : droneChaosMat;
                    }
                    if (droneCores[i] != null)
                        droneCores[i].SetActive(isOrderVision && i == currentCoreIndex);
                }
                if (currentAttackType == 2)
                {
                    warningArea.SetActive(false);
                }
                else if (currentAttackType == 3)
                {
                    warningArea.SetActive(isOrderVision && missileWarningActive);
                    shockwaveRingObj.SetActive(false);
                }
                else
                {
                    warningArea.SetActive(isOrderVision);
                    shockwaveRingObj.SetActive(false);
                }
                break;

            case EnemyState.Recovery:
                for (int i = 0; i < 3; i++)
                {
                    if (drones[i] != null)
                    {
                        bool isCore = (i == currentCoreIndex);
                        var r = drones[i].GetComponent<MeshRenderer>();
                        if (r != null) r.material = (isOrderVision && isCore) ? droneOrderMat : droneChaosMat;
                    }
                    if (droneCores[i] != null)
                        droneCores[i].SetActive(isOrderVision && i == currentCoreIndex);
                }
                warningArea.SetActive(false);
                shockwaveRingObj.SetActive(false);
                break;

            default: // Normal
                for (int i = 0; i < 3; i++)
                {
                    if (drones[i] != null)
                    {
                        var r = drones[i].GetComponent<MeshRenderer>();
                        if (r != null) r.material = droneChaosMat;
                    }
                    if (droneCores[i] != null) droneCores[i].SetActive(false);
                }
                warningArea.SetActive(false);
                shockwaveRingObj.SetActive(false);
                break;
        }
    }

    #endregion

    // ==========================================================================
    #region 战斗循环主体

    IEnumerator AttackCycle()
    {
        while (true)
        {
            currentState      = EnemyState.Normal;
            currentAttackType = -1;
            currentAttackerIndex = -1;
            ApplyVisibility();
            yield return new WaitForSeconds(waitDuration);

            currentAttackerIndex = (currentPhase == BossPhase.Phase1) ? 0 : Random.Range(0, 3);
            activeAttacker = (drones != null && currentAttackerIndex >= 0 && currentAttackerIndex < 3)
                ? drones[currentAttackerIndex].transform : transform;

            currentAttackType = Random.Range(0, 4);
            if (currentAttackType == 1 && playerRef != null)
            {
                float distToPlayer = Vector3.Distance(activeAttacker.position, playerRef.transform.position);
                if (distToPlayer > sweepRadius + 2f)
                    currentAttackType = Random.Range(0, 2) == 0 ? 0 : 2;
            }

            currentState = EnemyState.Telegraphing;

            if (currentPhase != BossPhase.Phase1)
            {
                int newCore;
                do { newCore = Random.Range(0, 3); } while (newCore == currentCoreIndex && Random.value > 0.3f);
                currentCoreIndex = newCore;
            }

            switch (currentAttackType)
            {
                case 0: yield return StartCoroutine(Attack0_Dash());      break;
                case 1: yield return StartCoroutine(Attack1_Sweep());     break;
                case 2: yield return StartCoroutine(Attack2_Shockwave()); break;
                case 3: yield return StartCoroutine(Attack3_Missiles());  break;
            }

            currentState = EnemyState.Recovery;
            ApplyVisibility();
            yield return new WaitForSeconds(recoveryDuration);
        }
    }

    #endregion

    // ==========================================================================
    #region 三体幻影：核心轮转

    // ─────────────────────────────────────────────────────────────────────────
    // 真身轮转已移至 AttackCycle() 的前摇阶段：每次发动攻击时随机更换真身。
    // ─────────────────────────────────────────────────────────────────────────

    #endregion

    // ==========================================================================
    #region 共用：蓄力黄点渐隐

    IEnumerator TelegraphFade(float duration)
    {
        Transform flashTarget = activeAttacker != null ? activeAttacker : transform;
        telegraphFlash.transform.position = flashTarget.position + Vector3.up * 1f;
        telegraphFlash.transform.localScale    = Vector3.one * 0.5f;
        flashMat.color = new Color(1f, 0.9f, 0f, 1f);
        flashMat.SetColor("_EmissionColor", new Color(1f, 0.9f, 0f) * 10f);
        telegraphFlash.SetActive(isOrderVision);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / duration);
            Color c = flashMat.color;
            c.a = Mathf.Lerp(1f, 0f, p);
            flashMat.color = c;
            telegraphFlash.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 0.15f, p);
            telegraphFlash.SetActive(isOrderVision);
            yield return null;
        }
        telegraphFlash.SetActive(false);
    }

    #endregion

    // ==========================================================================
    #region 招式 0：直线冲锋（精确 OBB 矩形判定）
    // ──────────────────────────────────────────────────────────────────────────
    // 视觉：warningArea Plane，世界尺寸 = 2m（宽）× 8m（长），中心在 Boss 前方 4m
    //
    // 判定（向量分解 / OBB）：
    //   以冲锋起点为原点，dashDirH 为前轴（XZ 水平化）
    //   fwdProj  = Dot(toPlayer_xz, dashDirH)         ∈ [0, chargeLength]
    //   perpProj = |Cross(dashDirH, toPlayer_xz).y|   ≤ chargeHalfWidth
    //
    // 视觉 ↔ 判定同步证明：
    //   Plane.scale.z * 10 = chargeLength = 8m   ✓
    //   Plane.scale.x * 10 = chargeHalfWidth*2 = 2m → 半宽 1m = chargeHalfWidth ✓
    //   Plane 中心 localPos.z = chargeLength/2 = 4m → 覆盖 [0, 8m] 前向 ✓
    // ──────────────────────────────────────────────────────────────────────────

    IEnumerator Attack0_Dash()
    {
        Vector3 attackerPos = activeAttacker.position;
        FaceDrone(activeAttacker);

        warningArea.transform.position = attackerPos + activeAttacker.forward * chargeLength * 0.5f + Vector3.up * 0.05f;
        warningArea.transform.localScale    = new Vector3(chargeHalfWidth * 2f / 10f, 1f, chargeLength / 10f);
        warningArea.transform.rotation = activeAttacker.rotation;
        warningMat.color = new Color(1f, 0.9f, 0f, 0.45f);
        warningMat.SetColor("_EmissionColor", new Color(1f, 0.9f, 0f) * 3f);
        ApplyVisibility();
        // 破核权限隔离：CanPierceCore（Shift 或子弹时间）才允许听到预警
        if (playerRef != null && playerRef.CanPierceCore) AudioManager.PlayWarning();

        // 预警动画：脉冲闪烁
        float preTime = telegraphDuration - 0.3f;
        float elapsed = 0f;
        while (elapsed < preTime)
        {
            elapsed += Time.deltaTime;
            float pulse = Mathf.Abs(Mathf.Sin(elapsed * Mathf.PI * 3f));
            Color c = warningMat.color;
            c.a = Mathf.Lerp(0.2f, 0.65f, pulse);
            warningMat.color = c;
            yield return null;
        }

        yield return StartCoroutine(TelegraphFade(0.3f));
        warningArea.SetActive(false);

        GameLogger.Log("[Boss] 招式0：直线冲锋");

        // 严格水平化：dashDir 强制 .y = 0 并归一化，确保冲锋全程在 XZ 平面运行
        // 起点 Y 锁定到 orbitBaseY（drone 贴地高度），消除任何随机浮空残留
        Vector3 dashStart = new Vector3(activeAttacker.position.x, orbitBaseY, activeAttacker.position.z);
        Vector3 dashDir   = activeAttacker.forward;
        dashDir.y = 0f;
        dashDir.Normalize();
        Vector3 dashEnd   = dashStart + dashDir * chargeLength;
        Vector3 dashDirH  = dashDir;
        elapsed = 0f;
        bool hit = false;

        // 同时把当前 drone 的 Y 也锁回 orbitBaseY，防止前一帧的浮空污染起跳点
        activeAttacker.position = dashStart;

        while (elapsed < chargeDuration)
        {
            elapsed += Time.deltaTime;
            activeAttacker.position = Vector3.Lerp(dashStart, dashEnd,
                Mathf.Clamp01(elapsed / chargeDuration));

            // ── 精确 OBB 判定
            if (!hit && playerRef != null)
            {
                Vector3 toPlayer = playerRef.transform.position - dashStart;
                toPlayer.y = 0f;

                float fwdProj  = Vector3.Dot(toPlayer, dashDirH);
                // Cross(dashDirH, toPlayer).y 的绝对值 = 两向量在 XZ 平面上的垂直距离
                float perpProj = Mathf.Abs(Vector3.Cross(dashDirH, toPlayer).y);

                if (fwdProj >= 0f && fwdProj <= chargeLength && perpProj <= chargeHalfWidth)
                {
                    playerRef.ReceiveAttack(chargeDamage);
                    hit = true;
                }
            }
            yield return null;
        }

        activeAttacker.position = dashEnd;
    }

    #endregion

    // ==========================================================================
    #region 招式 1：扇形横扫（程序化扇形砸落 + 精确 180° 半圆判定）
    // ──────────────────────────────────────────────────────────────────────────
    // 视觉（预警）：sweepFanObj 黄色贴地，仅 Shift 可见
    // 视觉（攻击）：sweepFanObj 红色从 5m 高空狠狠砸落，停留 0.3s 后消散
    //
    // 判定：
    //   dist_xz < sweepRadius   AND   angle_to_forward < sweepHalfAngle
    //
    // 视觉 ↔ 判定同步证明：
    //   fanMesh 在 BuildFanMesh(sweepHalfAngle, sweepRadius) 时构建
    //   Mesh 顶点半径 = sweepRadius，Mesh 弧度 = sweepHalfAngle * 2
    //   判定使用同一个 sweepRadius / sweepHalfAngle 常量 → 误差 = 0 ✓
    // ──────────────────────────────────────────────────────────────────────────

    IEnumerator Attack1_Sweep()
    {
        Vector3 attackerPos = activeAttacker.position;
        FaceDrone(activeAttacker);

        // ── 预警：扇形贴地，黄色脉冲（仅 Shift 可见）
        sweepFanObj.transform.position = attackerPos + Vector3.up * 0.06f;
        sweepFanObj.transform.rotation = activeAttacker.rotation;
        sweepFanMat.color = new Color(1f, 0.9f, 0f, 0.25f);
        sweepFanMat.SetColor("_EmissionColor", new Color(1f, 0.9f, 0f) * 2.5f);
        sweepFanObj.SetActive(isOrderVision);
        // 破核权限隔离：CanPierceCore（Shift 或子弹时间）才允许听到预警
        if (playerRef != null && playerRef.CanPierceCore) AudioManager.PlayWarning();

        float preTime = telegraphDuration - 0.3f;
        float elapsed = 0f;
        while (elapsed < preTime)
        {
            elapsed += Time.deltaTime;
            sweepFanObj.SetActive(isOrderVision);  // 跟随 Shift 实时同步
            float pulse = Mathf.Abs(Mathf.Sin(elapsed * Mathf.PI * 2.5f));
            Color c = sweepFanMat.color;
            c.a = Mathf.Lerp(0.1f, 0.45f, pulse);
            sweepFanMat.color = c;
            yield return null;
        }

        yield return StartCoroutine(TelegraphFade(0.3f));
        sweepFanObj.SetActive(false);

        GameLogger.Log("[Boss] 招式1：扇形横扫");

        // ── 攻击：红色扇形从 5m 高空砸落（EaseIn 加速，模拟重力）
        sweepFanMat.color = new Color(1f, 0.05f, 0f, 0.95f);
        sweepFanMat.SetColor("_EmissionColor", Color.red * 9f);
        Vector3 slamStart = attackerPos + Vector3.up * 5f;
        Vector3 slamEnd   = attackerPos + Vector3.up * 0.06f;
        sweepFanObj.transform.position = slamStart;
        sweepFanObj.transform.rotation = activeAttacker.rotation;
        sweepFanObj.SetActive(true);

        elapsed = 0f;
        const float slamTime = 0.1f;
        while (elapsed < slamTime)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / slamTime);
            sweepFanObj.transform.position = Vector3.Lerp(slamStart, slamEnd, p * p);
            yield return null;
        }
        sweepFanObj.transform.position = slamEnd;

        // ── 精确扇形判定（砸地瞬间）
        if (playerRef != null)
        {
            Vector3 toPlayer = playerRef.transform.position - attackerPos;
            toPlayer.y = 0f;
            if (toPlayer.magnitude < sweepRadius
                && Vector3.Angle(activeAttacker.forward, toPlayer) < sweepHalfAngle)
            {
                playerRef.ReceiveAttack(sweepDamage);
            }
        }

        // 停留 0.3s（玩家清晰看到"我在红色扇面里"）
        yield return new WaitForSeconds(0.3f);

        // 消散
        elapsed = 0f;
        const float fadeTime = 0.18f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            Color c = sweepFanMat.color;
            c.a = Mathf.Lerp(0.95f, 0f, elapsed / fadeTime);
            sweepFanMat.color = c;
            yield return null;
        }
        sweepFanObj.SetActive(false);
    }

    #endregion

    // ==========================================================================
    #region 招式 2：震地冲击波（动态空心圆环扩散，精确空心环伤害判定）
    // ──────────────────────────────────────────────────────────────────────────
    // 物理模型：
    //   midRadius(t) = Lerp(0, shockwaveMaxRadius, t),  t ∈ [0, shockwaveExpandTime]
    //   innerRadius  = max(0, midRadius - halfThick)
    //   outerRadius  = midRadius + halfThick
    //
    // 视觉（程序化）：
    //   每帧调用 UpdateRingMesh(ringMesh, innerR, outerR)，直接写顶点
    //   GameObject.localScale = (1,1,1) 永不变化 → 无 scale 换算误差
    //
    // 判定：
    //   dist2D ∈ [innerRadius, outerRadius]  AND  player.y < shockwaveGroundY
    //
    // 视觉 ↔ 判定同步证明：
    //   Mesh 顶点 (c*outerR, 0, s*outerR) / (c*innerR, 0, s*innerR) 定义的圆环边界
    //   与判定中使用的 innerR / outerR 是同一个变量 → 误差 = 0 ✓
    //
    // 博弈设计：
    //   圆环外（未到达）：安全
    //   圆环内（已扫过）：安全
    //   圆环 1.5m 实体宽度：危险 → 玩家需跳跃（Space）离开地面才能逃脱
    // ──────────────────────────────────────────────────────────────────────────

    IEnumerator Attack2_Shockwave()
    {
        Vector3 attackerPos = activeAttacker.position;

        // ── 蓄力：圆环从中心向外脉冲预警（Shift 可见）
        shockwaveRingObj.transform.position = attackerPos + Vector3.up * 0.2f;
        UpdateRingMesh(ringMesh, 0f, 0.5f);
        shockwaveMat.color = new Color(1f, 0.9f, 0f, 0.55f);
        shockwaveRingObj.SetActive(true);
        // 破核权限隔离：CanPierceCore（Shift 或子弹时间）才允许听到预警
        if (playerRef != null && playerRef.CanPierceCore) AudioManager.PlayWarning();

        float chargeT = telegraphDuration - 0.3f;
        float elapsed = 0f;
        while (elapsed < chargeT)
        {
            elapsed += Time.deltaTime;
            float p     = Mathf.Clamp01(elapsed / chargeT);
            float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * Mathf.PI * 5f);
            UpdateRingMesh(ringMesh, 0f, Mathf.Lerp(0.3f * attackScale, 2.2f * attackScale, p));
            Color c = shockwaveMat.color;
            c.a = Mathf.Lerp(0.3f, 0.9f, pulse);
            shockwaveMat.color = c;
            yield return null;
        }

        yield return StartCoroutine(TelegraphFade(0.3f));

        GameLogger.Log("[Boss] 招式2：震地冲击波");

        yield return StartCoroutine(ExpandShockwave());

        shockwaveRingObj.SetActive(false);
        shockwaveMat.color = new Color(1f, 0.9f, 0f, 0.7f);
    }

    IEnumerator ExpandShockwave()
    {
        float halfThick = shockwaveRingThickness * 0.5f;
        float elapsed   = 0f;
        bool  playerHit = false;

        Vector3 origin = (activeAttacker != null)
            ? new Vector3(activeAttacker.position.x, 0f, activeAttacker.position.z)
            : new Vector3(transform.position.x, 0f, transform.position.z);

        while (elapsed < shockwaveExpandTime)
        {
            elapsed += Time.deltaTime;
            float t     = Mathf.Clamp01(elapsed / shockwaveExpandTime);
            float midR  = Mathf.Lerp(0f, shockwaveMaxRadius, t);
            float innerR = Mathf.Max(0f, midR - halfThick);
            float outerR = midR + halfThick;

            // 同一对 innerR/outerR 同时驱动视觉和判定
            UpdateRingMesh(ringMesh, innerR, outerR);

            Color c = shockwaveMat.color;
            c.a = Mathf.Lerp(0.9f, 0.06f, t);
            shockwaveMat.color = c;

            // ── 精确空心环判定
            // 玩家须同时满足：被环实体覆盖（dist2D 在 [innerR, outerR]）AND 脚在地面
            if (!playerHit && playerRef != null)
            {
                Vector3 pPos   = playerRef.transform.position;
                Vector3 pFlat  = new Vector3(pPos.x, 0f, pPos.z);
                float   dist2D = Vector3.Distance(origin, pFlat);

                bool inRing   = dist2D >= innerR && dist2D <= outerR;
                bool onGround = pPos.y < shockwaveGroundY;

                if (inRing && onGround)
                {
                    playerRef.ReceiveAttack(shockwaveDamage);
                    playerHit = true;
                }
            }

            yield return null;
        }
    }

    #endregion

    // ==========================================================================
    #region 招式 3：瞬间锁定贝塞尔飞弹
    // ──────────────────────────────────────────────────────────────────────────
    // 博弈设计（"看前摇瞬闪"）：
    //   蓄力阶段（telegraphDuration - 0.3s）：追踪预警圈实时跟随玩家脚下
    //   锁定时刻（-0.3s）：黄点渐隐 → 玩家判断此刻位置已被冻结
    //   发射：3 枚飞弹走不同的二次贝塞尔弧线，同时飞向锁定点
    //
    // 贝塞尔控制点设计（P0 = 发射点，P1 = 控制点，P2 = 锁定点）：
    //   飞弹 0：P1 大幅向上（高弧抛物线，从天而降感）
    //   飞弹 1：P1 向右侧偏（右绕弧线，包抄感）
    //   飞弹 2：P1 向左侧偏（左绕弧线，夹击感）
    // ──────────────────────────────────────────────────────────────────────────

    IEnumerator Attack3_Missiles()
    {
        Vector3 attackerPos = activeAttacker.position;
        FaceDrone(activeAttacker);
        missileWarningActive = true;

        warningArea.transform.position = attackerPos + activeAttacker.forward * 3f + Vector3.up * 0.05f;
        warningArea.transform.localScale    = new Vector3(0.15f * attackScale, 1f, 0.15f * attackScale);
        warningArea.transform.rotation = Quaternion.identity;
        warningMat.color = new Color(1f, 0.9f, 0f, 0.5f);
        warningMat.SetColor("_EmissionColor", new Color(1f, 0.9f, 0f) * 3f);
        ApplyVisibility();
        // 破核权限隔离：CanPierceCore（Shift 或子弹时间）才允许听到预警
        if (playerRef != null && playerRef.CanPierceCore) AudioManager.PlayWarning();

        float   elapsed   = 0f;
        float   lockTime  = telegraphDuration - 0.3f;
        Vector3 lockedPos = Vector3.zero;

        // 追踪预警圈跟随玩家
        while (elapsed < lockTime)
        {
            elapsed += Time.deltaTime;

            if (playerRef != null)
            {
                Vector3 p = playerRef.transform.position;
                warningArea.transform.position = new Vector3(p.x, 0.05f, p.z);
            }

            float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * Mathf.PI * 4f);
            Color c = warningMat.color;
            c.a = Mathf.Lerp(0.2f, 0.7f, pulse);
            warningMat.color = c;

            yield return null;
        }

        // ── 瞬间锁定（玩家坐标冻结）
        lockedPos = playerRef != null
            ? playerRef.transform.position
            : transform.position + transform.forward * 5f;

        // 隐藏追踪圈（锁定已完成，不再跟随）
        missileWarningActive = false;
        warningArea.SetActive(false);

        // 黄点渐隐：视觉告知"目标已捕获，马上发射"
        yield return StartCoroutine(TelegraphFade(0.3f));

        GameLogger.Log("[Boss] 招式3：贝塞尔飞弹三连");

        Vector3 fireOrigin = activeAttacker.position + Vector3.up * 1.2f;
        Vector3 toTarget   = lockedPos - fireOrigin;
        float   span       = toTarget.magnitude;
        Vector3 midBase    = (fireOrigin + lockedPos) * 0.5f;
        Vector3 rightPerp  = Vector3.Cross(Vector3.up, toTarget.normalized).normalized;

        Vector3[] ctrlPts =
        {
            midBase + Vector3.up  * (span * 0.55f),                       // 高弧
            midBase + rightPerp   * (span * 0.40f) + Vector3.up * 2f,    // 右绕
            midBase - rightPerp   * (span * 0.40f) + Vector3.up * 2f,    // 左绕
        };

        for (int i = 0; i < missileCount; i++)
        {
            Vector3 ctrl = ctrlPts[i % ctrlPts.Length];
            StartCoroutine(LaunchBezierMissile(fireOrigin, ctrl, lockedPos,
                                               missileDuration, missileDamage));
            yield return new WaitForSeconds(0.12f);
        }
    }

    // 单枚贝塞尔弧线飞弹协程
    // 二次贝塞尔：Pos(t) = (1-t)²·P0 + 2(1-t)t·P1 + t²·P2
    IEnumerator LaunchBezierMissile(Vector3 p0, Vector3 p1, Vector3 p2,
                                    float duration, float dmg)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = "BezierMissile";
        obj.transform.position   = p0;
        obj.transform.localScale = new Vector3(0.2f, 0.2f, 0.5f);
        Destroy(obj.GetComponent<Collider>());

        var rend = obj.GetComponent<MeshRenderer>();
        // 无光照不透明红色材质：无论场景明暗均保持鲜红可见
        var mat  = CreateUnlitOpaqueMaterial(Color.red);
        rend.material = mat;
        rend.enabled  = isOrderVision;  // 严格跟随 Shift 视野

        activeMissileObjs.Add(obj);

        float   elapsed = 0f;
        Vector3 prevPos = p0;

        while (elapsed < duration)
        {
            if (obj == null) yield break;  // 已被 TriggerStun/Die 销毁

            elapsed += Time.deltaTime;
            float t    = Mathf.Clamp01(elapsed / duration);
            float invT = 1f - t;

            // 二次贝塞尔插值
            Vector3 cur = invT * invT * p0 + 2f * invT * t * p1 + t * t * p2;

            // ── OverlapSphere 大体积判定（半径 1.5m）
            // 巨大判定球配合玩家无敌帧（I-Frames），飞弹"擦过"玩家时
            // ReceiveAttack 会被无敌帧拦截并触发"完美闪避"子弹时间
            if (playerRef != null)
            {
                var cols = Physics.OverlapSphere(cur, 1.5f * attackScale);
                foreach (var col in cols)
                {
                    int dIdx = GetDroneIndex(col);
                    if (dIdx >= 0) continue;
                    var pc = col.GetComponentInParent<PlayerController>();
                    if (pc != null)
                    {
                        pc.ReceiveAttack(dmg);
                        activeMissileObjs.Remove(obj);
                        Destroy(obj);
                        yield break;
                    }
                }
            }

            Vector3 delta = cur - prevPos;
            obj.transform.position = cur;
            if (delta.sqrMagnitude > 0.0001f)
                obj.transform.forward = delta.normalized;

            prevPos = cur;
            yield return null;
        }

        if (obj != null)
        {
            activeMissileObjs.Remove(obj);
            Destroy(obj);
        }
    }

    #endregion

    // ==========================================================================
    #region 工具

    void FaceDrone(Transform drone)
    {
        if (playerRef == null || drone == null) return;
        Vector3 dir = playerRef.transform.position - drone.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
            drone.rotation = Quaternion.LookRotation(dir);
    }

    public int GetDroneIndex(Collider col)
    {
        if (drones == null) return -1;
        for (int i = 0; i < 3; i++)
        {
            if (drones[i] != null && (col.gameObject == drones[i] || col.transform.IsChildOf(drones[i].transform)))
                return i;
        }
        return -1;
    }

    public bool IsCorrectDrone(int droneIndex, bool orderVision)
    {
        return droneIndex == currentCoreIndex && orderVision;
    }

    #endregion

    // ==========================================================================
    #region 伤害结算与破防

    public void TakeDamage(float damage, bool isPerfectCounter)
    {
        TakeDamage(damage, isPerfectCounter, -1);
    }

    public void TakeDamage(float damage, bool isPerfectCounter, int droneIndex)
    {
        if (currentHealth <= 0) return;
        if (hitCooldown > 0f) return;

        if (droneIndex >= 0 && droneIndex != currentCoreIndex)
        {
            GameLogger.Log("幻影免疫！这不是真身！");
            return;
        }

        hitCooldown = 0.1f;

        switch (currentState)
        {
            case EnemyState.Normal:
            case EnemyState.Telegraphing:
                float reducedDamage = damage * damageReduction;
                currentHealth -= reducedDamage;
                GameLogger.Log($"外壳防护！仅受 {reducedDamage:F1} 点刮痧伤害");
                break;

            case EnemyState.Recovery:
                if (isPerfectCounter)
                {
                    GameLogger.Log("防反破核！怪物进入瘫痪！");
                    AudioManager.PlayHitCore();
                    TriggerStun();
                    CheckPhaseSplit();
                    return;
                }
                currentHealth -= damage;
                GameLogger.Log($"抓后摇！受到 {damage:F1} 点伤害");
                break;

            case EnemyState.Stunned:
                float critDamage = damage * critMultiplier;
                currentHealth -= critDamage;
                GameLogger.Log($"核心暴击！受到 {critDamage:F1} 点伤害");
                break;
        }

        if (currentHealth <= 0) { currentHealth = 0; Die(); return; }
        CheckPhaseSplit();
    }

    void TriggerStun()
    {
        if (attackCycleCoroutine != null)
        {
            StopCoroutine(attackCycleCoroutine);
            attackCycleCoroutine = null;
        }

        warningArea.SetActive(false);
        shockwaveRingObj.SetActive(false);
        telegraphFlash.SetActive(false);
        sweepFanObj.SetActive(false);
        missileWarningActive = false;

        for (int i = activeMissileObjs.Count - 1; i >= 0; i--)
            if (activeMissileObjs[i] != null) Destroy(activeMissileObjs[i]);
        activeMissileObjs.Clear();

        currentState      = EnemyState.Stunned;
        currentAttackType = -1;
        currentAttackerIndex = -1;

        for (int i = 0; i < 3; i++)
        {
            if (drones[i] != null)
            {
                var r = drones[i].GetComponent<MeshRenderer>();
                if (r != null) r.material = transparentMat;
            }
            if (droneCores[i] != null) droneCores[i].SetActive(true);
        }

        GameLogger.Log("[Boss] 核心暴露！瘫痪 " + stunDuration + " 秒");
        stunTimerCoroutine = StartCoroutine(StunTimer());
    }

    IEnumerator StunTimer()
    {
        yield return new WaitForSeconds(stunDuration);

        currentState      = EnemyState.Normal;
        currentAttackType = -1;
        currentAttackerIndex = -1;

        for (int i = 0; i < 3; i++)
        {
            if (drones[i] != null)
            {
                var r = drones[i].GetComponent<MeshRenderer>();
                if (r != null) r.material = droneChaosMat;
            }
            if (droneCores[i] != null) droneCores[i].SetActive(false);
        }

        warningArea.SetActive(false);
        shockwaveRingObj.SetActive(false);

        attackCycleCoroutine = StartCoroutine(AttackCycle());
    }

    void Die()
    {
        if (attackCycleCoroutine != null) StopCoroutine(attackCycleCoroutine);
        if (stunTimerCoroutine   != null) StopCoroutine(stunTimerCoroutine);
        if (phaseSplitCoroutine  != null) StopCoroutine(phaseSplitCoroutine);

        for (int i = activeMissileObjs.Count - 1; i >= 0; i--)
            if (activeMissileObjs[i] != null) Destroy(activeMissileObjs[i]);
        activeMissileObjs.Clear();

        if (drones != null)
            for (int i = 0; i < drones.Length; i++)
                if (drones[i] != null) SpawnDeathEffect(drones[i].transform.position);

        VictoryManager.Trigger();
        Destroy(gameObject);
    }

    void SpawnDeathEffect(Vector3 pos)
    {
        for (int i = 0; i < 10; i++)
        {
            GameObject fragment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fragment.transform.position   = pos + Random.insideUnitSphere * 0.5f;
            fragment.transform.localScale = Vector3.one * Random.Range(0.1f, 0.3f);
            var mat = CreateLitMaterial(Color.red);
            SetMaterialOpaque(mat, 0.2f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.red * 3f);
            fragment.GetComponent<Renderer>().material = mat;
            var rb = fragment.AddComponent<Rigidbody>();
            rb.AddExplosionForce(500f, pos, 2f);
            Destroy(fragment, 2f);
        }
    }

    #endregion

    // ==========================================================================
    #region 血条 OnGUI

    void OnGUI()
    {
        if (Camera.main == null || currentHealth <= 0) return;

        float barX = (Screen.width - healthBarWidth) * 0.5f;
        float barY = 40f;

        GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        GUI.DrawTexture(new Rect(barX, barY, healthBarWidth, healthBarHeight), Texture2D.whiteTexture);

        float ratio = currentHealth / maxHealth;
        GUI.color = currentState == EnemyState.Stunned ? Color.cyan : Color.red;
        GUI.DrawTexture(new Rect(barX, barY, healthBarWidth * ratio, healthBarHeight), Texture2D.whiteTexture);

        GUI.color = Color.white;
        var style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize  = 12,
            fontStyle = FontStyle.Bold
        };
        GUI.Label(new Rect(barX, barY, healthBarWidth, healthBarHeight),
            $"{Mathf.Ceil(currentHealth)} / {maxHealth}", style);

        if (currentState == EnemyState.Stunned)
        {
            var stunnedStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize  = 14,
                fontStyle = FontStyle.Bold
            };
            stunnedStyle.normal.textColor = Color.cyan;
            GUI.Label(new Rect(barX, barY - 20f, healthBarWidth, 20f), "【核心暴露！】", stunnedStyle);
        }

        GUI.color = Color.white;
    }

    #endregion
}
