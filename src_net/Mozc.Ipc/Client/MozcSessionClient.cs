using Google.Protobuf;
using Mozc.Commands;
using ConfigProto = Mozc.Config.Config;

namespace Mozc.Ipc.Client;

// C++ src/client/client.cc 相当(GUI が使う範囲)。
// commands.Input を組み立て→IIpcClient で IPC→commands.Output を parse。
// トランスポートは Func<IIpcClient> で注入(connect-per-call の C++ 挙動に一致)。
public sealed class MozcSessionClient
{
    private readonly Func<IIpcClient> _ipcClientFactory;
    private readonly TimeSpan _timeout;

    private ulong _sessionId;

    public MozcSessionClient(Func<IIpcClient> ipcClientFactory, TimeSpan? timeout = null)
    {
        _ipcClientFactory = ipcClientFactory;
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
    }

    public ulong SessionId => _sessionId;

    // client.cc: preferences_ (SET_CONFIG なしで毎回 Input に載せる設定)。
    public ConfigProto? Preferences { get; set; }

    // client.cc: Call(Input, Output)。serialize→IPC→parse。
    public Output Call(Input input)
    {
        byte[] request = input.ToByteArray();
        using IIpcClient client = _ipcClientFactory();
        byte[] response = client.Call(request, _timeout);
        return Output.Parser.ParseFrom(response);
    }

    // client.cc: InitInput。id と(あれば)preferences を載せる。
    private void InitInput(Input input)
    {
        input.Id = _sessionId;
        if (Preferences is not null)
        {
            input.Config = Preferences.Clone();
        }
    }

    // client.cc: CreateSession。id_=0 にして CREATE_SESSION→output.id() を保持。
    public bool CreateSession()
    {
        _sessionId = 0;
        var input = new Input { Type = Input.Types.CommandType.CreateSession };
        Output output = Call(input);
        _sessionId = output.Id;
        return _sessionId != 0;
    }

    // client.cc: GetConfig。InitInput→GET_CONFIG→output.config()。
    public ConfigProto GetConfig()
    {
        var input = new Input { Type = Input.Types.CommandType.GetConfig };
        InitInput(input);
        Output output = Call(input);
        return output.Config ?? new ConfigProto();
    }

    // client.cc: SetConfig。InitInput 後に config を上書きして SET_CONFIG。
    public bool SetConfig(ConfigProto config)
    {
        var input = new Input { Type = Input.Types.CommandType.SetConfig };
        InitInput(input);
        input.Config = config;
        Output output = Call(input);
        return output.ErrorCode != Output.Types.ErrorCode.SessionFailure;
    }

    // client.cc: CallCommand(type)。InitInput→type→Call。
    public Output CallCommand(Input.Types.CommandType type)
    {
        var input = new Input { Type = type };
        InitInput(input);
        return Call(input);
    }

    public bool ClearUserHistory() { CallCommand(Input.Types.CommandType.ClearUserHistory); return true; }
    public bool ClearUserPrediction() { CallCommand(Input.Types.CommandType.ClearUserPrediction); return true; }
    public bool ClearUnusedUserPrediction() { CallCommand(Input.Types.CommandType.ClearUnusedUserPrediction); return true; }
    public bool Reload() { CallCommand(Input.Types.CommandType.Reload); return true; }
    public bool SyncData() { CallCommand(Input.Types.CommandType.SyncData); return true; }

    // 疎通確認(NO_OPERATION)。
    public bool NoOperation()
    {
        Output output = CallCommand(Input.Types.CommandType.NoOperation);
        return output is not null;
    }
}
