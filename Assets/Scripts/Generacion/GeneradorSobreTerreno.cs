using UnityEngine;

/// <summary>
/// Base de los generadores que ponen cosas encima del terreno (arboles, vegetacion, objetos...).
///
/// No hace falta conectarlos a mano: el <see cref="GeneradorTerreno"/> busca todos los que haya
/// en su mismo objeto o en sus hijos y los ejecuta (de menor a mayor <see cref="Orden"/>)
/// justo despues de crear el terreno, tanto en Play como en la preview del editor.
/// Todos reciben la misma semilla que el terreno.
/// </summary>
public abstract class GeneradorSobreTerreno : MonoBehaviour
{
    /// <summary>Los de menor orden se ejecutan antes (arboles antes que vegetacion, por ejemplo).</summary>
    public virtual int Orden => 0;

    /// <summary>Se llama cada vez que se genera el terreno (o al regenerar solo la decoracion).</summary>
    public abstract void Generar(GeneradorTerreno terreno, int semilla);

    /// <summary>Borra lo que haya creado este generador fuera del TerrainData (objetos temporales, etc).</summary>
    public virtual void Limpiar() { }

    protected virtual void OnDestroy() => Limpiar();

    /// <summary>Mezcla la semilla del terreno con un valor propio de cada generador,
    /// para que cada uno tenga su propia secuencia aleatoria sin afectar a los demas.</summary>
    protected static int MezclarSemilla(int semilla, int sal)
    {
        unchecked
        {
            int h = semilla * 486187739 + sal;
            h ^= h >> 13;
            h *= 1274126177;
            return h ^ (h >> 16);
        }
    }
}
