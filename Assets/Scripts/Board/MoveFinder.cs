using CrystalClash.Board.Logic;
using UnityEngine;

/// <summary>
/// La búsqueda de movimientos ahora vive en BoardLogic (TryFindMove /
/// HasAvailableMoves). Este componente se mantiene para no romper escenas
/// que ya lo tengan agregado, y sirve para pedir una pista.
/// </summary>
public class MoveFinder : MonoBehaviour
{
    public bool TryGetHint(BoardLogic logic, out GridPos a, out GridPos b)
    {
        return logic.TryFindMove(out a, out b);
    }
}
