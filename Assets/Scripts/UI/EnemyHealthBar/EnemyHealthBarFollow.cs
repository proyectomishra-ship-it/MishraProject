using UnityEngine;

public class EnemyHealthBarFollow : MonoBehaviour
{
    private Transform target;

    [SerializeField]
    private Vector3 offset = new Vector3(0f, 2f, 0f);

    public void SetTarget(Transform targetTransform)
    {
        target = targetTransform;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        transform.position = target.position + offset;
    }
}