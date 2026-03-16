namespace pqy_server.Models.Email
{
    public class ZeptoTemplateRequest
    {
        public FromAddress from { get; set; }
        public List<ToAddress> to { get; set; }
        public string template_key { get; set; }
        public object merge_info { get; set; }

        public class FromAddress
        {
            public string address { get; set; }
            public string name { get; set; }
        }

        public class ToAddress
        {
            public EmailAddress email_address { get; set; }
        }

        public class EmailAddress
        {
            public string address { get; set; }
        }
    }
}
