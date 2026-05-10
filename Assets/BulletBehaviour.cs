using UnityEngine;

public class BulletBehaviour : MonoBehaviour
{
    public float speed = 150f;
    public float damage = 10f;
    public float lifeTime = 2f;
    private bool isPerfectCounter;

    void Awake()
    {
        speed = 150f;
        damage = 10f;
        lifeTime = 2f;
    }

    public void Init(float dmg, bool buff)
    {
        damage = dmg;
        isPerfectCounter = buff;
    }

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        float moveDistance = speed * Time.deltaTime;

        RaycastHit[] hits = Physics.RaycastAll(transform.position, transform.forward, moveDistance);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider.GetComponentInParent<PlayerController>() != null)
                continue;

            SpawnImpact(hits[i].point);

            EnemyOrderSystem enemy = hits[i].collider.GetComponentInParent<EnemyOrderSystem>();
            if (enemy != null)
                enemy.TakeDamage(damage, isPerfectCounter);

            Destroy(gameObject);
            return;
        }

        transform.Translate(Vector3.forward * moveDistance);
    }

    void SpawnImpact(Vector3 position)
    {
        GameObject impactObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        impactObj.name = "Impact";
        impactObj.transform.position = position;
        impactObj.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);

        Renderer renderer = impactObj.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(1f, 0.8f, 0f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", Color.yellow * 5f);
        renderer.material = mat;

        Destroy(impactObj.GetComponent<Collider>());
        Destroy(impactObj, 0.5f);
    }
}
