using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace BambuToElegooService;

public class ElegooClient
{
    private readonly string _baseUrl;
    private readonly HttpClient _httpClient;
    private readonly PrinterApiType _apiType;
    private readonly bool _quiet;

    private enum PrinterApiType
    {
        OctoPrint,
        Moonraker,
        DirectHttp
    }

    private ElegooClient(string ipAddress, int port, PrinterApiType apiType, bool quiet)
    {
        _baseUrl = $"http://{ipAddress}:{port}";
        _apiType = apiType;
        _quiet = quiet;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
    }

    public static async Task<ElegooClient?> CreateAsync(string ipAddress, bool quiet = false)
    {
        var configurations = new[]
        {
            (Port: 80, Type: PrinterApiType.DirectHttp, TestPath: "/")
        };

        using var testClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };

        foreach (var config in configurations)
        {
            try
            {
                var url = $"http://{ipAddress}:{config.Port}{config.TestPath}";
                if (!quiet)
                    Console.Write($"  Trying port {config.Port} ({config.Type})... ");

                var response = await testClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();

                    if (content.Contains("\"api\":") || content.Contains("\"server\":"))
                    {
                        if (!quiet)
                            Console.WriteLine("✓ OctoPrint API detected!");
                        return new ElegooClient(ipAddress, config.Port, PrinterApiType.OctoPrint, quiet);
                    }
                    else if (content.Contains("moonraker") || content.Contains("klippy"))
                    {
                        if (!quiet)
                            Console.WriteLine("✓ Moonraker API detected!");
                        return new ElegooClient(ipAddress, config.Port, PrinterApiType.Moonraker, quiet);
                    }
                    else
                    {
                        if (!quiet)
                            Console.WriteLine("✓ Web interface detected");
                        return new ElegooClient(ipAddress, config.Port, PrinterApiType.DirectHttp, quiet);
                    }
                }
                else
                {
                    if (!quiet)
                        Console.WriteLine($"✗ HTTP {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                if (!quiet)
                    Console.WriteLine($"✗ {ex.Message.Split('\n')[0]}");
            }
        }

        return null;
    }

