using System.ComponentModel.DataAnnotations.Schema;

namespace Recruit_Finder_AI.Models
{
public class JobApplication
{
    public int Id { get; set; }
    public int JobOfferId { get; set; }
    public JobOffer JobOffer { get; set; }

    public int CvId { get; set; }
    public Cv Cv { get; set; }

    public string CandidateId { get; set; }
    public ApplicationUser Candidate { get; set; }

    public DateTime AppliedAt { get; set; }
    public string? Message { get; set; }
    public string Status { get; set; }
    public virtual ICollection<JobApplication> Applications { get; set; } = new List<JobApplication>();
    }
}