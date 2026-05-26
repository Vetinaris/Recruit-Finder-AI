using Recruit_Finder_AI.Models;
namespace Recruit_Finder_AI.Models
{
    public class AiApplicationReport
    {
        public int Id { get; set; }
        public int JobApplicationId { get; set; }
        public JobApplication JobApplication { get; set; }

        public int Score { get; set; }
        public string Description { get; set; }
        public string Pros { get; set; }
        public string Cons { get; set; }
        public DateTime AnalyzedAt { get; set; }
    }
}