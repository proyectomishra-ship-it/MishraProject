using UnityEngine;
public class TargetingController : MonoBehaviour
{
    private Character character;
    private Camera mainCamera;
    private Character currentTarget;
    [SerializeField] private LayerMask enemyLayer;
    public Character CurrentTarget => currentTarget;
    public void Initialize(Character character)
    {
        this.character = character;
        mainCamera = Camera.main;
    }
    public void UpdateTarget()
    {
        if (mainCamera == null) return;
        float range = character.GetStats().AttackRange.Value;
        Ray ray = mainCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f));

        if (Physics.Raycast(ray, out RaycastHit hit, range, enemyLayer))
        {
            currentTarget = hit.collider.GetComponent<Character>();
            Debug.DrawRay(ray.origin, ray.direction * hit.distance, Color.red, 0.5f);
            Debug.DrawRay(hit.point, Vector3.up * 0.5f, Color.red, 0.5f);
        }
        else
        {
            currentTarget = null;
            Debug.DrawRay(ray.origin, ray.direction * range, Color.green, 0.1f);
        }
    }
}