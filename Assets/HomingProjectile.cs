using UnityEngine;

public class HomingProjectile : MonoBehaviour
{
    public float speed = 5f;
    public float homingStrength = 2f;
    public float damage = 15f;
    public float lifeTime = 8f;
    private Transform target;
    private MeshRenderer meshRend;

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
        if (target != null)
        {
            Vector3 direction = (target.position - transform.position).normalized;
            Vector3 newForward = Vector3.Lerp(transform.forward, direction, homingStrength * Time.deltaTime);
            transform.forward = newForward;
        }

        float moveDistance = speed * Time.deltaTime;

        RaycastHit[] hits = Physics.RaycastAll(transform.position, transform.forward, moveDistance + 0.3f);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider.GetComponentInParent<EnemyOrderSystem>() != null)
                continue;

            PlayerController player = hits[i].collider.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                player.ReceiveAttack(damage);
                Destroy(gameObject);
                return;
            }

            if (hits[i].collider.GetComponentInParent<HomingProjectile>() != null)
                continue;

            Destroy(gameObject);
            return;
        }

        transform.position += transform.forward * moveDistance;
    }
}
