using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;

namespace pqy_server.Initializers;

public static class FirebaseInitializer
{
    public static void Initialize()
    {
        if (FirebaseApp.DefaultInstance == null)
        {
            var keyPath = Path.Combine(AppContext.BaseDirectory, "Keys", "instapyq-firebase-adminsdk-fbsvc-2b146b9d10.json");

            FirebaseApp.Create(new AppOptions()
            {
                Credential = GoogleCredential.FromFile(keyPath)
            });
        }
    }
}
