namespace TeamsPhobicBusyLight;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, "TeamsPhobicBusyLight_SingleInstance", out bool isNew);
        if (!isNew)
        {
            MessageBox.Show("Teams Phobic Busy Light is already running.", "Already Running",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp());
    }
}
