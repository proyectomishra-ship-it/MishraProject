/// <summary>
/// Tipo de arma. Usado por CharacterData y WeaponData.
/// Separado en su propio archivo para que no colisione con la clase WeaponData.
/// ACCIÓN: reemplaza el contenido de Assets/Scripts/Data/WeaponData.cs existente
/// que solo tenía este enum. Crear este archivo como WeaponType.cs.
/// </summary>
public enum WeaponType
{
    Sword,
    Axe,
    Bow,
    Dagger,
    Staff,
    Mace,
    None
}
