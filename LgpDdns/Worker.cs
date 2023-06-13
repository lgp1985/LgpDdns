using Microsoft.Extensions.Hosting.Systemd;
using Microsoft.Extensions.Options;
using System.Net.NetworkInformation;
using System.Text.Json.Serialization;
using System.Web;

namespace LgpDdns;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> logger;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly DdnsSettings options;

    public Worker(ILogger<Worker> logger, IHostLifetime lifetime, IHttpClientFactory httpClientFactory, IOptions<DdnsSettings> options)
    {
        this.logger = logger;
        this.httpClientFactory = httpClientFactory;
        this.options = options.Value;
        this.logger.LogInformation("IsSystemd: {isSystemd}", lifetime.GetType() == typeof(SystemdLifetime));
        this.logger.LogInformation("IHostLifetime: {hostLifetime}", lifetime.GetType());
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;
        NetworkChange_NetworkAddressChanged(null, EventArgs.Empty);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Random.Shared.Next(1000, 5000), stoppingToken);
        }

        NetworkChange.NetworkAddressChanged -= NetworkChange_NetworkAddressChanged;
    }

    private async void NetworkChange_NetworkAddressChanged(object? sender, EventArgs e)
    {
        try
        {
            var ip = NetworkInterface.GetAllNetworkInterfaces()
                .Where(s => s.OperationalStatus is OperationalStatus.Up && s.NetworkInterfaceType is not NetworkInterfaceType.Loopback)
                .Select(s => s.GetIPProperties())
                .SelectMany(s => s.UnicastAddresses)
                .First(s =>
                (OperatingSystem.IsWindows() && s.PrefixOrigin is PrefixOrigin.RouterAdvertisement && s.SuffixOrigin is SuffixOrigin.LinkLayerAddress)
                ||
                (OperatingSystem.IsLinux() && s.Address.IsIPv6LinkLocal is false && s.Address.AddressFamily is System.Net.Sockets.AddressFamily.InterNetworkV6));
            var ipStandardNotation = ip.Address.ToString();

            var uriBuilder = new UriBuilder("http://api.dynu.com/nic/update");
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query["myipv6"] = ipStandardNotation;
            query["hostname"] = options.Hostname;
            uriBuilder.Query = query.ToString();
            var httpRequestMessage = new HttpRequestMessage { RequestUri = uriBuilder.Uri };
            httpRequestMessage.Headers.Authorization = new BasicAuthenticationHeaderValue(options.UserName, options.Password);
            using var httpClient = httpClientFactory.CreateClient();
            var httpResponseMessage = await httpClient.SendAsync(httpRequestMessage);
            var content = await httpResponseMessage.Content.ReadAsStringAsync();
            logger.LogInformation("Update sent to server, response is: {responseContent}", content);
        }
        catch (InvalidOperationException)
        {
            logger.LogError("Unable to get an IP that's PrefixOrigin.RouterAdvertisement and SuffixOrigin.LinkLayerAddress: {IsNetworkAvailable}", System.Text.Json.JsonSerializer.Serialize(NetworkInterface.GetAllNetworkInterfaces()
                .Select(s => new { s.Name, s.Description, s.OperationalStatus, UnicastAddresses = s.GetIPProperties().UnicastAddresses.Select(u => u.Address.ToString()) }), new System.Text.Json.JsonSerializerOptions { Converters = { new JsonStringEnumConverter { } } }));
            //throw;
        }
    }
}