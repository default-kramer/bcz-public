using System;
using System.Collections.Generic;
using System.Linq;
using BCZ.Core;
using Godot;

abstract class Request
{
    public abstract string Path { get; }
    public abstract Godot.HTTPClient.Method Method { get; }
    public virtual string Body() => "";
    public abstract double TimeoutSeconds { get; }
    public virtual void OnRequestCompleted(long result, long responseCode, string[] headers, byte[] body) { }
    public virtual void OnError(Error error) { }
}

interface IServerConnection
{
    void Execute(Request request);
}

/// <summary>
/// Relies on the player token cookie... Not sure how PC version will work yet.
/// Note that we inherit Godot.Node here because HTTPRequest is also a node that needs a parent.
/// </summary>
class BrowserBasedServerConnection : Godot.Node, IServerConnection
{
    private readonly string baseUrl;

    public BrowserBasedServerConnection(string baseUrl)
    {
        this.baseUrl = baseUrl;
    }

    public void Execute(Request request)
    {
        RequestNode.Execute(this, request);
    }

    /// <summary>
    /// From https://docs.godotengine.org/en/3.5/tutorials/networking/http_request_class.html
    ///   Keep in mind that you have to wait for a request to finish before sending another one.
    ///   Making multiple request at once requires you to have one node per request.
    ///   A common strategy is to create and delete HTTPRequest nodes at runtime as necessary.
    /// Sounds good to me. We will create one instance of this class per HTTP request.
    /// </summary>
    class RequestNode : Godot.Node
    {
        private readonly BrowserBasedServerConnection parent;
        private readonly HTTPRequest http;
        private readonly Request request;
        private readonly string url;

        private RequestNode(BrowserBasedServerConnection parent, Request request)
        {
            this.parent = parent;
            this.http = new HTTPRequest();
            this.request = request;
            this.url = parent.baseUrl + request.Path;

            parent.AddChild(this);
            this.AddChild(http);
            http.Connect("request_completed", this, nameof(OnRequestCompleted));
        }

        public static void Execute(BrowserBasedServerConnection parent, Request request)
        {
            var node = new RequestNode(parent, request);
            node.http.Timeout = request.TimeoutSeconds;
            var error = node.http.Request(node.url, method: request.Method, requestData: request.Body());
            if (error != Error.Ok)
            {
                GD.PushError($"{request.Method} {node.url} | fail | {error}");
                request.OnError(error);
                node.Cleanup();
            }
        }

        private void OnRequestCompleted(long result, long responseCode, string[] headers, byte[] body)
        {
            GD.Print($"{request.Method} {url} | {responseCode} | {result}");
            request.OnRequestCompleted(result, responseCode, headers, body);
            Cleanup();
        }

        private void Cleanup()
        {
            // I'm not sure if this is correct or even necessary...
            this.QueueFree();
        }
    }
}
