// Windows integration fixture: a disposable Codex-like window. Never closes real Codex.
if (args.Length != 1) return;
var control = args[0];
System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);
using var form = new System.Windows.Forms.Form { Text = "QuotaFloat startup test", Width = 500, Height = 250 };
using var timer = new System.Windows.Forms.Timer { Interval = 300 };
var previous = "";
timer.Tick += (_, _) =>
{
    string command;
    try { command = File.ReadAllText(control).Trim(); } catch (IOException) { return; }
    if (command == previous) return;
    previous = command;
    if (command == "hide") form.Hide();
    else if (command == "show") form.Show();
    else if (command == "close") form.Close();
};
timer.Start();
System.Windows.Forms.Application.Run(form);
