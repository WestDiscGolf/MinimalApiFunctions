var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        // loads out the configuration automatically
        services.AddCosmosRepository();
    })
    .Build();

await host.RunAsync();

public class Functions
{
    private readonly IRepository<Todo> _db;

    public Functions(IRepository<Todo> db)
    {
        _db = db;
    }

    [Function("hello-text")]
    public async Task<HttpResponseData> Root([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "text")] HttpRequestData req)
    {
        return await req.OkResponse("Hello World!");
    }

    [Function("hello-json")]
    public async Task<HttpResponseData> HelloJson([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "hello")] HttpRequestData req)
    {
        return await req.OkObjectResponse(new { Hello = "World" });
    }

    [Function("todo-list")]
    public async Task<HttpResponseData> Todos([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "todos")] HttpRequestData req)
    {
        var items = await _db.GetAsync(x => x.Id != null);
        return await req.OkObjectResponse(items.OrderBy(x => x.IsComplete));
    }

    [Function("todo-find")]
    public async Task<HttpResponseData> TodosFindById([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "todos/{id:guid}")] HttpRequestData req, Guid id)
    {
        if (await _db.GetAsync(id.ToString()) is Todo todo)
        {
            return await req.OkObjectResponse(todo);
        }
        return req.NotFoundResponse();
    }

    [Function("todo-list-complete")]
    public async Task<HttpResponseData> TodosComplete([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "todos/complete")] HttpRequestData req)
    {
        return await req.OkObjectResponse(await _db.GetAsync(x => x.IsComplete));
    }

    [Function("todo-list-incomplete")]
    public async Task<HttpResponseData> TodosInComplete([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "todos/incomplete")] HttpRequestData req)
    {
        return await req.OkObjectResponse(await _db.GetAsync(x => !x.IsComplete));
    }

    [Function("todo-post")]
    public async Task<HttpResponseData> TodosPost([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "todos")] HttpRequestData req)
    {
        var todo = await req.ReadFromJsonAsync<Todo>();

        if (!MinimalValidation.TryValidate(todo, out var errors))
        {
            return await req.ValidationResponse(errors);
        }

        await _db.CreateAsync(todo);
        return await req.CreatedAtResponse(nameof(TodosFindById), new { id = todo.Id }, todo);
    }

    [Function("todo-put")]
    public async Task<HttpResponseData> TodosPut([HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "todos/{id:guid}")] HttpRequestData req, Guid id)
    {
        var inputTodo = await req.ReadFromJsonAsync<Todo>();

        if (!MinimalValidation.TryValidate(inputTodo, out var errors))
        {
            return await req.ValidationResponse(errors);
        }

        if (await _db.ExistsAsync(id.ToString()))
        {
            await _db.UpdateAsync(id.ToString(),
                builder =>
                {
                    builder
                        .Replace(t => t.Title, inputTodo.Title)
                        .Replace(t => t.IsComplete, inputTodo.IsComplete);
                });
        }

        return req.NoContentResponse();
    }

    [Function("todo-mark-complete")]
    public async Task<HttpResponseData> TodosMarkComplete([HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "todos/{id:guid}/mark-complete")] HttpRequestData req, Guid id)
    {
        await _db.UpdateAsync(id.ToString(),
            builder => builder.Replace(todo => todo.IsComplete, true));

        return req.NoContentResponse();
    }

    [Function("todo-mark-incomplete")]
    public async Task<HttpResponseData> TodosMarkIncomplete([HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "todos/{id:guid}/mark-incomplete")] HttpRequestData req, Guid id)
    {
        await _db.UpdateAsync(id.ToString(),
            builder => builder.Replace(todo => todo.IsComplete, false));

        return req.NoContentResponse();
    }

    [Function("todo-delete")]
    public async Task<HttpResponseData> TodosDelete([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "todos/{id:guid}")] HttpRequestData req, Guid id)
    {
        await _db.DeleteAsync(id.ToString());

        return req.NoContentResponse();
    }

    [Function("todo-delete-all")]
    public async Task<HttpResponseData> TodosDeleteAll([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "todos/delete-all")] HttpRequestData req)
    {
        var todos = await _db.GetAsync(x => x.Id != null);
        // no bulk delete options so have to do it one at a time
        foreach (var todo in todos)
        {
            await _db.DeleteAsync(todo);
        }

        return req.NoContentResponse();
    }

    [Function(nameof(CosmosTrigger))]
    public void CosmosTrigger([CosmosDBTrigger(
        databaseName: "%RepositoryOptions:DatabaseId%",
        containerName: "%RepositoryOptions:ContainerId%",
        Connection = "RepositoryOptions:CosmosConnectionString",
        LeaseContainerName = "leases",
        CreateLeaseContainerIfNotExists = true)] IReadOnlyList<Todo> input, FunctionContext context)
    {
        if (input is { Count: > 0 })
        {
            foreach (var doc in input)
            {
                // do something; do not save back to the same container otherwise it will fire the trigger again!
                context.GetLogger("Function.CosmosTrigger").LogInformation($"id: {doc.Id}; title: {doc.Title}");
            }
        }
    }
}

public class Todo : Item
{
    [Required]
    public string Title { get; set; }

    public bool IsComplete { get; set; }
}