using System;
using System.Collections.Generic;
using System.Linq;
using CrystalClash.Board.Logic;
using CrystalClash.Ranked.Logic;

namespace CrystalClashRanked
{
    /// <summary>
    /// Reglas del servidor sin acceso a red (se pueden testear sin Unity Gaming Services):
    /// emparejamiento, bot, y cálculo del resultado final.
    /// </summary>
    public static class ServerLogic
    {
        /// <summary>Si nadie hace caso a su ticket en este tiempo, se borra.</summary>
        public const int TicketStaleSeconds = 20;

        /// <summary>Tras esperar esto sin rival, se juega contra un bot.</summary>
        public const int BotAfterSeconds = 25;

        /// <summary>Segundos de margen para la pantalla de VS antes de empezar a contar.</summary>
        public const int StartDelaySeconds = 8;

        /// <summary>Pasado este tiempo desde que se creó la partida, quien no envió pierde por abandono.</summary>
        public const int MatchGraceSeconds = RankedRules.TimeLimitSeconds + StartDelaySeconds + 60;

        /// <summary>Contra un bot se gana/pierde la mitad (para que no sirva para subir fácil).</summary>
        public const double BotMultiplier = 0.5;

        public const string BotNamePrefix = "Bot ";

        /// <summary>
        /// Diferencia de puntos aceptada para emparejar: crece mientras más
        /// esperó el ticket que ya estaba en la cola.
        /// </summary>
        public static int PointsWindow(long waitedSeconds)
        {
            return (int)Math.Min(3000, 250 + 40 * Math.Max(0, waitedSeconds));
        }

        /// <summary>Quita tickets abandonados y emparejados hace mucho.</summary>
        public static void CleanQueue(QueueState queue, long now)
        {
            queue.Tickets.RemoveAll(t =>
                (t.MatchId == null && now - t.LastPollUtc > TicketStaleSeconds) ||
                (t.MatchId != null && now - t.LastPollUtc > 120));
        }

        /// <summary>El mejor rival en la cola para este jugador, o null.</summary>
        public static Ticket PickOpponent(QueueState queue, string playerId, int points, long now)
        {
            return queue.Tickets
                .Where(t => t.MatchId == null && t.PlayerId != playerId)
                .Where(t => Math.Abs(t.Points - points) <= PointsWindow(now - t.CreatedUtc))
                .OrderBy(t => Math.Abs(t.Points - points))
                .ThenBy(t => t.CreatedUtc)
                .FirstOrDefault();
        }

        public static int NewSeed(Random random)
        {
            return random.Next(1, int.MaxValue);
        }

        // ------------------------------------------------------------------
        // Bot: juega el mismo tablero, de forma determinista, a ritmo humano
        // ------------------------------------------------------------------

        public static double BotSecondsPerMove(int seed)
        {
            return 4.5 + new Random(seed ^ 0x2c1b3c6d).NextDouble() * 2.0;
        }

        /// <summary>Puntaje del bot tras hacer N jugadas (siempre el mismo para la misma semilla).</summary>
        public static int BotScoreAfter(int seed, int moves)
        {
            BoardLogic board = RankedRules.CreateBoard(seed);
            var random = new Random(seed ^ 0x5bd1e995);
            moves = Math.Max(0, Math.Min(RankedRules.Moves, moves));

            for (int i = 0; i < moves; i++)
            {
                List<(GridPos a, GridPos b)> options = ValidMoves(board);
                if (options.Count == 0) break;
                var pick = options[random.Next(options.Count)];
                board.TrySwap(pick.a, pick.b);
            }
            return board.Score;
        }

        public static ProgressDto BotProgress(int seed, long matchCreatedUtc, long now)
        {
            double elapsed = Math.Max(0, now - matchCreatedUtc - StartDelaySeconds);
            int made = Math.Min(RankedRules.Moves, (int)(elapsed / BotSecondsPerMove(seed)));
            return new ProgressDto
            {
                Score = BotScoreAfter(seed, made),
                MovesLeft = RankedRules.Moves - made,
                Finished = made >= RankedRules.Moves
            };
        }

