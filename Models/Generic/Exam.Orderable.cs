using pqy_server.Models.Generic;

namespace pqy_server.Models.Exams
{
    public partial class Exam : IOrderable
    {
        public int Id => ExamId;

        public int Order
        {
            get => ExamOrder;
            set => ExamOrder = value;
        }
    }
}
