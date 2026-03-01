using UnityEngine;

public class PlayerInputController : MonoBehaviour
{
    private Player player;

    private void Awake()
    {
        player = GetComponent<Player>();
    }

    private void Update()
    {
        HandleMovement();
        HandleActions();
    }

    private void HandleMovement()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 direction = new Vector3(h, 0, v);

        if (Input.GetKey(KeyCode.LeftShift))
            player.Run(direction);
        else
            player.Move(direction);

        if (Input.GetKeyDown(KeyCode.Space))
            player.Jump();
    }

    private void HandleActions()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // Detectar target
            // player.Attack(target);
        }

        if (Input.GetMouseButtonDown(1))
        {
            // player.SpecialAttack(target);
        }
    }
}