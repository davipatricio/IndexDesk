using IndexDesk.BuildingBlocks.Observability;
using IndexDesk.Worker.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quartz;

var builder = Host.CreateApplicationBuilder(args);

// Observability
builder.Services.AddIndexDeskObservability(builder.Configuration, "IndexDesk.Worker");

// Quartz.NET Schedulers
builder.Services.AddQuartz(q =>
{
    var bcbJobKey = new JobKey("BcbSyncJob", "MarketDataIngest");
    q.AddJob<BcbSyncJob>(opts => opts.WithIdentity(bcbJobKey));

    // Scheduled daily at 23:00 UTC
    q.AddTrigger(opts =>
        opts.ForJob(bcbJobKey)
            .WithIdentity("BcbSyncTrigger", "MarketDataIngest")
            .WithCronSchedule("0 0 23 ? * * *")
    );
});

builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

var host = builder.Build();
host.Run();
