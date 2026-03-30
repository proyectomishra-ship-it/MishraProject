using UnityEngine;

public class MovementController : MonoBehaviour
{
    private Character character;
    private CharacterController controller;

    [SerializeField] private float speed = 5f;

    public void Initialize(Character character)
    {
        this.character = character;
        controller = character.GetComponent<CharacterController>();
    }

    public void Move(Vector3 direction)
    {
        Debug.Log("MovementController.Move: " + direction);

        if (controller == null)
        {
            Debug.LogError("NO HAY CharacterController");
            return;
        }

        controller.Move(direction * speed * Time.deltaTime);
    }

    public void Run(Vector3 direction)
    {
        if (controller == null) return;

        controller.Move(direction * speed * 1.5f * Time.deltaTime);
    }

    public void Jump()
    {
        // lo dejamos pendiente (requiere gravedad)
    }
}