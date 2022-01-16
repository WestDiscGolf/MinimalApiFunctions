# MinimalApiFunctions

This is my take on creating a "Minimal API" using Azure Functions Isolated model using the new ASP.NET Minimal API construct ideas.

## Main Branch

The main branch holds the CosmosDB implementation using the [Azure Cosmos Repository](https://github.com/IEvangelist/azure-cosmos-dotnet-repository) package to abstract the CosmosDB SDK to make CRUD operations easier.

It contains the main CRUD operations, as per the sample, and covers the main usages. It also shows an example of a Cosmos Change Feed Trigger using the same configuration options which fires on updated to the `Todo` items.

## SQL Branch

The SQL branch is archived off as this was the original implementation of the Azure Functions Minimal API. More details can be found in my [original blog post](https://adamstorr.azurewebsites.net/blog/minimal-api-in-net6.0-out-of-process-azure-functions) about it.

## Questions

Reach out on raising an issue in this repository or send a message to me on [Twitter @WestDiscGolf](https://twitter.com/WestDiscGolf)
