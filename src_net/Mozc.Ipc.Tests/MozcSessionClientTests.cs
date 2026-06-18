using Google.Protobuf;
using Mozc.Commands;
using Mozc.Ipc.Client;
using Xunit;
using ConfigProto = Mozc.Config.Config;

namespace Mozc.Ipc.Tests;

// MozcSessionClient のコマンド組み立て/応答解釈を、フェイクサーバで検証する。
// トランスポート非依存(OS非依存)の純ロジックテスト。
public class MozcSessionClientTests
{
    // commands.Input を解釈して妥当な Output を返すフェイク IPC サーバ。
    private sealed class FakeIpcClient : IIpcClient
    {
        private readonly FakeServerState _state;
        public FakeIpcClient(FakeServerState state) => _state = state;

        public byte[] Call(byte[] request, TimeSpan timeout)
        {
            var input = Input.Parser.ParseFrom(request);
            _state.LastInput = input;
            var output = new Output { Id = input.Id };

            switch (input.Type)
            {
                case Input.Types.CommandType.CreateSession:
                    output.Id = _state.AssignSessionId;
                    break;
                case Input.Types.CommandType.GetConfig:
                    output.Config = _state.StoredConfig.Clone();
                    break;
                case Input.Types.CommandType.SetConfig:
                    _state.StoredConfig = input.Config.Clone();
                    output.ErrorCode = Output.Types.ErrorCode.SessionSuccess;
                    break;
                default:
                    output.ErrorCode = Output.Types.ErrorCode.SessionSuccess;
                    break;
            }
            return output.ToByteArray();
        }

        public Task<byte[]> CallAsync(byte[] request, TimeSpan timeout, CancellationToken ct = default)
            => Task.FromResult(Call(request, timeout));

        public void Dispose() { }
    }

    private sealed class FakeServerState
    {
        public ulong AssignSessionId = 99;
        public ConfigProto StoredConfig = new() { IncognitoMode = true, VerboseLevel = 7 };
        public Input? LastInput;
    }

    private static MozcSessionClient NewClient(FakeServerState state)
        => new(() => new FakeIpcClient(state));

    [Fact]
    public void CreateSession_StoresSessionId()
    {
        var state = new FakeServerState { AssignSessionId = 4242 };
        var client = NewClient(state);

        Assert.True(client.CreateSession());
        Assert.Equal(4242ul, client.SessionId);
        Assert.Equal(Input.Types.CommandType.CreateSession, state.LastInput!.Type);
    }

    [Fact]
    public void GetConfig_ReturnsServerConfig()
    {
        var state = new FakeServerState();
        state.StoredConfig = new ConfigProto { IncognitoMode = true, VerboseLevel = 3 };
        var client = NewClient(state);

        ConfigProto config = client.GetConfig();

        Assert.True(config.IncognitoMode);
        Assert.Equal(3, config.VerboseLevel);
        Assert.Equal(Input.Types.CommandType.GetConfig, state.LastInput!.Type);
    }

    [Fact]
    public void SetConfig_SendsConfigToServer()
    {
        var state = new FakeServerState();
        var client = NewClient(state);

        var newConfig = new ConfigProto { IncognitoMode = false, VerboseLevel = 11 };
        Assert.True(client.SetConfig(newConfig));

        Assert.Equal(Input.Types.CommandType.SetConfig, state.LastInput!.Type);
        Assert.Equal(11, state.StoredConfig.VerboseLevel);
        Assert.False(state.StoredConfig.IncognitoMode);
    }

    [Fact]
    public void Preferences_AreAttachedToInput()
    {
        var state = new FakeServerState();
        var client = NewClient(state);
        client.Preferences = new ConfigProto { VerboseLevel = 5 };

        client.NoOperation();

        Assert.NotNull(state.LastInput!.Config);
        Assert.Equal(5, state.LastInput.Config.VerboseLevel);
    }

    [Fact]
    public void ClearAndMaintenanceCommands_SendCorrectTypes()
    {
        var state = new FakeServerState();
        var client = NewClient(state);

        client.ClearUserHistory();
        Assert.Equal(Input.Types.CommandType.ClearUserHistory, state.LastInput!.Type);
        client.Reload();
        Assert.Equal(Input.Types.CommandType.Reload, state.LastInput.Type);
        client.SyncData();
        Assert.Equal(Input.Types.CommandType.SyncData, state.LastInput.Type);
        Assert.True(client.NoOperation());
        Assert.Equal(Input.Types.CommandType.NoOperation, state.LastInput.Type);
    }
}
