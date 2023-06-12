using LgpDdns;

var host = Host.CreateDefaultBuilder(args)
    .UseSystemd()
    .ConfigureServices((builderContext, services) =>
    {
        _ = services
        .AddHostedService<Worker>()
        .Configure<DdnsSettings>(builderContext.Configuration.GetSection(nameof(DdnsSettings)))
        .AddHttpClient();

    })
    .Build();

await host.RunAsync();
