using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace ViSNET
{
    public sealed class ViSNETApiClient : MonoBehaviour
    {
        [SerializeField] private string apiBaseUrl = "";
        [SerializeField] private bool useMockWhenBaseUrlMissing = true;
        [SerializeField] private bool useMockOnNetworkFailure = false;
        [SerializeField] private int timeoutSeconds = 10;

        public string ApiBaseUrl
        {
            get => PlayerPrefs.GetString("VISNET_API_BASE_URL", apiBaseUrl).TrimEnd('/');
            set
            {
                apiBaseUrl = value ?? "";
                PlayerPrefs.SetString("VISNET_API_BASE_URL", apiBaseUrl);
            }
        }

        private void Awake()
        {
            if (!string.IsNullOrWhiteSpace(apiBaseUrl) || PlayerPrefs.HasKey("VISNET_API_BASE_URL"))
            {
                return;
            }

            TextAsset config = Resources.Load<TextAsset>("ViSNETApiConfig");
            if (config == null)
            {
                return;
            }

            ApiConfig parsed = JsonUtility.FromJson<ApiConfig>(config.text);
            apiBaseUrl = parsed?.apiBaseUrl ?? "";
        }

        public IEnumerator Login(string username, string password, Action<ApiResult<LoginResponse>> callback)
        {
            if (ShouldUseMock())
            {
                yield return MockDelay();
                callback(MockLogin(username, password));
                yield break;
            }

            string body = JsonUtility.ToJson(new LoginRequest { username = username, password = password });
            yield return SendJson<LoginResponse>("/api/login", "POST", body, result =>
            {
                if (!result.Success && result.NetworkFailure && useMockOnNetworkFailure)
                {
                    callback(MockLogin(username, password, true));
                    return;
                }

                callback(result);
            });
        }

        public IEnumerator GetProjects(Action<ApiResult<ProjectListResponse>> callback)
        {
            if (ShouldUseMock())
            {
                yield return MockDelay();
                callback(ApiResult<ProjectListResponse>.Ok(MockProjects(), true));
                yield break;
            }

            yield return SendJson<ProjectListResponse>("/api/projects", "GET", null, result =>
            {
                if (!result.Success && result.NetworkFailure && useMockOnNetworkFailure)
                {
                    callback(ApiResult<ProjectListResponse>.Ok(MockProjects(), true));
                    return;
                }

                callback(result);
            });
        }

        public IEnumerator GetFloors(int projectId, Action<ApiResult<FloorListResponse>> callback)
        {
            if (ShouldUseMock())
            {
                yield return MockDelay();
                callback(ApiResult<FloorListResponse>.Ok(MockFloors(projectId), true));
                yield break;
            }

            yield return SendJson<FloorListResponse>($"/api/projects/{projectId}/floors", "GET", null, result =>
            {
                if (!result.Success && result.NetworkFailure && useMockOnNetworkFailure)
                {
                    callback(ApiResult<FloorListResponse>.Ok(MockFloors(projectId), true));
                    return;
                }

                callback(result);
            });
        }

        private IEnumerator SendJson<T>(string path, string method, string body, Action<ApiResult<T>> callback)
        {
            using UnityWebRequest request = new UnityWebRequest(ApiBaseUrl + path, method);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = timeoutSeconds;
            request.SetRequestHeader("Accept", "application/json");

            if (!string.IsNullOrEmpty(body))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.SetRequestHeader("Content-Type", "application/json");
            }

            yield return request.SendWebRequest();

            bool networkFailure = request.result == UnityWebRequest.Result.ConnectionError ||
                                  request.result == UnityWebRequest.Result.DataProcessingError;
            if (request.result != UnityWebRequest.Result.Success)
            {
                string message = string.IsNullOrWhiteSpace(request.downloadHandler.text)
                    ? request.error
                    : ExtractErrorMessage(request.downloadHandler.text);
                callback(ApiResult<T>.Fail(message, networkFailure));
                yield break;
            }

            try
            {
                callback(ApiResult<T>.Ok(JsonUtility.FromJson<T>(request.downloadHandler.text), false));
            }
            catch (Exception ex)
            {
                callback(ApiResult<T>.Fail($"Invalid API response: {ex.Message}", true));
            }
        }

        private static string ExtractErrorMessage(string responseText)
        {
            try
            {
                ApiErrorResponse parsed = JsonUtility.FromJson<ApiErrorResponse>(responseText);
                if (!string.IsNullOrWhiteSpace(parsed?.message))
                {
                    return parsed.message;
                }

                if (!string.IsNullOrWhiteSpace(parsed?.error))
                {
                    return parsed.error;
                }
            }
            catch
            {
                // Fall through to a user-safe message below.
            }

            return responseText.Contains("credential", StringComparison.OrdinalIgnoreCase) ||
                   responseText.Contains("invalid", StringComparison.OrdinalIgnoreCase)
                ? "Invalid username or password"
                : "Request failed";
        }

        private bool ShouldUseMock()
        {
            return string.IsNullOrWhiteSpace(ApiBaseUrl) && useMockWhenBaseUrlMissing;
        }

        private static IEnumerator MockDelay()
        {
            yield return new WaitForSeconds(0.15f);
        }

        private static ApiResult<LoginResponse> MockLogin(string username, string password, bool fromFallback = false)
        {
            if (username == "testuser" && password == "123456")
            {
                return ApiResult<LoginResponse>.Ok(new LoginResponse
                {
                    success = true,
                    token = "mock_jwt_token_here",
                    user = new UserDto { id = 1, name = "Test User" }
                }, fromFallback);
            }

            return ApiResult<LoginResponse>.Fail("Invalid Credentials", false);
        }

        private static ProjectListResponse MockProjects()
        {
            return new ProjectListResponse
            {
                projects = new[]
                {
                    new ProjectDto { id = 1, name = "Project A" },
                    new ProjectDto { id = 2, name = "Project B" },
                    new ProjectDto { id = 3, name = "Project C" },
                    new ProjectDto { id = 4, name = "Project D" }
                }
            };
        }

        private static FloorListResponse MockFloors(int projectId)
        {
            string[] floors = projectId switch
            {
                1 => new[] { "Floor 1", "Floor 2", "Floor 3", "Floor 4" },
                2 => new[] { "Floor A", "Floor B" },
                3 => new[] { "Ground", "1", "2", "3", "4", "5" },
                4 => new[] { "Basement", "Ground", "Mezzanine" },
                _ => Array.Empty<string>()
            };

            return new FloorListResponse { floors = floors };
        }
    }

    public readonly struct ApiResult<T>
    {
        public readonly bool Success;
        public readonly bool NetworkFailure;
        public readonly bool FromMock;
        public readonly string Error;
        public readonly T Data;

        private ApiResult(bool success, T data, string error, bool networkFailure, bool fromMock)
        {
            Success = success;
            Data = data;
            Error = error;
            NetworkFailure = networkFailure;
            FromMock = fromMock;
        }

        public static ApiResult<T> Ok(T data, bool fromMock)
        {
            return new ApiResult<T>(true, data, "", false, fromMock);
        }

        public static ApiResult<T> Fail(string error, bool networkFailure)
        {
            return new ApiResult<T>(false, default, string.IsNullOrWhiteSpace(error) ? "Request failed" : error, networkFailure, false);
        }
    }

    [Serializable]
    internal sealed class LoginRequest
    {
        public string username;
        public string password;
    }

    [Serializable]
    internal sealed class ApiConfig
    {
        public string apiBaseUrl = "";
    }

    [Serializable]
    internal sealed class ApiErrorResponse
    {
        public string error = "";
        public string message = "";
    }

    [Serializable]
    public sealed class LoginResponse
    {
        public bool success;
        public string token;
        public UserDto user;
    }

    [Serializable]
    public sealed class UserDto
    {
        public int id;
        public string name;
    }

    [Serializable]
    public sealed class ProjectListResponse
    {
        public ProjectDto[] projects;
    }

    [Serializable]
    public sealed class ProjectDto
    {
        public int id;
        public string name;
    }

    [Serializable]
    public sealed class FloorListResponse
    {
        public string[] floors;
    }
}
