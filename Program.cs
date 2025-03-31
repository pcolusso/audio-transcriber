using Microsoft.Extensions.Configuration;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;
using Whisper.net;
using Whisper.net.Ggml;

// Load config, and file tracker.
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var localPath = Directory.GetCurrentDirectory();
var srcDir = config["srcDir"] ?? Path.Combine(localPath, "src");
var outDir = config["outDir"] ?? Path.Combine(localPath, "out");
var logPath = config["logPath"] ?? Path.Combine(localPath, "log");
var modelPath = config["modelPath"] ?? Path.Combine(localPath, "whisper-med-en-ggml.bin");
var ffmpegPath = config["ffmpegDir"] ?? Path.Combine(localPath, "ffmpeg");

Directory.CreateDirectory(outDir);
Directory.CreateDirectory(ffmpegPath);

Console.WriteLine($"Will read files in '{srcDir}', and place transcriptions in '{outDir}'.\nLoading whisper from '{modelPath}', ffmpeg from '{ffmpegPath}' and tracking in '{logPath}'");

var processed = new ProcessedFileTracker(logPath);

Console.WriteLine("Attempting to load Whisper & FFMpeg. If there's no internet connection, place files in locations according to configuration.\nPaths can be configured in appsettings.json, alongside the binary.");
// Set up dependencies, may need to download some artifacts.
FFmpeg.SetExecutablesPath(ffmpegPath);
await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official);
if (!File.Exists(modelPath))
{
    await DownloadModel(modelPath, GgmlType.MediumEn);
}
var whisperFactory = WhisperFactory.FromPath(modelPath);
Console.WriteLine("Confiugration complete.");

// Find all files not yet processed.
// Considering how heavy whisper is, there's not much sense paralellizing this.
foreach (var filePath in Directory.EnumerateFiles(srcDir))
{
    var fileName = Path.GetFileName(filePath).Trim();
    // TODO: Skip not mp4
    if (!processed.HasBeenProcessed(fileName))
    {
        Console.WriteLine($"File {fileName} hasn't been processed, transcribing...");
        var outputPath = Path.ChangeExtension(Path.Combine(outDir, fileName), ".txt");
        var outputFile = new StreamWriter(outputPath);
        var toProcess = await ConvertFile(filePath);
        Console.WriteLine($"Created WAV in '{toProcess}'");
        using var fileStream = File.OpenRead(toProcess);
        using var processor = whisperFactory.CreateBuilder()
            .Build();
        await foreach(var transcription in processor.ProcessAsync(fileStream))
        {
            var text = $"{transcription.Start}->{transcription.End}: {transcription.Text}";
            await outputFile.WriteAsync(text);
            Console.WriteLine(text);
        }
        processed.MarkProcessed(fileName);
        Console.WriteLine("---");
    }
}

async Task<string> ConvertFile(string inputPath)
{
    var outPath = Path.ChangeExtension(inputPath, ".wav");
    // Clear if already exists
    File.Delete(outPath);
    /*
    IMediaInfo info = await FFmpeg.GetMediaInfo(inputPath);
    IAudioStream audioStream = info.AudioStreams.FirstOrDefault()!
        .SetCodec(AudioCodec.pcm_s16le)
        .SetSampleRate(16000);
    var conversion = FFmpeg.Conversions.New()
        .AddStream(audioStream)
        .SetOutput(outPath);
    */

    var conversion = await FFmpeg.Conversions.FromSnippet.ExtractAudio(inputPath, outPath);
    conversion.AddParameter("-ar 16000");
    conversion.AddParameter("-acodec pcm_s16le");
    var res = await conversion.Start();
    if (!File.Exists(outPath))
    {
        throw new Exception("Conversion failed?");
    }
    return outPath;
}

async Task DownloadModel(string fileName, GgmlType ggmlType)
{
    Console.WriteLine($"Downloading Model {fileName}");
    using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(ggmlType);
    using var fileWriter = File.OpenWrite(fileName);
    await modelStream.CopyToAsync(fileWriter);
}
