using System.ComponentModel;

namespace pqy_server.Enums
{
    public static class QuestionEnums
    {
        public enum DifficultyLevel
        {
            Easy,
            Medium,
            Difficult
        }

        public enum Nature
        {
            [Description("Fundamental Conventional and Conceptual Question")]
            FundamentalConventionalConceptual,

            [Description("Fundamental Applied Question")]
            FundamentalApplied,

            [Description("Current Affairs Question")]
            CurrentAffair,

            [Description("Current Affairs Applied")]
            CurrentAffairApplied,

            [Description("Fundamental + Current Affairs")]
            FundamentalAndCurrentAffair,

            [Description("Unconventional Question")]
            Unconventional
        }

        public enum SourceType
        {
            [Description("Essential Material (Basic Books, etc.)")]
            EssentialMaterial,

            [Description("Reference Material")]
            ReferenceMaterial,

            [Description("Essential News / Current Affairs")]
            EssentialNews,

            [Description("Random Read (Random Website)")]
            RandomRead
        }
    }
}
