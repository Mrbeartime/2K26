using UnityEngine;

public class CharacterFacing : MonoBehaviour
{
    [Header("Model")]
    [SerializeField] private Transform visualRoot;

    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 540f;
    [SerializeField] private float modelYawOffset = 0f;

    private Quaternion targetRotation;
    private bool hasTarget;

    private void Awake()
    {
        // ถ้าไม่ได้ใส่ VisualRoot
        // ให้ใช้ GameObject ตัวเองแทน
        if (visualRoot == null)
        {
            visualRoot = transform;
        }
    }

    public void FaceTowards(Vector3 targetPosition, bool instant = false)
    {
        if (visualRoot == null)
            return;

        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        targetRotation =
            Quaternion.LookRotation(direction.normalized) *
            Quaternion.Euler(0f, modelYawOffset, 0f);

        hasTarget = true;

        if (instant)
        {
            visualRoot.rotation = targetRotation;
        }
    }

    private void LateUpdate()
    {
        if (!hasTarget || visualRoot == null)
            return;

        visualRoot.rotation = Quaternion.RotateTowards(
            visualRoot.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }
}