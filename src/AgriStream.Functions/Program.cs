// AgriStream IoT - Serverless data pipeline
// Author: Genius Mhirizhonga (ST10541838)
// Module: CLDV7112 - Cloud Development B

using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .Build();

host.Run();
