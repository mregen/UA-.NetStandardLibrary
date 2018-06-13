namespace Opc.Ua.Gds.Server.Database.CosmosDB
{
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using System;
    using System.Threading.Tasks;

    public class DocumentDBRepository
    {
        // TODO: config db and account
        private readonly string Endpoint = "https://iopgds.documents.azure.com:443/";
        private readonly string Key = "SwkTbAqYb8ZeNryDqLKKBM1DVSrcJq6TFwyMy3QuHkiqbuGUvS4mTC6jpwZ2Tcb7PBW5dzb9eEoznEkP3WBKVg==";

        public DocumentClient Client { get; }
        public readonly string DatabaseId = "GDS";

        public DocumentDBRepository()
        {
            this.Client = new DocumentClient(new Uri(Endpoint), Key);
            CreateDatabaseIfNotExistsAsync().Wait();
        }

        private async Task CreateDatabaseIfNotExistsAsync()
        {
            try
            {
                await Client.ReadDatabaseAsync(UriFactory.CreateDatabaseUri(DatabaseId));
            }
            catch (DocumentClientException e)
            {
                if (e.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    await Client.CreateDatabaseAsync(new Database { Id = DatabaseId });
                }
                else
                {
                    throw;
                }
            }
        }
    }
}