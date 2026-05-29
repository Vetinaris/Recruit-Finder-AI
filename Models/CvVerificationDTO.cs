namespace Recruit_Finder_AI.Models
{
    public class CvVerificationDto
    {
        public int Id { get; set; }
        public bool IsVerified { get; set; }
        public string AiFeedback { get; set; } = string.Empty;
    }
}
