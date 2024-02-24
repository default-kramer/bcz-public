using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BCZ.Core;
using BCZ.Core.ReplayModel;

/// <summary>
/// Uploads the completed game to a remote server
/// </summary>
class GameUploader : IReplayCollector
{
    private readonly IServerConnection server;
    private readonly StringWriter stringWriter;

    /// <summary>
    /// We are decorating this instance, so be sure to pass all calls through correctly.
    /// </summary>
    private readonly ReplayWriter replayWriter;

    public GameUploader(IServerConnection serverConnection, SeededSettings settings)
    {
        this.server = serverConnection;
        this.stringWriter = new StringWriter();
        this.replayWriter = ReplayWriter.Begin(stringWriter, settings, shouldDispose: false);
    }

    public void AfterCommand(Moment moment, State state)
    {
        replayWriter.AfterCommand(moment, state);
    }

    public void Collect(Stamped<Command> command)
    {
        replayWriter.Collect(command);
    }

    public void OnGameEnded()
    {
        replayWriter.OnGameEnded();
        stringWriter.Flush();
        var replayContent = stringWriter.GetStringBuilder().ToString();
        if (server.IsOnline)
        {
            server.Execute(new UploadGameRequest(replayContent));
        }

        stringWriter.Close();
        stringWriter.Dispose();
    }

    sealed class UploadGameRequest : Request
    {
        private readonly string replayFile;

        public UploadGameRequest(string replayFile)
        {
            this.replayFile = replayFile;
        }

        public override double TimeoutSeconds => 20;
        public override string Path => "/api/upload-game/v1";
        public override Godot.HTTPClient.Method Method => Godot.HTTPClient.Method.Post;
        public override string Body() => replayFile;
    }
}
