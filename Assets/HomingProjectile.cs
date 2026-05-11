using UnityEngine;

public class HomingProjectile : MonoBehaviour
{
    public float speed = 5f;
    public float homingStrength = 2f;
    public float damage = 15f;
    public float lifeTime = 8f;

    private Transform target;
    private MeshRenderer meshRend;

    private bool isStraightLine;
    private Vector3 straightDir;
    private Vector3 targetPos;
    private float straightSpeed = 40f;

    void Awake()
    {
        speed = 5f;
        homingStrength = 2f;
        damage = 15f;
        lifeTime = 8f;
    }

    public void Init(Transform targetTransform, float dmg)
    {
        target = targetTransform;
        damage = dmg;
    }

    public void SetAsStraightLine(Vector3 dir, Vector3 destination, float spd, float lt)
    {
        isStraightLine = true;
        straightDir = dir.normalized;
        targetPos = destination;
        straightSpeed = spd;
        lifeTime = lt;
    }

    void Start()
    {
        meshRend = GetComponent<MeshRenderer>();
        Destroy(gameObject, lifeTime);
    }

    public void SetVisible(bool visible)
    {
        if (meshRend != null)
            meshRend.enabled = visible;
    }

    void Update()
    {
        if (isStraightLine)
        {
            UpdateStraightLine();
            return;
        }

        UpdateHoming();
    }

    void UpdateStraightLine()
    {
        float moveDistance = straightSpeed * Time.deltaTime;

        float sphereRadius = 0.2f;
        RaycastHit[] hits = Physics.SphereCastAll(transform.position, sphereRadius, straightDir, moveDistance);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        EnemyOrderSystem enemySystem = FindObjectOfType<EnemyOrderSystem>();
        for (int i = 0; i < hits.Length; i++)
        {
            if (enemySystem != null && enemySystem.GetDroneIndex(hits[i].collider) >= 0)
                continue;
            if (hits[i].collider.GetComponentInParent<HomingProjectile>() != null)
                continue;

            PlayerController player = hits[i].collider.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                player.ReceiveAttack(damage);
                Destroy(gameObject);
                return;
            }
        }

        transform.position += straightDir * moveDistance;

        float distToTarget = Vector3.Distance(transform.position, targetPos);
        if (distToTarget < 1f)
        {
            Destroy(gameObject);
        }
    }

    void UpdateHoming()
    {
        if (target != null)
        {
            Vector3 direction = (target.position - transform.position).normalized;
            Vector3 newForward = Vector3.Lerp(transform.forward, direction, homingStrength * Time.deltaTime);
            transform.forward = newForward;
        }

        float moveDistance = speed * Time.deltaTime;

        float sphereRadius = 0.2f;
        RaycastHit[] hits = Physics.SphereCastAll(transform.position, sphereRadius, transform.forward, moveDistance);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        EnemyOrderSystem enemySystem = FindObjectOfType<EnemyOrderSystem>();
        for (int i = 0; i < hits.Length; i++)
        {
            if (enemySystem != null && enemySystem.GetDroneIndex(hits[i].collider) >= 0)
                continue;
            if (hits[i].collider.GetComponentInParent<HomingProjectile>() != null)
                continue;

            PlayerController player = hits[i].collider.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                player.ReceiveAttack(damage);
                Destroy(gameObject);
                return;
            }
        }

        transform.position += transform.forward * moveDistance;
    }
}
