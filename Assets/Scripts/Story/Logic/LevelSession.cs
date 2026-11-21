using System.Collections.Generic;
using CrystalClash.Board.Logic;

namespace CrystalClash.Story.Logic
{
    public enum SessionState
    {
        Playing,
        Won,
        Lost
    }

    public sealed class ObjectiveProgress
    {
        public LevelObjective Objective { get; }
        public int Current { get; internal set; }
        public bool IsComplete => Current >= Objective.Target;

        internal ObjectiveProgress(LevelObjective objective)
        {
            Objective = objective;
        }
    }

    /// <summary>
    /// Reglas de una partida del modo historia: movimientos, objetivos, victoria,
    /// derrota y estrellas. No conoce el tablero; solo recibe el resultado de
    /// cada jugada (SwapResult), así que es fácil de testear.
    /// </summary>
    public sealed class LevelSession
    {

        private readonly List<ObjectiveProgress> objectives = new List<ObjectiveProgress>();

        public LevelDefinition Level { get; }
        public int MovesLeft { get; private set; }
        public int Score { get; private set; }
        public SessionState State { get; private set; } = SessionState.Playing;
        public IReadOnlyList<ObjectiveProgress> Objectives => objectives;

        /// <summary>Puntos de las bombas de bonus (movimientos sobrantes al ganar).</summary>
        public int MoveBonus { get; private set; }

        /// <summary>Cuántas bombas de bonus hubo (= movimientos que sobraron).</summary>
        public int FinaleBombs { get; private set; }

        public int FinalScore => Score + MoveBonus;

        /// <summary>0 si perdió; 1-3 si ganó (ganar siempre da al menos 1).</summary>
        public int Stars { get; private set; }

        public bool CanPlay => State == SessionState.Playing && MovesLeft > 0;

        public LevelSession(LevelDefinition level)
        {
            level.Validate();
            Level = level;
            MovesLeft = level.MaxMoves;

            foreach (LevelObjective objective in level.Objectives)
            {
                objectives.Add(new ObjectiveProgress(objective));
            }
        }

        /// <summary>Aplica el resultado de una jugada. Las jugadas inválidas no gastan movimiento.</summary>
        public void ApplyMove(SwapResult result)
        {
            Apply(result, spendMove: true);
        }

        /// <summary>Aplica el resultado de un potenciador: suma puntos y objetivos, pero no gasta movimiento.</summary>
        public void ApplyBooster(SwapResult result)
        {
            Apply(result, spendMove: false);
        }

        private void Apply(SwapResult result, bool spendMove)
        {
            if (result == null || !result.IsValid || !CanPlay) return;

            if (spendMove) MovesLeft--;
            Score += result.PointsGained;

            foreach (CascadeStep step in result.Steps)
            {
                foreach (int type in step.ClearedTypes)
                {
                    foreach (ObjectiveProgress progress in objectives)
                    {
                        if (progress.Objective.Kind == ObjectiveKind.CollectMineral &&
                            progress.Objective.MineralType == type &&
                            !progress.IsComplete)
                        {
                            progress.Current++;
                        }
                    }
                }
            }

            foreach (ObjectiveProgress progress in objectives)
            {
                if (progress.Objective.Kind == ObjectiveKind.ReachScore)
                {
                    progress.Current = Score < progress.Objective.Target ? Score : progress.Objective.Target;
                }
            }

            if (AllObjectivesComplete())
            {
                State = SessionState.Won;
                MoveBonus = 0;
                Stars = CalculateStars(FinalScore);
            }
            else if (MovesLeft == 0)
            {
                State = SessionState.Lost;
                Stars = 0;
            }
        }

        /// <summary>
        /// Suma los puntos de las bombas de bonus (BoardLogic.PlayFinale) y
        /// recalcula las estrellas. Solo después de ganar.
        /// </summary>
        public void ApplyFinale(SwapResult finale)
        {
            if (State != SessionState.Won || finale == null) return;
            MoveBonus += finale.PointsGained;
            FinaleBombs += finale.FinaleBombs.Count;
            Stars = CalculateStars(FinalScore);
        }

        public bool AllObjectivesComplete()
        {
            foreach (ObjectiveProgress progress in objectives)
            {
                if (!progress.IsComplete) return false;
            }
            return true;
        }

        private int CalculateStars(int finalScore)
        {
            int stars = 1;
            if (finalScore >= Level.StarScores[1]) stars = 2;
            if (finalScore >= Level.StarScores[2]) stars = 3;
            return stars;
        }
    }
}
