using System.Diagnostics;

namespace Ds3ViGEmBridge;

public sealed class AboutForm : Form
{
    private const string Repo = "https://github.com/Cybertiron/ps3_bt_rumble_final_fix";
    private const string Coffee = "https://www.buymeacoffee.com/cybertiron";

    public AboutForm()
    {
        Text = "About";
        Width = 460; Height = 320; StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog; MinimizeBox = false; MaximizeBox = false;

        var close = new Button { Text = "Close", Dock = DockStyle.Bottom, Height = 32 };
        close.Click += (_, _) => Close();
        Controls.Add(close);

        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(16),
        };

        panel.Controls.Add(new Label { AutoSize = true, Text = "DS3 ↔ ViGEm bridge + rumble tester", Font = new Font("Segoe UI", 11f, FontStyle.Bold) });
        panel.Controls.Add(new Label { AutoSize = true, Margin = new Padding(3, 4, 3, 8), Text = "Part of the ps3_bt_rumble_final_fix project.\nA prototype ViGEmBus userland bridge + rumble tester." });

        var repo = new LinkLabel { AutoSize = true, Text = "Project on GitHub" };
        repo.LinkClicked += (_, _) => Open(Repo);
        panel.Controls.Add(repo);

        panel.Controls.Add(new Label { AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(3, 4, 3, 12), Text = "Original driver © Nefarius Software Solutions." });

        panel.Controls.Add(new Label { AutoSize = true, Text = "If this helped, you can support me:" });
        var coffee = new Button
        {
            Text = "☕  Buy me a coffee",
            AutoSize = true,
            BackColor = Color.FromArgb(0xFF, 0xDD, 0x00),
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(3, 6, 3, 6),
        };
        coffee.Click += (_, _) => Open(Coffee);
        panel.Controls.Add(coffee);

        Controls.Add(panel);
    }

    private static void Open(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }
}
