using System;
using System.Collections.Generic;
using System.Text;
using CrystalClash.Board.Logic;

namespace CrystalClash.Ranked.Logic
{
    public enum MatchOutcome
    {
        Loss = 0,
        Draw = 1,
        Win = 2
    }

    /// <summary>
    /// Reglas de una partida clasificatoria 1 vs 1. Este archivo lo usan el
    /// juego y el servidor (Cloud Code), así que los dos calculan exactamente lo mismo.
    /// </summary>
    public static class RankedRules
    {
        public const int Width = 8;
        public const int Height = 8;
        public const int MineralTypes = 6;
        public const int Moves = 20;

        /// <summary>Tiempo máximo de una partida. Al acabarse cuenta lo que lleves.</summary>
        public const int TimeLimitSeconds = 150;

        /// <summary>Factor K del Elo al ganar: cuánto se sube por partida (alto para que subir se sienta).</summary>
        public const int K = 60;

        /// <summary>Factor K del Elo al perder (más bajo: perder quita menos de lo que da ganar).</summary>
        public const int LossK = 40;

        /// <summary>Lo mínimo que se gana por victoria (para que ganar siempre se note).</summary>
        public const int MinWinPoints = 12;

        public const int StartingPoints = 0;

        /// <summary>
        /// Puntos de ranking que gana (positivo) o pierde (negativo) un jugador.
        /// Base Elo: ganarle a alguien con más puntos da más; perder contra
        /// alguien con menos quita más. Luego se ajusta por liga (ver
        /// RankTiers.Leagues). Nunca baja de 0.
        /// </summary>
        public static int PointsChange(int myPoints, int opponentPoints, MatchOutcome outcome)
        {
            double expected = 1.0 / (1.0 + Math.Pow(10.0, (opponentPoints - myPoints) / 400.0));
            double actual = outcome == MatchOutcome.Win ? 1.0 : outcome == MatchOutcome.Draw ? 0.5 : 0.0;
            double diff = actual - expected;
            double raw = (diff >= 0 ? K : LossK) * diff;

            // Según la liga del jugador: abajo se gana mucho y se pierde poco; arriba, al revés.
            RankTiers.League league = RankTiers.LeagueForPoints(myPoints);
            double multiplier = raw >= 0 ? league.WinMultiplier : league.LossMultiplier;
            int delta = (int)Math.Round(raw * multiplier, MidpointRounding.AwayFromZero);

            if (outcome == MatchOutcome.Win)
            {
                int minimum = (int)Math.Round(MinWinPoints * league.WinMultiplier, MidpointRounding.AwayFromZero);
                delta = Math.Max(delta, Math.Max(1, minimum));
            }
            if (myPoints + delta < 0) delta = -myPoints;
            return delta;
        }

        /// <summary>Cuánto se gana y se pierde (aprox.) contra un rival de tus mismos puntos.</summary>
        public static void TypicalChange(int myPoints, out int win, out int loss)
        {
            win = PointsChange(myPoints, myPoints, MatchOutcome.Win);
            // La pérdida típica sin el tope de 0 (para mostrarla aunque tengas pocos puntos).
            RankTiers.League league = RankTiers.LeagueForPoints(myPoints);
            loss = -(int)Math.Round(LossK * 0.5 * league.LossMultiplier, MidpointRounding.AwayFromZero);
        }

        public static MatchOutcome Compare(int myScore, int opponentScore)
        {
            if (myScore > opponentScore) return MatchOutcome.Win;
            if (myScore < opponentScore) return MatchOutcome.Loss;
            return MatchOutcome.Draw;
        }

        // ------------------------------------------------------------------
        // Nombres de jugador
        // ------------------------------------------------------------------

        public const int NameMinLength = 3;
        public const int NameMaxLength = 16;

        /// <summary>
        /// Limpia y valida un nombre: 3-16 caracteres, letras (con tildes y ñ),
        /// números, guion bajo y espacios simples. Devuelve null si no es válido.
        /// </summary>
        public static string NormalizeName(string name)
        {
            if (name == null) return null;
            var sb = new StringBuilder();
            bool lastWasSpace = true;
            foreach (char c in name.Trim())
            {
                if (char.IsWhiteSpace(c))
                {
                    if (!lastWasSpace) sb.Append(' ');
                    lastWasSpace = true;
                    continue;
                }
                if (!char.IsLetterOrDigit(c) && c != '_') return null;
                sb.Append(c);
                lastWasSpace = false;
            }

            string result = sb.ToString().Trim();
            return result.Length >= NameMinLength && result.Length <= NameMaxLength ? result : null;
        }

        public static BoardLogic CreateBoard(int seed)
        {
            return new BoardLogic(Width, Height, MineralTypes, seed);
        }

        // ------------------------------------------------------------------
        // Movimientos: se envían como texto compacto "x1y1x2y2;x1y1x2y2;..."
        // ------------------------------------------------------------------

        public static string EncodeMoves(IEnumerable<(GridPos a, GridPos b)> moves)
        {
            var sb = new StringBuilder();
            foreach (var move in moves)
            {
                if (sb.Length > 0) sb.Append(';');
                sb.Append(move.a.X).Append(',').Append(move.a.Y).Append(',')
                  .Append(move.b.X).Append(',').Append(move.b.Y);
            }
            return sb.ToString();
        }

        /// <summary>Lanza FormatException si el texto está mal formado.</summary>
        public static List<(GridPos a, GridPos b)> DecodeMoves(string encoded)
        {
            var moves = new List<(GridPos, GridPos)>();
            if (string.IsNullOrEmpty(encoded)) return moves;

            foreach (string part in encoded.Split(';'))
            {
                string[] n = part.Split(',');
                if (n.Length != 4) throw new FormatException($"Movimiento mal formado: '{part}'");
                moves.Add((new GridPos(int.Parse(n[0]), int.Parse(n[1])),
                           new GridPos(int.Parse(n[2]), int.Parse(n[3]))));
            }
            return moves;
        }

        /// <summary>
        /// Repite una partida y devuelve los puntos reales. Lo usa el servidor
        /// para no fiarse del puntaje que manda el cliente.
        /// Devuelve false si hay más movimientos de los permitidos o alguno es inválido.
        /// </summary>
        public static bool TryReplay(int seed, string encodedMoves, out int score, out string error)
        {
            score = 0;
            error = null;

            List<(GridPos a, GridPos b)> moves;
            try
            {
                moves = DecodeMoves(encodedMoves);
            }
            catch (FormatException e)
            {
                error = e.Message;
                return false;
            }

            if (moves.Count > Moves)
            {
                error = $"Demasiados movimientos: {moves.Count} (máximo {Moves})";
                return false;
            }

            BoardLogic board = CreateBoard(seed);
            for (int i = 0; i < moves.Count; i++)
            {
                if (!board.TrySwap(moves[i].a, moves[i].b).IsValid)
                {
                    error = $"Movimiento {i + 1} inválido: {moves[i].a} -> {moves[i].b}";
                    return false;
                }
            }

            score = board.Score;
            return true;
        }
    }
}
