using Medistock.UpdaterService;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "MedistockUpdater";
});

builder.Services.AddHostedService<UpdaterWorker>();

var host = builder.Build();
host.Run();
