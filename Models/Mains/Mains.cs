using pqy_server.Models.Topics;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using YearModel = pqy_server.Models.Years.Year;
using TopicModel = pqy_server.Models.Topics.Topic;

namespace pqy_server.Models.Mains
{
    public enum PaperType
    {
        GS,
        ESSAY,
        OPTIONAL
    }

    public enum OptionalSubject
    {
        Anthropology,
        AnimalHusbandry_VetScience,
        Botany,
        Chemistry,
        CivilEngineering,
        Commerce_Accountancy,
        Economics,
        ElectricalEngineering,
        Geography,
        Geology,
        History,
        Law,
        Management,
        Mathematics,
        MechanicalEngineering,
        MedicalScience,
        Philosophy,
        Physics,
        PoliticalScience_InternationalRelations,
        Psychology,
        PublicAdministration,
        Sociology,
        Statistics,
        Zoology,
        Agriculture,
        Literature
    }

    [Table("Mains")]
    public class MainsQuestion
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int YearId { get; set; }

        [ForeignKey("YearId")]
        public YearModel Year { get; set; }

        [Required]
        public PaperType PaperType { get; set; }

        [Required]
        public int PaperNumber { get; set; } // GS: 1-4, Optional:1-2, Essay:1

        public OptionalSubject? OptionalSubject { get; set; } // only for OPTIONAL

        public string Section { get; set; } // Section A/B, Part 1, etc.

        [Required]
        public int QuestionNumber { get; set; }

        [Required]
        public string QuestionText { get; set; }

        [Required]
        public int Marks { get; set; }

        public int? TopicId { get; set; }
        public int? SubjectId { get; set; }

        [ForeignKey("TopicId")]
        public TopicModel Topic { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
