using UnityEngine;
using UnityEngine.Events;

// A reusable arrow-only training target. Completion does not destroy the target.
public class ArrowTutorial : Entity
{
    [Header("Arrow Tutorial")]
    [SerializeField, Min(1)] private int requiredHits = 1;
    public UnityEvent onArrowHit = new UnityEvent();
    public UnityEvent onCompleted = new UnityEvent();
    public int HitCount { get; private set; }
    public bool IsCompleted { get; private set; }


    private void Start()
    {
        foreach (Collider targetCollider in GetComponentsInChildren<Collider>())
            ConfigureCollider(targetCollider);
        // Provide a visible target even when attached to an empty GameObject.
        if (GetComponentInChildren<Renderer>() == null)
        {
            CreateRing("Outer Ring", 0.7f, 0f, Color.red);
            CreateRing("Middle Ring", 0.48f, -0.025f, Color.white);
            CreateRing("Bullseye", 0.22f, -0.05f, Color.red);
        }
    }

    private void CreateRing(string partName, float size, float depth, Color color)
    {
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = partName;
        ring.transform.SetParent(transform, false);
        ring.transform.localPosition = new Vector3(0, 0.65f, depth);
        ring.transform.localRotation = Quaternion.Euler(90, 0, 0);
        ring.transform.localScale = new Vector3(size, 0.025f, size);
        // Enabled for mouse picking. Arrow wall checks ignore Entity colliders.
        ConfigureCollider(ring.GetComponent<Collider>());
        MaterialPropertyBlock properties = new MaterialPropertyBlock();
        properties.SetColor("_BaseColor", color);
        properties.SetColor("_Color", color);
        ring.GetComponent<Renderer>().SetPropertyBlock(properties);
    }

    // Sword hits must not complete an arrow lesson.
    private static void ConfigureCollider(Collider targetCollider)
    {
        if (targetCollider is MeshCollider mesh) mesh.convex = true;
        targetCollider.enabled = true;
        targetCollider.isTrigger = true;
    }

    public override void ReceiveHit() { }
    public override void ReceiveArrowHit()
    {
        if (Time.timeScale == 0) return;
        Debug.Log("HIT", this);
        onArrowHit.Invoke();
        if (IsCompleted) return;
        HitCount++;
        IsCompleted = HitCount >= Mathf.Max(1, requiredHits);

        if (IsCompleted) onCompleted.Invoke();
    }

    public void ResetTutorial()
    {
        HitCount = 0;
        IsCompleted = false;

    }

}

