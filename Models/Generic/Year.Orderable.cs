using pqy_server.Models.Generic;

namespace pqy_server.Models.Years
{
    public partial class Year : IOrderable
    {
        public int Id => YearId;

        public int Order
        {
            get => YearOrder;
            set => YearOrder = value;
        }
    }
}
