using QemmaProject.Models.Payment;

namespace QemmaProject.Models.Sports
{
    public class Prediction
    {
        public int Id { get; set; }

        public string UserId { get; set; }
        public virtual ApplicationUser User { get; set; }

        public int MatchId { get; set; }
        public virtual Match Match { get; set; }

        public int? PredictionLeagueId { get; set; }
        public virtual PredictionLeague PredictionLeague { get; set; }

        public int PredictedHomeScore { get; set; }
        public int PredictedAwayScore { get; set; }

        public bool IsProcessed { get; set; } = false; // هل تم حساب النتيجة بعد الماتش؟
        public decimal PointsEarned { get; set; } // النقط اللي كسبها لو توقعه صح

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
