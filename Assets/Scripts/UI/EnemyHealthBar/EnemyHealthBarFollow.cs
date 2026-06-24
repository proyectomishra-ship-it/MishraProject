using UnityEngine;

public class EnemyHealthBarFollow : MonoBehaviour
{
    private Transform target;

    [SerializeField]
    private Vector3 offset =
        new Vector3(0f, 2f, 0f);

    public void SetTarget(Transform targetTransform)
    {
        target = targetTransform;
        Debug.Log("Target asignado: " + target.name);
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            Debug.Log("NO TARGET");
            return;
        }

        transform.position =
            target.position + offset;
        Debug.Log(transform.position);
    }

    //private void LateUpdate()
    //{
    //    transform.position =
    //        new Vector3(0, 5, 0);
    //}
}