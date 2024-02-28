using System.Text.Json;
using Napoleon.Server.RequestReply;
using Napoleon.Server.SharedData;

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

namespace Napoleon.Tests
{

    public class RequestReplyTests
    {
        [Test]
        public async Task Client_writes_and_reads_data_with_tcp()
        {
            var dataStore = new DataStore();

            using var dataServer = new DataServer(dataStore);


            dataServer.Start(48455);


            await Task.Delay(100);
                

            using var dataClient = new DataClient("localhost", 48455);
            dataClient.Connect();

            // put a new value in the data store
            await dataClient.PutValue("col1", "key1", JsonSerializer.SerializeToNode("value01")!, CancellationToken.None);

            // check it is in the data store
            var fromDataStore = dataStore.TryGetValue("col1", "key1");
            Assert.That(fromDataStore.ToString(), Is.EqualTo("value01"));

            // get from the data store
            var reloaded = await dataClient.GetValue("col1", "key1", CancellationToken.None);
            Assert.That(reloaded.ToString(), Is.EqualTo("value01"));

            // delete value
            var wasDeleted = await dataClient.DeleteValue("col1", "key1", CancellationToken.None);
            Assert.That(wasDeleted.ValueKind, Is.EqualTo(JsonValueKind.True));

            // delete again will return false
            wasDeleted = await dataClient.DeleteValue("col1", "key1", CancellationToken.None);
            Assert.That(wasDeleted.ValueKind, Is.EqualTo(JsonValueKind.False));

            // trying to read a deleted value returns undefined
            reloaded = await dataClient.GetValue("col1", "key1", CancellationToken.None);
            Assert.That(reloaded.ValueKind, Is.EqualTo(JsonValueKind.Undefined));


            await Task.Delay(100);

        }

        [Test]
        public async Task Simulate_exception_on_the_server()
        {
            var dataStore = new DataStore();

            using var dataServer = new DataServer(dataStore);
            
            dataServer.Start(48455);
            await Task.Delay(100);
                

            using var dataClient = new DataClient("localhost", 48455);
            dataClient.Connect();


            try
            {
                await dataClient.SimulateException(CancellationToken.None);

                Assert.Fail("An exception should have been raised");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                
            }

            
        }
    }
}
