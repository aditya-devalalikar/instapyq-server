using pqy_server.Initializers;

namespace pqy_server.Extensions
{
    public static class FirebaseExtensions
    {
        public static void UseFirebase(this WebApplication app)
        {
            FirebaseInitializer.Initialize();
        }
    }
}
