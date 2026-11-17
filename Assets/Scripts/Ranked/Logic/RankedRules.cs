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

        /// <summary>Factor K del Elo: cuánto se mueve el ranking por partida.</summary>
        public const int K = 40;

        /// <summary>Lo mínimo que se gana por victoria (para que ganar siempre se note).</summary>
        public const int MinWinPoints = 8;

        public const int StartingPoints = 0;

        /// <summary>
        /// Puntos de ranking que gana (positivo) o pierde (negativo) un jugador.
        /// Ganarle a alguien con más puntos da más; perder contra alguien con
        /// menos quita más. Nunca baja de 0.
        /// </summary>
        public static int PointsChange(int myPoints, int opponentPoints, MatchOutcome outcome)
        {
            double expected = 1.0 / (1.0 + Math.Pow(10.0, (opponentPoints - myPoints) / 400.0));
            double actual = outcome == MatchOutcome.Win ? 1.0 : outcome == MatchOutcome.Draw ? 0.5 : 0.0;
            int delta = (int)Math.Round(K * (actual - expected), MidpointRounding.AwayFromZero);

            if (outcome == MatchOutcome.Win) delta = Math.Max(delta, MinWinPoints);
            if (myPoints + delta < 0) delta = -myPoints;
            return delta;
        }

        public static MatchOutcome Compare(int myScore, int opponentScore)
        {
            if (myScore > opponentScore) return MatchOutcome.Win;
            if (myScore < opponentScore) return MatchOutcome.Loss;
            return MatchOutcome.Draw;
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
