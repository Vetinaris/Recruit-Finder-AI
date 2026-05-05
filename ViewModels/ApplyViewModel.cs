using Recruit_Finder_AI.Models;

namespace Recruit_Finder_AI.Models
{
    public class ApplyViewModel
    {
        public int JobOfferId { get; set; }
        public JobOffer JobOffer { get; set; }
        public List<Cv> UserResumes { get; set; }
        public int SelectedCvId { get; set; }
        public string Message { get; set; }
    }
}