using Microsoft.EntityFrameworkCore;
using WaveProcessor.Data;
using WaveProcessor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient<WaveApiService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Wave:BaseUrl"] ?? "https://api.wave.com/v1/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddSingleton<WaveApiService>(sp =>
{
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(WaveApiService));
    var logger = sp.GetRequiredService<ILogger<WaveApiService>>();
    var config = sp.GetRequiredService<IConfiguration>();
    return new WaveApiService(http, logger, config);
});

builder.Services.AddHostedService<TransactionProcessorWorker>();

var app = builder.Build();

app.MapControllers();

app.Run();
