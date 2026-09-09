using UnityEngine;
using UnityEngine.Events;

// A reusable arrow-only training target. Completion does not destroy the target.
public class ArrowTutorial : Entity
{
    [Header("Arrow Tutorial")]
    [SerializeField, Min(1)] private int requiredHits = 1;
    [Tooltip("Drag the floor Tile that represents this target's shooting cell here.")]
    [SerializeField] private Tile targetTile;
    public override Vector2Int CurrentLocation => targetTile != null
        ? GridManager.Instance.WorldToGrid(targetTile.transform.position) : base.CurrentLocation;
    public UnityEvent onArrowHit = new UnityEvent();
    public UnityEvent onCompleted = new UnityEvent();
    [Header("Tutorial Text")]
    [SerializeField] private Camera textCamera;
    [SerializeField] private Vector3 textOffset = new Vector3(0f, 1.4f, 0f);
    [SerializeField] private Vector3 textRotationOffset = Vector3.zero;
    [SerializeField, Min(0.01f)] private float textSize = 0.06f;
    public int HitCount { get; private set; }
    public bool IsCompleted { get; private set; }
    private Tile occupiedTile;
    private TextMesh label;

    private void Start()
    {
        foreach (Collider targetCollider in GetComponentsInChildren<Collider>())
            targetCollider.enabled = true;
        if (GridManager.Instance != null)
        {
            Tile tile = targetTile != null ? targetTile : GridManager.Instance.GetTile(CurrentLocation);
            if (tile != null && tile.SetOccupant(gameObject)) occupiedTile = tile;
            else Debug.LogWarning("ArrowTutorial: ลากพื้นช่องของเป้าใส่ Target Tile และตรวจว่าช่องไม่มีตัวละครยืนอยู่ พิกัดเป้า: " + CurrentLocation, this);
        }
        // Provide a visible target even when attached to an empty GameObject.
        if (GetComponentInChildren<Renderer>() == null)
        {
            CreateRing("Outer Ring", 0.7f, 0f, Color.red);
            CreateRing("Middle Ring", 0.48f, -0.025f, Color.white);
            CreateRing("Bullseye", 0.22f, -0.05f, Color.red);
        }
        GameObject text = new GameObject("Tutorial Label");
        // Keep text outside the target hierarchy: rotated, non-uniform scales
        // otherwise shear a child label even when its world rotation is set.
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(text, gameObject.scene);
        label = text.AddComponent<TextMesh>();
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.characterSize = textSize;
        label.fontSize = 48;
        RefreshLabel();
        UpdateLabelTransform();
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
        ring.GetComponent<Collider>().enabled = true;
        MaterialPropertyBlock properties = new MaterialPropertyBlock();
        properties.SetColor("_BaseColor", color);
        properties.SetColor("_Color", color);
        ring.GetComponent<Renderer>().SetPropertyBlock(properties);
    }

    // Sword hits must not complete an arrow lesson.
    public override void ReceiveHit() { }
    public override void ReceiveArrowHit()
    {
        if (IsCompleted || Time.timeScale == 0) return;
        HitCount++;
        IsCompleted = HitCount >= Mathf.Max(1, requiredHits);
        RefreshLabel();
        onArrowHit.Invoke();
        if (IsCompleted) onCompleted.Invoke();
    }

    public void ResetTutorial()
    {
        HitCount = 0;
        IsCompleted = false;
        RefreshLabel();
    }

    private void RefreshLabel()
    {
        if (label == null) return;
        label.text = IsCompleted ? "Arrow Tutorial: Complete!" :
            "Select Player2\nRight-click to shoot\n" + HitCount + " / " + Mathf.Max(1, requiredHits);
        label.color = IsCompleted ? Color.green : Color.white;
    }

    private void LateUpdate()
    {
        UpdateLabelTransform();
    }

    private void UpdateLabelTransform()
    {
        if (label == null) return;
        Camera view = textCamera != null ? textCamera : Camera.main;
        label.transform.position = transform.position + textOffset;
        label.transform.localScale = Vector3.one;
        label.characterSize = Mathf.Max(0.01f, textSize);
        if (view != null)
            label.transform.rotation = view.transform.rotation * Quaternion.Euler(textRotationOffset);
    }

    private void OnEnable()
    {
        if (label != null) label.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        if (label != null) label.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (label != null) Destroy(label.gameObject);
        if (occupiedTile != null) occupiedTile.ClearOccupant(gameObject);
    }
}
