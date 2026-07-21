namespace QemmaProject.Models.Sports
{
    public class Standing
    {
        public int Id { get; set; }
        public int TournamentId { get; set; }
        public virtual Tournament Tournament { get; set; }

        public int TeamId { get; set; }
        public virtual Team Team { get; set; }

        public int Rank { get; set; } // المركز
        public int Points { get; set; }
        public int Played { get; set; }
        public int Won { get; set; }
        public int Drawn { get; set; }
        public int Lost { get; set; }
    }
}
