using System.Net;

namespace RikiPath.Application.Responses
{
    public class ApiResponse<T>
    {
        // Đổi "private set" thành "protected set" để lớp con truy cập được
        public int StatusCode { get; protected set; }
        public bool IsSuccess { get; protected set; }
        public string? ErrorMessage { get; protected set; }
        public List<string>? Errors { get; protected set; }
        public T? Result { get; protected set; }

        // --- Success Handlers ---
        public static ApiResponse<T> Success(T result, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            return new ApiResponse<T>
            {
                IsSuccess = true,
                StatusCode = (int)statusCode,
                Result = result
            };
        }

        public static ApiResponse<T> Created(T result)
            => Success(result, HttpStatusCode.Created);

        // --- Error Handlers ---
        public static ApiResponse<T> Fail(string message, HttpStatusCode statusCode = HttpStatusCode.BadRequest, List<string>? errors = null)
        {
            return new ApiResponse<T>
            {
                IsSuccess = false,
                StatusCode = (int)statusCode,
                ErrorMessage = message,
                Errors = errors
            };
        }

        public static ApiResponse<T> NotFound(string message = "Resource not found")
            => Fail(message, HttpStatusCode.NotFound);

        public static ApiResponse<T> Unauthorized(string message = "Unauthorized access")
            => Fail(message, HttpStatusCode.Unauthorized);
    }

    // Lớp non-generic cho API không trả về Result
    public class ApiResponse : ApiResponse<object>
    {
        public static ApiResponse Success(HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            return new ApiResponse
            {
                IsSuccess = true,
                StatusCode = (int)statusCode
            };
        }

        public static ApiResponse Fail(string message, HttpStatusCode statusCode = HttpStatusCode.BadRequest, List<string>? errors = null)
        {
            return new ApiResponse
            {
                IsSuccess = false,
                StatusCode = (int)statusCode,
                ErrorMessage = message,
                Errors = errors
            };
        }
        public static new ApiResponse NotFound(string message = "Resource not found")
        => Fail(message, HttpStatusCode.NotFound);

        public static new ApiResponse Unauthorized(string message = "Unauthorized access")
            => Fail(message, HttpStatusCode.Unauthorized);

        public static new ApiResponse Forbidden(string message = "Forbidden")
            => Fail(message, HttpStatusCode.Forbidden);
    }
}