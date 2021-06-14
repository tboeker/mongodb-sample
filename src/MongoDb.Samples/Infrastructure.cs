using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using MongoDB.Driver.Core.Configuration;
using MongoDB.Driver.Core.Events;

namespace MongoDb.Samples
{
    public static class MongoDbServiceCollectionExtensions
    {
        public static void ConfigureMongoDb(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<MongoDbOptions>(configuration.GetSection("MongoDb"))
                .AddSingleton<MongoDbHostedService>()
                // .AddSingleton(p => p.GetRequiredService<MongoDbHostedService>().Client)
                // .AddSingleton(p => p.GetRequiredService<MongoDbHostedService>().Database)
                .AddSingleton<IMongoDbConnectionProvider>(p => p.GetRequiredService<MongoDbHostedService>())
                .AddSingleton<IHostedService>(p => p.GetRequiredService<MongoDbHostedService>());

            // services.Configure<MongoDbOptions>(configuration.GetSection("MongoDb"))
            //     .AddSingleton<MongoDbHostedService2>()
            //     .AddSingleton(p => p.GetRequiredService<MongoDbHostedService2>().Client)
            //     .AddSingleton(p => p.GetRequiredService<MongoDbHostedService2>().Database)
            //     .AddSingleton<IHostedService>(p => p.GetRequiredService<MongoDbHostedService2>());
        }
    }

    public interface IMongoDbConnectionProvider
    {
        IMongoClient ReadClient { get; }

        IMongoDatabase ReadDatabase { get; }

        IMongoClient WriteClient { get; }

        IMongoDatabase WriteDatabase { get; }

        MongoDbOptions Options { get; }

        // string GetEntityTypeKey<T>() where T : new();
        //
        IMongoCollection<T> GetWriteCollection<T>() where T : new();

        IMongoCollection<T> GetReadCollection<T>() where T : new();
    }

    public class MongoDbHostedService : IHostedService, IMongoDbConnectionProvider
    {
        private readonly ILogger<MongoDbHostedService> _logger;

        public MongoDbHostedService(ILogger<MongoDbHostedService> logger, IOptions<MongoDbOptions> options)
        {
            _logger = logger;
            _logger.LogInformation("MongoDbHostedService ctor");

            var serializer = new DateTimeSerializer(DateTimeKind.Utc, BsonType.Document);
            BsonSerializer.RegisterSerializer(typeof(DateTime), serializer);

            Options = options.Value;

            ReadClient = CreateClient("query");
            ReadDatabase = ReadClient.GetDatabase(Options.DatabaseId);

            WriteClient = CreateClient("insert");
            WriteDatabase = WriteClient.GetDatabase(Options.DatabaseId);
        }


        public IMongoClient ReadClient { get; set; }
        public IMongoDatabase ReadDatabase { get; }
        public IMongoClient WriteClient { get; }
        public IMongoDatabase WriteDatabase { get; }

        public MongoDbOptions Options { get; }


        private readonly ConcurrentDictionary<string, string> _entityTypeKeyCache =
            new ConcurrentDictionary<string, string>();

        private string GetEntityTypeKey<T>() where T : new()
        {
            var typeName = typeof(T).FullName;

            if (_entityTypeKeyCache.TryGetValue(typeName ?? throw new InvalidOperationException(), out var v))
            {
                return v;
            }

            //var t = Activator.CreateInstance<T>();
            var n = typeof(T).Name;
            _entityTypeKeyCache.TryAdd(typeName, n);
            return n;
        }

        //
        public IMongoCollection<T> GetWriteCollection<T>() where T : new()
        {
            var name = GetEntityTypeKey<T>();
            _logger.LogInformation("Creating new Write Collection: {CollectionName}", name);
            return WriteDatabase.GetCollection<T>(name);
        }

        public IMongoCollection<T> GetReadCollection<T>() where T : new()
        {
            var name = GetEntityTypeKey<T>();
            _logger.LogInformation("Creating new Read Collection: {CollectionName}", name);
            return ReadDatabase.GetCollection<T>(name);
        }


        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("StartAsync MongoDbHostedService");
            _logger.LogInformation("Connecting to MongoDb with {@Options}", Options);

            await CheckDatabaseExistsAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("StartAsync MongoDbHostedService Finished");
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public async Task<bool> CheckDatabaseExistsAsync(CancellationToken cancellationToken)
        {
            _logger.LogTrace(nameof(CheckDatabaseExistsAsync));

            var databases = await CreateClient("check").ListDatabasesAsync(cancellationToken).ConfigureAwait(false);

            while (await databases.MoveNextAsync(cancellationToken).ConfigureAwait(false))
            {
                var items = databases.Current.ToArray();
                var c = items.Count(x => x["name"] == Options.DatabaseId) > 0;

                if (c)
                {
                    return true;
                }
            }

            return false;
        }

        private IMongoClient CreateClient(string name)
        {
            var constr = string.Concat(Options.ConnectionString, Options.ConnectionStringOptions);

            var settings = MongoClientSettings.FromConnectionString(constr);


            if (!string.IsNullOrEmpty(Options.ApplicationName))
            {
                settings.ApplicationName = string.Concat(Options.ApplicationName, " - ", name);
            }
            else
            {
                settings.ApplicationName = name;
            }

            if (Options.DebugLog)
            {
                settings.ClusterConfigurator = builder =>
                {
                    Options.ClusterConfigurator?.Invoke(builder);
                    builder.Subscribe(new DebugLogSubscriber(_logger));
                };
            }
            else if (Options.ClusterConfigurator != null)
            {
                settings.ClusterConfigurator = Options.ClusterConfigurator;
            }


            var client = new MongoClient(settings);
            return client;
        }
    }

    internal class DebugLogSubscriber : IEventSubscriber
    {
        private readonly IEventSubscriber _subscriber;

        private readonly ILogger<MongoDbHostedService> _logger;

        public DebugLogSubscriber(ILogger<MongoDbHostedService> logger)
        {
            _logger = logger;
            _subscriber = new ReflectionEventSubscriber(this);
        }

        public bool TryGetEventHandler<TEvent>(out Action<TEvent> handler)
        {
            return _subscriber.TryGetEventHandler(out handler);
        }

        public void Handle(ConnectionOpenedEvent @event)
        {
            _logger.LogDebug("Opened a connection");
        }

        public void Handle(ConnectionPoolAddedConnectionEvent @event)
        {
            _logger.LogDebug("Added a connection to the pool.");
        }

        public void Handle(ConnectionPoolRemovedConnectionEvent @event)
        {
            _logger.LogDebug("Removed a connection from the pool.");
        }
    }

    public class MongoDbOptions
    {
        [JsonIgnore]
        public Action<ClusterBuilder> ClusterConfigurator { get; set; }

        public string DatabaseId { get; set; } = "db1";

        public int? FindBatchSize { get; set; }

        public int? FindLimit { get; set; }

        public string ConnectionString { get; set; } = "mongodb://localhost:27017";

        public string ConnectionStringOptions { get; set; }

        public bool UseTelemetry { get; set; }
        public string ApplicationName { get; set; }
        public bool DebugLog { get; set; }

        //public override string ToString()
        //{
        //    return $"Server: {Server} // Port: {Port} // DatabaseId: {DatabaseId} // UserName: {UserName} ";
        //}
    }
}