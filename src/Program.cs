using Microsoft.Azure.CosmosRepository;

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
    public async Task<HttpResponseData> Todos([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "todos")] HttpRequestData req, FunctionContext executionContext)
    {
        return await req.OkObjectResponse(await _db.GetAsync(todo => true));
    }

    [Function("todo-find")]
    public async Task<HttpResponseData> TodosFindById([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "todos/{id:guid}")] HttpRequestData req, Guid id, FunctionContext executionContext)
    {
        if (await _db.GetAsync(id.ToString()) is Todo todo)
        {
            return await req.OkObjectResponse(todo);
        }
        return req.NotFoundResponse();
    }

    [Function("todo-list-complete")]
    public async Task<HttpResponseData> TodosComplete([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "todos/complete")] HttpRequestData req, FunctionContext executionContext)
    {
        return await req.OkObjectResponse(await _db.GetAsync(x => x.IsComplete));
    }

    [Function("todo-list-incomplete")]
    public async Task<HttpResponseData> TodosInComplete([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "todos/incomplete")] HttpRequestData req, FunctionContext executionContext)
    {
        return await req.OkObjectResponse(await _db.GetAsync(x => !x.IsComplete));
    }

    [Function("todo-post")]
    public async Task<HttpResponseData> TodosPost([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "todos")] HttpRequestData req, FunctionContext executionContext)
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
    public async Task<HttpResponseData> TodosPut([HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "todos/{id:guid}")] HttpRequestData req, Guid id, FunctionContext executionContext)
    {
        var inputTodo = await req.ReadFromJsonAsync<Todo>();

        if (!MinimalValidation.TryValidate(inputTodo, out var errors))
        {
            return await req.ValidationResponse(errors);
        }

        var todo = await _db.GetAsync(id.ToString());
        if (todo == null)
        {
            return req.NotFoundResponse();
        }

        todo.Title = inputTodo.Title;
        todo.IsComplete = inputTodo.IsComplete;

        await _db.UpdateAsync(todo);

        return req.NoContentResponse();
    }

    [Function("todo-mark-complete")]
    public async Task<HttpResponseData> TodosMarkComplete([HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "todos/{id:guid}/mark-complete")] HttpRequestData req, Guid id, FunctionContext executionContext)
    {
        if (await _db.GetAsync(id.ToString()) is Todo todo)
        {
            todo.IsComplete = true;
            await _db.UpdateAsync(todo);
            return req.NoContentResponse();
        }

        return req.NotFoundResponse();
    }

    [Function("todo-mark-incomplete")]
    public async Task<HttpResponseData> TodosMarkIncomplete([HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "todos/{id:guid}/mark-incomplete")] HttpRequestData req, Guid id, FunctionContext executionContext)
    {
        if (await _db.GetAsync(id.ToString()) is Todo todo)
        {
            todo.IsComplete = false;
            await _db.UpdateAsync(todo);
            return req.NoContentResponse();
        }

        return req.NotFoundResponse();
    }

    [Function("todo-delete")]
    public async Task<HttpResponseData> TodosDelete([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "todos/{id:guid}")] HttpRequestData req, Guid id, FunctionContext executionContext)
    {
        if (await _db.GetAsync(id.ToString()) is Todo todo)
        {
            await _db.DeleteAsync(todo);
            return req.NoContentResponse();
        }

        return req.NotFoundResponse();
    }

    [Function("todo-delete-all")]
    public async Task<HttpResponseData> TodosDeleteAll([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "todos/delete-all")] HttpRequestData req, FunctionContext executionContext)
    {
        throw new NotImplementedException("not implemented yet");
        //return req.NoContentResponse();
    }
}

public class Todo : Item
{
    //public int ExternalId { get; set; }

    [Required]
    public string Title { get; set; }

    public bool IsComplete { get; set; }

    //protected override string GetPartitionKeyValue() => ExternalId.ToString();
}