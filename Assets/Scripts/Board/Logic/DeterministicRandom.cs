namespace CrystalClash.Board.Logic
{
    /// <summary>
    /// Generador aleatorio determinista (Mulberry32).
    /// Con la misma semilla produce SIEMPRE la misma secuencia, en cualquier
    /// plataforma y en cualquier lenguaje. Esto permite que dos jugadores
    /// reciban el mismo tablero y que un servidor pueda repetir la partida
    /// para validarla. No usamos UnityEngine.Random ni System.Random porque
    /// su algoritmo no está garantizado entre versiones/plataformas.
    /// </summary>
    public sealed class DeterministicRandom
    {
        private uint state;

        public DeterministicRandom(int seed)
        {
            state = unchecked((uint)seed);
        }

        public uint NextUInt()
        {
            unchecked
            {
                state += 0x6D2B79F5u;
                uint z = state;
                z = (z ^ (z >> 15)) * (z | 1u);
                z ^= z + (z ^ (z >> 7)) * (z | 61u);
                return z ^ (z >> 14);
            }
        }

        /// <summary>Entero en [0, maxExclusive).</summary>
        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0)
            {
                throw new System.ArgumentOutOfRangeException(nameof(maxExclusive));
            }

            return (int)(NextUInt() % (uint)maxExclusive);
        }
    }
}
