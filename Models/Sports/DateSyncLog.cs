namespace QemmaProject.Models.Sports
{
    public class DateSyncLog
    {
        public int Id { get; set; }

        // بنخزن التاريخ بس من غير وقت (00:00:00) عشان يبقى المقارنة سهلة ودقيقة
        public DateTime Date { get; set; }

        public DateTime LastSyncedAt { get; set; }

        // هل لقينا ماتشات في آخر مرة اتعمل فيها sync لليوم ده؟
        public bool HasData { get; set; }
    }
}
