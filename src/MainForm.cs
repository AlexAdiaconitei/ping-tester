using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace PingTester;

internal sealed class MainForm : Form
{
    private const string HtmlResourceName = "PingTester.Resources.app.html";
    private const string IconResourceName = "PingTester.Resources.app-icon.ico";
    private readonly StorageService _storage;
    private readonly PowerShellRunner _runner;
    private readonly WebView2 _webView;
    private AppSettings _settings;
    private bool _allowClose;
    private bool _closePending;
    private bool _webReady;

    public MainForm()
    {
        _storage = new StorageService();
        _settings = _storage.LoadSettings();
        _runner = new PowerShellRunner(_storage);
        _runner.Started += parameters => Send(new { type = "runStarted", parameters });
        _runner.RecordsAdded += records => Send(new { type = "runRecords", records });
        _runner.Finished += outcome => Send(new
        {
            type = "runFinished",
            outcome,
            history = _storage.GetHistory()
        });

        Text = "Ping Tester";
        Icon = LoadEmbeddedIcon(IconResourceName);
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1440, 880);
        MinimumSize = new Size(1040, 680);
        BackColor = Color.FromArgb(8, 15, 24);

        _webView = new WebView2 { Dock = DockStyle.Fill };
        Controls.Add(_webView);
        Shown += InitializeWebViewAsync;
        FormClosing += OnFormClosingAsync;
        FormClosed += (_, _) => _runner.Dispose();
    }

    private async void InitializeWebViewAsync(object? sender, EventArgs e)
    {
        try
        {
            var browserArguments = Environment.GetEnvironmentVariable("PINGTESTER_WEBVIEW_ARGS");
            var environmentOptions = string.IsNullOrWhiteSpace(browserArguments)
                ? null
                : new CoreWebView2EnvironmentOptions(additionalBrowserArguments: browserArguments);
            var environment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: _storage.WebViewDataDirectory,
                options: environmentOptions);
            await _webView.EnsureCoreWebView2Async(environment);
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _webView.CoreWebView2.Settings.IsZoomControlEnabled = true;
            _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceivedAsync;
            _webView.CoreWebView2.NavigateToString(ReadEmbeddedText(HtmlResourceName));
        }
        catch (Exception exception)
        {
            var spanish = _settings.Language == "es";
            MessageBox.Show(
                this,
                spanish
                    ? $"No se pudo iniciar la interfaz. Comprueba que Microsoft Edge WebView2 está instalado.\n\n{exception.Message}"
                    : $"The interface could not start. Check that Microsoft Edge WebView2 is installed.\n\n{exception.Message}",
                "Ping Tester",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            _allowClose = true;
            Close();
        }
    }

    private async void OnWebMessageReceivedAsync(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var document = JsonDocument.Parse(e.WebMessageAsJson);
            var root = document.RootElement;
            var action = root.GetProperty("action").GetString() ?? "";
            var payload = root.TryGetProperty("payload", out var payloadElement)
                ? payloadElement
                : default;

            switch (action)
            {
                case "ready":
                    _webReady = true;
                    SendBootstrap();
                    break;

                case "start":
                    StartRun(payload);
                    break;

                case "stop":
                    Send(new { type = "runStopping" });
                    await _runner.StopAsync();
                    break;

                case "loadHistory":
                    EnsureIdle();
                    var loadId = RequiredString(payload, "id");
                    Send(new
                    {
                        type = "dataset",
                        source = "history",
                        id = loadId,
                        records = _storage.LoadRun(loadId)
                    });
                    break;

                case "deleteHistory":
                    EnsureIdle();
                    _storage.DeleteRun(RequiredString(payload, "id"));
                    Send(new { type = "history", history = _storage.GetHistory() });
                    break;

                case "openResults":
                    _storage.OpenResultsDirectory();
                    break;

                case "openExternal":
                    var destination = RequiredString(payload, "destination");
                    var url = destination switch
                    {
                        "repository" => "https://github.com/AlexAdiaconitei/ping-tester",
                        "koFi" => "https://ko-fi.com/K3Q5236GOO",
                        _ => throw new ArgumentException("Unknown external link destination.")
                    };
                    Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                    break;

                case "setLanguage":
                    _settings.Language = RequiredString(payload, "language") == "es" ? "es" : "en";
                    _storage.SaveSettings(_settings);
                    break;

                default:
                    SendError("unknownAction", null);
                    break;
            }
        }
        catch (Exception exception)
        {
            SendError("operationFailed", exception.Message);
        }
    }

    private void StartRun(JsonElement payload)
    {
        EnsureIdle();
        var parameters = payload.Deserialize<RunParameters>(JsonDefaults.Compact)
            ?? throw new ArgumentException("Run parameters are missing.");
        _settings.DurationMinutes = parameters.DurationMinutes;
        _settings.IntervalSeconds = parameters.IntervalSeconds;
        _settings.Targets = parameters.Targets;
        _storage.SaveSettings(_settings);
        _runner.Start(parameters);
    }

    private void SendBootstrap()
    {
        Send(new
        {
            type = "bootstrap",
            settings = _settings,
            history = _storage.GetHistory(),
            resultsDirectory = _storage.ResultsDirectory
        });
    }

    private void Send(object message)
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(() => Send(message));
            return;
        }

        if (!_webReady || _webView.CoreWebView2 is null)
        {
            return;
        }

        _webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(message, JsonDefaults.Compact));
    }

    private void SendError(string code, string? detail) => Send(new { type = "error", code, detail });

    private void EnsureIdle()
    {
        if (_runner.IsRunning)
        {
            throw new InvalidOperationException("Historical results cannot be changed while a test is running.");
        }
    }

    private static string RequiredString(JsonElement payload, string property)
    {
        if (payload.ValueKind != JsonValueKind.Object
            || !payload.TryGetProperty(property, out var element)
            || string.IsNullOrWhiteSpace(element.GetString()))
        {
            throw new ArgumentException($"The '{property}' value is required.");
        }

        return element.GetString()!;
    }

    private async void OnFormClosingAsync(object? sender, FormClosingEventArgs e)
    {
        if (_allowClose || !_runner.IsRunning)
        {
            _allowClose = true;
            return;
        }

        e.Cancel = true;
        if (_closePending)
        {
            return;
        }

        var spanish = _settings.Language == "es";
        var answer = MessageBox.Show(
            this,
            spanish
                ? "Hay una prueba en curso. ¿Quieres detenerla, guardar los resultados y cerrar?"
                : "A test is running. Stop it, save the results, and close?",
            "Ping Tester",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes)
        {
            return;
        }

        _closePending = true;
        Enabled = false;
        try
        {
            await _runner.StopAsync();
        }
        finally
        {
            _allowClose = true;
            Close();
        }
    }

    private static Icon LoadEmbeddedIcon(string resourceName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' is missing.");
        using var icon = new Icon(stream);
        return (Icon)icon.Clone();
    }

    private static string ReadEmbeddedText(string resourceName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' is missing.");
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
