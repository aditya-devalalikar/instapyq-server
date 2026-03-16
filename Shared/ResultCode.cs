namespace pqy_server.Shared
{
    public enum ResultCode
    {
        // Success Codes
        Success = 0,                // Operation completed successfully

        // Client Errors (4xx equivalent)
        BadRequest = 100,           // Invalid or malformed request
        Unauthorized = 101,         // Authentication failed or missing token
        Forbidden = 102,             // Authenticated but not authorized
        NotFound = 103,             // Resource not found
        Conflict = 104,             // Conflict, e.g., duplicate resource
        TooManyRequests = 105,      // Rate limit exceeded

        // Validation Errors
        ValidationError = 110,      // One or more validation rules failed
        NotEnoughQuestions = 120,

        // Server Errors (5xx equivalent)
        InternalServerError = 200,  // Generic server error
        DatabaseError = 201,        // Database operation failed
        ServiceUnavailable = 202,   // Downstream service unavailable

        // Token / Authentication related Errors
        TokenExpired = 300,         // JWT token expired
        TokenInvalid = 301,         // JWT token invalid or malformed
        TokenRevoked = 302,         // JWT token revoked

        // Refresh Token Errors
        RefreshTokenExpired = 400,  // Refresh token expired
        RefreshTokenInvalid = 401,  // Refresh token invalid or unrecognized
        DeviceMismatch = 402,       // Refresh attempted from a device other than the one that logged in

        // Custom / Application-specific Errors
        UserAlreadyExists = 500,
        PasswordTooWeak = 501,
        EmailNotVerified = 502,

        // ✅ Topic-specific
        TopicHasQuestions = 510,

        // Subscription-specific
        InvalidSignature = 601,
        WebhookError = 602,

        // Order-specific
        OrderError = 700,           // Razorpay order creation/verification failed
        InvalidPlan = 701,          // Plan ID sent by frontend is invalid
        OrderAlreadyPaid = 702,     // Order was already marked as paid
        OrderNotOwned = 703,        // Authenticated user does not own this order
        DuplicatePendingOrder = 704,// User already has a pending (Created) order
        ActivePlanExists = 705      // User already has an active premium plan
    }
}
