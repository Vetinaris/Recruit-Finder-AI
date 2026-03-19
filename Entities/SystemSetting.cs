using System.ComponentModel.DataAnnotations;
namespace Recruit_Finder_AI.Entities
{
    public class SystemSetting
    {
        [Key]
        public string Key { get; set; }
        [Required]
        public string Value { get; set; }
        public string Description { get; set; }
    }
}
