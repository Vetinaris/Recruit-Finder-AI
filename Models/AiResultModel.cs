public class AiResultModel
{
    public int offerId { get; set; }
    public string status { get; set; }
    public List<AiCandidateAnalysisResult> results { get; set; }
}

public class AiCandidateAnalysisResult
{
    public int applicationId { get; set; }
    public int score { get; set; }
    public string description { get; set; }
    public List<string> pros { get; set; }
    public List<string> cons { get; set; }
}