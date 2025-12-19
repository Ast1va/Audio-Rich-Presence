using System;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Media.Control;

internal class Program
{
    private static string? _lastOutput;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private static async Task Main()
    {
        GlobalSystemMediaTransportControlsSessionManager manager;

        try
        {
            manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        }
        catch (Exception ex)
        {
            EmitIfChanged(new
            {
                status = "Error",
                error = ex.Message
            });
            return;
        }

        while (true)
        {
            try
            {
                var session = manager.GetCurrentSession();

                if (session == null)
                {
                    EmitIfChanged(new { status = "None" });
                }
                else
                {
                    var media = await session.TryGetMediaPropertiesAsync();
                    var playback = session.GetPlaybackInfo();
                    var timeline = session.GetTimelineProperties();

                    double? positionSeconds = null;
                    double? durationSeconds = null;
                    double? progress = null;

                    try
                    {
                        var start = timeline.StartTime;
                        var end = timeline.EndTime;
                        var pos = timeline.Position;

                        durationSeconds = (end - start).TotalSeconds;
                        positionSeconds = pos.TotalSeconds;

                        if (durationSeconds > 0)
                        {
                            progress = positionSeconds / durationSeconds;
                        }
                    }
                    catch
                    {
                        // Bazı player'lar timeline vermeyebilir
                    }

                    var payload = new
                    {
                        status = playback?.PlaybackStatus.ToString() ?? "Unknown",
                        title = media?.Title ?? string.Empty,
                        artist = media?.Artist ?? string.Empty,
                        album = media?.AlbumTitle ?? string.Empty,
                        source = session.SourceAppUserModelId ?? string.Empty,

                        positionSeconds,
                        durationSeconds,
                        progress
                    };

                    EmitIfChanged(payload);
                }
            }
            catch (Exception ex)
            {
                EmitIfChanged(new
                {
                    status = "Error",
                    error = ex.Message
                });
            }

            await Task.Delay(1000);
        }
    }

    private static void EmitIfChanged(object payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);

        if (json == _lastOutput)
            return;

        Console.WriteLine(json);
        Console.Out.Flush();
        _lastOutput = json;
    }
}
