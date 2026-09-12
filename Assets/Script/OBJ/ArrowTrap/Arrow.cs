using UnityEngine;

public class Arrow : MonoBehaviour
{
    [SerializeField] private string destroyTag = "Enemy";

    void Start()
    {
        Destroy(gameObject, 5f);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag(destroyTag))
        {
            Destroy(collision.gameObject);
        }

        Destroy(gameObject);
    }
}