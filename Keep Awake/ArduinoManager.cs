using System.IO;
using System.IO.Ports;
using Microsoft.UI.Dispatching;

namespace Keep_Awake;

public sealed class ArduinoManager : IDisposable
{
    private SerialPort?              _port;
    private Thread?                  _readThread;
    private CancellationTokenSource? _cts;
    private readonly DispatcherQueue? _queue;
    private readonly object          _sendLock = new();

    public event EventHandler<string>? LineReceived;
    public event EventHandler<bool>?   ConnectionChanged;

    public bool    IsConnected => _port?.IsOpen == true;
    public string? PortName    { get; private set; }

    public ArduinoManager()
    {
        _queue = DispatcherQueue.GetForCurrentThread();
    }

    public async Task<bool> FindAndConnectAsync(
        IProgress<string>? progress = null,
        CancellationToken  ct       = default)
    {
        string[] ports = SerialPort.GetPortNames();

        if (ports.Length == 0)
        {
            progress?.Report("No COM ports found");
            return false;
        }

        foreach (string portName in ports)
        {
            if (ct.IsCancellationRequested) break;
            progress?.Report($"Trying {portName}…");

            if (await TryConnectAsync(portName, ct))
            {
                progress?.Report(portName);
                return true;
            }
        }

        return false;
    }

    private async Task<bool> TryConnectAsync(string portName, CancellationToken ct)
    {
        SerialPort? port = null;
        try
        {
            port = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One)
            {
                ReadTimeout  = 2500,
                WriteTimeout = 1000,
                DtrEnable    = true,
                RtsEnable    = false,
                NewLine      = "\n"
            };

            port.Open();
            port.DiscardInBuffer();
            port.DiscardOutBuffer();

            await Task.Delay(400, ct);
            port.DiscardInBuffer();

            port.WriteLine("isArduinoKeyboard");

            string response = await Task.Run(() =>
            {
                try  { return port.ReadLine().Trim(); }
                catch { return ""; }
            }, ct);

            if (response == "true")
            {
                port.ReadTimeout = 600;
                _port    = port;
                PortName = portName;
                StartReadThread();
                Raise(ConnectionChanged, true);
                return true;
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { }

        SafeClose(port);
        return false;
    }

    private void StartReadThread()
    {
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _readThread = new Thread(() => ReadLoop(token))
        {
            IsBackground = true,
            Name         = "ArduinoReadThread"
        };
        _readThread.Start();
    }

    private void ReadLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_port == null || !_port.IsOpen) break;

                string line = _port.ReadLine().Trim();
                if (line.Length > 0)
                    Raise(LineReceived, line);
            }
            catch (TimeoutException)   { continue; }
            catch (InvalidOperationException) { break; }
            catch (IOException)        { break; }
            catch                      { break; }
        }

        SafeClose(_port);
        _port    = null;
        PortName = null;
        Raise(ConnectionChanged, false);
    }

    public bool Send(string command)
    {
        lock (_sendLock)
        {
            try
            {
                if (_port?.IsOpen == true)
                {
                    _port.WriteLine(command);
                    return true;
                }
            }
            catch { }
        }
        return false;
    }

    public void Disconnect()
    {
        _cts?.Cancel();
        SafeClose(_port);
        _port    = null;
        PortName = null;
    }

    public void Dispose() => Disconnect();

    private static void SafeClose(SerialPort? port)
    {
        if (port == null) return;
        try { port.Close(); }   catch { }
        try { port.Dispose(); } catch { }
    }

    private void Raise<T>(EventHandler<T>? handler, T arg)
    {
        if (handler == null) return;
        if (_queue != null)
            _queue.TryEnqueue(() => handler.Invoke(this, arg));
        else
            handler.Invoke(this, arg);
    }
}
