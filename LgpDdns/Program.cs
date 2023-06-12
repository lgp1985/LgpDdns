using LgpDdns;

var host = Host.CreateDefaultBuilder(args)
    .UseSystemd()
    .ConfigureServices(services =>
    {
        _ = services.AddHostedService<Worker>()
        .AddHttpClient();
    })
    .Build();

await host.RunAsync();
