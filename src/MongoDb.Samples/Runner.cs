using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Bogus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MongoDb.Samples
{
    internal class Runner : IHostedService
    {
        private readonly ActivitySource MyActivitySource = new ActivitySource("MongoDb.Samples.Runner");

        private readonly ILogger<Runner> _logger;
        private readonly IMongoDbConnectionProvider _mongoDbConnectionProvider;
        private readonly int _personsToAdd = 300000;

        public Runner(ILogger<Runner> logger, IMongoDbConnectionProvider mongoDbConnectionProvider)
        {
            _logger = logger;
            _logger.LogInformation("Runner ctor");
            _mongoDbConnectionProvider = mongoDbConnectionProvider;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("StartAsync Runner");

            await InsertPersonsToDatabaseAsync(cancellationToken);
        }

        private List<MyPerson> CreatPersons()
        {
            using var act = MyActivitySource.StartActivity(nameof(CreatPersons));
//            act.SetTag("Count", _personsToAdd);

            _logger.LogInformation(nameof(CreatPersons));
            return MyPerson.Generate(_personsToAdd);
        }

        private async Task InsertPersonsToDatabaseAsync(CancellationToken cancellationToken)
        {
            using var act = MyActivitySource.StartActivity(nameof(InsertPersonsToDatabaseAsync));

            _logger.LogInformation(nameof(InsertPersonsToDatabaseAsync));

            var persons = CreatPersons();

            var col = _mongoDbConnectionProvider.GetWriteCollection<MyPerson>();
            
            _logger.LogInformation($"{nameof(InsertPersonsToDatabaseAsync)} starting insert");
            await col.InsertManyAsync(persons, cancellationToken: cancellationToken);


            _logger.LogInformation($"{nameof(InsertPersonsToDatabaseAsync)} finished");
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}