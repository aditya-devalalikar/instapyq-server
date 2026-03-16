using pqy_server.Models.Generic;

namespace pqy_server.Models.Topics
{
    public partial class Topic : IOrderable
    {
        public int Id => TopicId;

        public int Order
        {
            get => TopicOrder;
            set => TopicOrder = value;
        }
    }
}
