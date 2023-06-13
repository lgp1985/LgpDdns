using LgpDdns;

var host = Host.CreateDefaultBuilder(args)
    .UseSystemd()
    .UseWindowsService(options =>
    {
        options.ServiceName = "LgpDdns";
    })
    .ConfigureServices((builderContext, services) =>
    {
        _ = services
        .AddHostedService<Worker>()
        .Configure<DdnsSettings>(builderContext.Configuration.GetSection(nameof(DdnsSettings)))
        .AddHttpClient();

    })
    .Build();

await host.RunAsync();
