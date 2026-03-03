using System.IO.Ports;

namespace TeamsBusyLight;

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
            return true;
        }
        catch { return false; }
    }

    public bool IsOpen => _port is { IsOpen: true };

    public void SetLight(bool on)
    {
        if (_port is { IsOpen: true })
            _port.Write(on ? "1" : "0");
    }

    public void Dispose()
    {
        if (_port is { IsOpen: true })
        {
            try { _port.Write("0"); } catch { }
            _port.Close();
        }
        _port?.Dispose();
    }
}
