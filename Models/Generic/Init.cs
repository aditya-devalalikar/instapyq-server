using pqy_server.Models.Exams;
using pqy_server.Models.Notifications;
using pqy_server.Models.Order;
using pqy_server.Models.Quotes;
using pqy_server.Models.Subjects;
using TopicEntity = pqy_server.Models.Topics.Topic;
using pqy_server.Models.User;
using pqy_server.Models.Year;
using pqy_server.Models.Years;

namespace pqy_server.Models.Generic
{
    public class Init
    {
        public MyProfileDto Profile { get; set; }
        public IEnumerable<Exam> Exams { get; set; }
        public IEnumerable<YearDto> Years { get; set; }
        public IEnumerable<Subject> Subjects { get; set; }
        public IEnumerable<TopicEntity> Topics { get; set; }
        public IEnumerable<Notification> Notifications { get; set; }
        public IEnumerable<Quote> Quotes { get; set; }
        public MyPlanDto MyPlan { get; set; }
    }
}
