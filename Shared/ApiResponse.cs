namespace pqy_server.Shared
{
    public class ApiResponse<T>
    {
        public ResultCode ResultCode { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
        public object? Meta { get; set; }

        public static ApiResponse<T> Success(T data, string? message = "Success", object? meta = null)
        {
            return new ApiResponse<T>
            {
                ResultCode = ResultCode.Success,
                Message = message,
                Data = data,
                Meta = meta
            };
        }

        public static ApiResponse<T> Failure(ResultCode resultCode, string? message = "", object? meta = null)
        {
            return new ApiResponse<T>
            {
                ResultCode = resultCode,
                Message = message,
                Data = default,
                Meta = meta
            };
        }

        // --- Updated Paginated method ---
        public static ApiResponse<object> Paginated<U>(List<U> items, int totalCount, int page, int pageSize, string? message = "Success")
        {
            var paginatedData = new
            {
                items,
                meta = new
                {
                    totalCount,
                    page,
                    pageSize
                }
            };

            // Return as ApiResponse<object>
            return new ApiResponse<object>
            {
                ResultCode = ResultCode.Success,
                Message = message,
                Data = paginatedData,
                Meta = null
            };
        }
    }
}