    public async Task<bool> UploadAndPrintAsync(string filePath, string fileName)
    {
        try
        {
            Log($"Uploading {fileName} to Elegoo printer...");

            bool uploadSuccess = _apiType switch
            {
                PrinterApiType.Moonraker => await UploadMoonrakerAsync(filePath, fileName),
                PrinterApiType.OctoPrint => await UploadOctoPrintAsync(filePath, fileName),
                PrinterApiType.DirectHttp => await UploadDirectAsync(filePath, fileName),
                _ => false
            };

            if (uploadSuccess)
            {
                Log($"✓ File uploaded to Elegoo: {fileName}");
                await Task.Delay(1000);

                var printStarted = await StartPrintAsync(fileName);
                if (printStarted)
                {
                    Log($"✓ Print started on Elegoo: {fileName}");
                    return true;
                }
                else
                {
                    Log($"⚠ File uploaded but could not auto-start print.");
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            Log($"✗ Error communicating with Elegoo: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> UploadDirectAsync(string filePath, string fileName)
    {
        try
        {
            var fileBytes = await File.ReadAllBytesAsync(filePath);

            Log($"  Using Elegoo endpoint: /uploadFile/upload");

            using var md5 = System.Security.Cryptography.MD5.Create();
            var hashBytes = md5.ComputeHash(fileBytes);
            var md5Hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

            Log($"    File size: {fileBytes.Length} bytes");
            Log($"    MD5 hash: {md5Hash}");

            using var content = new MultipartFormDataContent();

            content.Add(new StringContent("1"), "Check");
            content.Add(new StringContent(md5Hash), "S-File-MD5");
            content.Add(new StringContent("0"), "Offset");
            content.Add(new StringContent(Guid.NewGuid().ToString()), "Uuid");
            content.Add(new StringContent(fileBytes.Length.ToString()), "TotalSize");

            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            content.Add(fileContent, "File", fileName);

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "ElegooLink/1.0.1");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/uploadFile/upload", content);

            Log($"    Upload Response: {response.StatusCode}");
            var responseBody = await response.Content.ReadAsStringAsync();
            Log($"    Response body: {responseBody}");

            if (response.IsSuccessStatusCode)
            {
                Log($"  ✓ Upload successful!");
                return true;
            }
            else
            {
                Log($"  ✗ Upload failed");
                return false;
            }
        }
        catch (Exception ex)
        {
            Log($"  Upload error: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> UploadMoonrakerAsync(string filePath, string fileName)
    {
        try
        {
            var fileBytes = await File.ReadAllBytesAsync(filePath);
            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(fileBytes);
            content.Add(fileContent, "file", fileName);

            var response = await _httpClient.PostAsync($"{_baseUrl}/server/files/upload", content);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> UploadOctoPrintAsync(string filePath, string fileName)
    {
        try
        {
            var fileBytes = await File.ReadAllBytesAsync(filePath);
            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            content.Add(fileContent, "file", fileName);

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/files/local", content);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> StartPrintAsync(string fileName)
    {
        try
        {
            Log($"  Attempting to auto-start print via WebSocket...");

            var printerIp = _baseUrl.Replace("http://", "").Split(':')[0];
            var wsUri = new Uri($"ws://{printerIp}:3030/websocket");

            using var ws = new ClientWebSocket();
            await ws.ConnectAsync(wsUri, CancellationToken.None);

            Log($"    Connected to {wsUri}");

            var mainboardId = "243a17950107625d00004c0000000000";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));

            var printStartedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var listenTask = Task.Run(async () =>
            {
                try
                {
                    while (ws.State == WebSocketState.Open && !cts.IsCancellationRequested)
                    {
                        var msg = await ReceiveFullTextMessageAsync(ws, cts.Token);
                        if (msg == null) break;

                        Log(msg.Length <= 400 ? $"    <- {msg}" : $"    <- {msg.Substring(0, 400)}...");

                        if (CheckForStartPrintAck(msg, out var ackValue))
                        {
                            if (ackValue == 0)
                            {
                                Log($"    [WS] Received ACK for START_PRINT");
                                printStartedTcs.TrySetResult(true);
                            }
                            else
                            {
                                Log($"    [WS] Received ACK with error (Ack={ackValue})");
                                printStartedTcs.TrySetResult(false);
                            }
                        }
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Log($"    [WS] Listener error: {ex.Message}");
                }
            }, CancellationToken.None);

            await SendCcCommandAsync(ws, mainboardId, cmd: 1, data: new { }, cts.Token);
            await Task.Delay(250, cts.Token);

            for (var i = 0; i < 3; i++)
            {
                await SendCcCommandAsync(ws, mainboardId, cmd: 0, data: new { }, cts.Token);
                await Task.Delay(500, cts.Token);
            }

            await Task.Delay(3000, cts.Token);

            await SendCcCommandAsync(ws, mainboardId, cmd: 128, data: new
            {
                Filename = $"/local/{fileName}",
                StartLayer = 0,
                Calibration_switch = 0,
                PrintPlatformType = 0,
                Tlp_Switch = 0
            }, cts.Token);

            Log("    Command sent, waiting for ACK response...");

            var completed = await Task.WhenAny(printStartedTcs.Task, Task.Delay(10000, cts.Token));

            try
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
            }
            catch { }

            return completed == printStartedTcs.Task && printStartedTcs.Task.Result;
        }
        catch (Exception ex)
        {
            Log($"  Error starting print: {ex.Message}");
            return false;
        }
    }

    private static async Task<string?> ReceiveFullTextMessageAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[8192];
        using var ms = new MemoryStream();

        while (true)
        {
            var result = await ws.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close)
                return null;

            ms.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
                break;
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private async Task SendCcCommandAsync(ClientWebSocket ws, string mainboardId, int cmd, object data, CancellationToken ct)
    {
        var innerData = new
        {
            Cmd = cmd,
            Data = data,
            RequestID = Guid.NewGuid().ToString("N"),
            MainboardID = mainboardId,
            TimeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            From = 1
        };

        var msg = new
        {
            Id = "",
            Data = innerData
        };

        var json = JsonSerializer.Serialize(msg);
        Log($"    -> {json}");

        var bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
    }

    private static bool CheckForStartPrintAck(string msgJson, out int ackValue)
    {
        ackValue = -1;
        try
        {
            using var doc = JsonDocument.Parse(msgJson);

            if (doc.RootElement.TryGetProperty("Data", out var dataEl) &&
                dataEl.ValueKind == JsonValueKind.Object &&
                dataEl.TryGetProperty("Cmd", out var cmdEl) &&
                cmdEl.ValueKind == JsonValueKind.Number &&
                cmdEl.GetInt32() == 128 &&
                dataEl.TryGetProperty("Data", out var innerDataEl) &&
                innerDataEl.ValueKind == JsonValueKind.Object &&
                innerDataEl.TryGetProperty("Ack", out var ackEl) &&
                ackEl.ValueKind == JsonValueKind.Number)
            {
                ackValue = ackEl.GetInt32();
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    private void Log(string message)
    {
        if (!_quiet)
            Console.WriteLine(message);
    }
}
