﻿using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;

namespace CozeApiClient
{
    /// <summary>
    /// Generic class to handle workflow operations.
    /// </summary>
    public class CozeWorkflow<TParameters, TResponse>
    {
        private readonly HttpClient _httpClient;
        private readonly string _workflowId;
        private readonly string _appId;

        /// <summary>
        /// Initializes a new instance with dependency injection support.
        /// </summary>
        public CozeWorkflow(HttpClient httpClient, string workflowId, string appId)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _workflowId = workflowId;
            _appId = appId;
        }

        /// <summary>
        /// Convenience constructor that creates its own HttpClient.
        /// </summary>
        public CozeWorkflow(string baseUrl, string authToken, string workflowId, string appId)
            : this(CreateAndConfigureHttpClient(baseUrl, authToken), workflowId, appId)
        {
        }

        private static HttpClient CreateAndConfigureHttpClient(string baseUrl, string authToken)
        {
            var client = new HttpClient
            {
                BaseAddress = new Uri(baseUrl)
            };
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {authToken}");
            return client;
        }

        /// <summary>
        /// Runs a workflow asynchronously.
        /// </summary>
        public async Task<RunWorkflowResponse<TResponse>> RunWorkflowAsync(TParameters parameters)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }
            var request = new WorkflowRequest<TParameters>
            {
                WorkflowId = _workflowId,
                //AppId = _appId,
                Parameters = parameters
            };
            return await PostAsync<WorkflowRequest<TParameters>, RunWorkflowResponse<TResponse>>("/v1/workflow/run", request);
        }

        /// <summary>
        /// Runs a workflow with streaming asynchronously and processes events.
        /// </summary>
        public async IAsyncEnumerable<WorkflowEvent> RunWorkflowStreamingAsync(TParameters parameters)
        {
            var request = new WorkflowRequest<TParameters>
            {
                WorkflowId = _workflowId,
                AppId = _appId,
                Parameters = parameters
            };

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/v1/workflow/stream_run")
            {
                Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json")
            };

            var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using var responseStream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(responseStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);

            char[] buffer = new char[1];
            var eventStringBuilder = new StringBuilder();
            while (await reader.ReadAsync(buffer, 0, 1) > 0)
            {
                char c = buffer[0];
                eventStringBuilder.Append(c);

                if (eventStringBuilder.Length >= 2 && eventStringBuilder.ToString().EndsWith("\n\n"))
                {
                    var eventString = eventStringBuilder.ToString();
                    var workflowEvent = WorkflowEvent.Parse(eventString);
                    yield return workflowEvent;
                    eventStringBuilder.Clear();
                }
            }

            if (eventStringBuilder.Length > 0)
            {
                var eventString = eventStringBuilder.ToString();
                var workflowEvent = WorkflowEvent.Parse(eventString);
                yield return workflowEvent;
            }
        }

        /// <summary>
        /// Resumes a workflow asynchronously and processes events.
        /// </summary>
        public async IAsyncEnumerable<WorkflowEvent> ResumeWorkflowAsync(string eventId, string resumeData, int interruptType)
        {
            var request = new WorkflowResumeRequest
            {
                EventId = eventId,
                WorkflowId = _workflowId,
                ResumeData = resumeData,
                InterruptType = interruptType
            };

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/v1/workflow/stream_resume")
            {
                Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json")
            };

            var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using var responseStream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(responseStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);

            char[] buffer = new char[1];
            var eventStringBuilder = new StringBuilder();

            while (await reader.ReadAsync(buffer, 0, 1) > 0)
            {
                char c = buffer[0];
                eventStringBuilder.Append(c);

                if (eventStringBuilder.Length >= 2 && eventStringBuilder.ToString().EndsWith("\n\n"))
                {
                    var eventString = eventStringBuilder.ToString();
                    var workflowEvent = WorkflowEvent.Parse(eventString);
                    yield return workflowEvent;
                    eventStringBuilder.Clear();
                }
            }

            if (eventStringBuilder.Length > 0)
            {
                var eventString = eventStringBuilder.ToString();
                var workflowEvent = WorkflowEvent.Parse(eventString);
                yield return workflowEvent;
            }
        }

        /// <summary>
        /// Posts a request and gets a response.
        /// </summary>
        private async Task<TResponse> PostAsync<TRequest, TResponse>(string url, TRequest payload)
        {
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<TResponse>(jsonResponse);
            }

            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Error: {response.StatusCode}, Details: {error}");
        }
    }

    /// <summary>
    /// Class representing a workflow request.
    /// </summary>
    public class WorkflowRequest<TParameters>
    {
        [JsonPropertyName("workflow_id")]
        public string WorkflowId { get; set; }

        [JsonPropertyName("app_id")]
        public string AppId { get; set; }

        [JsonPropertyName("parameters")]
        public TParameters Parameters { get; set; }
    }

    /// <summary>
    /// Class representing a workflow resume request.
    /// </summary>
    public class WorkflowResumeRequest
    {
        [JsonPropertyName("event_id")]
        public string EventId { get; set; }

        [JsonPropertyName("workflow_id")]
        public string WorkflowId { get; set; }

        [JsonPropertyName("resume_data")]
        public string ResumeData { get; set; }

        [JsonPropertyName("interrupt_type")]
        public int InterruptType { get; set; }
    }

    /// <summary>
    /// Class representing the response from running a workflow.
    /// </summary>
    public class RunWorkflowResponse<TWorkflowOutput>
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("cost")]
        public string Cost { get; set; }

        private string _data;

        [JsonPropertyName("data")]
        public string Data
        {
            get => _data;
            set
            {
                _data = value;
                ParsedData = JsonSerializer.Deserialize<TWorkflowOutput>(_data);
            }
        }

        [JsonIgnore]
        public TWorkflowOutput ParsedData { get; private set; }

        [JsonPropertyName("debug_url")]
        public string DebugUrl { get; set; }

        [JsonPropertyName("msg")]
        public string Msg { get; set; }

        [JsonPropertyName("token")]
        public int Token { get; set; }
    }

    // 定义事件类型枚举
    public enum WorkflowEventType
    {
        Message,
        Interrupt,
        Error,
        Unknown
    }

    // 修改 WorkflowEvent 类，增加 EventType 属性和对应的数据属性
    public class WorkflowEvent
    {
        public int Id { get; set; }
        public WorkflowEventType EventType { get; set; }
        public object Data { get; set; }

        public static WorkflowEvent Parse(string eventString)
        {
            var workflowEvent = new WorkflowEvent();
            var lines = eventString.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                if (line.StartsWith("id: "))
                {
                    if (int.TryParse(line.Substring(4).Trim(), out int id))
                    {
                        workflowEvent.Id = id;
                    }
                }
                else if (line.StartsWith("event: "))
                {
                    var eventTypeStr = line.Substring(7).Trim();
                    workflowEvent.EventType = eventTypeStr switch
                    {
                        "Message" => WorkflowEventType.Message,
                        "Interrupt" => WorkflowEventType.Interrupt,
                        "Error" => WorkflowEventType.Error,
                        _ => WorkflowEventType.Unknown
                    };
                }
                else if (line.StartsWith("data: "))
                {
                    var dataContent = line.Substring(6).Trim();

                    switch (workflowEvent.EventType)
                    {
                        case WorkflowEventType.Message:
                            workflowEvent.Data = JsonSerializer.Deserialize<MessageEventData>(dataContent);
                            break;
                        case WorkflowEventType.Interrupt:
                            workflowEvent.Data = JsonSerializer.Deserialize<InterruptEventData>(dataContent);
                            break;
                        case WorkflowEventType.Error:
                            workflowEvent.Data = JsonSerializer.Deserialize<ErrorEventData>(dataContent);
                            break;
                        default:
                            workflowEvent.Data = dataContent;
                            break;
                    }
                }
            }

            return workflowEvent;
        }
    }

    // 定义 MessageEventData 类
    public class MessageEventData
    {
        [JsonPropertyName("content")]
        public string Content { get; set; }

        [JsonPropertyName("node_title")]
        public string NodeTitle { get; set; }

        [JsonPropertyName("node_seq_id")]
        public string NodeSeqId { get; set; }

        [JsonPropertyName("node_is_finish")]
        public bool NodeIsFinish { get; set; }

        [JsonPropertyName("ext")]
        public Dictionary<string, string> Ext { get; set; }

        [JsonPropertyName("cost")]
        public string Cost { get; set; }
    }

    // 定义 InterruptEventData 类
    public class InterruptEventData
    {
        [JsonPropertyName("interrupt_data")]
        public InterruptData InterruptData { get; set; }

        [JsonPropertyName("node_title")]
        public string NodeTitle { get; set; }
    }

    public class InterruptData
    {
        [JsonPropertyName("event_id")]
        public string EventId { get; set; }

        [JsonPropertyName("type")]
        public int Type { get; set; }
    }

    // 定义 ErrorEventData 类
    public class ErrorEventData
    {
        [JsonPropertyName("error_code")]
        public int ErrorCode { get; set; }

        [JsonPropertyName("error_message")]
        public string ErrorMessage { get; set; }
    }
    public class Dta 
    {
        [JsonPropertyName("input")]
        public string input { get; set; }
    }
    public class DtaOutPut
    {
        [JsonPropertyName("output")]
        public string output { get; set; }
    }
}