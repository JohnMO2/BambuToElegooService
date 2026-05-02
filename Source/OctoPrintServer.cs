using System.Net;
using System.Text;
using System.Text.Json;

namespace BambuToElegooService;

public class OctoPrintServer
{
    private readonly HttpListener _listener;
    private readonly int _port;
    private readonly string _uploadPath;
    private readonly ElegooClient _elegooClient;
    private readonly bool _isInteractive;
    private bool _isRunning;

    public OctoPrintServer(int port, ElegooClient elegooClient, bool isInteractive = true)
    {
        _port = port;
        _listener = new HttpListener();
        _uploadPath = Path.Combine(Directory.GetCurrentDirectory(), $"uploads_{port}");
        _elegooClient = elegooClient;
        _isInteractive = isInteractive;

        Directory.CreateDirectory(_uploadPath);
    }

    public async Task StartAsync()
    {
        _listener.Prefixes.Add($"http://+:{_port}/");

        try
        {
            _listener.Start();
            _isRunning = true;
            Log($"OctoPrint server started on port {_port}");
            Log($"Upload directory: {_uploadPath}");
            Log($"Waiting for files from BambuLab...");

            _ = Task.Run(async () => await ListenAsync());
        }
        catch (Exception ex)
        {
            Log($"Error starting server: {ex.Message}");
            throw;
        }
    }

    public async Task StopAsync()
    {
        _isRunning = false;
        _listener.Stop();
        _listener.Close();
        Log("Server stopped.");
    }

