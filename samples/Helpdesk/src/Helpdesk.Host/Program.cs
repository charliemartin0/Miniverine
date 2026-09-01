using Microsoft.Extensions.Hosting;
using MiniVerine;

var builder = Host.CreateApplicationBuilder(args);
builder.UseMiniVerine();

var host = builder.Build();
await host.StartAsync();
await host.StopAsync();
