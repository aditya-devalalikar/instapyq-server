namespace pqy_server.Shared
{
    public static class CacheKeys
    {
        public const string QuestionEnumLabels = "question-enum-labels";
        public const string ExamCandidateVersion = "exam-candidate-version";

        public static string UserPremiumStatus(int userId)
            => $"user-premium-status:{userId}";

        public static string UserSelectedExamIds(int userId)
            => $"user-selected-exam-ids:{userId}";

        public static string ExamCandidateIds(
            int userId,
            bool isPremiumUser,
            int? subjectId,
            int? topicId,
            string yearIdsKey,
            string selectedExamIdsKey,
            int version)
            => $"exam-candidate-ids:{userId}:{isPremiumUser}:{subjectId}:{topicId}:{yearIdsKey}:{selectedExamIdsKey}:v{version}";
    }
}
