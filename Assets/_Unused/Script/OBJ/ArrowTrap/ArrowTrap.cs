using System.Collections;
using UnityEngine;

public class ArrowTrap : MonoBehaviour
{
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float shootEvery = 1.5f;
    [SerializeField] private float arrowSpeed = 18f;

    void OnEnable()
    {
        StartCoroutine(ShootLoop());
    }

    IEnumerator ShootLoop()
    {
        while (true)
        {
            Shoot();
            yield return new WaitForSeconds(shootEvery);
        }
    }

    void Shoot()
    {
        GameObject arrow = Instantiate(
            arrowPrefab,
            firePoint.position,
            firePoint.rotation
        );

        Rigidbody rb = arrow.GetComponent<Rigidbody>();

        if (rb != null)
            rb.linearVelocity = firePoint.forward * arrowSpeed;
    }
}