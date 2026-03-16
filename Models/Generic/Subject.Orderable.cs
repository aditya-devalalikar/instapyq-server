using pqy_server.Models.Generic;

namespace pqy_server.Models.Subjects
{
    public partial class Subject : IOrderable
    {
        public int Id => SubjectId;

        public int Order
        {
            get => SubjectOrder;
            set => SubjectOrder = value;
        }
    }
}