    private async Task ListenAsync()
    {
        while (_isRunning)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(async () => await HandleRequestAsync(context));
            }
            catch (Exception ex) when (_isRunning)
            {
                Log($"Error receiving request: {ex.Message}");
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            Log($"[{request.HttpMethod}] {request.Url?.PathAndQuery}");

            if (request.HttpMethod == "POST" && request.Url?.AbsolutePath == "/api/files/local")
            {
                await HandleFileUpload(request, response);
            }
            else if (request.HttpMethod == "GET" && request.Url?.AbsolutePath == "/api/files")
            {
                await HandleFileList(response);
            }
            else if (request.HttpMethod == "GET" && request.Url?.AbsolutePath == "/api/version")
            {
                await SendJsonResponse(response, new
                {
                    server = "1.9.3",
                    api = "0.1",
                    text = "OctoPrint (BambuLab Bridge) 1.9.3"
                });
            }
            else if (request.HttpMethod == "GET" && request.Url?.AbsolutePath.StartsWith("/api/files/") == true)
            {
                await HandleFileRequest(request, response);
            }
            else
            {
                await HandleNotFound(response);
            }
        }
        catch (Exception ex)
        {
            Log($"Error handling request: {ex.Message}");
            try
            {
                await SendErrorResponse(response, 500, "Internal server error");
            }
            catch { }
        }
    }

    private async Task HandleFileUpload(HttpListenerRequest request, HttpListenerResponse response)
    {
        try
        {
            var boundary = GetBoundary(request.ContentType ?? "");
            if (string.IsNullOrEmpty(boundary))
            {
                await SendErrorResponse(response, 400, "Invalid content type");
                return;
            }

            var files = await ParseMultipartFormData(request.InputStream, boundary);
            var uploadedFiles = new List<object>();

            foreach (var file in files)
            {
                var fileName = SanitizeFileName(file.FileName);
                var filePath = Path.Combine(_uploadPath, fileName);

                await File.WriteAllBytesAsync(filePath, file.Content);

                Log($"✓ File received from BambuLab: {fileName} ({file.Content.Length} bytes)");

                _ = Task.Run(async () => 
                {
                    await _elegooClient.UploadAndPrintAsync(filePath, fileName);
                });

                uploadedFiles.Add(new
                {
                    name = fileName,
                    origin = "local",
                    refs = new
                    {
                        resource = $"/api/files/local/{Uri.EscapeDataString(fileName)}",
                        download = $"/downloads/files/local/{Uri.EscapeDataString(fileName)}"
                    }
                });
            }

            var uploadResponse = new
            {
                files = uploadedFiles,
                done = true
            };

            await SendJsonResponse(response, uploadResponse, 201);
        }
        catch (Exception ex)
        {
            Log($"Error uploading file: {ex.Message}");
            await SendErrorResponse(response, 500, "Upload failed");
        }
    }

    private async Task HandleFileList(HttpListenerResponse response)
    {
        var files = Directory.GetFiles(_uploadPath)
            .Select(f => new FileInfo(f))
            .Select(fi => new
            {
                name = fi.Name,
                path = fi.Name,
                type = "machinecode",
                typePath = new[] { "machinecode", "gcode" },
                origin = "local",
                refs = new
                {
                    resource = $"/api/files/local/{Uri.EscapeDataString(fi.Name)}",
                    download = $"/downloads/files/local/{Uri.EscapeDataString(fi.Name)}"
                },
                size = fi.Length,
                date = ((DateTimeOffset)fi.LastWriteTime).ToUnixTimeSeconds()
            })
            .ToList();

        var fileListResponse = new
        {
            files = files,
            free = new DriveInfo(Path.GetPathRoot(_uploadPath) ?? "C:\\").AvailableFreeSpace
        };

        await SendJsonResponse(response, fileListResponse);
    }

    private async Task HandleFileRequest(HttpListenerRequest request, HttpListenerResponse response)
    {
        await SendJsonResponse(response, new { success = true });
    }

    private async Task HandleNotFound(HttpListenerResponse response)
    {
        await SendErrorResponse(response, 404, "Not found");
    }

    private async Task SendJsonResponse(HttpListenerResponse response, object data, int statusCode = 200)
    {
        response.StatusCode = statusCode;
        response.ContentType = "application/json";

        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var buffer = Encoding.UTF8.GetBytes(json);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer);
        response.Close();
    }

    private async Task SendErrorResponse(HttpListenerResponse response, int statusCode, string message)
    {
        await SendJsonResponse(response, new { error = message }, statusCode);
    }

    private string GetBoundary(string contentType)
    {
        var elements = contentType.Split(';');
        var boundaryElement = elements.FirstOrDefault(e => e.TrimStart().StartsWith("boundary="));
        return boundaryElement?.Split('=')[1].Trim() ?? "";
    }

    private async Task<List<UploadedFile>> ParseMultipartFormData(Stream stream, string boundary)
    {
        var files = new List<UploadedFile>();
        var boundaryBytes = Encoding.UTF8.GetBytes("--" + boundary);

        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        var data = memoryStream.ToArray();

        var position = 0;
        while (position < data.Length)
        {
            var boundaryIndex = FindBytes(data, boundaryBytes, position);
            if (boundaryIndex == -1)
                break;

            position = boundaryIndex + boundaryBytes.Length;

            if (position + 2 <= data.Length && data[position] == '\r' && data[position + 1] == '\n')
                position += 2;
            else if (position + 1 <= data.Length && data[position] == '\n')
                position += 1;

            if (position + 2 <= data.Length && data[position] == '-' && data[position + 1] == '-')
                break;

            var headerEndIndex = FindBytes(data, Encoding.UTF8.GetBytes("\r\n\r\n"), position);
            if (headerEndIndex == -1)
                headerEndIndex = FindBytes(data, Encoding.UTF8.GetBytes("\n\n"), position);

            if (headerEndIndex == -1)
                break;

            var headersLength = headerEndIndex - position;
            var headersText = Encoding.UTF8.GetString(data, position, headersLength);
            var fileName = ExtractFileName(headersText);

            position = headerEndIndex;
            if (position + 4 <= data.Length && data[position] == '\r' && data[position + 1] == '\n' 
                && data[position + 2] == '\r' && data[position + 3] == '\n')
                position += 4;
            else if (position + 2 <= data.Length && data[position] == '\n' && data[position + 1] == '\n')
                position += 2;

            var nextBoundaryIndex = FindBytes(data, boundaryBytes, position);
            if (nextBoundaryIndex == -1)
                break;

            var contentLength = nextBoundaryIndex - position;

            while (contentLength > 0 && (data[position + contentLength - 1] == '\r' || data[position + contentLength - 1] == '\n'))
                contentLength--;

            if (!string.IsNullOrEmpty(fileName) && contentLength > 0)
            {
                var fileContent = new byte[contentLength];
                Array.Copy(data, position, fileContent, 0, contentLength);

                files.Add(new UploadedFile
                {
                    FileName = fileName,
                    Content = fileContent
                });
            }

            position = nextBoundaryIndex;
        }

        return files;
    }

    private int FindBytes(byte[] data, byte[] pattern, int startIndex)
    {
        for (int i = startIndex; i <= data.Length - pattern.Length; i++)
        {
            bool found = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j])
                {
                    found = false;
                    break;
                }
            }
            if (found)
                return i;
        }
        return -1;
    }

    private string? ExtractFileName(string headers)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            headers, 
            @"filename=""(.+?)""", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return match.Success ? match.Groups[1].Value : null;
    }

    private string SanitizeFileName(string fileName)
    {
        return string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));
    }

    private void Log(string message)
    {
        if (_isInteractive)
            Console.WriteLine(message);
    }

    private class UploadedFile
    {
        public string FileName { get; set; } = "";
        public byte[] Content { get; set; } = Array.Empty<byte>();
    }
}
