using UnityEngine;

namespace SuperYao
{
    /// <summary>Support mobile qui emporte le héros posé dessus (pirogue, camion du convoi, balançoire) : sa vitesse
    /// horizontale s'ajoute à celle du héros (voir HeroController).</summary>
    public interface ICarrier
    {
        Vector2 Velocity { get; }
    }
}
