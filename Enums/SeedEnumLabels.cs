using pqy_server.Data;
using pqy_server.Models;

public static class SeedEnumLabels
{
    public static void Seed(AppDbContext context)
    {
        if (!context.EnumLabels.Any())
        {
            context.EnumLabels.AddRange(new List<EnumLabel>
            {
                new EnumLabel { EnumType = "Nature", EnumName = "FundamentalApplied", DisplayLabel = "Fundamental Applied" },
                new EnumLabel { EnumType = "Nature", EnumName = "FundamentalConventionalConceptual", DisplayLabel = "Fundamental Conventional Conceptual" },
                new EnumLabel { EnumType = "Nature", EnumName = "CurrentAffair", DisplayLabel = "Current Affair" },
                new EnumLabel { EnumType = "Nature", EnumName = "CurrentAffairApplied", DisplayLabel = "Current Affair Applied" },
                new EnumLabel { EnumType = "Nature", EnumName = "FundamentalAndCurrentAffair", DisplayLabel = "Fundamental and Current Affair" },
                new EnumLabel { EnumType = "Nature", EnumName = "Unconventional", DisplayLabel = "Unconventional Question" },

                new EnumLabel { EnumType = "SourceType", EnumName = "EssentialMaterial", DisplayLabel = "Essential Material" },
                new EnumLabel { EnumType = "SourceType", EnumName = "ReferenceMaterial", DisplayLabel = "Reference Material" },
                new EnumLabel { EnumType = "SourceType", EnumName = "EssentialNews", DisplayLabel = "Essential News" },
                new EnumLabel { EnumType = "SourceType", EnumName = "RandomRead", DisplayLabel = "Random Read" },

                new EnumLabel { EnumType = "DifficultyLevel", EnumName = "Easy", DisplayLabel = "Easy" },
                new EnumLabel { EnumType = "DifficultyLevel", EnumName = "Medium", DisplayLabel = "Medium" },
                new EnumLabel { EnumType = "DifficultyLevel", EnumName = "Difficult", DisplayLabel = "Difficult" }
            });

            context.SaveChanges();
        }
    }
}
