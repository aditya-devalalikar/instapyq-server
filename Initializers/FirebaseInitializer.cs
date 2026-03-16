using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;

namespace pqy_server.Initializers;

public static class FirebaseInitializer
{
    public static void Initialize()
    {
        if (FirebaseApp.DefaultInstance == null)
        {
            var credentialJson = Environment.GetEnvironmentVariable("FIREBASE_ADMINSDK_JSON")
                ?? throw new InvalidOperationException("FIREBASE_ADMINSDK_JSON environment variable is not set.");

            FirebaseApp.Create(new AppOptions()
            {
                Credential = GoogleCredential.FromJson(credentialJson)
            });
        }
    }
}
