public class DemiGod : Enemy
{
    // Nombre que se muestra en la pantalla de victoria.
    private const string DisplayName = "al Semidiós";

    // Character.NotifyDeathClientRpc llama a OnDeath en el servidor/host y en todos los clientes
    // justo antes de que el jefe desaparezca: cada jugador ve su propio aviso de victoria.
    protected override void OnDeath()
    {
        base.OnDeath();
        BossVictoryUI.Show(DisplayName);
    }
}