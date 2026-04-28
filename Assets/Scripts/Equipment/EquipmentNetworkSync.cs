using Unity.Netcode;

/// <summary>
/// Sincroniza el snapshot de slots a todos los clientes.
/// Todos ven qué lleva equipado cada personaje (necesario para modelos visuales).
/// No tiene lógica de negocio.
/// ACCIÓN: archivo nuevo en Assets/Scripts/Equipment/
/// EquipmentController lo agrega automáticamente via [RequireComponent].
/// </summary>
public class EquipmentNetworkSync : NetworkBehaviour
{
    public struct Snapshot : INetworkSerializable
    {
        public int Weapon, Helmet, Chest, Legs, Boots, Ring, Amulet;

        public static Snapshot Empty => new()
        {
            Weapon=-1, Helmet=-1, Chest=-1,
            Legs  =-1, Boots =-1, Ring =-1, Amulet=-1
        };

        public int Get(EquipmentSlot s) => s switch
        {
            EquipmentSlot.Weapon => Weapon, EquipmentSlot.Helmet => Helmet,
            EquipmentSlot.Chest  => Chest,  EquipmentSlot.Legs   => Legs,
            EquipmentSlot.Boots  => Boots,  EquipmentSlot.Ring   => Ring,
            EquipmentSlot.Amulet => Amulet, _ => -1
        };

        public void Set(EquipmentSlot s, int id)
        {
            switch (s)
            {
                case EquipmentSlot.Weapon: Weapon = id; break;
                case EquipmentSlot.Helmet: Helmet = id; break;
                case EquipmentSlot.Chest:  Chest  = id; break;
                case EquipmentSlot.Legs:   Legs   = id; break;
                case EquipmentSlot.Boots:  Boots  = id; break;
                case EquipmentSlot.Ring:   Ring   = id; break;
                case EquipmentSlot.Amulet: Amulet = id; break;
            }
        }

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Weapon); s.SerializeValue(ref Helmet);
            s.SerializeValue(ref Chest);  s.SerializeValue(ref Legs);
            s.SerializeValue(ref Boots);  s.SerializeValue(ref Ring);
            s.SerializeValue(ref Amulet);
        }
    }

    private NetworkVariable<Snapshot> netSnapshot = new(
        Snapshot.Empty,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public void UpdateSlot(EquipmentSlot slot, int itemId)
    {
        if (!IsServer) return;
        var s = netSnapshot.Value;
        s.Set(slot, itemId);
        netSnapshot.Value = s;
    }

    public int GetSlotId(EquipmentSlot slot) => netSnapshot.Value.Get(slot);

    public void Subscribe(NetworkVariable<Snapshot>.OnValueChangedDelegate cb)
        => netSnapshot.OnValueChanged += cb;

    public void Unsubscribe(NetworkVariable<Snapshot>.OnValueChangedDelegate cb)
        => netSnapshot.OnValueChanged -= cb;
}
