using System.IO.Ports;

namespace TeamsPhobicBusyLight;

public enum LightState
{
    Off,
    Available,
    Busy
}

public class SerialService : IDisposable
{
    private SerialPort? _port;

    public string[] GetAvailablePorts() => SerialPort.GetPortNames();

    public bool Open(string portName, int baud = 9600)
    {
        try
        {
            _port = new SerialPort(portName, baud);
            _port.Open();
            SetState(LightState.Available);
            return true;
        }
        catch { return false; }
    }

    public bool IsOpen => _port is { IsOpen: true };

    public void SetState(LightState state)
    {
        if (_port is not { IsOpen: true }) return;
        var cmd = state switch
        {
            LightState.Busy => "1",
            LightState.Available => "0",
            _ => "X"
        };
        _port.Write(cmd);
    }

    public void SetLight(bool on) => SetState(on ? LightState.Busy : LightState.Available);

    public void Dispose()
    {
        if (_port is { IsOpen: true })
        {
            try { _port.Write("X"); } catch { }
            _port.Close();
        }
        _port?.Dispose();
    }
}