        private static List<(GridPos, GridPos)> ValidMoves(BoardLogic board)
        {
            // Probamos sobre una copia para no tocar el tablero real.
            var moves = new List<(GridPos, GridPos)>();
            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    var from = new GridPos(x, y);
                    foreach (var to in new[] { new GridPos(x + 1, y), new GridPos(x, y + 1) })
                    {
                        if (!board.IsInside(to)) continue;
                        BoardLogic probe = Copy(board);
                        if (probe.TrySwap(from, to).IsValid) moves.Add((from, to));
                    }
                }
            }
            return moves;
        }

        private static BoardLogic Copy(BoardLogic board)
        {
            var types = new int[board.Width, board.Height];
            var specials = new SpecialKind[board.Width, board.Height];
            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    types[x, y] = board.GetMineral(x, y);
                    specials[x, y] = board.GetSpecial(x, y);
                }
            }
            return BoardLogic.FromLayout(types, specials, board.MineralTypeCount, 1);
        }

        // ------------------------------------------------------------------
        // Resultado
        // ------------------------------------------------------------------

        /// <summary>¿Ya se puede decidir la partida?</summary>
        public static bool CanFinalize(MatchState match, long now)
        {
            if (match.Finalized) return false;
            bool humansDone = match.Players.Where(p => !p.IsBot).All(p => p.Submitted);
            return humansDone || now - match.CreatedUtc > MatchGraceSeconds;
        }

        /// <summary>
        /// Calcula el resultado de cada jugador (sin guardar nada). Quien no
        /// envió su partida a tiempo cuenta con 0 puntos (abandono). El bot
        /// siempre completa sus 20 jugadas.
        /// </summary>
        public static Dictionary<string, ResultDto> ComputeResults(MatchState match, Func<string, int> currentPoints)
        {
            var scores = new Dictionary<string, int>();
            foreach (MatchPlayer p in match.Players)
            {
                if (p.IsBot) scores[p.PlayerId] = BotScoreAfter(match.Seed, RankedRules.Moves);
                else scores[p.PlayerId] = p.Submitted && !p.Invalid ? p.FinalScore : 0;
            }

            bool vsBot = match.Players.Any(p => p.IsBot);
            var results = new Dictionary<string, ResultDto>();
            foreach (MatchPlayer me in match.Players)
            {
                if (me.IsBot) continue;
                MatchPlayer other = match.Players.First(p => p.PlayerId != me.PlayerId);

                int myScore = scores[me.PlayerId];
                int otherScore = scores[other.PlayerId];
                MatchOutcome outcome = RankedRules.Compare(myScore, otherScore);

                // Abandono: quien no envió pierde; si el rival (humano) abandonó y yo sí envié, gano.
                if (!me.Submitted) outcome = MatchOutcome.Loss;
                else if (!other.IsBot && !other.Submitted) outcome = MatchOutcome.Win;

                int change = RankedRules.PointsChange(me.PointsAtStart, other.PointsAtStart, outcome);
                if (vsBot)
                {
                    change = (int)Math.Round(change * BotMultiplier, MidpointRounding.AwayFromZero);
                    if (outcome == MatchOutcome.Win) change = Math.Max(1, change);
                }

                int now = currentPoints(me.PlayerId);
                if (now + change < 0) change = -now;

                results[me.PlayerId] = new ResultDto
                {
                    Pending = false,
                    MyScore = myScore,
                    OpponentScore = otherScore,
                    Outcome = (int)outcome,
                    PointsChange = change,
                    NewPoints = now + change,
                    OpponentName = other.Name,
                    OpponentIsBot = other.IsBot
                };
            }
            return results;
        }
    }
}
