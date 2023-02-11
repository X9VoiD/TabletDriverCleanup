using System.Net;

namespace TabletDriverCleanup;

public static class Downloader
{
    public static void Download(string url, string path)
    {
        Task.Run(async () =>
        {
            using var client = new HttpClient();
            using var response = await client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                throw new WebException($"Failed to download '{url}'");

            using var content = response.Content;

            var data = await content.ReadAsByteArrayAsync();
            File.WriteAllBytes(path, data);
        }).Wait();
    }
}