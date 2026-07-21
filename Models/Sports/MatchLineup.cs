namespace QemmaProject.Models.Sports
{
    public class MatchLineup
    {
        public int Id { get; set; }
        public int MatchId { get; set; }
        public virtual Match Match { get; set; }

        public string TeamSide { get; set; }   // "Home" or "Away"
        public string PlayerName { get; set; }
        public string Number { get; set; }     // shirt number
        public string Position { get; set; }   // GK, DF, MF, FW ... etc
        public bool IsSubstitute { get; set; } // false = starting XI, true = bench
    }
}